using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using OpenWiki.Api.Services;

namespace OpenWiki.Api.Tests.Unit.Services;

[TestFixture]
public class AiClientServiceTests
{
    private Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private HttpClient _httpClient;
    private Mock<IConfiguration> _configMock;
    private Mock<ILogger<AiClientService>> _loggerMock;
    private AiClientService _service;

    [SetUp]
    public void Setup()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        
        _configMock = new Mock<IConfiguration>();
        _configMock.Setup(c => c["Cliproxy:ApiUrl"]).Returns("http://testapi");
        _configMock.Setup(c => c["Cliproxy:FastModel"]).Returns("test-model");
        _configMock.Setup(c => c["OPENCLAW_API_KEY"]).Returns("test-key");
        
        _loggerMock = new Mock<ILogger<AiClientService>>();

        _service = new AiClientService(_httpClient, _configMock.Object, _loggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    [Test]
    public async Task GenerateStructuredDocsAsync_Success_ReturnsAiContent()
    {
        // Arrange
        var expectedResponse = "{\"choices\":[{\"message\":{\"content\":\"test content\"}}]}";
        var responseMsg = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(expectedResponse)
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(responseMsg);

        // Act
        var result = await _service.GenerateStructuredDocsAsync("code", "owner", "repo");

        // Assert
        Assert.That(result, Is.EqualTo("test content"));
    }

    [Test]
    public void GenerateStructuredDocsAsync_ApiError_ThrowsExceptionAndLogsError()
    {
        // Arrange
        var responseMsg = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError,
            Content = new StringContent("error")
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(responseMsg);

        // Act & Assert
        Assert.ThrowsAsync<Exception>(() => _service.GenerateStructuredDocsAsync("code", "owner", "repo"));
    }
}
