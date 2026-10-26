using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{

    public class UserDto2nd
    {
        public string Id { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }
        [StringLength(20)]
        [Display(Name = "Middle Name")]
        public string MiddleName { get; set; }

        //[Required]
        [StringLength(20)]
        [Display(Name = "Sur Name")]
        public string SurName { get; set; }
        [Display(Name = "Full Name")]
        public string FullName { get { return string.Format("{0} {1} {2}", FirstName, MiddleName, SurName); } }
        [Required]
        [Display(Name = "User Name")]
        [StringLength(30)]
        public string UserName { get; set; }
        [Display(Name = "Email Address")]
        //[Required]
        [StringLength(30), DataType(DataType.EmailAddress)]
        public string Email { get; set; }

        [Required]
        public Gender Gender { get; set; }
        [Display(Name = "Employee Status")]
        [Required]
        public EmployeeStatus EmployeeStatus { get; set; }

        [Required]
        [StringLength(25, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
        [Display(Name = "School Name")]
        [StringLength(70)]
        public string SchoolName { get; set; }
        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; }
        [StringLength(15)]
        [Display(Name = "Phone No.")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }
        [StringLength(40)]
        public string UserId { get; set; }
        [StringLength(150)]
        public string Addresses { get; set; }
        [StringLength(40)]
        [Display(Name = "Signature Image Upload")]
        public string SignatureImageString { get; set; }

        [StringLength(7)]
        public string SchoolRegId { get; set; }
        public AccessStatus AccessStatus { get; set; }
        [StringLength(20)]
        [Display(Name = "EmployeeId No")]
        public string EmployeeIdNo { get; set; }

        public TimeStatus TimeStatus { get; set; }
        public UserStatus UserStatus { get; set; }

        [StringLength(150)]
        public string SmsApiKey { get; set; }
        [StringLength(100)]
        public string SmsApiToken { get; set; }
    }
}
