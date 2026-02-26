using Microsoft.EntityFrameworkCore;

namespace OpenWiki.Api.Data;

using OpenWiki.Api.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Repository> Repositories { get; set; }
    public DbSet<Documentation> Documentations { get; set; }
    public DbSet<DocSection> DocSections { get; set; }
    public DbSet<DocRelation> DocRelations { get; set; }
    public DbSet<CodeFile> CodeFiles { get; set; }
    public DbSet<Diagram> Diagrams { get; set; }
    public DbSet<AiConversation> AiConversations { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
}
