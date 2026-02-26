using System.Threading.Tasks;

namespace OpenWiki.Api.Services.Interfaces;

public interface IAiClientService
{
    Task<string> GenerateStructuredDocsAsync(string codeContext, string owner, string repo);
}
