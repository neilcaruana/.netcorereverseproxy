using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class HttpRequest
{
    public string Method { get; set; }
    public Uri RequestUri { get; set; }
    public string HttpVersion { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public static class HttpParser
{
    public static HttpRequest ParseHttpRequest(string httpRequest)
    {
        HttpRequest request = new HttpRequest();
        string[] lines = httpRequest.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        // Parse request line
        string requestLinePattern = @"^(GET|POST|PUT|DELETE|HEAD|OPTIONS|PATCH|CONNECT|TRACE)\s+(\S+)\s+(HTTP\/1\.[01])$";
        Match requestLineMatch = Regex.Match(lines[0], requestLinePattern, RegexOptions.IgnoreCase);
        if (requestLineMatch.Success)
        {
            request.Method = requestLineMatch.Groups[1].Value;
            request.RequestUri = new Uri(requestLineMatch.Groups[2].Value, UriKind.RelativeOrAbsolute);
            request.HttpVersion = requestLineMatch.Groups[3].Value;
        }
        else
        {
            throw new FormatException("Invalid HTTP request line");
        }

        // Parse headers
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                // Reached the end of headers, potentially start of message body or end of request
                break;
            }

            int colonIndex = lines[i].IndexOf(':');
            if (colonIndex > -1)
            {
                string headerName = lines[i].Substring(0, colonIndex).Trim();
                string headerValue = lines[i].Substring(colonIndex + 1).Trim();

                // Handle repeated headers by concatenating their values with a comma (as per RFC 7230)
                if (request.Headers.ContainsKey(headerName))
                {
                    request.Headers[headerName] = string.Join(", ", request.Headers[headerName], headerValue);
                }
                else
                {
                    request.Headers.Add(headerName, headerValue);
                }
            }
        }

        return request;
    }
}
