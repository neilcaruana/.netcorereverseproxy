# .NET Core Reverse Proxy
A lightweight reverse proxy and simple Honeypot in .NET 9, designed to proxy requests and also act as a honeypot capturing request data for analysis. It supports both HTTP and HTTPS traffic, provides configurable logging capabilities, and can be easily extended for more advanced scenarios such as load balancing, request modification, and more.

## Features
* HTTP and HTTPS Support: Forward HTTP and HTTPS requests transparently.
* Support for a range of listening ports: Allowing parallel forward requests to different target servers based on configuration.
* Sentinel Mode: Automatically listens on all available ports on the machine, used to act as a Honeypot for research purposes.
* Logging: Log requests and responses for debugging and monitoring purposes.
* Cancellation Support: Gracefully handle shutdown requests with proper cleanup.
* Duplex Streaming: Real-time data processing with the ability to intercept and log data.
* Asynchronous I/O: Leverages C#'s async/await for efficient network operations.
* Task-based Asynchronous Pattern (TAP): Asynchronous programming is used extensively to efficiently handle all events including I/O and network operations.
* Statistics: Provides a console report with active and historical connections by unique IP Addresses and ports.
* AbuseIPDB support: Every request IP is cross referenced with the AbuseIPDB database and is blacklisted based on confidence score.
* Sqlite Database support: All connection requests and data can be stored in a local DB, various statistics tables are generated storing all traffic history.

## Configuration
Listening endpoints can be configured through the appsettings.json file. Each entry within the Endpoints section represents a separate proxy endpoint configuration, including its own type, listening port ranges, target host, target port etc.

Below is an example configuration for setting up multiple listening endpoints:

```
{
  "SentinelMode": true,
  "SendTimeout": 60,
  "ReceiveTimeout": 60,
  "BufferSize": 4096,
  "LogLevel": "Debug",
  "DatabasePath": "stats.db",
  "AbuseIPDBApiKey": "APIKEY_VALUE",
  "EndPoints": [
    {
      "ProxyType": "Forward",
      "ListeningAddress": "localhost",
      "ListeningPortRange": "80",
      "TargetHost": "192.168.1.100",
      "TargetPort": 81
    },
    {
      "ProxyType": "Forward",
      "ListeningAddress": "localhost",
      "ListeningPortRange": "443",
      "TargetHost": "192.168.1.100",
      "TargetPort": 444
    },
    {
      "ProxyType": "Honeypot",
      "ListeningAddress": "localhost",
      "ListeningPortRange": "1-79",
      "TargetHost": "192.168.1.100",
      "TargetPort": -1
    },
    {
      "ProxyType": "Honeypot",
      "ListeningAddress": "localhost",
      "ListeningPortRange": "82-120",
      "TargetHost": "192.168.1.100",
      "TargetPort": -1
    }
  ]
}
```