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
        // Arrange
        var json = @"
        {
            ""overview"": ""Test Overview"",
            ""sections"": [
                { ""title"": ""Section 1"", ""content"": ""Content 1"" }
            ],
            ""relations"": [],
            ""diagrams"": [
                { ""title"": ""Diag 1"", ""content"": ""A -> B"" }
            ]
        }";
        var files = new System.Collections.Generic.List<string> { "file1.cs" };

        var methodInfo = typeof(GitHubIngestService).GetMethod("ParseAiResponse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var result = (IngestResult)methodInfo.Invoke(_service, new object[] { json, files });

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Overview, Is.EqualTo("Test Overview"));
        Assert.That(result.Sections.Count, Is.EqualTo(2)); // Overview + Section 1
        Assert.That(result.Diagrams.Count, Is.EqualTo(1));
        Assert.That(result.Diagrams[0].Title, Is.EqualTo("Diag 1"));
    }

    [Test]
    public void ProcessRepositoryAsync_GitCloneFails_ThrowsException()
    {
        // Act & Assert
        var ex = Assert.ThrowsAsync<Exception>(() => _service.ProcessRepositoryAsync("invalid-owner-xyz-123", "invalid-repo-xyz-123"));
        Assert.That(ex.Message, Does.Contain("Git clone failed"));
    }
}
