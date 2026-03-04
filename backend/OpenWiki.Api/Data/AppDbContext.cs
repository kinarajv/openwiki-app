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
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Repository>()
            .HasKey(r => r.Id);
        modelBuilder.Entity<Repository>()
            .HasIndex(r => r.FullName).IsUnique();

        modelBuilder.Entity<Documentation>()
            .HasKey(d => d.Id);
        modelBuilder.Entity<Documentation>()
            .HasOne(d => d.Repository)
            .WithMany(r => r.Documentations)
            .HasForeignKey(d => d.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocSection>()
            .HasKey(s => s.Id);
        modelBuilder.Entity<DocSection>()
            .HasOne(s => s.Documentation)
            .WithMany(d => d.Sections)
            .HasForeignKey(s => s.DocumentationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocRelation>()
            .HasKey(r => r.Id);

        modelBuilder.Entity<CodeFile>()
            .HasKey(c => c.Id);

        modelBuilder.Entity<Diagram>()
            .HasKey(d => d.Id);

        modelBuilder.Entity<AiConversation>()
            .HasKey(c => c.Id);
        modelBuilder.Entity<AiConversation>()
            .HasOne(c => c.Repository)
            .WithMany()
            .HasForeignKey(c => c.RepositoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ChatMessage>()
            .HasKey(m => m.Id);
        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
