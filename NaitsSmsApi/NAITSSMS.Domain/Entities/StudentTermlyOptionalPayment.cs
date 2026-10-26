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
    public class StudentTermlyOptionalPayment : AuditableEntity
    {
        [StringLength(40)]
        public override sealed string Id { get; set; }

        public StudentTermlyOptionalPayment()
        {
            Id = Guid.NewGuid().ToString();
            StudentTermlyOptionalChargesId = string.Empty;
            StudentTermlyOptionalCharges = null!;
            PaymentId = string.Empty;
        }

        public string StudentTermlyOptionalChargesId { get; set; }
        [ForeignKey("StudentTermlyOptionalChargesId")]
        public StudentTermlyOptionalCharges StudentTermlyOptionalCharges { get; set; }
        [Required]
        public DateTime PaymentDate { get; set; }
        public PaymentType PaymentType { get; set; }
        [MaxLength(10)]
        [Required]
        public string PaymentId { get; set; }
        [Required]
        public double Amount { get; set; }
    }
}
