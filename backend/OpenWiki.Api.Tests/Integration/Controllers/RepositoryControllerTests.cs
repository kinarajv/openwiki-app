using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenWiki.Api.Tests.Integration.Controllers
{
    [TestFixture]
    public class RepositoryControllerTests
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
        public async Task CheckRepo_ReturnsOk()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/api/repo/check?owner=test&repo=test");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
    }
}
