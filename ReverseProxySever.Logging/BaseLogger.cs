using System.Globalization;
using System.Text;

namespace ReverseProxyServer.Logging;

public enum LogLevel
{
    Info = 0,
    Error = 1,
    Request = 2,
    Warning = 3,
    Debug = 4
}
public class BaseLogger(LogLevel loggerLevel, CancellationToken cancellationToken = default)
{
    private string datePrefix => DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture);
    private readonly string delimeter = "\t";

    public LogLevel LoggerLevel { get; set; } = loggerLevel;
    public CancellationToken CancellationToken { get; } = cancellationToken;

    public string GetLogEntryHeader(LogLevel messageLoggerLevel, string correlationId = "")
    {
        StringBuilder newHeaderEntry = new();
        newHeaderEntry.Append(datePrefix).Append(this.delimeter)
                .Append(messageLoggerLevel.ToString()).Append(this.delimeter)
                .Append(string.IsNullOrWhiteSpace(correlationId) ? "" : "[" + correlationId + "]" + this.delimeter);

        return newHeaderEntry.ToString();
    }
    public string GetLogEntryMessage(string entry) => entry;
    public string CleanNonPrintableChars(string str)
    {
        StringBuilder sb = new();
        foreach (char c in str)
        {
            if (char.IsControl(c) && !char.IsWhiteSpace(c))
                continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

}
