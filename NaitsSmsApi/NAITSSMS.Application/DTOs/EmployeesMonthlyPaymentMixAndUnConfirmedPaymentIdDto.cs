using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class EmployeesMonthlyPaymentMixAndUnConfirmedPaymentIdDto
    {
        public EmployeesMonthlyPaymentMixDto EmployeesMonthlyPaymentMix { get; set; }
        public int UnConfirmedMonthlyPaymentId { get; set; }
    }
}
