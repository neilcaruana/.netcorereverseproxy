using Kavalan.Core;
using Kavalan.Logging;
using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Helpers;
using ReverseProxyServer.Core.Interfaces;
using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel.Design;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using static Kavalan.Core.TaskExtensions;

namespace ReverseProxyServer.Core;

public class ReverseProxy : IDisposable
{
    public DateTime StartedOn => startedListeningOn;
    public int TotalConnectionsReceived => totalConnectionsReceived;
    public IEnumerable<IReverseProxyConnection> ActiveConnections => [.. activeConnectionsInternal.Keys];

    public event EventHandler<ConnectionEventArgs>? BeforeNewConnection;
    public event AsyncEventHandler<ConnectionEventArgs>? NewConnection;
    public event AsyncEventHandler<ConnectionDataEventArgs>? NewConnectionData;
    public event AsyncEventHandler<NotificationEventArgs>? Notification;
    public event AsyncEventHandler<NotificationErrorEventArgs>? Error;

    private ConcurrentDictionary<IReverseProxyConnection, Task> activeConnectionsInternal = [];
    private DateTime startedListeningOn;
    private int totalConnectionsReceived;
    private readonly IProxyConfig settings;
    private readonly IList<Task> listeners = [];
    private readonly CancellationToken cancellationToken = new();
    private readonly ILogger? externalLogger;
    private readonly SemaphoreSlim pauseNotifications = new(1, 1);
    private bool disposed = false;

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

    public async Task StartAsync()
    {
        startedListeningOn = DateTime.Now;

        //Pause new connection log notifications until proxy has finished opening a listener on all ports 
        await pauseNotifications.WaitAsync(cancellationToken);

        foreach (IProxyEndpointConfig endpointSetting in settings.EndPoints)
        {
            //Create listener for every port in listening ports range
            for (int port = endpointSetting.ListeningStartingPort; port <= endpointSetting.ListeningEndingPort; port++)
                listeners.Add(CreateTcpListener(port, endpointSetting, cancellationToken));

            string endpointLog = $"Reverse Proxy ({endpointSetting.ProxyType}) listening on {endpointSetting.ListeningAddress}" + (endpointSetting.IsPortRange ? $" ({endpointSetting.TotalPorts}) ports {endpointSetting.ListeningPortRange}" : $":{endpointSetting.ListeningStartingPort}") +
                                 (endpointSetting.ProxyType == ReverseProxyType.Forward ? $" » {endpointSetting.TargetHost}:{endpointSetting.TargetPort}" : "");
            await OnNotification(endpointLog, string.Empty, LogLevel.Info);
        }
        await OnNotification($"Listening on {listeners.Count - settings.FaultyEndpoints:N0} total ports [{settings.FaultyEndpoints} faulty]", string.Empty, LogLevel.Info);

        //Continue new connection notifications
        pauseNotifications.Release();

        //Start thread that cleans dictionary of closed connections 
        _ = CleanCompletedConnectionsAsync(cancellationToken);
    }
    public async Task Stop()
    {
        try
        {
            //Wait for listeners to stop with a timeout of 30 seconds
            await Task.WhenAll([.. listeners]).WaitAsync(TimeSpan.FromSeconds(30));

            //Wait for pending connections to finish with a timeout of 30 seconds
            await Task.WhenAll([.. activeConnectionsInternal.Values]).WaitAsync(TimeSpan.FromSeconds(30));

            //Clean pending connection tasks manually before exit, this is because clean-up thread has now exited
            CleanCompletedConnectionsInternal();
        }
        catch (Exception ex)
        {
            await OnError(ex.Message, string.Empty, ex);
        }
    }

