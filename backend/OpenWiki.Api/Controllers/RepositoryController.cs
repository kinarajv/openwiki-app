using Microsoft.AspNetCore.Mvc;
using OpenWiki.Api.Models;
using OpenWiki.Api.Repositories.Interfaces;
using OpenWiki.Api.Services.Interfaces;
using System.Text.Json;

namespace OpenWiki.Api.Controllers;

[ApiController]
[Route("api")]
public class RepositoryController : ControllerBase
{
    private readonly IRepositoryRepo _repositoryRepo;
    private readonly IDocumentationRepo _documentationRepo;
    private readonly IDiagramRepo _diagramRepo;
    private readonly IGitHubService _gitHubService;
    private readonly IGitHubIngestService _ingestService;
    private readonly ILogger<RepositoryController> _logger;

    public RepositoryController(
        IRepositoryRepo repositoryRepo,
        IDocumentationRepo documentationRepo,
        IDiagramRepo diagramRepo,
        IGitHubService gitHubService,
        IGitHubIngestService ingestService,
        ILogger<RepositoryController> logger)
    {
        _repositoryRepo = repositoryRepo;
        _documentationRepo = documentationRepo;
        _diagramRepo = diagramRepo;
        _gitHubService = gitHubService;
        _ingestService = ingestService;
        _logger = logger;
    }

    [HttpGet("repo/check")]
    public async Task<IActionResult> CheckRepo([FromQuery] string owner, [FromQuery] string repo)
    {
        var fullName = $"{owner}/{repo}";
        var existing = await _repositoryRepo.GetByFullNameAsync(fullName);
        if (existing != null && existing.DocStatus == "completed")
        {
            return Ok(new
            {
                indexed = true,
                indexedAt = existing.IndexedAt,
                sectionsCount = existing.Documentations.SelectMany(d => d.Sections).Count()
            });
        }

        return Ok(new { indexed = false });
    }

    [HttpGet("repo/data")]
    public async Task<IActionResult> GetRepoData([FromQuery] string owner, [FromQuery] string repo)
    {
        var fullName = $"{owner}/{repo}";
        var existing = await _repositoryRepo.GetByFullNameAsync(fullName);
        if (existing == null)
        {
            return NotFound(new { error = "Repository not indexed" });
        }

        var sections = existing.Documentations
            .SelectMany(d => d.Sections)
            .OrderBy(s => s.OrderIndex)
            .ToList();

        var relations = await _documentationRepo.GetRelationsByRepoIdAsync(existing.Id);
        var diagrams = await _diagramRepo.GetByRepoIdAsync(existing.Id);

        var fullDoc = string.Join("\n\n---\n\n", sections.Select(s =>
            $"{(s.Level == 1 ? "# " : "## ")}{s.Title}\n\n{s.ContentMarkdown}"
        ));

        return Ok(new
        {
            owner = existing.Owner,
            repo = existing.Name,
            document = fullDoc,
            metadata = new
            {
                stars = existing.StarsCount,
                language = existing.Language,
                description = existing.Description,
                sourceUrl = existing.SourceUrl
            },
            sections = sections.Select(s => new
            {
                id = s.Id,
                title = s.Title,
                slug = s.Slug,
                level = s.Level,
                type = s.SectionType,
                summary = s.Summary,
                content = s.ContentMarkdown,
                sectionNumber = s.SectionNumber,
                sourceCitations = !string.IsNullOrEmpty(s.SourceCitationsJson)
                    ? JsonSerializer.Deserialize<string[]>(s.SourceCitationsJson) ?? Array.Empty<string>()
                    : Array.Empty<string>()
            }),
            relations = relations.Select(r => new
            {
                from = sections.FirstOrDefault(s => s.Id == r.FromSectionId)?.Title,
                to = sections.FirstOrDefault(s => s.Id == r.ToSectionId)?.Title,
                type = r.RelationType,
                description = r.Description
            }),
            diagrams = diagrams.Select(d => new
            {
                title = d.Title,
                type = d.DiagramType,
                content = d.Content
            })
        });
    }

