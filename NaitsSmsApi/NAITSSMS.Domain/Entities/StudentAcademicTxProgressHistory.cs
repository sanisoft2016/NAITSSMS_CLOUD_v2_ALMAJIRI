using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Domain.Entities
{
    public class StudentAcademicTxProgressHistory
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }

        [StringLength(40)]
        public string StudentId { get; set; } = string.Empty;
        [ForeignKey("StudentId")]
        public TxStudentDetail? StudentDetails { get; set; }
        [StringLength(40)]
        public string YearlyAndTermlyStatusId { get; set; } = string.Empty;
        [ForeignKey("YearlyAndTermlyStatusId")]
        public TxYearlyAndTermlyStatus? YearlyAndTermlyStatus { get; set; }
        [StringLength(40)]
        public string ClassId { get; set; } = string.Empty;
        [ForeignKey("ClassId")]
        public RxClasss? Classs { get; set; }
        public ClassCategoryStatus ClassCategory { get; set; }
        public List<StudentTyTermlyFeePayment> StudentTermlyFeePayment { get; set; } = new List<StudentTyTermlyFeePayment>();
    }
}
