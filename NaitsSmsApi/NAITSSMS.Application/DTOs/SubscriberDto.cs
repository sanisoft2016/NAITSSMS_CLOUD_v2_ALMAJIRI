using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class SubscriberDto
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public Gender Gender { get; set; }
    }
}
