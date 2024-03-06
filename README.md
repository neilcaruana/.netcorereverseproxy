# .NET Core Reverse Proxy
This project implements a lightweight reverse proxy in .NET 8, designed to forward requests from clients to other servers and return responses back to the clients. It supports both HTTP and HTTPS traffic, provides basic logging capabilities, and can be easily extended for more advanced scenarios such as load balancing, request modification, and more.

## Features
* HTTP and HTTPS Support: Forward HTTP and HTTPS requests transparently.
* Support for a range of listening ports: Allowing parallel forward requests to different target servers based on configuration.
* Logging: Log requests and responses for debugging and monitoring purposes.
* Cancellation Support: Gracefully handle shutdown requests with proper cleanup.
* Duplex Streaming: Real-time data processing with the ability to intercept and log data
* Asynchronous I/O: Leverages C#'s async/await for efficient network operations.

## Configuring Endpoints
Listening endpoints can be configured through the appsettings.json file under the ReverseProxyEndpoints section. Each entry within this section represents a separate proxy endpoint configuration, including its own listening port ranges, target host, and target port etc.

Below is an example configuration for setting up multiple listening endpoints:

```
{
  "ReverseProxyEndpoints": [
    {
      "ListeningPortRange": {
        "Start": 8080,
        "End": 8085
      },
      "TargetHost": "example.com",
      "TargetPort": 80,
      "CertificatePath": "",
      "CertificatePassword": "",
      "ProxyType": "LogOnly"
    },
    {
      "ListeningPortRange": {
        "Start": 8086,
        "End": 8086
      },
      "TargetHost": "anotherexample.com",
      "TargetPort": 80,
      "CertificatePath": "",
      "CertificatePassword": "",
      "ProxyType": "LogAndProxy"
    }
  ]
}
```