using NAITSSMS.Domain.Enum;
using System;
using System.ComponentModel.DataAnnotations;

namespace NAITSSMS.Application.DTOs
{
    public class UserSmsDto
    {
        public string Id { get; set; }
        [StringLength(70), Required]
        public string UserName { get; set; }
        [Display(Name = "Email Address")]
        [Required]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }

        [StringLength(15), Required]
        public string PhoneNumber { get; set; }
        [Required]
        [StringLength(40)]
        public string FirstName { get; set; }
        [StringLength(40)]
        public string MiddleName { get; set; }
        [StringLength(40)]
        public string SurName { get; set; }
        public DateTime CreatedDate { get; set; }
        public string FullName { get { return string.Format("{0} {1} {2}", FirstName, MiddleName, SurName); } }
        public Gender Gender { get; set; }
        public AccessStatus AccessStatus { get; set; }
        public TimeStatus TimeStatus { get; set; }
        public UserStatus UserStatus { get; set; }
    }
}