using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Domain.Entities
{

    public class RxSubject
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        [Required]
        [StringLength(50)]
        [Display(Name = "Subject's Name")]
        public string SubjectName { get; set; } = string.Empty;
        public TimeStatus TimeStatus { get; set; }
        [ForeignKey("SubjectCategoryId")]
        public SubjectRxCategories? SubjectCategory { get; set; }
        [StringLength(40)] 
        public string SubjectCategoryId { get; set; } = string.Empty;
    }

    public class SubjectRxCategories
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        public SubjectStatus SubjectStatus { get; set; }
        [StringLength(50)]
        public string? SubjectStatusLabel { get; set; }
        [StringLength(40)]
        public string SchoolId { get; set; }
        [ForeignKey("SchoolId")]
        public SchoolTyInfo? SchoolInfo { get; set; }
    }

    //public class RxSubject: Entity
    //{
    //    //public short SNo { get; set; }
    //    [StringLength(35)]
    //    [Required]
    //    [Display(Name = "Subject's Name")]
    //    public string SubjectName { get; set; }
    //    public SubjectStatus SubjectStatus { get; set; }
    //    public TimeStatus TimeStatus { get; set; }
    //    public string SchoolId { get; set; }//Teacher
    //    [ForeignKey("SchoolId")]
    //    public SchoolTyInfo SchoolInfo { get; set; }
    //}
}
