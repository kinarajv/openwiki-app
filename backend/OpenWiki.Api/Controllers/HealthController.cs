using Microsoft.AspNetCore.Mvc;

namespace OpenWiki.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        [Route("/api/health")]
        public IActionResult Get()
        {
            return Ok(new { status = "healthy" });
        }
    }
}
