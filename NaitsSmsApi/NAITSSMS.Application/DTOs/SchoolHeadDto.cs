using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class SchoolHeadDto
    {
        public string Id { set; get; }

        [Required]
        [StringLength(40)]
        [Display(Name = "Head's Name")]
        public string HeadsName { get; set; }
        [StringLength(15)]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }
        [StringLength(40)]
        [Display(Name = "Upload Signature Image")]
        public string SignatureImageString { get; set; }
        public TimeStatus Status { get; set; }
        public string SchoolInfoId { get; set; }
        public SchoolInfoDto SchoolInfo { get; set; }
        public byte SchoolHeadCategoryId { get; set; }
        public HeadStatus HeadStatus { set; get; }
        [Required]
        public Gender Gender { get; set; }
        public SchoolHeadCategoryDto SchoolHeadCategory { set; get; }
    }

    public class SchoolHeadCategoryDto
    {
        public byte Id { set; get; }
        public HeadStatus HeadStatus { set; get; }

    }
}
