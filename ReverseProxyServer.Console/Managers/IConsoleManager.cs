using ReverseProxyServer.Data.DTO;

namespace ReverseProxyServer
{
    internal interface IConsoleManager
    {
        Task<AbuseIPDB_CheckedIP?> GetAbuseIPDB_CheckedIPAsync(string ip);
        Task<long> GetApiConnectionsForInstance(DateTime startedOn);
        Task<List<Connection>> GetConnections(string instanceId);
        Task<IPAddressHistory?> GetIPAddressHistoryAsync(string ip);
        Task InsertNewConnectionData(ConnectionData connectionData);
        Task<string> RegisterConnectionDetails(Connection connection, string abuseIPDB_Key);
        Instance RegisterServer(string version);
        void UpdateServerInstanceAsStopped(Instance instance);
    }
}