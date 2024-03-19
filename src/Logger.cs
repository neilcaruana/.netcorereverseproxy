using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ReverseProxyServer;

public class Logger : ILogger
{
    private LoggerType loggerType = LoggerType.ConsoleAndFile;
    private LoggerLevel loggerLevel = LoggerLevel.Debug;
    private string logDatePrefix => DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture);
    private const string logDelimiter = "  ";
    private readonly object logLock = new();
    private readonly string logPrefix = "";
    private readonly string logFilePath = "";

    public LoggerType LoggerType
    {
        get => loggerType;
        set => loggerType = value;
    }
    public LoggerLevel LoggerLevel
    {
        get => loggerLevel;
        set => loggerLevel = value;
    }

    public Logger(LoggerType loggerType, LoggerLevel loggerLevel, string logPrefix = "", string logFilename = "")
    {
        this.loggerType = loggerType;
        this.loggerLevel = loggerLevel;
        this.logPrefix = logPrefix;

        if (string.IsNullOrWhiteSpace(logFilename))
            this.logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "main.log");
        else
            this.logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logFilename + ".log");
    }
    public Task LogInfoAsync(string message, string correlationId = "") => LogAsync(message, LoggerLevel.Info, null, correlationId);
    public Task LogErrorAsync(string errorMessage, Exception? exception = null, string correlationId = "") =>
        LogAsync($"{errorMessage} {(exception != null ? $"{exception.GetType().Name} : {exception.GetBaseException().Message}" : "")}", LoggerLevel.Error, ConsoleColor.Red, correlationId);
    public Task LogWarningAsync(string warningMessage, string correlationId = "") => LogAsync(warningMessage, LoggerLevel.Warning, ConsoleColor.Yellow, correlationId);
    public Task LogDebugAsync(string debugMessage, string correlationId = "") => LogAsync(debugMessage, LoggerLevel.Debug, ConsoleColor.Green, correlationId);
    private async Task LogAsync(string entry, LoggerLevel messageLoggerLevel, ConsoleColor? color = null, string correlationId = "")
    {
        if (messageLoggerLevel <= loggerLevel)
        {
            StringBuilder newEntry = new StringBuilder()
                .Append(string.IsNullOrWhiteSpace(logPrefix) ? "" : "[" + logPrefix + "]" + logDelimiter)
                .Append(string.IsNullOrWhiteSpace(correlationId) ? "" : "[" + correlationId + "]" + logDelimiter)
                .Append(logDelimiter).Append(logDatePrefix)
                .Append(logDelimiter).Append(messageLoggerLevel.ToString())
                .Append(logDelimiter).Append(entry);

            if (color.HasValue)
                Console.ForegroundColor = color.Value;

            switch (loggerType)
            {
                case LoggerType.Console:
                    Console.WriteLine(newEntry);
                    break;
                case LoggerType.File:
                    await LogToFileAsync(newEntry.ToString());
                    break;
                case LoggerType.ConsoleAndFile:
                    Console.WriteLine(newEntry);
                    await LogToFileAsync(newEntry.ToString());
                    break;
                default:
                    throw new NotSupportedException($"LoggerType '{loggerType}' is not supported.");
            }
            Console.ResetColor();
        }
    }

    private async Task LogToFileAsync(string entry)
    {
        lock (logLock)
        {
            _ = File.AppendAllTextAsync(logFilePath, entry + Environment.NewLine);
        }
    }
}
