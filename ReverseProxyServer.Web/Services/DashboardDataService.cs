using System.Diagnostics;
using Kavalan.Data.Sqlite;
using Kavalan.Data.Sqlite.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using ReverseProxyServer.Data.DTO;

namespace ReverseProxyServer.Web.Services;

public class DashboardDataService : IDashboardDataService
{
    private readonly IOptionsMonitor<DatabaseSettings> _settings;

    public DashboardDataService(IOptionsMonitor<DatabaseSettings> settings)
    {
        _settings = settings;
    }

    private string DatabasePath => _settings.CurrentValue.Path;

    private static string GetDateFilter(DateTime fromDate, DateTime toDate) =>
        $"ConnectionTime >= '{fromDate:yyyy-MM-dd HH:mm:ss}' AND ConnectionTime <= '{toDate:yyyy-MM-dd HH:mm:ss}'";

    private static string GetOrderByClause(ConnectionFilter? filter)
    {
        var direction = filter?.SortDescending != false ? "DESC" : "ASC";
        return filter?.SortColumn switch
        {
            "ConnectionTime" => $"c.ConnectionTime {direction}",
            "ProxyType" => $"c.ProxyType {direction}, c.ConnectionTime DESC",
            "RemoteAddress" => $"c.RemoteAddress {direction}, c.ConnectionTime DESC",
            "RemotePort" => $"c.RemotePort {direction}, c.ConnectionTime DESC",
            "LocalAddress" => $"c.LocalAddress {direction}, c.ConnectionTime DESC",
            "LocalPort" => $"c.LocalPort {direction}, c.ConnectionTime DESC",
            _ => "c.ConnectionTime DESC"
        };
    }
    public async Task<ConnectionStats> GetConnectionStatsAsync(DateTime fromDate, DateTime toDate)
    {
        using var repo = new GenericSqliteRepository<Connection>(DatabasePath);
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
            using var repo = new GenericSqliteRepository<IPAddressHistory>(DatabasePath);
            string dateFilter = $"LastConnectionTime >= '{fromDate:yyyy-MM-dd HH:mm:ss}' AND LastConnectionTime <= '{toDate:yyyy-MM-dd HH:mm:ss}'";
            return await repo.CountAsync(dateFilter);
        }

        public async Task<long> GetUniquePortsCountAsync(DateTime fromDate, DateTime toDate)
        {
            using var repo = new GenericSqliteRepository<PortHistory>(DatabasePath);
            string dateFilter = $"LastConnectionTime >= '{fromDate:yyyy-MM-dd HH:mm:ss}' AND LastConnectionTime <= '{toDate:yyyy-MM-dd HH:mm:ss}'";
            return await repo.CountAsync(dateFilter);
        }

    public async Task<long> GetBlacklistedIPsCountAsync(DateTime fromDate, DateTime toDate)
    {
        using var repo = new GenericSqliteRepository<IPAddressHistory>(DatabasePath);
        string dateFilter = $"IsBlacklisted = 1 AND LastConnectionTime >= '{fromDate:yyyy-MM-dd HH:mm:ss}' AND LastConnectionTime <= '{toDate:yyyy-MM-dd HH:mm:ss}'";
        return await repo.CountAsync(dateFilter);
    }

    public async Task<PagedResult<Connection>> GetConnectionsPagedAsync(DateTime fromDate, DateTime toDate, int page, int pageSize, ConnectionFilter? filter = null)
    {
        var dataLayer = new SqlLiteDataLayer(DatabasePath);
        using var connection = await dataLayer.GetOpenConnection();

        var whereClause = GetDateFilter(fromDate, toDate);
        var parameters = new Dictionary<string, object>();
        var filterJoin = string.Empty;
        int offset = (page - 1) * pageSize;

        var orderBy = GetOrderByClause(filter);

        if (filter != null)
        {
            AddFilter(filter.ProxyType, "c.ProxyType = @ProxyType", "@ProxyType", v => v);
            AddFilter(filter.RemoteAddress, "c.RemoteAddress LIKE @RemoteAddress", "@RemoteAddress", v => $"%{v}%");
            AddFilter(filter.RemotePort, "c.RemotePort = @RemotePort", "@RemotePort", v => v);
            AddFilter(filter.LocalAddress, "c.LocalAddress LIKE @LocalAddress", "@LocalAddress", v => $"%{v}%");
            AddFilter(filter.LocalPort, "c.LocalPort = @LocalPort", "@LocalPort", v => v);
            AddFilter(filter.CountryCode, "c.RemoteAddress IN (SELECT IPAddress FROM AbuseIPDB_CheckedIPS WHERE CountryCode = @CountryCode)", "@CountryCode", v => v);

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
                ORDER BY {orderBy}
                LIMIT @PageSize OFFSET @Offset
            ) AS page ON c.Id = page.Id
            ORDER BY {orderBy}
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
        using var repo = new GenericSqliteRepository<ConnectionData>(DatabasePath);

        var connectionData = await repo.SelectDataByExpressionAsync(
            fieldName: "SessionId",
            fieldValue: sessionId,
            whereClause: "",
            orderByCaluse: "Id ASC");

        return connectionData;
    }

