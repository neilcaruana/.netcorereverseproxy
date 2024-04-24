using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Interfaces;
using ReverseProxyServer.Helpers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

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
    public static bool IsPublicIpAddress(IPAddress? ip)
    {
        if (ip == null)
            return false;

        byte[] addressBytes = ip.GetAddressBytes();
        byte first = addressBytes[0];
        byte second = addressBytes[1];

        if (first == 10)
            return false; // 10.0.0.0/8
        if (first == 172 && second >= 16 && second <= 31)
            return false; // 172.16.0.0/12
        if (first == 192 && second == 168)
            return false; // 192.168.0.0/16

        return true;
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
