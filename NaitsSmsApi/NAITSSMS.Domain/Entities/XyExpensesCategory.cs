using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAITSSMS.Domain.Base;

namespace NAITSSMS.Domain.Entities
{
   public class XyExpensesCategory
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        [StringLength(40)]
        public string SchoolId { get; set; } = string.Empty;
        [ForeignKey("SchoolId")]
        public SchoolTyInfo? School { get; set; }
        [StringLength(50)]
        public string CategoryName { get; set; } = string.Empty;

    }
}
