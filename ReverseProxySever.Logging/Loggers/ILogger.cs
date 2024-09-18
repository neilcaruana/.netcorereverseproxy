﻿namespace ReverseProxySever.Logging.Interfaces;

public interface ILogger
{
    Task LogInfoAsync(string message, string correlationId = "");
    Task LogErrorAsync(string message, Exception? exception = null, string correlationId = "");
    Task LogWarningAsync(string message, string correlationId = "");
    Task LogDebugAsync(string message, string correlationId = "");
    Task LogRequestAsync(string message, string correlationId = "");
}