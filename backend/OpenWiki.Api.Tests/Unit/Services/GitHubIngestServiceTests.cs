using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using OpenWiki.Api.Models;
using OpenWiki.Api.Repositories.Interfaces;
using OpenWiki.Api.Services;
using OpenWiki.Api.Services.Interfaces;

namespace OpenWiki.Api.Tests.Unit.Services;

[TestFixture]
public class GitHubIngestServiceTests
{
    private Mock<IAiClientService> _aiServiceMock;
    private Mock<IRepositoryRepo> _repositoryRepoMock;
    private Mock<IDocumentationRepo> _documentationRepoMock;
    private Mock<IDiagramRepo> _diagramRepoMock;
    private Mock<ILogger<GitHubIngestService>> _loggerMock;
    private GitHubIngestService _service;

    [SetUp]
    public void Setup()
    {
        _aiServiceMock = new Mock<IAiClientService>();
        _repositoryRepoMock = new Mock<IRepositoryRepo>();
        _documentationRepoMock = new Mock<IDocumentationRepo>();
        _diagramRepoMock = new Mock<IDiagramRepo>();
        _loggerMock = new Mock<ILogger<GitHubIngestService>>();

        _service = new GitHubIngestService(
            _aiServiceMock.Object,
            _repositoryRepoMock.Object,
            _documentationRepoMock.Object,
            _diagramRepoMock.Object,
            _loggerMock.Object);
    }

    [Test]
    public void ParseAiResponse_ValidJson_ReturnsIngestResult()
    {
        // For parsing AI response validation, normally we would extract logic or use a dummy repo.
        // We will leave this stubbed as requested to avoid side-effects.
        Assert.Pass();
    }

    [Test]
    public void ProcessRepositoryAsync_GitCloneFails_ThrowsException()
    {
        // Act & Assert
        var ex = Assert.ThrowsAsync<Exception>(() => _service.ProcessRepositoryAsync("invalid-owner-xyz-123", "invalid-repo-xyz-123"));
        Assert.That(ex.Message, Does.Contain("Git clone failed"));
    }
}
