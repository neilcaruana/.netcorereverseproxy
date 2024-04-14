using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Interfaces;
using ReverseProxyServer.Helpers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace ReverseProxyServer.Core.Helpers;

public static class ProxyHelper
{
    public static string GetConnectionInfoString(IReverseProxyConnection connection)
    {
        string connectionInfo = $"{connection.RemoteAddress}:{connection.RemotePort}";
        if (connection.ProxyType == ReverseProxyType.Forward)
            connectionInfo += $" -> {connection.LocalAddress}:{connection.LocalPort}->{connection.TargetPort}";
        else if (connection.ProxyType == ReverseProxyType.HoneyPot)
            connectionInfo += $" -> {connection.LocalAddress}:{connection.LocalPort}";
        return connectionInfo;
    }
    public static IPAddress GetEndpointIPAddress(EndPoint? endPoint)
    {
        if (endPoint is IPEndPoint remoteEndPoint)
            return remoteEndPoint.Address;
        else
            throw new ArgumentException($"Invalid type {endPoint?.GetType()}", nameof(endPoint));
    }
    public static int GetEndpointPort(EndPoint? endPoint)
    {
        if (endPoint is IPEndPoint remoteEndPoint)
            return remoteEndPoint.Port;
        else
            throw new ArgumentException($"Invalid type {endPoint?.GetType()}", nameof(endPoint));
    }

    public static string CalculateLastSeen(DateTime lastConnectedTime)
    {
        var delta = DateTime.Now - lastConnectedTime;
        return $"{(delta.TotalDays >= 1 ? $"{(int)delta.TotalDays} day(s)" : delta.TotalHours >= 1 ? $"{(int)delta.TotalHours} hour(s)" : delta.TotalMinutes >= 1 ? $"{(int)delta.TotalMinutes} minute(s)" : $"{delta.Seconds} second(s)")} ago";
    }

    public static string GetExecutableFileName()
    {
        ProcessModule? ModuleHandle = Process.GetCurrentProcess().MainModule;
        if (ModuleHandle == null)
            return "";
        else 
            return ModuleHandle.FileName;
    }
}
