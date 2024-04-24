using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Interfaces;
using ReverseProxyServer.Data;
using ReverseProxyServer.Logging;

namespace ReverseProxySever.Logging.Loggers;
public class ConsoleLogger(LogLevel logLevel) : BaseLogger(logLevel), ILogger
{
    private static readonly SemaphoreSlim logSemaphore = new(1, 1);
    public async Task LogInfoAsync(string message, string correlationId = "") => await ConsoleLog(message, LogLevel.Info, null, correlationId);
    public async Task LogErrorAsync(string errorMessage, Exception? exception = null, string correlationId = "") =>
        await ConsoleLog($"{errorMessage} {(exception != null ? $"{exception.GetType().Name} : {exception.GetBaseException().Message}" : "")}", LogLevel.Error, ConsoleColor.Red, correlationId);
    public async Task LogWarningAsync(string warningMessage, string correlationId = "") => await ConsoleLog(warningMessage, LogLevel.Warning, ConsoleColor.Yellow, correlationId);
    public async Task LogDebugAsync(string debugMessage, string correlationId = "") => await ConsoleLog(debugMessage, LogLevel.Debug, ConsoleColor.Green, correlationId);
    public async Task LogRequestAsync(string requestData, string correlationId = "") => await ConsoleLog(base.CleanNonPrintableChars(requestData), LogLevel.Request, ConsoleColor.Blue, correlationId);
    private async Task ConsoleLog(string entry, LogLevel messageLoggerLevel, ConsoleColor? color = null, string correlationId = "")
    {
        if (messageLoggerLevel <= LoggerLevel)
        {
            await logSemaphore.WaitAsync();
            try
            {
                Console.ResetColor();
                Console.Write($"{base.GetLogEntryHeader(messageLoggerLevel, correlationId)} ");

                if (color.HasValue)
                    Console.ForegroundColor = color.Value;

                Console.Write($"{base.GetLogEntryMessage(entry)}" + Environment.NewLine);
            }
            finally
            {
                logSemaphore.Release();
            }
        }
    }
}