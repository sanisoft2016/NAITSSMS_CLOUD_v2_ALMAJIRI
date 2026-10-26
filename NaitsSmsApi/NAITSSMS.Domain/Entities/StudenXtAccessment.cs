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
    public class StudenXtAccessment
    {
        [StringLength(40)]
        public required string Id { get; set; }
        public DateTime LastUpdateDate { get; set; }
        //public TermlyAccessmentStatus TermlyAccessmentStatus { get; set; }
        //public CurrentTermStatus Term { get; set; }
        [StringLength(40)]
        public string SubjectId { get; set; } = string.Empty;
        [ForeignKey("SubjectId")]
        public RxSubject? Subject { get; set; }
        [StringLength(40)]
        public string StudentId { get; set; } = string.Empty;

        [ForeignKey("StudentId")]
        public TxStudentDetail? Student { get; set; }
        [StringLength(40)] public string EmployeeId { get; set; } = string.Empty;
        [ForeignKey("EmployeeId")]
        public TxEmployee? Employee { get; set; }
        [StringLength(40)]
        public string ClassArmId { get; set; } = string.Empty;
        [ForeignKey("ClassArmId")]
        public AxClassArm? ClassArm { get; set; }
        [StringLength(40)]
        public string YearlyAndTermlyStatusId { get; set; } = string.Empty;  
        [ForeignKey("YearlyAndTermlyStatusId")]
        public TxYearlyAndTermlyStatus? YearlyAndTermlyStatus { get; set; }
        public float Ca1 { get; set; }
        public float Ca2 { get; set; }
        public float Ca3 { get; set; }
        public float Exams { get; set; }
    }
}
