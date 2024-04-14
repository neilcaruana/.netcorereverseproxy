using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Interfaces;
using ReverseProxyServer.Logging;

namespace ReverseProxySever.Logging.Loggers;
public class FileLogger : BaseLogger, ILogger
{
    private readonly string logFilePath = "";
    private static readonly SemaphoreSlim logSemaphore = new(1, 1);

    public FileLogger(LogLevel loggerLevel, string logFilename) : base(loggerLevel)
    {
        if (string.IsNullOrWhiteSpace(logFilename))
            logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "main.log");
        else
            logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logFilename + ".log");
    }
    public Task LogInfoAsync(string message, string correlationId = "") => LogToFileAsync(message, LogLevel.Info, correlationId);
    public Task LogErrorAsync(string errorMessage, Exception? exception = null, string correlationId = "") =>
        LogToFileAsync($"{errorMessage} {(exception != null ? $"{exception.GetBaseException().GetType().Name} : {exception.GetBaseException().Message}" : "")}", LogLevel.Error, correlationId);
    public Task LogRequestAsync(string message, string correlationId = "") => LogToFileAsync(message, LogLevel.Request, correlationId);
    public Task LogWarningAsync(string warningMessage, string correlationId = "") => LogToFileAsync(warningMessage, LogLevel.Warning, correlationId);
    public Task LogDebugAsync(string debugMessage, string correlationId = "") => LogToFileAsync(debugMessage, LogLevel.Debug, correlationId);
    private async Task LogToFileAsync(string entry, LogLevel messageLoggerLevel, string correlationId = "")
    {
        if (messageLoggerLevel <= LoggerLevel)
        {
            await logSemaphore.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(logFilePath, base.GetLogEntryHeader(messageLoggerLevel, correlationId) + " " + base.GetLogEntryMessage(entry) + Environment.NewLine);
            }
            finally
            {
                logSemaphore.Release();
            }
        }
    }
}