    public async Task<IPDetails> GetIPDetailsAsync(string ipAddress)
    {
        var dataLayer = new SqlLiteDataLayer(DatabasePath);
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
        var dataLayer = new SqlLiteDataLayer(DatabasePath);
        using var connection = await dataLayer.GetOpenConnection();

        string sql = """
            SELECT abuse.CountryCode, abuse.CountryName, SUM(ip_counts.Hits) AS ConnectionCount
            FROM (
                SELECT RemoteAddress, COUNT(*) AS Hits
                FROM Connections
                WHERE ConnectionTime >= @FromDate AND ConnectionTime <= @ToDate
                GROUP BY RemoteAddress
            ) AS ip_counts
            INNER JOIN AbuseIPDB_CheckedIPS abuse ON ip_counts.RemoteAddress = abuse.IPAddress
            WHERE abuse.CountryCode IS NOT NULL
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

    public async Task<List<CountryInfo>> GetDistinctCountriesAsync(DateTime fromDate, DateTime toDate)
    {
        var dataLayer = new SqlLiteDataLayer(DatabasePath);
        using var connection = await dataLayer.GetOpenConnection();

        string sql = """
            SELECT DISTINCT abuse.CountryCode, abuse.CountryName
            FROM Connections c
            INNER JOIN AbuseIPDB_CheckedIPS abuse ON c.RemoteAddress = abuse.IPAddress
            WHERE c.ConnectionTime >= @FromDate AND c.ConnectionTime <= @ToDate
              AND abuse.CountryCode IS NOT NULL AND abuse.CountryCode != ''
            ORDER BY abuse.CountryName, abuse.CountryCode
            """;

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@FromDate", fromDate.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@ToDate", toDate.ToString("yyyy-MM-dd HH:mm:ss"));

        var results = new List<CountryInfo>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new CountryInfo
            {
                CountryCode = reader.GetString(reader.GetOrdinal("CountryCode")),
                CountryName = reader.IsDBNull(reader.GetOrdinal("CountryName")) ? string.Empty : reader.GetString(reader.GetOrdinal("CountryName"))
            });
        }
        return results;
    }

    public async Task<BandwidthStats> GetBandwidthStatsAsync(DateTime fromDate, DateTime toDate)
    {
        var dataLayer = new SqlLiteDataLayer(DatabasePath);
        using var connection = await dataLayer.GetOpenConnection();

        // Single query: IN subquery avoids explicit JOIN, sum in C# avoids GROUP BY
        // Covering index IX_ConnectionsData_Session_Direction_Size serves this entirely from the index
        string sql = """
            SELECT cd.CommunicationDirection, cd.DataSize
            FROM ConnectionsData cd
            WHERE cd.SessionId IN (
                SELECT c.SessionId FROM Connections c
                WHERE c.ConnectionTime >= @FromDate AND c.ConnectionTime <= @ToDate
            )
            """;

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@FromDate", fromDate.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@ToDate", toDate.ToString("yyyy-MM-dd HH:mm:ss"));

        long incoming = 0;
        long outgoing = 0;

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.GetString(0) == "Incoming")
                incoming += reader.GetInt64(1);
            else
                outgoing += reader.GetInt64(1);
        }

        return new BandwidthStats { IncomingBytes = incoming, OutgoingBytes = outgoing };
    }

    public async Task<TopConnectionInfo?> GetTopConnectionAsync(DateTime fromDate, DateTime toDate)
    {
        var dataLayer = new SqlLiteDataLayer(DatabasePath);
        using var connection = await dataLayer.GetOpenConnection();

        string sql = """
            SELECT top.RemoteAddress, top.Hits,
                   abuse.CountryCode, abuse.CountryName
            FROM (
                SELECT RemoteAddress, COUNT(*) AS Hits
                FROM Connections
                WHERE ConnectionTime >= @FromDate AND ConnectionTime <= @ToDate
                GROUP BY RemoteAddress
                ORDER BY Hits DESC
                LIMIT 1
            ) AS top
            LEFT JOIN AbuseIPDB_CheckedIPS abuse ON top.RemoteAddress = abuse.IPAddress
            """;

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@FromDate", fromDate.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@ToDate", toDate.ToString("yyyy-MM-dd HH:mm:ss"));

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new TopConnectionInfo
            {
                IPAddress = reader.GetString(reader.GetOrdinal("RemoteAddress")),
                Hits = reader.GetInt64(reader.GetOrdinal("Hits")),
                CountryCode = reader.IsDBNull(reader.GetOrdinal("CountryCode")) ? null : reader.GetString(reader.GetOrdinal("CountryCode")),
                CountryName = reader.IsDBNull(reader.GetOrdinal("CountryName")) ? null : reader.GetString(reader.GetOrdinal("CountryName"))
            };
        }

        return null;
    }

    public async Task<long> GetTotalConnectionDataCountAsync()
    {
        using var repo = new GenericSqliteRepository<ConnectionData>(DatabasePath);
        return await repo.CountAsync(string.Empty);
    }

    public async Task<PagedResult<SearchResult>> SearchConnectionDataAsync(string searchTerm, int page, int pageSize, SearchSortOrder sortOrder = SearchSortOrder.Relevance, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var sw = Stopwatch.StartNew();
        var dataLayer = new SqlLiteDataLayer(DatabasePath);
        using var connection = await dataLayer.GetOpenConnection();
        var ftsQuery = EscapeFtsQuery(searchTerm);
        var hasDateFilter = fromDate.HasValue && toDate.HasValue;

        string countSql;
        if (hasDateFilter)
        {
            countSql = """
                SELECT COUNT(*)
                FROM ConnectionsDataFts fts
                INNER JOIN ConnectionsData cd ON fts.rowid = cd.Id
                INNER JOIN Connections c ON cd.SessionId = c.SessionId
                WHERE ConnectionsDataFts MATCH @SearchTerm
                  AND c.ConnectionTime BETWEEN @FromDate AND @ToDate
                """;
        }
        else
        {
            countSql = """
                SELECT COUNT(*)
                FROM ConnectionsDataFts
                WHERE ConnectionsDataFts MATCH @SearchTerm
                """;
        }

        using var countCmd = new SqliteCommand(countSql, connection);
        countCmd.Parameters.AddWithValue("@SearchTerm", ftsQuery);
        if (hasDateFilter)
        {
            countCmd.Parameters.AddWithValue("@FromDate", fromDate!.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            countCmd.Parameters.AddWithValue("@ToDate", toDate!.Value.ToString("yyyy-MM-dd HH:mm:ss"));
        }
        int totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

        string sql;
        if (!hasDateFilter && sortOrder == SearchSortOrder.Relevance)
        {
            sql = """
                SELECT rowid, SessionId,
                       snippet(ConnectionsDataFts, 1, @MarkOpen, @MarkClose, @MarkEllipsis, 48) AS Snippet,
                       rank
                FROM ConnectionsDataFts
                WHERE ConnectionsDataFts MATCH @SearchTerm
                ORDER BY rank
                LIMIT @PageSize OFFSET @Offset
                """;
        }
        else
        {
            var orderBy = sortOrder switch
            {
                SearchSortOrder.Newest => "c.ConnectionTime DESC",
                SearchSortOrder.Oldest => "c.ConnectionTime ASC",
                SearchSortOrder.Largest => "cd.DataSize DESC",
                _ => "fts.rank"
            };

            var dateClause = hasDateFilter ? "AND c.ConnectionTime BETWEEN @FromDate AND @ToDate" : string.Empty;

            sql = $"""
                SELECT fts.rowid, fts.SessionId,
                       snippet(ConnectionsDataFts, 1, @MarkOpen, @MarkClose, @MarkEllipsis, 48) AS Snippet,
                       fts.rank
                FROM ConnectionsDataFts fts
                INNER JOIN ConnectionsData cd ON fts.rowid = cd.Id
                INNER JOIN Connections c ON cd.SessionId = c.SessionId
                WHERE ConnectionsDataFts MATCH @SearchTerm
                  {dateClause}
                ORDER BY {orderBy}
                LIMIT @PageSize OFFSET @Offset
                """;
        }

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@SearchTerm", ftsQuery);
        cmd.Parameters.AddWithValue("@PageSize", pageSize);
        cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
        cmd.Parameters.AddWithValue("@MarkOpen", "\u00AB");
        cmd.Parameters.AddWithValue("@MarkClose", "\u00BB");
        cmd.Parameters.AddWithValue("@MarkEllipsis", "\u2026");
        if (hasDateFilter)
        {
            cmd.Parameters.AddWithValue("@FromDate", fromDate!.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@ToDate", toDate!.Value.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        var results = new List<SearchResult>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(new SearchResult
            {
                Id = reader.GetInt64(reader.GetOrdinal("rowid")),
                SessionId = reader.IsDBNull(reader.GetOrdinal("SessionId"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("SessionId")),
                Snippet = reader.IsDBNull(reader.GetOrdinal("Snippet"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("Snippet")),
                RelevanceScore = reader.GetDouble(reader.GetOrdinal("rank"))
            });
        }

        sw.Stop();

        return new PagedResult<SearchResult>
        {
            Items = results,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            ElapsedMilliseconds = sw.ElapsedMilliseconds
        };
    }

    private static string EscapeFtsQuery(string input)
    {
        var trimmed = input.Trim();

        if (trimmed.StartsWith('"') && trimmed.EndsWith('"'))
            return trimmed;

        if (trimmed.Contains(" OR ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains(" NOT ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains(" NEAR(", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains('*'))
            return trimmed;

        var escaped = trimmed.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    public async Task<string?> GetHighlightedDataAsync(long connectionDataId, string searchTerm)
    {
        var dataLayer = new SqlLiteDataLayer(DatabasePath);
        using var connection = await dataLayer.GetOpenConnection();

        string sql = """
            SELECT highlight(ConnectionsDataFts, 1, @MarkOpen, @MarkClose) AS Highlighted
            FROM ConnectionsDataFts
            WHERE rowid = @Id AND ConnectionsDataFts MATCH @SearchTerm
            """;

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", connectionDataId);
        cmd.Parameters.AddWithValue("@SearchTerm", EscapeFtsQuery(searchTerm));
        cmd.Parameters.AddWithValue("@MarkOpen", "\u00AB");
        cmd.Parameters.AddWithValue("@MarkClose", "\u00BB");

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return reader.IsDBNull(0) ? null : reader.GetString(0);
        }

        return null;
    }

    public async Task<string?> GetConnectionDataRawAsync(long connectionDataId)
    {
        var dataLayer = new SqlLiteDataLayer(DatabasePath);
        using var connection = await dataLayer.GetOpenConnection();

        string sql = """
            SELECT Data FROM ConnectionsData WHERE Id = @Id LIMIT 1
            """;

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", connectionDataId);

        var result = await cmd.ExecuteScalarAsync();
        return result is DBNull or null ? null : (string)result;
    }

    public async Task<SearchResult?> GetSearchResultMetadataAsync(long connectionDataId)
    {
        var dataLayer = new SqlLiteDataLayer(DatabasePath);
        using var connection = await dataLayer.GetOpenConnection();

        string sql = """
            SELECT cd.CommunicationDirection, cd.DataSize,
                   c.ConnectionTime, c.ProxyType, c.RemoteAddress, c.RemotePort,
                   abuse.CountryCode, abuse.CountryName
            FROM ConnectionsData cd
            INNER JOIN Connections c ON cd.SessionId = c.SessionId
            LEFT JOIN AbuseIPDB_CheckedIPS abuse ON c.RemoteAddress = abuse.IPAddress
            WHERE cd.Id = @Id
            LIMIT 1
            """;

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", connectionDataId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SearchResult
            {
                Id = connectionDataId,
                MetadataLoaded = true,
                CommunicationDirection = reader.IsDBNull(reader.GetOrdinal("CommunicationDirection"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("CommunicationDirection")),
                DataSize = reader.GetInt64(reader.GetOrdinal("DataSize")),
                ConnectionTime = reader.IsDBNull(reader.GetOrdinal("ConnectionTime"))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal("ConnectionTime")),
                ProxyType = reader.IsDBNull(reader.GetOrdinal("ProxyType"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("ProxyType")),
                RemoteAddress = reader.IsDBNull(reader.GetOrdinal("RemoteAddress"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("RemoteAddress")),
                RemotePort = reader.GetInt64(reader.GetOrdinal("RemotePort")),
                CountryCode = reader.IsDBNull(reader.GetOrdinal("CountryCode"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("CountryCode")),
                CountryName = reader.IsDBNull(reader.GetOrdinal("CountryName"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("CountryName"))
            };
        }

        return null;
    }

    }
