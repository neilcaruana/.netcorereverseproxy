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
        public int PendingConnectionsCount => pendingConnections.Count;
        public int TotalConnectionsCount => statistics.Count;
        public IEnumerable<IStatistics> Statistics => [.. statistics];
        public IEnumerable<string> ActiveConnectionsInfo => [.. pendingConnections.Keys];

        private readonly int bufferSize = 4096;
        private ConcurrentDictionary<string,Task> pendingConnections = [];
        private readonly ConcurrentBag<IStatistics> statistics = [];
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
        public void Start()
        {
            foreach (IProxyEndpointConfig endpointSetting in settings.EndPoints)
            {
                //Create listener for every port in listening ports range
                for (int port = endpointSetting.ListeningStartingPort; port <= endpointSetting.ListeningEndingPort; port++)
                {
                    listeners.Add(CreateEndpointListener(port, endpointSetting, cancellationToken));
                }
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
                listener = new(IPAddress.Any, port);
                listener.Start();

                string endpointLog = $"Reverse Proxy ({endpointSetting.ProxyType}) listening on {IPAddress.Any} port {port}" + (endpointSetting.ProxyType == ReverseProxyType.Forward ? $" -> {endpointSetting.TargetHost}:{endpointSetting.TargetPort}" : "");

                await (externalLogger?.LogInfoAsync(endpointLog) ?? Task.CompletedTask);

                //Check for incoming connections until user needs to stop server
                while (!cancellationToken.IsCancellationRequested) 
                {
                    TcpClient incomingConnectionTcpClient = await listener.AcceptTcpClientAsync(cancellationToken);

                    string sessionId = Guid.NewGuid().ToString()[..8];

                    //TODO: To change to an event and handle outside also to check values
                    ProcessStatistics(incomingConnectionTcpClient);

                    //Fire and forget processing of actual traffic, this will not block the current thread from processing new connections
                    Task newConnection = ProxyTraffic(incomingConnectionTcpClient, endpointSetting, sessionId, cancellationToken);
                    
                    string connectionInfo = DateTime.Now + $"|[{endpointSetting.ProxyType}]\t[{sessionId}]\t" + ProxyHelper.GetConnectionInfo(incomingConnectionTcpClient, endpointSetting.ProxyType, endpointSetting, port);

                    //Add connections to a thread safe dictionary to monitor and gracefully wait when exiting
                    if (!pendingConnections.TryAdd(connectionInfo, newConnection))
                        throw new Exception($"Could not add to pending conneciton: [{connectionInfo}]");
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
        private async Task ProxyTraffic(TcpClient incomingTcpClient, IProxyEndpointConfig endpointSetting, string sessionId, CancellationToken cancellationToken)
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

                    //TODO: This needs refactoring
                    string connectionInfo = ProxyHelper.GetConnectionInfo(incomingTcpClient, endpointSetting.ProxyType, endpointSetting, (incomingTcpClient.Client.LocalEndPoint as IPEndPoint)?.Port);
                    await (externalLogger?.LogInfoAsync($"Connection received from {connectionInfo}", sessionId) ?? Task.CompletedTask);            

                    if (incomingTcpClient.Connected)
                    {
                        using NetworkStream incomingDataStream = incomingTcpClient.GetStream();
                        if (endpointSetting.ProxyType == ReverseProxyType.HoneyPot)
                        {
                            //Log data only and close connection
                            using MemoryStream tempMemory = await ConvertNetworkStreamIntoMemory(incomingDataStream, sessionId, cancellationToken);
                            //Drop incoming connection immediately after reading data
                            incomingTcpClient.Close();
                            if (tempMemory.Length > 0)
                            {
                                //If actual data was received proceed with logging
                                StringBuilder rawData = await ConvertMemoryStreamToString(tempMemory, cancellationToken);
                                await (externalLogger?.LogInfoAsync($"Connection rejected and raw data logged. Request size [{tempMemory.Length} bytes]", sessionId) ?? Task.CompletedTask);

                                //Endpoint is set as debug, log request data in seperate log file with same session id
                                if (settings.LogLevel == LogLevel.Debug)
                                    await (externalLogger?.LogDebugAsync($"Request size [{tempMemory.Length} bytes] from {connectionInfo}{Environment.NewLine}{rawData.ToString().Trim()}", sessionId) ?? Task.CompletedTask);

                                rawData.Clear();
                            }
                            else
                            {
                                await (externalLogger?.LogWarningAsync($"Connection dropped. No data received from {connectionInfo}", sessionId) ?? Task.CompletedTask);
                            }
                            return;
                        }

                        if (endpointSetting.ProxyType == ReverseProxyType.Forward)
                        {
                            TcpClient targetTcpClient = new(AddressFamily.InterNetwork)
                            {
                                ReceiveTimeout = settings.ReceiveTimeout,
                                SendTimeout = settings.SendTimeout
                            };

                            using (targetTcpClient)
                            {
                                targetTcpClient.Connect(endpointSetting.TargetHost, endpointSetting.TargetPort);
                                NetworkStream destinationDataStream = targetTcpClient.GetStream();
                                if (targetTcpClient.Connected)
                                {
                                    using (incomingDataStream)
                                    {
                                        using (destinationDataStream)
                                        {
                                            Task incomingToDestinationTask = RelayDataAsync(incomingDataStream, destinationDataStream, sessionId, CommunicationDirection.Incoming, cancellationToken);
                                            Task destinationToIncomingTask = RelayDataAsync(destinationDataStream, incomingDataStream, sessionId, CommunicationDirection.Outgoing, cancellationToken);

                                            //Return a task that awaits both incoming and destination tasks
                                            await Task.WhenAll(incomingToDestinationTask, destinationToIncomingTask);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                        await (externalLogger?.LogWarningAsync("Incoming connection disconnected", sessionId) ?? Task.CompletedTask);
                }
            }
            catch (IOException ex) when (ex.InnerException is SocketException socketEx && 
                                        (socketEx.SocketErrorCode == SocketError.ConnectionReset ||
                                         socketEx.SocketErrorCode == SocketError.ConnectionAborted))
            {
                await (externalLogger?.LogWarningAsync($"Connection was forcibly closed by the remote host. {socketEx.SocketErrorCode}", sessionId) ?? Task.CompletedTask);
            }
            catch (OperationCanceledException)
            {
                await (externalLogger?.LogDebugAsync("Operation cancelled: User requested to stop reverse proxy", sessionId) ?? Task.CompletedTask);
            }
            catch (Exception ex)
            {
                await (externalLogger?.LogErrorAsync($"Error handling normal traffic on port {endpointSetting.TargetPort}", ex, sessionId) ?? Task.CompletedTask);
            }
        }
        private async Task RelayDataAsync(NetworkStream inputStream, NetworkStream outputStream, string sessionId, CommunicationDirection direction, CancellationToken cancellationToken)
        {
            try
            {
                using (MemoryStream rawDataPacket = new())
                {
                    Memory<byte> buffer = new byte[bufferSize];
                    int bytesRead;

                    while ((bytesRead = await inputStream.ReadAsyncWithTimeout(buffer, settings.ReceiveTimeout, cancellationToken)) > 0)
                    {
                        //First write to network stream
                        await outputStream.WriteAsync(buffer[..bytesRead], cancellationToken);
                        //Also write the same buffer to temp memory for logging purposes
                        await rawDataPacket.WriteAsync(buffer[..bytesRead], cancellationToken);

                        //Don't wait for all data to be transmitted before logging, stream usually waits
                        if (!inputStream.DataAvailable)
                        {
                            StringBuilder rawPacket = await ConvertMemoryStreamToString(rawDataPacket, cancellationToken);
                            await (externalLogger?.LogInfoAsync($"{direction} request size [{rawDataPacket.Length} bytes] {(direction == CommunicationDirection.Incoming ? "Raw data logged" : "")}", sessionId) ?? Task.CompletedTask);

                            //Log only incoming requests raw data
                            if (direction == CommunicationDirection.Incoming)
                            {
                                string connectionInfo = $"{inputStream.Socket.RemoteEndPoint?.ToString()} -> {outputStream.Socket.RemoteEndPoint?.ToString()}";
                                await (externalLogger?.LogDebugAsync($"{direction} request size [{rawDataPacket.Length} bytes] from {connectionInfo} Raw data received;{Environment.NewLine}{rawPacket.ToString().Trim()}", sessionId) ?? Task.CompletedTask);
                            }
                                            
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
                await (externalLogger?.LogWarningAsync($"Connection was forcibly closed by the remote host during relay of [{direction}] data. {socketEx.SocketErrorCode}", sessionId) ?? Task.CompletedTask);
            }
            catch (OperationCanceledException)
            {
                await (externalLogger?.LogDebugAsync($"Operation canceled while relaying [{direction}] data. User requested to stop reverse proxy.", sessionId) ?? Task.CompletedTask);
            }
            catch (Exception ex)
            {
                await (externalLogger?.LogErrorAsync($"Relaying [{direction}] data error", ex, sessionId) ?? Task.CompletedTask);
            }
        }

        private async Task<StringBuilder> ConvertMemoryStreamToString(MemoryStream memoryStream, CancellationToken cancellationToken)
        {
            StringBuilder rawData = new(this.bufferSize);

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
        
        private async Task<MemoryStream> ConvertNetworkStreamIntoMemory(NetworkStream networkStream, string sessionId, CancellationToken cancellationToken)
        {
            MemoryStream memoryStream = new(this.bufferSize);
            Memory<byte> buffer = new(new byte[this.bufferSize]);
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

        private void ProcessStatistics(TcpClient newConnection)
        {
            if (newConnection.Client.LocalEndPoint is IPEndPoint local && newConnection.Client.RemoteEndPoint is IPEndPoint remote)
                statistics.Add(new ProxyStatistics(DateTime.Now, local.Address.ToString(), local.Port, remote.Address.ToString(), remote.Port));
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
            ConcurrentDictionary<string, Task> cleanedConnections = [];
            //Atomic operation to swap pending connections to empty cleaned connections, returning the current values
            ConcurrentDictionary<string, Task> tempPendingConnections = Interlocked.Exchange(ref pendingConnections, cleanedConnections);

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