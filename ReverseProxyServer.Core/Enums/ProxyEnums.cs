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
    Request = 2,
    Warning = 3,
    Debug = 4
}