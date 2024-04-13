using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using ReverseProxyServer.Core;
using ReverseProxyServer.Core.Helpers;
using ReverseProxyServer.Core.Interfaces;
using ReverseProxyServer.Core.Logging;
using ReverseProxyServer.Data;
using ReverseProxySever.Logging.Converters;
using System.Net;

namespace ReverseProxyServer
{
    public class Program
    {
        private static readonly ILogger logger = LoggerFactory.CreateDefaultCompositeLogger();
        public static async Task Main(string[] args)
        {
            try
            {
                ConsoleHelpers.LoadSplashScreen();
                Console.TreatControlCAsInput = true;
                CancellationTokenSource cancellationTokenSource = new();

                await logger.LogInfoAsync($"{Environment.OSVersion} {RuntimeInformation.FrameworkDescription}");
                await logger.LogInfoAsync("Loading settings...");
                IProxyConfig settings = ConsoleHelpers.LoadProxySettings();

                await logger.LogInfoAsync("Starting Reverse proxy server");
                //Start the reverse proxy with the specified setting
                ReverseProxy reverseProxy = new(settings, cancellationTokenSource.Token, LoggerFactory.CreateCompositeLogger(settings.LogLevel));
                reverseProxy.Start();

                 //Loop and check for user actions
                do
                {
                    ConsoleKeyInfo keyPressedInfo = Console.ReadKey(intercept: true);
                    switch (keyPressedInfo.Key)
                    {
                        //Handle Ctrl+C
                        case ConsoleKey.C when keyPressedInfo.Modifiers == ConsoleModifiers.Control:
                            await logger.LogInfoAsync("Caught signal interrupt: shutting down server...");
                            cancellationTokenSource.Cancel();
                            break;
                        case ConsoleKey.S:
                            if (reverseProxy.TotalConnectionsCount > 0)
                                await logger.LogInfoAsync(Environment.NewLine + ConsoleHelpers.DisplayStatistics(reverseProxy));
                            else
                                await logger.LogWarningAsync("No statistics generated");
                            break;
                        case ConsoleKey.A:
                            if (reverseProxy.ActiveConnections.Any())
                                await logger.LogInfoAsync(Environment.NewLine+ ConsoleHelpers.GetActiveConnections(reverseProxy));
                            else
                                await logger.LogWarningAsync("No active connections");
                            break;
                        default:
                            await logger.LogInfoAsync($"Press Ctrl+C to shutdown server or h for help");
                            break;
                    }
                } 
                while (!cancellationTokenSource.IsCancellationRequested);

                await logger.LogWarningAsync($"Stopping Reverse proxy server...");
                if (reverseProxy.PendingConnectionsCount > 0)
                    await logger.LogWarningAsync($"Waiting for all tasks to finish [{reverseProxy.PendingConnectionsCount}]");

                reverseProxy.Stop().Wait(TimeSpan.FromSeconds(10));
                await logger.LogInfoAsync($"Stopped Reverse proxy server..." + (reverseProxy.PendingConnectionsCount > 0 ? $" Some tasks did not finish {reverseProxy.PendingConnectionsCount}" : ""));
            }
            //TODO: handling of timeouts
            catch (Exception ex)
            {
                await logger.LogErrorAsync("General failure", ex);
            }
        }
    }
}
