using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using OpenWiki.Api.Data;
using OpenWiki.Api.Models;
using OpenWiki.Api.Repositories;
using OpenWiki.Api.Tests.Infrastructure;

namespace OpenWiki.Api.Tests.Integration.Repositories;

[TestFixture]
public class RepositoryRepoTests : IntegrationTestBase
{
    private RepositoryRepo _repositoryRepo = null!;

    [SetUp]
    public void SetUp()
    {
        _repositoryRepo = new RepositoryRepo(DbContext);
    }

    [Test]
    public async Task AddAsync_ShouldAddRepository()
    {
        // Arrange
        var repo = new Repository
        {
            Owner = "testowner",
            Name = "testrepo",
            FullName = "testowner/testrepo",
            Description = "A test repository",
            StarsCount = 10,
            Language = "C#",
            DocStatus = "completed",
            IndexedAt = DateTime.UtcNow
        };

        // Act
        var addedRepo = await _repositoryRepo.AddAsync(repo);

        // Assert
        Assert.That(addedRepo.Id, Is.Not.EqualTo(Guid.Empty));
        var inDb = await DbContext.Repositories.FindAsync(addedRepo.Id);
        Assert.That(inDb, Is.Not.Null);
        Assert.That(inDb!.FullName, Is.EqualTo("testowner/testrepo"));
    }

    [Test]
    public async Task GetByFullNameAsync_ShouldReturnRepositoryWithDocumentationsAndSections()
    {
        // Arrange
        var repo = new Repository
        {
            Owner = "getowner",
            Name = "getrepo",
            FullName = "getowner/getrepo",
            Description = "Test desc",
            StarsCount = 5,
            Language = "TypeScript",
            DocStatus = "completed",
            IndexedAt = DateTime.UtcNow
        };
        await DbContext.Repositories.AddAsync(repo);

        var documentation = new Documentation
        {
            Repository = repo,
            Title = "getowner/getrepo Documentation",
            Status = "completed"
        };
        await DbContext.Documentations.AddAsync(documentation);

        var section = new DocSection
        {
            Documentation = documentation,
            Title = "Introduction",
            Slug = "introduction",
            Level = 1,
            OrderIndex = 0,
            ContentMarkdown = "Welcome to testrepo",
            Summary = "Intro summary",
            SectionType = "Overview"
        };
        await DbContext.DocSections.AddAsync(section);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _repositoryRepo.GetByFullNameAsync("getowner/getrepo");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.FullName, Is.EqualTo("getowner/getrepo"));
        Assert.That(result.Documentations, Has.Count.EqualTo(1));
        var doc = result.Documentations.First();
        Assert.That(doc.Sections, Has.Count.EqualTo(1));
        var sec = doc.Sections.First();
        Assert.That(sec.Title, Is.EqualTo("Introduction"));
    }
}