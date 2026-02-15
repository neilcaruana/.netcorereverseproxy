using ReverseProxyServer.Data.DTO;

namespace ReverseProxyServer.Web.Services;

public interface IDashboardDataService
{
    // Stats methods - all filtered by date range
    Task<ConnectionStats> GetConnectionStatsAsync(DateTime fromDate, DateTime toDate);
    Task<long> GetUniqueIPsCountAsync(DateTime fromDate, DateTime toDate);
    Task<long> GetUniquePortsCountAsync(DateTime fromDate, DateTime toDate);
    Task<long> GetBlacklistedIPsCountAsync(DateTime fromDate, DateTime toDate);

    // Paged connections with column filters
    Task<PagedResult<Connection>> GetConnectionsPagedAsync(DateTime fromDate, DateTime toDate, int page, int pageSize, ConnectionFilter? filter = null);

    // Connection details
    Task<List<ConnectionData>> GetConnectionDataAsync(string sessionId);

    // IP details (on-demand)
    Task<IPDetails> GetIPDetailsAsync(string ipAddress);

    // World view - connection counts by country
    Task<List<CountryConnectionCount>> GetConnectionCountsByCountryAsync(DateTime fromDate, DateTime toDate);
}

public class CountryConnectionCount
{
    public string CountryCode { get; init; } = string.Empty;
    public string CountryName { get; init; } = string.Empty;
    public long ConnectionCount { get; init; }
}

public class ConnectionStats
{
    public long TotalConnections { get; set; }
    public long ForwardConnections { get; set; }
    public long HoneypotConnections { get; set; }
}

public class ConnectionFilter
{
    public string? ProxyType { get; set; }
    public string? RemoteAddress { get; set; }
    public string? RemotePort { get; set; }
    public string? LocalAddress { get; set; }
    public string? LocalPort { get; set; }

    public bool HasAnyFilter => !string.IsNullOrWhiteSpace(ProxyType) ||
                                !string.IsNullOrWhiteSpace(RemoteAddress) ||
                                !string.IsNullOrWhiteSpace(RemotePort) ||
                                !string.IsNullOrWhiteSpace(LocalAddress) ||
                                !string.IsNullOrWhiteSpace(LocalPort);
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public HashSet<string> SessionsWithData { get; set; } = [];
    public HashSet<string> BlacklistedIPs { get; set; } = [];
    public Dictionary<string, string> CountryCodeMap { get; set; } = new();
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public class IPDetails
{
    public string IPAddress { get; init; } = string.Empty;

    // From IPAddressHistory
    public long Hits { get; init; }
    public DateTime? LastConnectionTime { get; init; }
    public bool IsBlacklisted { get; init; }

    // From AbuseIPDB_CheckedIPS
    public long? AbuseConfidence { get; init; }
    public string? CountryCode { get; init; }
    public string? CountryName { get; init; }
    public string? ISP { get; init; }
    public string? Domain { get; init; }
    public string? UsageType { get; init; }
    public long? TotalReports { get; init; }
    public long? DistinctUserCount { get; init; }
    public DateTime? LastReportedAt { get; init; }
    public bool HasAbuseData => AbuseConfidence != null;
}
