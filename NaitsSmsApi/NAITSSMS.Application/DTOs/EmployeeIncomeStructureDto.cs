using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
   public class EmployeeIncomeStructureDto
    {
        public short Id { get; set; }
        public int EmployeeId { get; set; }
        public EmployeeDto Employee { get; set; }
        public EmployeeQualificationStatus EmployeeQualificationStatus { set; get; }
        [Display(Name = "Basic Salary")]
        public double BasicSalary { get; set; }
        public double Allowance { get; set; }
   }
}


