using System.ComponentModel.DataAnnotations;

namespace NAITSSMS.Application.DTOs;

public class ContactUsDto
{
    [Required]
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(160)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [MaxLength(160)]
    public string? School { get; set; }

    [MaxLength(160)]
    public string? Subject { get; set; }

    [Required]
    [MinLength(10)]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;
}

public sealed class ContactEnquiryResult
{
    public string Status { get; set; } = "Failure";
    public string Message { get; set; } = string.Empty;
}
