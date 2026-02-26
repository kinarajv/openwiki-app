using System;

namespace OpenWiki.Api.Models;

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
