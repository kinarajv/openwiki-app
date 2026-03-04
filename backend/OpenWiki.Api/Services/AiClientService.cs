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

    public virtual async Task<string> GenerateStructuredDocsAsync(string codeContext, string owner, string repo, string commitHash = "main", string hostBaseUrl = "https://github.com")
    {

        if (string.IsNullOrEmpty(codeContext)) throw new ArgumentException("codeContext cannot be null or empty", nameof(codeContext));

        try {
        var systemPrompt = $@"You are an expert software architect generating a documentation STRUCTURE for a code repository.

Your task is to analyze the provided source code of the {owner}/{repo} repository and produce a JSON object.

ANTI-HALLUCINATION RULES (CRITICAL - VIOLATION IS UNACCEPTABLE):
- You MUST ONLY describe files, modules, directories, and features that ACTUALLY EXIST in the provided source code context.
- If the repository has very few files or almost no source code, produce a SHORT, HONEST overview. Do NOT invent elaborate architectures, design patterns, technology stacks, or file structures that are not present.
- NEVER fabricate file names, directory paths, class names, function names, or module names. If you cannot see it in the code context, do NOT mention it.
- The number of sections MUST be PROPORTIONAL to the actual code content:
  * 0-2 source files: 1-3 sections, overview 200-500 words, 0-1 diagrams
  * 3-10 source files: 3-8 sections, overview 500-1000 words, 1-3 diagrams
  * 10-30 source files: 8-15 sections, overview 1000-1500 words, 3-5 diagrams
  * 30+ source files: 15-30 sections, overview 1500+ words with tables, 5-8 diagrams
- If the code context includes a WARNING about minimal content, your output MUST be minimal and factual.
  - It is BETTER to produce a short, accurate document than a long, fabricated one.

    The JSON object must contain:
      1. A comprehensive overview describing what THIS project actually does (length proportional to actual code)
      2. Section STUBS derived from the actual code structure (title, summary, level, type, sectionNumber - NO detailed content)
      3. Relations between sections
      4. Mermaid diagrams relevant to THIS project's architecture (ONLY if enough code exists)

      IMPORTANT: For sections, provide title, summary (2-3 sentences), level, type, sectionNumber, and files. Detailed content will be generated separately.

    You MUST output valid JSON with this EXACT structure:
      {{
  ""overview"": ""A proportional-length overview based on actual code content. Include tables only if there is enough real data."",
  ""sections"": [
    {{
      ""title"": ""Project Overview"",
      ""sectionNumber"": ""1"",
      ""summary"": ""High-level overview of the project, its goals, and key concepts."",
      ""level"": 1,
      ""type"": ""overview"",
      ""files"": [""README.md""]
    }}
  ],
  ""relations"": [
    {{
      ""from"": ""Section A"",
      ""to"": ""Section B"",
      ""type"": ""depends_on"",
      ""description"": ""Relationship description""
    }}
  ],
  ""diagrams"": [
    {{
      ""title"": ""Architecture Overview"",
      ""type"": ""mermaid"",
      ""content"": ""graph TD\n    A[Module A] --> B[Module B]""
    }}
  ]
}}

HOW TO DETERMINE SECTIONS:
- Look at the ACTUAL directory structure and module organization in the provided source code
- Identify major components, services, models, controllers, utilities that ACTUALLY EXIST
- ONLY create sections for modules and functionality you can SEE in the code context
- Section types can be: overview, architecture, api, module, data, config, testing, devops, advanced
- First section should always be a level-1 overview

DIAGRAM GUIDELINES:
- Generate diagrams ONLY if there is enough actual code to warrant them
- Use diagram types: graph TD, graph LR, sequenceDiagram, flowchart LR, classDiagram
- MERMAID SYNTAX: NEVER use parentheses inside square brackets. Use hyphens instead.
- Each diagram should show REAL components from the source code

Output ONLY the JSON object, no other text.";

            return await CallAiAsync(systemPrompt, codeContext, 65536);
        } 
        catch (Exception ex) 
        {
            _logger.LogError(ex, "=== AI STRUCTURE GENERATION FAILED ===\nError: {Message}\n============================\n", ex.Message);
            throw; 
        }
    }

    public virtual async Task<string> GenerateSectionBatchAsync(string codeContext, string owner, string repo, string commitHash, List<string> sectionTitles, List<string> sectionDescriptions, string hostBaseUrl = "https://github.com")
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
        var systemPrompt = $@"You are an expert software architect and technical writer. Generate documentation content for the following sections of the {owner}/{repo} repository.

ANTI-HALLUCINATION RULES (CRITICAL):
- ONLY describe code, files, modules, and features that ACTUALLY EXIST in the provided source code context.
- NEVER fabricate file names, class names, function names, directory structures, or code patterns that are not visible in the provided context.
- If a section topic has NO corresponding code in the context, write a brief note stating that no relevant source code was found for this section.
- It is BETTER to produce a short, accurate section than a long, fabricated one.
- All code examples MUST come from the actual source code provided. Do NOT invent code examples.
- All source citations MUST point to REAL files visible in the code context. Do NOT fabricate file paths.

SECTIONS TO GENERATE:
{sectionList}

Content length should be PROPORTIONAL to available code:
- If the code context has substantial source files for a section topic: write 1000-3000 words with subsections, tables, code examples, and source citations.
- If the code context has minimal or no source files for a section topic: write 100-300 words acknowledging limited content. Do NOT pad with fabricated details.

You MUST output valid JSON with this EXACT structure:
{{
  ""sections"": [
    {{
      ""title"": ""Section Title"",
      ""content"": ""## Section Title\n\n[Content based on ACTUAL source code]\n\n**Sources:** [file:L1-L50]({hostBaseUrl}/{owner}/{repo}/blob/{commitHash}/file#L1-L50)"",
      ""sources"": [""{hostBaseUrl}/{owner}/{repo}/blob/{commitHash}/path/to/real/file#L1-L50""]
    }}
  ]
}}

CONTENT REQUIREMENTS (scaled to available code):
1. Reference ONLY actual source code visible in the context
2. Include subsections with ### headers where appropriate
3. Include code examples ONLY from the actual repository source files
4. Include markdown tables ONLY if real data exists to populate them
5. Source citations MUST point to REAL files from the code context. Format: {hostBaseUrl}/{owner}/{repo}/blob/{commitHash}/path/to/file#L1-L50
6. Cross-references to other sections: [Section Title](/wiki/{owner}/{repo}/section-slug)
7. MERMAID SYNTAX: NEVER use parentheses inside square brackets. Use hyphens instead.
8. Include mermaid diagrams ONLY if they can accurately represent real components from the source code.

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
