namespace ReverseProxyServer
{
    class ReverseProxyEndpointConfig
    {
        public int ListeningPort { get; set; }
        public string TargetHost { get; set; }
        public int TargetPort { get; set; }
        public string CertificatePath { get; set; } = "";
        public string CertificatePassword { get; set; } = "";
        public ReverseProxyType ProxyType { get; set; } = ReverseProxyType.LogOnly;
        
        public ReverseProxyEndpointConfig(int listeningPort,
                                          string targetHost,
                                          int targetPort,
                                          string certificatePath,
                                          string certificatePassword,
                                          ReverseProxyType proxyType)
        {
            if (listeningPort < 1 || listeningPort > 65535)
                throw new Exception($"Invalid listening port specified {listeningPort}");
            else 
                ListeningPort = listeningPort;

            if (targetPort < 0 || targetPort > 65535)
                throw new Exception($"Invalid target port specified {targetPort}");
            else 
                TargetPort = targetPort;

            if (!string.IsNullOrEmpty(certificatePath))
                if (!Path.Exists(certificatePath))
                    throw new Exception($"Invalid certificate path specified {certificatePath}");
            else 
                CertificatePath = certificatePath;

            CertificatePassword = certificatePassword;
            TargetHost = targetHost;
            ProxyType = proxyType;
        }
    }
}