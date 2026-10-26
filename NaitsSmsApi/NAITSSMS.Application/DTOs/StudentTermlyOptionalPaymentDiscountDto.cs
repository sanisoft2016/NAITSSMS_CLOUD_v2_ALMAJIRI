using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class StudentTermlyOptionalPaymentDiscountDto
    {
        public int Id { get; set; }
        public string StudentTermlyOptionalChargesId { get; set; }
        public StudentTermlyOptionalChargesDto StudentTermlyOptionalCharges { get; set; }
        public double DiscountAmount { get; set; }
        [StringLength(100)]
        public string ReasonsForDiscount { get; set; }
    }
}
