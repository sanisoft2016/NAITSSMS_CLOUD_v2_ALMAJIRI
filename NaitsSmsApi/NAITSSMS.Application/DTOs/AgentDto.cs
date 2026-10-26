using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
   public class AgentDto
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public UserDto User { get; set; }
        public TimeStatus TimeStatus { get; set; }

        public AgentDto()
        {
            Id = Guid.NewGuid().ToString();
        }
    }
}
