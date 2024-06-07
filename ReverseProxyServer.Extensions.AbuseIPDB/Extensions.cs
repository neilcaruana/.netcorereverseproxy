using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ReverseProxyServer.Extensions.AbuseIPDB
{
    internal static class Extensions
    {
        public static async Task<T?> Deseralize<T>(this HttpResponseMessage res, JsonSerializerOptions? options = null)
        {
            using Stream stream = await res.Content.ReadAsStreamAsync();
            if (stream.Length == 0) throw new Exception("Response content is empty, can't parse as JSON.");

            try
            {
                return await JsonSerializer.DeserializeAsync<T>(stream, options);
            }
            catch (Exception ex)
            {
                throw new Exception($"Exception while parsing JSON: {ex.GetType().Name} => {ex.Message}");
            }
        }
    }
}
