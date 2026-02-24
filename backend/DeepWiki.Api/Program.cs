using DeepWiki.Api.Data;
using DeepWiki.Api.Hubs;
using DeepWiki.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        b => b.SetIsOriginAllowed(origin => true) 
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseInMemoryDatabase("DeepWikiDb");
});

builder.Services.AddSignalR();
builder.Services.AddHttpClient<GitHubService>();
builder.Services.AddHttpClient<AiClientService>();
builder.Services.AddScoped<GitHubIngestService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));

// Check if repo is already indexed
app.MapGet("/api/repo/check", async (string owner, string repo, AppDbContext db) => 
{
    var fullName = $"{owner}/{repo}";
    var existing = await db.Repositories
        .Include(r => r.Documentations)
        .ThenInclude(d => d.Sections)
        .FirstOrDefaultAsync(r => r.FullName == fullName);
    
    if (existing != null && existing.DocStatus == "completed")
    {
        return Results.Ok(new { 
            indexed = true, 
            indexedAt = existing.IndexedAt,
            sectionsCount = existing.Documentations.SelectMany(d => d.Sections).Count()
        });
    }
    
    return Results.Ok(new { indexed = false });
});

// Get indexed repo data
app.MapGet("/api/repo/data", async (string owner, string repo, AppDbContext db) => 
{
    var fullName = $"{owner}/{repo}";
    var existing = await db.Repositories
        .Include(r => r.Documentations)
        .ThenInclude(d => d.Sections)
        .FirstOrDefaultAsync(r => r.FullName == fullName);
    
    if (existing == null)
    {
        return Results.NotFound(new { error = "Repository not indexed" });
    }

    var sections = existing.Documentations
        .SelectMany(d => d.Sections)
        .OrderBy(s => s.OrderIndex)
        .ToList();

    var relations = await db.DocRelations
        .Where(r => r.RepositoryId == existing.Id)
        .ToListAsync();

    var diagrams = await db.Diagrams
        .Where(d => d.RepositoryId == existing.Id)
        .ToListAsync();

    var fullDoc = string.Join("\n\n---\n\n", sections.Select(s => 
        $"{(s.Level == 1 ? "# " : "## ")}{s.Title}\n\n{s.ContentMarkdown}"
    ));

    return Results.Ok(new { 
        owner = existing.Owner,
        repo = existing.Name,
        document = fullDoc,
        metadata = new { 
            stars = existing.StarsCount, 
            language = existing.Language,
            description = existing.Description 
        },
        sections = sections.Select(s => new {
            id = s.Id,
            title = s.Title,
            slug = s.Slug,
            level = s.Level,
            type = s.SectionType,
            summary = s.Summary
        }),
        relations = relations.Select(r => new {
            from = sections.FirstOrDefault(s => s.Id == r.FromSectionId)?.Title,
            to = sections.FirstOrDefault(s => s.Id == r.ToSectionId)?.Title,
            type = r.RelationType,
            description = r.Description
        }),
        diagrams = diagrams.Select(d => new {
            title = d.Title,
            type = d.DiagramType,
            content = d.Content
        })
    });
});

// Search endpoint
app.MapGet("/api/search", async (string q, GitHubService gitHubService) => 
{
    try
    {
        var response = await gitHubService.SearchReposAsync(q);
        return Results.Ok(new { results = response });
    }
    catch
    {
        return Results.Ok(new { results = Array.Empty<object>() });
    }
});

