using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Domain.Entities
{
    public class TxEmployeeMonthlyPaymentLevel
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        [StringLength(40)] public string MonthId { get; set; } = string.Empty;
        [ForeignKey("MonthId")]
        public TxMonth? Month { set; get; }
        public EmployeeMonthlyPaymentLevel EmployeeMonthlyPaymentLevel { set; get; }
    }
}
