namespace ReverseProxyServer;

public enum ReverseProxyType
{
    LogOnly,
    LogAndProxy,
    ProxyOnly
}

public enum LoggerType
{
    Console,
    File,
    ConsoleAndFile
}