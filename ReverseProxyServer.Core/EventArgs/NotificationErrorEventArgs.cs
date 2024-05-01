namespace ReverseProxyServer.Core
{
    public class NotificationErrorEventArgs(string errorMessage, string sessionId, Exception? exception) : EventArgs
    {
        public string ErrorMessage { get; init; } = errorMessage;
        public string SessionId { get; init; } = sessionId;
        public Exception? Exception { get; init; } = exception;
    }
}
