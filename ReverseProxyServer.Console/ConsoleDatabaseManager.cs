using ReverseProxyServer.Core.Helpers;
using ReverseProxyServer.Data.DTO;
using ReverseProxyServer.Data.Sqlite;
using ReverseProxyServer.Repositories;

namespace ReverseProxyServer
{
    internal class ConsoleDatabaseManager
    {
        private GenericSqliteRepository<Instance> instances;
        private GenericSqliteRepository<Connection> connections;
        private GenericSqliteRepository<ConnectionData> connectionsData;
        private GenericSqliteRepository<IPAddressHistory> ipAddressHistory;
        private GenericSqliteRepository<PortHistory> portsHistory;
        private readonly bool enabled = false;
        private readonly string databasePath = "";
        public ConsoleDatabaseManager(string databasePath)
        {
            this.databasePath = databasePath;
            enabled = !string.IsNullOrWhiteSpace(databasePath);
            instances = new GenericSqliteRepository<Instance>(databasePath);
            connections = new GenericSqliteRepository<Connection>(databasePath);
            connectionsData = new GenericSqliteRepository<ConnectionData>(databasePath);
            ipAddressHistory = new GenericSqliteRepository<IPAddressHistory>(databasePath);
            portsHistory = new GenericSqliteRepository<PortHistory>(databasePath);

        }
        public Instance? RegisterServer()
        {
            if (!enabled) return null;
            string createDbScript = ProxyHelper.ReadResourceFile("ReverseProxyServer.Console.Resources.CreateDatabase.sql");
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
        public async Task RegisterConnectionDetails(Connection connection)
        {
            if (!enabled) return;
            await connections.InsertAsync(connection);

            DateTime currentDateTime = DateTime.Now;
            IPAddressHistory? ip = await ipAddressHistory.GetByPrimaryKeyAsync(connection.RemoteAddress);
            if (ip == null)
            {
                IPAddressHistory newIp = new()
                {
                    IP = connection.RemoteAddress,
                    Hits = 1,
                    LastConnectionTime = currentDateTime
                };
                await ipAddressHistory.InsertAsync(newIp);
            }
            else
            {
                ip.Hits++;
                ip.LastConnectionTime = currentDateTime;
                await ipAddressHistory.UpdateAsync(ip);
            }

            PortHistory? port = await portsHistory.GetByPrimaryKeyAsync(connection.LocalPort);
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
        }
        public async Task InsertNewConnectionData(ConnectionData connectionData)
        {
            if (!enabled) return;
            await connectionsData.InsertAsync(connectionData);
        }
        public async Task<List<Connection>> GetConnections(string instanceId)
        {
            return await connections.GetListDataByFieldValueAsync("InstanceId", instanceId);
        }

    }
}
