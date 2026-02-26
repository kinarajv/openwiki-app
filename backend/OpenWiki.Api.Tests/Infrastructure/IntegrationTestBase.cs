using Testcontainers.PostgreSql;
using Microsoft.EntityFrameworkCore;
using OpenWiki.Api.Data;

namespace OpenWiki.Api.Tests.Infrastructure;

public abstract class IntegrationTestBase
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("ankane/pgvector:v0.5.1")
        .Build();

    protected AppDbContext DbContext { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        DbContext = new AppDbContext(options);
        await DbContext.Database.EnsureCreatedAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await DbContext.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}