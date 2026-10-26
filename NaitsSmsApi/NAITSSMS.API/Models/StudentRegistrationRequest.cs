using Microsoft.AspNetCore.Http;
using NAITSSMS.Domain.Enum;
using System;
using System.ComponentModel.DataAnnotations;

namespace NAITSSMS.API.Models;

public sealed class StudentRegistrationRequest
{
    [Required]
    public string ClassId { get; set; } = string.Empty;

    [Required]
    [StringLength(40)]
    public string ClassLabel { get; set; } = string.Empty;

    [Required]
    public string ArmId { get; set; } = string.Empty;

    [StringLength(40)]
    public string? ClassArmId { get; set; }

    [Required]
    public ArmName ArmName { get; set; }

    [Required]
    public CurrentClass CurrentClass { get; set; }

    public short SNo { get; set; }

    [Required]
    [StringLength(15)]
    public string IdNo { get; set; } = string.Empty;

    [Required]
    [StringLength(40)]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(40)]
    public string? MiddleName { get; set; }

    [Required]
    [StringLength(40)]
    public string SurName { get; set; } = string.Empty;

    [Required]
    public DateTime DateOfBirth { get; set; }

    [StringLength(50)]
    public string? PlaceOfBirth { get; set; }

    [Required]
    public Religion Religion { get; set; }

    [Required]
    public Gender Gender { get; set; }

    [StringLength(10)]
    public string? BloodGroup { get; set; }

    [Required]
    public DateTime DateOfAdmsn { get; set; }

    [StringLength(150)]
    public string? TransferedFromWhere { get; set; }

    [StringLength(50)]
    public string? GuardiansName { get; set; }

    [StringLength(50)]
    public string? Relationship { get; set; }

    [StringLength(200)]
    public string? GuardianAddress { get; set; }

    [StringLength(50)]
    public string? Occupation { get; set; }

    [StringLength(50)]
    public string? MobileNumber { get; set; }

    [StringLength(50)]
    public string? HomeNumber { get; set; }

    [StringLength(100)]
    public string? EmailAddress { get; set; }

    [StringLength(50)]
    public string? StudentMobileNo { get; set; }

    [StringLength(50)]
    public string? Country { get; set; }

    [Required]
    public ClassCategoryStatus ClassCategory { get; set; }

    [Required]
    public StudentStatus Status { get; set; }

    public IFormFile? Passport { get; set; }
}