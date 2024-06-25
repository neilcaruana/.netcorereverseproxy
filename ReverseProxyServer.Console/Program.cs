using ReverseProxyServer.Core;
using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Interfaces;
using ReverseProxyServer.Core.Logging;
using ReverseProxyServer.Extensions.AbuseIPDB;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ReverseProxyServer
{
    public class Program
    {
        private static ILogger logger = LoggerFactory.CreateDefaultCompositeLogger();
        private static readonly CancellationTokenSource reverseProxyCancellationSource = new();
        private static readonly CancellationTokenSource logCancellationSource = new();
        private static readonly SemaphoreSlim logSemaphore = new(1, 1);
        public static async Task Main(string[] args)
        {
            try
            {
                ConsoleHelper.LoadSplashScreen();
                Console.TreatControlCAsInput = true;
                Console.OutputEncoding = Encoding.UTF8;

                await logger.LogInfoAsync($"{Environment.OSVersion} {RuntimeInformation.FrameworkDescription}");
                await logger.LogInfoAsync("Loading settings...");

                IProxyConfig settings = ConsoleHelper.LoadProxySettings();
                logger = LoggerFactory.CreateCompositeLogger(settings.LogLevel, logCancellationSource.Token);

                await logger.LogInfoAsync($"Starting Reverse proxy server on {Dns.GetHostName()}");
                //Start the reverse proxy with the specified setting
                ReverseProxy reverseProxy = new(settings, reverseProxyCancellationSource.Token);

                reverseProxy.NewConnection += ReverseProxy_OnNewConnection;
                reverseProxy.Notification += ReverseProxy_Notification;
                reverseProxy.Error += ReverseProxy_Error;
                reverseProxy.Start();

                //Loop and check for user actions
                do
                {
                    try
                    {
                        ConsoleKeyInfo keyPressedInfo = Console.ReadKey(intercept: true);
                        switch (keyPressedInfo.Key)
                        {
                            //Handle Ctrl+C
                            case ConsoleKey.C when keyPressedInfo.Modifiers == ConsoleModifiers.Control:
                                await logger.LogWarningAsync("Caught signal interrupt: shutting down server...");
                                await reverseProxyCancellationSource.CancelAsync();
                                break;
                            case ConsoleKey.H:
                                await logSemaphore.WaitAsync(logCancellationSource.Token);
                                ConsoleHelper.DisplayHelp();
                                break;
                            case ConsoleKey.X:
                                await logSemaphore.WaitAsync(logCancellationSource.Token);
                                if (reverseProxy.TotalConnectionsCount > 0)
                                {
                                    await foreach (string result in ConsoleHelper.GetAbuseIPDBCrossReference(reverseProxy, true, 1))
                                    {
                                        await logger.LogInfoAsync(result);
                                    }
                                    await logger.LogInfoAsync("Search completed");
                                }
                                else
                                    await logger.LogWarningAsync("No connection history to check");
                                break;
                            case ConsoleKey.Z when keyPressedInfo.Modifiers == ConsoleModifiers.None:
                                await logSemaphore.WaitAsync(logCancellationSource.Token);
                                AbuseIPDBClient abuseIPDBClient = new("346ec4585ffe5c587c34760fe79e3f4b4b3ddb7ba3376592e7cb26d6ffa44422c92e096bde8ea64f");

                                Console.Write("Enter IP Address to check: ");
                                string ip = ConsoleHelper.ReadConsoleValueUntilEnter();
                                if (!IPAddress.TryParse(ip, out IPAddress? ipAddress))
                                {
                                    Console.WriteLine($"Invalid IP Address: {ip}");
                                    break;
                                }
                                Console.Write("Enter number of days for reports history : ");

                                string numberOfDays = ConsoleHelper.ReadConsoleValueUntilEnter();
                                int days = Convert.ToInt32(numberOfDays);
                                await logger.LogInfoAsync(ConsoleHelper.FormatAbuseIPDBCheckIP(await abuseIPDBClient.CheckIP(ip, true), days));
                                await Task.Delay(2000);
                                break;
                            case ConsoleKey.S:
                                await logSemaphore.WaitAsync(logCancellationSource.Token);
                                if (reverseProxy.TotalConnectionsCount > 0)
                                {
                                    StringBuilder stats = ConsoleHelper.GetStatistics(reverseProxy);
                                    await logger.LogInfoAsync(Environment.NewLine + String.Join(Environment.NewLine, stats));

                                    //Logic to scroll to top of report after displaying results
                                    int cursorBeforeScroll = Console.CursorTop;
                                    Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - stats.ToString().Split(Environment.NewLine).Length);
                                    //Wait for 5 seconds before log continues
                                    await Task.Delay(5000);
                                    Console.SetCursorPosition(Console.CursorLeft, cursorBeforeScroll);
                                }
                                else
                                    await logger.LogInfoAsync("No statistics generated");
                                break;
                            case ConsoleKey.A:
                                await logSemaphore.WaitAsync(logCancellationSource.Token);
                                if (reverseProxy.ActiveConnections.Any())
                                    await logger.LogInfoAsync(Environment.NewLine + ConsoleHelper.GetActiveConnections(reverseProxy));
                                else
                                    await logger.LogInfoAsync("No active connections");
                                break;
                            default:
                                await logger.LogWarningAsync($"Press Ctrl+C to shutdown server or h for help");
                                break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        await logger.LogDebugAsync($"Operation cancelled [Console operations]: User requested to stop reverse proxy");
                    }
                    catch (Exception ex)
                    {
                        await logger.LogErrorAsync(ex.GetBaseException().Message, ex);
                    }
                    finally
                    {
                        if (logSemaphore.CurrentCount == 0)
                            logSemaphore.Release();
                    }
                }
                while (!reverseProxyCancellationSource.IsCancellationRequested);

                await logger.LogInfoAsync($"Stopping Reverse proxy server...");
                if (reverseProxy.ActiveConnections.Any())
                    await logger.LogInfoAsync($"Waiting for all tasks to finish [{reverseProxy.ActiveConnections.Count()}]");

                reverseProxy.Stop().Wait(TimeSpan.FromSeconds(60));
                await logger.LogInfoAsync($"Stopped Reverse proxy server..." + (reverseProxy.ActiveConnections.Any() ? $" Some tasks did not finish {reverseProxy.ActiveConnections.Count()}" : ""));

                //Cancel all logging operations
                logCancellationSource.Cancel();
            }
            catch (OperationCanceledException)
            {
                await logger.LogDebugAsync($"Operation cancelled [General]: User requested to stop reverse proxy");
            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync("General failure" + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine + ex.GetBaseException().Message + Environment.NewLine + ex.GetBaseException().StackTrace, ex);
            }
        }

        private static async Task ReverseProxy_Error(object sender, NotificationErrorEventArgs e)
        {
            await logSemaphore.WaitAsync(logCancellationSource.Token);

            await logger.LogErrorAsync(e.ErrorMessage, e.Exception, e.SessionId);

            if (logSemaphore.CurrentCount == 0)
                logSemaphore.Release();
        }
        private static async Task ReverseProxy_Notification(object sender, NotificationEventArgs e)
        {
            await logSemaphore.WaitAsync(logCancellationSource.Token);
            switch (e.LogLevel)
            {
                case LogLevel.Info:
                    await logger.LogInfoAsync(e.Message, e.SessionId);
                    break;
                case LogLevel.Request:
                    await logger.LogRequestAsync(e.Message, e.SessionId);
                    break;
                case LogLevel.Warning:
                    await logger.LogWarningAsync(e.Message, e.SessionId);
                    break;
                case LogLevel.Debug:
                    await logger.LogDebugAsync(e.Message, e.SessionId);
                    break;
                default:
                    throw new Exception($"Not supported {e.LogLevel}");
            }

            if (logSemaphore.CurrentCount == 0)
                logSemaphore.Release();
        }
        private static async Task ReverseProxy_OnNewConnection(object sender, ConnectionEventArgs e)
        {
            await Task.CompletedTask;
        }
    }
}

