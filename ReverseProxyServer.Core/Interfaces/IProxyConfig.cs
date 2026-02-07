using Kavalan.Logging;

namespace ReverseProxyServer.Core.Interfaces;

public interface IProxyConfig
{
    public string DatabasePath { get; init; }
    public bool DatabaseEnabled { get; }
    public string AbuseIPDBApiKey { get; init; }
    public int SendTimeout { get; init; }
    public int ReceiveTimeout { get; init; }
    public int BufferSize { get; init; }
    public bool SentinelMode { get; init; }
    public LogLevel LogLevel { get; init; }
    public string DashboardUrl { get; init; }
    public IList<IProxyEndpointConfig> EndPoints { get; set; }
    public int FaultyEndpoints { get; set; }
}
