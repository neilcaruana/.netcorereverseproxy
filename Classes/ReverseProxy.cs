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
        private ConcurrentBag<Task> pendingConnections = new();
        private readonly List<ReverseProxyEndpointConfig> settings;
        private readonly List<Task> listeners = new();
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
            foreach (ReverseProxyEndpointConfig setting in settings)
            {
                listeners.Add(CreateEndpointListener(setting, cancellationTokenSource.Token));
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
        private async Task CreateEndpointListener(ReverseProxyEndpointConfig endpointSetting, CancellationToken cancellationToken)
        {
            TcpListener? listener = null;
            try
            {
                listener = new(IPAddress.Any, endpointSetting.ListeningPort);
                listener.Start();
                
                Logger.LogInfo($"Reverse Proxy ({endpointSetting.ProxyType}) listening on {IPAddress.Any.ToString()} port {endpointSetting.ListeningPort}");
                //Check for incoming connections until user needs to stop server
                while (!cancellationToken.IsCancellationRequested) 
                {
                    var tcpClientTask = await listener.AcceptTcpClientAsync(cancellationToken);
                                        
                    Task newConnection;
                    //Handle normal traffic
                    if (string.IsNullOrEmpty(endpointSetting.CertificatePath))
                    {
                        //Fire and forget processing of actual traffic, this will not block the current thread from processing new connections
                        newConnection = ProxyNormalTraffic(tcpClientTask, endpointSetting, cancellationToken);
                    }
                    //Handle SSL traffic
                    else 
                    {
                        newConnection = proxySSLTraffic(tcpClientTask, endpointSetting, cancellationToken);
                    }
                    
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
                Logger.LogDebug("Operation cancelled: User requested to stop reverse proxy");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Creating endpoint listener on port {endpointSetting.TargetPort}.", ex);
            }
            finally
            {
                //Stop the listener since the user wants to stop the server
                listener?.Stop(); 
                Logger.LogInfo($"Stopped listening on port {endpointSetting.ListeningPort}.. Thread Id: {Thread.CurrentThread.ManagedThreadId}");
            }
        }
        private async Task ProxyNormalTraffic(TcpClient incomingTcpClient, ReverseProxyEndpointConfig endpointSetting, CancellationToken cancellationToken)
        {
            try
            {   
                if (!cancellationToken.IsCancellationRequested)
                {
                    using (incomingTcpClient)
                    {
                        Logger.LogInfo($"Connection received from {incomingTcpClient.Client.RemoteEndPoint?.ToString()} on {incomingTcpClient.Client.LocalEndPoint?.ToString()}");            
                        if (incomingTcpClient.Connected)
                        {
                            using (NetworkStream incomingDataStream = incomingTcpClient.GetStream())
                            {
                                if (endpointSetting.ProxyType == ReverseProxyType.LogOnly)
                                {
                                    //Log data only and close connection
                                    MemoryStream tempMemory = await convertNetworkStreamIntoMemory(incomingDataStream, cancellationToken);

                                    Logger.LogInfo($"Logging request size {tempMemory.Length} bytes and closing connection");
                                    Logger.LogDebug($"Raw data received: {await convertMemoryStreamToString(tempMemory, cancellationToken)}");

                                    await tempMemory.DisposeAsync();
                                    incomingTcpClient.Close();
                                    return;
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
                                                    Task incomingToDestinationTask = RelayDataAsync(incomingDataStream, destinationDataStream, cancellationToken, "Incoming to Destination connection");
                                                    Task destinationToIncomingTask = RelayDataAsync(destinationDataStream, incomingDataStream, cancellationToken, "Destination to Incoming connection");

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
                            Logger.LogWarning("Incoming client disconnected");
                    }
                }
            }
            catch (IOException ex) when (ex.InnerException is SocketException socketEx && 
                                        (socketEx.SocketErrorCode == SocketError.ConnectionReset ||
                                         socketEx.SocketErrorCode == SocketError.ConnectionAborted))
            {
                Logger.LogWarning($"Connection was forcibly closed by the remote host. {socketEx.SocketErrorCode}");
            }
            catch (OperationCanceledException)
            {
                Logger.LogDebug("Operation cancelled: User requested to stop reverse proxy");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error handling normal traffic on port {endpointSetting.TargetPort}", ex);
            }
        }
        private async Task RelayDataAsync(NetworkStream inputStream, NetworkStream outputStream, CancellationToken cancellationToken, string directionDescription)
        {
            try
            {
                using (MemoryStream fullPacket = new())
                {
                    Memory<byte> buffer = new byte[bufferSize];
                    int bytesRead;

                    while ((bytesRead = await inputStream.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        await outputStream.WriteAsync(buffer[..bytesRead], cancellationToken);
                        await fullPacket.WriteAsync(buffer[..bytesRead], cancellationToken);

                        //Don't wait for all data to ne transmitted before logging
                        if (!inputStream.DataAvailable)
                        {
                            Logger.LogDebug($"Full packet {fullPacket.Length}: {await convertMemoryStreamToString(fullPacket, cancellationToken)}");        
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
                Logger.LogWarning($"Connection was forcibly closed by the remote host during relay of data. {socketEx.SocketErrorCode}");
            }
            catch (OperationCanceledException)
            {
                Logger.LogDebug($"Operation canceled while relaying data {directionDescription}. User requested to stop reverse proxy.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Relaying data error ({directionDescription})", ex);
            }
        }

        private async Task<string> convertMemoryStreamToString(MemoryStream memoryStream, CancellationToken cancellationToken)
        {
            if (memoryStream.Length == 0)
                return string.Empty;

            if (memoryStream.CanSeek)
                memoryStream.Seek(0, SeekOrigin.Begin);

            using (StreamReader reader = new StreamReader(memoryStream, Encoding.UTF8, true, leaveOpen: true))
            {
                return await reader.ReadToEndAsync(cancellationToken);
            }
        }
        
        private async Task<MemoryStream> convertNetworkStreamIntoMemory(NetworkStream networkStream, CancellationToken cancellationToken)
        {
            MemoryStream memoryStream = new MemoryStream(this.bufferSize);
            Memory<byte> buffer = new Memory<byte>(new byte[this.bufferSize]);
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
            ConcurrentBag<Task> cleanedConnections = new ConcurrentBag<Task>();
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
        private async Task proxySSLTraffic(TcpClient incomingTcpClient, ReverseProxyEndpointConfig endpointSetting, CancellationToken cancellationToken)
        {
            await Task.Delay(100);
            throw new Exception("SSL Tarffic not implemented");

            // X509Certificate2 certificate = new X509Certificate2(endpointSetting.CertificatePath, endpointSetting.CertificatePassword);
            // NetworkStream clientStream = client.GetStream();
            // SslStream sslStream = new SslStream(clientStream, false);
            
            // await sslStream.AuthenticateAsServerAsync(certificate, clientCertificateRequired: false, checkCertificateRevocation: true);

            // // Assuming target server is HTTP for simplicity
            // var targetClient = new TcpClient(endpointSetting.TargetHost, endpointSetting.TargetPort);
            // using (targetClient)
            // {
            //     var targetStream = targetClient.GetStream();

            //     // Forward the decrypted request to the target server over HTTP
            //     var requestTask = sslStream.CopyToAsync(targetStream);
            //     // Forward the response back to the client over the secured connection
            //     var responseTask = targetStream.CopyToAsync(sslStream);

            //     await Task.WhenAll(requestTask, responseTask);
            // }
        }
    }
}

