using System;
using System.Collections.Generic;

namespace OpenWiki.Api.Models;

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
