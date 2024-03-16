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

public enum LoggerLevel
{
    Info = 0,
    Error = 1,
    Warning = 2,
    Debug = 3
}