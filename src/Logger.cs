using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ReverseProxyServer;

public class Logger : ILogger
{
    private LoggerType loggerType = LoggerType.ConsoleAndFile;
    private LoggerLevel loggerLevel = LoggerLevel.Debug;
    private string logFilePath {get {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "connections.log");
    }}

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

    private string LogDatePrefix => DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture);
    private const string LogDelimiter = "\t";
    private readonly object fileLock = new();

    public Logger(LoggerType loggerType, LoggerLevel loggerLevel)
    {
        this.loggerType = loggerType;
        this.loggerLevel = loggerLevel;
    }
    public Task LogInfoAsync(string message) => LogAsync(message, LoggerLevel.Info);
    public Task LogErrorAsync(string errorMessage, Exception? exception = null) =>
        LogAsync($"{errorMessage} {(exception != null ? $"{exception.GetType().Name} : {exception.GetBaseException().Message}" : "")}", LoggerLevel.Error, ConsoleColor.Red);
    public Task LogWarningAsync(string warningMessage) => LogAsync(warningMessage, LoggerLevel.Warning, ConsoleColor.Yellow);
    public Task LogDebugAsync(string debugMessage) => LogAsync(debugMessage, LoggerLevel.Debug, ConsoleColor.Green);
    private async Task LogAsync(string entry, LoggerLevel messageLoggerLevel, ConsoleColor? color = null)
    {
        if (messageLoggerLevel <= loggerLevel)
        {
            StringBuilder newEntry = new StringBuilder()
                .Append($"[{Thread.GetCurrentProcessorId()}][{Thread.CurrentThread.ManagedThreadId}]")
                .Append(LogDelimiter).Append(LogDatePrefix)
                .Append(LogDelimiter).Append(messageLoggerLevel.ToString())
                .Append(LogDelimiter).Append(entry);

            if (color.HasValue)
            {
                Console.ForegroundColor = color.Value;
            }

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
        lock (fileLock)
        {
            File.AppendAllTextAsync(logFilePath, entry + Environment.NewLine);
        }
    }
}
