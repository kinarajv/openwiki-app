using System;
using System.Collections.Generic;

namespace OpenWiki.Api.Models;

public class Repository
{
    public Guid Id { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int StarsCount { get; set; }
    public string? Language { get; set; }
    public string CommitHash { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = "main";
    public string DocStatus { get; set; } = "pending";
    public string SourceUrl { get; set; } = string.Empty;  // Full clone URL (e.g., https://gitlab.mycompany.com/team/project)
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Documentation> Documentations { get; set; } = new List<Documentation>();
}
