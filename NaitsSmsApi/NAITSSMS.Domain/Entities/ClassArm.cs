using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Domain.Entities
{
   public class AxClassArm
   {
        [Key, MaxLength(40)]
        public required string Id { get; set; }

        public byte SNo { get; set; }

        [MaxLength(40)]
        public string ClassId { get; set; } = string.Empty;
        [ForeignKey("ClassId")]
       public RxClasss? Classs { get; set; }
        public ArmName ArmName { get; set; }
        [MaxLength(40)]
        public string FormMaster { get; set; } = string.Empty;
        public ICollection<TxStudentDetail> StudentDetails { get; set; } = new List<TxStudentDetail>();
        //public IEnumerable<TeachTxerSubjectArmAssignment> TeacherSubjectArmAssignment { get; set; }
    }
}
