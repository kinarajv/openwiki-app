using Microsoft.AspNetCore.Mvc;
using OpenWiki.Api.Services;
using System;
using System.Threading.Tasks;

namespace OpenWiki.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly GitHubService _gitHubService;

        public SearchController(GitHubService gitHubService)
        {
            _gitHubService = gitHubService;
        }

        [HttpGet]
        [Route("/api/search")]
        public async Task<IActionResult> Get([FromQuery] string q)
        {
            try
            {
                var response = await _gitHubService.SearchReposAsync(q);
                return Ok(new { results = response });
            }
            catch
            {
                return Ok(new { results = Array.Empty<object>() });
            }
        }
    }
}
