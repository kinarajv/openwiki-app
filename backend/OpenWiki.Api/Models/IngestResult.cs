using System.Collections.Generic;

namespace OpenWiki.Api.Models;

public class IngestResult
{
    public string Overview { get; set; } = "";
    public List<SectionResult> Sections { get; set; } = new();
    public List<RelationResult> Relations { get; set; } = new();
    public List<DiagramResult> Diagrams { get; set; } = new();
}

public class SectionResult
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Summary { get; set; } = "";
    public int Level { get; set; } = 2;
    public string SectionType { get; set; } = "content";
    public List<string> RelatedFiles { get; set; } = new();
}

public class RelationResult
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Type { get; set; } = "references";
    public string? Description { get; set; }
}

public class DiagramResult
{
    public string Title { get; set; } = "";
    public string Type { get; set; } = "mermaid";
    public string Content { get; set; } = "";
}
