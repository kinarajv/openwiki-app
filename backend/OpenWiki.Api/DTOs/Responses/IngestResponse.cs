namespace OpenWiki.Api.DTOs.Responses;

public record IngestResponse(
    string Status,
    string Owner,
    string Repo,
    string Document,
    RepoMetadata Metadata,
    int Sections,
    int Relations,
    int Diagrams,
    string? Message = null
);
