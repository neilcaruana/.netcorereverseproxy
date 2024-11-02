using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Interfaces;

namespace ReverseProxyServer.Data;
public class ProxyEndpointConfig : IProxyEndpointConfig
{
    public ReverseProxyType ProxyType { get; init; } = ReverseProxyType.HoneyPot;
    public string ListeningAddress { get; init; } = string.Empty;
    public string ListeningPortRange { get; init; } = string.Empty;
    public string TargetHost { get; init; } = string.Empty;
    public int TargetPort { get; init; } = 0;
    public bool IsFaulty { get; set; } = false;
}
