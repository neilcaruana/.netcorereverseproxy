namespace ReverseProxyServer
{
    public record PortRange
    {
        public int Start { get; init; }
        public int End { get; init; }
    }
    public record ReverseProxyEndpointConfig
    {
        public PortRange ListeningPortRange { get; init; }
        public string TargetHost { get; init; } = string.Empty;
        public int TargetPort { get; init; }
        public string CertificatePath { get; init; } = string.Empty;
        public string CertificatePassword { get; init; } = string.Empty;
        public ReverseProxyType ProxyType { get; init; } = ReverseProxyType.LogOnly;
            
        public ReverseProxyEndpointConfig(PortRange listeningPortRange,
                                            string targetHost,
                                            int targetPort,
                                            string certificatePath,
                                            string certificatePassword,
                                            ReverseProxyType proxyType)
        {
        

            CertificatePassword = certificatePassword;
            TargetHost = targetHost;
            ProxyType = proxyType;
            ListeningPortRange = listeningPortRange;
            TargetPort = targetPort;
            CertificatePath = certificatePath;

            Validate();
        }

        public ReverseProxyEndpointConfig()
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

            if (!string.IsNullOrEmpty(CertificatePath))
                if (!Path.Exists(CertificatePath))
                    throw new Exception($"Invalid certificate path specified {CertificatePath}");
        }
    }
}