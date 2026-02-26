using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenWiki.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using OpenWiki.Api.Data;
using System.Linq;

namespace OpenWiki.Api.Tests.Integration.Controllers
{
    [TestFixture]
    public class RepositoryControllerTests
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
        public async Task CheckRepo_ReturnsOk()
        {
            var response = await _client.GetAsync("/api/repo/check?owner=test&repo=test");

            var content = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
