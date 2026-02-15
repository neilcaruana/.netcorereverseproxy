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

        var whereClause = GetDateFilter(fromDate, toDate);
        var parameters = new Dictionary<string, object>();
        var filterJoin = string.Empty;
        int offset = (page - 1) * pageSize;

        if (filter != null)
        {
            AddFilter(filter.ProxyType, "c.ProxyType = @ProxyType", "@ProxyType", v => v);
            AddFilter(filter.RemoteAddress, "c.RemoteAddress LIKE @RemoteAddress", "@RemoteAddress", v => $"%{v}%");
            AddFilter(filter.RemotePort, "c.RemotePort = @RemotePort", "@RemotePort", v => v);
            AddFilter(filter.LocalAddress, "c.LocalAddress LIKE @LocalAddress", "@LocalAddress", v => $"%{v}%");
            AddFilter(filter.LocalPort, "c.LocalPort = @LocalPort", "@LocalPort", v => v);
            AddFilter(filter.CountryCode, "c.RemoteAddress IN (SELECT IPAddress FROM AbuseIPDB_CheckedIPS WHERE CountryCode LIKE @CountryCode)", "@CountryCode", v => $"%{v}%");

            if (!string.IsNullOrWhiteSpace(filter.IsBlacklisted))
            {
                filterJoin = filter.IsBlacklisted == "Yes"
                    ? " INNER JOIN IPAddressHistory bl ON c.RemoteAddress = bl.IPAddress AND bl.IsBlacklisted = 1"
                    : " INNER JOIN IPAddressHistory bl ON c.RemoteAddress = bl.IPAddress AND bl.IsBlacklisted = 0";
            }
        }

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
                {filterJoin}
                WHERE {whereClause}
                ORDER BY c.ConnectionTime DESC
                LIMIT @PageSize OFFSET @Offset
            ) AS page ON c.Id = page.Id
            ORDER BY c.ConnectionTime DESC
            """;

        using var countCmd = new SqliteCommand($"SELECT COUNT(*) FROM Connections c {filterJoin} WHERE {whereClause}", connection);
        using var dataCmd = new SqliteCommand(sql, connection);

        parameters["@PageSize"] = pageSize;
        parameters["@Offset"] = offset;

        foreach (var (name, value) in parameters)
        {
            countCmd.Parameters.AddWithValue(name, value);
            dataCmd.Parameters.AddWithValue(name, value);
        }

        long totalCount = Convert.ToInt64(await countCmd.ExecuteScalarAsync());

        var connections = new List<Connection>();
        var sessionsWithData = new HashSet<string>();
        var blacklistedIPs = new HashSet<string>();
        var countryCodeMap = new Dictionary<string, string>();

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

            if (!reader.IsDBNull(reader.GetOrdinal("CountryCode")))
                countryCodeMap.TryAdd(conn.RemoteAddress, reader.GetString(reader.GetOrdinal("CountryCode")));
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

        void AddFilter(string? value, string clause, string paramName, Func<string, object> transform)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            whereClause += $" AND {clause}";
            parameters[paramName] = transform(value);
        }
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
