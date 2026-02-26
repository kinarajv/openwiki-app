using System;

namespace OpenWiki.Api.Models;

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
