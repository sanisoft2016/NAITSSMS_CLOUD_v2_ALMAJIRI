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
    public class StudentTyTermlyFeePayment:AuditableEntity
    {
        [Key, MaxLength(40)]
        public override string Id { get => base.Id; set => base.Id = value; }
        [StringLength(40)]
        public string StudentAcademicProgressHistoryId { get; set; } = string.Empty;
        [ForeignKey("StudentAcademicProgressHistoryId")]
        public StudentAcademicTxProgressHistory? StudentAcademicProgressHistory { get; set; }
        public PaymentType PaymentType { get; set; }

        [MaxLength(10)]
        [Required]
        public string PaymentId { get; set; } = string.Empty; //Unique Payment Identifier
        public DateTime PaymentDate { get; set; }
        public double Amount { get; set; }
    }
}
