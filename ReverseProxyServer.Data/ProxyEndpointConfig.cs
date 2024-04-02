﻿using ReverseProxyServer.Core.Enums.ProxyEnums;
using ReverseProxyServer.Core.Interfaces;
using System.Text.Json.Serialization;

namespace ReverseProxyServer.Data
{
    public class ProxyEndpointConfig : IProxyEndpointConfig
    {
        public ReverseProxyType ProxyType { get; init; } = ReverseProxyType.HoneyPot;
        public string ListeningAddress { get; init; } = string.Empty;
        public string ListeningPortRange { get; init; } = string.Empty;
        public string TargetHost { get; init; } = string.Empty;
        public int TargetPort { get; init; } = 0;
    }
}
