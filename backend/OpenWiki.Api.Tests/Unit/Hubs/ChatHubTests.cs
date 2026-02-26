using System.Net;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using OpenWiki.Api.Hubs;
using OpenWiki.Api.Services;

namespace OpenWiki.Api.Tests.Unit.Hubs;

[TestFixture]
public class ChatHubTests
{
    private Mock<IHttpClientFactory> _httpClientFactoryMock;
    private Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private HttpClient _httpClient;
    private Mock<AiClientService> _aiServiceMock;
    private Mock<ILogger<ChatHub>> _loggerMock;
    private Mock<IConfiguration> _configMock;
    private Mock<IHubCallerClients> _clientsMock;
    private Mock<ISingleClientProxy> _clientProxyMock;
    private ChatHub _hub;

    [SetUp]
    public void Setup()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);

        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        _aiServiceMock = new Mock<AiClientService>(MockBehavior.Loose, new HttpClient(), new Mock<IConfiguration>().Object, new Mock<ILogger<AiClientService>>().Object);
        _loggerMock = new Mock<ILogger<ChatHub>>();
        _configMock = new Mock<IConfiguration>();
        _configMock.Setup(c => c["Cliproxy:ApiUrl"]).Returns("http://testapi");
        _configMock.Setup(c => c["Cliproxy:FastModel"]).Returns("test-model");
        _configMock.Setup(c => c["OPENCLAW_API_KEY"]).Returns("test-key");

        _clientProxyMock = new Mock<ISingleClientProxy>();
        _clientsMock = new Mock<IHubCallerClients>();
        _clientsMock.Setup(c => c.Caller).Returns(_clientProxyMock.Object);

        _hub = new ChatHub(_aiServiceMock.Object, _httpClientFactoryMock.Object, _loggerMock.Object, _configMock.Object)
        {
            Clients = _clientsMock.Object
        };
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
        _hub?.Dispose();
    }

    [Test]
    public async Task AskQuestion_Success_SendsChunksAndComplete()
    {
        // Arrange
        var expectedResponse = "{\"choices\":[{\"message\":{\"content\":\"hello world\"}}]}";
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
        await _hub.AskQuestion("repo1", "mode1", "question?");

        // Assert
        _clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveChunk", It.Is<object[]>(o => o.Contains(" Analyzing codebase...\n\n")), default), Times.Once);
        _clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveChunk", It.Is<object[]>(o => o.Contains("hello ")), default), Times.Once);
        _clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveChunk", It.Is<object[]>(o => o.Contains("world ")), default), Times.Once);
        _clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveComplete", Array.Empty<object>(), default), Times.Once);
    }

    [Test]
    public async Task AskQuestion_ApiError_SendsErrorMessage()
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
        await _hub.AskQuestion("repo1", "mode1", "question?");

        // Assert
        _clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveChunk", It.Is<object[]>(o => o.Contains("Sorry, I couldn't connect to the AI service. Please check that cliproxy is running.")), default), Times.Once);
        _clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveComplete", Array.Empty<object>(), default), Times.Once);
    }
}
