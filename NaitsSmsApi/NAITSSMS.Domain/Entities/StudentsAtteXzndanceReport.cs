using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Entities;

namespace NAITSSMS.Domain.Entities
{
   public class StudentsAtteXzndanceAndCommentsReport
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        public byte Attendance { get; set; }
        [StringLength(150)]
        public string TeacherComments { get; set; } = string.Empty;
        [StringLength(150)]
        public string SchoolHeadComments { get; set; } = string.Empty;
        [StringLength(40)]
        public string YearlyAndTermlyStatusId { get; set; } = string.Empty;
        [ForeignKey("YearlyAndTermlyStatusId")]
        public TxYearlyAndTermlyStatus? YearlyAndTermlyStatus { get; set; }
        [StringLength(40)]
        public string StudentId { get; set; } = string.Empty;
        [ForeignKey("StudentId")]
        public TxStudentDetail? Student { get; set; }
        [StringLength(40)]
        public string ClassArmId { get; set; } = string.Empty;
        [ForeignKey("ClassArmId")]
        public AxClassArm? ClassArm { get; set; }
        //public int SchoolInfoId { get; set; }
        //[ForeignKey("SchoolInfoId")]
        //public SchoolTyInfo SchoolInfo { get; set; }
    }
}
