using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class StudentTermlyOptionalPaymentDto:AuditableEntityDto
    {
        public StudentTermlyOptionalPaymentDto()
        {
            Id = Guid.NewGuid().ToString();
        }
        public string StudentTermlyOptionalChargesId { get; set; }
        public StudentTermlyOptionalChargesDto StudentTermlyOptionalCharges { get; set; }
        public DateTime PaymentDate { get; set; }
        public PaymentType PaymentType { get; set; }
        [MaxLength(10)]
        [Required]
        public string PaymentId { get; set; }
        public double Amount { get; set; }


        public double Discount { get; set; }
        public string ReasonForDiscount { get; set; }

    }
}
