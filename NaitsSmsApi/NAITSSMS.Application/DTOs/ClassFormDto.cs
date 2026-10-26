using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class ClassFormDto
    {
        public string Id { get; set; } = string.Empty;
        public byte SNo { get; set; }
        [Display(Name = "Class's Name")]
        [Required]
        public CurrentClass ClassName { get; set; }
        [Display(Name = "Class Label")]
        [StringLength(40)]
        public string ClassLabel { get; set; } = string.Empty;
        [StringLength(40)]
        public string SchoolInfoId { get; set; } = string.Empty;
    }
}
