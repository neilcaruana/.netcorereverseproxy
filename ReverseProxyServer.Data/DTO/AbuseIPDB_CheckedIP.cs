using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReverseProxyServer.Data.DTO;

[Table("AbuseIPDB_CheckedIPS")]
public class AbuseIPDB_CheckedIP
{
    /// <summary>
    /// The IP address that's being checked.
    /// </summary>
    [Key]
    public string IPAddress { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a public or private IP address.
    /// </summary>
    public long IsPublic { get; set; }

    /// <summary>
    /// The version of this IP address (4/6).
    /// </summary>
    public long IPVersion { get; set; }

    /// <summary>
    /// Whether this is a whitelisted IP address.
    /// </summary>
    public long? IsWhitelisted { get; set; }

    /// <summary>
    /// The abuse confidence score for this IP address as percentage.
    /// </summary>
    public long AbuseConfidence { get; set; }

    /// <summary>
    /// The ISO 3166 alpha-2 country code where this IP address is located.
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>
    /// The country name where this IP address is located.
    /// </summary>
    public string? CountryName { get; set; }

    /// <summary>
    /// The usage type of this IP address, example: Data Center/Web Hosting/Transit.
    /// </summary>
    public string? UsageType { get; set; }

    /// <summary>
    /// The longernet Service Provider of this IP address.
    /// </summary>
    public string? ISP { get; set; }

    /// <summary>
    /// The domain name associated with this IP address.
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// The hostnames that are polonging to this IP address.
    /// </summary>
    public string Hostnames { get; set; } = string.Empty;

    /// <summary>
    /// The total amount of reports that have been submitted for this IP address in the time period.
    /// </summary>
    public long TotalReports { get; set; }

    /// <summary>
    /// The amount of distinct users that have ever reported this IP address.
    /// </summary>
    public long DistinctUserCount { get; set; }

    /// <summary>
    /// When the newest report for this IP address has been submitted.
    /// </summary>
    public DateTime? LastReportedAt { get; set; }

    /// <summary>
    /// The row ID, which is unique.
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public long? RowId { get; set; }
}
