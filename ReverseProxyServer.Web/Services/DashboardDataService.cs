using Kavalan.Data.Sqlite.Repositories;
using Microsoft.Extensions.Options;
using ReverseProxyServer.Data.DTO;

namespace ReverseProxyServer.Web.Services;

public class DashboardDataService : IDashboardDataService
{
    private readonly string _databasePath;

    public DashboardDataService(IOptions<DatabaseSettings> settings)
    {
        _databasePath = settings.Value.Path;
    }

    private static string GetDateFilter(DateTime fromDate, DateTime toDate) =>
        $"ConnectionTime >= '{fromDate:yyyy-MM-dd HH:mm:ss}' AND ConnectionTime <= '{toDate:yyyy-MM-dd HH:mm:ss}'";

    public async Task<ConnectionStats> GetConnectionStatsAsync(DateTime fromDate, DateTime toDate)
    {
        using var repo = new GenericSqliteRepository<Connection>(_databasePath);
        string dateFilter = GetDateFilter(fromDate, toDate);

        var forwardCount = await repo.CountAsync($"{dateFilter} AND ProxyType = 'Forward'");
        var honeypotCount = await repo.CountAsync($"{dateFilter} AND ProxyType = 'HoneyPot'");

        return new ConnectionStats
        {
            TotalConnections = forwardCount + honeypotCount,
            ForwardConnections = forwardCount,
            HoneypotConnections = honeypotCount
        };
    }

        public async Task<long> GetUniqueIPsCountAsync(DateTime fromDate, DateTime toDate)
        {
            using var repo = new GenericSqliteRepository<IPAddressHistory>(_databasePath);
            string dateFilter = $"LastConnectionTime >= '{fromDate:yyyy-MM-dd HH:mm:ss}' AND LastConnectionTime <= '{toDate:yyyy-MM-dd HH:mm:ss}'";
            return await repo.CountAsync(dateFilter);
        }

        public async Task<long> GetUniquePortsCountAsync(DateTime fromDate, DateTime toDate)
        {
            using var repo = new GenericSqliteRepository<PortHistory>(_databasePath);
            string dateFilter = $"LastConnectionTime >= '{fromDate:yyyy-MM-dd HH:mm:ss}' AND LastConnectionTime <= '{toDate:yyyy-MM-dd HH:mm:ss}'";
            return await repo.CountAsync(dateFilter);
        }

        public async Task<long> GetBlacklistedIPsCountAsync()
        {
        using var repo = new GenericSqliteRepository<IPAddressHistory>(_databasePath);
        return await repo.CountAsync("IsBlacklisted = 1");
    }

    public async Task<PagedResult<Connection>> GetConnectionsPagedAsync(DateTime fromDate, DateTime toDate, int page, int pageSize, ConnectionFilter? filter = null)
    {
        using var repo = new GenericSqliteRepository<Connection>(_databasePath);
        string whereClause = GetDateFilter(fromDate, toDate);

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.ProxyType))
                whereClause += $" AND ProxyType = '{filter.ProxyType.Replace("'", "''")}'";
            if (!string.IsNullOrWhiteSpace(filter.RemoteAddress))
                whereClause += $" AND RemoteAddress LIKE '%{filter.RemoteAddress.Replace("'", "''")}%'";
            if (!string.IsNullOrWhiteSpace(filter.RemotePort))
                whereClause += $" AND RemotePort = '{filter.RemotePort.Replace("'", "''")}'";
            if (!string.IsNullOrWhiteSpace(filter.LocalAddress))
                whereClause += $" AND LocalAddress LIKE '%{filter.LocalAddress.Replace("'", "''")}%'";
            if (!string.IsNullOrWhiteSpace(filter.LocalPort))
                whereClause += $" AND LocalPort = '{filter.LocalPort.Replace("'", "''")}'";
        }

        long totalCount = await repo.CountAsync(whereClause);
        int offset = (page - 1) * pageSize;

        var connections = await repo.SelectDataByExpressionAsync(
            fieldName: "",
            fieldValue: null,
            whereClause: whereClause,
            orderByCaluse: $"ConnectionTime DESC LIMIT {pageSize} OFFSET {offset}");

        return new PagedResult<Connection>
        {
            Items = connections,
            TotalCount = (int)totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<List<ConnectionData>> GetConnectionDataAsync(string sessionId)
    {
        using var repo = new GenericSqliteRepository<ConnectionData>(_databasePath);

        var connectionData = await repo.SelectDataByExpressionAsync(
            fieldName: "SessionId",
            fieldValue: sessionId,
            whereClause: "",
            orderByCaluse: "Id ASC");

        return connectionData;
    }
}
