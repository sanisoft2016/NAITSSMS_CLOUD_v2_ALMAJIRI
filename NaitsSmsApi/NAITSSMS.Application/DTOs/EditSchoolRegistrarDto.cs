using NAITSSMS.Domain.Enum;
using System;
using System.ComponentModel.DataAnnotations;

namespace NAITSSMS.Application.DTOs
{
    /// <summary>
    /// Legacy SupperAdmin EditSchoolRegistrar form model (UserDto fields used by GET/POST).
    /// </summary>
    public class EditSchoolRegistrarDto
    {
        [StringLength(40)]
        public string Id { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "Middle Name")]
        public string MiddleName { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "Sur Name")]
        public string SurName { get; set; } = string.Empty;

        [StringLength(30)]
        [Display(Name = "User Name")]
        public string UserName { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Email Address")]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; } = string.Empty;

        public Gender Gender { get; set; }

        [StringLength(15)]
        [Display(Name = "Phone No.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(70)]
        [Display(Name = "School Name")]
        public string SchoolName { get; set; } = string.Empty;

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [StringLength(150)]
        public string Addresses { get; set; } = string.Empty;

        [StringLength(40)]
        public string SignatureImageString { get; set; } = string.Empty;

        public TimeStatus TimeStatus { get; set; }
        public AccessStatus AccessStatus { get; set; }
        public UserStatus UserStatus { get; set; }

        [StringLength(150)]
        [Display(Name = "Sms Api Key")]
        public string? SmsApiKey { get; set; }

        [StringLength(100)]
        [Display(Name = "Sms Api Token")]
        public string? SmsApiToken { get; set; }

        [StringLength(300)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        public string? ConfirmPassword { get; set; }
    }

    public class UpdateSchoolRegistrarResultDto
    {
        public byte Status { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? RedirectPath { get; set; }
    }
}
