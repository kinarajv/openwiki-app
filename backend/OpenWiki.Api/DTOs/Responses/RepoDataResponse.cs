using System;
using System.Collections.Generic;

namespace OpenWiki.Api.DTOs.Responses;

public record RepoDataResponse(
    string Owner,
    string Repo,
    string Document,
    RepoMetadata Metadata,
    List<RepoSection> Sections,
    List<RepoRelation> Relations,
    List<RepoDiagram> Diagrams
);

public record RepoMetadata(
    int Stars,
    string? Language,
    string? Description
);

public record RepoSection(
    Guid Id,
    string Title,
    string Slug,
    int Level,
    string Type,
    string? Summary
);

public record RepoRelation(
    string? From,
    string? To,
    string Type,
    string? Description
);

public record RepoDiagram(
    string Title,
    string Type,
    string Content
);
