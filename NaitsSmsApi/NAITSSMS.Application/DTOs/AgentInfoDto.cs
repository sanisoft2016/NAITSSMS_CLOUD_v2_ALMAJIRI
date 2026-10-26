using NAITSSMS.Domain.Enum;
using System;
using System.ComponentModel.DataAnnotations;

namespace NAITSSMS.Application.DTOs
{
    public class AgentInfoDto
    {
        [StringLength(70), Required]
        public string UserName { get; set; }
        [Display(Name = "Email Address")]
        [Required]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }

        [StringLength(15), Required]
        public string PhoneNumber { get; set; }
        [Required]
        public Gender Gender { get; set; }
        public TimeStatus TimeStatus { get; set; }

        //[Required]
        [StringLength(40)]
        public string UserId { get; set; }
        //[ForeignKey("UserId")]
        //public ApplicationUser User { get; set; }

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

        [StringLength(40)]
        [Display(Name = "Sur Name")]
        public string SurName { get; set; }
        [Display(Name = "Full Name")]
        public string FullName { get { return string.Format("{0} {1} {2}", FirstName, MiddleName, SurName); } }
        public AccessStatus AccessStatus { get; set; }
        [StringLength(100)]
        public string FullAddress { get; set; }
        public AgentInfoDto()
        {
            UserId = Guid.NewGuid().ToString();
        }
    }
}