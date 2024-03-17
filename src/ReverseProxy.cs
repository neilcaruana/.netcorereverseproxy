using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ReverseProxyServer
{
    class ReverseProxy
    {
        public int PendingConnectionsCount => pendingConnections.Count;
        private readonly int bufferSize = 4096;
        private ConcurrentBag<Task> pendingConnections = [];
        private readonly List<ReverseProxyEndpointConfig> settings;
        private readonly List<Task> listeners = [];
        private readonly CancellationTokenSource cancellationTokenSource = new();
        public ReverseProxy(List<ReverseProxyEndpointConfig> reverseProxySettings, CancellationTokenSource cancellationTokenSource)
        {
            if (reverseProxySettings == null)
                throw new Exception("Settings cannot be null");

            if (reverseProxySettings.Count == 0)                
                throw new Exception("No settings provided");

            this.settings = reverseProxySettings;
            this.cancellationTokenSource = cancellationTokenSource;
        }
        public void Start()
        {
            foreach (ReverseProxyEndpointConfig endpointSetting in settings)
            {
                //Create listener for every port in listening ports range
                for (int port = endpointSetting.ListeningPortRange.Start; port <= endpointSetting.ListeningPortRange.End; port++)
                {
                    listeners.Add(CreateEndpointListener(port, endpointSetting, cancellationTokenSource.Token));
                }
            }
        }
        public async Task Stop()
        {
            //Wait for listeners to stop with a timeout of 10 seconds
            await Task.WhenAll([.. listeners]).WaitAsync(TimeSpan.FromSeconds(10));

            //Wait for pending connections to finish with a timeout of 10 seconds
            await Task.WhenAll([.. pendingConnections]).WaitAsync(TimeSpan.FromSeconds(10));

            //Remove any completed connection tasks from memory
            cleanCompletedConnections();

            //TODO: handling of timeouts
        }
        private async Task CreateEndpointListener(int port, ReverseProxyEndpointConfig endpointSetting, CancellationToken cancellationToken)
        {
            TcpListener? listener = null;
            Logger endpointLogger = new(endpointSetting.LoggerType, endpointSetting.LoggerLevel, endpointSetting.ProxyType.ToString());
            try
            {
                listener = new(IPAddress.Any, port);
                listener.Start();
                
                string endpointLog = $"Reverse Proxy ({endpointSetting.ProxyType}) listening on {IPAddress.Any} port {port}";
                if (endpointSetting.ProxyType == ReverseProxyType.LogAndProxy || endpointSetting.ProxyType == ReverseProxyType.ProxyOnly)
                    endpointLog += $" -> {endpointSetting.TargetHost}:{endpointSetting.TargetPort}";

                await endpointLogger.LogInfoAsync(endpointLog);

                //Check for incoming connections until user needs to stop server
                while (!cancellationToken.IsCancellationRequested) 
                {
                    var tcpClientTask = await listener.AcceptTcpClientAsync(cancellationToken);
                                        
                    //Fire and forget processing of actual traffic, this will not block the current thread from processing new connections
                    Task newConnection = ProxyTraffic(tcpClientTask, endpointSetting, endpointLogger, cancellationToken);
                    
                    //Add connections to a thread safe list to monitor and gracefully wait and close when exiting
                    pendingConnections.Add(newConnection);

                    //Periodic cleaning of completed tasks
                    cleanCompletedConnections();

                    //Pause a little to avoid tight loop issues
                    await Task.Delay(500, cancellationToken);
                }
                
            }
            catch (OperationCanceledException)
            {
                await endpointLogger.LogDebugAsync("Operation cancelled: User requested to stop reverse proxy");
            }
            catch (Exception ex)
            {
                await endpointLogger.LogErrorAsync($"Creating endpoint listener on port {endpointSetting.TargetPort}.", ex);
            }
            finally
            {
                //Stop the listener since the user wants to stop the server
                listener?.Stop(); 
                await endpointLogger.LogInfoAsync($"Stopped listening on port {port}");
            }
        }
        private async Task ProxyTraffic(TcpClient incomingTcpClient, ReverseProxyEndpointConfig endpointSetting,
                                        Logger endpointLogger, CancellationToken cancellationToken)
        {
            try
            {   
                if (!cancellationToken.IsCancellationRequested)
                {
                    using (incomingTcpClient)
                    {
                        _ = endpointLogger.LogInfoAsync($"Connection received from {incomingTcpClient.Client.RemoteEndPoint?.ToString()} -> {incomingTcpClient.Client.LocalEndPoint?.ToString()}");            
                        if (incomingTcpClient.Connected)
                        {
                            using (NetworkStream incomingDataStream = incomingTcpClient.GetStream())
                            {
                                if (endpointSetting.ProxyType == ReverseProxyType.LogOnly)
                                {
                                    //Log data only and close connection
                                    using (MemoryStream tempMemory = await convertNetworkStreamIntoMemory(incomingDataStream, cancellationToken))
                                    {
                                        //Drop incoming connection immediately
                                        incomingTcpClient.Close();
                                        if (tempMemory.Length > 0)
                                        {
                                            //If actual data was received proceed with logging
                                            StringBuilder rawData = await convertMemoryStreamToString(tempMemory, cancellationToken);
                                            await endpointLogger.LogDebugAsync($"Connection dropped. Request size [{tempMemory.Length} bytes] - Raw data received;{Environment.NewLine}{rawData.ToString().Trim()}");
                                            rawData.Clear();
                                        }
                                        else
                                            await endpointLogger.LogDebugAsync($"Connection dropped. No data received");
                                        
                                        return;
                                    }
                                }

                                if (endpointSetting.ProxyType == ReverseProxyType.LogAndProxy)
                                {
                                    TcpClient destinationTcpClient = new();
                                    using (destinationTcpClient)
                                    {
                                        destinationTcpClient.Connect(endpointSetting.TargetHost, endpointSetting.TargetPort);
                                        NetworkStream destinationDataStream = destinationTcpClient.GetStream();
                                        if (destinationTcpClient.Connected)
                                        {
                                            using (incomingDataStream)
                                            {
                                                using (destinationDataStream)
                                                {
                                                    Task incomingToDestinationTask = RelayDataAsync(incomingDataStream, destinationDataStream, endpointLogger, "Incoming to Destination connection", cancellationToken);
                                                    Task destinationToIncomingTask = RelayDataAsync(destinationDataStream, incomingDataStream, endpointLogger, "Destination to Incoming connection", cancellationToken);

                                                    //Return a task that awaits both incoming and destination tasks
                                                    await Task.WhenAll(incomingToDestinationTask, destinationToIncomingTask);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                            await endpointLogger.LogWarningAsync("Incoming client disconnected");
                    }
                }
            }
            catch (IOException ex) when (ex.InnerException is SocketException socketEx && 
                                        (socketEx.SocketErrorCode == SocketError.ConnectionReset ||
                                         socketEx.SocketErrorCode == SocketError.ConnectionAborted))
            {
                await endpointLogger.LogWarningAsync($"Connection was forcibly closed by the remote host. {socketEx.SocketErrorCode}");
            }
            catch (OperationCanceledException)
            {
                await endpointLogger.LogDebugAsync("Operation cancelled: User requested to stop reverse proxy");
            }
            catch (Exception ex)
            {
                await endpointLogger.LogErrorAsync($"Error handling normal traffic on port {endpointSetting.TargetPort}", ex);
            }
        }
        private async Task RelayDataAsync(NetworkStream inputStream, NetworkStream outputStream, Logger endpointLogger, string directionDescription, CancellationToken cancellationToken)
        {
            try
            {
                using (MemoryStream fullPacket = new())
                {
                    Memory<byte> buffer = new byte[bufferSize];
                    int bytesRead;

                    while ((bytesRead = await inputStream.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        //First write to network stream
                        await outputStream.WriteAsync(buffer[..bytesRead], cancellationToken);
                        //Also write the same buffer to temp memory for logging purposes
                        await fullPacket.WriteAsync(buffer[..bytesRead], cancellationToken);

                        //Don't wait for all data to be transmitted before logging, stream usually waits
                        if (!inputStream.DataAvailable)
                        {
                            StringBuilder rawPacket = await convertMemoryStreamToString(fullPacket, cancellationToken);
                            _ = endpointLogger.LogDebugAsync($"Full packet {fullPacket.Length} bytes{Environment.NewLine}{rawPacket.ToString().Trim()}");        
                            fullPacket.SetLength(0);
                            fullPacket.Position = 0;
                        }
                    }
                }
            }
            catch (IOException ex) when (ex.InnerException is SocketException socketEx &&
                                        (socketEx.SocketErrorCode == SocketError.ConnectionReset ||
                                        socketEx.SocketErrorCode == SocketError.ConnectionAborted))
            {
                await endpointLogger.LogWarningAsync($"Connection was forcibly closed by the remote host during relay of data. {socketEx.SocketErrorCode}");
            }
            catch (OperationCanceledException)
            {
                await endpointLogger.LogDebugAsync($"Operation canceled while relaying data {directionDescription}. User requested to stop reverse proxy.");
            }
            catch (Exception ex)
            {
                await endpointLogger.LogErrorAsync($"Relaying data error ({directionDescription})", ex);
            }
        }

        private async Task<StringBuilder> convertMemoryStreamToString(MemoryStream memoryStream, CancellationToken cancellationToken)
        {
            StringBuilder rawData = new(this.bufferSize);

            if (memoryStream.Length == 0)
                return new StringBuilder(0);

            if (memoryStream.CanSeek)
                memoryStream.Seek(0, SeekOrigin.Begin);

            using (StreamReader reader = new StreamReader(memoryStream, Encoding.UTF8, true, leaveOpen: true))
            {
                rawData.Append(await reader.ReadToEndAsync(cancellationToken));
            }
            return rawData;
        }
        
        private async Task<MemoryStream> convertNetworkStreamIntoMemory(NetworkStream networkStream, CancellationToken cancellationToken)
        {
            MemoryStream memoryStream = new(this.bufferSize);
            Memory<byte> buffer = new(new byte[this.bufferSize]);
            if (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead;
                //Read packet into memory for parsing before sending to other stream
                while ((bytesRead = await networkStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await memoryStream.WriteAsync(buffer[..bytesRead], cancellationToken);
                    await memoryStream.FlushAsync(cancellationToken);

                    //This is only used for logging so return as soon as no data is available
                    if (!networkStream.DataAvailable)
                        break;
                }
            }
            return memoryStream;
        }

        private void cleanCompletedConnections()
        {
            ConcurrentBag<Task> cleanedConnections = [];
            //Atomic operation to swap pending connections to empty cleaned connections, returning the current values
            ConcurrentBag<Task> tempPendingConnections = Interlocked.Exchange(ref pendingConnections, cleanedConnections);

            foreach (var pendingConnection in tempPendingConnections)
            {
                if (pendingConnection.IsCompleted == false)
                {
                    pendingConnections.Add(pendingConnection);
                }
            }
        }
        
    }
}

