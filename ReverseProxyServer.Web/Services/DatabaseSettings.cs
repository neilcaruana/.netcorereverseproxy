namespace ReverseProxyServer.Web.Services;

public class DatabaseSettings
{
    public string Path { get; set; } = "ReverseProxyDatabase.db";
    public string ReleasePath { get; set; } = string.Empty;
    public string DebugPath { get; set; } = string.Empty;
}
