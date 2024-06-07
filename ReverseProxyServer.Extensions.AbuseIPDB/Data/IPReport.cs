using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ReverseProxyServer.Extensions.AbuseIPDB.Data
{
    /// <summary>
    /// An IP address report.
    /// </summary>
    public class IPReport
    {
        /// <summary>
        /// A category for IP address reports.
        /// </summary>
        public enum IPReportCategory
        {
            /// <summary>
            /// IP address is involved in: DNS Compromise
            /// <para><b>AbuseIPDB Definition:</b> Altering DNS records resulting in improper redirection.</para>
            /// </summary>
            DNSCompromise = 1,
            /// <summary>
            /// IP address is involved in: DNS Poisoning
            /// <para><b>AbuseIPDB Definition:</b>  Falsifying domain server cache (cache poisoning).</para>
            /// </summary>
            DNSpoisoning = 2,
            /// <summary>
            /// IP address is involved in: Fraud Orders
            /// <para><b>AbuseIPDB Definition:</b> Fraudulent orders.</para>
            /// </summary>
            FraudOrders = 3,
            /// <summary>
            /// IP address is involved in: DDoS Attack
            /// <para><b>AbuseIPDB Definition:</b> Participating in distributed denial-of-service (usually part of botnet).</para>
            /// </summary>
            DDoSAttack = 4,
            /// <summary>
            /// IP address is involved in: FTP Brute-Force
            /// </summary>
            FTPBruteForce = 5,
            /// <summary>
            /// IP address is involved in: Ping of Death
            /// <para><b>AbuseIPDB Definition:</b> Oversized IP packet.</para>
            /// </summary>
            PingOfDeath = 6,
            /// <summary>
            /// IP address is involved in: Phishing
            /// <para><b>AbuseIPDB Definition:</b> Phishing websites and/or email.</para>
            /// </summary>
            Phishing = 7,
            /// <summary>
            /// IP address is involved in: Fraud VoIP
            /// </summary>
            FraudVoIP = 8,
            /// <summary>
            /// IP address is involved in: Open Proxy
            /// <para><b>AbuseIPDB Definition:</b> Open proxy, open relay, or Tor exit node.</para>
            /// </summary>
            OpenProxy = 9,
            /// <summary>
            /// IP address is involved in: Web Spam
            /// <para><b>AbuseIPDB Definition:</b> Comment/forum spam, HTTP referer spam, or other CMS spam.</para>
            /// </summary>
            WebSpam = 10,
            /// <summary>
            /// IP address is involved in: Email Spam
            /// <para><b>AbuseIPDB Definition:</b> Spam email content, infected attachments, and phishing emails.</para>
            /// </summary>
            EmailSpam = 11,
            /// <summary>
            /// IP address is involved in: Blog Spam
            /// <para><b>AbuseIPDB Definition:</b> CMS blog comment spam.</para>
            /// </summary>
            BlogSpam = 12,
            /// <summary>
            /// IP address is involved in: VPN IP
            /// <para><b>AbuseIPDB Definition:</b> Conjunctive category.</para>
            /// </summary>
            VPN = 13,
            /// <summary>
            /// IP address is involved in: Port Scan
            /// <para><b>AbuseIPDB Definition:</b> Scanning for open ports and vulnerable services.</para>
            /// </summary>
            PortScan = 14,
            /// <summary>
            /// IP address is involved in: Hacking
            /// </summary>
            Hacking = 15,
            /// <summary>
            /// IP address is involved in: SQL Injection
            /// <para><b>AbuseIPDB Definition:</b> Attempts at SQL injection.</para>
            /// </summary>
            SQLInjection = 16,
            /// <summary>
            /// IP address is involved in: Spoofing
            /// <para><b>AbuseIPDB Definition:</b> Email sender spoofing.</para>
            /// </summary>
            Spoofing = 17,
            /// <summary>
            /// IP address is involved in: Brute Force
            /// <para><b>AbuseIPDB Definition:</b> Credential brute-force attacks on webpage logins and services like SSH, FTP, SIP, SMTP, RDP, etc. This category is seperate from DDoS attacks.</para>
            /// </summary>
            BruteForce = 18,
            /// <summary>
            /// IP address is involved in: Bad Web Bot
            /// <para><b>AbuseIPDB Definition:</b> Webpage scraping (for email addresses, content, etc) and crawlers that do not honor robots.txt. Excessive requests and user agent spoofing can also be reported here.</para>
            /// </summary>
            BadWebBot = 19,
            /// <summary>
            /// IP address is involved in: Exploited Host
            /// <para><b>AbuseIPDB Definition:</b> Host is likely infected with malware and being used for other attacks or to host malicious content. The host owner may not be aware of the compromise. This category is often used in combination with other attack categories.</para>
            /// </summary>
            ExploitedHost = 20,
            /// <summary>
            /// IP address is involved in: Web App Attack
            /// <para><b>AbuseIPDB Definition:</b> Attempts to probe for or exploit installed web applications such as a CMS like WordPress/Drupal, e-commerce solutions, forum software, phpMyAdmin and various other software plugins/solutions.</para>
            /// </summary>
            WebAppAttack = 21,
            /// <summary>
            /// IP address is involved in: SSH Brute-Forcé
            /// <para><b>AbuseIPDB Definition:</b> Secure Shell (SSH) abuse. Use this category in combination with more specific categories.</para>
            /// </summary>
            SSH = 22,
            /// <summary>
            /// IP address is involved in: IoT Targeted
            /// <para><b>AbuseIPDB Definition:</b> Abuse was targeted at an "Internet of Things" type device. Include information about what type of device was targeted in the comments.</para>
            /// </summary>
            IoTTargeted = 23
        }

        /// <summary>
        /// When was this report submitted.
        /// </summary>
        [JsonPropertyName("reportedAt")]
        public DateTime ReportedAt { get; set; }

        /// <summary>
        /// A short comment explaining the abusive activity from this IP address.
        /// </summary>
        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        /// <summary>
        /// The report categories for the abusive activity.
        /// </summary>
        [JsonPropertyName("categories")]
        public IPReportCategory[]? Categories { get; set; }

        /// <summary>
        /// The ID of the reporter.<br/>
        /// You can view their profile by going to <a href="https://www.abuseipdb.com/user/"></a> and appending the user ID.
        /// </summary>
        [JsonPropertyName("reporterId")]
        public int ReporterId { get; set; }

        /// <summary>
        /// The <c>ISO 3166 alpha-2</c> country code of the reporter.
        /// </summary>
        [JsonPropertyName("reporterCountryCode")]
        public string? ReporterCountryCode { get; set; }

        /// <summary>
        /// The country name of the reporter.
        /// </summary>
        [JsonPropertyName("reporterCountryName")]
        public string? ReporterCountryName { get; set; }
    }
}
