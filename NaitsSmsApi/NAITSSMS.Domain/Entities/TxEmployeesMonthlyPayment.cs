using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Domain.Entities
{
    public class TxEmployeesMonthlyPayment:AuditableEntity
    {
        [Key, MaxLength(40)]
        public override string Id { get; set; } = string.Empty;
        [MaxLength(40)]
        public string EmployeeMonthlyPaymentLevelId { get; set; } = string.Empty;
        [ForeignKey("EmployeeMonthlyPaymentLevelId")]
        public TxEmployeeMonthlyPaymentLevel? EmployeeMonthlyPaymentLevel { get; set; }
        [MaxLength(40)]
        public string EmployeeId { get; set; } = string.Empty;
        [ForeignKey("EmployeeId")]
        public TxEmployee? Employee { get; set; }
        public double BasicSalary { get; set; }
        public double Allowance { get; set; }
        public double Bonus { get; set; }
        public double Deduction { get; set; }
        public EmployeeMonthlyPaymentStatus EmployeeMonthlyPaymentStatus { set; get; }
        public PaymentType PaymentType { set; get; }
        [StringLength(170)]
        public string Comments { get; set; } = string.Empty;

    }
}
