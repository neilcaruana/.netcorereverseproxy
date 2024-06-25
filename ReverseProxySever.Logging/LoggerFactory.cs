using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Helpers;
using ReverseProxyServer.Core.Interfaces;
using ReverseProxyServer.Data;
using ReverseProxySever.Logging.Loggers;
using System.Threading;

namespace ReverseProxyServer.Core.Logging
{
    public static class LoggerFactory
    {
        public static ILogger CreateDefaultCompositeLogger(CancellationToken cancellationToken = default)
        {
            return new CompositeLogger([new FileLogger(LogLevel.Debug, ProxyHelper.GetExecutableFileName(), cancellationToken), 
                                        new ConsoleLogger(LogLevel.Debug, cancellationToken)]);
        }
        public static ILogger CreateCompositeLogger(LogLevel logLevel, CancellationToken cancellationToken)
        {
            return new CompositeLogger([new FileLogger(logLevel, ProxyHelper.GetExecutableFileName(), cancellationToken),
                                        new ConsoleLogger(logLevel, cancellationToken)]);
        }


    }
}
