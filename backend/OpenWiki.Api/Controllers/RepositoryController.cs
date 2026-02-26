using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenWiki.Api.Data;
using OpenWiki.Api.Models;
using OpenWiki.Api.Services;
using Serilog;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenWiki.Api.Controllers
{
    [ApiController]
    [Route("api")]
    public class RepositoryController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly GitHubService _gitHubService;
        private readonly GitHubIngestService _ingestService;

        public RepositoryController(AppDbContext db, GitHubService gitHubService, GitHubIngestService ingestService)
        {
            _db = db;
            _gitHubService = gitHubService;
            _ingestService = ingestService;
        }

        [HttpGet("repo/check")]
        public async Task<IActionResult> CheckRepo([FromQuery] string owner, [FromQuery] string repo)
        {
            var fullName = $"{owner}/{repo}";
            var existing = await _db.Repositories
                .Include(r => r.Documentations)
                .ThenInclude(d => d.Sections)
                .FirstOrDefaultAsync(r => r.FullName == fullName);
            
            if (existing != null && existing.DocStatus == "completed")
            {
                return Ok(new { 
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
            var existing = await _db.Repositories
                .Include(r => r.Documentations)
                .ThenInclude(d => d.Sections)
                .FirstOrDefaultAsync(r => r.FullName == fullName);
            
            if (existing == null)
            {
                return NotFound(new { error = "Repository not indexed" });
            }

            var sections = existing.Documentations
                .SelectMany(d => d.Sections)
                .OrderBy(s => s.OrderIndex)
                .ToList();

            var relations = await _db.DocRelations
                .Where(r => r.RepositoryId == existing.Id)
                .ToListAsync();

            var diagrams = await _db.Diagrams
                .Where(d => d.RepositoryId == existing.Id)
                .ToListAsync();

            var fullDoc = string.Join("\n\n---\n\n", sections.Select(s => 
                $"{(s.Level == 1 ? "# " : "## ")}{s.Title}\n\n{s.ContentMarkdown}"
            ));

            return Ok(new { 
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
        }

        [HttpPost("ingest")]
        public async Task<IActionResult> Ingest([FromQuery] string owner, [FromQuery] string repo)
        {
            try 
            {
                var fullName = $"{owner}/{repo}";
                
                var existing = await _db.Repositories.FirstOrDefaultAsync(r => r.FullName == fullName);
                if (existing != null && existing.DocStatus == "completed")
                {
                    Log.Information("[Program.cs] Repository {FullName} already indexed, returning cached data", fullName);
                    var existingSections = await _db.DocSections
                        .Where(s => s.Documentation.RepositoryId == existing.Id)
                        .OrderBy(s => s.OrderIndex)
                        .ToListAsync();

                    var fullDoc = string.Join("\n\n---\n\n", existingSections.Select(s => 
                        $"{(s.Level == 1 ? "# " : "## ")}{s.Title}\n\n{s.ContentMarkdown}"
                    ));

                    return Ok(new { 
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

                int stars = 0;
                string language = "Unknown";
                string description = "";
                
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
                    Log.Warning(ex, "[Program.cs] GitHub metadata fetch failed (continuing without): {Message}", ex.Message);
                }

                var result = await _ingestService.ProcessRepositoryAsync(owner, repo);
                
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
                _db.Repositories.Add(repository);

                var documentation = new Documentation 
                { 
                    Repository = repository,
                    Title = $"{owner}/{repo} Documentation",
                    Status = "completed"
                };
                _db.Documentations.Add(documentation);

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
                    _db.DocSections.Add(docSection);
                }

                foreach (var diagram in result.Diagrams)
                {
                    _db.Diagrams.Add(new Diagram
                    {
                        RepositoryId = repository.Id,
                        Title = diagram.Title,
                        DiagramType = diagram.Type,
                        Content = diagram.Content
                    });
                }

                await _db.SaveChangesAsync();

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

                return Ok(new { 
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
                Log.Error(ex, "[Program.cs] Critical Ingest Error: {Message}", ex.Message);
                return Problem(ex.Message);
            }
        }
    }
}