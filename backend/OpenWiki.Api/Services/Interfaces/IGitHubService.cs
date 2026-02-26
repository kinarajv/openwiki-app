using System.Collections.Generic;
using System.Threading.Tasks;
using OpenWiki.Api.DTOs.Responses;

namespace OpenWiki.Api.Services.Interfaces;

public interface IGitHubService
{
    Task<string> GetRepoMetadataAsync(string owner, string repo);
    Task<List<SearchResultItem>> SearchReposAsync(string query);
}
