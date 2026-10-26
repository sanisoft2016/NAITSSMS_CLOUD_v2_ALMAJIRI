using System;
using System.Threading;
using System.Threading.Tasks;
using NAITSSMS.Application.DTOs;

namespace NAITSSMS.Application.IService
{
    /// <summary>
    /// Daily attendance register for learners and facilitators. Scoped to the caller's school
    /// (centre) so a Centre Administrator can only ever mark their own roster.
    /// </summary>
    public interface IDailyAttendanceService
    {
        Task<DailyAttendanceRosterDto> GetLearnerRosterAsync(
            string userId, string classArmId, DateTime attendanceDate, CancellationToken ct = default);

        Task<DailyAttendanceSaveResultDto> SaveLearnerRegisterAsync(
            string userId, DailyAttendanceSubmitDto model, CancellationToken ct = default);

        Task<DailyAttendanceRosterDto> GetFacilitatorRosterAsync(
            string userId, DateTime attendanceDate, CancellationToken ct = default);

        Task<DailyAttendanceSaveResultDto> SaveFacilitatorRegisterAsync(
            string userId, DailyAttendanceSubmitDto model, CancellationToken ct = default);

        Task<DailyAttendanceSummaryDto> GetSummaryAsync(
            string userId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);

        /// <summary>
        /// Rolls the daily register up into the term-level StudentsAttendanceReports.Attendance
        /// value that report cards print.
        /// </summary>
        Task<SyncTermAttendanceResultDto> SyncTermTotalsAsync(
            string userId, CancellationToken ct = default);
    }
}
