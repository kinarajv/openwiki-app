using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using OpenWiki.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using Xunit;

namespace OpenWiki.Api.Tests;

public class GitHubServiceTests
{
    [Fact]
    public async Task GetRepoMetadataAsync_ShouldReturnJsonString()
    {
        // Arrange
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        var mockResponse = "{\"name\": \"test-repo\", \"stargazers_count\": 100}";
        
        mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is(
                    (HttpRequestMessage req) => req.RequestUri!.ToString() == "https://api.github.com/repos/test/test-repo"
                ),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(mockResponse)
            });

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var inMemorySettings = new Dictionary<string, string> {
            {"GITHUB_API_TOKEN", "test-token"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var githubService = new GitHubService(httpClient, configuration);

        // Act
        var result = await githubService.GetRepoMetadataAsync("test", "test-repo");

        // Assert
        result.Should().Be("{\"name\": \"test-repo\", \"stargazers_count\": 100}");
    }
}
