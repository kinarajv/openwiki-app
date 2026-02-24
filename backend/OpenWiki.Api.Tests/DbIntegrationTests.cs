using System.Threading.Tasks;
using OpenWiki.Api.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace OpenWiki.Api.Tests;

public class DbIntegrationTests 
{
    [Fact]
    public async Task AppDbContext_CanConnectAndSaveData_InMemory()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "OpenWikiTestDb")
            .Options;

        using var context = new AppDbContext(options);

        var repo = new Repository
        {
            Owner = "test",
            Name = "test-repo",
            FullName = "test/test-repo",
            StarsCount = 100
        };

        // Act
        context.Repositories.Add(repo);
        await context.SaveChangesAsync();

        var savedRepo = await context.Repositories.FirstOrDefaultAsync(r => r.FullName == "test/test-repo");

        // Assert
        savedRepo.Should().NotBeNull();
        savedRepo!.StarsCount.Should().Be(100);
    }
}
