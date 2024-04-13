using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Interfaces;

namespace ReverseProxyServer.Helpers
{
    public record ConnectionInfo(DateTime connectionTime, string sessionId, ReverseProxyType reverseProxyType, string localAddress, int localPort, string remoteAddress, int remotePort) : IReverseProxyConnection
    {
        public DateTime ConnectionTime { get; init; } = connectionTime;
        public string SessionId { get; init; } = sessionId;
        public ReverseProxyType ProxyType { get; init; } = reverseProxyType;
        public string LocalAddress { get; init; } = localAddress;
        public int LocalPort { get; init; } = localPort;
        public string RemoteAddress { get; init; } = remoteAddress;
        public int RemotePort { get; init; } = remotePort;
    }
}