using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Domain.Entities
{
    public class TxStudentDetail
    {
        [NotMapped]
        [StringLength(40)]
        public string Id
        {
            get => UserId;
            set => UserId = value;
        }

        public short SNo { get; set; }
        [Display(Name = "Identification No")]
        [StringLength(15)]
        public string IdNo { get; set; } = string.Empty;

        [Display(Name = "Date Of Birth")]
        public DateTime DateOfBirth { get; set; }
        [Display(Name = "Place Of Birth")]
        [StringLength(50)]
        public string PlaceOfBirth { get; set; } = string.Empty;
        public Religion Religion { get; set; }
        //public Gender Gender { get; set; }
        [Display(Name = "Blood Group")]
        [StringLength(10)]
        public string BloodGroup { get; set; } = string.Empty;

        [Display(Name = "Date Of Admission")]
        public DateTime DateOfAdmsn { get; set; }
        public DateTime? DateOfDischarge { get; set; }
        [StringLength(150)]
        [Display(Name = "Transfered From Where")]
        public string TransferedFromWhere { get; set; } = string.Empty;
        [Display(Name = "Start Class")]
        [StringLength(40)]
        public string StartClass { get; set; } = string.Empty;
        [Display(Name = "Current Class")]
        public CurrentClass CurrentClass { get; set; }
        [StringLength(40)]
        public string Passport { get; set; } = string.Empty;

        [StringLength(40)] 
        public string ClassArmId { get; set; } = string.Empty;
        [ForeignKey("ClassArmId")]
        public AxClassArm? ClassArm { get; set; }
        [StringLength(50)]
        [Display(Name = "Guardian's Name")]
        public string GuardiansName { get; set; } = string.Empty;
        [StringLength(50)]
        public string Relationship { get; set; } = string.Empty;
        [StringLength(200)]
        [Display(Name = "Parent/Guardian Address")]
        public string GuardianAddress { get; set; } = string.Empty;
        [StringLength(50)]
        public string Occupation { get; set; } = string.Empty;
        [Display(Name = "Contact Mobile No")]
        [StringLength(50)]
        public string MobileNumber { get; set; } = string.Empty;
        //public NetworkType NetworkType { get; set; }

        [StringLength(50)]
        [Display(Name = "Contact Home No")]
        public string HomeNumber { get; set; } = string.Empty;
        [StringLength(100)]
        [Display(Name = "Contact Email")]
        [DataType(DataType.EmailAddress)]
        public string EmailAddress { get; set; } = string.Empty;
        [StringLength(150)]
        [Display(Name = "Mailing Address")]
        public string MailingAddress { get; set; } = string.Empty;
        [StringLength(50)]
        public string Country { get; set; } = string.Empty;
        [Display(Name = "Current or Previous")]
        public TimeStatus TimeStatus { get; set; }
        [Key]
        [Required]
        [StringLength(40)]
        public string UserId { get; set; } = string.Empty;
        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }
        [StringLength(40)]
        public string SchoolInfoId { get; set; } = string.Empty;
        [ForeignKey("SchoolInfoId")]
        public SchoolTyInfo? SchoolInfo { get; set; }
        public ClassCategoryStatus ClassCategory { get; set; }
        public StudentStatus Status { get; set; }
        public StudentInitialRegistrationStatus StudentInitialRegStatus { get; set; }
        [ForeignKey("AdmisnTermId")]
        public TxYearlyAndTermlyStatus? YearlyAndTermlyStatus { get; set; }
        [StringLength(40)]
        public string AdmisnTermId { get; set; } = string.Empty;

    }
}
