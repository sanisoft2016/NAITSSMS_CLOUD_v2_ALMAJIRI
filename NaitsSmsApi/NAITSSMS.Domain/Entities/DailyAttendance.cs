using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Domain.Entities
{
    /// <summary>
    /// One row per learner per calendar day. The term-level
    /// <see cref="StudentsAtteXzndanceAndCommentsReport.Attendance"/> total is derived from these rows.
    /// </summary>
    public class LearnerDailyAttendance
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }

        /// <summary>Date only; the time component is always stripped so the unique index behaves.</summary>
        public DateTime AttendanceDate { get; set; }

        public DailyAttendanceStatus Status { get; set; }

        [StringLength(150)]
        public string Remark { get; set; } = string.Empty;

        [StringLength(40)]
        public string? MarkedByUserId { get; set; }

        public DateTime MarkedAtUtc { get; set; }

        [StringLength(40)]
        public string SchoolInfoId { get; set; } = string.Empty;
        [ForeignKey("SchoolInfoId")]
        public SchoolTyInfo? SchoolInfo { get; set; }

        [StringLength(40)]
        public string StudentId { get; set; } = string.Empty;
        [ForeignKey("StudentId")]
        public TxStudentDetail? Student { get; set; }

        [StringLength(40)]
        public string ClassArmId { get; set; } = string.Empty;
        [ForeignKey("ClassArmId")]
        public AxClassArm? ClassArm { get; set; }

        [StringLength(40)]
        public string YearlyAndTermlyStatusId { get; set; } = string.Empty;
        [ForeignKey("YearlyAndTermlyStatusId")]
        public TxYearlyAndTermlyStatus? YearlyAndTermlyStatus { get; set; }
    }

    /// <summary>
    /// One row per facilitator (teaching staff) per calendar day.
    /// </summary>
    public class FacilitatorDailyAttendance
    {
        [Key, MaxLength(40)]
        public required string Id { get; set; }

        public DateTime AttendanceDate { get; set; }

        public DailyAttendanceStatus Status { get; set; }

        [StringLength(150)]
        public string Remark { get; set; } = string.Empty;

        [StringLength(40)]
        public string? MarkedByUserId { get; set; }

        public DateTime MarkedAtUtc { get; set; }

        [StringLength(40)]
        public string SchoolInfoId { get; set; } = string.Empty;
        [ForeignKey("SchoolInfoId")]
        public SchoolTyInfo? SchoolInfo { get; set; }

        [StringLength(40)]
        public string EmployeeId { get; set; } = string.Empty;
        [ForeignKey("EmployeeId")]
        public TxEmployee? Employee { get; set; }

        [StringLength(40)]
        public string YearlyAndTermlyStatusId { get; set; } = string.Empty;
        [ForeignKey("YearlyAndTermlyStatusId")]
        public TxYearlyAndTermlyStatus? YearlyAndTermlyStatus { get; set; }
    }
}
