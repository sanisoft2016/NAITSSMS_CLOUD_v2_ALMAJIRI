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
    public class SubjectDto
    {
        public string? Id { get; set; }
        [Required]
        [StringLength(50)]
        [Display(Name = "Subject's Name")]
        public string SubjectName { get; set; } = string.Empty;
        public TimeStatus TimeStatus { get; set; }
        public SubjectCategoriesDto? SubjectCategory { get; set; }
        public string? SubjectCategoryId { get; set; }
    }

    public class SubjectCategoriesDto
    {
        public short Id { get; set; }
        public SubjectStatus SubjectStatus { get; set; }
        [StringLength(50)]
        public string SubjectStatusLabel { get; set; }
        public string SchoolId { get; set; }
        public SchoolInfoDto SchoolInfo { get; set; }
    }
}
