using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ReverseProxyServer.Extensions.AbuseIPDB.Data
{
    /// <summary>
    /// A result of an IP check request.
    /// </summary>
    public class CheckedIP
    {
        /// <summary>
        /// The IP address that's being checked.
        /// </summary>
        [JsonPropertyName("ipAddress")]
        public string? IPAddress { get; set; }

        /// <summary>
        /// Whether this is a public or private IP address.
        /// </summary>
        [JsonPropertyName("isPublic")]
        public bool IsPublic { get; set; }

        /// <summary>
        /// The version of this IP address (4/6).
        /// </summary>
        [JsonPropertyName("ipVersion")]
        public int IPVersion { get; set; }

        /// <summary>
        /// <para>Whether this is a whitelisted IP address.</para>
        /// Whitelisted netblocks are typically owned by trusted entities, such as Google or Microsoft who may use them for search engine spiders.<br/>
        /// However, these same entities sometimes also provide cloud servers and mail services which are easily abused.<br/>
        /// Pay special attention when trusting or distrusting these IPs.<br/>
        /// <i>Source: AbuseIPDB</i>
        /// </summary>
        [JsonPropertyName("isWhitelisted")]
        public bool? IsWhitelisted { get; set; }

        /// <summary>
        /// The abuse confidence score for this IP address as percentage.
        /// </summary>
        [JsonPropertyName("abuseConfidenceScore")]
        public int AbuseConfidence { get; set; }

        /// <summary>
        /// The <c>ISO 3166 alpha-2</c> country code where this IP address is located.
        /// </summary>
        [JsonPropertyName("countryCode")]
        public string? CountryCode { get; set; }

        /// <summary>
        /// The country name where this IP address is located.
        /// </summary>
        [JsonPropertyName("countryName")]
        public string? CountryName { get; set; }

        /// <summary>
        /// The usage type of this IP address, example: <c>Data Center/Web Hosting/Transit</c>.
        /// </summary>
        [JsonPropertyName("usageType")]
        public string? UsageType { get; set; }

        /// <summary>
        /// The Internet Service Provider of this IP address.
        /// </summary>
        [JsonPropertyName("isp")]
        public string? ISP { get; set; }

        /// <summary>
        /// The domain name associated with this IP address.
        /// </summary>
        [JsonPropertyName("domain")]
        public string? Domain { get; set; }

        /// <summary>
        /// The hostnames that are pointing to this IP address.
        /// </summary>
        [JsonPropertyName("hostnames")]
        public string[]? Hostnames { get; set; }

        /// <summary>
        /// The total amount of reports that have been submitted for this IP address in the time period.
        /// </summary>
        [JsonPropertyName("totalReports")]
        public int TotalReports { get; set; }

        /// <summary>
        /// The amount of distinct users that have ever reported this IP address.
        /// </summary>
        [JsonPropertyName("numDistinctUsers")]
        public int DistinctUserCount { get; set; }

        /// <summary>
        /// When the newest report for this IP address has been submitted.
        /// </summary>
        [JsonPropertyName("lastReportedAt")]
        public DateTime? LastReportedAt { get; set; }

        /// <summary>
        /// An array of <see cref="IPReport"/> containing the recent reports for this IP address. You can also use the <see cref="AbuseIPDBClient"/><c>.GetReports()</c> method.
        /// </summary>
        [JsonPropertyName("reports")]
        public IPReport[]? Reports { get; set; }
    }
    /// <summary>
    /// A container for <see cref="CheckedIP"></see>.
    /// </summary>
    public class CheckedIPContainer
    {
        [JsonPropertyName("data")]
        public CheckedIP? Data { get; set; }
    }
}
