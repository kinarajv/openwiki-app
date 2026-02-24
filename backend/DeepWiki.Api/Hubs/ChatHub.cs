using Microsoft.AspNetCore.SignalR;
using DeepWiki.Api.Services;
using System.Text.Json;

namespace DeepWiki.Api.Hubs;

public class ChatHub : Hub
{
    private readonly AiClientService _aiService;
    private readonly IServiceProvider _serviceProvider;

    public ChatHub(AiClientService aiService, IServiceProvider serviceProvider)
    {
        _aiService = aiService;
        _serviceProvider = serviceProvider;
    }

    public async Task AskQuestion(string repoId, string mode, string question)
    {
        if (Clients == null || Clients.Caller == null) return;
        
        await Clients.Caller.SendAsync("ReceiveChunk", "🧠 Analyzing codebase...\n\n");
        await Task.Delay(500);
        
        try
        {
            var prompt = $@"You are a technical assistant helping with questions about the {repoId} repository.

Question: {question}

Provide a helpful, accurate response based on the repository context. Be concise but thorough.";

            var request = new
            {
                model = "gpt-5-codex-mini",
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful technical assistant for a code repository." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.5
            };

            var content = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");
            
            var requestMsg = new HttpRequestMessage(HttpMethod.Post, "http://localhost:8317/v1/chat/completions");
            requestMsg.Content = content;
            requestMsg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "sk-apikey");

            using var httpClient = new HttpClient();
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
            await Clients.Caller.SendAsync("ReceiveChunk", $"Error: {ex.Message}");
        }
        
        await Clients.Caller.SendAsync("ReceiveComplete");
    }
}
