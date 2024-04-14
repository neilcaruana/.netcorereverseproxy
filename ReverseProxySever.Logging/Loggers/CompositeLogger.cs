using ReverseProxyServer.Core.Interfaces;
using ReverseProxyServer.Logging;

namespace ReverseProxySever.Logging.Loggers;
public class CompositeLogger(IEnumerable<ILogger> loggers) : ILogger
{
    private readonly IEnumerable<ILogger> loggers = loggers;

    public async Task LogDebugAsync(string message, string correlationId = "")
    {
        IEnumerable<Task> logs = loggers.Select(logger => logger.LogDebugAsync(message, correlationId));
        await Task.WhenAll(logs);
    }

    public async Task LogErrorAsync(string message, Exception? exception = null, string correlationId = "")
    {
        IEnumerable<Task> logs = loggers.Select(logger => logger.LogErrorAsync(message, exception, correlationId));
        await Task.WhenAll(logs);
    }

    public async Task LogInfoAsync(string message, string correlationId = "")
    {
        IEnumerable<Task> logs = loggers.Select(logger => logger.LogInfoAsync(message, correlationId));
        await Task.WhenAll(logs);
    }

    public async Task LogRequestAsync(string message, string correlationId = "")
    {
        IEnumerable<Task> logs = loggers.Select(logger => logger.LogRequestAsync(message, correlationId));
        await Task.WhenAll(logs);
    }

    public async Task LogWarningAsync(string message, string correlationId = "")
    {
        IEnumerable<Task> logs = loggers.Select(logger => logger.LogWarningAsync(message, correlationId));
        await Task.WhenAll(logs);
    }

}