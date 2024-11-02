using Kavalan.Logging;
using ReverseProxyServer.Core.Interfaces;
using System.Text.Json.Serialization;

namespace ReverseProxyServer.Data;

public class ProxyConfig : IProxyConfig
{
    public string DatabasePath { get; init; } = string.Empty;
    [JsonIgnore]
    public bool DatabaseEnabled { get { return !string.IsNullOrWhiteSpace(this.DatabasePath); } }
    public string AbuseIPDBApiKey { get; init; } = string.Empty;
    public int BufferSize { get; init; } = 4096;
    public LogLevel LogLevel { get; init; } = LogLevel.Debug;
    public int SendTimeout { get; init; }
    public int ReceiveTimeout { get; init; }
    public bool SentinelMode { get; init; } = false;
    public IList<IProxyEndpointConfig> EndPoints { get; set; }
    [JsonIgnore]
    public int FaultyEndpoints { get; set; }
    public ProxyConfig()
    {
        EndPoints = [];
    }
}
