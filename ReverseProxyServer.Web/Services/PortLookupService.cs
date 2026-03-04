using System.Collections.Frozen;
using System.Reflection;

namespace ReverseProxyServer.Web.Services;

public sealed class PortLookupService
{
    private readonly FrozenDictionary<int, PortInfo> _portMap;

    public PortLookupService()
    {
        _portMap = LoadPortData();
    }

    public PortInfo? GetPortInfo(long port) =>
        port is >= 0 and <= int.MaxValue && _portMap.TryGetValue((int)port, out var info) ? info : null;

    public Task<PortInfo?> GetPortInfoAsync(long port) =>
        Task.FromResult(GetPortInfo(port));

    private static FrozenDictionary<int, PortInfo> LoadPortData()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .First(n => n.EndsWith("service-names-port-numbers.csv", StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);

        var portMap = new Dictionary<int, PortInfo>();

        // Skip header
        reader.ReadLine();

        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            if (fields.Count < 4)
                continue;

            var serviceName = fields[0].Trim();
            var portStr = fields[1].Trim();
            var protocol = fields[2].Trim();
            var description = fields[3].Trim();

            if (string.IsNullOrEmpty(portStr) || !int.TryParse(portStr, out var port))
                continue;

            // Skip if no useful info
            if (string.IsNullOrEmpty(serviceName) && string.IsNullOrEmpty(description))
                continue;

            // Keep first meaningful entry per port (tcp preferred)
            if (portMap.ContainsKey(port))
                continue;

            portMap[port] = new PortInfo
            {
                Port = port,
                ServiceName = serviceName,
                Protocol = protocol,
                Description = string.IsNullOrEmpty(description) ? serviceName : description
            };
        }

        return portMap.ToFrozenDictionary();
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var inQuotes = false;
        var start = 0;

        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (line[i] == ',' && !inQuotes)
            {
                fields.Add(Unquote(line.AsSpan(start, i - start)));
                start = i + 1;
            }
        }

        fields.Add(Unquote(line.AsSpan(start)));
        return fields;
    }

    private static string Unquote(ReadOnlySpan<char> value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            return trimmed[1..^1].ToString().Replace("\"\"", "\"");
        return trimmed.ToString();
    }
}

public sealed class PortInfo
{
    public int Port { get; init; }
    public string ServiceName { get; init; } = string.Empty;
    public string Protocol { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
