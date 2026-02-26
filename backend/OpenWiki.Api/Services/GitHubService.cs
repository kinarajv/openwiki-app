using System.Net.Http.Headers;
using System.Text.Json;
using OpenWiki.Api.DTOs.Responses;
using OpenWiki.Api.Services.Interfaces;

namespace OpenWiki.Api.Services;

public class GitHubService : IGitHubService
{
    private readonly HttpClient _httpClient;
    private readonly string? _token;

    public GitHubService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://api.github.com/");
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OpenWiki", "1.0"));
        
        _token = config["GITHUB_API_TOKEN"];
        if (!string.IsNullOrEmpty(_token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }
    }

    public virtual async Task<string> GetRepoMetadataAsync(string owner, string repo)
    {
        var response = await _httpClient.GetAsync($"repos/{owner}/{repo}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public virtual async Task<List<SearchResultItem>> SearchReposAsync(string query)
    {
        try
        {
            var response = await _httpClient.GetAsync($"search/repositories?q={Uri.EscapeDataString(query)}&per_page=10");
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            
            var results = new List<SearchResultItem>();
            if (doc.RootElement.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    results.Add(new SearchResultItem(
                        item.GetProperty("full_name").GetString(),
                        item.TryGetProperty("description", out var desc) ? desc.GetString() : "",
                        item.GetProperty("stargazers_count").GetInt32(),
                        item.TryGetProperty("language", out var lang) ? lang.GetString() : "Unknown"
                    ));
                }
            }
            return results;
        }
        catch
        {
            return new List<SearchResultItem>();
        }
    }
}
