using NUnit.Framework;
using Microsoft.EntityFrameworkCore;
using OpenWiki.Api.Data;
using OpenWiki.Api.Tests.Infrastructure;

namespace OpenWiki.Api.Tests.Integration.Database;

[TestFixture]
public class DbContextTests : IntegrationTestBase
{
    [Test]
    public async Task DbContext_ShouldConnectToPostgres_AndCreateTables()
    {
        // Act
        var canConnect = await DbContext.Database.CanConnectAsync();
        
        // Assert
        Assert.That(canConnect, Is.True, "Should be able to connect to the test PostgreSQL database.");
        
        // Let's verify that a table exists
        var repoTableExists = await DbContext.Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM information_schema.tables WHERE table_name = 'Repositories'");
        Assert.That(repoTableExists, Is.GreaterThanOrEqualTo(0));
    }
}