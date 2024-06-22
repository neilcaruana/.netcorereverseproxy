using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Interfaces;

namespace ReverseProxyServer.Core.Helpers
{
    public static class ProxySettingsValidator
    {
        public static void Validate(IProxyConfig settings)
        {
            _ = settings ?? throw new ArgumentNullException(nameof(settings), "Settings cannot be null");

            if (settings.EndPoints is null || !settings.EndPoints.Any())
                throw new ArgumentException("No endpoints specified", nameof(settings.EndPoints));

            if (settings.BufferSize <= 0)
                throw new ArgumentException("BufferSize", "Value must be between 0 and 65535");

            foreach (IProxyEndpointConfig endpoint in settings.EndPoints)
            {
                if (string.IsNullOrEmpty(endpoint.ListeningPortRange))
                    throw new ArgumentNullException("ListeningPortRange", "Please specify a listening port range ex. 8080:9000 or 8000");

                // Assuming endpoint.ListeningPortRange is a string like "80-100" or "80"
                var portRange = endpoint.ListeningPortRange.Split('-');
                int endingPort = -1;
                // Parse starting port
                if (!int.TryParse(portRange[0], out int startingPort) || startingPort < 0 || startingPort > 65535)
                    throw new ArgumentException($"Invalid starting port specified: {portRange[0]}", nameof(endpoint.ListeningPortRange));

                // If a range is specified, parse ending port
                if (portRange.Length == 2 && (!int.TryParse(portRange[1], out endingPort) || endingPort < 0 || endingPort > 65535))
                    throw new ArgumentException($"Invalid ending port specified: {portRange[1]}", nameof(endpoint.ListeningPortRange));
                else if (portRange.Length == 1)
                    // If only one port is specified, treat it as both starting and ending port
                    endingPort = startingPort;

                // Validate port range order
                if (startingPort > endingPort)
                    throw new ArgumentException($"Starting port must be less than or equal to ending port: {endpoint.ListeningPortRange}", nameof(endpoint.ListeningPortRange));

                if (string.IsNullOrEmpty(endpoint.ListeningAddress))
                    throw new ArgumentNullException("ListeningAddress", "Please specify a listening address");

                if (string.IsNullOrEmpty(endpoint.TargetHost))
                    throw new ArgumentNullException("TargetHost", "Please specify a target host address");

                //Only validate target port when forwarding requests
                if (endpoint.ProxyType == ReverseProxyType.Forward)
                {
                    if (endpoint.TargetPort < 0 || endpoint.TargetPort > 65535)
                        throw new Exception($"Invalid target port specified {endpoint.TargetPort}");
                }
            }
        }
    }
}