// Ingest repository
app.MapPost("/api/ingest", async (string owner, string repo, GitHubIngestService ingestService, GitHubService gitHubService, AppDbContext db) => 
{
    try 
    {
        var fullName = $"{owner}/{repo}";
        
        // Check if already indexed
        var existing = await db.Repositories.FirstOrDefaultAsync(r => r.FullName == fullName);
        if (existing != null && existing.DocStatus == "completed")
        {
            Console.WriteLine($"[Program.cs] Repository {fullName} already indexed, returning cached data");
            
            var existingSections = await db.DocSections
                .Where(s => s.Documentation.RepositoryId == existing.Id)
                .OrderBy(s => s.OrderIndex)
                .ToListAsync();

            var fullDoc = string.Join("\n\n---\n\n", existingSections.Select(s => 
                $"{(s.Level == 1 ? "# " : "## ")}{s.Title}\n\n{s.ContentMarkdown}"
            ));

            return Results.Ok(new { 
                status = "cached", 
                owner, 
                repo,
                document = fullDoc,
                metadata = new { 
                    stars = existing.StarsCount, 
                    language = existing.Language,
                    description = existing.Description 
                },
                sections = existingSections.Count,
                message = "Loaded from cache (previously indexed)"
            });
        }

        // Get metadata (with graceful failure for rate limits)
        int stars = 0;
        string language = "Unknown";
        string description = "";
        
        try 
        {
            var metadataJson = await gitHubService.GetRepoMetadataAsync(owner, repo);
            var metadata = JsonDocument.Parse(metadataJson);
            
            if (metadata.RootElement.TryGetProperty("stargazers_count", out var starsEl))
                stars = starsEl.GetInt32();
            if (metadata.RootElement.TryGetProperty("language", out var langEl) && langEl.ValueKind != JsonValueKind.Null)
                language = langEl.GetString() ?? "Unknown";
            if (metadata.RootElement.TryGetProperty("description", out var descEl) && descEl.ValueKind != JsonValueKind.Null)
                description = descEl.GetString() ?? "";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Program.cs] GitHub metadata fetch failed (continuing without): {ex.Message}");
            // Continue without metadata - git clone will still work
        }

        // Process repository
        var result = await ingestService.ProcessRepositoryAsync(owner, repo);
        
        // Create repository record
        var repository = new Repository 
        { 
            Owner = owner, 
            Name = repo, 
            FullName = fullName,
            Description = description,
            StarsCount = stars,
            Language = language,
            DocStatus = "completed",
            IndexedAt = DateTime.UtcNow
        };
        db.Repositories.Add(repository);

        // Create documentation
        var documentation = new Documentation 
        { 
            Repository = repository,
            Title = $"{owner}/{repo} Documentation",
            Status = "completed"
        };
        db.Documentations.Add(documentation);

        // Save all sections
        for (int i = 0; i < result.Sections.Count; i++)
        {
            var section = result.Sections[i];
            var docSection = new DocSection
            {
                Documentation = documentation,
                Title = section.Title,
                Slug = section.Title.ToLower().Replace(" ", "-").Replace("[^a-z0-9-]", ""),
                Level = section.Level,
                OrderIndex = i,
                ContentMarkdown = section.Content,
                Summary = section.Summary,
                SectionType = section.SectionType
            };
            db.DocSections.Add(docSection);
        }

        // Save diagrams
        foreach (var diagram in result.Diagrams)
        {
            db.Diagrams.Add(new Diagram
            {
                RepositoryId = repository.Id,
                Title = diagram.Title,
                DiagramType = diagram.Type,
                Content = diagram.Content
            });
        }

        await db.SaveChangesAsync();

        // Build full document
        var fullDocOutput = result.Overview;
        if (result.Sections.Any())
        {
            fullDocOutput = string.Join("\n\n---\n\n", result.Sections.Select(s => 
                $"{(s.Level == 1 ? "# " : "## ")}{s.Title}\n\n{s.Content}"
            ));
        }

        // Add diagrams to output
        if (result.Diagrams.Any())
        {
            fullDocOutput += "\n\n---\n\n## Architecture Diagrams\n\n";
            foreach (var d in result.Diagrams)
            {
                fullDocOutput += $"### {d.Title}\n\n```{d.Type}\n{d.Content}\n```\n\n";
            }
        }

        return Results.Ok(new { 
            status = "completed", 
            owner, 
            repo,
            document = fullDocOutput,
            metadata = new { stars, language, description },
            sections = result.Sections.Count,
            relations = result.Relations.Count,
            diagrams = result.Diagrams.Count
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Program.cs] Critical Ingest Error: {ex}");
        return Results.Problem(ex.Message);
    }
});

app.MapHub<ChatHub>("/chatHub");

app.Run();
public partial class Program { }
