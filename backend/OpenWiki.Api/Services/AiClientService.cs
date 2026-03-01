using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using OpenWiki.Api.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace OpenWiki.Api.Services;

public class AiClientService : IAiClientService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;
    private readonly string _fastModel;
    private readonly string _apiKey;
    private readonly ILogger<AiClientService> _logger;

    public AiClientService(HttpClient httpClient, IConfiguration config, ILogger<AiClientService> logger)
    {
        _httpClient = httpClient;
        _apiUrl = config["Cliproxy:ApiUrl"]?.TrimEnd('/') ?? "http://localhost:8317";
        _fastModel = config["Cliproxy:FastModel"] ?? "gpt-5-codex-mini";
        _apiKey = config["Cliproxy:ApiKey"] ?? "not-configured"; 
        _logger = logger;
    }

    public virtual async Task<string> GenerateStructuredDocsAsync(string codeContext, string owner, string repo, string commitHash = "main")
    {

        if (string.IsNullOrEmpty(codeContext)) throw new ArgumentException("codeContext cannot be null or empty", nameof(codeContext));

        try {
        var systemPrompt = $@"You are an expert software architect generating a documentation STRUCTURE for a code repository.

Your task is to analyze the source code and produce a JSON object containing:
1. A comprehensive overview (1500+ words with tables)
2. A list of 34 section STUBS (title, summary, level, type, sectionNumber — but NO detailed content)
3. Relations between sections (20+)
4. 10 Mermaid diagrams

IMPORTANT: For sections, you do NOT need to write full content. Just provide title, summary (2-3 sentences), level, type, sectionNumber, and files. The detailed content will be generated separately.

You MUST output valid JSON with this EXACT structure:
{{
  ""overview"": ""A comprehensive 1500+ word overview covering: what the project is, key capabilities table, supported languages table, browser support table, repository organization, architecture layers, build scripts, version management, engine requirements. Include at least 4 markdown tables."",
  ""sections"": [
    {{
      ""title"": ""Core Architecture"",
      ""sectionNumber"": ""2"",
      ""summary"": ""Covers the client-server architecture, protocol transport, and browser communication model."",
      ""level"": 2,
      ""type"": ""architecture"",
      ""files"": [""src/client/browserType.ts"", ""src/server/browserType.ts""]
    }}
  ],
  ""relations"": [
    {{
      ""from"": ""Core Architecture"",
      ""to"": ""Package Structure"",
      ""type"": ""depends_on"",
      ""description"": ""Core architecture is organized by the package structure""
    }}
  ],
  ""diagrams"": [
    {{
      ""title"": ""Architecture Overview"",
      ""type"": ""mermaid"",
      ""content"": ""graph TD\n    A[Playwright Client] --> B[Protocol Transport]\n    B --> C[Browser Server]\n    C --> D[Chromium - CDP]\n    C --> E[Firefox - Juggler]\n    C --> F[WebKit - Inspector]""
    }}
  ]
}}

MANDATORY SECTION LIST - Generate ALL of these as section stubs (title + summary + level + type + sectionNumber):
1. Playwright Overview (level 1, type overview, sectionNumber ""1"")
2. Core Architecture (level 2, type architecture, sectionNumber ""2"")
3. Package Structure (level 2, type architecture, sectionNumber ""3"")
4. Browser Management (level 2, type architecture, sectionNumber ""4"")
5. Browser Configuration and Protocols (level 2, type architecture, sectionNumber ""5"")
6. Browser Installation and Registry (level 2, type architecture, sectionNumber ""6"")
7. Browser Patching System (level 2, type architecture, sectionNumber ""7"")
8. Core API (level 2, type api, sectionNumber ""8"")
9. Browser and Context Management (level 2, type api, sectionNumber ""9"")
10. Page and Frame Management (level 2, type api, sectionNumber ""10"")
11. Locator and Element Interactions (level 2, type api, sectionNumber ""11"")
12. Network Interception (level 2, type api, sectionNumber ""12"")
13. API Request Context (level 2, type api, sectionNumber ""13"")
14. Test Framework (level 2, type testing, sectionNumber ""14"")
15. Test Configuration and Projects (level 2, type testing, sectionNumber ""15"")
16. Fixtures and Lifecycle (level 2, type testing, sectionNumber ""16"")
17. Test Execution Architecture (level 2, type testing, sectionNumber ""17"")
18. Assertions and Expectations (level 2, type testing, sectionNumber ""18"")
19. Developer Tools (level 2, type tooling, sectionNumber ""19"")
20. Recorder and Code Generation (level 2, type tooling, sectionNumber ""20"")
21. UI Mode (level 2, type tooling, sectionNumber ""21"")
22. Trace Viewer (level 2, type tooling, sectionNumber ""22"")
23. Reporting System (level 2, type reporting, sectionNumber ""23"")
24. HTML Reporter (level 2, type reporting, sectionNumber ""24"")
25. Built-in Reporters (level 2, type reporting, sectionNumber ""25"")
26. Blob Reporter and Report Merging (level 2, type reporting, sectionNumber ""26"")
27. CI/CD and Release Management (level 2, type devops, sectionNumber ""27"")
28. Test Workflows (level 2, type devops, sectionNumber ""28"")
29. Release and Distribution (level 2, type devops, sectionNumber ""29"")
30. Cross-Language Synchronization (level 2, type architecture, sectionNumber ""30"")
31. Advanced Topics (level 2, type advanced, sectionNumber ""31"")
32. Component Testing (level 2, type testing, sectionNumber ""32"")
33. Protocol Communication Architecture (level 2, type architecture, sectionNumber ""33"")
34. MCP Browser Automation Server (level 2, type advanced, sectionNumber ""34"")

MANDATORY DIAGRAMS - Generate exactly 10 Mermaid diagrams:
1. Architecture Overview (graph TD)
2. Package Dependencies (graph LR)
3. Test Execution Flow (sequenceDiagram)
4. Browser Management Flow (graph TD)
5. Protocol Generation Pipeline (flowchart LR)
6. CI/CD Pipeline (graph TD)
7. Component Testing Architecture (graph TD)
8. Reporting System Flow (graph LR)
9. Fixture Lifecycle (sequenceDiagram)
10. Browser Installation Flow (flowchart TD)

MERMAID SYNTAX: NEVER use parentheses inside square brackets. Use hyphens instead. Example: A[Playwright Driver - Node.js] NOT A[Playwright Driver (Node.js)].

Generate at least 20 relations between sections.

Output ONLY the JSON object, no other text.";

            return await CallAiAsync(systemPrompt, codeContext, 65536);
        } 
        catch (Exception ex) 
        {
            _logger.LogError(ex, "=== AI STRUCTURE GENERATION FAILED ===\nError: {Message}\n============================\n", ex.Message);
            throw; 
        }
    }

    public virtual async Task<string> GenerateSectionBatchAsync(string codeContext, string owner, string repo, string commitHash, List<string> sectionTitles, List<string> sectionDescriptions)
    {
        if (string.IsNullOrEmpty(codeContext)) throw new ArgumentException("codeContext cannot be null or empty", nameof(codeContext));
        if (sectionTitles == null || sectionTitles.Count == 0) throw new ArgumentException("sectionTitles cannot be null or empty", nameof(sectionTitles));

        var sectionList = new StringBuilder();
        for (int i = 0; i < sectionTitles.Count; i++)
        {
            var desc = i < sectionDescriptions.Count ? sectionDescriptions[i] : "";
            sectionList.AppendLine($"- \"{sectionTitles[i]}\": {desc}");
        }

        try {
        var systemPrompt = $@"You are an expert software architect and technical writer. Generate DETAILED documentation content for the following sections of the {owner}/{repo} repository.

For EACH section listed below, write 2000-3500 words of DENSE, DETAILED technical content. NOT summaries — write FULL documentation with subsections, tables, code examples, source citations, and cross-references.

SECTIONS TO GENERATE:
{sectionList}

You MUST output valid JSON with this EXACT structure:
{{
  ""sections"": [
    {{
      ""title"": ""Section Title"",
      ""content"": ""## Section Title\n\n### Subsection 1\n[200+ words of detailed content]\n\n### Subsection 2\n[200+ words]\n\n| Column 1 | Column 2 | Column 3 |\n| :--- | :--- | :--- |\n| data | data | data |\n\n```javascript\n// code example\n```\n\n**Sources:** [file:L1-L50](https://github.com/{owner}/{repo}/blob/{commitHash}/file#L1-L50)\n\nFor details, see [Other Section](/wiki/{owner}/{repo}/other-section)"",
      ""sources"": [""https://github.com/{owner}/{repo}/blob/{commitHash}/path/to/file#L1-L50""]
    }}
  ]
}}

CONTENT QUALITY REQUIREMENTS FOR EACH SECTION:
1. 2000-3500 words of REAL technical content referencing the actual source code
2. At least 3 subsections with ### headers
3. At least 3 code examples with real code patterns from the repository source files, using ```language tag (javascript, typescript, bash, yaml, json)
4. At least 1 markdown table with | :--- | column alignment and 3+ data rows
5. At least 15 source citations as GitHub URLs in the ""sources"" array, each pointing to a REAL file visible in the code context provided above
6. At least 10 inline source citations throughout the content: **Sources:** [filename:L1-L10](https://github.com/{owner}/{repo}/blob/{commitHash}/filename#L1-L10)
7. At least 2 cross-references to other sections: [Section Title](/wiki/{owner}/{repo}/section-slug)
8. Proper markdown formatting: ## and ### headers, bullet lists, numbered lists, **bold**, code blocks
9. Include 5-6 mermaid diagrams per section as ```mermaid code blocks inside the content. Each diagram should have 8+ nodes showing real component relationships from the source code. Use graph TD, sequenceDiagram, flowchart LR, classDiagram, or stateDiagram types.
10. MERMAID SYNTAX: NEVER use parentheses inside square brackets. Use hyphens instead. Example: A[Playwright Driver - Node.js] NOT A[Playwright Driver (Node.js)].
11. The sources array MUST contain at least 15 GitHub URLs pointing to REAL files from the repository code context provided. Use the actual file paths you see in the code context above. Format: https://github.com/{owner}/{repo}/blob/{commitHash}/path/to/file.ts#L1-L50. DO NOT fabricate paths — only reference files present in the code context above.

ABSOLUTE MINIMUM: Each section must be at least 1800 words. Sections under 1200 words are UNACCEPTABLE.

Output ONLY the JSON object, no other text.";

            return await CallAiAsync(systemPrompt, codeContext, 131072);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== AI SECTION BATCH GENERATION FAILED ===\nError: {Message}\n============================\n", ex.Message);
            throw;
        }
    }

    private async Task<string> CallAiAsync(string systemPrompt, string userContent, int maxTokens)
    {
        var request = new
        {
            model = _fastModel,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContent }
            },
            temperature = 0.4,
            max_tokens = maxTokens
        };

        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        
        var requestMsg = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/v1/chat/completions");
        requestMsg.Content = content;
        requestMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _httpClient.SendAsync(requestMsg);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"AI API returned {response.StatusCode}: {errorBody}");
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseBody);
        
        var aiContent = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrEmpty(aiContent))
            throw new Exception("AI returned empty content");

        return aiContent;
    }
}
