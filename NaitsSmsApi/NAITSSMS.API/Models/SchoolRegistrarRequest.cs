using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.API.Models;

public sealed class SchoolRegistrarRequest
{
    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    public string? SurName { get; set; }

    [Required]
    public Gender Gender { get; set; }

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string SchoolName { get; set; } = string.Empty;

    public string? SmsApiKey { get; set; }

    public string? SmsApiToken { get; set; }

    public IFormFile? Signature { get; set; }
}
