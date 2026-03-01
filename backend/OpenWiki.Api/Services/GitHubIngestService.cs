using OpenWiki.Api.Repositories.Interfaces;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenWiki.Api.Models;
using OpenWiki.Api.Services.Interfaces;

namespace OpenWiki.Api.Services;

public class GitHubIngestService : IGitHubIngestService
{
    private readonly IAiClientService _aiService;
    private readonly IRepositoryRepo _repositoryRepo;
    private readonly IDocumentationRepo _documentationRepo;
    private readonly IDiagramRepo _diagramRepo;
    private readonly ILogger<GitHubIngestService> _logger;
    private static readonly string[] _codeExtensions = { ".cs", ".ts", ".js", ".go", ".py", ".java", ".rs", ".cpp", ".h", ".tsx", ".jsx", ".swift", ".kt", ".vue", ".rb" };

    public GitHubIngestService(IAiClientService aiService, IRepositoryRepo repositoryRepo, IDocumentationRepo documentationRepo, IDiagramRepo diagramRepo, ILogger<GitHubIngestService> logger)
    {
        _aiService = aiService;
        _repositoryRepo = repositoryRepo;
        _documentationRepo = documentationRepo;
        _diagramRepo = diagramRepo;
        _logger = logger;
    }
    public async Task<IngestResult> ProcessRepositoryAsync(string owner, string repo)
    {
        string workDir = Path.Combine(Path.GetTempPath(), "deepwiki-repos", $"{owner}-{repo}-{Guid.NewGuid():N}");
        string commitHash = "";
        string defaultBranch = "main";
        try
        {
            Directory.CreateDirectory(workDir);
            
            // STEP 1: Clone
            _logger.LogInformation(" STEP 1: Cloning repository...");
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
                // Check if clone partially succeeded (e.g., long filename errors on Windows)
                var hasFiles = Directory.GetFiles(workDir, "*.*", SearchOption.TopDirectoryOnly).Length > 0
                    || Directory.GetDirectories(workDir).Any(d => !d.EndsWith(".git"));
                
                if (hasFiles)
                {
                    _logger.LogWarning($"Git clone had errors ({cloneProcess.ExitCode}) but files exist, continuing: {error.Substring(0, Math.Min(error.Length, 200))}");
                }
                else
                {
                    _logger.LogWarning($"Git clone failed ({cloneProcess.ExitCode}): {error}. Falling back to zip download via GitHub API...");

                    using var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("OpenWiki-App");

                    // Get default branch and commit hash
                    var repoInfoUrl = $"https://api.github.com/repos/{owner}/{repo}";
                    var repoInfoResponse = await httpClient.GetAsync(repoInfoUrl);
                    repoInfoResponse.EnsureSuccessStatusCode();
                    var repoInfoJson = await repoInfoResponse.Content.ReadAsStringAsync();
                    using var repoInfoDoc = JsonDocument.Parse(repoInfoJson);
                    defaultBranch = repoInfoDoc.RootElement.GetProperty("default_branch").GetString() ?? "main";

                    var commitInfoUrl = $"https://api.github.com/repos/{owner}/{repo}/commits/{defaultBranch}?per_page=1";
                    var commitInfoResponse = await httpClient.GetAsync(commitInfoUrl);
                    if (commitInfoResponse.IsSuccessStatusCode)
                    {
                        var commitInfoJson = await commitInfoResponse.Content.ReadAsStringAsync();
                        using var commitInfoDoc = JsonDocument.Parse(commitInfoJson);
                        commitHash = commitInfoDoc.RootElement.GetProperty("sha").GetString() ?? "";
                    }

                    // Download zip
                    var zipUrl = $"https://api.github.com/repos/{owner}/{repo}/zipball/{defaultBranch}";
                    var zipResponse = await httpClient.GetAsync(zipUrl);
                    zipResponse.EnsureSuccessStatusCode();

                    var zipPath = Path.Combine(workDir, "repo.zip");
                    using (var fs = new FileStream(zipPath, FileMode.Create))
                    {
                        await zipResponse.Content.CopyToAsync(fs);
                    }

                    // Extract zip
                    _logger.LogInformation(" Extracting repository zip...");
                    var extractDir = Path.Combine(workDir, "extracted");
                    Directory.CreateDirectory(extractDir);
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir);

                    // Move contents from subdirectory to workDir
                    var extractedSubDir = Directory.GetDirectories(extractDir).FirstOrDefault();
                    if (extractedSubDir != null)
                    {
                        foreach (var dirPath in Directory.GetDirectories(extractedSubDir, "*", SearchOption.AllDirectories))
                        {
                            Directory.CreateDirectory(dirPath.Replace(extractedSubDir, workDir));
                        }
                        foreach (var newPath in Directory.GetFiles(extractedSubDir, "*.*", SearchOption.AllDirectories))
                        {
                            File.Copy(newPath, newPath.Replace(extractedSubDir, workDir), true);
                        }
                    }

                    // Cleanup extraction temp files
                    Directory.Delete(extractDir, true);
                    File.Delete(zipPath);
                    _logger.LogInformation(" Repository downloaded and extracted successfully via API fallback");
                }
            }

