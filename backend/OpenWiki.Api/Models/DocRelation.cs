using System;

namespace OpenWiki.Api.Models;

public class DocRelation
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid FromSectionId { get; set; }
    public Guid ToSectionId { get; set; }
    public string RelationType { get; set; } = "references"; // references, uses, extends, implements
    public string? Description { get; set; }
}
