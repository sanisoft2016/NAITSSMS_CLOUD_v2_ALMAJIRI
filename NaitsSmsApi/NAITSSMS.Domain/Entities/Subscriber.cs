using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Enum;
using System;
using System.ComponentModel.DataAnnotations;

namespace NAITSSMS.Domain.Entities
{
    public class Subscriber
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }

        public Subscriber()
        {
            Id = Guid.NewGuid().ToString();
            Email = string.Empty;
        }
        [MaxLength(50)]
        public string Email { get; set; } = string.Empty;
        public Gender? Gender { get; set; }
    }
}
