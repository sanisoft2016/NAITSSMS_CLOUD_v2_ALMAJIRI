using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class ItemCategoryDto
    {
        public short Id { get; set; }
        public string SchoolId { get; set; }
        public SchoolInfoDto School { get; set; }
        [Display(Name = "Category Name")]
        [StringLength(50)]
        public string CategoryName { get; set; }
    }
}
