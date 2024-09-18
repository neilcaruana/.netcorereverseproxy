using ReverseProxyServer.Logging;
using ReverseProxySever.Logging.Loggers;

namespace ReverseProxyServer.Core.Logging
{
    public static class LoggerFactory
    {
        public static ILogger CreateDefaultCompositeLogger(string logFileName, CancellationToken cancellationToken = default)
        {
            return new CompositeLogger([new FileLogger(LogLevel.Debug, logFileName, cancellationToken), 
                                        new ConsoleLogger(LogLevel.Debug, cancellationToken)]);
        }
        public static ILogger CreateCompositeLogger(LogLevel logLevel, string logFileName, CancellationToken cancellationToken)
        {
            return new CompositeLogger([new FileLogger(logLevel, logFileName, cancellationToken),
                                        new ConsoleLogger(logLevel, cancellationToken)]);
        }


    }
}
