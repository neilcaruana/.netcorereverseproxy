﻿using Kavalan.Logging;
using ReverseProxyServer.Core;
using ReverseProxyServer.Core.Helpers;
using ReverseProxyServer.Core.Interfaces;
using ReverseProxyServer.Data.DTO;
using ReverseProxyServer.Extensions.AbuseIPDB;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace ReverseProxyServer
{
    public class Program
    {
        private static ILogger logger = LoggerFactory.CreateDefaultCompositeLogger();
        private static IProxyConfig? settings;
        private static Instance? serverDBInstance;
        private static ConsoleDatabaseManager? consoleDatabaseManager;
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
                settings = ConsoleHelper.LoadProxySettings();
                 
                logger = LoggerFactory.CreateCompositeLogger(settings.LogLevel, logCancellationSource.Token);
                if (settings.SentinelMode)
                    await logger.LogWarningAsync($"Sentinel mode detected!");

                await logger.LogInfoAsync($"Starting Reverse proxy server on {Dns.GetHostName()}");
                //Load database manager (works only if database path is set in config file)
                consoleDatabaseManager = new ConsoleDatabaseManager(settings.DatabasePath);
                serverDBInstance = consoleDatabaseManager.RegisterServer();

                //Start the reverse proxy with the specified setting
                ReverseProxy reverseProxy = new(settings, reverseProxyCancellationSource.Token);
                reverseProxy.BeforeNewConnection += ReverseProxy_BeforeNewConnection;
                reverseProxy.NewConnection += ReverseProxy_OnNewConnection;
                reverseProxy.NewConnectionData += ReverseProxy_NewConnectionData;
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
                                if (reverseProxy.TotalConnectionsReceived > 0)
                                {
                                    if (!settings.DatabaseEnabled)
                                    {
                                        await logger.LogWarningAsync("Database not enabled, operation not supported");
                                        break;
                                    }
                                    await foreach (string result in ConsoleHelper.GetAbuseIPDBCrossReference(consoleDatabaseManager, serverDBInstance, true, 1))
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
                                AbuseIPDBClient abuseIPDBClient = new(settings.AbuseIPDBApiKey);

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
                                await logger.LogInfoAsync(ConsoleHelper.FormatAbuseIPDBCheckIP(await abuseIPDBClient.CheckIP(ip, true), days, 0));
                                await Task.Delay(2000);
                                break;
                            case ConsoleKey.S:
                                await logSemaphore.WaitAsync(logCancellationSource.Token);
                                if (reverseProxy.TotalConnectionsReceived > 0)
                                {
                                    if (!settings.DatabaseEnabled)
                                    {
                                        await logger.LogWarningAsync("Database not enabled, operation not supported");
                                        break;
                                    }
                                    StringBuilder stats = await ConsoleHelper.GetStatistics(reverseProxy, consoleDatabaseManager, serverDBInstance);
                                    await logger.LogInfoAsync(Environment.NewLine + Environment.NewLine + stats.ToString());

                                    int numberOfLines = stats.ToString().Split(Environment.NewLine).Length;

                                    ConsoleHelper.MoveCursorToPositionWithWait(numberOfLines, "ReverseProxy statistics");
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
                            case ConsoleKey.D:

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
                //Checks for active connections and displays them
                if (reverseProxy.ActiveConnections.Any())
                {
                    await logger.LogInfoAsync($"Waiting for all tasks to finish [{reverseProxy.ActiveConnections.Count()}]");
                    foreach (IReverseProxyConnection activeConnection in reverseProxy.ActiveConnections)
                        await logger.LogInfoAsync(ProxyHelper.GetConnectionInfo(activeConnection));
                }
                //Wait for 60 seconds and force shutdown
                reverseProxy.Stop().Wait(TimeSpan.FromSeconds(60));

                //Update database instance with stop time
                consoleDatabaseManager.UpdateServerInstanceAsStopped(serverDBInstance);
                await logger.LogInfoAsync($"Stopped Reverse proxy server..." + (reverseProxy.ActiveConnections.Any() ? $" Some tasks did not finish {reverseProxy.ActiveConnections.Count()}" : ""));

                //Cancel all logging operations
                logCancellationSource.Cancel();

                await logger.LogWarningAsync($"Press any key to exit...");
                Console.ReadKey();
            }
            catch (OperationCanceledException)
            {
                await logger.LogDebugAsync($"Operation cancelled [General]: User requested to stop reverse proxy");
            }
             catch (Exception ex)
            {
                await logger.LogErrorAsync("General failure" + ex.GetBaseException().Message, ex);
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
        private static async void ReverseProxy_BeforeNewConnection(object? sender, ConnectionEventArgs e)
        {
            if (consoleDatabaseManager != null)
            {
                IPAddressHistory? ip = await consoleDatabaseManager.GetIPAddressHistoryAsync(e.RemoteAddress);
                e.IsBlacklisted = ip?.IsBlacklisted == 1;

                AbuseIPDB_CheckedIP? checkedIP = await consoleDatabaseManager.GetAbuseIPDB_CheckedIPAsync(e.RemoteAddress);
                e.CountryName = checkedIP?.CountryName;
            }
        }
        private static async Task ReverseProxy_OnNewConnection(object sender, ConnectionEventArgs e)
        {
            try
            {
                if (consoleDatabaseManager != null)
                {
                    Connection newConnection = new()
                    {
                        ConnectionTime = e.ConnectionTime,
                        ProxyType = e.ProxyType.ToString(),
                        InstanceId = serverDBInstance?.InstanceId ?? "",
                        SessionId = e.SessionId,
                        LocalAddress = e.LocalAddress,
                        LocalPort = e.LocalPort,
                        TargetHost = e.TargetHost,
                        TargetPort = e.TargetPort,
                        RemoteAddress = e.RemoteAddress,
                        RemotePort = e.RemotePort
                    };
                    await consoleDatabaseManager.RegisterConnectionDetails(newConnection, settings?.AbuseIPDBApiKey);
                }
            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync("OnNewConnection event", ex, e.SessionId);
            }
        }
        private static async Task ReverseProxy_NewConnectionData(object sender, ConnectionDataEventArgs e)
        {
            try
            {
                if (consoleDatabaseManager != null)
                    await consoleDatabaseManager.InsertNewConnectionData(new ConnectionData(e.SessionId,
                                                                                            e.CommunicationDirection,
                                                                                            e.RawData.ToString()));
            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync("NewConnectionData event", ex, e.SessionId);
            }
        }
    }
}

