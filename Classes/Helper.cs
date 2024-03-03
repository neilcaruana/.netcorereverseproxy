namespace ReverseProxyServer
{
    public static class Helper
    {
        public static string CleanData(string data)
        {
            return data.Replace(Environment.NewLine, " ").Trim();
        }
    }
}