    private async Task CreateTcpListener(int port, IProxyEndpointConfig endpoint, CancellationToken cancellationToken)
    {
        //This is the main thread for every listening port, it will accept incoming connections
        //and then fire and forget the processing of the traffic to another method to free up the thread
        //to accept new connections immediately

        TcpListener? listener = null;
        try
        {
            listener = new(endpoint.ListeningIpAddress, port);

            //4096 bytes is the default buffer size for TcpClient and TcpListener in .NET, we can set it explicitly here to ensure consistency and avoid any potential issues with different defaults on different platforms
            listener.Server.ReceiveBufferSize = settings.BufferSize;
            listener.Server.SendBufferSize = settings.BufferSize;

            //Timeout settings based on config
            listener.Server.ReceiveTimeout = settings.ReceiveTimeout;
            listener.Server.SendTimeout = settings.SendTimeout;

            //Disable Nagle algorithm; designed to reduce network traffic by causing the socket to buffer small packets
            //and then combine and send them in one packet under certain circumstances
            listener.Server.NoDelay = true;

            // Immediate close for honeypot connections to avoid hanging sockets and free up resources faster, for forwarded connections
            // this will also help to ensure that connections are closed immediately when the client disconnects
            if (endpoint.ProxyType == ReverseProxyType.HoneyPot)
            {
                listener.Server.LingerState = new LingerOption(true, 0);
                listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, false);
            }
            else if (endpoint.ProxyType == ReverseProxyType.Forward)
                listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            listener.Start();

            //Check for incoming connections until user needs to stop server
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient incomingTcpConnection = await listener.AcceptTcpClientAsync(cancellationToken);

                //Set TCP conneciton settings based on config, this is needed to ensure that timeouts and buffer sizes are applied to the accepted connection
                incomingTcpConnection.ReceiveBufferSize = settings.BufferSize;      
                incomingTcpConnection.SendBufferSize = settings.BufferSize;
                incomingTcpConnection.ReceiveTimeout = settings.ReceiveTimeout;
                incomingTcpConnection.SendTimeout = settings.SendTimeout;
                incomingTcpConnection.NoDelay = true;

                // Immediate close for honeypot connections to avoid hanging sockets and free up resources faster, for forwarded connections
                // this will also help to ensure that connections are closed immediately when the client disconnects
                if (endpoint.ProxyType == ReverseProxyType.HoneyPot)
                {
                    listener.Server.LingerState = new LingerOption(true, 0);
                    listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, false);
                }
                else if (endpoint.ProxyType == ReverseProxyType.Forward)
                    listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                //Internal counter of all the connections received
                totalConnectionsReceived += 1; 
                string sessionId = Guid.NewGuid().ToString()[..8];

                //Create connection info object to pass to event handlers and traffic processing method
                ConnectionEventArgs connectionInfo = new(DateTime.Now, sessionId, endpoint.ProxyType, CommunicationDirection.Incoming, endpoint.ListeningAddress, port, endpoint.TargetHost, endpoint.TargetPort,
                                                         NetworkHelper.GetEndpointIPAddress(incomingTcpConnection.Client.RemoteEndPoint).ToString(), NetworkHelper.GetEndpointPort(incomingTcpConnection.Client.RemoteEndPoint), string.Empty);

                //Fire and forget processing of actual traffic, this will not block the current thread from processing new connections
                Task newConnection = ProxyTraffic(incomingTcpConnection, connectionInfo, endpoint, cancellationToken);

