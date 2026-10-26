using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Domain.Entities
{
    public class Agent
    {
        [StringLength(40), Key]
        public string UserId { get; set; } = string.Empty;
        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }
        public TimeStatus TimeStatus { get; set; }

        [StringLength(100)]
        public string FullAddress { get; set; } = string.Empty;
        [StringLength(7)]
        public string SchoolRegId { get; set; } = string.Empty;
    }
}
