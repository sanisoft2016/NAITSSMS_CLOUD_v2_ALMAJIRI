using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Domain.Entities
{
   public class SchoolHeadTx
   {
        [Key, MaxLength(40)]
        public required string Id { get; set; }

        [StringLength(40)]
        public string HeadsName { get; set; } = string.Empty;
        [StringLength(15)]
        public string PhoneNumber { get; set; } = string.Empty;
        [StringLength(40)]
        public string SignatureImageString { get; set; } = string.Empty;
        public TimeStatus Status { get; set; }
        [StringLength(40)]
        public string SchoolInfoId { get; set; } = string.Empty;
        [ForeignKey("SchoolInfoId")]
        public SchoolTyInfo? SchoolInfo { get; set; }
        public byte SchoolHeadCategoryId { get; set; }
        [Required]
        public Gender Gender { get; set; }

        [ForeignKey("SchoolHeadCategoryId")]
        public SchoolHeadTxCategory? SchoolHeadCategory { set; get; }
    }

    public class SchoolHeadTxCategory
    {
        [Key]
        public required byte Id { get; set; }
        public HeadStatus HeadStatus { set; get; }
    }
}
