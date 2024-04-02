using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Data;
using System.Globalization;
using System.Text;

namespace ReverseProxyServer.Logging;

public class BaseLogger(LogLevel loggerLevel)
{
    private string datePrefix => DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture);
    private readonly string delimeter = "\t";

    public LogLevel LoggerLevel { get; set; } = loggerLevel;
    public string GetLogEntry(string entry, LogLevel messageLoggerLevel, string correlationId = "")
    {
        StringBuilder newEntry = new();
        newEntry.Append(this.datePrefix).Append(this.delimeter)
                .Append(messageLoggerLevel.ToString()).Append(this.delimeter)
                .Append(string.IsNullOrWhiteSpace(correlationId) ? "" : "[" + correlationId + "]" + this.delimeter)
                .Append(entry);

        return newEntry.ToString();
    }
}
