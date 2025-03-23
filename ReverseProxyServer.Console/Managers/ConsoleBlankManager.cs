using ReverseProxyServer.Data.DTO;

namespace ReverseProxyServer
{
    //This is used for no database support
    internal class ConsoleBlankManager : IConsoleManager
    {
        public Task<AbuseIPDB_CheckedIP?> GetAbuseIPDB_CheckedIPAsync(string ip)
        {
            return Task.FromResult<AbuseIPDB_CheckedIP?>(new AbuseIPDB_CheckedIP());
        }
        public Task<long> GetApiConnectionsForInstance(DateTime startedOn)
        {
            return Task.FromResult<long>(0);
        }
        public Task<List<Connection>> GetConnections(string instanceId)
        {
            return Task.FromResult<List<Connection>>([]);
        }
        public Task<IPAddressHistory?> GetIPAddressHistoryAsync(string ip)
        {
            return Task.FromResult<IPAddressHistory?>(new IPAddressHistory());
        }
        public Task InsertNewConnectionData(ConnectionData connectionData)
        {
            return Task.CompletedTask;
        }
        public Task<string> RegisterConnectionDetails(Connection connection, string abuseIPDB_Key)
        {
            return Task.FromResult("");
        }
        public Instance RegisterServer(string version)
        {
            return new Instance();
        }
        public void UpdateServerInstanceAsStopped(Instance? instance)
        {
            return;
        }
    }
}
