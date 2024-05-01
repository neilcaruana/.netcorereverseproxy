using ReverseProxyServer.Core.Enums.ProxyEnums;

namespace ReverseProxyServer.Core.Interfaces
{
    public interface IReverseProxyConnection
    {
        DateTime ConnectionTime { get; init; }
        string SessionId { get; init; }
        ReverseProxyType ProxyType { get; init; }
        CommunicationDirection CommunicationDirection { get; init; }
        string LocalAddress { get; init; }
        int LocalPort { get; init; }
        public string TargetHost { get; init; }
        public int TargetPort { get; init; }
        string RemoteAddress { get; init; }
        int RemotePort { get; init; }
    }
}