﻿using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;

namespace ReverseProxyServer
{
    public class Program
    {
        static readonly Logger logger = new Logger(LoggerType.ConsoleAndFile, LoggerLevel.Debug, "Main");
        static async Task Main(string[] args)
        {
            try
            {
                Console.TreatControlCAsInput = true;
                CancellationTokenSource cancellationTokenSource = new();
                
                LoadSplashScreen();
                await logger.LogInfoAsync("Starting Reverse proxy server...");
                await logger.LogInfoAsync($"{Environment.OSVersion} {RuntimeInformation.FrameworkDescription}");

                //Start the reverse proxy with the specified setting
                var settings = LoadConfigSettings();
                ReverseProxy reverseProxy = new(settings, cancellationTokenSource);
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
                        case ConsoleKey.L:
                            await logger.LogInfoAsync($"Change log level to ({string.Join(",", Enum.GetNames(typeof(LoggerType)))}");
                            LoggerType newLoggerType;
                            while (!Enum.TryParse(Console.ReadLine(), true, out newLoggerType))
                            {   
                                await logger.LogWarningAsync("Invalid entry");
                            }
                            logger.LoggerType = newLoggerType;
                            break;
                        case ConsoleKey.S:
                            await logger.LogInfoAsync($"Active connections: {reverseProxy.PendingConnectionsCount}");
                            break;
                        default:
                            await logger.LogInfoAsync($"Press Ctrl+C to shutdown server or h for help");
                            break;
                    }
                } 
                while (!cancellationTokenSource.IsCancellationRequested);

                await logger.LogWarningAsync($"Stopping Reverse proxy server... finishing pending tasks {reverseProxy.PendingConnectionsCount}");
                reverseProxy.Stop().Wait(TimeSpan.FromSeconds(10));
                await logger.LogInfoAsync($"Stopped Reverse proxy server..." + (reverseProxy.PendingConnectionsCount > 0 ? $" Some tasks did not finish {reverseProxy.PendingConnectionsCount}" : ""));
            }
            //TODO: handling of timeouts
            catch (Exception ex)
            {
                await logger.LogErrorAsync("General failure", ex);
            }
        }

        static async void LoadSplashScreen()
        {
           try
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
                    System.Threading.Thread.Sleep(100);
                }
           }
           catch (Exception ex)
           {
                await logger.LogErrorAsync("Splashscreen error", ex);
           }
        }

        static List<ReverseProxyEndpointConfig> LoadConfigSettings()
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                                                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            IConfigurationRoot configuration = builder.Build();

            List<ReverseProxyEndpointConfig> endpoints = [];
            configuration.GetSection("ReverseProxyEndpoints").Bind(endpoints);

            //Validate each loaded endpoint config
            endpoints.ForEach(endpoint => endpoint.Validate());

            return endpoints;
        }
    }
}
