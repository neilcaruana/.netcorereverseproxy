using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Interfaces;

namespace ReverseProxyServer.Core
{
    public class ConnectionEventArgs(DateTime connectionTime, string sessionId, ReverseProxyType reverseProxyType, CommunicationDirection communicationDirection,
                                string localAddress, int localPort, string targetHost, int targetPort, string remoteAddress,
                                int remotePort, string countryName, bool isBlackListed = false) : EventArgs, IReverseProxyConnection
    {
        public DateTime ConnectionTime { get; init; } = connectionTime;
        public string SessionId { get; init; } = sessionId;
        public ReverseProxyType ProxyType { get; init; } = reverseProxyType;
        public CommunicationDirection CommunicationDirection { get; init; } = communicationDirection;
        public string LocalAddress { get; init; } = localAddress;
        public int LocalPort { get; init; } = localPort;
        public string TargetHost { get; init; } = targetHost;
        public int TargetPort { get; init; } = targetPort;
        public string RemoteAddress { get; init; } = remoteAddress;
        public int RemotePort { get; init; } = remotePort;
        public bool IsBlacklisted { get; set; } = isBlackListed;
        public string CountryName { get; set; } = countryName;
    }
}