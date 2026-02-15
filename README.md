# .NET Core Reverse Proxy
A high-performance TCP reverse proxy and honeypot built in C# on .NET 10. Designed for both traffic forwarding and honeypot monitoring, it captures and logs all connection activity in real-time to a local SQLite database.

## Features
* **Forward Proxy** — Relays TCP traffic to configured target hosts with full request/response capture
* **Honeypot Mode** — Listens on configurable port ranges, captures attacker payloads, and drops connections
* **Sentinel Mode** — Automatically listens on all 65,000 ports for maximum security coverage
* **AbuseIPDB Integration** — Cross-references connecting IPs against the AbuseIPDB threat database and auto-blacklists threats
* **Real-time Dashboard** — Blazor Server UI with SignalR-powered live event streaming and world heatmap
* **SQLite Storage** — All connections, IP history, and abuse data persisted locally
* **Composite Logging** — Console, file, and SignalR logger outputs running simultaneously
* **Async I/O** — Leverages async/await and System.IO.Pipelines for efficient network operations
* **Duplex Streaming** — Real-time bidirectional data capture with interception and logging
* **Cancellation Support** — Graceful shutdown with proper cleanup

## Configuration
Listening endpoints are configured through `appsettings.json`. Each entry in the `EndPoints` section defines a proxy endpoint with its own type, listening port ranges, target host, and target port.

The `DashboardUrl` setting points to the Blazor Server dashboard's SignalR hub for real-time event streaming.

```json
{
  "SentinelMode": true,
  "SendTimeout": 60,
  "ReceiveTimeout": 60,
  "BufferSize": 4096,
  "LogLevel": "Info",
  "DatabasePath": "stats.db",
  "DashboardUrl": "http://ADDRESS:PORT/hubs/proxy-events",
  "AbuseIPDBApiKey": "YOUR_API_KEY",
  "EndPoints": [
    {
      "ProxyType": "Forward",
      "ListeningAddress": "192.168.1.100",
      "ListeningPortRange": "80",
      "TargetHost": "localhost",
      "TargetPort": 81
    },
    {
      "ProxyType": "Forward",
      "ListeningAddress": "192.168.1.100",
      "ListeningPortRange": "443",
      "TargetHost": "localhost",
      "TargetPort": 444
    },
    {
      "ProxyType": "Honeypot",
      "ListeningAddress": "192.168.1.100",
      "ListeningPortRange": "1-79",
      "TargetHost": "localhost",
      "TargetPort": -1
    },
    {
      "ProxyType": "Honeypot",
      "ListeningAddress": "192.168.1.100",
      "ListeningPortRange": "82-442",
      "TargetHost": "localhost",
      "TargetPort": -1
    }
  ]
}
```