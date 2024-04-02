using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Interfaces;
using ReverseProxyServer.Data;
using ReverseProxyServer.Logging;

namespace ReverseProxySever.Logging.Loggers;
public class ConsoleLogger(LogLevel logLevel) : BaseLogger(logLevel), ILogger
{
    public Task LogInfoAsync(string message, string correlationId = "") => Task.Run(() => ConsoleLog(message, LogLevel.Info, null, correlationId));
    public Task LogErrorAsync(string errorMessage, Exception? exception = null, string correlationId = "") =>
        Task.Run(() => ConsoleLog($"{errorMessage} {(exception != null ? $"{exception.GetType().Name} : {exception.GetBaseException().Message}" : "")}", LogLevel.Error, ConsoleColor.Red, correlationId));
    public Task LogWarningAsync(string warningMessage, string correlationId = "") => Task.Run(() => ConsoleLog(warningMessage, LogLevel.Warning, ConsoleColor.Yellow, correlationId));
    public Task LogDebugAsync(string debugMessage, string correlationId = "") => Task.Run(() => ConsoleLog(debugMessage, LogLevel.Debug, ConsoleColor.Green, correlationId));
    private void ConsoleLog(string entry, LogLevel messageLoggerLevel, ConsoleColor? color = null, string correlationId = "")
    {
        if (messageLoggerLevel <= LoggerLevel)
        {
            if (color.HasValue)
                Console.ForegroundColor = color.Value;

            Console.WriteLine($"{GetLogEntry(entry, messageLoggerLevel, correlationId)}");
            Console.ResetColor();
        }
    }
}