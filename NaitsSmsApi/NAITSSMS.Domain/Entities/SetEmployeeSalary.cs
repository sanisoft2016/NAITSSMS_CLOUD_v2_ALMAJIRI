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
    public class SetEmployeeSalary:AuditableEntity
    {
        [Key, MaxLength(40)]
        public override string Id { get => base.Id; set => base.Id = value; }
        public string EmployeeId { get; set; } = string.Empty;
        [ForeignKey("EmployeeId")]
        public TxEmployee? Employee { get; set; }
        public TimeStatus TimeStatus { get; set; }
        public decimal Salary { get; set; }
    }
}
