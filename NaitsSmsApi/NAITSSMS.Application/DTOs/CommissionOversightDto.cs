using System;
using System.Collections.Generic;

namespace NAITSSMS.Application.DTOs
{
    /// <summary>One learning centre's attendance standing for the requested period.</summary>
    public sealed class CommissionCentreRowDto
    {
        public string CentreId { get; set; } = string.Empty;
        public string CentreName { get; set; } = string.Empty;
        public string RegistrationNumber { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public string CentreType { get; set; } = string.Empty;
        public string RegistrarUserId { get; set; } = string.Empty;
        public int Learners { get; set; }
        public int FemaleLearners { get; set; }
        public int Facilitators { get; set; }
        public int DaysRegistered { get; set; }
        public double LearnerAttendanceRate { get; set; }
        public double FacilitatorAttendanceRate { get; set; }
        public int AtRiskLearners { get; set; }
        /// <summary>Most recent learner (class) register.</summary>
        public DateTime? LastRegisterDate { get; set; }
        public int DaysSinceLastRegister { get; set; }

        /// <summary>Most recent facilitator (staff) register.</summary>
        public DateTime? LastStaffRegisterDate { get; set; }
        public int StaffDaysSinceLastRegister { get; set; }

        /// <summary>No learner register for three or more school days.</summary>
        public bool ClassRegisterOverdue { get; set; }

        /// <summary>No facilitator register for three or more school days.</summary>
        public bool StaffRegisterOverdue { get; set; }

        /// <summary>True when either register — learners' or facilitators' — is overdue.</summary>
        public bool RegisterOverdue { get; set; }

        /// <summary>True when the centre has never submitted a register.</summary>
        public bool NeverSubmitted { get; set; }
    }

    public sealed class CommissionDayPointDto
    {
        public DateTime Date { get; set; }
        public int LearnersPresent { get; set; }
        public int LearnersAbsent { get; set; }
        public double LearnerAttendanceRate { get; set; }
        public double FacilitatorAttendanceRate { get; set; }
    }

    public sealed class CommissionEnrolmentPointDto
    {
        public string CentreId { get; set; } = string.Empty;
        public string CentreName { get; set; } = string.Empty;
        public int Learners { get; set; }
        public int Facilitators { get; set; }
    }

