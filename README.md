# .NET Reverse Proxy
This project implements a simple yet powerful reverse proxy in .NET, designed to forward requests from clients to other servers and return responses back to the clients. It supports both HTTP and HTTPS traffic, provides basic logging capabilities, and can be easily extended for more advanced scenarios such as load balancing, request modification, and more.

# Features
* Support multiple listening ports: Allowing parallel forward requests to different target servers based on configuration.
* HTTP and HTTPS Support: Forward HTTP and HTTPS requests transparently.
* Logging: Log requests and responses for debugging and monitoring purposes.
* Cancellation Support: Gracefully handle shutdown requests with proper cleanup.
* Duplex Streaming: Real-time data processing with the ability to intercept and log data
* Asynchronous I/O: Leverages C#'s async/await for efficient network operations.