                //Post connection async processing for logging and statistics 
                await OnNewConnection(newConnection, connectionInfo);
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse || ex.SocketErrorCode == SocketError.AccessDenied)
        {
            settings.FaultyEndpoints++;
            // In Sentinel mode if the port is already in use, skip it
            if (settings.SentinelMode)
                await OnNotification($"Skipping port {port} since its already in use", string.Empty, LogLevel.Warning);
            else
                await OnError($"Port {port} is already in use", string.Empty, ex);
        }
        catch (OperationCanceledException)
        {
            settings.FaultyEndpoints++;
            await OnNotification($"Operation cancelled on port {port}: User requested to stop reverse proxy", string.Empty, LogLevel.Debug);
        }
        catch (Exception ex)
        {
            settings.FaultyEndpoints++;
            await OnError($"Creating endpoint listener on port {port}", string.Empty, ex);
        }
        finally
        {
            await OnNotification($"Stopped listening on port {port}", string.Empty, LogLevel.Debug);
        }
    }
    private async Task ProxyTraffic(TcpClient incomingTcpClient, ConnectionEventArgs connection, IProxyEndpointConfig endPointSettings, CancellationToken cancellationToken)
    {
        TcpClient? targetTcpClient = null;
        NetworkStream? incomingDataStream = null;
        NetworkStream? destinationDataStream = null;

        try
        {
            //Check if user requested to stop server
            if (cancellationToken.IsCancellationRequested)
                return;

            using (incomingTcpClient)
            {
                string connectionInfo = ProxyHelper.GetConnectionInfo(connection);

                //Checks before continue processing (ex. blacklisted?)
                BeforeNewConnection?.Invoke(this, connection);
                await OnNotification($"Connection received from {(!string.IsNullOrEmpty(connection.CountryName) ? $"[{connection.CountryName}] " : "")}{connectionInfo}{(connection.IsBlacklisted ? " is blacklisted" : "")}", connection.SessionId, connection.IsBlacklisted ? LogLevel.Info : LogLevel.Info);

                if (incomingTcpClient.Connected)
                {
                    incomingDataStream = incomingTcpClient.GetStream();

                    //Honey pot requests, only captured raw data and drop request
                    if (connection.ProxyType == ReverseProxyType.HoneyPot)
                    {
                        await CaptureRawDataAndDropRequest(incomingTcpClient, incomingDataStream, connection, connectionInfo);
                        return;
                    }

                    if (connection.ProxyType == ReverseProxyType.Forward)
                    {
                        if (connection.IsBlacklisted)
                        {
                            //Forwarded requests from blacklisted connections, are only captured (raw data) and then dropped
                            //No need to forward requests from blacklisted connections to protect internal services
                            await OnNotification($"Connection data will not be forwarded as [{connection.RemoteAddress}] is blacklisted", connection.SessionId, LogLevel.Info);
                            await CaptureRawDataAndDropRequest(incomingTcpClient, incomingDataStream, connection, connectionInfo);
                            return;
                        }

                        // Ensure proper disposal of target connection
                        try
                        {
                            targetTcpClient = new TcpClient(AddressFamily.InterNetwork)
                            {
                                ReceiveTimeout = settings.ReceiveTimeout,
                                SendTimeout = settings.SendTimeout
                            };

                            // Add connection timeout to prevent hanging connections
                            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                            await targetTcpClient.ConnectAsync(endPointSettings.TargetHost, endPointSettings.TargetPort, combinedCts.Token);

                            if (targetTcpClient.Connected)
                            {
                                destinationDataStream = targetTcpClient.GetStream();

                                //Incoming network connection
                                Task incomingToDestinationTask = RelayTraffic(incomingDataStream, destinationDataStream, connection, CommunicationDirection.Incoming, cancellationToken);
                                //Outgoing network connection
                                Task destinationToIncomingTask = RelayTraffic(destinationDataStream, incomingDataStream, connection, CommunicationDirection.Outgoing, cancellationToken);

                                await Task.WhenAll(incomingToDestinationTask, destinationToIncomingTask);
                            }
                            else
                            {
                                await OnNotification($"Failed to establish target connection to {endPointSettings.TargetHost}:{endPointSettings.TargetPort}", connection.SessionId, LogLevel.Warning);
                            }
                        }
                        catch (SocketException ex)
                        {
                            await OnNotification($"Socket error connecting to target {endPointSettings.TargetHost}:{endPointSettings.TargetPort}: {ex.SocketErrorCode}", connection.SessionId, LogLevel.Warning);
                        }
                        catch (OperationCanceledException)
                        {
                            await OnNotification($"Target connection timeout or cancellation to {endPointSettings.TargetHost}:{endPointSettings.TargetPort}", connection.SessionId, LogLevel.Warning);
                        }
                        finally
                        {
                            // Ensure target resources are always disposed
                            try
                            {
                                destinationDataStream?.Close();
                                destinationDataStream?.Dispose();
                            }
                            catch (Exception ex)
                            {
                                await OnNotification($"Error disposing destination stream: {ex.Message}", connection.SessionId, LogLevel.Debug);
                            }

                            try
                            {
                                targetTcpClient?.Close();
                                targetTcpClient?.Dispose();
                            }
                            catch (Exception ex)
                            {
                                await OnNotification($"Error disposing target TCP client: {ex.Message}", connection.SessionId, LogLevel.Debug);
                            }
                        }
                    }
                }
                else
                {
                    await OnNotification("Incoming connection disconnected", connection.SessionId, LogLevel.Warning);
                }
            }
        }
        catch (IOException ex) when (ex.InnerException is SocketException socketEx &&
                                    (socketEx.SocketErrorCode == SocketError.ConnectionReset ||
                                     socketEx.SocketErrorCode == SocketError.ConnectionAborted))
        {
            await OnNotification($"Connection was forcibly closed by the remote host. {socketEx.SocketErrorCode}", connection.SessionId, LogLevel.Warning);
        }
        catch (OperationCanceledException)
        {
            await OnNotification($"Operation cancelled: User requested to stop reverse proxy", connection.SessionId, LogLevel.Debug);
        }
        catch (Exception ex)
        {
            await OnError($"Error proxying traffic on port {connection.LocalPort}", connection.SessionId, ex);
        }
        finally
        {
            // Final cleanup to ensure no handles leak
            try
            {
                incomingDataStream?.Close();
                incomingDataStream?.Dispose();
            }
            catch (Exception ex)
            {
                await OnNotification($"Error disposing incoming stream: {ex.Message}", connection.SessionId, LogLevel.Debug);
            }

            // Double-check target client disposal
            if (targetTcpClient != null)
            {
                try
                {
                    if (destinationDataStream != null && destinationDataStream != targetTcpClient.GetStream())
                    {
                        destinationDataStream.Close();
                        destinationDataStream.Dispose();
                    }
                    targetTcpClient.Close();
                    targetTcpClient.Dispose();
                }
                catch (Exception ex)
                {
                    await OnNotification($"Final cleanup error for target client: {ex.Message}", connection.SessionId, LogLevel.Debug);
                }
            }
        }
    }
    private async Task RelayTraffic(NetworkStream inputNetworkStream, NetworkStream outputNetworkStream, IReverseProxyConnection connection, CommunicationDirection direction, CancellationToken cancellationToken)
    {
        Pipe? pipe = null;
        try
        {
            // Create pipe for efficient stream handling
            pipe = new Pipe();

            // Create timeout for relay operations
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5)); // 5 minute timeout
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // Start async tasks for reading and writing
            var readTask = FillPipeAsync(inputNetworkStream, pipe.Writer, combinedCts.Token, settings.ReceiveTimeout);
            var writeTask = DrainPipeAsync(pipe.Reader, outputNetworkStream, connection, direction, combinedCts.Token);

            // Wait for both operations to complete
            await Task.WhenAll(readTask, writeTask);
        }
        catch (IOException ex) when (ex.InnerException is SocketException socketEx &&
                                    (socketEx.SocketErrorCode == SocketError.ConnectionReset ||
                                    socketEx.SocketErrorCode == SocketError.ConnectionAborted))
        {
            await OnNotification($"Connection was forcibly closed by the remote host during relay of [{direction}] data. {socketEx.SocketErrorCode}", connection.SessionId, LogLevel.Warning);
        }
        catch (OperationCanceledException)
        {
            await OnNotification($"Operation canceled while relaying [{direction}] data. User requested to stop reverse proxy.", connection.SessionId, LogLevel.Debug);
        }
        catch (Exception ex)
        {
            await OnError($"Relaying [{direction}] data error", connection.SessionId, ex);
        }
        finally
        {
            // Ensure pipe resources are cleaned up
            try
            {
                pipe?.Writer.Complete();
                pipe?.Reader.Complete();
            }
            catch (Exception ex)
            {
                await OnNotification($"Error cleaning up pipe for [{direction}] relay: {ex.Message}", connection.SessionId, LogLevel.Debug);
            }
        }
    }
    private async Task FillPipeAsync(NetworkStream inputStream, PipeWriter writer, CancellationToken cancellationToken, int timeoutInSeconds)
    {
        const int minimumBufferSize = 512;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Request a minimum amount of memory from the PipeWriter
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);

                int bytesRead = await inputStream.ReadAsyncWithTimeout(memory, timeoutInSeconds, cancellationToken);
                if (bytesRead == 0)
                    break;

                // Tell the PipeWriter how much was actually written
                writer.Advance(bytesRead);

                // Make the data available to the PipeReader
                FlushResult result = await writer.FlushAsync(cancellationToken);

                if (result.IsCompleted)
                    break;
            }
        }
        finally
        {
            await writer.CompleteAsync();
        }
    }
    private async Task DrainPipeAsync(PipeReader reader, NetworkStream outputStream, IReverseProxyConnection connection, CommunicationDirection direction, CancellationToken cancellationToken)
    {
        MemoryStream? rawDataPacket = null;
        try
        {
            rawDataPacket = new MemoryStream();

            while (!cancellationToken.IsCancellationRequested)
            {
                ReadResult result = await reader.ReadAsync(cancellationToken);
                ReadOnlySequence<byte> buffer = result.Buffer;

                foreach (ReadOnlyMemory<byte> segment in buffer)
                {
                    await outputStream.WriteAsync(segment, cancellationToken);
                    await rawDataPacket.WriteAsync(segment, cancellationToken);
                }

                // Log the data if no more is immediately available
                if (!outputStream.DataAvailable && rawDataPacket.Length > 0)
                {
                    StringBuilder? rawPacket = null;
                    try
                    {
                        rawPacket = await ConvertMemoryStreamToString(rawDataPacket, cancellationToken);
                        string connectionInfo = ProxyHelper.GetConnectionInfo(connection, direction);
                        await OnNotification($"{direction} request size [{rawDataPacket.Length} bytes] from {connectionInfo}{Environment.NewLine}{rawPacket.ToString().Trim()}", connection.SessionId, LogLevel.Request);
                        await OnNewConnectionData(new ConnectionDataEventArgs(connection.SessionId, direction.ToString(), rawPacket));
                    }
                    finally
                    {
                        // Always clear StringBuilder
                        rawPacket?.Clear();
                    }

                    // Reset for next batch
                    rawDataPacket.SetLength(0);
                }

                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted)
                    break;
            }
        }
        finally
        {
            // Always dispose MemoryStream
            try
            {
                rawDataPacket?.Close();
                rawDataPacket?.Dispose();
            }
            catch (Exception ex)
            {
                await OnNotification($"Error disposing rawDataPacket in DrainPipeAsync: {ex.Message}", connection.SessionId, LogLevel.Debug);
            }

            await reader.CompleteAsync();
        }
    }

    private async Task<StringBuilder> ConvertMemoryStreamToString(MemoryStream memoryStream, CancellationToken cancellationToken)
    {
        StringBuilder rawData = new(settings.BufferSize);
        StreamReader? reader = null;

        try
        {
            if (memoryStream.Length == 0)
                return new StringBuilder(0);

            if (memoryStream.CanSeek)
                memoryStream.Seek(0, SeekOrigin.Begin);

            reader = new StreamReader(memoryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            rawData.Append(await reader.ReadToEndAsync(cancellationToken));

            return rawData;
        }
        finally
        {
            // Always dispose StreamReader
            try
            {
                reader?.Close();
                reader?.Dispose();
            }
            catch (Exception ex)
            {
                await OnNotification($"Error disposing StreamReader: {ex.Message}", "", LogLevel.Debug);
            }
        }
    }
    private async Task<MemoryStream> ConvertNetworkStreamIntoMemory(NetworkStream networkStream, CancellationToken cancellationToken)
    {
        if (networkStream == null)
            throw new ArgumentNullException(nameof(networkStream), "Network stream cannot be null.");

        if (!networkStream.CanRead)
            throw new InvalidOperationException("The provided network stream is not readable.");

        // Use smaller initial capacity for Sentinel mode honeypot connections
        int initialCapacity = settings.SentinelMode ? 512 : settings.BufferSize;
        MemoryStream memoryStream = new(initialCapacity);
        byte[]? bufferArray = null;

        try
        {
            // Use smaller buffer for honeypot data capture
            int bufferSize = settings.SentinelMode ? 1024 : settings.BufferSize;
            bufferArray = new byte[bufferSize];
            Memory<byte> buffer = new(bufferArray);

            if (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead;
                int totalBytesRead = 0;
                const int maxHoneypotCapture = 8192; // Limit honeypot data capture to 8KB

                // Read packet into memory for parsing before sending to other stream
                while ((bytesRead = await networkStream.ReadAsyncWithTimeout(buffer, settings.ReceiveTimeout, cancellationToken)) > 0)
                {
                    await memoryStream.WriteAsync(buffer[..bytesRead], cancellationToken);
                    totalBytesRead += bytesRead;

                    // Limit honeypot data capture in Sentinel mode
                    if (settings.SentinelMode && totalBytesRead >= maxHoneypotCapture)
                    {
                        break; // Stop reading after 8KB in Sentinel mode
                    }

                    // This is only used for logging so return as soon as no data is available
                    if (!networkStream.DataAvailable)
                        break;
                }
            }
            return memoryStream;
        }
        catch
        {
            // Dispose MemoryStream on exception
            try
            {
                memoryStream?.Close();
                memoryStream?.Dispose();
            }
            catch
            {
                // Ignore disposal errors during exception handling
            }
            throw;
        }
        finally
        {
            // Clear buffer array reference to help GC
            if (bufferArray != null)
            {
                Array.Clear(bufferArray, 0, bufferArray.Length);
                bufferArray = null; // Remove reference to help GC
            }
        }
    }
    private async Task CaptureRawDataAndDropRequest(TcpClient incomingTcpClient, NetworkStream incomingDataStream, IReverseProxyConnection connection, string connectionInfo)
    {
        MemoryStream? tempMemory = null;
        StringBuilder? rawPacket = null;

        try
        {
            //Log data only and close connection
            tempMemory = await ConvertNetworkStreamIntoMemory(incomingDataStream, cancellationToken);

            //Drop incoming connection immediately after reading data
            try
            {
                incomingTcpClient?.Close();
                incomingTcpClient?.Dispose();
            }
            catch (Exception ex)
            {
                await OnNotification($"Error closing incoming TCP client: {ex.Message}", connection.SessionId, LogLevel.Debug);
            }

            //If actual data was received proceed with logging, sometimes no data is sent by 3rd party
            if (tempMemory.Length > 0)
            {
                rawPacket = await ConvertMemoryStreamToString(tempMemory, cancellationToken);
                await OnNotification($"Connection dropped from {connectionInfo} logging raw data", connection.SessionId, LogLevel.Info);
                await OnNotification($"Received data [{tempMemory.Length} bytes]{Environment.NewLine}{rawPacket.ToString().Trim()}", connection.SessionId, LogLevel.Request);
                await OnNewConnectionData(new ConnectionDataEventArgs(connection.SessionId, "Incoming", rawPacket));
            }
            else
                await OnNotification($"Connection dropped. No data received from {connectionInfo}", connection.SessionId, LogLevel.Warning);
        }
        finally
        {
            // Always dispose resources
            try
            {
                rawPacket?.Clear();
            }
            catch (Exception ex)
            {
                await OnNotification($"Error clearing rawPacket: {ex.Message}", connection.SessionId, LogLevel.Debug);
            }

            try
            {
                tempMemory?.Close();
                tempMemory?.Dispose();
            }
            catch (Exception ex)
            {
                await OnNotification($"Error disposing tempMemory: {ex.Message}", connection.SessionId, LogLevel.Debug);
            }
        }
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
        ConcurrentDictionary<IReverseProxyConnection, Task> tempPendingConnections = Interlocked.Exchange(ref activeConnectionsInternal, cleanedConnections);

        foreach (var pendingConnection in tempPendingConnections)
        {
            if (pendingConnection.Value.IsCompleted == false)
            {
                if (!activeConnectionsInternal.TryAdd(pendingConnection.Key, pendingConnection.Value))
                    throw new Exception($"Could not clean connection: [{pendingConnection.Key}]");
            }
        }
    }
    private async Task OnNewConnection(Task newConnection, ConnectionEventArgs connectionInfo)
    {
        //Add connections to a thread safe dictionary to monitor and gracefully wait when exiting
        if (!activeConnectionsInternal.TryAdd(connectionInfo, newConnection))
            throw new Exception($"Could not add to pending connection: [{ProxyHelper.GetConnectionInfo(connectionInfo)}]");

        await pauseNotifications.WaitAsync(cancellationToken);

        //Get all handlers of NewConnection event and call them async
        await ProxyHelper.RaiseEventAsync(NewConnection, this, connectionInfo);

        pauseNotifications.Release();
    }
    private async Task OnNewConnectionData(ConnectionDataEventArgs connectionData)
    {
        //Get all handlers of NewConnection event and call them async
        await ProxyHelper.RaiseEventAsync(NewConnectionData, this, connectionData);
    }
    private async Task OnNotification(string notificationMessage, string sessionId, LogLevel logLevel)
    {
        switch (logLevel)
        {
            case LogLevel.Info:
                await (externalLogger?.LogInfoAsync(notificationMessage, sessionId) ?? Task.CompletedTask);
                break;
            case LogLevel.Request:
                await (externalLogger?.LogRequestAsync(notificationMessage, sessionId) ?? Task.CompletedTask);
                break;
            case LogLevel.Warning:
                await (externalLogger?.LogWarningAsync(notificationMessage, sessionId) ?? Task.CompletedTask);
                break;
            case LogLevel.Debug:
                await (externalLogger?.LogDebugAsync(notificationMessage, sessionId) ?? Task.CompletedTask);
                break;
            default:
                throw new Exception($"Not supported {logLevel}");
        }

        await ProxyHelper.RaiseEventAsync(Notification, this, new NotificationEventArgs(notificationMessage, sessionId, logLevel));
    }
    private async Task OnError(string errorMessage, string correlationId = "", Exception? exception = null)
    {
        await (externalLogger?.LogErrorAsync(errorMessage, exception, correlationId) ?? Task.CompletedTask);

        await ProxyHelper.RaiseEventAsync(Error, this, new NotificationErrorEventArgs(errorMessage, correlationId, exception));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                pauseNotifications?.Dispose();
            }
            disposed = true;
        }
    }
}
