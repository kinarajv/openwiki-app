using Microsoft.AspNetCore.Mvc;
using OpenWiki.Api.Services;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenWiki.Api.Services.Interfaces;

namespace OpenWiki.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly IGitHubService _gitHubService;
        private readonly ILogger<SearchController> _logger;

        public SearchController(IGitHubService gitHubService, ILogger<SearchController> logger)
        {
            _gitHubService = gitHubService;
            _logger = logger;
        }

        [HttpGet]
        [Route("/api/search")]
        public async Task<IActionResult> Get([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return Ok(new { results = Array.Empty<object>() });
            try
            {
                var response = await _gitHubService.SearchReposAsync(q);
                return Ok(new { results = response });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Search failed for query: {Query}", q);
                return Ok(new { results = Array.Empty<object>() });
            }
        }
    }
}