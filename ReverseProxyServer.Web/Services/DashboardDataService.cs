using Kavalan.Data.Sqlite;
using Kavalan.Data.Sqlite.Repositories;
using Microsoft.Data.Sqlite;
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

        var totalCount = await repo.CountAsync(dateFilter);
        var forwardCount = await repo.CountAsync($"{dateFilter} AND ProxyType = 'Forward'");

        return new ConnectionStats
        {
            TotalConnections = totalCount,
            ForwardConnections = forwardCount,
            HoneypotConnections = totalCount - forwardCount
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
        var dataLayer = new SqlLiteDataLayer(_databasePath);
        using var connection = await dataLayer.GetOpenConnection();

        string whereClause = GetDateFilter(fromDate, toDate);

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.ProxyType))
                whereClause += $" AND c.ProxyType = @ProxyType";
            if (!string.IsNullOrWhiteSpace(filter.RemoteAddress))
                whereClause += $" AND c.RemoteAddress LIKE @RemoteAddress";
            if (!string.IsNullOrWhiteSpace(filter.RemotePort))
                whereClause += $" AND c.RemotePort = @RemotePort";
            if (!string.IsNullOrWhiteSpace(filter.LocalAddress))
                whereClause += $" AND c.LocalAddress LIKE @LocalAddress";
            if (!string.IsNullOrWhiteSpace(filter.LocalPort))
                whereClause += $" AND c.LocalPort = @LocalPort";
        }

        int offset = (page - 1) * pageSize;

        string sql = $"""
            SELECT c.Id, c.SessionId, c.InstanceId, c.ConnectionTime, c.ProxyType, 
                   c.LocalAddress, c.LocalPort, c.TargetHost, c.TargetPort, 
                   c.RemoteAddress, c.RemotePort,
                   EXISTS(SELECT 1 FROM ConnectionsData cd WHERE cd.SessionId = c.SessionId) AS HasData,
                   COALESCE(ip.IsBlacklisted, 0) AS IsBlacklisted
            FROM Connections c
            LEFT JOIN IPAddressHistory ip ON c.RemoteAddress = ip.IPAddress
            INNER JOIN (
                SELECT c.Id FROM Connections c
                WHERE {whereClause}
                ORDER BY c.ConnectionTime DESC
                LIMIT @PageSize OFFSET @Offset
            ) AS page ON c.Id = page.Id
            ORDER BY c.ConnectionTime DESC
            """;

        string countSql = $"SELECT COUNT(*) FROM Connections c WHERE {whereClause}";

        using var countCmd = new SqliteCommand(countSql, connection);
        using var dataCmd = new SqliteCommand(sql, connection);

        // Add shared parameters to both commands
        countCmd.Parameters.AddWithValue("@PageSize", pageSize);
        countCmd.Parameters.AddWithValue("@Offset", offset);
        dataCmd.Parameters.AddWithValue("@PageSize", pageSize);
        dataCmd.Parameters.AddWithValue("@Offset", offset);

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.ProxyType))
            {
                countCmd.Parameters.AddWithValue("@ProxyType", filter.ProxyType);
                dataCmd.Parameters.AddWithValue("@ProxyType", filter.ProxyType);
            }
            if (!string.IsNullOrWhiteSpace(filter.RemoteAddress))
            {
                countCmd.Parameters.AddWithValue("@RemoteAddress", $"%{filter.RemoteAddress}%");
                dataCmd.Parameters.AddWithValue("@RemoteAddress", $"%{filter.RemoteAddress}%");
            }
            if (!string.IsNullOrWhiteSpace(filter.RemotePort))
            {
                countCmd.Parameters.AddWithValue("@RemotePort", filter.RemotePort);
                dataCmd.Parameters.AddWithValue("@RemotePort", filter.RemotePort);
            }
            if (!string.IsNullOrWhiteSpace(filter.LocalAddress))
            {
                countCmd.Parameters.AddWithValue("@LocalAddress", $"%{filter.LocalAddress}%");
                dataCmd.Parameters.AddWithValue("@LocalAddress", $"%{filter.LocalAddress}%");
            }
            if (!string.IsNullOrWhiteSpace(filter.LocalPort))
            {
                countCmd.Parameters.AddWithValue("@LocalPort", filter.LocalPort);
                dataCmd.Parameters.AddWithValue("@LocalPort", filter.LocalPort);
            }
        }

        long totalCount = Convert.ToInt64(await countCmd.ExecuteScalarAsync());

        var connections = new List<Connection>();
        var sessionsWithData = new HashSet<string>();
        var blacklistedIPs = new HashSet<string>();

        using var reader = await dataCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var conn = new Connection
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                SessionId = reader.GetString(reader.GetOrdinal("SessionId")),
                InstanceId = reader.GetString(reader.GetOrdinal("InstanceId")),
                ConnectionTime = Convert.ToDateTime(reader["ConnectionTime"]),
                ProxyType = reader.GetString(reader.GetOrdinal("ProxyType")),
                LocalAddress = reader.GetString(reader.GetOrdinal("LocalAddress")),
                LocalPort = reader.GetInt64(reader.GetOrdinal("LocalPort")),
                TargetHost = reader.GetString(reader.GetOrdinal("TargetHost")),
                TargetPort = reader.GetInt64(reader.GetOrdinal("TargetPort")),
                RemoteAddress = reader.GetString(reader.GetOrdinal("RemoteAddress")),
                RemotePort = reader.GetInt64(reader.GetOrdinal("RemotePort"))
            };

            connections.Add(conn);

            if (reader.GetInt64(reader.GetOrdinal("HasData")) == 1)
                sessionsWithData.Add(conn.SessionId);

            if (reader.GetInt64(reader.GetOrdinal("IsBlacklisted")) == 1)
                blacklistedIPs.Add(conn.RemoteAddress);
        }

        return new PagedResult<Connection>
        {
            Items = connections,
            TotalCount = (int)totalCount,
            Page = page,
            PageSize = pageSize,
            SessionsWithData = sessionsWithData,
            BlacklistedIPs = blacklistedIPs
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
