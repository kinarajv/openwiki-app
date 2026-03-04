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
        _apiKey = config["Cliproxy:ApiKey"] ?? "not-configured";
        if (string.IsNullOrEmpty(config["Cliproxy:ApiKey"]))
            _logger.LogWarning("Cliproxy:ApiKey is not configured. AI chat features will not work.");
    }

    public async Task AskQuestion(string repoId, string mode, string question)
    {
        if (Clients == null || Clients.Caller == null) return;

        if (string.IsNullOrEmpty(repoId)) return;
        
        await Clients.Caller.SendAsync("ReceiveChunk", " Analyzing codebase...\n\n");

        try
        {
            var prompt = $@"You are a technical assistant that ONLY answers questions about the {repoId} repository and its codebase.

Question: {question}

RULES:
- ONLY answer questions directly related to this repository's code, architecture, documentation, dependencies, or technical details.
- If the question is NOT about this repository (e.g., general coding questions, personal questions, requests to write unrelated code, or off-topic conversations), respond EXACTLY with:
  ""I can only answer questions about the **{repoId}** repository. Please ask something related to this project's codebase or documentation.""
- Do NOT write code that is unrelated to this repository.
- Do NOT answer general knowledge questions.
- Be concise but thorough when answering relevant questions.";

            var requestBody = new
            {
                model = _fastModel,
                stream = true,
                messages = new[]
                {
                    new { role = "system", content = $"You are a documentation assistant exclusively for the {repoId} repository. You must REFUSE to answer any question that is not directly about this repository's source code, architecture, features, dependencies, or documentation. If a user asks anything off-topic (general programming, personal questions, unrelated code generation), politely decline and remind them you only discuss this specific repository." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.5
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");
            
            var requestMsg = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/v1/chat/completions");
            requestMsg.Content = content;
            requestMsg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(Context.ConnectionAborted);
            cts.CancelAfter(TimeSpan.FromSeconds(60));

            using var response = await httpClient.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                using var reader = new System.IO.StreamReader(stream);

                while (!reader.EndOfStream && !cts.Token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cts.Token);

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line == "data: [DONE]" || line == "data:[DONE]")
                        break;

                    var jsonStr = line.StartsWith("data: ") ? line.Substring(6)
                                : line.StartsWith("data:") ? line.Substring(5)
                                : null;

                    if (jsonStr == null)
                        continue;

                    try
                    {
                        using var doc = JsonDocument.Parse(jsonStr);
                        var choices = doc.RootElement.GetProperty("choices");
                        if (choices.GetArrayLength() > 0)
                        {
                            var delta = choices[0].GetProperty("delta");
                            if (delta.TryGetProperty("content", out var contentProp))
                            {
                                var chunk = contentProp.GetString();
                                if (!string.IsNullOrEmpty(chunk))
                                {
                                    await Clients.Caller.SendAsync("ReceiveChunk", chunk, cts.Token);
                                }
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse SSE chunk: {Line}", line);
                    }
                }
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
                _logger.LogError("Cliproxy API returned {StatusCode}: {Body}", response.StatusCode, errorBody);
                await Clients.Caller.SendAsync("ReceiveChunk", "Sorry, I couldn't connect to the AI service. Please check that cliproxy is running.");
            }
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Client disconnected during streaming for repo {RepoId}", repoId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Stream read timed out for repo {RepoId}", repoId);
            await Clients.Caller.SendAsync("ReceiveChunk", "\n\n*Response timed out. Please try again.*");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChatHub for repo {RepoId}: {Message}", repoId, ex.Message);
            await Clients.Caller.SendAsync("ReceiveChunk", $"Error: {ex.Message}");
        }
        finally
        {
            await Clients.Caller.SendAsync("ReceiveComplete");
        }
    }
}
