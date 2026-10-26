using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Domain.Entities
{
    /// <summary>
    /// The same as Form Master
    /// </summary>
    public class TeacherClassArmTxAssignment
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        [StringLength(40)] 
        public string EmployeeId { get; set; } = string.Empty;
        [ForeignKey("EmployeeId")]
        public TxEmployee? Employee { get; set; }

        [StringLength(40)] 
        public string ArmId { get; set; } = string.Empty;
        [ForeignKey("ArmId")]
        public AxClassArm? Arm { get; set; }
        public ArmTermlyAssessmentStatus ArmTermlyAssessmentStatus { get; set; }
        //public int SchoolInfoId { get; set; }
        //[ForeignKey("SchoolInfoId")]
        //public SchoolTyInfo SchoolInfo { get; set; }
    }
}
