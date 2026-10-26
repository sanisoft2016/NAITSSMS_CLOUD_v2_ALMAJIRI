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
    public class StudentTermlyOptionalPaymentDiscount : AuditableEntity
    {
        [Key, MaxLength(40)]
        public override string Id { get => base.Id; set => base.Id = value; }

        public required string StudentTermlyOptionalChargesId { get; set; }

        [ForeignKey("StudentTermlyOptionalChargesId")]
        public StudentTermlyOptionalCharges? StudentTermlyOptionalCharges { get; set; }
        public double DiscountAmount { get; set; }
        [StringLength(100)]
        public string ReasonsForDiscount { get; set; } = string.Empty;
    }
}
