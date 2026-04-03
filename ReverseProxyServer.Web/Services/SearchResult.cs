namespace ReverseProxyServer.Web.Services;

public class SearchResult
{
    // FTS fields (populated immediately)
    public long Id { get; init; }
    public string SessionId { get; init; } = string.Empty;
    public string Data { get; init; } = string.Empty;
    public string Snippet { get; init; } = string.Empty;
    public double RelevanceScore { get; init; }

    // Highlighted full data (lazy-loaded on expand)
    public string? HighlightedData { get; set; }

    // Metadata (lazy-loaded per card)
    public bool MetadataLoaded { get; set; }
    public string CommunicationDirection { get; set; } = string.Empty;
    public long DataSize { get; set; }
    public DateTime? ConnectionTime { get; set; }
    public string ProxyType { get; set; } = string.Empty;
    public string RemoteAddress { get; set; } = string.Empty;
    public long RemotePort { get; set; }
    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }
}
