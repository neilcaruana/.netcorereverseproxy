using ReverseProxyServer.Data.DTO;

namespace ReverseProxyServer.Web.Services;

public interface IDashboardDataService
{
    // Stats methods - all filtered by date range
    Task<ConnectionStats> GetConnectionStatsAsync(DateTime fromDate, DateTime toDate);
    Task<long> GetUniqueIPsCountAsync(DateTime fromDate, DateTime toDate);
    Task<long> GetUniquePortsCountAsync(DateTime fromDate, DateTime toDate);
    Task<long> GetBlacklistedIPsCountAsync();

    // Paged connections with column filters
    Task<PagedResult<Connection>> GetConnectionsPagedAsync(DateTime fromDate, DateTime toDate, int page, int pageSize, ConnectionFilter? filter = null);

    // Connection details
    Task<List<ConnectionData>> GetConnectionDataAsync(string sessionId);
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
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
