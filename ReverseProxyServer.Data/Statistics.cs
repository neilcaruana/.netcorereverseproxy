namespace ReverseProxyServer.Data
{
    public record Statistics(DateTime connectionTime, string localAddress, int localPort, string remoteAddress,int remotePort)
    {
        public DateTime ConnectionTime { get; init; } = connectionTime;
        public string LocalAddress { get; init; } = localAddress;
        public int LocalPort { get; init; } = localPort;
        public string RemoteAddress { get; init; } = remoteAddress;
        public int RemotePort { get; init; } = remotePort;
    }
}