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
    public class OptionalXyChargeItems:AuditableEntity
    {
        [Key, MaxLength(40)]
        public override string Id { get => base.Id; set => base.Id = value; }

        [ForeignKey("ItemCategoryId")]
        public XyOPtionalChargeCategory? ItemCategory { get; set; }
        public string ItemCategoryId { get; set; }

        [StringLength(30)]
        public string ItemName { get; set; } = string.Empty;
        public double Cost { get; set; }
        public string YearlyAndTermlyStatusId { get; set; } = string.Empty;
        [ForeignKey("YearlyAndTermlyStatusId")]
        public TxYearlyAndTermlyStatus? YearlyAndTermlyStatus { get; set; }
    }
}
