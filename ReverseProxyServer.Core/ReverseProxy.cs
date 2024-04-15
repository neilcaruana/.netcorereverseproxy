using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Helpers;
using ReverseProxyServer.Core.Interfaces;
using ReverseProxyServer.Helpers;

namespace ReverseProxyServer.Core
{
    public class ReverseProxy
    {
        public int TotalConnectionsCount => statistics.Count;
        public int PendingConnectionsCount => pendingConnections.Count;
        public IEnumerable<IReverseProxyConnection> Statistics => [.. statistics];
        public IEnumerable<IReverseProxyConnection> ActiveConnections => [.. pendingConnections.Keys];

        private ConcurrentDictionary<IReverseProxyConnection, Task> pendingConnections = [];
        private readonly ConcurrentBag<IReverseProxyConnection> statistics = [];
        private readonly IProxyConfig settings;
        private readonly IList<Task> listeners = [];
        private readonly CancellationToken cancellationToken = new();
        private readonly ILogger? externalLogger;

        public ReverseProxy(IProxyConfig settings, CancellationToken cancellationToken, ILogger? externalLogger = null)
        {
            if (settings == null)
                throw new ArgumentException("Settings cannot be null", nameof(settings));
            
            if (!settings.EndPoints.Any())                
                throw new ArgumentException("No settings provided", nameof(settings.EndPoints));

            ProxySettingsValidator.Validate(settings);

            this.settings = settings;
            this.cancellationToken = cancellationToken;
            this.externalLogger = externalLogger;
        }
        public async void Start()
        {
            foreach (IProxyEndpointConfig endpointSetting in settings.EndPoints)
            {
                //Create listener for every port in listening ports range
                for (int port = endpointSetting.ListeningStartingPort; port <= endpointSetting.ListeningEndingPort; port++)
                {
                    listeners.Add(CreateEndpointListener(port, endpointSetting, cancellationToken));
                }
                string endpointLog = $"Reverse Proxy ({endpointSetting.ProxyType}) listening on {endpointSetting.ListeningAddress}" + (endpointSetting.IsPortRange ? $" ports {endpointSetting.ListeningPortRange}" : $" port {endpointSetting.ListeningStartingPort}") +  
                                     (endpointSetting.ProxyType == ReverseProxyType.Forward ? $" -> {endpointSetting.TargetHost}:{endpointSetting.TargetPort}" : "");
                await (externalLogger?.LogInfoAsync(endpointLog) ?? Task.CompletedTask);
            }

            //Start thread that cleans dictionary of closed connections 
            _ = CleanCompletedConnectionsAsync(cancellationToken); 
        }
        public async Task Stop()
        {
            //Wait for listeners to stop with a timeout of 10 seconds
            await Task.WhenAll([.. listeners]).WaitAsync(TimeSpan.FromSeconds(10));

            //Wait for pending connections to finish with a timeout of 10 seconds
            await Task.WhenAll([.. pendingConnections.Values]).WaitAsync(TimeSpan.FromSeconds(10));

            //Clean pending connection tasks manually before exit, this is because clean-up thread has now exited
            CleanCompletedConnectionsInternal();

            //TODO: handling of timeouts
        }

