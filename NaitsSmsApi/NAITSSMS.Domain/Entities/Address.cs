using NAITSSMS.Domain.Enum;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NAITSSMS.Domain.Entities
{
    public sealed class Address
    {
        public Address()
        {
            Id = Guid.NewGuid().ToString();
            UserId = string.Empty;
            City = string.Empty;
            StreetLine1 = string.Empty;
            StreetLine2 = string.Empty;
            User = null!;
        }
        [Key, MaxLength(40)]
        public required string Id { get; set; }

        [ForeignKey("User")]
        public string UserId { get; set; }

        public string City { get; set; }
        public string StreetLine1 { get; set; }
        public string StreetLine2 { get; set; }
        public int StateId { get; set; }
        public int LgaId { get; set; }
        public AddressType Type { get; set; }
        public ApplicationUser User { get; set; }
    }
}