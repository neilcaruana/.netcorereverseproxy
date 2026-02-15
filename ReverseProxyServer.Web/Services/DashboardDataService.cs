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

    public async Task<long> GetBlacklistedIPsCountAsync(DateTime fromDate, DateTime toDate)
    {
        using var repo = new GenericSqliteRepository<IPAddressHistory>(_databasePath);
        string dateFilter = $"IsBlacklisted = 1 AND LastConnectionTime >= '{fromDate:yyyy-MM-dd HH:mm:ss}' AND LastConnectionTime <= '{toDate:yyyy-MM-dd HH:mm:ss}'";
        return await repo.CountAsync(dateFilter);
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
                   COALESCE(ip.IsBlacklisted, 0) AS IsBlacklisted,
                   abuse.CountryCode
            FROM Connections c
            LEFT JOIN IPAddressHistory ip ON c.RemoteAddress = ip.IPAddress
            LEFT JOIN AbuseIPDB_CheckedIPS abuse ON c.RemoteAddress = abuse.IPAddress
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
        var countryCodeMap = new Dictionary<string, string>();

        using var reader = await dataCmd.ExecuteReaderAsync();
        var countryCodeOrdinal = reader.GetOrdinal("CountryCode");

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

            if (!reader.IsDBNull(countryCodeOrdinal))
                countryCodeMap.TryAdd(conn.RemoteAddress, reader.GetString(countryCodeOrdinal));
        }

        return new PagedResult<Connection>
        {
            Items = connections,
            TotalCount = (int)totalCount,
            Page = page,
            PageSize = pageSize,
            SessionsWithData = sessionsWithData,
            BlacklistedIPs = blacklistedIPs,
            CountryCodeMap = countryCodeMap
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

    public async Task<IPDetails> GetIPDetailsAsync(string ipAddress)
    {
        var dataLayer = new SqlLiteDataLayer(_databasePath);
        using var connection = await dataLayer.GetOpenConnection();

        string sql = """
            SELECT 
                ip.IPAddress, ip.Hits, ip.LastConnectionTime, ip.IsBlacklisted,
                abuse.AbuseConfidence, abuse.CountryCode, abuse.CountryName,
                abuse.ISP, abuse.Domain, abuse.UsageType, 
                abuse.TotalReports, abuse.DistinctUserCount, abuse.LastReportedAt
            FROM IPAddressHistory ip
            LEFT JOIN AbuseIPDB_CheckedIPS abuse ON ip.IPAddress = abuse.IPAddress
            WHERE ip.IPAddress = @IPAddress
            """;

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@IPAddress", ipAddress);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new IPDetails
            {
                IPAddress = reader.GetString(reader.GetOrdinal("IPAddress")),
                Hits = reader.GetInt64(reader.GetOrdinal("Hits")),
                LastConnectionTime = Convert.ToDateTime(reader["LastConnectionTime"]),
                IsBlacklisted = reader.GetInt64(reader.GetOrdinal("IsBlacklisted")) == 1,
                AbuseConfidence = reader.IsDBNull(reader.GetOrdinal("AbuseConfidence")) ? null : reader.GetInt64(reader.GetOrdinal("AbuseConfidence")),
                CountryCode = reader.IsDBNull(reader.GetOrdinal("CountryCode")) ? null : reader.GetString(reader.GetOrdinal("CountryCode")),
                CountryName = reader.IsDBNull(reader.GetOrdinal("CountryName")) ? null : reader.GetString(reader.GetOrdinal("CountryName")),
                ISP = reader.IsDBNull(reader.GetOrdinal("ISP")) ? null : reader.GetString(reader.GetOrdinal("ISP")),
                Domain = reader.IsDBNull(reader.GetOrdinal("Domain")) ? null : reader.GetString(reader.GetOrdinal("Domain")),
                UsageType = reader.IsDBNull(reader.GetOrdinal("UsageType")) ? null : reader.GetString(reader.GetOrdinal("UsageType")),
                TotalReports = reader.IsDBNull(reader.GetOrdinal("TotalReports")) ? null : reader.GetInt64(reader.GetOrdinal("TotalReports")),
                DistinctUserCount = reader.IsDBNull(reader.GetOrdinal("DistinctUserCount")) ? null : reader.GetInt64(reader.GetOrdinal("DistinctUserCount")),
                LastReportedAt = reader.IsDBNull(reader.GetOrdinal("LastReportedAt")) ? null : Convert.ToDateTime(reader["LastReportedAt"])
            };
        }

        return new IPDetails { IPAddress = ipAddress };
    }

    public async Task<List<CountryConnectionCount>> GetConnectionCountsByCountryAsync(DateTime fromDate, DateTime toDate)
    {
        var dataLayer = new SqlLiteDataLayer(_databasePath);
        using var connection = await dataLayer.GetOpenConnection();

        string sql = """
            SELECT abuse.CountryCode, abuse.CountryName, COUNT(*) AS ConnectionCount
            FROM Connections c
            INNER JOIN AbuseIPDB_CheckedIPS abuse ON c.RemoteAddress = abuse.IPAddress
            WHERE c.ConnectionTime >= @FromDate AND c.ConnectionTime <= @ToDate
              AND abuse.CountryCode IS NOT NULL
            GROUP BY abuse.CountryCode, abuse.CountryName
            ORDER BY ConnectionCount DESC
            """;

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@FromDate", fromDate.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@ToDate", toDate.ToString("yyyy-MM-dd HH:mm:ss"));

        var results = new List<CountryConnectionCount>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new CountryConnectionCount
            {
                CountryCode = reader.GetString(reader.GetOrdinal("CountryCode")),
                CountryName = reader.IsDBNull(reader.GetOrdinal("CountryName")) ? string.Empty : reader.GetString(reader.GetOrdinal("CountryName")),
                ConnectionCount = reader.GetInt64(reader.GetOrdinal("ConnectionCount"))
            });
        }
        return results;
    }
}
