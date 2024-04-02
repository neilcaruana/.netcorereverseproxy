using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Interfaces;
using System.Diagnostics;
using System.Net.Sockets;

namespace ReverseProxyServer.Core.Helpers;

public static class ProxyHelper
{
    public static string GetConnectionInfo(TcpClient client, ReverseProxyType reverseProxyType, IProxyEndpointConfig setting, int? port)
    {
        string connectionInfo = $"{client?.Client?.RemoteEndPoint?.ToString()}";
        if (reverseProxyType == ReverseProxyType.Forward)
            connectionInfo += $" -> {setting.TargetHost}:{setting.TargetPort}";
        else if (reverseProxyType == ReverseProxyType.HoneyPot)
            connectionInfo += $" -> Port {port}";
        return connectionInfo;
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
