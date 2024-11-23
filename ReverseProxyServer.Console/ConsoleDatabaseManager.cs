﻿using Kavalan.Core;
using Kavalan.Data.Sqlite;
using Kavalan.Data.Sqlite.Repositories;
using Kavalan.Logging;
using Microsoft.Extensions.Caching.Memory;
using ReverseProxyServer.Core.Helpers;
using ReverseProxyServer.Data.DTO;
using ReverseProxyServer.Extensions.AbuseIPDB;
using ReverseProxyServer.Extensions.AbuseIPDB.Data;
using System.Diagnostics;

namespace ReverseProxyServer;

internal class ConsoleDatabaseManager
{
    private GenericSqliteRepository<Instance> instances;
    private GenericSqliteRepository<Connection> connections;
    private GenericSqliteRepository<ConnectionData> connectionsData;
    private GenericSqliteRepository<IPAddressHistory> ipAddressHistory;
    private GenericSqliteRepository<PortHistory> portsHistory;
    private GenericSqliteRepository<AbuseIPDB_CheckedIP> abuseIPDB_CheckedIP;

    private readonly bool enabled = false;
    private readonly string databasePath = "";
    private readonly ILogger logger;

    public ConsoleDatabaseManager(string databasePath, ILogger logger)
    {
        this.databasePath = databasePath;
        this.logger = logger;

        enabled = !string.IsNullOrWhiteSpace(databasePath);
        if (enabled)
        {
            instances = new GenericSqliteRepository<Instance>(databasePath);
            connections = new GenericSqliteRepository<Connection>(databasePath);
            connectionsData = new GenericSqliteRepository<ConnectionData>(databasePath);
            ipAddressHistory = new GenericSqliteRepository<IPAddressHistory>(databasePath);
            portsHistory = new GenericSqliteRepository<PortHistory>(databasePath);
            abuseIPDB_CheckedIP = new GenericSqliteRepository<AbuseIPDB_CheckedIP>(databasePath);
        }
    }
    public Instance? RegisterServer()
    {
        if (!enabled) return null;
        string createDbScript = ResourceHelper.ReadResourceFile("ReverseProxyServer.Console.Resources.CreateDatabase.sql");
        SqlLiteDataLayer sqlLiteDataLayer = new(databasePath);
        //Execute code in sync so that we make sure the database is created
        sqlLiteDataLayer.ExecuteScript(createDbScript).GetAwaiter().GetResult();

        Instance instance = new()
        {
            InstanceId = Guid.NewGuid().ToString()[0..8],
            StartTime = DateTime.Now,
            Version = ConsoleHelper.GetFileVersion(),
            Status = "Started"
        };
        return instances.InsertAsync(instance).GetAwaiter().GetResult();
    }
    public void UpdateServerInstanceAsStopped(Instance? instance)
    {
        if (instance != null && enabled)
        {
            instance.EndTime = DateTime.Now;
            instance.Status = "Stopped";
            instances.UpdateAsync(instance).GetAwaiter().GetResult();
        }
    }
    public async Task RegisterConnectionDetails(Connection connection, string abuseIPDB_Key = "")
    {
        if (!enabled) return;
        Stopwatch stopwatch = Stopwatch.StartNew();
        string log = Environment.NewLine;
        DateTime currentDateTime = DateTime.Now;

        log += $"Insert connection record {await connections.InsertAsync(connection).TimeOnlyAsync()}ms{Environment.NewLine}";

        (IPAddressHistory dbIPRecord, long selectIPOpTime) = await ipAddressHistory.SelectByPrimaryKeyAsync([connection.RemoteAddress]).TimeAsync();
        log += $"Select IP record {selectIPOpTime}ms{Environment.NewLine}";
        
        if (dbIPRecord == null)
        {
            IPAddressHistory newIp = new()
            {
                IPAddress = connection.RemoteAddress,
                Hits = 1,
                LastConnectionTime = currentDateTime
            };
            (IPAddressHistory insertedIpRecord, long insertOpTime) = await ipAddressHistory.InsertAsync(newIp).TimeAsync();
            dbIPRecord = insertedIpRecord;
            log += $"Insert IP record {insertOpTime}ms{Environment.NewLine}";
        }
        else
        {
            dbIPRecord.Hits++;
            dbIPRecord.LastConnectionTime = currentDateTime;

            log += $"Update IP record {await ipAddressHistory.UpdateAsync(dbIPRecord).TimeOnlyAsync()}ms{Environment.NewLine}";
        }

        (PortHistory dbPortRecord, long selectPortOpTime) = await portsHistory.SelectByPrimaryKeyAsync([connection.LocalPort]).TimeAsync();
        log += $"Select Port record {selectPortOpTime}ms{Environment.NewLine}";

        if (dbPortRecord == null)
        {
            PortHistory newPort = new()
            {
                Port = connection.LocalPort,
                Hits = 1,
                LastConnectionTime = currentDateTime
            };
            log += $"Insert Port record {await portsHistory.InsertAsync(newPort).TimeOnlyAsync()}ms{Environment.NewLine}";
        }
        else
        {
            dbPortRecord.Hits++;
            dbPortRecord.LastConnectionTime = currentDateTime;
            
            log += $"Update Port record {await portsHistory.UpdateAsync(dbPortRecord).TimeOnlyAsync()}ms{Environment.NewLine}";
        }

        if (!string.IsNullOrWhiteSpace(abuseIPDB_Key))
        {
            (AbuseIPDB_CheckedIP? abuseIPDBRecord, long dbOpTime) = await abuseIPDB_CheckedIP.SelectByPrimaryKeyAsync([connection.RemoteAddress]).TimeAsync();
            log += $"Select AbuseIPDB record {dbOpTime}ms{Environment.NewLine}";
            /*If IP is one of the following criteria query AbuseIPDB API
             * First time IP hits
             * No results saved in database (Usually first time IP hits or last API query failed?)
             * Last API call was >24 hours ago (This will ensure that we don't spam the API and only query once a day per IP)
             */
            double? intervalFromLastCheck = (DateTime.Now - abuseIPDBRecord?.LastCheckedAt)?.TotalHours;
            if (abuseIPDBRecord is null || dbIPRecord?.Hits == 1 || intervalFromLastCheck >= 24)
            {
                AbuseIPDBClient abuseIPDBClient = new(abuseIPDB_Key);
                (CheckedIP checkedIP, long APICallTime) = await abuseIPDBClient.CheckIP(connection.RemoteAddress, true, 30).TimeAsync();
                log += $"Query AbuseIPDB API {APICallTime}ms{Environment.NewLine}";

                if (checkedIP.IPAddress != null)
                {
                    AbuseIPDB_CheckedIP dbAbuseIPDB = new()
                    {
                        IPAddress = checkedIP.IPAddress,
                        AbuseConfidence = checkedIP.AbuseConfidence,
                        CountryCode = checkedIP.CountryCode ?? "",
                        CountryName = checkedIP.CountryName,
                        DistinctUserCount = checkedIP.DistinctUserCount,
                        Domain = checkedIP.Domain,
                        Hostnames = string.Join(",", checkedIP.Hostnames ?? []),
                        IPVersion = checkedIP.IPVersion,
                        ISP = checkedIP.ISP,
                        IsPublic = checkedIP.IsPublic ? 1 : 0,
                        IsWhitelisted = (checkedIP.IsWhitelisted ?? false) ? 1 : 0,
                        TotalReports = checkedIP.TotalReports,
                        UsageType = checkedIP.UsageType,
                        LastReportedAt = checkedIP.LastReportedAt,
                        LastCheckedAt = DateTime.Now
                    };
                    log += $"Upsert AbuseIPDB record {await abuseIPDB_CheckedIP.UpsertAsync(dbAbuseIPDB).TimeOnlyAsync()}ms{Environment.NewLine}";

                    if (checkedIP.AbuseConfidence >= 75)
                        dbIPRecord.IsBlacklisted = 1;
                    else if (checkedIP.AbuseConfidence < 75)
                        dbIPRecord.IsBlacklisted = 0;

                    log += $"Update IP record {await ipAddressHistory.UpdateAsync(dbIPRecord).TimeOnlyAsync()}ms{Environment.NewLine}";

                    log += $"Total {stopwatch.ElapsedMilliseconds}ms";
                    await logger.LogDebugAsync(log, connection.SessionId);
                }
            }
        }
    }

    public async Task InsertNewConnectionData(ConnectionData connectionData)
    {
        if (!enabled) return;
        await connectionsData.InsertAsync(connectionData);
    }
    public async Task<List<Connection>> GetConnections(string instanceId)
    {
        return await connections.SelectDataByFieldValueAsync("InstanceId", instanceId);
    }
    public async Task<long> GetApiConnectionsForInstance(DateTime startedOn)
    {
        return await abuseIPDB_CheckedIP.CountAsync($"LastCheckedAt >= strftime('%Y-%m-%d %H:%M:%S', '{startedOn:yyyy-MM-dd HH:mm:ss}')");
    }
    public async Task<IPAddressHistory?> GetIPAddressHistoryAsync(string ip)
    {
        return await ipAddressHistory.SelectByPrimaryKeyAsync([ip]);
    }
    public async Task<AbuseIPDB_CheckedIP?> GetAbuseIPDB_CheckedIPAsync(string ip)
    {
        return await abuseIPDB_CheckedIP.SelectByPrimaryKeyAsync([ip]);
    }
}
