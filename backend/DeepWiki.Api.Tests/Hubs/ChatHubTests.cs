using System.Threading;
using System.Threading.Tasks;
using DeepWiki.Api.Hubs;
using DeepWiki.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using System.Net.Http;
using System.Collections.Generic;

namespace DeepWiki.Api.Tests.Hubs;

public class ChatHubTests
{
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<ISingleClientProxy> _mockClientProxy;
    private readonly Mock<HubCallerContext> _mockContext;

    public ChatHubTests()
    {
        _mockClients = new Mock<IHubCallerClients>();
        _mockClientProxy = new Mock<ISingleClientProxy>();
        _mockContext = new Mock<HubCallerContext>();
        
        _mockClients.Setup(c => c.Caller).Returns(_mockClientProxy.Object);
    }

    [Fact]
    public async Task AskQuestion_ShouldStreamChunksAndComplete()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string> {
            {"Cliproxy:ApiUrl", "http://localhost"},
            {"Cliproxy:FastModel", "test-model"}
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();
        
        var mockAiService = new Mock<AiClientService>(new HttpClient(), config);
        mockAiService.Setup(s => s.GenerateDocSectionAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Mocked response");
            
        var hub = new ChatHub(mockAiService.Object)
        {
            Clients = _mockClients.Object,
            Context = _mockContext.Object
        };

        // Act
        await hub.AskQuestion("test-repo", "fast", "What is this?");

        // Assert
        _mockClientProxy.Verify(
            c => c.SendCoreAsync("ReceiveChunk", It.Is<object[]>(o => o != null && o.Length == 1), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce
        );

        _mockClientProxy.Verify(
            c => c.SendCoreAsync("ReceiveComplete", It.Is<object[]>(o => o != null && o.Length == 0), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
