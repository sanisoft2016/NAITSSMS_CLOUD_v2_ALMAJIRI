using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class StudentTermlyOptionalPaymentAndDiscountDto
    {
        public List<StudentTermlyOptionalPaymentDto> StudentTermlyOptionalPayment { get; set; }
        public List<StudentTermlyOptionalPaymentDiscountDto> StudentTermlyOptionalPaymentDiscount { get; set; }
    }
}
