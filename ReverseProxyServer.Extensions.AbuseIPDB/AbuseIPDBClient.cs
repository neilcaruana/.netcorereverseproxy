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
    public class AbuseIPDBClient : IDisposable
    {
        private static readonly Uri BaseUri = new($"https://api.abuseipdb.com/api/v2/");
        private static readonly Version HttpVersion = new(2, 0);
        private static readonly String UserAgent = ".NET ReverseProxy";

        // Static HttpClient for shared usage with proper lifetime management
        // This prevents socket exhaustion while avoiding disposal issues
        private static readonly Lazy<HttpClient> SharedHttpClient = new(() =>
        {
            var handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.All
            };

            var client = new HttpClient(handler)
            {
                BaseAddress = BaseUri,
                DefaultRequestVersion = HttpVersion,
                Timeout = TimeSpan.FromSeconds(30) // Set appropriate timeout
            };

            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

            return client;
        });

        private readonly string apiKey;
        private bool disposed = false;

        public AbuseIPDBClient(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException($"'{nameof(apiKey)}' cannot be null or whitespace.", nameof(apiKey));
            }

            this.apiKey = apiKey;
        }

        public async Task<CheckedIP> CheckIP(string ip, bool verbose = false, int maxAge = 90)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(AbuseIPDBClient));
            
            try
            {
                if (string.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip), "IP address to check is null or empty.");
                if (maxAge <= 0) throw new ArgumentOutOfRangeException(nameof(maxAge), "Max age has to be a positive value.");

                var client = SharedHttpClient.Value;
                using var requestMessage = new HttpRequestMessage(HttpMethod.Get, 
                    $"check?ipAddress={WebUtility.UrlEncode(ip)}&maxAgeInDays={maxAge}{(verbose ? "&verbose" : "")}");
                
                // Add API key to this specific request
                requestMessage.Headers.Add("Key", apiKey);

                using HttpResponseMessage res = await client.SendAsync(requestMessage);
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
                throw new Exception($"[{ip}] AbuseIPDB {ex.GetBaseException().Message}");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // No need to dispose shared HttpClient
                    // It will be disposed when the application shuts down
                }
                disposed = true;
            }
        }
    }
}
