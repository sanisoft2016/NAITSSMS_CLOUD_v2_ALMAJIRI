using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAITSSMS.Domain.Base;

namespace NAITSSMS.Domain.Entities
{
    public class StudentTyTermlyFixedPaymentDiscount:AuditableEntity
    {
        [Key, MaxLength(40)]
        public override string Id { get => base.Id; set => base.Id = value; }
        [MaxLength(40)]
        public string StudentAcademicProgressId { get; set; } = string.Empty;
        [ForeignKey("StudentAcademicProgressId")]
        public StudentAcademicTxProgressHistory? StudentAcademicProgressHistory { get; set; }
        public double DiscountAmount { get; set; }
        [StringLength(100)]
        public string ReasonsForDiscount { get; set; } = string.Empty;
    }
}
