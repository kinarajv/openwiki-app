using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using OpenWiki.Api.Data;
using OpenWiki.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace OpenWiki.Api.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        var mockGitHubService = new Mock<GitHubService>(new HttpClient(), new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        mockGitHubService.Setup(x => x.GetRepoMetadataAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("{\"name\": \"test\"}");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase("InMemoryDbForTesting");
                });

                var githubServiceDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(GitHubService));
                if (githubServiceDescriptor != null)
                {
                    services.Remove(githubServiceDescriptor);
                }
                services.AddSingleton(mockGitHubService.Object);
            });
        });
        
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("healthy");
    }

    [Fact]
    public async Task IngestEndpoint_ReturnsQueued()
    {
        // Act
        var response = await _client.PostAsync("/api/ingest?owner=test&repo=test-repo", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("queued");
        content.Should().Contain("test-repo");
    }
}
