using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class TermDto
    {
        public byte Id { get; set; }
        public TermStatus TermStatus { get; set; }
    }
}
