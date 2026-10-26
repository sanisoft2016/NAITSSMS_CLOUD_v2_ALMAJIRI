using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class AddressDto
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string Username { get; set; }
        public string City { get; set; }
        public string StreetLine1 { get; set; }
        public string StreetLine2 { get; set; }
        public int StateId { get; set; }
        public int LgaId { get; set; }
        public AddressType Type { get; set; }
    }
}
