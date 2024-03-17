namespace ReverseProxyServer;

public interface ILogger
{
    Task LogInfoAsync(string message);
    Task LogErrorAsync(string message, Exception? exception = null);
    Task LogWarningAsync(string message);
    Task LogDebugAsync(string message);
}