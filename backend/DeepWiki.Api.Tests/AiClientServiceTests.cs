using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DeepWiki.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using Xunit;

namespace DeepWiki.Api.Tests;

public class AiClientServiceTests
{
    [Fact]
    public async Task GenerateDocSectionAsync_ShouldReturnGeneratedContent()
    {
        // Arrange
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        var mockResponse = "{\"choices\": [{\"message\": {\"content\": \"# Test Doc\\nThis is a test.\"}}]}";
        
        mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(mockResponse)
            });

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var inMemorySettings = new Dictionary<string, string> {
            {"Cliproxy:ApiUrl", "http://localhost"},
            {"Cliproxy:FastModel", "test-model"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var aiService = new AiClientService(httpClient, configuration);

        // Act
        var result = await aiService.GenerateDocSectionAsync("repo-metadata", "files-tree");

        // Assert
        result.Should().Be("# Test Doc\nThis is a test.");
    }

    [Fact]
    public async Task GenerateDocSectionAsync_ShouldThrowOnHttpError()
    {
        // Arrange
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        
        mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var inMemorySettings = new Dictionary<string, string> {
            {"Cliproxy:ApiUrl", "http://localhost"},
            {"Cliproxy:FastModel", "test-model"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var aiService = new AiClientService(httpClient, configuration);

        // Act & Assert
        await FluentActions.Invoking(() => aiService.GenerateDocSectionAsync("test", "test"))
            .Should().ThrowAsync<HttpRequestException>();
    }
}
