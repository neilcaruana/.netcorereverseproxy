using ReverseProxyServer.Core.Interfaces;
using ReverseProxyServer.Data;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReverseProxySever.Logging.Converters
{
    public class ProxyEndpointConfigConverter : JsonConverter<IProxyEndpointConfig>
    {
        public override IProxyEndpointConfig Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Assume ProxyEndpointConfig is a concrete implementation of IProxyEndpointConfig
            return JsonSerializer.Deserialize<ProxyEndpointConfig>(ref reader, options) ?? throw new Exception($"Invalid settings file could not parse {reader.GetString()}");
        }
        public override void Write(Utf8JsonWriter writer, IProxyEndpointConfig value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}