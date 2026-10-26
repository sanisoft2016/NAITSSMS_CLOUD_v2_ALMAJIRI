using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
   public class OptionalChargeItemsDto: AuditableEntityDto//CreatedDate
    {
        public short Id { get; set; }
        public OPtionalChargeCategoryDto ItemCategory { get; set; }
        public short ItemCategoryId { get; set; }
        [StringLength(30)]
        [Display(Name = "Item Name")]
        public string ItemName { get; set; }
        public double Cost { get; set; }
        public int YearlyAndTermlyStatusId { get; set; }
        public YearlyAndTermlyStatusDto YearlyAndTermlyStatus { get; set; }
    }
}
