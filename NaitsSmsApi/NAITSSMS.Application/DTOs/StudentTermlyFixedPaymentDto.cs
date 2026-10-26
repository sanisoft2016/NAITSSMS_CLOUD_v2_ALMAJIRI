using NAITSSMS.Domain.Enum;
using System;
using System.ComponentModel.DataAnnotations;

using System.ComponentModel.DataAnnotations.Schema;

namespace NAITSSMS.Application.DTOs
{
    public class StudentTermlyFixedPaymentDto
    {
        public int StudentAcademicProgressHistoryId { get; set; }
        [ForeignKey("StudentAcademicProgressHistoryId")]
        public StudentAcademicProgressHistoryDto StudentAcademicProgressHistory { get; set; }
        public PaymentType PaymentType { get; set; }

        [MaxLength(10)]
        [Required]
        public string PaymentId { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
    }
}
