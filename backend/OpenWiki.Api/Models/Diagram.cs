using System;

namespace OpenWiki.Api.Models;

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
