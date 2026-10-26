using NAITSSMS.Domain.Enum;
using System.ComponentModel.DataAnnotations.Schema;

namespace NAITSSMS.Application.DTOs
{
    public class SetEmployeeSalary
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public EmployeeDto Employee { get; set; }
        public TimeStatus TimeStatus { get; set; }
        public decimal Salary { get; set; }
    }
}
