using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Application.DTOs
{
    public class EmployeesMonthlyPaymentDto
    {
        public int Id { get; set; }
        public int EmployeeMonthlyPaymentLevelId { get; set; }
        public EmployeeMonthlyPaymentLevelDto EmployeeMonthlyPaymentLevel { get; set; }
        public int EmployeeId { get; set; }
        public EmployeeDto Employee { get; set; }
        public string FullName { get; set; }//Only on the dto for for reasons
        public double BasicSalary { get; set; }
        public double Allowance { get; set; }
        public double Bonus { get; set; }
        public double Deduction { get; set; }
        public EmployeeMonthlyPaymentStatus EmployeeMonthlyPaymentStatus { set; get; }
        public PaymentType PaymentType { set; get; }
        [StringLength(170)]
        public string Comments { get; set; }
    }
}

public class YearDto
{
    public byte Id { get; set; }
    public short Year { get; set; }
    public string SchoolId { get; set; }
    public SchoolInfoDto School { get; set; }
}

public class MonthDto
{
    public int Id { get; set; }
    public short YearId { set; get; }
    public YearDto Year { get; set; }
    public byte Month { get; set; }
    public EmployeeMonthlyPaymentLevel EmployeeMonthlyPaymentLevel { set; get; }
}
