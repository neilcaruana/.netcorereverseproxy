using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Interfaces;
using ReverseProxyServer.Data;
using ReverseProxyServer.Logging.Converters;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReverseProxyServer;

internal class SettingsManager
{
    internal IProxyConfig LoadProxySettings()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = {
                    new JsonStringEnumConverter(),
                    new ProxyEndpointConfigConverter()
                }
        };

        var jsonContent = File.ReadAllText("appsettings.json");
        IProxyConfig settings = JsonSerializer.Deserialize<ProxyConfig>(jsonContent, options) ?? new ProxyConfig();

        /* Sentinel Mode;
         * Remove all Honeypot endpoints and create one that listens on almost all machine port range 1-65000
         * Leaving 535 ports available on the machine to avoid port exhaustion */
        if (settings.SentinelMode)
        {
            //Validate if we have at least one Honeypot endpoint
            var honeypot = settings.EndPoints.Where(s => s.ProxyType == ReverseProxyType.HoneyPot).FirstOrDefault();
            if (honeypot == null)
                throw new Exception("When in Sentinel mode you must have at least one Honeypot endpoint configured");

            //Keep the forwarding rules
            settings.EndPoints = settings.EndPoints.Where(s => s.ProxyType == ReverseProxyType.Forward).ToList();

            //Add one sentinel endpoint
            settings.EndPoints.Add(new ProxyEndpointConfig()
            {
                ListeningAddress = honeypot.ListeningAddress,
                ProxyType = ReverseProxyType.HoneyPot,
                ListeningPortRange = "1-65000",
                TargetHost = "localhost"
            });
        }

        return settings;
    }
}
