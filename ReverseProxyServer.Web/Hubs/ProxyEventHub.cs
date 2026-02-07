using Kavalan.Logging;
using Microsoft.AspNetCore.SignalR;

namespace ReverseProxyServer.Web.Hubs;

public class ProxyEventHub : Hub
{
    public async Task LogEvent(LogEvent logEvent)
    {
        await Clients.Others.SendAsync("ReceiveLogEvent", logEvent);
    }
}
