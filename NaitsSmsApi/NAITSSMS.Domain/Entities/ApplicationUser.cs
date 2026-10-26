using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using NAITSSMS.Domain.Enum;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NAITSSMS.Domain.Entities
{
    public class ApplicationUser : IdentityUser
    {
        [StringLength(40)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public override required string Id { get; set; }

        [StringLength(50)]
        public override string? UserName { get; set; } = string.Empty;

        [StringLength(50)]
        public override string? Email { get; set; } = string.Empty;

        [StringLength(13)]
        public override string? PhoneNumber { get; set; }

        [StringLength(150)]
        public override string? PasswordHash { get; set; } = string.Empty;

        [StringLength(40)]
        public override string? SecurityStamp { get; set; } = string.Empty;

        [StringLength(20)]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(20)]
        public string MiddleName { get; set; } = string.Empty;

        [StringLength(20)]
        public string SurName { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }

        public string FullName => string.Format("{0} {1} {2}", FirstName, MiddleName, SurName);

        public Gender Gender { get; set; }
        public AccessStatus AccessStatus { get; set; }
        public UserStatus UserStatus { get; set; }
    }
}