    /// <summary>A learner flagged as at risk within the Commission's selected period.</summary>
    public sealed class CommissionAtRiskLearnerDto
    {
        public string CentreId { get; set; } = string.Empty;
        public string CentreName { get; set; } = string.Empty;
        public string ClassArmLabel { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string IdentificationNo { get; set; } = string.Empty;
        public double AttendanceRate { get; set; }
        public int CurrentAbsenceStreak { get; set; }
        public int LongestAbsenceStreak { get; set; }
        public DateTime? LastPresentDate { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Submission standing for one register over the selected period: a class arm's learner register,
    /// or (when <see cref="IsStaffRegister"/>) the centre's facilitator register.
    /// </summary>
    public sealed class CommissionClassArmAttendanceStatusDto
    {
        /// <summary>The centre's facilitator register, kept by the centre administrator.</summary>
        public bool IsStaffRegister { get; set; }

        public string CentreId { get; set; } = string.Empty;
        public string CentreName { get; set; } = string.Empty;
        public string ClassArmId { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string ClassArmName { get; set; } = string.Empty;
        public string FormMasterId { get; set; } = string.Empty;
        public string FormMasterName { get; set; } = string.Empty;
        public int EnrolledLearners { get; set; }
        public int ExpectedSubmissions { get; set; }
        public int SubmittedDates { get; set; }

        /// <summary>True when no register was saved for this arm in the selected period.</summary>
        public bool NeverSubmitted { get; set; }

        public IReadOnlyList<DateTime> MissingDates { get; set; } = Array.Empty<DateTime>();
    }

    /// <summary>A programme centre with at least one missing class-arm attendance submission.</summary>
    public sealed class CommissionDefaultedCentreDto
    {
        public string CentreId { get; set; } = string.Empty;
        public string CentreName { get; set; } = string.Empty;
        public int MissingSubmissionCount { get; set; }
        public int AffectedClassArms { get; set; }
        public IReadOnlyList<CommissionClassArmAttendanceStatusDto> ClassArms { get; set; } =
            Array.Empty<CommissionClassArmAttendanceStatusDto>();
    }

    /// <summary>A programme learning centre with its current roll — no attendance.</summary>
    public sealed class CommissionCentreDirectoryRowDto
    {
        public string CentreId { get; set; } = string.Empty;
        public string CentreName { get; set; } = string.Empty;
        public string RegistrationNumber { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Lga { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public string CentreType { get; set; } = string.Empty;
        public string RegistrarUserId { get; set; } = string.Empty;
        public int Learners { get; set; }
        public int FemaleLearners { get; set; }
        public int MaleLearners { get; set; }
        public int Facilitators { get; set; }
    }

    public sealed class CommissionCentresDirectoryDto
    {
        public int TotalCentres { get; set; }
        public int TotalLearners { get; set; }
        public int TotalFemaleLearners { get; set; }
        public int TotalFacilitators { get; set; }
        public int States { get; set; }
        public int Zones { get; set; }
        public IReadOnlyList<CommissionCentreDirectoryRowDto> Centres { get; set; } =
            Array.Empty<CommissionCentreDirectoryRowDto>();
    }

    /// <summary>Roll-up of centres by state or by geopolitical zone.</summary>
    public sealed class CommissionAreaRowDto
    {
        public string Name { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public int Centres { get; set; }
        public int Learners { get; set; }
        public int FemaleLearners { get; set; }
        public int Facilitators { get; set; }
        public double LearnerAttendanceRate { get; set; }
        public double FacilitatorAttendanceRate { get; set; }
        public int AtRiskLearners { get; set; }
        public int CentresOverdue { get; set; }
    }

    /// <summary>Where learners who left the programme went.</summary>
    public sealed class CommissionTransitionSummaryDto
    {
        public int Leavers { get; set; }
        public int ToFormalSchool { get; set; }
        public int ToVocational { get; set; }
        public int Withdrawn { get; set; }

        /// <summary>Share of leavers who moved into formal school or vocational training.</summary>
        public double TransitionRate { get; set; }
    }

    /// <summary>A learner who has left the programme, and where they went.</summary>
    public sealed class CommissionLeaverDto
    {
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string IdentificationNo { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string CentreId { get; set; } = string.Empty;
        public string CentreName { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string LastLevel { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        /// <summary>formal, vocational or withdrawn — for filtering.</summary>
        public string Outcome { get; set; } = string.Empty;
        public string OutcomeLabel { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public DateTime? LeftOn { get; set; }
    }

    public sealed class CommissionOversightDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public DateTime AsAtDate { get; set; }

        /// <summary>
        /// The school day the "today" figures describe: today on a school day, otherwise the most
        /// recent school day (so a weekend shows Friday's registers rather than zeros).
        /// </summary>
        public DateTime ReportDate { get; set; }

        // Programme totals
        public int TotalCentres { get; set; }
        public int TotalLearners { get; set; }
        public int FemaleLearners { get; set; }
        public int MaleLearners { get; set; }
        public int TotalFacilitators { get; set; }
        public int ActiveTerms { get; set; }

        // Today
        public int LearnersPresentToday { get; set; }
        public int LearnersAbsentToday { get; set; }
        public double LearnerAttendanceRateToday { get; set; }
        public int FacilitatorsPresentToday { get; set; }
        public int FacilitatorsAbsentToday { get; set; }
        public double FacilitatorAttendanceRateToday { get; set; }
        public int CentresReportingToday { get; set; }

        // Period
        public double LearnerAttendanceRate { get; set; }
        public double FemaleAttendanceRate { get; set; }
        public double MaleAttendanceRate { get; set; }
        public double FacilitatorAttendanceRate { get; set; }
        public int TotalAtRiskLearners { get; set; }
        public int CentresWithOverdueRegister { get; set; }

        public CommissionTransitionSummaryDto Transitions { get; set; } = new();

        public IReadOnlyList<CommissionCentreRowDto> Centres { get; set; } =
            Array.Empty<CommissionCentreRowDto>();
        public IReadOnlyList<CommissionAreaRowDto> StatesSummary { get; set; } =
            Array.Empty<CommissionAreaRowDto>();
        public IReadOnlyList<CommissionAreaRowDto> ZonesSummary { get; set; } =
            Array.Empty<CommissionAreaRowDto>();
        public IReadOnlyList<CommissionDayPointDto> DailyTrend { get; set; } =
            Array.Empty<CommissionDayPointDto>();
        public IReadOnlyList<CommissionEnrolmentPointDto> EnrolmentByCentre { get; set; } =
            Array.Empty<CommissionEnrolmentPointDto>();
        public IReadOnlyList<CommissionAtRiskLearnerDto> AtRiskLearners { get; set; } =
            Array.Empty<CommissionAtRiskLearnerDto>();
        public IReadOnlyList<CommissionDefaultedCentreDto> DefaultedCentres { get; set; } =
            Array.Empty<CommissionDefaultedCentreDto>();
        public IReadOnlyList<CommissionClassArmAttendanceStatusDto> ClassArmStatuses { get; set; } =
            Array.Empty<CommissionClassArmAttendanceStatusDto>();
    }

    // ----------------------------------------------------------------------------------------
    // Learning outcomes
    // ----------------------------------------------------------------------------------------

    public sealed class CommissionTermOptionDto
    {
        public string Session { get; set; } = string.Empty;
        public byte Term { get; set; }
        public string Label { get; set; } = string.Empty;

        /// <summary>False while the term is open and its examinations are not yet recorded.</summary>
        public bool Complete { get; set; }
    }

    public sealed class CommissionOutcomeRowDto
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public int Learners { get; set; }
        public double AverageScore { get; set; }
        public double PassRate { get; set; }
        public string StrongestSubject { get; set; } = string.Empty;
        public string WeakestSubject { get; set; } = string.Empty;
    }

    public sealed class CommissionGradeBandDto
    {
        public string Grade { get; set; } = string.Empty;
        public string Range { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public sealed class CommissionOutcomesDto
    {
        public IReadOnlyList<CommissionTermOptionDto> AvailableTerms { get; set; } = Array.Empty<CommissionTermOptionDto>();
        public CommissionTermOptionDto? Term { get; set; }
        public CommissionTermOptionDto? PreviousTerm { get; set; }
        public int CentresReporting { get; set; }
        public int LearnersAssessed { get; set; }
        public int ScoresRecorded { get; set; }
        public double AverageScore { get; set; }
        public double? PreviousAverageScore { get; set; }
        public double PassRate { get; set; }
        public double? PreviousPassRate { get; set; }
        public double PassMark { get; set; }

        /// <summary>Learners whose average across subjects is below the pass mark.</summary>
        public int LearnersNeedingSupport { get; set; }

        public IReadOnlyList<CommissionGradeBandDto> Grades { get; set; } = Array.Empty<CommissionGradeBandDto>();
        public IReadOnlyList<CommissionOutcomeRowDto> Centres { get; set; } = Array.Empty<CommissionOutcomeRowDto>();
        public IReadOnlyList<CommissionOutcomeRowDto> Subjects { get; set; } = Array.Empty<CommissionOutcomeRowDto>();
        public IReadOnlyList<CommissionOutcomeRowDto> Genders { get; set; } = Array.Empty<CommissionOutcomeRowDto>();
        public IReadOnlyList<CommissionOutcomeRowDto> Levels { get; set; } = Array.Empty<CommissionOutcomeRowDto>();
        public IReadOnlyList<CommissionOutcomeRowDto> States { get; set; } = Array.Empty<CommissionOutcomeRowDto>();
    }

    // ----------------------------------------------------------------------------------------
    // Centre drill-down
    // ----------------------------------------------------------------------------------------

    public sealed class CommissionCentreLevelDto
    {
        public string ClassArmId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string FormMaster { get; set; } = string.Empty;
        public int Learners { get; set; }
        public int Female { get; set; }
        public int Male { get; set; }
        public double AttendanceRate { get; set; }
    }

    public sealed class CommissionCentreFacilitatorDto
    {
        public string EmployeeId { get; set; } = string.Empty;
        public string StaffNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Discipline { get; set; } = string.Empty;
        public string Qualification { get; set; } = string.Empty;
        public int DaysRegistered { get; set; }
        public int DaysAbsent { get; set; }
        public double AttendanceRate { get; set; }
        public DateTime? LastAbsentDate { get; set; }
    }

    public sealed class CommissionCentreLearnerDto
    {
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string IdentificationNo { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double AttendanceRate { get; set; }
        public bool AtRisk { get; set; }
    }

    public sealed class CommissionCentreDetailDto
    {
        public string CentreId { get; set; } = string.Empty;
        public string CentreName { get; set; } = string.Empty;
        public string RegistrationNumber { get; set; } = string.Empty;
        public string CentreType { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Community { get; set; } = string.Empty;
        public string Lga { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public string Head { get; set; } = string.Empty;
        public string Telephone { get; set; } = string.Empty;
        public short? YearEstablished { get; set; }
        public string CurrentSession { get; set; } = string.Empty;
        public string CurrentTerm { get; set; } = string.Empty;

        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int Learners { get; set; }
        public int FemaleLearners { get; set; }
        public int Facilitators { get; set; }
        public int DaysRegistered { get; set; }
        public int ExpectedDays { get; set; }
        public DateTime? LastRegisterDate { get; set; }
        public DateTime? LastStaffRegisterDate { get; set; }
        public bool ClassRegisterOverdue { get; set; }
        public bool StaffRegisterOverdue { get; set; }

        /// <summary>True when either register — learners' or facilitators' — is overdue.</summary>
        public bool RegisterOverdue { get; set; }
        public double LearnerAttendanceRate { get; set; }
        public double FacilitatorAttendanceRate { get; set; }

        public CommissionTransitionSummaryDto Transitions { get; set; } = new();
        public CommissionOutcomeRowDto? LastTermOutcome { get; set; }
        public string LastTermLabel { get; set; } = string.Empty;

        public IReadOnlyList<CommissionCentreLevelDto> Levels { get; set; } = Array.Empty<CommissionCentreLevelDto>();
        public IReadOnlyList<CommissionCentreFacilitatorDto> FacilitatorsList { get; set; } = Array.Empty<CommissionCentreFacilitatorDto>();
        public IReadOnlyList<CommissionAtRiskLearnerDto> AtRiskLearners { get; set; } = Array.Empty<CommissionAtRiskLearnerDto>();
        public IReadOnlyList<CommissionCentreLearnerDto> Roll { get; set; } = Array.Empty<CommissionCentreLearnerDto>();
        public IReadOnlyList<CommissionDayPointDto> DailyTrend { get; set; } = Array.Empty<CommissionDayPointDto>();
        public IReadOnlyList<CommissionOutcomeRowDto> SubjectOutcomes { get; set; } = Array.Empty<CommissionOutcomeRowDto>();
    }

    // ----------------------------------------------------------------------------------------
    // Learner records
    // ----------------------------------------------------------------------------------------

    public sealed class CommissionLearnerSearchRowDto
    {
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string IdentificationNo { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public int Age { get; set; }
        public string CentreId { get; set; } = string.Empty;
        public string CentreName { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public sealed class CommissionLearnerSubjectDto
    {
        public string Subject { get; set; } = string.Empty;
        public double Ca1 { get; set; }
        public double Ca2 { get; set; }
        public double Ca3 { get; set; }
        public double Exam { get; set; }
        public double Total { get; set; }
        public string Grade { get; set; } = string.Empty;
    }

    public sealed class CommissionLearnerTermDto
    {
        public string Session { get; set; } = string.Empty;
        public string Term { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public bool Complete { get; set; }
        public int DaysOpen { get; set; }
        public int DaysPresent { get; set; }
        public double AttendanceRate { get; set; }
        public double TotalScore { get; set; }
        public double AverageScore { get; set; }
        public string Grade { get; set; } = string.Empty;
        public int Position { get; set; }
        public int ClassSize { get; set; }
        public string TeacherComment { get; set; } = string.Empty;
        public string HeadComment { get; set; } = string.Empty;
        public IReadOnlyList<CommissionLearnerSubjectDto> Subjects { get; set; } = Array.Empty<CommissionLearnerSubjectDto>();
    }

    public sealed class CommissionRegisterMarkDto
    {
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
    }

    public sealed class CommissionLearnerRecordDto
    {
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string IdentificationNo { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public int Age { get; set; }
        public string PlaceOfBirth { get; set; } = string.Empty;
        public string CentreId { get; set; } = string.Empty;
        public string CentreName { get; set; } = string.Empty;
        public string CentreAddress { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string StartLevel { get; set; } = string.Empty;
        public DateTime AdmissionDate { get; set; }
        public DateTime? DischargeDate { get; set; }
        public string Status { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;
        public string StateOfOrigin { get; set; } = string.Empty;
        public string LgaOfOrigin { get; set; } = string.Empty;
        public string Tsangaya { get; set; } = string.Empty;
        public int? QuranJuzMemorised { get; set; }
        public string PriorSchooling { get; set; } = string.Empty;
        public string TransitionStatus { get; set; } = string.Empty;
        public DateTime? TransitionDate { get; set; }
        public string TransitionDestination { get; set; } = string.Empty;

        public string GuardianName { get; set; } = string.Empty;
        public string GuardianRelationship { get; set; } = string.Empty;
        public string GuardianPhone { get; set; } = string.Empty;
        public string GuardianAltPhone { get; set; } = string.Empty;
        public string GuardianAddress { get; set; } = string.Empty;
        public string GuardianOccupation { get; set; } = string.Empty;

        public int CurrentTermDaysRegistered { get; set; }
        public int CurrentTermDaysPresent { get; set; }
        public int CurrentTermDaysAbsent { get; set; }
        public int CurrentTermDaysLate { get; set; }
        public int CurrentAbsenceStreak { get; set; }
        public double CurrentTermAttendanceRate { get; set; }

        public IReadOnlyList<CommissionLearnerTermDto> Terms { get; set; } = Array.Empty<CommissionLearnerTermDto>();
        public IReadOnlyList<CommissionRegisterMarkDto> RecentRegister { get; set; } = Array.Empty<CommissionRegisterMarkDto>();
    }
}
