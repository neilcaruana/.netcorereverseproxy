using ReverseProxyServer.Core;
using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Helpers;
using ReverseProxyServer.Core.Interfaces;
using ReverseProxyServer.Data;
using ReverseProxyServer.Extensions.AbuseIPDB.Data;
using ReverseProxyServer.Extensions.AbuseIPDB;
using ReverseProxyServer.Logging.Converters;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Transactions;
using System.Net.NetworkInformation;

namespace ReverseProxyServer
{
    internal static class ConsoleHelper
    {
        internal static List<String> GetStatistics(ReverseProxy reverseProxy)
        {
            List<String> statisticsResult = [];
            statisticsResult.Add($"ReverseProxy statistics");
            statisticsResult.Add($"-----------------------");
            statisticsResult.Add($"Uptime: Running for {ProxyHelper.CalculateLastSeen(reverseProxy.StartedOn)}");
            statisticsResult.Add($"Proxy connections: {reverseProxy.Statistics.Count(s => s.ProxyType == ReverseProxyType.Forward)}");
            statisticsResult.Add($"Honeypot connections: {reverseProxy.Statistics.Count(s => s.ProxyType == ReverseProxyType.HoneyPot)}");
            statisticsResult.Add($"Total connections: {reverseProxy.TotalConnectionsCount}");
            statisticsResult.Add(GetActiveConnections(reverseProxy));

            var groupByRemoteIPs = reverseProxy.Statistics
                                                .GroupBy(stat => stat.RemoteAddress)
                                                .Select(group => new { RemoteAddress = group.Key, Count = group.Count(), LastConnectTime = group.Max(stat => stat.ConnectionTime) })
                                                .OrderByDescending(group => group.Count);

            statisticsResult.Add($"Hits by Unique IPs [{groupByRemoteIPs.Count()}]");
            foreach (var item in groupByRemoteIPs)
            {
                statisticsResult.Add($"\tIP: {item.RemoteAddress,-15} hit {item.Count:N0}x last seen {ProxyHelper.CalculateLastSeen(item.LastConnectTime)} ago");
            }

            var groupByLocalPorts = reverseProxy.Statistics
                                                .GroupBy(stat => stat.LocalPort)
                                                .Select(group => new { LocalPort = group.Key, Count = group.Count(), LastConnectTime = group.Max(stat => stat.ConnectionTime) })
                                                .OrderByDescending(group => group.Count);

            statisticsResult.Add(Environment.NewLine);
            statisticsResult.Add($"Hits by Unique Ports [{groupByLocalPorts.Count()}]");
            foreach (var item in groupByLocalPorts)
            {
                statisticsResult.Add($"\tPort: {item.LocalPort.ToString().PadRight(3, ' ')} hit {item.Count.ToString("N0")}x last hit {ProxyHelper.CalculateLastSeen(item.LastConnectTime)} ago");
            }

            return statisticsResult;
        }
        internal static void DisplayHelp()
        {
            Console.WriteLine("Help Menu");
            Console.WriteLine("-------------------------------------------------");
            Console.WriteLine("Ctrl+C           : Shutdown server");
            Console.WriteLine("H                : Display this help menu");
            Console.WriteLine("X                : Cross-reference connection history with AbuseIPDB");
            Console.WriteLine("Z                : Check specific IP address with AbuseIPDB");
            Console.WriteLine("S                : Display connection statistics");
            Console.WriteLine("A                : Display active connections");
            Console.WriteLine("-------------------------------------------------");
            Console.WriteLine(Environment.NewLine);
        }
        internal static string GetActiveConnections(ReverseProxy reverseProxy)
        {
            StringBuilder statisticsResult = new();
            statisticsResult.AppendLine($"Active connections: {reverseProxy.ActiveConnections.Count()}");
            foreach (var activeConnection in reverseProxy.ActiveConnections)
            {
                string cleanedConnectionInfo = $"[{activeConnection.SessionId}] {ProxyHelper.GetConnectionInfo(activeConnection)}";
                statisticsResult.AppendLine($"\t {cleanedConnectionInfo} \t started {ProxyHelper.CalculateLastSeen(activeConnection.ConnectionTime)} ago");
            }
            return statisticsResult.ToString();
        }
        internal async static IAsyncEnumerable<string> GetAbuseIPDBCrossReference(ReverseProxy reverseProxy, bool verbose = true, int days = 1)
        {
            AbuseIPDBClient abuseIPDBClient = new("346ec4585ffe5c587c34760fe79e3f4b4b3ddb7ba3376592e7cb26d6ffa44422c92e096bde8ea64f");
            yield return Environment.NewLine;
            yield return $"{Environment.NewLine}Cross referencing remote IPs with AbuseIPDB service{Environment.NewLine}";

            string result = "";
            IEnumerable<string> distinctTopRemoteIPs = reverseProxy.Statistics
                                                                   .GroupBy(stat => stat.RemoteAddress)
                                                                   .Select(group => group.Key)
                                                                   .OrderByDescending(remoteAddress => reverseProxy.Statistics.Count(stat => stat.RemoteAddress == remoteAddress))
                                                                   .Take(100);

            foreach (string remoteIP in distinctTopRemoteIPs)
            {
                try
                {
                    CheckedIP checkedip = await abuseIPDBClient.CheckIP(remoteIP, verbose, days);
                    result = FormatAbuseIPDBCheckIP(checkedip, days);
                }
                catch (Exception ex) 
                {
                    result = $"Error when requesting info on IP {remoteIP}. {ex.GetBaseException().Message}";
                }
                yield return result;
            }
        }
        internal static string FormatAbuseIPDBCheckIP(CheckedIP checkedip, int days)
        {
            return $"IP: {checkedip?.IPAddress?.PadRight(15, ' ')}  Confidence: {(checkedip?.AbuseConfidence.ToString() + "%").PadRight(4, ' ')}  Reports [{days} day(s)]: {checkedip?.TotalReports.ToString().PadRight(4, ' ')}  Country: {checkedip?.CountryCode}  Last reported: {ProxyHelper.CalculateLastSeen(checkedip?.LastReportedAt)} ago";
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
        internal static string ReadConsoleValueUntilEnter()
        {
            ConsoleKeyInfo keyInfo;
            string data = "";
            do
            {
                keyInfo = Console.ReadKey();

                //On enter key stop the loop
                if (keyInfo.Key == ConsoleKey.Enter)
                    break;

                //Remove last character from value
                if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    data = data[..^1];

                    //Remove value from console
                    Console.Write(' ');
                    Console.CursorLeft -= 1;
                }
                else //Any other key, accept value
                    data += keyInfo.KeyChar;

            } while (keyInfo.Key != ConsoleKey.Enter);

            //Reset line value in console, by overwriting with empty string
            Console.CursorLeft = 0;
            Console.Write(new string(' ', 100));
            Console.CursorLeft = 0;
            return data;
        }
    }
}
