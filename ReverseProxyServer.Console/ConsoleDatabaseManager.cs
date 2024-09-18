using Kavalan.Core;
using Kavalan.Data.Sqlite;
using Kavalan.Data.Sqlite.Repositories;
using ReverseProxyServer.Core.Helpers;
using ReverseProxyServer.Data.DTO;
using ReverseProxyServer.Extensions.AbuseIPDB;
using ReverseProxyServer.Extensions.AbuseIPDB.Data;

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
    public ConsoleDatabaseManager(string databasePath)
    {
        this.databasePath = databasePath;
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
        await connections.InsertAsync(connection);

        DateTime currentDateTime = DateTime.Now;
        IPAddressHistory? ip = await ipAddressHistory.SelectByPrimaryKeyAsync([connection.RemoteAddress]);
        if (ip == null)
        {
            IPAddressHistory newIp = new()
            {
                IPAddress = connection.RemoteAddress,
                Hits = 1,
                LastConnectionTime = currentDateTime
            };
            ip = await ipAddressHistory.InsertAsync(newIp);
        }
        else
        {
            ip.Hits++;
            ip.LastConnectionTime = currentDateTime;
            await ipAddressHistory.UpdateAsync(ip);
        }

        PortHistory? port = await portsHistory.SelectByPrimaryKeyAsync([connection.LocalPort]);
        if (port == null)
        {
            PortHistory newPort = new()
            {
                Port = connection.LocalPort,
                Hits = 1,
                LastConnectionTime = currentDateTime
            };
            await portsHistory.InsertAsync(newPort);
        }
        else
        {
            port.Hits++;
            port.LastConnectionTime = currentDateTime;
            await portsHistory.UpdateAsync(port);
        }

        /* On new IP or existing IP check is older than a day query AbuseIPDB webservice;
        * if confidence is >=75% blacklist IP */
        if (!string.IsNullOrWhiteSpace(abuseIPDB_Key))
        {
            AbuseIPDB_CheckedIP? abuseIP = await abuseIPDB_CheckedIP.SelectByPrimaryKeyAsync([connection.RemoteAddress]);
            double hoursFromLastRefreshed = 0;
            if (abuseIP != null)
                hoursFromLastRefreshed = (DateTime.Now - (abuseIP.LastReportedAt ?? DateTime.Now)).TotalHours;

            if (hoursFromLastRefreshed >= 24 || ip.Hits == 1) //If API last reported is more than 24 hours or first hit check
            {
                AbuseIPDBClient abuseIPDBClient = new(abuseIPDB_Key);
                CheckedIP checkedIP = await abuseIPDBClient.CheckIP(connection.RemoteAddress, true, 30);
                if (checkedIP.IPAddress != null)
                {
                    AbuseIPDB_CheckedIP dbCheckedIP = new()
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
                        LastReportedAt = checkedIP.LastReportedAt,
                        TotalReports = checkedIP.TotalReports,
                        UsageType = checkedIP.UsageType
                    };
                    await abuseIPDB_CheckedIP.UpsertAsync(dbCheckedIP);

                    if (checkedIP.AbuseConfidence >= 75)
                    {
                        ip.IsBlacklisted = 1;
                        await ipAddressHistory.UpdateAsync(ip);
                    }
                    else if (checkedIP.AbuseConfidence < 75)
                    {
                        ip.IsBlacklisted = 0;
                        await ipAddressHistory.UpdateAsync(ip);
                    }
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
    public IPAddressHistory? GetIPAddressHistoryAsync(string ip)
    {
        return ipAddressHistory.SelectByPrimaryKeyAsync([ip]).Result;
    }

}
