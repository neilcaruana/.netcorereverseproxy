# .NET Reverse Proxy
This project implements a simple yet powerful reverse proxy in .NET, designed to forward requests from clients to other servers and return responses back to the clients. It supports both HTTP and HTTPS traffic, provides basic logging capabilities, and can be easily extended for more advanced scenarios such as load balancing, request modification, and more.

# Features
* Support multiple listening ports: Allowing parallel forward requests to different target servers based on configuration.
* HTTP and HTTPS Support: Forward HTTP and HTTPS requests transparently.
* Logging: Log requests and responses for debugging and monitoring purposes.
* Cancellation Support: Gracefully handle shutdown requests with proper cleanup.
* Duplex Streaming: Real-time data processing with the ability to intercept and log data
* Asynchronous I/O: Leverages C#'s async/await for efficient network operations.

# Configuring Listening Ports
Listening ports can be configured through the appsettings.json file under the ReverseProxyEndpoints section. Each entry within this section represents a separate proxy endpoint configuration, including its own listening port, target host, and target port etc.

Here is an example configuration for setting up multiple listening ports:

```
{
  "ReverseProxyEndpoints": [
    {
      "ListeningPort": 8080,
      "TargetHost": "example.com",
      "TargetPort": 80,
      "CertificatePath": "",
      "CertificatePassword": "",
      "ProxyType": "LogOnly"
    },
    {
      "ListeningPort": 8081,
      "TargetHost": "anotherexample.com",
      "TargetPort": 80,
      "CertificatePath": "",
      "CertificatePassword": "",
      "ProxyType": "LogAndProxy"
    }
  ]
}
```