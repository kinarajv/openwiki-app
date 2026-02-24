using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeepWiki.Api.Services;

public class IngestResult
{
    public string Overview { get; set; } = "";
    public List<SectionResult> Sections { get; set; } = new();
    public List<RelationResult> Relations { get; set; } = new();
    public List<DiagramResult> Diagrams { get; set; } = new();
}

public class SectionResult
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Summary { get; set; } = "";
    public int Level { get; set; } = 2;
    public string SectionType { get; set; } = "content";
    public List<string> RelatedFiles { get; set; } = new();
}

public class RelationResult
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Type { get; set; } = "references";
    public string? Description { get; set; }
}

public class DiagramResult
{
    public string Title { get; set; } = "";
    public string Type { get; set; } = "mermaid";
    public string Content { get; set; } = "";
}

public class GitHubIngestService
{
    private readonly AiClientService _aiService;
    private readonly ILogger<GitHubIngestService> _logger;
    private static readonly string[] _codeExtensions = { ".cs", ".ts", ".js", ".go", ".py", ".java", ".rs", ".cpp", ".h", ".tsx", ".jsx", ".swift", ".kt", ".vue", ".rb" };

    public GitHubIngestService(AiClientService aiService, ILogger<GitHubIngestService> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<IngestResult> ProcessRepositoryAsync(string owner, string repo)
    {
        string workDir = Path.Combine(Path.GetTempPath(), "deepwiki-repos", $"{owner}-{repo}-{Guid.NewGuid():N}");
        
        try
        {
            Directory.CreateDirectory(workDir);
            
            // STEP 1: Clone
            _logger.LogInformation("📥 STEP 1: Cloning repository...");
            var cloneProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"clone --depth 1 https://github.com/{owner}/{repo}.git .",
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            await cloneProcess!.WaitForExitAsync();
            
            if (cloneProcess.ExitCode != 0)
            {
                var error = await cloneProcess.StandardError.ReadToEndAsync();
                throw new Exception($"Git clone failed: {error}");
            }
            _logger.LogInformation("✅ Repository cloned");

            // STEP 2: Map files
            _logger.LogInformation("📂 STEP 2: Reading source files...");
            var allFiles = Directory.GetFiles(workDir, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.Combine(workDir, ".git")))
                .Select(f => f.Substring(workDir.Length).TrimStart(Path.DirectorySeparatorChar))
                .ToList();

            var codeFiles = allFiles
                .Where(f => _codeExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // STEP 3: Read files
            var fileContents = new Dictionary<string, string>();
            var totalChars = 0;
            var maxContextChars = 60000;

            foreach (var file in codeFiles)
            {
                if (totalChars > maxContextChars) break;
                
                try 
                {
                    var fullPath = Path.Combine(workDir, file);
                    var content = await File.ReadAllTextAsync(fullPath);
                    
                    if (content.Length > 200 && content.Length < 30000 && 
                        !file.Contains("node_modules") && 
                        !file.Contains(".min.") &&
                        !file.Contains("generated") &&
                        !file.Contains("dist/"))
                    {
                        if (content.Length > 2000) content = content.Substring(0, 2000) + "\n... [TRUNCATED]";
                        fileContents[file] = content;
                        totalChars += content.Length;
                    }
                }
                catch { }
            }

            // Read configs
            var configFiles = allFiles.Where(f => 
                f.EndsWith("package.json") || 
                f.EndsWith("go.mod") ||
                f.EndsWith(".csproj") ||
                f.EndsWith("Cargo.toml") ||
                f.EndsWith("requirements.txt") ||
                f.EndsWith("pom.xml") ||
                f.Equals("README.md", StringComparison.OrdinalIgnoreCase) ||
                f.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)
            );

            foreach (var file in configFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(Path.Combine(workDir, file));
                    if (content.Length < 5000)
                        fileContents[file] = content;
                }
                catch { }
            }

            _logger.LogInformation($"📖 Read {fileContents.Count} files ({totalChars:N0} chars)");

            // STEP 4: Generate comprehensive documentation with AI
            _logger.LogInformation("🧠 STEP 3: Generating multi-document architecture...");
            
            var contextBuilder = new System.Text.StringBuilder();
            contextBuilder.AppendLine($"REPOSITORY: {owner}/{repo}");
            contextBuilder.AppendLine("CODE FILES:");
            
            foreach (var kvp in fileContents.Take(30))
            {
                contextBuilder.AppendLine($"\n### {kvp.Key}\n```\n{kvp.Value}\n```");
            }

            var aiResponse = await _aiService.GenerateStructuredDocsAsync(contextBuilder.ToString(), owner, repo);
            
            // STEP 5: Parse AI response
            _logger.LogInformation("📝 STEP 4: Creating document relations and diagrams...");
            
            var result = ParseAiResponse(aiResponse, fileContents.Keys.ToList());
            
            return result;
        }
        finally
        {
            try 
            {
                if (Directory.Exists(workDir))
                    Directory.Delete(workDir, true);
                _logger.LogInformation("🧹 Cleaned up");
            }
            catch { }
        }
    }

    private IngestResult ParseAiResponse(string aiResponse, List<string> allFiles)
    {
        var result = new IngestResult();
        
        try
        {
            // Try to extract JSON from response
            var jsonMatch = Regex.Match(aiResponse, @"\{[\s\S]*\}", RegexOptions.Multiline);
            if (jsonMatch.Success)
            {
                var json = JsonSerializer.Deserialize<JsonElement>(jsonMatch.Value);
                
                // Parse overview
                if (json.TryGetProperty("overview", out var overviewEl))
                {
                    result.Overview = overviewEl.GetString() ?? "";
                    result.Sections.Add(new SectionResult
                    {
                        Title = "Overview",
                        Content = result.Overview,
                        Summary = "High-level introduction to the repository",
                        Level = 1,
                        SectionType = "overview"
                    });
                }

                // Parse sections
                if (json.TryGetProperty("sections", out var sectionsEl))
                {
                    foreach (var section in sectionsEl.EnumerateArray())
                    {
                        result.Sections.Add(new SectionResult
                        {
                            Title = section.GetProperty("title").GetString() ?? "",
                            Content = section.GetProperty("content").GetString() ?? "",
                            Summary = section.TryGetProperty("summary", out var sum) ? sum.GetString() ?? "" : "",
                            Level = section.TryGetProperty("level", out var lvl) ? lvl.GetInt32() : 2,
                            SectionType = section.TryGetProperty("type", out var t) ? t.GetString() ?? "content" : "content",
                            RelatedFiles = section.TryGetProperty("files", out var files) 
                                ? files.EnumerateArray().Select(f => f.GetString() ?? "").Where(f => !string.IsNullOrEmpty(f)).ToList() 
                                : new List<string>()
                        });
                    }
                }

                // Parse relations
                if (json.TryGetProperty("relations", out var relationsEl))
                {
                    foreach (var rel in relationsEl.EnumerateArray())
                    {
                        result.Relations.Add(new RelationResult
                        {
                            From = rel.GetProperty("from").GetString() ?? "",
                            To = rel.GetProperty("to").GetString() ?? "",
                            Type = rel.TryGetProperty("type", out var t) ? t.GetString() ?? "references" : "references",
                            Description = rel.TryGetProperty("description", out var d) ? d.GetString() : null
                        });
                    }
                }

                // Parse diagrams
                if (json.TryGetProperty("diagrams", out var diagramsEl))
                {
                    foreach (var diag in diagramsEl.EnumerateArray())
                    {
                        result.Diagrams.Add(new DiagramResult
                        {
                            Title = diag.GetProperty("title").GetString() ?? "",
                            Type = diag.TryGetProperty("type", out var t) ? t.GetString() ?? "mermaid" : "mermaid",
                            Content = diag.GetProperty("content").GetString() ?? ""
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to parse AI response: {ex.Message}");
            
            // Fallback: treat entire response as overview
            result.Overview = aiResponse;
            result.Sections.Add(new SectionResult
            {
                Title = "Overview",
                Content = aiResponse,
                Level = 1,
                SectionType = "overview"
            });
        }

        // Ensure we have at least basic sections
        if (result.Sections.Count == 0)
        {
            result.Sections.Add(new SectionResult
            {
                Title = "Overview",
                Content = aiResponse,
                Level = 1,
                SectionType = "overview"
            });
        }

        return result;
    }
}
