using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReverseProxyServer.Data.DTO
{
    [Table("IPAddressHistory")]
    public class IPAddressHistory
    {
        [Key]
        public string IPAddress { get; set; } = string.Empty;
        public DateTime LastConnectionTime { get; set; }
        public long Hits { get; set; }
        public long IsBlacklisted { get; set; }
        
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public long RowId { get; set; }
    }
}
