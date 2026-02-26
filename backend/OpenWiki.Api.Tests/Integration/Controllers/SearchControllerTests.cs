using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;

using OpenWiki.Api.Tests.Infrastructure;
using FluentAssertions;

namespace OpenWiki.Api.Tests.Integration.Controllers
{
    [TestFixture]
    public class SearchControllerTests
    {
        private WebAppFactory _factory;
        private HttpClient _client;

        [OneTimeSetUp]
        public async Task Setup()
        {
            _factory = new WebAppFactory();
            await _factory.InitializeAsync();
            _client = _factory.CreateClient();
        }

        [OneTimeTearDown]
        public async Task TearDown()
        {
            _client?.Dispose();
            if (_factory != null) await _factory.DisposeAsync();
        }
        [Test]
        public async Task Search_WithQuery_ReturnsOk()
        {
            var response = await _client.GetAsync("/api/search?q=test");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
