using Kavalan.Logging;

namespace ReverseProxyServer.Core;
public class NotificationEventArgs(string message, string sessionId, LogLevel logLevel) : EventArgs
{
    public string Message { get; init; } = message;
    public string SessionId { get; init; } = sessionId;
    public LogLevel LogLevel { get; init; } = logLevel;
}
