using ReverseProxyServer.Core.Enums.ProxyEnums;
using System.Text.Json.Serialization;

namespace ReverseProxyServer.Core.Interfaces
{
    public interface IProxyEndpointConfig
    {
        public ReverseProxyType ProxyType { get; init; }
        public string ListeningAddress { get; init; }
        public string ListeningPortRange { get; init; }
        public string TargetHost { get; init; }
        public int TargetPort { get; init; }

        [JsonIgnore]
        public int ListeningStartingPort
        {
            get
            {
                var ports = ListeningPortRange?.Split(':');
                if (ports?.Length > 0 && int.TryParse(ports[0], out int startingPort))
                {
                    return startingPort;
                }
                throw new InvalidOperationException("Invalid or unspecified Starting Port.");
            }
        }

        [JsonIgnore]
        public int ListeningEndingPort
        {
            get
            {
                var ports = ListeningPortRange?.Split(':');
                // If only one port is specified, or two ports are specified and the second is valid
                if (ports?.Length == 1 && int.TryParse(ports[0], out int port))
                {
                    return port;
                }
                else if (ports?.Length == 2 && int.TryParse(ports[1], out int endingPort))
                {
                    return endingPort;
                }
                throw new InvalidOperationException("Invalid or unspecified Ending Port.");
            }
        }
    }
}