using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenWiki.Api.Tests.Integration.Controllers
{
    [TestFixture]
    public class HealthControllerTests
    {
        private WebApplicationFactory<Program> _factory;

        [SetUp]
        public void Setup()
        {
            _factory = new WebApplicationFactory<Program>();
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
        }

        [Test]
        public async Task GetHealth_ReturnsHealthyStatus()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/api/health");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var content = await response.Content.ReadAsStringAsync();
            Assert.That(content, Does.Contain("\"status\":\"healthy\""));
        }
    }
}
