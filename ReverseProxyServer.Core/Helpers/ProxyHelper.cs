using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Interfaces;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using static ReverseProxyServer.Core.ReverseProxy;

namespace ReverseProxyServer.Core.Helpers;

public static class ProxyHelper
{
    public static string GetConnectionInfo(IReverseProxyConnection connection, CommunicationDirection communicationDirection = CommunicationDirection.Incoming)
    {
        string connectionInfo = $"{connection.RemoteAddress}:{connection.RemotePort}";
        if (connection.ProxyType == ReverseProxyType.Forward)
        {
            if (communicationDirection == CommunicationDirection.Incoming)
                connectionInfo += $" » {connection.LocalAddress}:{connection.LocalPort}»{connection.TargetPort}";
            else if (communicationDirection == CommunicationDirection.Outgoing)
                connectionInfo = $"{connection.LocalAddress}:{connection.LocalPort}»{connection.TargetPort} » {connection.RemoteAddress}:{connection.RemotePort}";
        }
        else if (connection.ProxyType == ReverseProxyType.HoneyPot)
            connectionInfo += $" » {connection.LocalAddress}:{connection.LocalPort}";
        return connectionInfo;
    }
 
    public static async Task RaiseEventAsync<TEventArgs>(AsyncEventHandler<TEventArgs>? eventHandler, object sender, TEventArgs e) where TEventArgs : EventArgs
    {
        var handlers = eventHandler?.GetInvocationList().Cast<AsyncEventHandler<TEventArgs>>();
        if (handlers != null)
        {
            var tasks = handlers.Select(handler => handler(sender, e));
            await Task.WhenAll(tasks);
        }
    }
}
