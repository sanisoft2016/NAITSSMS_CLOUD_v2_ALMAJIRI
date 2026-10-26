using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAITSSMS.Domain.Base;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Domain.Entities
{
    public class TxYearlyAndTermlyStatus
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public TermlyAccessmentStatus TermlyAccessmentStatus { get; set; }
        public TermStatus TermStatus { get; set; }
        public CasCategory CasCategory { get; set; }
        public byte? PerCasValue { get; set; }
        public byte? MinPassMark { get; set; }
        public DateTime? NextTermBeginsDate { get; set; }
        public DateTime? AssessmtEndDate { get; set; }
        public byte? TotalAttendance { get; set; }
        [StringLength(40)] public string SessionId { get; set; } = string.Empty;
        [ForeignKey("SessionId")]
        public TxAcademicSession? Session { get; set; }
        [StringLength(40)] public string? HeadmasterId { get; set; }
        [StringLength(40)] public string? PrincipalId { get; set; }
    }

    public class TxAcademicSession
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }

        [StringLength(9)]
        public string Session { get; set; } = string.Empty; //2020/2021
        public TimeStatus Status { get; set; }
        [StringLength(40)]
        public string SchoolInfoId { get; set; } = string.Empty;
        [ForeignKey("SchoolInfoId")]
        public SchoolTyInfo? SchoolInfo { get; set; }
    }
}
