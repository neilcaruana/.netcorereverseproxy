using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using Kavalan.Core;
using Kavalan.Logging;
using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Helpers;
using ReverseProxyServer.Core.Interfaces;

namespace ReverseProxyServer.Core;
public class ReverseProxy
{
    public DateTime StartedOn => startedListeningOn;
    public int TotalConnectionsReceived => totalConnectionsReceived;
    public IEnumerable<IReverseProxyConnection> ActiveConnections => [.. activeConnectionsInternal.Keys];

    public delegate Task AsyncEventHandler<TEventArgs>(object sender, TEventArgs e) where TEventArgs : EventArgs;
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
        startedListeningOn = DateTime.Now;

        //Pause new connection log notifications until proxy has finsihed opening a listener on all ports 
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
        await OnNotification($"Listening on {listeners.Count.ToString("N0")} total ports", string.Empty, LogLevel.Info);

        //Continue new connection notifications
        pauseNotifications.Release();

        //Start thread that cleans dictionary of closed connections 
        _ = CleanCompletedConnectionsAsync(cancellationToken);
    }
    public async Task Stop()
    {
        try
        {
            //Wait for listeners to stop with a timeout of 10 seconds
            await Task.WhenAll([.. listeners]).WaitAsync(TimeSpan.FromSeconds(30));

            //Wait for pending connections to finish with a timeout of 10 seconds
            await Task.WhenAll([.. activeConnectionsInternal.Values]).WaitAsync(TimeSpan.FromSeconds(30));
            //Clean pending connection tasks manually before exit, this is because clean-up thread has now exited
            CleanCompletedConnectionsInternal();
        }
        catch (Exception ex)
        {
            await OnError(ex.Message, string.Empty, ex);
        }

    }

    private async Task CreateTcpListener(int port, IProxyEndpointConfig endpointSetting, CancellationToken cancellationToken)
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

                totalConnectionsReceived += 1; //Internal counter of all the connections received
                string sessionId = Guid.NewGuid().ToString()[..8];
                ConnectionEventArgs connectionInfo = new(DateTime.Now, sessionId, endpointSetting.ProxyType, CommunicationDirection.Incoming, endpointSetting.ListeningAddress, port, endpointSetting.TargetHost, endpointSetting.TargetPort,
                                                         NetworkHelper.GetEndpointIPAddress(incomingTcpConnection.Client.RemoteEndPoint).ToString(), NetworkHelper.GetEndpointPort(incomingTcpConnection.Client.RemoteEndPoint));

                //Fire and forget processing of actual traffic, this will not block the current thread from processing new connections
                Task newConnection = ProxyTraffic(incomingTcpConnection, connectionInfo, endpointSetting, cancellationToken);

                //Handles all new connection event listeners asynchronously
                await OnNewConnection(newConnection, connectionInfo);
            }
        }
        catch (OperationCanceledException)
        {
            await OnNotification($"Operation cancelled on port {port}: User requested to stop reverse proxy", string.Empty, LogLevel.Debug);
        }
        catch (Exception ex)
        {
            await OnError($"Creating endpoint listener on port {port}", string.Empty, ex);
        }
        finally
        {
            //Stop the listener since the user wants to stop the server
            listener?.Stop();
            await OnNotification($"Stopped listening on port {port}", string.Empty, LogLevel.Debug);
        }
    }
    private async Task ProxyTraffic(TcpClient incomingTcpClient, ConnectionEventArgs connection, IProxyEndpointConfig endPointSettings, CancellationToken cancellationToken)
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

                string connectionInfo = ProxyHelper.GetConnectionInfo(connection);

                //Checks before continue processing (ex. blacklisted?)
                BeforeNewConnection?.Invoke(this, connection);
                await OnNotification($"Connection received from {connectionInfo}{(connection.IsBlacklisted ? " is blacklisted" : "")}", connection.SessionId, connection.IsBlacklisted ? LogLevel.Warning : LogLevel.Info);
                if (incomingTcpClient.Connected)
                {
                    using NetworkStream incomingDataStream = incomingTcpClient.GetStream();
                    //Honey pot requests, only captured raw data and are drop request
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
                            await OnNotification($"Connection data will not be forwarded as [{connection.RemoteAddress}] is blacklisted", connection.SessionId, LogLevel.Warning);
                            await CaptureRawDataAndDropRequest(incomingTcpClient, incomingDataStream, connection, connectionInfo);
                            return;
                        }

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
                                        //Incoming network connection
                                        Task incomingToDestinationTask = RelayTraffic(incomingDataStream, destinationDataStream, connection, CommunicationDirection.Incoming, cancellationToken);
                                        //Outgoing network connection
                                        Task destinationToIncomingTask = RelayTraffic(destinationDataStream, incomingDataStream, connection, CommunicationDirection.Outgoing, cancellationToken);

                                        //Return a task that awaits both connection tasks
                                        await Task.WhenAll(incomingToDestinationTask, destinationToIncomingTask);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                    await OnNotification("Incoming connection disconnected", connection.SessionId, LogLevel.Warning);
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

                //Don't wait for all data to be transmitted before logging, stream usually waits (Code execution stops)
                if (!inputNetworkStream.DataAvailable)
                {
                    //Convert network stream data to string memory representation
                    StringBuilder rawPacket = await ConvertMemoryStreamToString(rawDataPacket, cancellationToken);
                    string connectionInfo = ProxyHelper.GetConnectionInfo(connection, direction);
                    await OnNotification($"{direction} request size [{rawDataPacket.Length} bytes] from {connectionInfo}{Environment.NewLine}{rawPacket.ToString().Trim()}", connection.SessionId, LogLevel.Request);
                    await OnNewConnectionData(new ConnectionDataEventArgs(connection.SessionId, direction.ToString(), rawPacket));
                }
            }
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
    private async Task CaptureRawDataAndDropRequest(TcpClient incomingTcpClient, NetworkStream incomingDataStream, IReverseProxyConnection connection, string connectionInfo)
    {
        //Log data only and close connection
        using MemoryStream tempMemory = await ConvertNetworkStreamIntoMemory(incomingDataStream, cancellationToken);
        //Drop incoming connection immediately after reading data
        incomingTcpClient.Close();
        if (tempMemory.Length > 0)
        {
            //If actual data was received proceed with logging
            StringBuilder rawPacket = await ConvertMemoryStreamToString(tempMemory, cancellationToken);
            await OnNotification($"Connection dropped from {connectionInfo} logging raw data", connection.SessionId, LogLevel.Info);
            await OnNotification($"Received data [{tempMemory.Length} bytes]{Environment.NewLine}{rawPacket.ToString().Trim()}", connection.SessionId, LogLevel.Request);
            await OnNewConnectionData(new ConnectionDataEventArgs(connection.SessionId, "Incoming", rawPacket));
            rawPacket.Clear();
        }
        else
            await OnNotification($"Connection dropped. No data received from {connectionInfo}", connection.SessionId, LogLevel.Warning);
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

}
