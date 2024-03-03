namespace ReverseProxyServer
{
    public record ReverseProxyEndpointConfig
    {
        public int ListeningPort { get; init; }
        public string TargetHost { get; init; } = string.Empty;
        public int TargetPort { get; init; }
        public string CertificatePath { get; init; } = string.Empty;
        public string CertificatePassword { get; init; } = string.Empty;
        public ReverseProxyType ProxyType { get; init; } = ReverseProxyType.LogOnly;
            
        public ReverseProxyEndpointConfig(int listeningPort,
                                            string targetHost,
                                            int targetPort,
                                            string certificatePath,
                                            string certificatePassword,
                                            ReverseProxyType proxyType)
        {
        

            CertificatePassword = certificatePassword;
            TargetHost = targetHost;
            ProxyType = proxyType;
            ListeningPort = listeningPort;
            TargetPort = targetPort;
            CertificatePath = certificatePath;

            Validate();
        }

        public ReverseProxyEndpointConfig()
        {
            
        }

        public void Validate()
        {
            if (ListeningPort < 1 || ListeningPort > 65535)
                throw new Exception($"Invalid listening port specified {ListeningPort}");

            if (TargetPort < 0 || TargetPort > 65535)
                throw new Exception($"Invalid target port specified {TargetPort}");

            if (!string.IsNullOrEmpty(CertificatePath))
                if (!Path.Exists(CertificatePath))
                    throw new Exception($"Invalid certificate path specified {CertificatePath}");
        }
    }
}