using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReverseProxyServer.Data.DTO
{
    [Table("Instances")]
    public class Instance
    {
        [Key]
        public string InstanceId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public long? RowId { get; set; }

    }
}
