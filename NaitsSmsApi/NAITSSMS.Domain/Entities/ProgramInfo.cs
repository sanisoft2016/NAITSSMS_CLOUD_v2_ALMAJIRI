using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Domain.Entities
{
    public class ProgramYInfo
    {
        public byte Id { get; set; }
        [StringLength(150)]
        public string Machines { get; set; } = string.Empty;
        [StringLength(70)]
        public string Matches { get; set; } = string.Empty;
    }

    public class SystemsInfo
    {
        public string Id { get; set; } = string.Empty;
        [StringLength(40)]
        public string SchoolId { get; set; } = string.Empty;
        public TimeStatus TimeStatus { get; set; }
        public string RegDate { get; set; } = string.Empty;
        public string ExpiryDate { get; set; } = string.Empty;
        public string TemLicenseId { get; set; } = string.Empty;

    }
}
