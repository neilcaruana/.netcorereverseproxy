using System.Net.Sockets;

namespace ReverseProxyServer;

public static class ReverseProxyHelper
{
    public static string GetConnectionInfo(TcpClient client, ReverseProxyType reverseProxyType, ReverseProxyEndpointConfig setting, int? port)
    {
        string connectionInfo = $"{client?.Client?.RemoteEndPoint?.ToString()}";
        if (reverseProxyType == ReverseProxyType.LogAndProxy || reverseProxyType == ReverseProxyType.ProxyOnly)
            connectionInfo += $" -> {setting.TargetHost}:{setting.TargetPort}";
        else
            connectionInfo += $" -> Port {port}";
        return connectionInfo;
    }

    public static string CalculateLastSeen(DateTime lastConnectedTime)
    {
        var delta = DateTime.Now - lastConnectedTime;
        return $"{(delta.TotalDays >= 1 ? $"{(int)delta.TotalDays} day(s)" : delta.TotalHours >= 1 ? $"{(int)delta.TotalHours} hour(s)" : delta.TotalMinutes >= 1 ? $"{(int)delta.TotalMinutes} minute(s)" : $"{delta.Seconds} second(s)")} ago";
    }
}
