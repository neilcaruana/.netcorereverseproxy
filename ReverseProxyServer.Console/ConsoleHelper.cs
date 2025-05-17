using Kavalan.Core;
using ReverseProxyServer.Core;
using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Helpers;
using ReverseProxyServer.Data.DTO;
using ReverseProxyServer.Extensions.AbuseIPDB;
using ReverseProxyServer.Extensions.AbuseIPDB.Data;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ReverseProxyServer
{
    internal class ConsoleHelper()
    {
        internal async Task<StringBuilder> GetStatistics(IConsoleManager consoleManager, ReverseProxy reverseProxy, Instance instance)
        {
            List<Connection> connections = await consoleManager.GetConnections(instance.InstanceId);
            long apiRequestsForInstance = await consoleManager.GetApiConnectionsForInstance(instance.StartTime);

            StringBuilder statisticsResult = new();
            statisticsResult.AppendLine($"ReverseProxy statistics");
            statisticsResult.AppendLine($"-----------------------");
            statisticsResult.AppendLine($"Uptime: Running for {reverseProxy.StartedOn.CalculateLastSeen()}");
            statisticsResult.AppendLine($"API connections: {apiRequestsForInstance:N0}");
            statisticsResult.AppendLine($"Proxy connections: {connections.Count(s => s.ProxyType == ReverseProxyType.Forward.ToString()):N0}");
            statisticsResult.AppendLine($"Honeypot connections: {connections.Count(s => s.ProxyType == ReverseProxyType.HoneyPot.ToString()):N0}");
            statisticsResult.AppendLine($"Total connections: {reverseProxy.TotalConnectionsReceived:N0}{Environment.NewLine}");
            statisticsResult.AppendLine(GetActiveConnections(reverseProxy));

            var groupByRemoteIPs = connections.GroupBy(stat => stat.RemoteAddress)
                                               .Select(group => new { RemoteAddress = group.Key, Count = group.Count(), LastConnectTime = group.Max(stat => stat.ConnectionTime) })
                                               .OrderByDescending(group => group.Count);

            statisticsResult.AppendLine($"Hits by Unique IPs [{groupByRemoteIPs.Count()}]");
            foreach (var ip in groupByRemoteIPs)
            {
                AbuseIPDB_CheckedIP? ipDB = await consoleManager.GetAbuseIPDB_CheckedIPAsync(ip.RemoteAddress);
                statisticsResult.AppendLine($"\t{(ipDB != null ? "Country: " + ipDB.CountryName?.PadRight(52, ' ') : " ")}\tIP: {ip.RemoteAddress,-15}\tHits: {ip.Count:N0}x\tlast seen {ip.LastConnectTime.CalculateLastSeen()}");
            }

            var groupByLocalPorts = connections.GroupBy(stat => stat.LocalPort)
                                               .Select(group => new { LocalPort = group.Key, Count = group.Count(), LastConnectTime = group.Max(stat => stat.ConnectionTime) })
                                               .OrderByDescending(group => group.Count);

            statisticsResult.AppendLine(Environment.NewLine);
            statisticsResult.AppendLine($"Hits by Unique Ports [{groupByLocalPorts.Count()}]");
            foreach (var port in groupByLocalPorts)
            {
                statisticsResult.AppendLine($"\tPort: {port.LocalPort.ToString().PadRight(3, ' ')} hit {port.Count.ToString("N0")}x last hit {port.LastConnectTime.CalculateLastSeen()} ago");
            }
            string[] result = statisticsResult.ToString().Split(Environment.NewLine);
            return new StringBuilder(string.Join(Environment.NewLine, result[..Math.Min(7000, result.Length)]));
        }
        internal void DisplayHelp()
        {
            Console.WriteLine("Help Menu");
            Console.WriteLine("-------------------------------------------------");
            Console.WriteLine("Ctrl+C           : Shutdown server");
            Console.WriteLine("H                : Display this help menu");
            Console.WriteLine("D                : Change log level");
            Console.WriteLine("X                : Cross-reference connection history with AbuseIPDB");
            Console.WriteLine("Z                : Check specific IP address with AbuseIPDB");
            Console.WriteLine("S                : Display connection statistics");
            Console.WriteLine("A                : Display active connections");
            Console.WriteLine("-------------------------------------------------");
            Console.WriteLine(Environment.NewLine);
        }
        internal string GetActiveConnections(ReverseProxy reverseProxy)
        {
            StringBuilder statisticsResult = new();
            statisticsResult.AppendLine($"Active connections: {reverseProxy.ActiveConnections.Count()}");
            foreach (var activeConnection in reverseProxy.ActiveConnections)
            {
                string cleanedConnectionInfo = $"[{activeConnection.SessionId}] {ProxyHelper.GetConnectionInfo(activeConnection)}";
                statisticsResult.AppendLine($"\t {cleanedConnectionInfo} \t started {activeConnection.ConnectionTime.CalculateLastSeen()} ago");
            }
            return statisticsResult.ToString();
        }
        internal async IAsyncEnumerable<string> GetAbuseIPDBCrossReference(IConsoleManager consoleManager, Instance instance, bool verbose = true, int days = 1)
        {
            AbuseIPDBClient abuseIPDBClient = new("346ec4585ffe5c587c34760fe79e3f4b4b3ddb7ba3376592e7cb26d6ffa44422c92e096bde8ea64f");
            yield return Environment.NewLine;
            yield return $"{Environment.NewLine}Cross referencing remote IPs with AbuseIPDB service{Environment.NewLine}";

            string result = "";
            var connections = await consoleManager.GetConnections(instance.InstanceId);
            var topRemoteIPs = connections.GroupBy(stat => stat.RemoteAddress)
                                                                  .Select(group => new { group.Key, Count = group.Count() })
                                                                  .OrderByDescending(group => group.Count)
                                                                  .Take(100);

            foreach (var remoteIP in topRemoteIPs.ToList())
            {
                try
                {
                    CheckedIP checkedIP = await abuseIPDBClient.CheckIP(remoteIP.Key, verbose, days);
                    result = FormatAbuseIPDBCheckIP(checkedIP, days, remoteIP.Count);
                }
                catch (Exception ex)
                {
                    result = $"Error when requesting info on IP {remoteIP}. {ex.GetBaseException().Message}";
                }
                yield return result;
            }
        }
        internal string FormatAbuseIPDBCheckIP(CheckedIP checkedip, int days, int localHits)
        {
            return $"IP: {checkedip?.IPAddress?.PadRight(15, ' ')} {(localHits > 0 ? $"Hits: {localHits}" : "")} Confidence: {(checkedip?.AbuseConfidence.ToString() + "%").PadRight(4, ' ')}  Reports [{days} day(s)]: {checkedip?.TotalReports.ToString().PadRight(4, ' ')}  Country: {checkedip?.CountryCode}  Reported: {checkedip?.LastReportedAt.CalculateLastSeen()} ago";
        }
        internal string GetFileVersion()
        {
            var attribute = Assembly.GetExecutingAssembly()
                                    .GetCustomAttribute<AssemblyFileVersionAttribute>();
            return attribute != null ? attribute.Version : "File Version not found";
        }
        internal void LoadSplashScreen()
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
        internal string ReadConsoleValueUntilEnter()
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
        internal void MoveCursorToPositionWithWait(int numberOfLines, string waitMessage)
        {
            int newPosition = Console.CursorTop - numberOfLines;
            int cursorBeforeMove = Console.CursorTop;

            //Move cursor to new position and clear line (No way to read existing text)
            Console.SetCursorPosition(0, newPosition);
            Console.Write(new string(' ', 100));
            Console.SetCursorPosition(0, newPosition);
            Console.Write(waitMessage + "... Press any key to continue ");

            CancellationTokenSource cancellationTokenSource = new();
            //Show blinking cursor and wait for key
            _ = ShowWaitCursorAsync(cancellationTokenSource.Token);

            Console.ReadKey(intercept: true);
            cancellationTokenSource.Cancel();
            Console.ResetColor();
            Console.SetCursorPosition(0, cursorBeforeMove);
        }
        internal async Task ShowWaitCursorAsync(CancellationToken cancellationToken)
        {
            var cursorSymbols = new[] { '|', '/', '-', '\\' };
            int cursorIndex = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                SetRandomConsoleColor();

                // Save the current cursor position
                var originalCursorLeft = Console.CursorLeft;
                var originalCursorTop = Console.CursorTop;

                // Write the cursor symbol
                Console.Write(cursorSymbols[cursorIndex]);

                // Move the cursor back to the original position
                Console.SetCursorPosition(originalCursorLeft, originalCursorTop);

                // Wait before changing the cursor symbol
                await Task.Delay(100, cancellationToken);

                // Update the cursor symbol index
                cursorIndex = (cursorIndex + 1) % cursorSymbols.Length;
            }

            // Clear the cursor symbol after stopping
            Console.ResetColor();
            var currentCursorLeft = Console.CursorLeft;
            var currentCursorTop = Console.CursorTop;
            Console.SetCursorPosition(currentCursorLeft - 5, currentCursorTop);
            Console.Write("     ");
            Console.SetCursorPosition(currentCursorLeft, currentCursorTop);
        }
        internal void SetRandomConsoleColor()
        {
            // Exclude the background color if desired (optional)
            var consoleColors = Enum.GetValues(typeof(ConsoleColor)).Cast<ConsoleColor>().Where(c => c != Console.BackgroundColor).ToArray();

            // Create a Random object
            Random random = new();

            // Generate a random index based on the number of console colors
            int randomIndex = random.Next(consoleColors.Length);

            // Set the console foreground color to the random color
            Console.ForegroundColor = (ConsoleColor)(consoleColors.GetValue(randomIndex) ?? ConsoleColor.Red);
        }
    }
}
