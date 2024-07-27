using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReverseProxyServer.Data.DTO
{
    [Table("Connections")]
    public class Connection
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]   
        public long Id { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string InstanceId { get; set; } = string.Empty;
        public DateTime ConnectionTime { get; set; }
        public string ProxyType { get; set; } = string.Empty;
        public string LocalAddress { get; set; } = string.Empty;
        public long LocalPort { get; set; }
        public string TargetHost { get; set; } = string.Empty;
        public long TargetPort { get; set; }
        public string RemoteAddress { get; set; } = string.Empty;
        public long RemotePort { get; set; }
    }
}
