using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Domain.Entities
{
   public class TxEmployeeIncomeStructure
   {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        public TxEmployee? Employee { get; set; }
        public double BasicSalary { get; set; }
        public double Allowance { get; set; }
        
    }
}
