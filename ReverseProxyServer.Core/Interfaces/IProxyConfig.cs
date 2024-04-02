using ReverseProxyServer.Core.Enums.ProxyEnums;

namespace ReverseProxyServer.Core.Interfaces
{
    public interface IProxyConfig
    {
        public int SendTimeout { get; init; }
        public int ReceiveTimeout { get; init; }
        public int BufferSize { get; init; }
        public LogLevel LogLevel { get; init; }
        public IEnumerable<IProxyEndpointConfig> EndPoints { get; init; }
    }
}