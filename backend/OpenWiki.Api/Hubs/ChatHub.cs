using OpenWiki.Api.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using OpenWiki.Api.Services;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace OpenWiki.Api.Hubs;

public class ChatHub : Hub
{
    private readonly IAiClientService _aiService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChatHub> _logger;
    private readonly string _apiUrl;
    private readonly string _fastModel;
    private readonly string _apiKey;

    public ChatHub(IAiClientService aiService, IHttpClientFactory httpClientFactory, ILogger<ChatHub> logger, IConfiguration config)
    {
        _aiService = aiService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _apiUrl = config["Cliproxy:ApiUrl"]?.TrimEnd('/') ?? "http://localhost:8317";
        _fastModel = config["Cliproxy:FastModel"] ?? "gpt-5-codex-mini";
        _apiKey = config["OPENCLAW_API_KEY"] ?? "not-configured";
        if (string.IsNullOrEmpty(config["OPENCLAW_API_KEY"]))
            _logger.LogWarning("OPENCLAW_API_KEY is not configured. AI features will not work.");
    }
    public async Task AskQuestion(string repoId, string mode, string question)
    {
        if (Clients == null || Clients.Caller == null) return;

        if (string.IsNullOrEmpty(repoId)) return;
        
        await Clients.Caller.SendAsync("ReceiveChunk", " Analyzing codebase...\n\n");
        await Task.Delay(500);
        
        try
        {
            var prompt = $@"You are a technical assistant helping with questions about the {repoId} repository.

Question: {question}

Provide a helpful, accurate response based on the repository context. Be concise but thorough.";

            var request = new
            {
                model = _fastModel,
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful technical assistant for a code repository." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.5
            };

            var content = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");
            
            var requestMsg = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/v1/chat/completions");
            requestMsg.Content = content;
            requestMsg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            using var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.SendAsync(requestMsg);
            
            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(responseBody);
                
                var aiContent = document.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                // Stream the response word by word
                if (!string.IsNullOrEmpty(aiContent))
                {
                    foreach (var word in aiContent.Split(' '))
                    {
                        await Clients.Caller.SendAsync("ReceiveChunk", word + " ");
                        await Task.Delay(30);
                    }
                }
            }
            else
            {
                await Clients.Caller.SendAsync("ReceiveChunk", "Sorry, I couldn't connect to the AI service. Please check that cliproxy is running.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChatHub for repo {RepoId}: {Message}", repoId, ex.Message);
            await Clients.Caller.SendAsync("ReceiveChunk", $"Error: {ex.Message}");
        }
        
        await Clients.Caller.SendAsync("ReceiveComplete");
    }
}

