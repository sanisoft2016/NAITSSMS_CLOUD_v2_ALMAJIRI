using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class ItemsDto:AuditableEntityDto
    {
        public int Id { get; set; }
        public int WeekId { get; set; }
        public WeekDto Week { get; set; }
        public ItemCategoryDto ItemCategory { get; set; }
        public short ItemCategoryId { get; set; }
        [Display(Name= "Item' Date")]
        public DateTime CapturedDate { get; set; }
        [StringLength(150)]
        public string Details { get; set; }
        [Required]
        public double Cost { get; set; }
    }
    public class WeekDto
    {
        public int Id { get; set; }
        public TimeStatus TimeStatus { get; set; }
        public WeekOfMonth WeekOfMonth { get; set; }
        public SchoolInfoDto School { get; set; }
        public string SchoolId { get; set; }
    }
}
