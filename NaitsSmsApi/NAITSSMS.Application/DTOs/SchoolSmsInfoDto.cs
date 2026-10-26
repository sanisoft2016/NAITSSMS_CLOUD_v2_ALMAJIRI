using NAITSSMS.Domain.Enum;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NAITSSMS.Application.DTOs
{
    public class SchoolSmsInfoDto
    {
        public string Id { get; set; }
        public string AgentUserId { get; set; }
        [ForeignKey("AgentUserId")]
        public UserDto AgentUser { get; set; }
        [ForeignKey("SchoolUserId")]
        [StringLength(40)]
        [Required]
        public string SchoolUserId { get; set; }
        [ForeignKey("SchoolUserId")]
        public UserDto SchoolUser { get; set; }
        [StringLength(70), Required]
        public string UserName { get; set; }
        [Display(Name = "Email Address")]
        [DataType(DataType.EmailAddress), StringLength(70), Required]
        public string Email { get; set; }

        [StringLength(15), Required]
        public string PhoneNumber { get; set; }
        public Gender Gender { get; set; }

        [StringLength(70), Required]
        public string SmsAutToken { get; set; }
        [StringLength(150), Required]
        public string SmsAutKey { get; set; }
        [StringLength(70), Required]
        public string SchoolName { get; set; }
        public TimeStatus TimeStatus { get; set; }
        [StringLength(150)]
        public string SchoolAddress { get; set; }
        [StringLength(70)]
        public string Website { get; set; }
        [StringLength(40)]
        public string SchoolLogoString { get; set; }
        [StringLength(70)]
        public SchoolAdminAccountantStatus SchoolAdminAccountantStatus { get; set; }//For Super Admin Consumption only
        public string Motto { get; set; }
        [Required]
        [StringLength(40, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        [StringLength(40)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }
        [StringLength(40)]
        [Display(Name = "Middle Name")]
        public string MiddleName { get; set; }
        public UserStatus UserStatus { get; set; }
        [StringLength(40)]
        [Display(Name = "Sur Name")]
        public string SurName { get; set; }
        [Display(Name = "Full Name")]
        public string FullName { get { return string.Format("{0} {1} {2}", FirstName, MiddleName, SurName); } }
        public AccessStatus AccessStatus { get; set; }
        public float PricePerSms { get; set; }
        public float SmsBalance { get; set; }
        public string FeatureImplementationType { get; set; }
        [StringLength(170)]
        public string TotalStudents { get; set; }
        public string NetworkImplementationType { get; set; }

    }
}