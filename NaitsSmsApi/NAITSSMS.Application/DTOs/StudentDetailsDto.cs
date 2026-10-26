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
    public class StudentDetailsDto
    {
        public string Id { get; set; }
        public short SNo { get; set; }
        [Display(Name = "Admission No.")]
        [Required]
        [StringLength(15)]
        public string IdNo { get; set; }
        [Required]
        [StringLength(40)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }
        [StringLength(40)]
        [Display(Name = "Middle Name")]
        public string MiddleName { get; set; }
        [Required]
        [StringLength(40)]
        [Display(Name = "Sur Name")]
        public string SurName { get; set; }
        public string FullName { get { return string.Format("{0} {1} {2}", FirstName, MiddleName, SurName); } }

        //[Required]
        [Display(Name = "Date Of Birth")]
        public DateTime DateOfBirth { get; set; }
        [StringLength(50)]
        [Display(Name = "Place Of Birth")]
        public string PlaceOfBirth { get; set; }
        public Religion Religion { get; set; }
        [Required]
        public Gender Gender { get; set; }
        [Display(Name = "Blood Group")]
        [StringLength(10)]
        public string BloodGroup { get; set; }
        //[Required]
        [Display(Name = "Date Of Admission")]
        public DateTime DateOfAdmsn { get; set; }
        public DateTime? DateOfDischarge { get; set; }
        [StringLength(150)]
        [Display(Name = "Transfered From Where")]
        public string TransferedFromWhere { get; set; }
        //[Required]
        [Display(Name = "Start Class")]
        [StringLength(40)]
        public string StartClass { get; set; }
        [Required]
        [Display(Name = "Current Class")]
        public CurrentClass CurrentClass { get; set; }
        //[Required]
        [StringLength(40)]
        public string Passport { get; set; }

        public string ClassArmId { get; set; }
        [ForeignKey("ClassArmId")]
        public ClassArmDto ClassArm { get; set; }

        //[Required]
        [StringLength(50)]
        [Display(Name = "Guardian's Name")]
        public string GuardiansName { get; set; }
        [StringLength(50)]
        public string Relationship { get; set; }
        [StringLength(200)]
        //[Required]
        [Display(Name = "Address")]
        public string GuardianAddress { get; set; }
        //[Required]
        [StringLength(50)]
        public string Occupation { get; set; }
        [Display(Name = "Contact Mobile No")]
        //[Required]
        [StringLength(50)]
        public string MobileNumber { get; set; }
        //public NetworkType NetworkType { get; set; }
        [StringLength(50)]
        [Display(Name = "Contact Home No")]
        public string HomeNumber { get; set; }
        //[Required]
        [StringLength(100)]
        [Display(Name = "Contact Email")]
        [DataType(DataType.EmailAddress)]
        public string EmailAddress { get; set; }
        [StringLength(150)]
        [Display(Name = "Mailing Address")]
        public string MailingAddress { get; set; }
        [StringLength(50)]
        [Display(Name = "Student Mobile No")]
        public string StudentMobileNo { get; set; }
        
        [StringLength(50)]
        public string Country { get; set; }
        //[Display(Name = "Time Status")]
        //public TimeStatus TimeStatus { get; set; }
        public string UserId { get; set; }
        public ApplicationUserDto User { get; set; }

        public string SchoolInfoId { get; set; }
        public SchoolInfoDto SchoolInfo { get; set; }
        [Display(Name = "Class Status")]
        public ClassCategoryStatus ClassCategory { get; set; }
        [Display(Name = "Student Status")]
        public StudentStatus Status { get; set; }
        public StudentInitialRegistrationStatus StudentInitialRegStatus { get; set; }
        public YearlyAndTermlyStatusDto YearlyAndTermlyStatus { get; set; }

        public string AdmisnTermId { get; set; }
    }
}
