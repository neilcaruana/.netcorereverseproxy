using Microsoft.VisualBasic;
using ReverseProxyServer.Extensions.AbuseIPDB.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace ReverseProxyServer.Extensions.AbuseIPDB
{
    public class AbuseIPDBClient
    {
        private static readonly Uri BaseUri = new($"https://api.abuseipdb.com/api/v2/");
        private static readonly Version HttpVersion = new(2, 0);
        private static readonly String UserAgent = ".NET ReverseProxy";

        private static readonly HttpClientHandler HttpHandler = new()
        {
            AutomaticDecompression = DecompressionMethods.All
        };
        private readonly HttpClient Client = new(handler: HttpHandler)
        {
            BaseAddress = BaseUri,
            DefaultRequestVersion = HttpVersion
        };

        public AbuseIPDBClient(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException($"'{nameof(apiKey)}' cannot be null or whitespace.", nameof(apiKey));
            }

            Client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
            Client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            Client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            Client.DefaultRequestHeaders.Add("Key", apiKey);
        }

        public async Task<CheckedIP> CheckIP(string ip, bool verbose = false, int maxAge = 90)
        {
            try
            {
                if (string.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip), "IP address to check is null or empty.");
                if (maxAge <= 0) throw new ArgumentOutOfRangeException(nameof(maxAge), "Max age has to be a positive value.");

                using HttpResponseMessage res = await Client.GetAsync($"check?ipAddress={WebUtility.UrlEncode(ip)}&maxAgeInDays={maxAge}{(verbose ? "&verbose" : "")}");
                res.EnsureSuccessStatusCode();

                using Stream stream = await res.Content.ReadAsStreamAsync();
                if (stream.Length == 0)
                    throw new Exception($"No data returned from AbuseIPDB web service for IP Address {ip}");

                CheckedIPContainer? checkedIpData = await res.Deseralize<CheckedIPContainer>();

                CheckedIP checkedIP = (checkedIpData?.Data) ?? throw new Exception($"Invalid data returned from AbuseIPDB web service for IP Address {ip}");
                return checkedIP;

            }
            catch (Exception ex)
            {
                throw new Exception($"[{ip}] AbuseIPDB {ex.Message}");
            }
        }
    }
}
