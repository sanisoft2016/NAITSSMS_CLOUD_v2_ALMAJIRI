using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
   public class YearlyAndTermlyStatusDto
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public TermlyAccessmentStatus TermlyAccessmentStatus { get; set; }
        public TermStatus TermStatus { get; set; }
        public CasCategory CasCategory { get; set; }
        public byte? PerCasValue { get; set; }
        public byte? MinPassMark { get; set; }
        public DateTime? NextTermBeginsDate { get; set; }
        public DateTime? AssessmtEndDate { get; set; }
        public byte? TotalAttendance { get; set; }
        public short SessionId { get; set; }
        public AcademicSessionDto Session { get; set; }
        public string SchoolInfoId { get; set; }
        public SchoolInfoDto SchoolInfo { get; set; }
        public short? HeadmasterId { get; set; }
        public short? PrincipalId { get; set; }
        public SystemImplementationType SystemImplementationType { get; set; }
    }

    public class AcademicSessionDto
    {
        public string Id { set; get; } = string.Empty;
        [Required]
        [StringLength(9)]
        public string Session { get; set; } = string.Empty;
        public string SchoolInfoId { get; set; } = string.Empty;
        public SchoolInfoDto? SchoolInfo { get; set; }
        public TimeStatus Status { get; set; }
    }
}
