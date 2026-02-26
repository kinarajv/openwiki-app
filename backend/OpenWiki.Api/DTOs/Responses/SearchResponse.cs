using System.Collections.Generic;

namespace OpenWiki.Api.DTOs.Responses;

public record SearchResponse(List<SearchResultItem> Results);

public record SearchResultItem(string FullName, string? Description, int Stars, string? Language);