    [HttpPost("ingest")]
    public async Task Ingest([FromQuery] string owner, [FromQuery] string repo, [FromQuery] bool force = false, [FromQuery] string? repoUrl = null, [FromQuery] string? token = null)
    {
        try
        {
            var fullName = $"{owner}/{repo}";
            var existing = await _repositoryRepo.GetByFullNameSimpleAsync(fullName);
            if (existing != null)
            {
                if (force)
                {
                    _logger.LogInformation("Force refresh requested. Deleting existing data for {FullName}", fullName);
                    await _repositoryRepo.DeleteAsync(existing);
                    await _repositoryRepo.SaveChangesAsync();
                }
                else if (existing.DocStatus == "completed")
                {
                    _logger.LogInformation("Repository {FullName} already indexed, returning cached data", fullName);
                    var existingSections = await _documentationRepo.GetSectionsByRepoIdAsync(existing.Id);

                    var fullDoc = string.Join("\n\n---\n\n", existingSections.Select(s =>
                        $"{(s.Level == 1 ? "# " : "## ")}{s.Title}\n\n{s.ContentMarkdown}"
                    ));

                    Response.ContentType = "application/json";
                    var cached = new
                    {
                        status = "cached",
                        owner,
                        repo,
                        document = fullDoc,
                        metadata = new
                        {
                            stars = existing.StarsCount,
                            language = existing.Language,
                            description = existing.Description,
                            sourceUrl = existing.SourceUrl
                        },
                        sections = existingSections.Select(s => new
                        {
                            id = s.Id,
                            title = s.Title,
                            slug = s.Slug,
                            level = s.Level,
                            type = s.SectionType,
                            summary = s.Summary,
                            content = s.ContentMarkdown,
                            sectionNumber = s.SectionNumber,
                            sourceCitations = !string.IsNullOrEmpty(s.SourceCitationsJson)
                                ? JsonSerializer.Deserialize<string[]>(s.SourceCitationsJson) ?? Array.Empty<string>()
                                : Array.Empty<string>()
                        }),
                        message = "Loaded from cache (previously indexed)"
                    };
                    await Response.WriteAsJsonAsync(cached);
                    return;
                }
            }

            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";
            await Response.Body.FlushAsync();

            async Task WriteEvent(string data)
            {
                await Response.WriteAsync($"data: {data}\n\n");
                await Response.Body.FlushAsync();
            }

            var isGitHub = string.IsNullOrEmpty(repoUrl) || 
                (Uri.TryCreate(repoUrl, UriKind.Absolute, out var parsedUri) && 
                 parsedUri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase));

            int stars = 0;
            string language = "Unknown";
            string description = "";

            if (isGitHub)
            {
                try
                {
                    var metadataJson = await _gitHubService.GetRepoMetadataAsync(owner, repo);
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
                    _logger.LogWarning(ex, "GitHub metadata fetch failed for {FullName}: {Message}", fullName, ex.Message);
                }
            }
            else
            {
                _logger.LogInformation("Non-GitHub repository, skipping GitHub API metadata fetch for {FullName}", fullName);
            }

            var result = await _ingestService.ProcessRepositoryAsync(owner, repo, repoUrl, token, async (progressMsg) =>
            {
                try { await WriteEvent(progressMsg); } catch {  }
            });

            var repository = new Repository
            {
                Owner = owner,
                Name = repo,
                FullName = fullName,
                Description = description,
                StarsCount = stars,
                Language = language,
                DocStatus = "completed",
                SourceUrl = repoUrl ?? $"https://github.com/{owner}/{repo}",
                IndexedAt = DateTime.UtcNow,
                CommitHash = result.CommitHash,
                DefaultBranch = result.DefaultBranch
            };
            await _repositoryRepo.AddAsync(repository);

            var documentation = new Documentation
            {
                Repository = repository,
                Title = $"{owner}/{repo} Documentation",
                Status = "completed"
            };
            await _documentationRepo.AddAsync(documentation);

            var levelCounters = new Dictionary<int, int>();
            for (int i = 0; i < result.Sections.Count; i++)
            {
                var section = result.Sections[i];
                
                string sectionNumber;
                if (!string.IsNullOrWhiteSpace(section.SectionNumber))
                {
                    sectionNumber = section.SectionNumber;
                }
                else
                {
                    var level = section.Level;
                    if (!levelCounters.ContainsKey(level))
                        levelCounters[level] = 0;
                    levelCounters[level]++;
                    
                    foreach (var key in levelCounters.Keys.Where(k => k > level).ToList())
                        levelCounters.Remove(key);
                    
                    if (level <= 1)
                        sectionNumber = $"{levelCounters.GetValueOrDefault(1, i + 1)}";
                    else
                    {
                        var parts = new List<string>();
                        for (int l = 1; l <= level; l++)
                            parts.Add($"{levelCounters.GetValueOrDefault(l, 1)}");
                        sectionNumber = string.Join(".", parts);
                    }
                }
                
                var docSection = new DocSection
                {
                    Documentation = documentation,
                    Title = section.Title,
                    Slug = System.Text.RegularExpressions.Regex.Replace(section.Title.ToLower().Replace(" ", "-"), "[^a-z0-9-]", ""),
                    Level = section.Level,
                    OrderIndex = i,
                    ContentMarkdown = section.Content,
                    Summary = section.Summary,
                    SectionType = section.SectionType,
                    SectionNumber = sectionNumber,
                    SourceCitationsJson = section.SourceCitations?.Any() == true ? JsonSerializer.Serialize(section.SourceCitations) : null
                };
                await _documentationRepo.AddSectionAsync(docSection);
            }

            foreach (var diagram in result.Diagrams)
            {
                await _diagramRepo.AddAsync(new Diagram
                {
                    RepositoryId = repository.Id,
                    Title = diagram.Title,
                    DiagramType = diagram.Type,
                    Content = diagram.Content
                });
            }

            await _repositoryRepo.SaveChangesAsync();

            var fullDocOutput = result.Overview;
            if (result.Sections.Any())
            {
                fullDocOutput = string.Join("\n\n---\n\n", result.Sections.Select(s =>
                    $"{(s.Level == 1 ? "# " : "## ")}{s.Title}\n\n{s.Content}"
                ));
            }

            if (result.Diagrams.Any())
            {
                fullDocOutput += "\n\n---\n\n## Architecture Diagrams\n\n";
                foreach (var d in result.Diagrams)
                {
                    fullDocOutput += $"### {d.Title}\n\n```{d.Type}\n{d.Content}\n```\n\n";
                }
            }

            var completeData = JsonSerializer.Serialize(new
            {
                type = "complete",
                status = "completed",
                owner,
                repo,
                document = fullDocOutput,
                metadata = new { stars, language, description, sourceUrl = repoUrl ?? $"https://github.com/{owner}/{repo}" },
                sections = result.Sections.Count,
                relations = result.Relations.Count,
                diagrams = result.Diagrams.Count
            });
            await WriteEvent(completeData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ingest failed for {Owner}/{Repo}: {Message}", owner, repo, ex.Message);
            
            if (!Response.HasStarted)
            {
                Response.StatusCode = 500;
                await Response.WriteAsJsonAsync(new { error = ex.Message });
            }
            else
            {
                try
                {
                    await Response.WriteAsync($"data: {{\"type\":\"error\",\"message\":\"{ex.Message.Replace("\"", "\\\"")}\"}}\n\n");
                    await Response.Body.FlushAsync();
                }
                catch {  }
            }
        }
}
}
