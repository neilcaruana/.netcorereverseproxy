namespace ReverseProxyServer.Core.Interfaces
{
    public interface IStatistics
    {
        DateTime ConnectionTime { get; init; }
        string LocalAddress { get; init; }
        int LocalPort { get; init; }
        string RemoteAddress { get; init; }
        int RemotePort { get; init; }
    }
}