            // Get commit hash and branch from git if not already set
            if (string.IsNullOrEmpty(commitHash) && Directory.Exists(Path.Combine(workDir, ".git")))
            {
                _logger.LogInformation(" Repository cloned successfully");
                var revParseProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse HEAD",
                    WorkingDirectory = workDir,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                });
                commitHash = (await revParseProcess!.StandardOutput.ReadToEndAsync()).Trim();
                await revParseProcess.WaitForExitAsync();

                var branchProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --abbrev-ref HEAD",
                    WorkingDirectory = workDir,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                });
                defaultBranch = (await branchProcess!.StandardOutput.ReadToEndAsync()).Trim();
                await branchProcess.WaitForExitAsync();
                if (string.IsNullOrEmpty(defaultBranch) || defaultBranch == "HEAD") defaultBranch = "main";
            }
            // STEP 2: Map files
            _logger.LogInformation(" STEP 2: Reading source files...");
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
            var maxContextChars = 120000;

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
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to read file: {File}", file); }
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
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to read config file: {File}", file); }
            }

            _logger.LogInformation($" Read {fileContents.Count} files ({totalChars:N0} chars)");

            // STEP 4: Generate comprehensive documentation with AI
            _logger.LogInformation(" STEP 3: Generating multi-document architecture...");
            
            var contextBuilder = new System.Text.StringBuilder();
            contextBuilder.AppendLine($"REPOSITORY: {owner}/{repo}");
            contextBuilder.AppendLine("CODE FILES:");
            
            foreach (var kvp in fileContents.Take(50))
            {
                contextBuilder.AppendLine($"\n### {kvp.Key}\n```\n{kvp.Value}\n```");
            }

            var aiResponse = await _aiService.GenerateStructuredDocsAsync(contextBuilder.ToString(), owner, repo, commitHash);
            
            // STEP 5: Parse structure response (overview, diagrams, relations)
            _logger.LogInformation(" STEP 4: Parsing structure response...");
            
            var result = ParseAiResponse(aiResponse, fileContents.Keys.ToList());
            
            // STEP 6: Inject required sections if AI didn't return them
            EnsureRequiredSections(result);
            
            // Add default diagrams if Phase 1 didn't produce any
            if (result.Diagrams.Count == 0)
                AddDefaultDiagrams(result);
            // STEP 7: Multi-pass section content generation
            var sectionsNeedingContent = result.Sections
                .Where(s => string.IsNullOrEmpty(s.Content) || s.Content.Length < 500)
                .ToList();
            
            if (sectionsNeedingContent.Any())
            {
                _logger.LogInformation(" STEP 5: Generating detailed content for {Count} sections in batches...", sectionsNeedingContent.Count);
                
                const int batchSize = 1;
                var batches = new List<List<SectionResult>>();
                for (int b = 0; b < sectionsNeedingContent.Count; b += batchSize)
                    batches.Add(sectionsNeedingContent.Skip(b).Take(batchSize).ToList());
                
                for (int batchIdx = 0; batchIdx < batches.Count; batchIdx++)
                {
                    var batch = batches[batchIdx];
                    _logger.LogInformation(" Generating batch {BatchNum}/{TotalBatches}: {Titles}", 
                        batchIdx + 1, batches.Count, string.Join(", ", batch.Select(s => s.Title)));
                    
                    try
                    {
                        var titles = batch.Select(s => s.Title).ToList();
                        var descriptions = batch.Select(s => s.Summary).ToList();
                        
                        var batchResponse = await _aiService.GenerateSectionBatchAsync(
                            contextBuilder.ToString(), owner, repo, commitHash, titles, descriptions);
                        
                        var batchResult = ParseSectionBatchResponse(batchResponse);
                        foreach (var batchSection in batchResult)
                        {
                            var targetSection = result.Sections.FirstOrDefault(s => 
                                s.Title.Equals(batchSection.Title, StringComparison.OrdinalIgnoreCase));
                            if (targetSection != null)
                            {
                                targetSection.Content = batchSection.Content;
                                if (batchSection.SourceCitations.Any())
                                    targetSection.SourceCitations = batchSection.SourceCitations;
                            }
                        }
                        _logger.LogInformation(" Batch {BatchNum} complete: {Count} sections updated", batchIdx + 1, batchResult.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, " Batch {BatchNum} failed, sections will have stub content: {Message}", batchIdx + 1, ex.Message);
                    }
                }
            }

            // STEP 7b: Retry failed sections (content empty or < 200 words)
            var failedSections = result.Sections
                .Where(s => string.IsNullOrEmpty(s.Content) || s.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 200)
                .Where(s => !s.Title.Equals("Overview", StringComparison.OrdinalIgnoreCase) && !s.Title.Equals("Playwright Overview", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (failedSections.Any())
            {
                _logger.LogWarning(" {Count} sections need retry: {Titles}",
                    failedSections.Count, string.Join(", ", failedSections.Select(s => s.Title)));

                foreach (var section in failedSections)
                {
                    for (int retry = 1; retry <= 2; retry++)
                    {
                        try
                        {
                            _logger.LogWarning(" Retrying '{Title}' (attempt {Retry}/2)", section.Title, retry);
                            var retryResponse = await _aiService.GenerateSectionBatchAsync(
                                contextBuilder.ToString(), owner, repo, commitHash,
                                new List<string> { section.Title }, new List<string> { section.Summary });
                            var retryResult = ParseSectionBatchResponse(retryResponse);
                            var retrySection = retryResult.FirstOrDefault();
                            if (retrySection != null && !string.IsNullOrEmpty(retrySection.Content)
                                && retrySection.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 200)
                            {
                                section.Content = retrySection.Content;
                                if (retrySection.SourceCitations.Any())
                                    section.SourceCitations = retrySection.SourceCitations;
                                _logger.LogInformation(" Retry succeeded for '{Title}'", section.Title);
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, " Retry {Retry}/2 failed for '{Title}'", retry, section.Title);
                        }
                    }
                }
            }
            
            // STEP 8: Enrich source citations from local file paths
            try
            {
                EnrichSourceCitations(result, allFiles, owner, repo, commitHash, defaultBranch);
                _logger.LogInformation(" Source citations enriched from local file paths");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, " Source citation enrichment failed (non-fatal): {Message}", ex.Message);
            }

            _logger.LogInformation(" Documentation generation complete: {Sections} sections, {Diagrams} diagrams, {Relations} relations",
                result.Sections.Count, result.Diagrams.Count, result.Relations.Count);
            
            return result;
        }
        finally
        {
            try 
            {
                if (Directory.Exists(workDir))
                    Directory.Delete(workDir, true);
                _logger.LogInformation(" Cleaned up");
            }
            catch { /* Best-effort cleanup — do not propagate */ }
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
                        var sectionContent = section.GetProperty("content").GetString() ?? "";
                        
                        // Parse source citations: first try JSON 'sources' array, then extract from markdown content
                        var citations = new List<string>();
                        if (section.TryGetProperty("sources", out var sources))
                        {
                            citations = sources.EnumerateArray()
                                .Select(s => s.GetString() ?? "")
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList();
                        }
                        
                        // Fallback: extract GitHub URLs from content markdown
                        if (citations.Count == 0 && !string.IsNullOrEmpty(sectionContent))
                        {
                            var githubUrlPattern = new Regex(@"https://github\.com/[^\s\)\]]+", RegexOptions.IgnoreCase);
                            citations = githubUrlPattern.Matches(sectionContent)
                                .Select(m => m.Value.TrimEnd('.', ',', ';', ')', ']'))
                                .Distinct()
                                .ToList();
                        }

                        // Parse sectionNumber from JSON if available
                        var sectionNumber = section.TryGetProperty("sectionNumber", out var snEl) ? snEl.GetString() ?? "" : "";

                        result.Sections.Add(new SectionResult
                        {
                            Title = section.GetProperty("title").GetString() ?? "",
                            Content = sectionContent,
                            Summary = section.TryGetProperty("summary", out var sum) ? sum.GetString() ?? "" : "",
                            Level = section.TryGetProperty("level", out var lvl) ? lvl.GetInt32() : 2,
                            SectionType = section.TryGetProperty("type", out var t) ? t.GetString() ?? "content" : "content",
                            SectionNumber = sectionNumber,
                            RelatedFiles = section.TryGetProperty("files", out var files) 
                                ? files.EnumerateArray().Select(f => f.GetString() ?? "").Where(f => !string.IsNullOrEmpty(f)).ToList() 
                                : new List<string>(),
                            SourceCitations = citations
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

    private List<SectionResult> ParseSectionBatchResponse(string batchResponse)
    {
        var results = new List<SectionResult>();
        try
        {
            var jsonMatch = Regex.Match(batchResponse, @"\{[\s\S]*\}", RegexOptions.Multiline);
            if (jsonMatch.Success)
            {
                var json = JsonSerializer.Deserialize<JsonElement>(jsonMatch.Value);
                if (json.TryGetProperty("sections", out var sectionsEl))
                {
                    foreach (var section in sectionsEl.EnumerateArray())
                    {
                        var sectionContent = section.GetProperty("content").GetString() ?? "";
                        
                        // Parse source citations from JSON 'sources' array
                        var citations = new List<string>();
                        if (section.TryGetProperty("sources", out var sources))
                        {
                            citations = sources.EnumerateArray()
                                .Select(s => s.GetString() ?? "")
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList();
                        }
                        
                        // Fallback: extract GitHub URLs from markdown content
                        if (citations.Count == 0 && !string.IsNullOrEmpty(sectionContent))
                        {
                            var githubUrlPattern = new Regex(@"https://github\.com/[^\s\)\]]+", RegexOptions.IgnoreCase);
                            citations = githubUrlPattern.Matches(sectionContent)
                                .Select(m => m.Value.TrimEnd('.', ',', ';', ')', ']'))
                                .Distinct()
                                .ToList();
                        }

                        results.Add(new SectionResult
                        {
                            Title = section.GetProperty("title").GetString() ?? "",
                            Content = sectionContent,
                            SourceCitations = citations
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to parse section batch response: {ex.Message}");
        }
        return results;
    }

    private void EnsureRequiredSections(IngestResult result)
    {
        var required = new[]
        {
            ("Playwright Overview", "Comprehensive overview of the Playwright testing framework", 1, "overview", "1"),
            ("Core Architecture", "Client-server architecture, protocol transport, and browser communication", 2, "architecture", "2"),
            ("Package Structure", "Monorepo layout, npm packages, and workspace configuration", 2, "architecture", "3"),
            ("Browser Management", "Browser registry, version management, device emulation", 2, "architecture", "4"),
            ("Browser Configuration and Protocols", "CDP, Juggler, WebKit Inspector Protocol details", 2, "architecture", "5"),
            ("Browser Installation and Registry", "Installation logic, cache directories, platform handling", 2, "architecture", "6"),
            ("Browser Patching System", "Patch categories for Firefox, WebKit, Chromium", 2, "architecture", "7"),
            ("Core API", "BrowserType, Browser, BrowserContext, Page class hierarchy", 2, "api", "8"),
            ("Browser and Context Management", "BrowserContext isolation, multi-session testing", 2, "api", "9"),
            ("Page and Frame Management", "Navigation, frame hierarchy, iframe handling", 2, "api", "10"),
            ("Locator and Element Interactions", "Locator API, auto-waiting, selector engines", 2, "api", "11"),
            ("Network Interception", "Route mocking, HAR replay, request monitoring", 2, "api", "12"),
            ("API Request Context", "HTTP request API, shared state with browser context", 2, "api", "13"),
            ("Test Framework", "@playwright/test, fixtures, projects, test runner", 2, "testing", "14"),
            ("Test Configuration and Projects", "Configuration options, multi-project setup", 2, "testing", "15"),
            ("Fixtures and Lifecycle", "Built-in fixtures, custom fixtures, lifecycle hooks", 2, "testing", "16"),
            ("Test Execution Architecture", "Workers, sharding, isolation, parallelism", 2, "testing", "17"),
            ("Assertions and Expectations", "Web-first assertions, soft assertions, matchers", 2, "testing", "18"),
            ("Developer Tools", "Playwright Inspector, debug mode, VS Code extension", 2, "tooling", "19"),
            ("Recorder and Code Generation", "Codegen command, live code generation", 2, "tooling", "20"),
            ("UI Mode", "Interactive test development, watch mode, time travel", 2, "tooling", "21"),
            ("Trace Viewer", "Trace recording, post-mortem debugging, performance", 2, "tooling", "22"),
            ("Reporting System", "Reporter architecture, custom reporters, configuration", 2, "reporting", "23"),
            ("HTML Reporter", "React/Vite implementation, interactive report features", 2, "reporting", "24"),
            ("Built-in Reporters", "List, Dot, Line, JSON, JUnit reporters", 2, "reporting", "25"),
            ("Blob Reporter and Report Merging", "Sharded execution, blob storage, merge-reports", 2, "reporting", "26"),
            ("CI/CD and Release Management", "GitHub Actions, Docker, release process", 2, "devops", "27"),
            ("Test Workflows", "CI configurations, matrix testing, artifacts", 2, "devops", "28"),
            ("Release and Distribution", "npm publishing, version management, canary releases", 2, "devops", "29"),
            ("Cross-Language Synchronization", "Protocol-based generation, language bindings", 2, "architecture", "30"),
            ("Advanced Topics", "Custom selectors, HAR replay, CDP access", 2, "advanced", "31"),
            ("Component Testing", "CT packages, Vite integration, framework adapters", 2, "testing", "32"),
            ("Protocol Communication Architecture", "Channels, dispatchers, protocol schema", 2, "architecture", "33"),
            ("MCP Browser Automation Server", "Model Context Protocol, AI browser automation", 2, "advanced", "34"),
        };

        foreach (var (title, summary, level, type, number) in required)
        {
            if (!result.Sections.Any(s => s.Title.Equals(title, StringComparison.OrdinalIgnoreCase)))
            {
                result.Sections.Add(new SectionResult
                {
                    Title = title, Summary = summary, Level = level,
                    SectionType = type, SectionNumber = number, Content = ""
                });
            }
        }
    }

    private void AddDefaultDiagrams(IngestResult result)
    {
        result.Diagrams.Add(new DiagramResult { Title = "Architecture Overview", Type = "mermaid", Content = "graph TD\n    A[Playwright Client] --> B[Protocol Transport]\n    B --> C[Browser Server]\n    C --> D[Chromium - CDP]\n    C --> E[Firefox - Juggler]\n    C --> F[WebKit - Inspector]\n    G[Test Runner] --> A" });
        result.Diagrams.Add(new DiagramResult { Title = "Package Dependencies", Type = "mermaid", Content = "graph LR\n    A[playwright] --> B[playwright-core]\n    C[@playwright/test] --> A\n    D[playwright-chromium] --> B\n    E[playwright-firefox] --> B\n    F[playwright-webkit] --> B" });
        result.Diagrams.Add(new DiagramResult { Title = "Test Execution Flow", Type = "mermaid", Content = "sequenceDiagram\n    participant T as Test Runner\n    participant W as Worker\n    participant B as Browser\n    T->>W: Dispatch test\n    W->>B: Launch browser\n    B-->>W: Ready\n    W->>B: Execute test\n    B-->>W: Results\n    W-->>T: Report" });
        result.Diagrams.Add(new DiagramResult { Title = "Browser Management Flow", Type = "mermaid", Content = "graph TD\n    A[Registry] --> B[Check installed]\n    B -->|Found| C[Use cached]\n    B -->|Missing| D[Download]\n    D --> E[Extract]\n    E --> C\n    C --> F[Launch]" });
        result.Diagrams.Add(new DiagramResult { Title = "Protocol Generation Pipeline", Type = "mermaid", Content = "flowchart LR\n    A[protocol.yml] --> B[generate_channels.js]\n    B --> C[TypeScript channels]\n    B --> D[TypeScript dispatchers]\n    A --> E[generate_dotnet.js]\n    E --> F[C-sharp channels]" });
        result.Diagrams.Add(new DiagramResult { Title = "CI/CD Pipeline", Type = "mermaid", Content = "graph TD\n    A[Push] --> B[Lint]\n    B --> C[Test]\n    C --> D[Build]\n    D --> E[Publish]\n    E --> F[Docs]" });
        result.Diagrams.Add(new DiagramResult { Title = "Component Testing", Type = "mermaid", Content = "graph TD\n    A[Test File] --> B[Vite Dev Server]\n    B --> C[Component Bundle]\n    C --> D[Browser Preview]\n    D --> E[Assertions]" });
        result.Diagrams.Add(new DiagramResult { Title = "Reporting System Flow", Type = "mermaid", Content = "graph LR\n    A[Test Events] --> B[Reporter Interface]\n    B --> C[List Reporter]\n    B --> D[JSON Reporter]\n    B --> E[HTML Reporter]\n    B --> F[Blob Reporter]\n    F --> G[merge-reports]\n    G --> E" });
        result.Diagrams.Add(new DiagramResult { Title = "Fixture Lifecycle", Type = "mermaid", Content = "sequenceDiagram\n    participant R as Runner\n    participant F as Setup\n    participant T as Test\n    participant C as Teardown\n    R->>F: beforeAll\n    F->>T: Provide fixtures\n    T->>T: Execute\n    T->>C: afterEach\n    C->>R: Done" });
        result.Diagrams.Add(new DiagramResult { Title = "Browser Installation Flow", Type = "mermaid", Content = "flowchart TD\n    A[npm install] --> B[install.js]\n    B --> C[Read browsers.json]\n    C --> D[For each browser]\n    D -->|Cached| E[Skip]\n    D -->|Missing| F[Download CDN]\n    F --> G[Extract and verify]" });
    }

    private void EnrichSourceCitations(IngestResult result, List<string> allFiles, string owner, string repo, string commitHash, string defaultBranch)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "and", "the", "of", "in", "for", "a", "an", "to", "with", "is", "are", "on", "by", "from", "at"
        };

        var codeFiles = allFiles
            .Where(f => _codeExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var section in result.Sections)
        {
            var textToMatch = $"{section.Title} {section.Summary}".ToLowerInvariant();
            var keywords = textToMatch
                .Split(new[] { ' ', '-', '_', '/', '\\', '.', ',', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !stopWords.Contains(w))
                .Distinct()
                .ToList();

            if (keywords.Count == 0) continue;

            var matches = new List<(string filePath, int matchCount)>();

            foreach (var file in codeFiles)
            {
                var fileLower = file.ToLowerInvariant();
                var matchCount = keywords.Count(k => fileLower.Contains(k));

                if (matchCount >= 1)
                {
                    matches.Add((file, matchCount));
                }
            }

            // Directory-level matching: map common section keywords to directory prefixes
            var dirKeywords = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "browser", new[] { "src/server/", "packages/playwright-core/src/server/" } },
                { "test", new[] { "packages/playwright-test/", "packages/playwright/src/" } },
                { "api", new[] { "packages/playwright-core/src/client/", "packages/playwright-core/src/server/" } },
                { "report", new[] { "packages/playwright/src/reporters/", "packages/html-reporter/" } },
                { "trace", new[] { "packages/trace-viewer/", "packages/playwright-core/src/server/trace/" } },
                { "codegen", new[] { "packages/playwright-core/src/server/codegen/", "packages/recorder/" } },
                { "protocol", new[] { "packages/protocol/", "packages/playwright-core/src/protocol/" } },
                { "fixture", new[] { "packages/playwright-test/src/common/", "packages/playwright/src/" } },
                { "config", new[] { "packages/playwright-test/src/common/", "packages/playwright/src/" } },
                { "locator", new[] { "packages/playwright-core/src/client/", "packages/playwright-core/src/server/injected/" } },
                { "network", new[] { "packages/playwright-core/src/server/", "packages/playwright-core/src/client/" } },
                { "frame", new[] { "packages/playwright-core/src/server/frames", "packages/playwright-core/src/client/" } },
                { "install", new[] { "packages/playwright-core/src/server/registry/", "packages/playwright-core/src/utils/" } },
                { "ci", new[] { ".github/workflows/", "utils/" } },
                { "release", new[] { "utils/", ".github/workflows/" } },
                { "component", new[] { "packages/playwright-ct-", "packages/playwright-test/" } },
                { "recorder", new[] { "packages/recorder/", "packages/playwright-core/src/server/codegen/" } },
            };

            foreach (var keyword in keywords)
            {
                if (dirKeywords.TryGetValue(keyword, out var dirs))
                {
                    foreach (var file in codeFiles)
                    {
                        var fileFwd = file.Replace('\\', '/');
                        if (dirs.Any(d => fileFwd.StartsWith(d, StringComparison.OrdinalIgnoreCase))
                            && !matches.Any(m => m.filePath == file))
                        {
                            matches.Add((file, 1)); // directory match = 1 point
                        }
                    }
                }
            }

            var topMatches = matches
                .OrderByDescending(m => m.matchCount)
                .Take(30)
                .Select(m => $"https://github.com/{owner}/{repo}/blob/{commitHash}/{m.filePath.Replace('\\', '/')}")
                .ToList();

            if (topMatches.Any())
            {
                section.SourceCitations.AddRange(topMatches);
                section.SourceCitations = section.SourceCitations.Distinct().ToList();
            }
        }

        _logger.LogInformation(" EnrichSourceCitations: Processed {Count} sections", result.Sections.Count);
    }
}
