using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Unitofwork;

namespace NAITSSMS.Application.Service
{
    public class DailyAttendanceService : IDailyAttendanceService
    {
        /// <summary>A learner below this rate over the requested window is flagged as at risk.</summary>
        private const double AtRiskAttendanceRate = 60d;

        /// <summary>Consecutive absent days that flag a learner as a drop-out risk.</summary>
        private const int AtRiskAbsenceStreak = 3;

        /// <summary>The attendance rate is only judged once a learner has this many register days.</summary>
        private const int AtRiskMinimumDays = 5;

        private readonly IUnitOfWork _uow;
        private readonly ISchoolScopeResolver _schoolScopeResolver;

        public DailyAttendanceService(IUnitOfWork uow, ISchoolScopeResolver schoolScopeResolver)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _schoolScopeResolver = schoolScopeResolver ?? throw new ArgumentNullException(nameof(schoolScopeResolver));
        }

        public async Task<DailyAttendanceRosterDto> GetLearnerRosterAsync(
            string userId, string classArmId, DateTime attendanceDate, CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var date = attendanceDate.Date;

            var arm = await _uow.Repository<AxClassArm>().FindAsync(
                entry => entry.Id == classArmId,
                ct,
                entry => entry.Classs!);

            if (arm?.Classs == null || arm.Classs.SchoolInfoId != schoolId)
            {
                return new DailyAttendanceRosterDto { AttendanceDate = date, ClassArmId = classArmId ?? string.Empty };
            }

            var armLabel = BuildArmLabel(arm);

            var learners = await _uow.Repository<TxStudentDetail>().WhereAsync(
                entry => entry.ClassArmId == classArmId
                    && entry.SchoolInfoId == schoolId
                    && entry.TimeStatus == TimeStatus.Current,
                ct,
                entry => entry.User!);

            var existing = (await _uow.Repository<LearnerDailyAttendance>().WhereAsync(
                    entry => entry.ClassArmId == classArmId && entry.AttendanceDate == date,
                    ct))
                .GroupBy(entry => entry.StudentId)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var entries = learners
                .Select(learner =>
                {
                    existing.TryGetValue(learner.UserId, out var mark);
                    return new DailyAttendanceRosterEntryDto
                    {
                        PersonId = learner.UserId,
                        FullName = BuildFullName(learner.User),
                        IdentificationNo = learner.IdNo ?? string.Empty,
                        ClassArmId = classArmId,
                        ClassArmLabel = armLabel,
                        Status = mark?.Status ?? DailyAttendanceStatus.Present,
                        Remark = mark?.Remark ?? string.Empty,
                        AlreadyMarked = mark != null
                    };
                })
                .OrderBy(entry => entry.FullName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return BuildRoster(date, classArmId, armLabel, entries);
        }

        public async Task<DailyAttendanceSaveResultDto> SaveLearnerRegisterAsync(
            string userId, DailyAttendanceSubmitDto model, CancellationToken ct = default)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.ClassArmId))
            {
                return Failed("Select a class arm before saving the register.");
            }

            if (model.Marks.Count == 0)
            {
                return Failed("There is nothing to save on this register.");
            }

            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var date = model.AttendanceDate.Date;
            if (date > DateTime.Today)
            {
                return Failed("The register cannot be taken for a future date.");
            }

            var currentTerm = await GetCurrentTermAsync(schoolId, ct);
            if (currentTerm == null)
            {
                return Failed("Session and term initialisation must be completed before taking a register.");
            }

            var arm = await _uow.Repository<AxClassArm>().FindAsync(
                entry => entry.Id == model.ClassArmId,
                ct,
                entry => entry.Classs!);

            if (arm?.Classs == null || arm.Classs.SchoolInfoId != schoolId)
            {
                return Failed("The selected class arm does not belong to this centre.");
            }

            var learnerIds = (await _uow.Repository<TxStudentDetail>().WhereAsync(
                    entry => entry.ClassArmId == model.ClassArmId
                        && entry.SchoolInfoId == schoolId
                        && entry.TimeStatus == TimeStatus.Current,
                    ct))
                .Select(entry => entry.UserId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var repository = _uow.Repository<LearnerDailyAttendance>();
            var existing = (await repository.WhereAsync(
                    entry => entry.ClassArmId == model.ClassArmId && entry.AttendanceDate == date,
                    ct))
                .GroupBy(entry => entry.StudentId)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var created = 0;
            var updated = 0;
            var markedAt = DateTime.UtcNow;

            foreach (var mark in model.Marks)
            {
                if (string.IsNullOrWhiteSpace(mark.PersonId) || !learnerIds.Contains(mark.PersonId))
                {
                    continue;
                }

                var remark = Truncate(mark.Remark, 150);

                if (existing.TryGetValue(mark.PersonId, out var row))
                {
                    row.Status = mark.Status;
                    row.Remark = remark;
                    row.MarkedByUserId = userId;
                    row.MarkedAtUtc = markedAt;
                    row.YearlyAndTermlyStatusId = currentTerm.Id;
                    repository.Update(row);
                    updated++;
                    continue;
                }

                repository.Add(new LearnerDailyAttendance
                {
                    Id = Guid.NewGuid().ToString(),
                    AttendanceDate = date,
                    Status = mark.Status,
                    Remark = remark,
                    MarkedByUserId = userId,
                    MarkedAtUtc = markedAt,
                    SchoolInfoId = schoolId,
                    StudentId = mark.PersonId,
                    ClassArmId = model.ClassArmId,
                    YearlyAndTermlyStatusId = currentTerm.Id
                });
                created++;
            }

            if (created == 0 && updated == 0)
            {
                return Failed("None of the submitted learners belong to the selected class arm.");
            }

            await _uow.SaveChangesAsync(ct);

            return new DailyAttendanceSaveResultDto
            {
                Success = true,
                Created = created,
                Updated = updated,
                Message = $"Register saved for {date:dd MMM yyyy} ({created + updated} learner(s))."
            };
        }

        public async Task<DailyAttendanceRosterDto> GetFacilitatorRosterAsync(
            string userId, DateTime attendanceDate, CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var date = attendanceDate.Date;

            var facilitators = await GetFacilitatorsAsync(schoolId, ct);

            var existing = (await _uow.Repository<FacilitatorDailyAttendance>().WhereAsync(
                    entry => entry.SchoolInfoId == schoolId && entry.AttendanceDate == date,
                    ct))
                .GroupBy(entry => entry.EmployeeId)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var entries = facilitators
                .Select(facilitator =>
                {
                    existing.TryGetValue(facilitator.Id, out var mark);
                    return new DailyAttendanceRosterEntryDto
                    {
                        PersonId = facilitator.Id,
                        FullName = BuildFullName(facilitator.User),
                        IdentificationNo = facilitator.EmployeeIdNo ?? string.Empty,
                        Status = mark?.Status ?? DailyAttendanceStatus.Present,
                        Remark = mark?.Remark ?? string.Empty,
                        AlreadyMarked = mark != null
                    };
                })
                .OrderBy(entry => entry.FullName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return BuildRoster(date, string.Empty, "All facilitators", entries);
        }

        public async Task<DailyAttendanceSaveResultDto> SaveFacilitatorRegisterAsync(
            string userId, DailyAttendanceSubmitDto model, CancellationToken ct = default)
        {
            if (model == null || model.Marks.Count == 0)
            {
                return Failed("There is nothing to save on this register.");
            }

            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var date = model.AttendanceDate.Date;
            if (date > DateTime.Today)
            {
                return Failed("The register cannot be taken for a future date.");
            }

            var currentTerm = await GetCurrentTermAsync(schoolId, ct);
            if (currentTerm == null)
            {
                return Failed("Session and term initialisation must be completed before taking a register.");
            }

            var facilitatorIds = (await GetFacilitatorsAsync(schoolId, ct))
                .Select(entry => entry.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var repository = _uow.Repository<FacilitatorDailyAttendance>();
            var existing = (await repository.WhereAsync(
                    entry => entry.SchoolInfoId == schoolId && entry.AttendanceDate == date,
                    ct))
                .GroupBy(entry => entry.EmployeeId)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var created = 0;
            var updated = 0;
            var markedAt = DateTime.UtcNow;

            foreach (var mark in model.Marks)
            {
                if (string.IsNullOrWhiteSpace(mark.PersonId) || !facilitatorIds.Contains(mark.PersonId))
                {
                    continue;
                }

                var remark = Truncate(mark.Remark, 150);

                if (existing.TryGetValue(mark.PersonId, out var row))
                {
                    row.Status = mark.Status;
                    row.Remark = remark;
                    row.MarkedByUserId = userId;
                    row.MarkedAtUtc = markedAt;
                    row.YearlyAndTermlyStatusId = currentTerm.Id;
                    repository.Update(row);
                    updated++;
                    continue;
                }

                repository.Add(new FacilitatorDailyAttendance
                {
                    Id = Guid.NewGuid().ToString(),
                    AttendanceDate = date,
                    Status = mark.Status,
                    Remark = remark,
                    MarkedByUserId = userId,
                    MarkedAtUtc = markedAt,
                    SchoolInfoId = schoolId,
                    EmployeeId = mark.PersonId,
                    YearlyAndTermlyStatusId = currentTerm.Id
                });
                created++;
            }

            if (created == 0 && updated == 0)
            {
                return Failed("None of the submitted facilitators belong to this centre.");
            }

            await _uow.SaveChangesAsync(ct);

            return new DailyAttendanceSaveResultDto
            {
                Success = true,
                Created = created,
                Updated = updated,
                Message = $"Facilitator register saved for {date:dd MMM yyyy} ({created + updated} staff)."
            };
        }

        public async Task<DailyAttendanceSummaryDto> GetSummaryAsync(
            string userId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var from = fromDate.Date;
            var to = toDate.Date;
            if (to < from)
            {
                (from, to) = (to, from);
            }

            var school = await _uow.Repository<SchoolTyInfo, string>().FindAsync(schoolId, ct);
            var currentTerm = await GetCurrentTermAsync(schoolId, ct);

            var learners = await _uow.Repository<TxStudentDetail>().WhereAsync(
                entry => entry.SchoolInfoId == schoolId && entry.TimeStatus == TimeStatus.Current,
                ct,
                entry => entry.User!);

            var arms = await GetSchoolArmsAsync(schoolId, ct);
            var armLabels = arms.ToDictionary(arm => arm.Id, BuildArmLabel, StringComparer.OrdinalIgnoreCase);

            var learnerMarks = await _uow.Repository<LearnerDailyAttendance>().WhereAsync(
                entry => entry.SchoolInfoId == schoolId
                    && entry.AttendanceDate >= from
                    && entry.AttendanceDate <= to,
                ct);

            var facilitators = await GetFacilitatorsAsync(schoolId, ct);
            var facilitatorMarks = await _uow.Repository<FacilitatorDailyAttendance>().WhereAsync(
                entry => entry.SchoolInfoId == schoolId
                    && entry.AttendanceDate >= from
                    && entry.AttendanceDate <= to,
                ct);

            var armSummaries = learnerMarks
                .GroupBy(mark => mark.ClassArmId, StringComparer.OrdinalIgnoreCase)
                .Select(group => new AttendanceArmSummaryDto
                {
                    ClassArmId = group.Key,
                    ClassArmLabel = armLabels.TryGetValue(group.Key, out var label) ? label : "Unknown arm",
                    Learners = group.Select(mark => mark.StudentId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    DaysRegistered = group.Select(mark => mark.AttendanceDate).Distinct().Count(),
                    PresentMarks = group.Count(mark => mark.Status == DailyAttendanceStatus.Present),
                    AbsentMarks = group.Count(mark => mark.Status == DailyAttendanceStatus.Absent),
                    LateMarks = group.Count(mark => mark.Status == DailyAttendanceStatus.Late),
                    ExcusedMarks = group.Count(mark => mark.Status == DailyAttendanceStatus.Excused),
                    AttendanceRate = Rate(group.Count(mark => IsCountedPresent(mark.Status)), group.Count())
                })
                .OrderBy(row => row.ClassArmLabel, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var dailyTrend = learnerMarks
                .GroupBy(mark => mark.AttendanceDate)
                .Select(group => new AttendanceDayPointDto
                {
                    Date = group.Key,
                    Present = group.Count(mark => IsCountedPresent(mark.Status)),
                    Absent = group.Count(mark => mark.Status == DailyAttendanceStatus.Absent),
                    AttendanceRate = Rate(group.Count(mark => IsCountedPresent(mark.Status)), group.Count())
                })
                .OrderBy(point => point.Date)
                .ToList();

            var learnerLookup = learners.ToDictionary(
                learner => learner.UserId,
                learner => learner,
                StringComparer.OrdinalIgnoreCase);

            var atRisk = BuildAtRiskLearners(learnerMarks, learnerLookup, armLabels);

            var facilitatorSummaries = facilitators
                .Select(facilitator =>
                {
                    var marks = facilitatorMarks
                        .Where(mark => string.Equals(mark.EmployeeId, facilitator.Id, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    var present = marks.Count(mark => IsCountedPresent(mark.Status));
                    return new AttendanceFacilitatorSummaryDto
                    {
                        EmployeeId = facilitator.Id,
                        FullName = BuildFullName(facilitator.User),
                        StaffNo = facilitator.EmployeeIdNo ?? string.Empty,
                        DaysRegistered = marks.Count,
                        DaysPresent = present,
                        DaysAbsent = marks.Count(mark => mark.Status == DailyAttendanceStatus.Absent),
                        AttendanceRate = Rate(present, marks.Count)
                    };
                })
                .OrderBy(row => row.AttendanceRate)
                .ThenBy(row => row.FullName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return new DailyAttendanceSummaryDto
            {
                FromDate = from,
                ToDate = to,
                SchoolName = school?.SchoolName ?? string.Empty,
                TotalLearners = learners.Count,
                TotalFacilitators = facilitators.Count,
                DaysRegistered = learnerMarks.Select(mark => mark.AttendanceDate).Distinct().Count(),
                LearnerAttendanceRate = Rate(
                    learnerMarks.Count(mark => IsCountedPresent(mark.Status)),
                    learnerMarks.Count),
                FacilitatorAttendanceRate = Rate(
                    facilitatorMarks.Count(mark => IsCountedPresent(mark.Status)),
                    facilitatorMarks.Count),
                TermTotalAttendanceDays = currentTerm?.TotalAttendance,
                Arms = armSummaries,
                AtRiskLearners = atRisk,
                DailyTrend = dailyTrend,
                Facilitators = facilitatorSummaries
            };
        }

        public async Task<SyncTermAttendanceResultDto> SyncTermTotalsAsync(
            string userId, CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var currentTerm = await GetCurrentTermAsync(schoolId, ct);
            if (currentTerm == null)
            {
                return new SyncTermAttendanceResultDto
                {
                    Success = false,
                    Message = "Session and term initialisation must be completed before syncing attendance."
                };
            }

            var marks = await _uow.Repository<LearnerDailyAttendance>().WhereAsync(
                entry => entry.SchoolInfoId == schoolId
                    && entry.YearlyAndTermlyStatusId == currentTerm.Id,
                ct);

            if (marks.Count == 0)
            {
                return new SyncTermAttendanceResultDto
                {
                    Success = false,
                    Message = "No daily register has been taken for the current term yet."
                };
            }

            var schoolDays = marks.Select(mark => mark.AttendanceDate).Distinct().Count();

            var attendanceRepo = _uow.Repository<StudentsAtteXzndanceAndCommentsReport>();
            var termRows = (await attendanceRepo.WhereAsync(
                    entry => entry.YearlyAndTermlyStatusId == currentTerm.Id,
                    ct))
                .GroupBy(entry => entry.StudentId)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var created = 0;
            var updated = 0;

            foreach (var learnerMarks in marks.GroupBy(mark => mark.StudentId, StringComparer.OrdinalIgnoreCase))
            {
                var daysPresent = learnerMarks.Count(mark => IsCountedPresent(mark.Status));
                var presentValue = (byte)Math.Min(byte.MaxValue, daysPresent);
                var armId = learnerMarks
                    .OrderByDescending(mark => mark.AttendanceDate)
                    .First()
                    .ClassArmId;

                if (termRows.TryGetValue(learnerMarks.Key, out var row))
                {
                    row.Attendance = presentValue;
                    attendanceRepo.Update(row);
                    updated++;
                    continue;
                }

                attendanceRepo.Add(new StudentsAtteXzndanceAndCommentsReport
                {
                    Id = Guid.NewGuid().ToString(),
                    Attendance = presentValue,
                    StudentId = learnerMarks.Key,
                    ClassArmId = armId,
                    YearlyAndTermlyStatusId = currentTerm.Id
                });
                created++;
            }

            // Report cards print "days present out of total days"; keep the denominator honest.
            var totalDays = (byte)Math.Min(byte.MaxValue, schoolDays);
            if (currentTerm.TotalAttendance == null || currentTerm.TotalAttendance < totalDays)
            {
                currentTerm.TotalAttendance = totalDays;
                _uow.Repository<TxYearlyAndTermlyStatus>().Update(currentTerm);
            }

            await _uow.SaveChangesAsync(ct);

            return new SyncTermAttendanceResultDto
            {
                Success = true,
                LearnersUpdated = created + updated,
                RowsCreated = created,
                SchoolDaysCounted = totalDays,
                Message = $"{created + updated} learner record(s) updated from {schoolDays} day(s) of register."
            };
        }

        private static List<AttendanceAtRiskLearnerDto> BuildAtRiskLearners(
            IReadOnlyCollection<LearnerDailyAttendance> marks,
            IReadOnlyDictionary<string, TxStudentDetail> learners,
            IReadOnlyDictionary<string, string> armLabels)
        {
            var rows = new List<AttendanceAtRiskLearnerDto>();

            foreach (var group in marks.GroupBy(mark => mark.StudentId, StringComparer.OrdinalIgnoreCase))
            {
                var ordered = group.OrderBy(mark => mark.AttendanceDate).ToList();
                var present = ordered.Count(mark => IsCountedPresent(mark.Status));
                var absent = ordered.Count(mark => mark.Status == DailyAttendanceStatus.Absent);
                var rate = Rate(present, ordered.Count);

                var streak = 0;
                var longestStreak = 0;
                foreach (var mark in ordered)
                {
                    if (mark.Status == DailyAttendanceStatus.Absent)
                    {
                        streak++;
                        longestStreak = Math.Max(longestStreak, streak);
                        continue;
                    }

                    streak = 0;
                }

                var isAtRisk = longestStreak >= AtRiskAbsenceStreak
                    || (ordered.Count >= AtRiskMinimumDays && rate < AtRiskAttendanceRate);
                if (!isAtRisk)
                {
                    continue;
                }

                learners.TryGetValue(group.Key, out var learner);
                var armId = ordered[^1].ClassArmId;

                rows.Add(new AttendanceAtRiskLearnerDto
                {
                    StudentId = group.Key,
                    FullName = BuildFullName(learner?.User),
                    IdentificationNo = learner?.IdNo ?? string.Empty,
                    ClassArmLabel = armLabels.TryGetValue(armId, out var label) ? label : string.Empty,
                    DaysRegistered = ordered.Count,
                    DaysPresent = present,
                    DaysAbsent = absent,
                    AttendanceRate = rate,
                    LongestAbsenceStreak = longestStreak,
                    LastPresentDate = ordered.LastOrDefault(mark => IsCountedPresent(mark.Status))?.AttendanceDate,
                    RiskReason = longestStreak >= AtRiskAbsenceStreak
                        ? $"{longestStreak} consecutive days absent"
                        : $"Attendance {rate:0.#}% over {ordered.Count} day(s)"
                });
            }

            return rows
                .OrderByDescending(row => row.LongestAbsenceStreak)
                .ThenBy(row => row.AttendanceRate)
                .ToList();
        }

        private async Task<List<TxEmployee>> GetFacilitatorsAsync(string schoolId, CancellationToken ct)
        {
            var facilitators = await _uow.Repository<TxEmployee>().WhereAsync(
                entry => entry.SchoolId == schoolId && entry.TimeStatus == TimeStatus.Current,
                ct,
                entry => entry.User!);

            return facilitators;
        }

        private async Task<List<AxClassArm>> GetSchoolArmsAsync(string schoolId, CancellationToken ct)
        {
            return await _uow.Repository<AxClassArm>().WhereAsync(
                entry => entry.Classs != null && entry.Classs.SchoolInfoId == schoolId,
                ct,
                entry => entry.Classs!);
        }

        private async Task<TxYearlyAndTermlyStatus?> GetCurrentTermAsync(string schoolId, CancellationToken ct)
        {
            return await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                entry => entry.Session != null
                    && entry.Session.SchoolInfoId == schoolId
                    && (entry.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                        || entry.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing),
                ct,
                entry => entry.Session!);
        }

        private static DailyAttendanceRosterDto BuildRoster(
            DateTime date,
            string classArmId,
            string armLabel,
            List<DailyAttendanceRosterEntryDto> entries)
        {
            return new DailyAttendanceRosterDto
            {
                AttendanceDate = date,
                ClassArmId = classArmId,
                ClassArmLabel = armLabel,
                RegisterAlreadyTaken = entries.Count > 0 && entries.All(entry => entry.AlreadyMarked),
                TotalPeople = entries.Count,
                PresentCount = entries.Count(entry => entry.Status == DailyAttendanceStatus.Present),
                AbsentCount = entries.Count(entry => entry.Status == DailyAttendanceStatus.Absent),
                LateCount = entries.Count(entry => entry.Status == DailyAttendanceStatus.Late),
                ExcusedCount = entries.Count(entry => entry.Status == DailyAttendanceStatus.Excused),
                Entries = entries
            };
        }

        /// <summary>Late and excused still count towards presence for reporting purposes.</summary>
        private static bool IsCountedPresent(DailyAttendanceStatus status)
            => status is DailyAttendanceStatus.Present or DailyAttendanceStatus.Late or DailyAttendanceStatus.Excused;

        private static double Rate(int part, int total)
            => total <= 0 ? 0d : Math.Round(part * 100d / total, 1);

        private static string BuildArmLabel(AxClassArm arm)
        {
            var className = string.IsNullOrWhiteSpace(arm.Classs?.ClassLabel)
                ? arm.Classs?.ClassName.ToString() ?? string.Empty
                : arm.Classs!.ClassLabel;

            return $"{className} {arm.ArmName}".Trim();
        }

        private static string BuildFullName(ApplicationUser? user)
        {
            if (user == null)
            {
                return string.Empty;
            }

            var parts = new[] { user.SurName, user.FirstName, user.MiddleName }
                .Where(part => !string.IsNullOrWhiteSpace(part));
            return string.Join(" ", parts).Trim();
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }

        private static DailyAttendanceSaveResultDto Failed(string message)
            => new() { Success = false, Message = message };
    }
}
