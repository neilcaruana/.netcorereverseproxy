# .NET Core Reverse Proxy
This project implements a lightweight reverse proxy in .NET 8, designed to proxy requests and can also act as a simple honey pot to listen on ports and log the request only. It supports both HTTP and HTTPS traffic, provides configurable logging capabilities per endpoint, and can be easily extended for more advanced scenarios such as load balancing, request modification, and more.

## Features
* HTTP and HTTPS Support: Forward HTTP and HTTPS requests transparently.
* Support for a range of listening ports: Allowing parallel forward requests to different target servers based on configuration.
* Logging: Log requests and responses for debugging and monitoring purposes.
* Cancellation Support: Gracefully handle shutdown requests with proper cleanup.
* Duplex Streaming: Real-time data processing with the ability to intercept and log data
* Asynchronous I/O: Leverages C#'s async/await for efficient network operations.

## Configuring Endpoints
Listening endpoints can be configured through the appsettings.json file under the ReverseProxyEndpoints section. Each entry within this section represents a separate proxy endpoint configuration, including its own listening port ranges, target host, target port and log levels etc.

Below is an example configuration for setting up multiple listening endpoints:

```
{
  "ReverseProxyEndpoints": [
    {
      "ProxyType": "LogAndProxy",
      "ListeningPortRange": {
        "Start": 80,
        "End": 80
      },
      "TargetHost": "localhost",
      "TargetPort": 81,
      "LoggerType": "ConsoleAndFile",
      "LoggerLevel": "Warning"
    },
    {
      "ProxyType": "LogAndProxy",
      "ListeningPortRange": {
        "Start": 443,
        "End": 443
      },
      "TargetHost": "localhost",
      "TargetPort": 444,
      "LoggerType": "ConsoleAndFile",
      "LoggerLevel": "Warning"
    },
    {
      "ProxyType": "LogOnly",
      "ListeningPortRange": {
        "Start": 1,
        "End": 79
      },
      "TargetHost": "",
      "TargetPort": 0,
      "LoggerType": "ConsoleAndFile",
      "LoggerLevel": "Debug"
    }
  ]
}
```