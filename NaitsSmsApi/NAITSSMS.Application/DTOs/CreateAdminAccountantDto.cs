using NAITSSMS.Domain.Enum;
using System;
using System.ComponentModel.DataAnnotations;

namespace NAITSSMS.Application.DTOs
{
    /// <summary>
    /// Payload for creating the SuperAccountant employee record after Identity user creation
    /// (legacy RegistrationController.CreateAdminAccountant → CreateClassTeacher SuperAccountant branch).
    /// </summary>
    public class CreateAdminAccountantEmployeeDto
    {
        [Required]
        public string SchoolId { get; set; } = string.Empty;
        [Required]
        public string UserId { get; set; } = string.Empty;
        [Required]
        public string UserName { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public string? SignatureImageString { get; set; }
        public string? FullAddress { get; set; }
        public string? EmployeeIdNo { get; set; }
    }

    public class CreateAdminAccountantRequestDto
    {
        [Required, StringLength(40)]
        public string FirstName { get; set; } = string.Empty;
        [StringLength(40)]
        public string? MiddleName { get; set; }
        [Required, StringLength(40)]
        public string SurName { get; set; } = string.Empty;
        [StringLength(50), DataType(DataType.EmailAddress)]
        public string? Email { get; set; }
        [StringLength(15)]
        public string? PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public Gender Gender { get; set; }
        /// <summary>Optional data-URL or saved file name for signature.</summary>
        public string? SignatureImageString { get; set; }
    }

    /// <summary>
    /// Payload for creating a Subordinate Accountant (legacy RegistrationController.CreateAdminAccountant,
    /// Accounting/SuperAccountant branch). Differs from the Admin Accountant in that the creator supplies
    /// the login <see cref="UserName"/> instead of it being auto-generated.
    /// </summary>
    public class CreateSubordinateAccountantRequestDto
    {
        [Required, StringLength(40)]
        public string FirstName { get; set; } = string.Empty;
        [StringLength(40)]
        public string? MiddleName { get; set; }
        [Required, StringLength(40)]
        public string SurName { get; set; } = string.Empty;
        [Required, StringLength(40)]
        public string UserName { get; set; } = string.Empty;
        [StringLength(50), DataType(DataType.EmailAddress)]
        public string? Email { get; set; }
        [StringLength(15)]
        public string? PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public Gender Gender { get; set; }
        /// <summary>Optional data-URL or saved file name for signature.</summary>
        public string? SignatureImageString { get; set; }
    }

    public class UpdateAdminAccountantDto
    {
        [Required]
        public string SchoolRegId { get; set; } = string.Empty;
        [Required]
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        [Required]
        public string SurName { get; set; } = string.Empty;
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }
    }

    public class CreateAdminAccountantResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// term_not_ready | identity_failed | role_failed | employee_failed | ok
        /// </summary>
        public string Code { get; set; } = string.Empty;
        public string? RedirectPath { get; set; }
    }
}
