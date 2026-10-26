using System.ComponentModel.DataAnnotations;

namespace NAITSSMS.API.Models;

public sealed class TeacherRegistrationRequest
{
    [StringLength(20)]
    public string EmployeeIdNo { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(20)]
    public string MiddleName { get; set; } = string.Empty;

    [StringLength(20)]
    public string SurName { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string UserName { get; set; } = string.Empty;

    [StringLength(30)]
    [DataType(DataType.EmailAddress)]
    public string Email { get; set; } = string.Empty;

    [StringLength(15)]
    public string PhoneNumber { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }

    [Required]
    public string Gender { get; set; } = string.Empty;

    [Required]
    public string EmployeeStatus { get; set; } = string.Empty;

    public string SignatureImageString { get; set; } = string.Empty;
}