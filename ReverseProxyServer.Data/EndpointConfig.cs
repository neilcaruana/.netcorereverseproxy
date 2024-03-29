namespace ReverseProxyServer.Data
{
    public record PortRange
    {
        public int Start { get; init; }
        public int End { get; init; }
    }
    public record EndpointConfig
    {
        public PortRange ListeningPortRange { get; init; } = new PortRange();
        public string TargetHost { get; init; } = string.Empty;
        public int TargetPort { get; init; } = 0;
        public ReverseProxyType ProxyType { get; init; } = ReverseProxyType.LogOnly;
        public LoggerType LoggerType { get; init; } = LoggerType.ConsoleAndFile;
        public LoggerLevel LoggerLevel { get; init; } = LoggerLevel.Debug;
            
        public EndpointConfig(PortRange listeningPortRange,
                                            string targetHost,
                                            int targetPort,
                                            string certificatePath,
                                            string certificatePassword,
                                            ReverseProxyType proxyType,
                                            LoggerType loggerType,
                                            LoggerLevel loggerLevel)
        {
        

            TargetHost = targetHost;
            ProxyType = proxyType;
            ListeningPortRange = listeningPortRange;
            TargetPort = targetPort;
            LoggerType = loggerType;
            LoggerLevel = loggerLevel;

            Validate();
        }

        public EndpointConfig()
        {
            
        }

        public void Validate()
        {
            if (ListeningPortRange.Start < 1 || ListeningPortRange.Start > 65535 ||
                ListeningPortRange.End < 1 || ListeningPortRange.End > 65535 ||
                ListeningPortRange.Start > ListeningPortRange.End)
            {
                throw new Exception($"Invalid listening port range specified {ListeningPortRange.Start}-{ListeningPortRange.End}");
            }

            if (TargetPort < 0 || TargetPort > 65535)
                throw new Exception($"Invalid target port specified {TargetPort}");
        }
    }
}