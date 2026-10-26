using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Domain.Entities
{
    public class Term
    {
        [Key]
        public required byte Id { get; set; }
        public TermStatus TermStatus { get; set; }

    }
}
