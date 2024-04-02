namespace ReverseProxyServer.Core.Enums.ProxyEnums;

public enum ReverseProxyType
{
    HoneyPot,
    Forward
}

public enum CommunicationDirection
{
    Incoming,
    Outgoing,
}
public enum LogLevel
{
    Info = 0,
    Error = 1,
    Warning = 2,
    Debug = 3
}