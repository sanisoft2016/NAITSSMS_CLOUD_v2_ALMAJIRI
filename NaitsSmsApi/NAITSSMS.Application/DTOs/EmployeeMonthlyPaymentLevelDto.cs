using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class EmployeeMonthlyPaymentLevelDto
    {
        public int MonthId { get; set; }
        public MonthDto Month { set; get; }
        public EmployeeMonthlyPaymentLevel EmployeeMonthlyPaymentLevel { set; get; }
    }
}
