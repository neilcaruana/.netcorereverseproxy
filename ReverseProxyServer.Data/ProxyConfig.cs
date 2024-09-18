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
    public IEnumerable<IProxyEndpointConfig> EndPoints { get; init; }

    public ProxyConfig()
    {
        EndPoints = [];
    }
}
