
using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Helpers;
using ReverseProxyServer.Core.Interfaces;
using System.Linq;
using System.Net;

namespace ReverseProxyServer.Data
{
    public class ProxyConfig : IProxyConfig
    {
        public int BufferSize { get; init; } = 4096;
        public LogLevel LogLevel { get; init; } = LogLevel.Debug;
        public int SendTimeout { get; init; }
        public int ReceiveTimeout { get; init; }
        public IEnumerable<IProxyEndpointConfig> EndPoints { get; init; }

        public ProxyConfig() : base()
        {
            EndPoints = [];
        }
    }
}