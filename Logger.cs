using System.Globalization;

namespace ReverseProxyServer;

public static class Logger
{
    private static LoggerType loggerType = LoggerType.ConsoleAndFile;

    public static LoggerType GetLoggerType()
    {
        return loggerType;
    }

    public static void SetLoggerType(LoggerType value)
    {
        loggerType = value;
    }

    private static string logDatePrefix => DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture);
    private const string logDelimiter = "\t";
    private const string logInfoKeyword = "Info";
    private const string logErrorKeyword = "Error";
    private const string logWarningKeyword = "Warning";
    private const string logDebugKeyword = "Debug";
    private const string logFilename = "connections.log";

     public static void LogInfo(string message)
    {
        Console.ResetColor();
        logMessage(message, logInfoKeyword);
    }

    public static void LogError(string errorMessage, Exception? exception = null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        if (exception != null)
            errorMessage += " " + exception.GetType().Name + " : " + exception.GetBaseException().Message;
        logMessage(errorMessage, logErrorKeyword);
    }

    public static void LogWarning(string warningMesssage)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        logMessage(warningMesssage, logWarningKeyword);
    }
    public static void LogDebug(string debugMessage)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        logMessage(debugMessage, logDebugKeyword);
    }

    private static void logMessage(string entry, string keyword)
    {
        string newEntry = $"{logDatePrefix}{logDelimiter}{keyword}{logDelimiter}{entry}";
        switch (loggerType)
        {
            case LoggerType.Console:
                Console.WriteLine(newEntry);
                break;
            case LoggerType.File:
                logToFile(newEntry);
                break;
            case LoggerType.ConsoleAndFile:
                Console.WriteLine(newEntry);
                logToFile(newEntry);
                break;
            default:
                throw new NotSupportedException($"LoggerType '{loggerType}' is not supported.");
        }
        Console.ResetColor();
    }

    private static void logToFile(string entry)
    {
        File.AppendAllTextAsync(logFilename, entry + Environment.NewLine);
    }
}
