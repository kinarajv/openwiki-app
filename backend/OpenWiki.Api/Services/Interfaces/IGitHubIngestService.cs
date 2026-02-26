using System.Threading.Tasks;
using OpenWiki.Api.Models;

namespace OpenWiki.Api.Services.Interfaces;

public interface IGitHubIngestService
{
    Task<IngestResult> ProcessRepositoryAsync(string owner, string repo);
}
