using System.Threading.Tasks;

namespace OpenWiki.Api.Services.Interfaces;

public interface IAiClientService
{
    Task<string> GenerateStructuredDocsAsync(string codeContext, string owner, string repo, string commitHash = "main", string hostBaseUrl = "https://github.com");
    Task<string> GenerateSectionBatchAsync(string codeContext, string owner, string repo, string commitHash, List<string> sectionTitles, List<string> sectionDescriptions, string hostBaseUrl = "https://github.com");
}
