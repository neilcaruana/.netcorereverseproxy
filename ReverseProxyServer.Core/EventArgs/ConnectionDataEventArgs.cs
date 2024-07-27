using System.Text;

namespace ReverseProxyServer.Core
{
    public class ConnectionDataEventArgs(string sessionId, string communicationDirection, StringBuilder rawData) : EventArgs
    {
        public string SessionId { get; init; } = sessionId;
        public string CommunicationDirection { get; init; } = communicationDirection;
        public StringBuilder RawData { get; init; } = rawData;
    }
}
