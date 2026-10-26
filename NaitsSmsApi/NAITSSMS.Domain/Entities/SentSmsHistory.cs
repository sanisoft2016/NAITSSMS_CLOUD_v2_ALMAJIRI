using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAITSSMS.Domain.Base;

namespace NAITSSMS.Domain.Entities
{
    public class SentSmsHistory
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }

        [StringLength(40)]
        public string SchoolId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public byte NoOfMtnDndLine { get; set; }
        public byte NoOfOrdidaryLine { get; set; }
        public byte NoOfIvalidLine { get; set; }
        public byte MessageDate { get; set; }
    }
}