        private async Task CreateEndpointListener(int port, IProxyEndpointConfig endpointSetting, CancellationToken cancellationToken)
        {
            TcpListener? listener = null;
            try
            {
                listener = new(endpointSetting.ListeningIpAddress, port);
                listener.Start();

                //Check for incoming connections until user needs to stop server
                while (!cancellationToken.IsCancellationRequested) 
                {
                    TcpClient incomingTcpConnection = await listener.AcceptTcpClientAsync(cancellationToken);

                    string sessionId = Guid.NewGuid().ToString()[..8];
                    ConnectionInfo connection = new(DateTime.Now, sessionId, endpointSetting.ProxyType, endpointSetting.ListeningAddress, port, endpointSetting.TargetHost, endpointSetting.TargetPort,
                                                    ProxyHelper.GetEndpointIPAddress(incomingTcpConnection.Client.RemoteEndPoint).ToString(), ProxyHelper.GetEndpointPort(incomingTcpConnection.Client.RemoteEndPoint));
                    statistics.Add(connection);//TODO: To change to an event and handle outside

                    //Fire and forget processing of actual traffic, this will not block the current thread from processing new connections
                    Task newConnection = ProxyTraffic(incomingTcpConnection, connection, endpointSetting, cancellationToken);
                    
                    //Add connections to a thread safe dictionary to monitor and gracefully wait when exiting
                    if (!pendingConnections.TryAdd(connection, newConnection))
                        throw new Exception($"Could not add to pending connection: [{ProxyHelper.GetConnectionInfoString(connection)}]");
                }
            }
            catch (OperationCanceledException)
            {
                await (externalLogger?.LogDebugAsync("Operation cancelled: User requested to stop reverse proxy") ?? Task.CompletedTask);
            }
            catch (Exception ex)
            {
                await (externalLogger?.LogErrorAsync($"Creating endpoint listener on port {port}.", ex) ?? Task.CompletedTask);
            }
            finally
            {
                //Stop the listener since the user wants to stop the server
                listener?.Stop(); 
                await (externalLogger?.LogInfoAsync($"Stopped listening on port {port}") ?? Task.CompletedTask);
            }
        }
        private async Task ProxyTraffic(TcpClient incomingTcpClient, IReverseProxyConnection connection, IProxyEndpointConfig endPointSettings, CancellationToken cancellationToken)
        {
            try
            {   
                //Check if user requested to stop server
                if (cancellationToken.IsCancellationRequested)
                    return;
            
                using (incomingTcpClient)
                {
                    incomingTcpClient.ReceiveTimeout = settings.ReceiveTimeout;
                    incomingTcpClient.SendTimeout = settings.SendTimeout;

                    string connectionInfo = ProxyHelper.GetConnectionInfoString(connection);
                    await (externalLogger?.LogInfoAsync($"Connection received from {connectionInfo}", connection.SessionId) ?? Task.CompletedTask);            

                    if (incomingTcpClient.Connected)
                    {
                        using NetworkStream incomingDataStream = incomingTcpClient.GetStream();
                        if (connection.ProxyType == ReverseProxyType.HoneyPot)
                        {
                            //Log data only and close connection
                            using MemoryStream tempMemory = await ConvertNetworkStreamIntoMemory(incomingDataStream, cancellationToken);
                            //Drop incoming connection immediately after reading data
                            incomingTcpClient.Close();
                            if (tempMemory.Length > 0)
                            {
                                //If actual data was received proceed with logging
                                StringBuilder rawData = await ConvertMemoryStreamToString(tempMemory, cancellationToken);
                                await (externalLogger?.LogInfoAsync($"Connection dropped from {connectionInfo} logging raw data", connection.SessionId) ?? Task.CompletedTask);
                                await (externalLogger?.LogRequestAsync($"Received data [{tempMemory.Length} bytes]{Environment.NewLine}{rawData.ToString().Trim()}", connection.SessionId) ?? Task.CompletedTask);

                                rawData.Clear();
                            }
                            else
                            {
                                await (externalLogger?.LogWarningAsync($"Connection dropped. No data received from {connectionInfo}", connection.SessionId) ?? Task.CompletedTask);
                            }
                            return;
                        }

                        if (connection.ProxyType == ReverseProxyType.Forward)
                        {
                            TcpClient targetTcpClient = new(AddressFamily.InterNetwork)
                            {
                                ReceiveTimeout = settings.ReceiveTimeout,
                                SendTimeout = settings.SendTimeout
                            };

                            using (targetTcpClient)
                            {
                                targetTcpClient.Connect(endPointSettings.TargetHost, endPointSettings.TargetPort);
                                NetworkStream destinationDataStream = targetTcpClient.GetStream();
                                if (targetTcpClient.Connected)
                                {
                                    using (incomingDataStream)
                                    {
                                        using (destinationDataStream)
                                        {
                                            Task incomingToDestinationTask = RelayTraffic(incomingDataStream, destinationDataStream, connection, CommunicationDirection.Incoming, cancellationToken);
                                            Task destinationToIncomingTask = RelayTraffic(destinationDataStream, incomingDataStream, connection, CommunicationDirection.Outgoing, cancellationToken);

                                            //Return a task that awaits both incoming and destination tasks
                                            await Task.WhenAll(incomingToDestinationTask, destinationToIncomingTask);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                        await (externalLogger?.LogWarningAsync("Incoming connection disconnected", connection.SessionId) ?? Task.CompletedTask);
                }
            }
            catch (IOException ex) when (ex.InnerException is SocketException socketEx && 
                                        (socketEx.SocketErrorCode == SocketError.ConnectionReset ||
                                         socketEx.SocketErrorCode == SocketError.ConnectionAborted))
            {
                await (externalLogger?.LogWarningAsync($"Connection was forcibly closed by the remote host. {socketEx.SocketErrorCode}", connection.SessionId) ?? Task.CompletedTask);
            }
            catch (OperationCanceledException)
            {
                await (externalLogger?.LogDebugAsync("Operation cancelled: User requested to stop reverse proxy", connection.SessionId) ?? Task.CompletedTask);
            }
            catch (Exception ex)
            {
                await (externalLogger?.LogErrorAsync($"Error handling normal traffic on port {connection.LocalPort}", ex, connection.SessionId) ?? Task.CompletedTask);
            }
        }
        private async Task RelayTraffic(NetworkStream inputNetworkStream, NetworkStream outputNetworkStream, IReverseProxyConnection connection, CommunicationDirection direction, CancellationToken cancellationToken)
        {
            try
            {
                using MemoryStream rawDataPacket = new();
                Memory<byte> buffer = new byte[settings.BufferSize];
                int bytesRead;

                while ((bytesRead = await inputNetworkStream.ReadAsyncWithTimeout(buffer, settings.ReceiveTimeout, cancellationToken)) > 0)
                {
                    /*Duplex streaming of buffer data
                     * First write to network stream (not to stop communication)
                     * and then to memory to capture request data */
                    await outputNetworkStream.WriteAsync(buffer[..bytesRead], cancellationToken);
                    await rawDataPacket.WriteAsync(buffer[..bytesRead], cancellationToken);

                    //Don't wait for all data to be transmitted before logging, stream usually waits
                    if (!inputNetworkStream.DataAvailable)
                    {
                        //Log incoming requests (Internet) raw data or both directions if debug logging enabled
                        if (direction == CommunicationDirection.Incoming || settings.LogLevel == LogLevel.Debug)
                        {
                            //Convert network stream data to string memory representation
                            StringBuilder rawPacket = await ConvertMemoryStreamToString(rawDataPacket, cancellationToken);
                            string connectionInfo = ProxyHelper.GetConnectionInfoString(connection);
                            await (externalLogger?.LogRequestAsync($"{direction} request size [{rawDataPacket.Length} bytes] from {connectionInfo} Raw data received;{Environment.NewLine}{rawPacket.ToString().Trim()}", connection.SessionId) ?? Task.CompletedTask);

                            //TODO: To check if needed? clears memory after reading data
                            rawDataPacket.SetLength(0);
                            rawDataPacket.Position = 0;
                        }
                    }
                }
            }
            catch (IOException ex) when (ex.InnerException is SocketException socketEx &&
                                        (socketEx.SocketErrorCode == SocketError.ConnectionReset ||
                                        socketEx.SocketErrorCode == SocketError.ConnectionAborted))
            {
                await (externalLogger?.LogWarningAsync($"Connection was forcibly closed by the remote host during relay of [{direction}] data. {socketEx.SocketErrorCode}", connection.SessionId) ?? Task.CompletedTask);
            }
            catch (OperationCanceledException)
            {
                await (externalLogger?.LogDebugAsync($"Operation canceled while relaying [{direction}] data. User requested to stop reverse proxy.", connection.SessionId) ?? Task.CompletedTask);
            }
            catch (Exception ex)
            {
                await (externalLogger?.LogErrorAsync($"Relaying [{direction}] data error", ex, connection.SessionId) ?? Task.CompletedTask);
            }
        }

        private async Task<StringBuilder> ConvertMemoryStreamToString(MemoryStream memoryStream, CancellationToken cancellationToken)
        {
            StringBuilder rawData = new(settings.BufferSize);

            if (memoryStream.Length == 0)
                return new StringBuilder(0);

            if (memoryStream.CanSeek)
                memoryStream.Seek(0, SeekOrigin.Begin);

            using (StreamReader reader = new(memoryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            {
                rawData.Append(await reader.ReadToEndAsync(cancellationToken));
            }
            return rawData;
        }
        private async Task<MemoryStream> ConvertNetworkStreamIntoMemory(NetworkStream networkStream, CancellationToken cancellationToken)
        {
            MemoryStream memoryStream = new(settings.BufferSize);
            Memory<byte> buffer = new(new byte[settings.BufferSize]);
            if (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead;
                //Read packet into memory for parsing before sending to other stream
                while ((bytesRead = await networkStream.ReadAsyncWithTimeout(buffer, settings.ReceiveTimeout, cancellationToken)) > 0)
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

        private async Task CleanCompletedConnectionsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    CleanCompletedConnectionsInternal();

                    //Pause a little to avoid tight loop issues
                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                await (this.externalLogger?.LogErrorAsync($"Cleaning pending connections", ex) ?? Task.CompletedTask);
            }
        }
        private void CleanCompletedConnectionsInternal()
        {
            ConcurrentDictionary<IReverseProxyConnection, Task> cleanedConnections = [];
            //Atomic operation to swap pending connections to empty cleaned connections, returning the current values
            ConcurrentDictionary<IReverseProxyConnection, Task> tempPendingConnections = Interlocked.Exchange(ref pendingConnections, cleanedConnections);

            foreach (var pendingConnection in tempPendingConnections)
            {
                if (pendingConnection.Value.IsCompleted == false)
                {
                    if (!pendingConnections.TryAdd(pendingConnection.Key, pendingConnection.Value))
                        throw new Exception($"Could not clean connection: [{pendingConnection.Key}]");
                }
            }   
        }
    }
}