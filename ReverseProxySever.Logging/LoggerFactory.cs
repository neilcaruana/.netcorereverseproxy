using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Helpers;
using ReverseProxyServer.Core.Interfaces;
using ReverseProxyServer.Data;
using ReverseProxySever.Logging.Loggers;

namespace ReverseProxyServer.Core.Logging
{
    public static class LoggerFactory
    {
        public static ILogger CreateDefaultCompositeLogger()
        {
            return new CompositeLogger([new FileLogger(LogLevel.Debug, ProxyHelper.GetExecutableFileName()), 
                                        new ConsoleLogger(LogLevel.Debug)]);
        }
        public static ILogger CreateCompositeLogger(LogLevel logLevel)
        {
            return new CompositeLogger([new FileLogger(logLevel, ProxyHelper.GetExecutableFileName()),
                                        new ConsoleLogger(logLevel)]);
        }


    }
}
