# .NET Core Reverse Proxy
This project implements a lightweight reverse proxy and simple Honeypot in .NET 8, designed to proxy requests and also act as a simple honeypot capturing request data for analysis. It supports both HTTP and HTTPS traffic, provides configurable logging capabilities, and can be easily extended for more advanced scenarios such as load balancing, request modification, and more.

## Features
* HTTP and HTTPS Support: Forward HTTP and HTTPS requests transparently.
* Support for a range of listening ports: Allowing parallel forward requests to different target servers based on configuration.
* Logging: Log requests and responses for debugging and monitoring purposes.
* Cancellation Support: Gracefully handle shutdown requests with proper cleanup.
* Duplex Streaming: Real-time data processing with the ability to intercept and log data
* Asynchronous I/O: Leverages C#'s async/await for efficient network operations.
* Statistics: Provides a console report with active and historical connections by unique IP Addresses and ports

## Configuration
Listening endpoints can be configured through the appsettings.json file. Each entry within the Endpoints section represents a separate proxy endpoint configuration, including its own type, listening port ranges, target host, target port etc.

Below is an example configuration for setting up multiple listening endpoints:

```
{
  "SendTimeout": 60,
  "ReceiveTimeout": 60,
  "BufferSize": 4096,
  "LogLevel": "Debug",
  "EndPoints": [
    {
      "ProxyType": "Forward",
      "ListeningAddress": "localhost",
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
      "ListeningPortRange": "82-120",
      "TargetHost": "localhost",
      "TargetPort": -1
    }
  ]
}
```