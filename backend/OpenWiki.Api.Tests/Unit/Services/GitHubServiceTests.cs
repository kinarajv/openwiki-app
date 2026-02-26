using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using OpenWiki.Api.DTOs.Responses;
using OpenWiki.Api.Services;

namespace OpenWiki.Api.Tests.Unit.Services;

[TestFixture]
public class GitHubServiceTests
{
    private Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private HttpClient _httpClient;
    private Mock<IConfiguration> _configMock;
    private Mock<ILogger<GitHubService>> _loggerMock;
    private GitHubService _service;

    [SetUp]
    public void Setup()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _configMock = new Mock<IConfiguration>();
        _configMock.Setup(c => c["GITHUB_API_TOKEN"]).Returns("test-token");
        _loggerMock = new Mock<ILogger<GitHubService>>();

        _service = new GitHubService(_httpClient, _configMock.Object, _loggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    [Test]
    public async Task GetRepoMetadataAsync_Success_ReturnsMetadata()
    {
        // Arrange
        var expectedContent = "{\"stargazers_count\": 10, \"language\": \"C#\"}";
        var responseMsg = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(expectedContent)
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(responseMsg);

        // Act
        var result = await _service.GetRepoMetadataAsync("testowner", "testrepo");

        // Assert
        Assert.That(result, Is.EqualTo(expectedContent));
    }

    [Test]
    public async Task SearchReposAsync_Success_ReturnsSearchResults()
    {
        // Arrange
        var expectedContent = "{\"items\":[{\"full_name\":\"testowner/testrepo\",\"description\":\"desc\",\"stargazers_count\":42,\"language\":\"C#\"}]}";
        var responseMsg = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(expectedContent)
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri.ToString().Contains("search/repositories")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(responseMsg);

        // Act
        var results = await _service.SearchReposAsync("test query");

        // Assert
        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].FullName, Is.EqualTo("testowner/testrepo"));
        Assert.That(results[0].Description, Is.EqualTo("desc"));
        Assert.That(results[0].Stars, Is.EqualTo(42));
        Assert.That(results[0].Language, Is.EqualTo("C#"));
    }

    [Test]
    public async Task SearchReposAsync_Failure_ReturnsEmptyList()
    {
        // Arrange
        var responseMsg = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(responseMsg);

        // Act
        var results = await _service.SearchReposAsync("test query");

        // Assert
        Assert.That(results, Is.Not.Null);
        Assert.That(results, Is.Empty);
    }


    [Test]
    public async Task SearchReposAsync_NullOrEmptyQuery_ReturnsEmptyList()
    {
        var result = await _service.SearchReposAsync("");
        result.Should().BeEmpty();
    }

}
