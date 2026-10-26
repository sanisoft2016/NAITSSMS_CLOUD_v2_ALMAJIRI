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
    public class XyIncome : AuditableEntity
    {
        [Key, MaxLength(40)]
        public override string Id { get => base.Id; set => base.Id = value; }
        public string WeekId { get; set; } = string.Empty;
        [ForeignKey("WeekId")]
        public XyIncomeWeek? Week { get; set; }
        [ForeignKey("ItemCategoryId")]
        public XyIncomeCategory? ItemCategory { get; set; }
        public string ItemCategoryId { get; set; } = string.Empty;
        public DateTime CapturedDate { get; set; }

        [StringLength(150)]
        public string Details { get; set; } = string.Empty;
        public double Cost { get; set; }
    }

    public class XyIncomeWeek
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        public TimeStatus TimeStatus { get; set; }
        public WeekOfMonth WeekOfMonth { get; set; }
        [ForeignKey("SchoolId")]
        public SchoolTyInfo? School { get; set; }
        [StringLength(40)]
        public string SchoolId { get; set; } = string.Empty;

    }
}
