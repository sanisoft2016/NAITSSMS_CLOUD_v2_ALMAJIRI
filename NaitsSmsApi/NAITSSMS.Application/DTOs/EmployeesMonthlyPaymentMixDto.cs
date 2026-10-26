using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class EmployeesMonthlyPaymentMixDto
    {
        public List<EmployeesMonthlyPaymentDto> EmployeesMonthlyPayment { get; set; }
        public List<EmployeeIncomeStructureDto> EmployeeIncomeStructure { get; set; }
    }
}
