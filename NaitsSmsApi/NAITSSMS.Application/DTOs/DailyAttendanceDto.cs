using System;
using System.Collections.Generic;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Application.DTOs
{
    /// <summary>One roster line on a daily register.</summary>
    public sealed class DailyAttendanceRosterEntryDto
    {
        public string PersonId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string IdentificationNo { get; set; } = string.Empty;
        public string ClassArmId { get; set; } = string.Empty;
        public string ClassArmLabel { get; set; } = string.Empty;
        public DailyAttendanceStatus Status { get; set; }
        public string Remark { get; set; } = string.Empty;

        /// <summary>False when the register has not been taken for this person on this date.</summary>
        public bool AlreadyMarked { get; set; }
    }

    public sealed class DailyAttendanceRosterDto
    {
        public DateTime AttendanceDate { get; set; }
        public string ClassArmId { get; set; } = string.Empty;
        public string ClassArmLabel { get; set; } = string.Empty;
        public bool RegisterAlreadyTaken { get; set; }
        public int TotalPeople { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int LateCount { get; set; }
        public int ExcusedCount { get; set; }
        public IReadOnlyList<DailyAttendanceRosterEntryDto> Entries { get; set; } =
            Array.Empty<DailyAttendanceRosterEntryDto>();
    }

    public sealed class DailyAttendanceMarkDto
    {
        public string PersonId { get; set; } = string.Empty;
        public DailyAttendanceStatus Status { get; set; }
        public string? Remark { get; set; }
    }

    public sealed class DailyAttendanceSubmitDto
    {
        public DateTime AttendanceDate { get; set; }

        /// <summary>Required for the learner register; ignored for facilitators.</summary>
        public string? ClassArmId { get; set; }

        public List<DailyAttendanceMarkDto> Marks { get; set; } = new();
    }

    public sealed class DailyAttendanceSaveResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int Created { get; set; }
        public int Updated { get; set; }
    }

    public sealed class AttendanceArmSummaryDto
    {
        public string ClassArmId { get; set; } = string.Empty;
        public string ClassArmLabel { get; set; } = string.Empty;
        public int Learners { get; set; }
        public int DaysRegistered { get; set; }
        public int PresentMarks { get; set; }
        public int AbsentMarks { get; set; }
        public int LateMarks { get; set; }
        public int ExcusedMarks { get; set; }
        public double AttendanceRate { get; set; }
    }

    public sealed class AttendanceAtRiskLearnerDto
    {
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string IdentificationNo { get; set; } = string.Empty;
        public string ClassArmLabel { get; set; } = string.Empty;
        public int DaysRegistered { get; set; }
        public int DaysPresent { get; set; }
        public int DaysAbsent { get; set; }
        public double AttendanceRate { get; set; }
        public int LongestAbsenceStreak { get; set; }
        public DateTime? LastPresentDate { get; set; }

        /// <summary>Plain-language reason shown to the user, e.g. "5 consecutive days absent".</summary>
        public string RiskReason { get; set; } = string.Empty;
    }

    public sealed class AttendanceDayPointDto
    {
        public DateTime Date { get; set; }
        public int Present { get; set; }
        public int Absent { get; set; }
        public double AttendanceRate { get; set; }
    }

    public sealed class AttendanceFacilitatorSummaryDto
    {
        public string EmployeeId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string StaffNo { get; set; } = string.Empty;
        public int DaysRegistered { get; set; }
        public int DaysPresent { get; set; }
        public int DaysAbsent { get; set; }
        public double AttendanceRate { get; set; }
    }

    public sealed class DailyAttendanceSummaryDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string SchoolName { get; set; } = string.Empty;
        public int TotalLearners { get; set; }
        public int TotalFacilitators { get; set; }
        public int DaysRegistered { get; set; }
        public double LearnerAttendanceRate { get; set; }
        public double FacilitatorAttendanceRate { get; set; }
        public byte? TermTotalAttendanceDays { get; set; }
        public IReadOnlyList<AttendanceArmSummaryDto> Arms { get; set; } =
            Array.Empty<AttendanceArmSummaryDto>();
        public IReadOnlyList<AttendanceAtRiskLearnerDto> AtRiskLearners { get; set; } =
            Array.Empty<AttendanceAtRiskLearnerDto>();
        public IReadOnlyList<AttendanceDayPointDto> DailyTrend { get; set; } =
            Array.Empty<AttendanceDayPointDto>();
        public IReadOnlyList<AttendanceFacilitatorSummaryDto> Facilitators { get; set; } =
            Array.Empty<AttendanceFacilitatorSummaryDto>();
    }

    public sealed class SyncTermAttendanceResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int LearnersUpdated { get; set; }
        public int RowsCreated { get; set; }
        public byte SchoolDaysCounted { get; set; }
    }
}
