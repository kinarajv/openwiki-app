using System;

namespace OpenWiki.Api.DTOs.Responses;

public record RepoCheckResponse(bool Indexed, DateTime? IndexedAt = null, int SectionsCount = 0);
