using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Domain.Entities
{
   public class TeachTxerSubjectArmAssignment
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        [MaxLength(40)]
        public string SubjectId { get; set; } = string.Empty;
        [ForeignKey("SubjectId")]
        public RxSubject? Subject { get; set; }
        [MaxLength(40)]
        public string EmployeeId { get; set; } = string.Empty;
        [ForeignKey("EmployeeId")]
        public TxEmployee? Employee { get; set; }
        [MaxLength(40)]
        public string ArmId { get; set; } = string.Empty;
        [ForeignKey("ArmId")]
        public AxClassArm? Arm { get; set; }
    }
}
