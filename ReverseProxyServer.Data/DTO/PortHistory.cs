using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace ReverseProxyServer.Data.DTO
{
    [Table("PortsHistory")]
    public class PortHistory
    {
        [Key]
        public long Port { get; set; }
        public DateTime LastConnectionTime { get; set; }
        
        public long Hits { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public long RowId { get; set; }
    }
}
