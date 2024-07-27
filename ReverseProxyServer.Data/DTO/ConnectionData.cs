using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReverseProxyServer.Data.DTO
{
    [Table("ConnectionsData")]
    public class ConnectionData
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        public string? SessionId { get; set; }
        public string? CommunicationDirection { get; set; }
        public string? Data { get; set; }

        public ConnectionData()
        {

        }
        public ConnectionData(string sessionId, string communicationDirection, string data)
        {
            this.SessionId = sessionId;
            this.CommunicationDirection = communicationDirection;
            this.Data = data;
        }
    }
}
