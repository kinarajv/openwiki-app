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
    public async Task<IngestResult> ProcessRepositoryAsync(string owner, string repo, string? repoUrl = null, string? token = null, Func<string, Task>? onProgress = null)
    {
        var isGitHub = true;
        string hostBaseUrl = "https://github.com";
        string cloneUrl;

        if (!string.IsNullOrEmpty(repoUrl))
        {
            if (Uri.TryCreate(repoUrl, UriKind.Absolute, out var parsedUri))
            {
                hostBaseUrl = $"{parsedUri.Scheme}://{parsedUri.Host}";
                isGitHub = parsedUri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase);
            }
            if (!string.IsNullOrEmpty(token) && Uri.TryCreate(repoUrl, UriKind.Absolute, out var tokenUri))
            {
                cloneUrl = $"{tokenUri.Scheme}://oauth2:{token}@{tokenUri.Host}{tokenUri.AbsolutePath}";
            }
            else
            {
                cloneUrl = repoUrl;
            }
            if (!cloneUrl.EndsWith(".git"))
                cloneUrl += ".git";
        }
        else
        {
            cloneUrl = $"https://github.com/{owner}/{repo}.git";
            if (!string.IsNullOrEmpty(token))
            {
                cloneUrl = $"https://oauth2:{token}@github.com/{owner}/{repo}.git";
            }
        }

        string workDir = Path.Combine(Path.GetTempPath(), "deepwiki-repos", $"{owner}-{repo}-{Guid.NewGuid():N}");
        string commitHash = "";
        string defaultBranch = "main";
        try
        {
            Directory.CreateDirectory(workDir);
            
            _logger.LogInformation(" STEP 1: Cloning repository...");
            if (onProgress != null) await onProgress("step:0:Cloning repository...");
            var cloneProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"clone --depth 1 {cloneUrl} .",
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            var stdoutTask = cloneProcess!.StandardOutput.ReadToEndAsync();
            var stderrTask = cloneProcess.StandardError.ReadToEndAsync();
            await cloneProcess.WaitForExitAsync();
            await stdoutTask; // consume stdout to prevent deadlock
            
            if (cloneProcess.ExitCode != 0)
            {
                var error = await stderrTask;
                var hasFiles = Directory.GetFiles(workDir, "*.*", SearchOption.TopDirectoryOnly).Length > 0
                    || Directory.GetDirectories(workDir).Any(d => !d.EndsWith(".git"));
                
                if (hasFiles)
                {
                    _logger.LogWarning($"Git clone had errors ({cloneProcess.ExitCode}) but files exist, continuing: {error.Substring(0, Math.Min(error.Length, 200))}");
                }
                else if (isGitHub)
                {
                    _logger.LogWarning($"Git clone failed ({cloneProcess.ExitCode}): {error}. Falling back to zip download via GitHub API...");

                    using var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("OpenWiki-App");

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

                    var zipUrl = $"https://api.github.com/repos/{owner}/{repo}/zipball/{defaultBranch}";
                    var zipResponse = await httpClient.GetAsync(zipUrl);
                    zipResponse.EnsureSuccessStatusCode();

                    var zipPath = Path.Combine(workDir, "repo.zip");
                    using (var fs = new FileStream(zipPath, FileMode.Create))
                    {
                        await zipResponse.Content.CopyToAsync(fs);
                    }

                    _logger.LogInformation(" Extracting repository zip...");
                    var extractDir = Path.Combine(workDir, "extracted");
                    Directory.CreateDirectory(extractDir);
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir);

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

                    Directory.Delete(extractDir, true);
                    File.Delete(zipPath);
                    _logger.LogInformation(" Repository downloaded and extracted successfully via API fallback");
                }
                else
                {
                    throw new InvalidOperationException($"Git clone failed for non-GitHub repository ({cloneProcess.ExitCode}): {error}");
                }
            }
            if (string.IsNullOrEmpty(commitHash) && Directory.Exists(Path.Combine(workDir, ".git")))
            {
                _logger.LogInformation(" Repository cloned successfully");
                if (onProgress != null) await onProgress("log:Repository cloned successfully");
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
            _logger.LogInformation(" STEP 2: Reading source files...");
            if (onProgress != null) await onProgress("step:1:Analyzing source code structure...");
            var allFiles = Directory.GetFiles(workDir, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.Combine(workDir, ".git")))
                .Select(f => f.Substring(workDir.Length).TrimStart(Path.DirectorySeparatorChar))
                .ToList();

            var codeFiles = allFiles
                .Where(f => _codeExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .ToList();

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
                    
                    if (content.Length > 0 && content.Length < 30000 && 
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
            if (onProgress != null) await onProgress($"log:Read {fileContents.Count} files ({totalChars:N0} chars)");

            _logger.LogInformation(" STEP 3: Generating multi-document architecture...");
            if (onProgress != null) await onProgress("step:2:Generating multi-document architecture...");
            
            var contextBuilder = new System.Text.StringBuilder();
            contextBuilder.AppendLine($"REPOSITORY: {owner}/{repo}");

            contextBuilder.AppendLine($"\nTOTAL FILES IN REPOSITORY: {allFiles.Count}");
            contextBuilder.AppendLine($"CODE FILES FOUND: {codeFiles.Count}");
            contextBuilder.AppendLine($"FILES WITH READABLE CONTENT: {fileContents.Count}");
            contextBuilder.AppendLine("\nCOMPLETE FILE LISTING:");
            foreach (var f in allFiles.Take(200))
                contextBuilder.AppendLine($"  - {f}");

            var isMinimalRepo = fileContents.Count <= 2 || totalChars < 500;
            if (isMinimalRepo)
            {
                contextBuilder.AppendLine("\n⚠️ WARNING: This repository has VERY LITTLE source code content.");
                contextBuilder.AppendLine("You MUST NOT fabricate or invent files, modules, architectures, or features that do not exist.");
                contextBuilder.AppendLine("Only describe what is actually present in the files listed above.");
                contextBuilder.AppendLine("If there is almost no code, say so honestly — do not make up elaborate descriptions.\n");
            }

            contextBuilder.AppendLine("\nCODE FILES:");
            
            foreach (var kvp in fileContents.Take(50))
            {
                contextBuilder.AppendLine($"\n### {kvp.Key}\n```\n{kvp.Value}\n```");
            }

            var aiResponse = await _aiService.GenerateStructuredDocsAsync(contextBuilder.ToString(), owner, repo, commitHash, hostBaseUrl);
            
            _logger.LogInformation(" STEP 4: Parsing structure response...");
            if (onProgress != null) await onProgress("step:3:Parsing structure response...");
            
            var result = ParseAiResponse(aiResponse, fileContents.Keys.ToList());
            
            var sectionsNeedingContent = result.Sections
                .Where(s => string.IsNullOrEmpty(s.Content) || s.Content.Length < 500)
                .ToList();
            
            if (sectionsNeedingContent.Any())
            {
                _logger.LogInformation(" STEP 5: Generating detailed content for {Count} sections in batches...", sectionsNeedingContent.Count);
                if (onProgress != null) await onProgress($"step:4:Generating detailed content for {sectionsNeedingContent.Count} sections...");
                
                const int batchSize = 1;
                var batches = new List<List<SectionResult>>();
                for (int b = 0; b < sectionsNeedingContent.Count; b += batchSize)
                    batches.Add(sectionsNeedingContent.Skip(b).Take(batchSize).ToList());
                
                for (int batchIdx = 0; batchIdx < batches.Count; batchIdx++)
                {
                    var batch = batches[batchIdx];
                    _logger.LogInformation(" Generating batch {BatchNum}/{TotalBatches}: {Titles}", 
                        batchIdx + 1, batches.Count, string.Join(", ", batch.Select(s => s.Title)));
                    if (onProgress != null) await onProgress($"log:Generating section {batchIdx + 1}/{batches.Count}: {string.Join(", ", batch.Select(s => s.Title))}");
                    
                    try
                    {
                        var titles = batch.Select(s => s.Title).ToList();
                        var descriptions = batch.Select(s => s.Summary).ToList();
                        
                        var batchResponse = await _aiService.GenerateSectionBatchAsync(
                            contextBuilder.ToString(), owner, repo, commitHash, titles, descriptions, hostBaseUrl);
                        
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
                        if (onProgress != null) await onProgress($"log:Section {batchIdx + 1}/{batches.Count} complete");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, " Batch {BatchNum} failed, sections will have stub content: {Message}", batchIdx + 1, ex.Message);
                    }
                }
            }

            var failedSections = result.Sections
                .Where(s => string.IsNullOrEmpty(s.Content) || s.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 50)
                .Where(s => !s.Title.Equals("Overview", StringComparison.OrdinalIgnoreCase) && !s.Title.Contains("Overview", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (failedSections.Any())
            {
                _logger.LogWarning(" {Count} sections need retry: {Titles}",
                    failedSections.Count, string.Join(", ", failedSections.Select(s => s.Title)));
                if (onProgress != null) await onProgress($"log:Retrying {failedSections.Count} sections...");

                foreach (var section in failedSections)
                {
                    for (int retry = 1; retry <= 2; retry++)
                    {
                        try
                        {
                            _logger.LogWarning(" Retrying '{Title}' (attempt {Retry}/2)", section.Title, retry);
                            var retryResponse = await _aiService.GenerateSectionBatchAsync(
                                contextBuilder.ToString(), owner, repo, commitHash,
                                new List<string> { section.Title }, new List<string> { section.Summary }, hostBaseUrl);
                            var retryResult = ParseSectionBatchResponse(retryResponse);
                            var retrySection = retryResult.FirstOrDefault();
                            if (retrySection != null && !string.IsNullOrEmpty(retrySection.Content)
                                && retrySection.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 30)
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
            
            try
            {
                EnrichSourceCitations(result, allFiles, owner, repo, commitHash, defaultBranch, hostBaseUrl);
                _logger.LogInformation(" Source citations enriched from local file paths");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, " Source citation enrichment failed (non-fatal): {Message}", ex.Message);
            }

            _logger.LogInformation(" Documentation generation complete: {Sections} sections, {Diagrams} diagrams, {Relations} relations",
                result.Sections.Count, result.Diagrams.Count, result.Relations.Count);
            if (onProgress != null) await onProgress($"step:5:Complete — {result.Sections.Count} sections, {result.Diagrams.Count} diagrams, {result.Relations.Count} relations");
            
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
            catch {  }
        }
    }

    private IngestResult ParseAiResponse(string aiResponse, List<string> allFiles)
    {
        var result = new IngestResult();
        
        try
        {
            var jsonMatch = Regex.Match(aiResponse, @"\{[\s\S]*\}", RegexOptions.Multiline);
            if (jsonMatch.Success)
            {
                var json = JsonSerializer.Deserialize<JsonElement>(jsonMatch.Value);
                
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

                if (json.TryGetProperty("sections", out var sectionsEl))
                {
                    foreach (var section in sectionsEl.EnumerateArray())
                    {
                        var sectionContent = section.TryGetProperty("content", out var contentEl) ? contentEl.GetString() ?? "" : "";
                        
                        var citations = new List<string>();
                        if (section.TryGetProperty("sources", out var sources))
                        {
                            citations = sources.EnumerateArray()
                                .Select(s => s.GetString() ?? "")
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList();
                        }
                        
                        if (citations.Count == 0 && !string.IsNullOrEmpty(sectionContent))
                        {
                            var urlPattern = new Regex(@"https?://[^\s\)\]]+/blob/[^\s\)\]]+", RegexOptions.IgnoreCase);
                            citations = urlPattern.Matches(sectionContent)
                                .Select(m => m.Value.TrimEnd('.', ',', ';', ')', ']'))
                                .Distinct()
                                .ToList();
                        }

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
            
            result.Overview = aiResponse;
            result.Sections.Add(new SectionResult
            {
                Title = "Overview",
                Content = aiResponse,
                Level = 1,
                SectionType = "overview"
            });
        }

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
                        
                        var citations = new List<string>();
                        if (section.TryGetProperty("sources", out var sources))
                        {
                            citations = sources.EnumerateArray()
                                .Select(s => s.GetString() ?? "")
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList();
                        }
                        
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

    private void EnrichSourceCitations(IngestResult result, List<string> allFiles, string owner, string repo, string commitHash, string defaultBranch, string hostBaseUrl = "https://github.com")
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
                .Select(m => $"{hostBaseUrl}/{owner}/{repo}/blob/{commitHash}/{m.filePath.Replace('\\', '/')}")
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
