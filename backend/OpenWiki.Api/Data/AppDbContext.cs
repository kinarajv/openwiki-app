using Microsoft.EntityFrameworkCore;

namespace OpenWiki.Api.Data;

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

public class Repository
{
    public Guid Id { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int StarsCount { get; set; }
    public string? Language { get; set; }
    public string DocStatus { get; set; } = "pending";
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Documentation> Documentations { get; set; } = new List<Documentation>();
}

public class Documentation
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Repository? Repository { get; set; }
    public string? Title { get; set; }
    public string Status { get; set; } = "generating";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<DocSection> Sections { get; set; } = new List<DocSection>();
}

public class DocSection
{
    public Guid Id { get; set; }
    public Guid DocumentationId { get; set; }
    public Documentation? Documentation { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int Level { get; set; }
    public int OrderIndex { get; set; }
    public string? ContentMarkdown { get; set; }
    public string? Summary { get; set; }
    public string SectionType { get; set; } = "content"; // content, diagram, code, api, model
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DocRelation
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid FromSectionId { get; set; }
    public Guid ToSectionId { get; set; }
    public string RelationType { get; set; } = "references"; // references, uses, extends, implements
    public string? Description { get; set; }
}

public class CodeFile
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? Summary { get; set; }
    public string? Language { get; set; }
    public int LineCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Diagram
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid? SectionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DiagramType { get; set; } = "mermaid"; // mermaid, plantuml
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AiConversation
{
    public Guid Id { get; set; }
    public Guid? RepositoryId { get; set; }
    public Repository? Repository { get; set; }
    public string Mode { get; set; } = "fast";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public AiConversation? Conversation { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
