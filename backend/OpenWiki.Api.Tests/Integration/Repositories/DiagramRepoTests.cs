using NUnit.Framework;
using OpenWiki.Api.Models;
using OpenWiki.Api.Repositories;
using OpenWiki.Api.Tests.Infrastructure;

namespace OpenWiki.Api.Tests.Integration.Repositories;

[TestFixture]
public class DiagramRepoTests : IntegrationTestBase
{
    private DiagramRepo _diagramRepo;
    private RepositoryRepo _repositoryRepo;

    [SetUp]
    public void Setup()
    {
        _diagramRepo = new DiagramRepo(DbContext);
        _repositoryRepo = new RepositoryRepo(DbContext);
    }

    [Test]
    public async Task AddAsync_ValidDiagram_ReturnsCreatedDiagram()
    {
        // Arrange
        var repoId = Guid.NewGuid();
        var repository = new Repository 
        { 
            Id = repoId,
            Owner = "test-owner", 
            Name = "test-repo", 
            Description = "Test Repo", 
            StarsCount = 10, 
            Language = "C#"
        };
        await _repositoryRepo.AddAsync(repository);

        var diagram = new Diagram
        {
            Id = Guid.NewGuid(),
            RepositoryId = repoId,
            Title = "Architecture Diagram",
            DiagramType = "mermaid",
            Content = "graph TD\nA-->B"
        };

        // Act
        await _diagramRepo.AddAsync(diagram);
        await DbContext.SaveChangesAsync();

        // Assert
        var savedDiagram = await DbContext.Diagrams.FindAsync(diagram.Id);
        Assert.That(savedDiagram, Is.Not.Null);
        Assert.That(savedDiagram.Id, Is.EqualTo(diagram.Id));
        Assert.That(savedDiagram.RepositoryId, Is.EqualTo(repoId));
        Assert.That(savedDiagram.Title, Is.EqualTo("Architecture Diagram"));
        Assert.That(savedDiagram.DiagramType, Is.EqualTo("mermaid"));
        Assert.That(savedDiagram.Content, Is.EqualTo("graph TD\nA-->B"));
    }

    [Test]
    public async Task GetByRepoIdAsync_ValidRepoId_ReturnsDiagrams()
    {
        // Arrange
        var repoId = Guid.NewGuid();
        var repository = new Repository 
        { 
            Id = repoId,
            Owner = "test-owner2", 
            Name = "test-repo2", 
            Description = "Test Repo", 
            StarsCount = 10, 
            Language = "C#"
        };
        await _repositoryRepo.AddAsync(repository);
        await DbContext.SaveChangesAsync();

        var diagram1 = new Diagram
        {
            Id = Guid.NewGuid(),
            RepositoryId = repoId,
            Title = "Diagram 1",
            DiagramType = "mermaid",
            Content = "graph TD\nA-->B"
        };
        
        var diagram2 = new Diagram
        {
            Id = Guid.NewGuid(),
            RepositoryId = repoId,
            Title = "Diagram 2",
            DiagramType = "mermaid",
            Content = "graph TD\nC-->D"
        };
        
        await _diagramRepo.AddAsync(diagram1);
        await _diagramRepo.AddAsync(diagram2);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _diagramRepo.GetByRepoIdAsync(repoId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result.Any(d => d.Id == diagram1.Id), Is.True);
        Assert.That(result.Any(d => d.Id == diagram2.Id), Is.True);
    }
}
