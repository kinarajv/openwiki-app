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
        _apiKey = config["OPENCLAW_API_KEY"] ?? "not-configured"; 
        _logger = logger;
    }

    public virtual async Task<string> GenerateStructuredDocsAsync(string codeContext, string owner, string repo)
    {

        if (string.IsNullOrEmpty(codeContext)) throw new ArgumentException("codeContext cannot be null or empty", nameof(codeContext));

        {
            var systemPrompt = @"You are an expert software architect and technical writer. You must analyze the provided source code and generate COMPREHENSIVE documentation in JSON format.

IMPORTANT: You MUST output valid JSON with this EXACT structure:
{
  ""overview"": ""A high-level overview of what this repository does..."",
  ""sections"": [
    {
      ""title"": ""Core Architecture"",
      ""content"": ""Detailed markdown content about the architecture..."",
      ""summary"": ""Brief 1-2 sentence summary"",
      ""level"": 2,
      ""type"": ""architecture"",
      ""files"": [""src/main.ts"", ""src/app.ts""]
    }
  ],
  ""relations"": [
    {
      ""from"": ""Entry Points"",
      ""to"": ""Core Architecture"",
      ""type"": ""initializes"",
      ""description"": ""Entry point initializes the core architecture""
    }
  ],
  ""diagrams"": [
    {
      ""title"": ""Architecture Overview"",
      ""type"": ""mermaid"",
      ""content"": ""graph TD\n    A[Entry] --> B[Core]""
    }
  ]
}

Generate 5-10 detailed sections covering different aspects of the codebase.
Each section should have rich markdown content with code examples where relevant.
Create meaningful relations between sections showing how they interact.
Generate 2-3 Mermaid diagrams showing architecture, data flow, and relationships.

Output ONLY the JSON object, no other text.";

            var request = new
            {
                model = _fastModel,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = codeContext }
                },
                temperature = 0.3,
                max_tokens = 8192
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
        catch (Exception ex) 
        {
            _logger.LogError(ex, "=== AI CONNECTION FAILED ===\nError: {Message}\n============================\n", ex.Message);
            throw; 
        }
    }
}

