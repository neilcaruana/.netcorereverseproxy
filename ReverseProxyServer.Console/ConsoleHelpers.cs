using ReverseProxyServer.Core;
using ReverseProxyServer.Core.Helpers;
using ReverseProxyServer.Core.Interfaces;
using ReverseProxyServer.Core.Logging;
using ReverseProxyServer.Data;
using ReverseProxySever.Logging.Converters;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReverseProxyServer
{
    internal static class ConsoleHelpers
    {
        internal static string DisplayStatistics(ReverseProxy reverseProxy)
        {
            StringBuilder statisticsResult = new();
            statisticsResult.AppendLine($"Proxy statistics");
            statisticsResult.AppendLine($"----------------");
            statisticsResult.AppendLine();
            statisticsResult.AppendLine($"Total connections: {reverseProxy.TotalConnectionsCount}");
            statisticsResult.AppendLine(GetActiveConnections(reverseProxy));

            var groupByRemoteIPs = reverseProxy.Statistics
                                                .GroupBy(stat => stat.RemoteAddress)
                                                .Select(group => new { RemoteAddress = group.Key, Count = group.Count(), LastConnectTime = group.Max(stat => stat.ConnectionTime) })
                                                .OrderByDescending(group => group.Count);

            statisticsResult.AppendLine($"Hits by Unique IPs [{groupByRemoteIPs.Count()}]");
            foreach (var item in groupByRemoteIPs)
            {
                statisticsResult.AppendLine($"\tIP: {item.RemoteAddress.PadRight(15, ' ')} hit {item.Count.ToString("N0")}x last seen {ProxyHelper.CalculateLastSeen(item.LastConnectTime)}");
            }
            statisticsResult.AppendLine();

            var groupByLocalPorts = reverseProxy.Statistics
                                                .GroupBy(stat => stat.LocalPort)
                                                .Select(group => new { LocalPort = group.Key, Count = group.Count(), LastConnectTime = group.Max(stat => stat.ConnectionTime) })
                                                .OrderByDescending(group => group.Count);

            statisticsResult.AppendLine($"Hits by Unique Ports [{groupByLocalPorts.Count()}]");
            foreach (var item in groupByLocalPorts)
            {
                statisticsResult.AppendLine($"\tPort: {item.LocalPort.ToString().PadRight(3, ' ')} hit {item.Count.ToString("N0")}x last hit {ProxyHelper.CalculateLastSeen(item.LastConnectTime)}");
            }

            return statisticsResult.ToString();
        }
        internal static string GetActiveConnections(ReverseProxy reverseProxy)
        {
            StringBuilder statisticsResult = new();
            statisticsResult.AppendLine($"Active connections: {reverseProxy.PendingConnectionsCount}");
            foreach (var activeConnection in reverseProxy.ActiveConnections)
            {
                string cleanedConnectionInfo = $"[{activeConnection.SessionId}] {ProxyHelper.GetConnectionInfoString(activeConnection)}";
                statisticsResult.AppendLine("\t" + cleanedConnectionInfo + "\t" + "started " + ProxyHelper.CalculateLastSeen(activeConnection.ConnectionTime));
            }
            statisticsResult.AppendLine();
            return statisticsResult.ToString();
        }
        internal static IProxyConfig LoadProxySettings()
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
            return JsonSerializer.Deserialize<ProxyConfig>(jsonContent, options) ?? new ProxyConfig();
        }
        internal static void LoadSplashScreen()
        {
            //Get max between current and desired window size
            int windowWidth = Math.Max(100, Console.WindowWidth);
            int windowHeight = Math.Max(40, Console.WindowHeight);

            // Ensure the buffer is big enough for the window size
            int bufferWidth = Math.Max(Console.BufferWidth, windowWidth);
            int bufferHeight = Math.Max(Console.BufferHeight, windowHeight);

            // Set window size first if making the buffer smaller
            if (Console.BufferWidth >= bufferWidth && Console.BufferHeight >= bufferHeight)
            {
                Console.SetWindowSize(windowWidth, windowHeight);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Console.SetBufferSize(bufferWidth, bufferHeight);
            }
            else
            {
                // Increase buffer size before setting window size to avoid errors
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Console.SetBufferSize(bufferWidth, bufferHeight);
                Console.SetWindowSize(windowWidth, windowHeight);
            }

            // Now, it's safe to reposition the window if needed
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Console.SetWindowPosition(0, 0); // Move window to top-left corner

            string[] lines = [
            @"   _   _ _____ _____   ____                                ____                      ",
            @"  | \ | | ____|_   _| |  _ \ _____   _____ _ __ ___  ___  |  _ \ _ __ _____  ___   _ ",
            @"  |  \| |  _|   | |   | |_) / _ \ \ / / _ | '__/ __|/ _ \ | |_) | '__/ _ \ \/ | | | |",
            @" _| |\  | |___  | |   |  _ |  __/\ V |  __| |  \__ |  __/ |  __/| | | (_) >  <| |_| |",
            @"(_|_| \_|_____| |_|   |_| \_\___| \_/ \___|_|  |___/\___| |_|   |_|  \___/_/\_\\__, |",
            @"                                                                                |___/"];

            foreach (string line in lines)
            {
                Console.WriteLine(line);
                _ = Task.Delay(100);
            }
        }
    }
}