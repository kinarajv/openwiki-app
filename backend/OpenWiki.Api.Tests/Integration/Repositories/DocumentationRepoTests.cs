using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using OpenWiki.Api.Data;
using OpenWiki.Api.Models;
using OpenWiki.Api.Repositories;
using OpenWiki.Api.Tests.Infrastructure;

namespace OpenWiki.Api.Tests.Integration.Repositories;

[TestFixture]
public class DocumentationRepoTests : IntegrationTestBase
{
    private DocumentationRepo _documentationRepo = null!;

    [SetUp]
    public void SetUp()
    {
        _documentationRepo = new DocumentationRepo(DbContext);
    }

    [Test]
    public async Task AddSectionAsync_ShouldAddSection()
    {
        // Arrange
        var repo = new Repository
        {
            Owner = "docowner",
            Name = "docrepo",
            FullName = "docowner/docrepo",
            Description = "A test repository for doc",
            StarsCount = 10,
            Language = "C#",
            DocStatus = "completed",
            IndexedAt = DateTime.UtcNow
        };
        await DbContext.Repositories.AddAsync(repo);

        var doc = new Documentation
        {
            Repository = repo,
            Title = "docowner/docrepo Documentation",
            Status = "completed"
        };
        await DbContext.Documentations.AddAsync(doc);
        await DbContext.SaveChangesAsync();

        var section = new DocSection
        {
            DocumentationId = doc.Id,
            Title = "Getting Started",
            Slug = "getting-started",
            Level = 1,
            OrderIndex = 0,
            ContentMarkdown = "Start here",
            Summary = "Start summary",
            SectionType = "Guide"
        };

        // Act
        await _documentationRepo.AddSectionAsync(section);

        // Assert
        Assert.That(section.Id, Is.Not.EqualTo(Guid.Empty));
        var inDb = await DbContext.DocSections.FindAsync(section.Id);
        Assert.That(inDb, Is.Not.Null);
        Assert.That(inDb!.Title, Is.EqualTo("Getting Started"));
    }

    [Test]
    public async Task GetSectionsByRepoIdAsync_ShouldReturnOrderedSections()
    {
        // Arrange
        var repo = new Repository
        {
            Owner = "ordowner",
            Name = "ordrepo",
            FullName = "ordowner/ordrepo",
            Description = "Order test repo",
            StarsCount = 5,
            Language = "TypeScript",
            DocStatus = "completed",
            IndexedAt = DateTime.UtcNow
        };
        await DbContext.Repositories.AddAsync(repo);

        var doc = new Documentation
        {
            Repository = repo,
            Title = "ordowner/ordrepo Documentation",
            Status = "completed"
        };
        await DbContext.Documentations.AddAsync(doc);
        await DbContext.SaveChangesAsync();

        var section2 = new DocSection
        {
            Documentation = doc,
            Title = "Section 2",
            Slug = "section-2",
            Level = 1,
            OrderIndex = 1,
            ContentMarkdown = "Sec 2",
            Summary = "Sum 2",
            SectionType = "Guide"
        };

        var section1 = new DocSection
        {
            Documentation = doc,
            Title = "Section 1",
            Slug = "section-1",
            Level = 1,
            OrderIndex = 0,
            ContentMarkdown = "Sec 1",
            Summary = "Sum 1",
            SectionType = "Intro"
        };
        await DbContext.DocSections.AddRangeAsync(section1, section2);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _documentationRepo.GetSectionsByRepoIdAsync(repo.Id);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Title, Is.EqualTo("Section 1"));
        Assert.That(result[1].Title, Is.EqualTo("Section 2"));
    }
}