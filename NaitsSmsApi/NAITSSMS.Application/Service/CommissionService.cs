using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CustRewardMgtSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Calendar;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using Npgsql;
using NpgsqlTypes;

namespace NAITSSMS.Application.Service
{
    /// <summary>
    /// Commission oversight. Unlike the school-level services this uses <see cref="AppDbContext"/>
    /// directly: every figure is a GROUP BY across all centres, and the repository abstraction
    /// can only materialise rows. Aggregating in PostgreSQL keeps response time flat as the number
    /// of centres and register marks grows.
    /// </summary>
    public sealed class CommissionService : ICommissionService
    {
        private const int OverdueSchoolDays = 3;
        private const int AtRiskAbsenceStreak = 3;
        private const double AtRiskAttendanceRate = 60d;

        /// <summary>Attendance rate is only judged once a learner has this many register days.</summary>
        private const int AtRiskMinimumDays = 5;
        private const double DefaultPassMark = 40d;

        private readonly AppDbContext _db;

        public CommissionService(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        // ------------------------------------------------------------------------------------
        // Programme scope
        // ------------------------------------------------------------------------------------

        private sealed class CentreInfo
        {
            public required string Id { get; init; }
            public required string Name { get; init; }
            public string RegistrationNumber { get; init; } = string.Empty;
            public string Address { get; init; } = string.Empty;
            public string Telephone { get; init; } = string.Empty;
            public string CentreType { get; init; } = string.Empty;
            public string Community { get; init; } = string.Empty;
            public string State { get; init; } = string.Empty;
            public string Lga { get; init; } = string.Empty;
            public string Zone { get; init; } = string.Empty;
            public string Proprietor { get; init; } = string.Empty;
            public short? YearEstablished { get; init; }
        }

        /// <summary>
        /// Programme centres are schools with a <see cref="ProgrammeCentreProfile"/>. A database with
        /// no profiles yet falls back to schools that keep a daily register, the earlier rule.
        /// </summary>
        private async Task<Dictionary<string, CentreInfo>> LoadCentresAsync(CancellationToken ct)
        {
            var profiled = await (
                    from profile in _db.ProgrammeCentreProfiles.AsNoTracking()
                    join school in _db.SchoolInfos.AsNoTracking()
                        on profile.SchoolInfoId equals school.SchoolRegistrationOfficerId
                    join state in _db.States.AsNoTracking() on profile.StateId equals state.Id into states
                    from state in states.DefaultIfEmpty()
                    join region in _db.Regions.AsNoTracking() on state.RegionId equals region.Id into regions
                    from region in regions.DefaultIfEmpty()
                    join lga in _db.LocalGovernments.AsNoTracking() on profile.LocalGovernmentId equals (short?)lga.Id into lgas
                    from lga in lgas.DefaultIfEmpty()
                    select new
                    {
                        school.SchoolRegistrationOfficerId,
                        school.SchoolName,
                        school.SsNo,
                        school.SchoolAddress,
                        school.Telephone,
                        profile.CentreType,
                        profile.Community,
                        profile.ProprietorName,
                        profile.YearEstablished,
                        StateName = state != null ? state.Name : string.Empty,
                        RegionName = region != null ? region.Name : string.Empty,
                        LgaName = lga != null ? lga.Name : string.Empty,
                    })
                .ToListAsync(ct);

            if (profiled.Count > 0)
            {
                return profiled.ToDictionary(
                    row => row.SchoolRegistrationOfficerId,
                    row => new CentreInfo
                    {
                        Id = row.SchoolRegistrationOfficerId,
                        Name = row.SchoolName,
                        RegistrationNumber = row.SsNo.ToString(),
                        Address = row.SchoolAddress,
                        Telephone = row.Telephone,
                        CentreType = row.CentreType,
                        Community = row.Community,
                        State = TidyStateName(row.StateName),
                        Lga = row.LgaName,
                        Zone = ZoneName(row.RegionName),
                        Proprietor = row.ProprietorName,
                        YearEstablished = row.YearEstablished,
                    },
                    StringComparer.OrdinalIgnoreCase);
            }

            var registerCentres = await _db.LearnerDailyAttendances.AsNoTracking()
                .Select(mark => mark.SchoolInfoId)
                .Distinct()
                .ToListAsync(ct);

            return await _db.SchoolInfos.AsNoTracking()
                .Where(school => registerCentres.Contains(school.SchoolRegistrationOfficerId))
                .ToDictionaryAsync(
                    school => school.SchoolRegistrationOfficerId,
                    school => new CentreInfo
                    {
                        Id = school.SchoolRegistrationOfficerId,
                        Name = school.SchoolName,
                        RegistrationNumber = school.SsNo.ToString(),
                        Address = school.SchoolAddress,
                        Telephone = school.Telephone,
                    },
                    StringComparer.OrdinalIgnoreCase,
                    ct);
        }

        // ------------------------------------------------------------------------------------
        // Directory
        // ------------------------------------------------------------------------------------

        public async Task<CommissionCentresDirectoryDto> GetCentresDirectoryAsync(CancellationToken ct = default)
        {
            var centres = await LoadCentresAsync(ct);
            var ids = centres.Keys.ToList();

            var enrolment = await LoadEnrolmentAsync(ids, ct);
            var facilitators = await LoadFacilitatorCountsAsync(ids, ct);

            var rows = centres.Values
                .Select(centre =>
                {
                    enrolment.TryGetValue(centre.Id, out var roll);
                    return new CommissionCentreDirectoryRowDto
                    {
                        CentreId = centre.Id,
                        CentreName = centre.Name,
                        RegistrationNumber = centre.RegistrationNumber,
                        Location = centre.Address,
                        State = centre.State,
                        Lga = centre.Lga,
                        Zone = centre.Zone,
                        CentreType = centre.CentreType,
                        RegistrarUserId = centre.Id,
                        Learners = roll.Female + roll.Male,
                        FemaleLearners = roll.Female,
                        MaleLearners = roll.Male,
                        Facilitators = facilitators.GetValueOrDefault(centre.Id),
                    };
                })
                .OrderBy(row => row.Zone, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.State, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.CentreName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new CommissionCentresDirectoryDto
            {
                TotalCentres = rows.Count,
                TotalLearners = rows.Sum(row => row.Learners),
                TotalFemaleLearners = rows.Sum(row => row.FemaleLearners),
                TotalFacilitators = rows.Sum(row => row.Facilitators),
                States = rows.Select(row => row.State).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                Zones = rows.Select(row => row.Zone).Where(z => z.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                Centres = rows,
            };
        }

        private async Task<Dictionary<string, (int Female, int Male)>> LoadEnrolmentAsync(
            List<string> centreIds, CancellationToken ct)
        {
            var rows = await (
                    from student in _db.StudentsDetails.AsNoTracking()
                    join user in _db.Users.AsNoTracking() on student.UserId equals user.Id
                    where student.TimeStatus == TimeStatus.Current && centreIds.Contains(student.SchoolInfoId)
                    group student by new { student.SchoolInfoId, user.Gender } into g
                    select new { g.Key.SchoolInfoId, g.Key.Gender, Count = g.Count() })
                .ToListAsync(ct);

            return rows
                .GroupBy(row => row.SchoolInfoId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => (g.Where(r => r.Gender == Gender.Female).Sum(r => r.Count),
                        g.Where(r => r.Gender == Gender.Male).Sum(r => r.Count)),
                    StringComparer.OrdinalIgnoreCase);
        }

        private async Task<Dictionary<string, int>> LoadFacilitatorCountsAsync(List<string> centreIds, CancellationToken ct)
        {
            return await _db.ClassTeachers.AsNoTracking()
                .Where(employee => employee.TimeStatus == TimeStatus.Current && centreIds.Contains(employee.SchoolId))
                .GroupBy(employee => employee.SchoolId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(row => row.Key, row => row.Count, StringComparer.OrdinalIgnoreCase, ct);
        }

        // ------------------------------------------------------------------------------------
        // Oversight
        // ------------------------------------------------------------------------------------

        public async Task<CommissionOversightDto> GetOversightAsync(
            DateTime? from, DateTime? to, CancellationToken ct = default)
        {
            var today = DateTime.Today;
            var (fromDate, toDate) = ResolveRange(from, to, today);

            var centres = await LoadCentresAsync(ct);
            var ids = centres.Keys.ToList();

            var enrolment = await LoadEnrolmentAsync(ids, ct);
            var facilitatorCounts = await LoadFacilitatorCountsAsync(ids, ct);

            // One pass over each register table; every figure below is folded from these rows.
            var learnerFacts = await LoadLearnerRegisterFactsAsync(ids, fromDate, toDate, ct);
            var facilitatorFacts = await LoadFacilitatorRegisterFactsAsync(ids, fromDate, toDate, ct);
            var lastRegister = await LoadLastRegisterDatesAsync(ids, today, ct);
            var lastStaffRegister = await LoadLastRegisterDatesAsync(ids, today, ct, staffRegister: true);

            var learnerByCentre = learnerFacts
                .GroupBy(f => f.CentreId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => new MarkTotals(g.Sum(f => f.Total), g.Sum(f => f.Present), g.Select(f => f.Day).Distinct().Count()),
                    StringComparer.OrdinalIgnoreCase);

            var facilitatorByCentre = facilitatorFacts
                .GroupBy(f => f.CentreId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => new MarkTotals(g.Sum(f => f.Total), g.Sum(f => f.Present), g.Select(f => f.Day).Distinct().Count()),
                    StringComparer.OrdinalIgnoreCase);

            var reportDay = SchoolCalendar.LatestSchoolDayOnOrBefore(today);
            var learnersToday = learnerFacts
                .Where(f => f.Day == reportDay)
                .GroupBy(f => f.CentreId, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { CentreId = g.Key, Present = g.Sum(f => f.Present), Absent = g.Sum(f => f.Total - f.Present) })
                .ToList();

            var facilitatorsTodayRows = facilitatorFacts.Where(f => f.Day == reportDay).ToList();
            var facilitatorsToday = facilitatorsTodayRows.Count == 0
                ? null
                : new
                {
                    Present = facilitatorsTodayRows.Sum(f => f.Present),
                    Absent = facilitatorsTodayRows.Sum(f => f.Total - f.Present),
                };

            var byGender = learnerFacts
                .GroupBy(f => f.Gender)
                .Select(g => new { Gender = (Gender)g.Key, Total = g.Sum(f => f.Total), Present = g.Sum(f => f.Present) })
                .ToList();

            var learnerTrend = learnerFacts
                .GroupBy(f => f.Day)
                .Select(g => new
                {
                    Date = g.Key,
                    Total = g.Sum(f => f.Total),
                    Present = g.Sum(f => f.Present),
                    Absent = g.Sum(f => f.Total - f.Present),
                })
                .ToList();

            var facilitatorTrend = facilitatorFacts
                .GroupBy(f => f.Day)
                .ToDictionary(g => g.Key, g => new { Total = g.Sum(f => f.Total), Present = g.Sum(f => f.Present) });

            var submittedByArm = learnerFacts
                .GroupBy(f => f.ClassArmId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(f => f.Day.Date).ToHashSet(), StringComparer.OrdinalIgnoreCase);

            var staffSubmittedByCentre = facilitatorFacts
                .GroupBy(f => f.CentreId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(f => f.Day.Date).ToHashSet(), StringComparer.OrdinalIgnoreCase);

            var activeTerms = await (
                    from term in _db.YearlyAndTermlyAssessmentStatus.AsNoTracking()
                    join session in _db.AcademicSessions.AsNoTracking() on term.SessionId equals session.Id
                    where ids.Contains(session.SchoolInfoId)
                        && (term.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                            || term.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing)
                    select term.Id)
                .CountAsync(ct);

            // The national list is capped at the most urgent learners; the counts stay exact.
            var (atRisk, atRiskByCentre, atRiskTotal) =
                await LoadAtRiskAsync(ids, fromDate, toDate, centres, NationalAtRiskListLimit, ct);

            var classArmStatuses = await BuildClassArmStatusesAsync(
                ids, centres, fromDate, toDate, today, submittedByArm, staffSubmittedByCentre, ct);
            var transitions = await LoadTransitionsAsync(ids, ct);

            var centreRows = new List<CommissionCentreRowDto>();
            var markTotals = new Dictionary<string, (int LT, int LP, int FT, int FP)>(StringComparer.OrdinalIgnoreCase);
            foreach (var centre in centres.Values)
            {
                enrolment.TryGetValue(centre.Id, out var roll);
                learnerByCentre.TryGetValue(centre.Id, out var learnerMarks);
                facilitatorByCentre.TryGetValue(centre.Id, out var facilitatorMarks);
                var classRegister = RegisterStanding(lastRegister, centre.Id, today, expected: true);
                var staffRegister = RegisterStanding(
                    lastStaffRegister, centre.Id, today, expected: facilitatorCounts.GetValueOrDefault(centre.Id) > 0);

                markTotals[centre.Id] = (learnerMarks?.Total ?? 0, learnerMarks?.Present ?? 0,
                    facilitatorMarks?.Total ?? 0, facilitatorMarks?.Present ?? 0);

                centreRows.Add(new CommissionCentreRowDto
                {
                    CentreId = centre.Id,
                    CentreName = centre.Name,
                    RegistrationNumber = centre.RegistrationNumber,
                    Location = centre.Address,
                    State = centre.State,
                    Zone = centre.Zone,
                    CentreType = centre.CentreType,
                    RegistrarUserId = centre.Id,
                    Learners = roll.Female + roll.Male,
                    FemaleLearners = roll.Female,
                    Facilitators = facilitatorCounts.GetValueOrDefault(centre.Id),
                    DaysRegistered = learnerMarks?.Days ?? 0,
                    LearnerAttendanceRate = Rate(learnerMarks?.Present ?? 0, learnerMarks?.Total ?? 0),
                    FacilitatorAttendanceRate = Rate(facilitatorMarks?.Present ?? 0, facilitatorMarks?.Total ?? 0),
                    AtRiskLearners = atRiskByCentre.GetValueOrDefault(centre.Id),
                    LastRegisterDate = classRegister.Last,
                    DaysSinceLastRegister = classRegister.MissedDays,
                    LastStaffRegisterDate = staffRegister.Last,
                    StaffDaysSinceLastRegister = staffRegister.MissedDays,
                    NeverSubmitted = classRegister.Last == null,
                    ClassRegisterOverdue = classRegister.Overdue,
                    StaffRegisterOverdue = staffRegister.Overdue,
                    RegisterOverdue = classRegister.Overdue || staffRegister.Overdue,
                });
            }

            var defaultedCentres = classArmStatuses
                .Where(status => status.MissingDates.Count > 0)
                .GroupBy(status => new { status.CentreId, status.CentreName })
                .Select(group => new CommissionDefaultedCentreDto
                {
                    CentreId = group.Key.CentreId,
                    CentreName = group.Key.CentreName,
                    MissingSubmissionCount = group.Sum(status => status.MissingDates.Count),
                    AffectedClassArms = group.Count(),
                    ClassArms = group
                        .OrderBy(status => status.IsStaffRegister)
                        .ThenBy(status => status.ClassName, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(status => status.ClassArmName, StringComparer.CurrentCultureIgnoreCase)
                        .ToList(),
                })
                .OrderByDescending(centre => centre.MissingSubmissionCount)
                .ThenBy(centre => centre.CentreName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var female = byGender.FirstOrDefault(row => row.Gender == Gender.Female);
            var male = byGender.FirstOrDefault(row => row.Gender == Gender.Male);
            var learnerTotal = learnerByCentre.Values.Sum(row => row.Total);
            var learnerPresent = learnerByCentre.Values.Sum(row => row.Present);
            var facilitatorTotal = facilitatorByCentre.Values.Sum(row => row.Total);
            var facilitatorPresent = facilitatorByCentre.Values.Sum(row => row.Present);
            var presentToday = learnersToday.Sum(row => row.Present);
            var absentToday = learnersToday.Sum(row => row.Absent);

            return new CommissionOversightDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                AsAtDate = today,
                ReportDate = reportDay,
                TotalCentres = centres.Count,
                TotalLearners = centreRows.Sum(row => row.Learners),
                FemaleLearners = centreRows.Sum(row => row.FemaleLearners),
                MaleLearners = centreRows.Sum(row => row.Learners - row.FemaleLearners),
                TotalFacilitators = centreRows.Sum(row => row.Facilitators),
                ActiveTerms = activeTerms,
                LearnersPresentToday = presentToday,
                LearnersAbsentToday = absentToday,
                LearnerAttendanceRateToday = Rate(presentToday, presentToday + absentToday),
                FacilitatorsPresentToday = facilitatorsToday?.Present ?? 0,
                FacilitatorsAbsentToday = facilitatorsToday?.Absent ?? 0,
                FacilitatorAttendanceRateToday = Rate(
                    facilitatorsToday?.Present ?? 0,
                    (facilitatorsToday?.Present ?? 0) + (facilitatorsToday?.Absent ?? 0)),
                CentresReportingToday = learnersToday.Count,
                LearnerAttendanceRate = Rate(learnerPresent, learnerTotal),
                FemaleAttendanceRate = Rate(female?.Present ?? 0, female?.Total ?? 0),
                MaleAttendanceRate = Rate(male?.Present ?? 0, male?.Total ?? 0),
                FacilitatorAttendanceRate = Rate(facilitatorPresent, facilitatorTotal),
                TotalAtRiskLearners = atRiskTotal,
                CentresWithOverdueRegister = centreRows.Count(row => row.RegisterOverdue),
                Transitions = transitions,
                Centres = centreRows
                    .OrderBy(row => row.RegisterOverdue ? 0 : 1)
                    .ThenBy(row => row.LearnerAttendanceRate)
                    .ThenByDescending(row => row.AtRiskLearners)
                    .ToList(),
                StatesSummary = SummariseAreas(centreRows, markTotals, row => row.State),
                ZonesSummary = SummariseAreas(centreRows, markTotals, row => row.Zone),
                DailyTrend = learnerTrend
                    .OrderBy(point => point.Date)
                    .Select(point => new CommissionDayPointDto
                    {
                        Date = point.Date,
                        LearnersPresent = point.Present,
                        LearnersAbsent = point.Absent,
                        LearnerAttendanceRate = Rate(point.Present, point.Total),
                        FacilitatorAttendanceRate = facilitatorTrend.TryGetValue(point.Date, out var f)
                            ? Rate(f.Present, f.Total)
                            : 0,
                    })
                    .ToList(),
                EnrolmentByCentre = centreRows
                    .OrderByDescending(row => row.Learners)
                    .Select(row => new CommissionEnrolmentPointDto
                    {
                        CentreId = row.CentreId,
                        CentreName = row.CentreName,
                        Learners = row.Learners,
                        Facilitators = row.Facilitators,
                    })
                    .ToList(),
                AtRiskLearners = atRisk,
                DefaultedCentres = defaultedCentres,
                ClassArmStatuses = classArmStatuses,
            };
        }

        private const int NationalAtRiskListLimit = 500;

        private sealed record MarkTotals(int Total, int Present, int Days);

        private sealed class RegisterFact
        {
            public string CentreId { get; set; } = string.Empty;
            public string ClassArmId { get; set; } = string.Empty;
            public DateTime Day { get; set; }
            public short Gender { get; set; }
            public int Total { get; set; }
            public int Present { get; set; }
        }

        private sealed class LastRegisterRow
        {
            public string CentreId { get; set; } = string.Empty;
            public DateTime? Last { get; set; }
        }

        /// <summary>Learner marks folded to one row per centre, class arm, day and gender.</summary>
        private Task<List<RegisterFact>> LoadLearnerRegisterFactsAsync(
            List<string> centreIds, DateTime fromDate, DateTime toDate, CancellationToken ct)
        {
            const string sql = """
                SELECT m."SchoolInfoId" AS "CentreId", m."ClassArmId" AS "ClassArmId",
                       m."AttendanceDate"::timestamp AS "Day", u."Gender" AS "Gender",
                       COUNT(*)::int AS "Total", COUNT(*) FILTER (WHERE m."Status" <> 1)::int AS "Present"
                FROM "LearnerDailyAttendances" m
                JOIN "AspNetUsers" u ON u."Id" = m."StudentId"
                WHERE m."AttendanceDate" BETWEEN @from AND @to AND m."SchoolInfoId" = ANY(@centres)
                GROUP BY 1, 2, 3, 4
                """;
            return _db.Database.SqlQueryRaw<RegisterFact>(sql, RangeParameters(centreIds, fromDate, toDate)).ToListAsync(ct);
        }

        /// <summary>Facilitator marks folded to one row per centre and day.</summary>
        private Task<List<RegisterFact>> LoadFacilitatorRegisterFactsAsync(
            List<string> centreIds, DateTime fromDate, DateTime toDate, CancellationToken ct)
        {
            const string sql = """
                SELECT m."SchoolInfoId" AS "CentreId", '' AS "ClassArmId", m."AttendanceDate"::timestamp AS "Day",
                       0::smallint AS "Gender",
                       COUNT(*)::int AS "Total", COUNT(*) FILTER (WHERE m."Status" <> 1)::int AS "Present"
                FROM "FacilitatorDailyAttendances" m
                WHERE m."AttendanceDate" BETWEEN @from AND @to AND m."SchoolInfoId" = ANY(@centres)
                GROUP BY m."SchoolInfoId", m."AttendanceDate"
                """;
            return _db.Database.SqlQueryRaw<RegisterFact>(sql, RangeParameters(centreIds, fromDate, toDate)).ToListAsync(ct);
        }

        /// <summary>
        /// Each centre's most recent register ever (not just in the window), so a stalled centre is
        /// caught either way. One index probe per centre on the (SchoolInfoId, AttendanceDate) index
        /// of the learner or facilitator register.
        /// </summary>
        private async Task<Dictionary<string, DateTime>> LoadLastRegisterDatesAsync(
            List<string> centreIds, DateTime today, CancellationToken ct, bool staffRegister = false)
        {
            var sql = staffRegister ? LastStaffRegisterSql : LastClassRegisterSql;
            var rows = await _db.Database
                .SqlQueryRaw<LastRegisterRow>(
                    sql,
                    new NpgsqlParameter("centres", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = centreIds.ToArray() },
                    new NpgsqlParameter("today", NpgsqlDbType.Date) { Value = today.Date })
                .ToListAsync(ct);

            return rows
                .Where(row => row.Last != null)
                .ToDictionary(row => row.CentreId, row => row.Last!.Value, StringComparer.OrdinalIgnoreCase);
        }

        private const string LastClassRegisterSql = """
            SELECT c AS "CentreId",
                   (SELECT MAX(m."AttendanceDate") FROM "LearnerDailyAttendances" m
                    WHERE m."SchoolInfoId" = c AND m."AttendanceDate" <= @today)::timestamp AS "Last"
            FROM unnest(@centres) AS c
            """;

        private const string LastStaffRegisterSql = """
            SELECT c AS "CentreId",
                   (SELECT MAX(m."AttendanceDate") FROM "FacilitatorDailyAttendances" m
                    WHERE m."SchoolInfoId" = c AND m."AttendanceDate" <= @today)::timestamp AS "Last"
            FROM unnest(@centres) AS c
            """;

        /// <summary>
        /// Standing of one register type for a centre: when it was last kept and whether it is
        /// overdue. Centres with nobody to register (no facilitators) are never overdue on staff.
        /// </summary>
        private static (DateTime? Last, int MissedDays, bool Overdue) RegisterStanding(
            IReadOnlyDictionary<string, DateTime> lastByCentre, string centreId, DateTime today, bool expected)
        {
            DateTime? last = lastByCentre.TryGetValue(centreId, out var date) ? date : null;
            var missed = last == null ? 0 : SchoolDaysMissedSince(last.Value, today);
            var overdue = expected && (last == null || missed >= OverdueSchoolDays);
            return (last, missed, overdue);
        }

        private static object[] RangeParameters(List<string> centreIds, DateTime fromDate, DateTime toDate) =>
        [
            new NpgsqlParameter("from", NpgsqlDbType.Date) { Value = fromDate.Date },
            new NpgsqlParameter("to", NpgsqlDbType.Date) { Value = toDate.Date },
            new NpgsqlParameter("centres", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = centreIds.ToArray() },
        ];

        private static List<CommissionAreaRowDto> SummariseAreas(
            List<CommissionCentreRowDto> rows,
            IReadOnlyDictionary<string, (int LT, int LP, int FT, int FP)> marks,
            Func<CommissionCentreRowDto, string> key)
        {
            return rows
                .Where(row => key(row).Length > 0)
                .GroupBy(key, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var totals = g.Select(row => marks.GetValueOrDefault(row.CentreId)).ToList();
                    return new CommissionAreaRowDto
                    {
                        Name = g.Key,
                        Zone = g.First().Zone,
                        Centres = g.Count(),
                        Learners = g.Sum(row => row.Learners),
                        FemaleLearners = g.Sum(row => row.FemaleLearners),
                        Facilitators = g.Sum(row => row.Facilitators),
                        LearnerAttendanceRate = Rate(totals.Sum(t => t.LP), totals.Sum(t => t.LT)),
                        FacilitatorAttendanceRate = Rate(totals.Sum(t => t.FP), totals.Sum(t => t.FT)),
                        AtRiskLearners = g.Sum(row => row.AtRiskLearners),
                        CentresOverdue = g.Count(row => row.RegisterOverdue),
                    };
                })
                .OrderBy(area => area.LearnerAttendanceRate)
                .ToList();
        }

        private async Task<CommissionTransitionSummaryDto> LoadTransitionsAsync(List<string> centreIds, CancellationToken ct)
        {
            var counts = await _db.ProgrammeLearnerProfiles.AsNoTracking()
                .Where(profile => centreIds.Contains(profile.SchoolInfoId)
                    && profile.TransitionStatus != LearnerTransitionStatus.Active)
                .GroupBy(profile => profile.TransitionStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(row => row.Status, row => row.Count, ct);

            var formal = counts.GetValueOrDefault(LearnerTransitionStatus.TransitionedToFormalSchool);
            var vocational = counts.GetValueOrDefault(LearnerTransitionStatus.TransitionedToVocational);
            var withdrawn = counts.GetValueOrDefault(LearnerTransitionStatus.Withdrawn);
            var leavers = formal + vocational + withdrawn;

            return new CommissionTransitionSummaryDto
            {
                Leavers = leavers,
                ToFormalSchool = formal,
                ToVocational = vocational,
                Withdrawn = withdrawn,
                TransitionRate = Rate(formal + vocational, leavers),
            };
        }

        // ------------------------------------------------------------------------------------
        // At-risk learners (computed in PostgreSQL)
        // ------------------------------------------------------------------------------------

        private sealed class AtRiskRow
        {
            public string StudentId { get; set; } = string.Empty;
            public string CentreId { get; set; } = string.Empty;
            public string ClassArmId { get; set; } = string.Empty;
            public int Total { get; set; }
            public int Present { get; set; }
            public DateTime? LastPresent { get; set; }
            public int Longest { get; set; }
            public int Current { get; set; }
        }

        /// <summary>
        /// Same rule as the centre-level summary: a run of three or more absences, or attendance
        /// below 60% once there are five register days to judge. Absence runs are found with the
        /// gaps-and-islands technique, and only learners with enough absences to qualify are
        /// windowed, so the work tracks the flagged few rather than the whole register. Counts are exact; <paramref name="listLimit"/> caps how many
        /// named learners are returned (most urgent first).
        /// </summary>
        private async Task<(List<CommissionAtRiskLearnerDto> Learners, Dictionary<string, int> CountByCentre, int Total)> LoadAtRiskAsync(
            List<string> centreIds,
            DateTime fromDate,
            DateTime toDate,
            IReadOnlyDictionary<string, CentreInfo> centres,
            int? listLimit,
            CancellationToken ct)
        {
            var none = (new List<CommissionAtRiskLearnerDto>(), new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), 0);
            if (centreIds.Count == 0)
            {
                return none;
            }

            const string sql = """
                WITH candidates AS (
                    -- Both rules need at least @streak absences (a run of them, or a sub-60% rate over
                    -- at least @minDays days), so only learners with that many are examined.
                    SELECT a."StudentId"
                    FROM "LearnerDailyAttendances" a
                    WHERE a."Status" = 1
                      AND a."AttendanceDate" BETWEEN @from AND @to
                      AND a."SchoolInfoId" = ANY(@centres)
                    GROUP BY a."StudentId"
                    HAVING COUNT(*) >= @streak
                ),
                marks AS (
                    SELECT m."StudentId", m."SchoolInfoId", m."ClassArmId", m."AttendanceDate", m."Status",
                           ROW_NUMBER() OVER (PARTITION BY m."StudentId" ORDER BY m."AttendanceDate")
                         - ROW_NUMBER() OVER (PARTITION BY m."StudentId", (m."Status" = 1) ORDER BY m."AttendanceDate") AS grp
                    FROM candidates c
                    JOIN "LearnerDailyAttendances" m ON m."StudentId" = c."StudentId"
                    WHERE m."AttendanceDate" BETWEEN @from AND @to
                ),
                per_learner AS (
                    SELECT "StudentId",
                           COUNT(*) AS total,
                           COUNT(*) FILTER (WHERE "Status" <> 1) AS present,
                           MAX("AttendanceDate") FILTER (WHERE "Status" <> 1) AS last_present,
                           (ARRAY_AGG("SchoolInfoId" ORDER BY "AttendanceDate" DESC))[1] AS centre,
                           (ARRAY_AGG("ClassArmId" ORDER BY "AttendanceDate" DESC))[1] AS arm
                    FROM marks
                    GROUP BY "StudentId"
                ),
                runs AS (
                    SELECT "StudentId", MAX(run_length) AS longest
                    FROM (
                        SELECT "StudentId", grp, COUNT(*) AS run_length
                        FROM marks
                        WHERE "Status" = 1
                        GROUP BY "StudentId", grp
                    ) r
                    GROUP BY "StudentId"
                ),
                ongoing AS (
                    SELECT m."StudentId", COUNT(*) AS current_run
                    FROM marks m
                    JOIN per_learner p ON p."StudentId" = m."StudentId"
                    WHERE m."AttendanceDate" > COALESCE(p.last_present, DATE '1900-01-01')
                    GROUP BY m."StudentId"
                )
                SELECT p."StudentId" AS "StudentId",
                       p.centre AS "CentreId",
                       p.arm AS "ClassArmId",
                       p.total::int AS "Total",
                       p.present::int AS "Present",
                       p.last_present::timestamp AS "LastPresent",
                       COALESCE(r.longest, 0)::int AS "Longest",
                       COALESCE(o.current_run, 0)::int AS "Current"
                FROM per_learner p
                LEFT JOIN runs r ON r."StudentId" = p."StudentId"
                LEFT JOIN ongoing o ON o."StudentId" = p."StudentId"
                WHERE COALESCE(r.longest, 0) >= @streak
                   OR (p.total >= @minDays AND p.present * 100.0 / p.total < @rate)
                """;

            var rows = await _db.Database
                .SqlQueryRaw<AtRiskRow>(
                    sql,
                    new NpgsqlParameter("from", NpgsqlDbType.Date) { Value = fromDate.Date },
                    new NpgsqlParameter("to", NpgsqlDbType.Date) { Value = toDate.Date },
                    new NpgsqlParameter("centres", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = centreIds.ToArray() },
                    new NpgsqlParameter("streak", NpgsqlDbType.Integer) { Value = AtRiskAbsenceStreak },
                    new NpgsqlParameter("rate", NpgsqlDbType.Double) { Value = AtRiskAttendanceRate },
                    new NpgsqlParameter("minDays", NpgsqlDbType.Integer) { Value = AtRiskMinimumDays })
                .ToListAsync(ct);

            if (rows.Count == 0)
            {
                return none;
            }

            var countByCentre = rows
                .GroupBy(row => row.CentreId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            // Most urgent first: an ongoing absence run, then the lowest attendance.
            var ordered = rows
                .OrderByDescending(row => row.Current)
                .ThenBy(row => row.Total == 0 ? 0 : row.Present * 1.0 / row.Total)
                .ToList();
            if (listLimit is int limit && ordered.Count > limit)
            {
                ordered = ordered.Take(limit).ToList();
            }

            var studentIds = ordered.Select(row => row.StudentId).ToList();
            var people = await (
                    from student in _db.StudentsDetails.AsNoTracking()
                    join user in _db.Users.AsNoTracking() on student.UserId equals user.Id
                    where studentIds.Contains(student.UserId)
                    select new { student.UserId, student.IdNo, user.FirstName, user.MiddleName, user.SurName })
                .ToDictionaryAsync(row => row.UserId, StringComparer.OrdinalIgnoreCase, ct);

            var armIds = ordered.Select(row => row.ClassArmId).Distinct().ToList();
            var armLabels = await LoadArmLabelsAsync(armIds, ct);

            var learners = ordered
                .Select(row =>
                {
                    people.TryGetValue(row.StudentId, out var person);
                    var rate = Rate(row.Present, row.Total);
                    return new CommissionAtRiskLearnerDto
                    {
                        CentreId = row.CentreId,
                        CentreName = centres.TryGetValue(row.CentreId, out var centre) ? centre.Name : string.Empty,
                        ClassArmLabel = armLabels.GetValueOrDefault(row.ClassArmId) ?? string.Empty,
                        StudentId = row.StudentId,
                        FullName = person == null ? string.Empty : FullName(person.SurName, person.FirstName, person.MiddleName),
                        IdentificationNo = person?.IdNo ?? string.Empty,
                        AttendanceRate = rate,
                        CurrentAbsenceStreak = row.Current,
                        LongestAbsenceStreak = row.Longest,
                        LastPresentDate = row.LastPresent,
                        Reason = row.Current >= AtRiskAbsenceStreak
                            ? $"Absent {row.Current} school days in a row (ongoing)"
                            : row.Longest >= AtRiskAbsenceStreak
                                ? $"Missed {row.Longest} school days in a row"
                                : $"Attendance {rate:0.#}% over {row.Total} days",
                    };
                })
                .OrderByDescending(row => row.CurrentAbsenceStreak)
                .ThenBy(row => row.AttendanceRate)
                .ThenBy(row => row.CentreName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return (learners, countByCentre, rows.Count);
        }

        private async Task<Dictionary<string, string>> LoadArmLabelsAsync(List<string> armIds, CancellationToken ct)
        {
            var arms = await (
                    from arm in _db.ClassArms.AsNoTracking()
                    join classRow in _db.Classes.AsNoTracking() on arm.ClassId equals classRow.Id
                    where armIds.Contains(arm.Id)
                    select new { arm.Id, classRow.ClassLabel, classRow.ClassName, arm.ArmName })
                .ToListAsync(ct);

            return arms.ToDictionary(
                arm => arm.Id,
                arm => $"{(string.IsNullOrWhiteSpace(arm.ClassLabel) ? arm.ClassName.ToString() : arm.ClassLabel)} {arm.ArmName}".Trim(),
                StringComparer.OrdinalIgnoreCase);
        }

        // ------------------------------------------------------------------------------------
        // Register submission by class arm
        // ------------------------------------------------------------------------------------

        private async Task<List<CommissionClassArmAttendanceStatusDto>> BuildClassArmStatusesAsync(
            List<string> centreIds,
            IReadOnlyDictionary<string, CentreInfo> centres,
            DateTime fromDate,
            DateTime toDate,
            DateTime today,
            IReadOnlyDictionary<string, HashSet<DateTime>>? submittedByArm,
            IReadOnlyDictionary<string, HashSet<DateTime>>? staffSubmittedByCentre,
            CancellationToken ct)
        {
            var learnersByArm = await _db.StudentsDetails.AsNoTracking()
                .Where(student => student.TimeStatus == TimeStatus.Current && centreIds.Contains(student.SchoolInfoId))
                .GroupBy(student => student.ClassArmId)
                .Select(g => new { ArmId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(row => row.ArmId, row => row.Count, StringComparer.OrdinalIgnoreCase, ct);

            var armIds = learnersByArm.Keys.ToList();

            var arms = await (
                    from arm in _db.ClassArms.AsNoTracking()
                    join classRow in _db.Classes.AsNoTracking() on arm.ClassId equals classRow.Id
                    where armIds.Contains(arm.Id)
                    select new { arm.Id, arm.ArmName, arm.FormMaster, classRow.SchoolInfoId, classRow.ClassLabel, classRow.ClassName, classRow.SNo })
                .ToListAsync(ct);

            var formMasters = await (
                    from assignment in _db.TeacherClassArmAssignments.AsNoTracking()
                    join employee in _db.ClassTeachers.AsNoTracking() on assignment.EmployeeId equals employee.Id
                    join user in _db.Users.AsNoTracking() on employee.UserId equals user.Id
                    where armIds.Contains(assignment.ArmId) && employee.TimeStatus == TimeStatus.Current
                    select new { assignment.ArmId, employee.Id, user.FirstName, user.MiddleName, user.SurName })
                .ToListAsync(ct);
            var formMasterByArm = formMasters
                .GroupBy(row => row.ArmId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            if (submittedByArm == null)
            {
                var submitted = await _db.LearnerDailyAttendances.AsNoTracking()
                    .Where(m => m.AttendanceDate >= fromDate && m.AttendanceDate <= toDate && armIds.Contains(m.ClassArmId))
                    .Select(m => new { m.ClassArmId, m.AttendanceDate })
                    .Distinct()
                    .ToListAsync(ct);
                submittedByArm = submitted
                    .GroupBy(row => row.ClassArmId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Select(row => row.AttendanceDate.Date).ToHashSet(), StringComparer.OrdinalIgnoreCase);
            }

            // A centre is only expected to keep a register from the day its current term opened.
            var termOpened = await (
                    from term in _db.YearlyAndTermlyAssessmentStatus.AsNoTracking()
                    join session in _db.AcademicSessions.AsNoTracking() on term.SessionId equals session.Id
                    where centreIds.Contains(session.SchoolInfoId)
                        && (term.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                            || term.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing)
                    group term by session.SchoolInfoId into g
                    select new { CentreId = g.Key, Opened = g.Max(term => term.CreatedDate) })
                .ToDictionaryAsync(row => row.CentreId, row => row.Opened.Date, StringComparer.OrdinalIgnoreCase, ct);

            var lastExpected = toDate.Date > today ? today : toDate.Date;
            var calendarDays = SchoolCalendar.SchoolDaysBetween(fromDate, lastExpected);

            // The staff register is the centre administrator's: one per centre with facilitators.
            var facilitatorsByCentre = await _db.ClassTeachers.AsNoTracking()
                .Where(employee => employee.TimeStatus == TimeStatus.Current && centreIds.Contains(employee.SchoolId))
                .GroupBy(employee => employee.SchoolId)
                .Select(g => new { CentreId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(row => row.CentreId, row => row.Count, StringComparer.OrdinalIgnoreCase, ct);

            if (staffSubmittedByCentre == null)
            {
                var staffSubmitted = await _db.FacilitatorDailyAttendances.AsNoTracking()
                    .Where(m => m.AttendanceDate >= fromDate && m.AttendanceDate <= toDate && centreIds.Contains(m.SchoolInfoId))
                    .Select(m => new { m.SchoolInfoId, m.AttendanceDate })
                    .Distinct()
                    .ToListAsync(ct);
                staffSubmittedByCentre = staffSubmitted
                    .GroupBy(row => row.SchoolInfoId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Select(row => row.AttendanceDate.Date).ToHashSet(), StringComparer.OrdinalIgnoreCase);
            }

            var staffRows = facilitatorsByCentre
                .Select(entry =>
                {
                    var opened = termOpened.TryGetValue(entry.Key, out var date) ? date : DateTime.MinValue;
                    var expected = calendarDays.Where(day => day >= opened).ToList();
                    var submittedDates = staffSubmittedByCentre.GetValueOrDefault(entry.Key) ?? new HashSet<DateTime>();
                    var submittedCount = expected.Count(submittedDates.Contains);
                    return new CommissionClassArmAttendanceStatusDto
                    {
                        IsStaffRegister = true,
                        CentreId = entry.Key,
                        CentreName = centres.TryGetValue(entry.Key, out var centre) ? centre.Name : string.Empty,
                        ClassArmId = $"staff:{entry.Key}",
                        ClassName = "Staff register",
                        ClassArmName = "Facilitators",
                        FormMasterId = entry.Key,
                        FormMasterName = "Centre administrator",
                        EnrolledLearners = entry.Value,
                        ExpectedSubmissions = expected.Count,
                        SubmittedDates = submittedCount,
                        NeverSubmitted = submittedCount == 0 && expected.Count > 0,
                        MissingDates = expected.Where(day => !submittedDates.Contains(day)).ToList(),
                    };
                });

            return arms
                .Select(arm =>
                {
                    var opened = termOpened.TryGetValue(arm.SchoolInfoId, out var date) ? date : DateTime.MinValue;
                    var expected = calendarDays.Where(day => day >= opened).ToList();
                    var submittedDates = submittedByArm.GetValueOrDefault(arm.Id) ?? new HashSet<DateTime>();
                    var submittedCount = expected.Count(submittedDates.Contains);
                    formMasterByArm.TryGetValue(arm.Id, out var formMaster);

                    return new CommissionClassArmAttendanceStatusDto
                    {
                        CentreId = arm.SchoolInfoId,
                        CentreName = centres.TryGetValue(arm.SchoolInfoId, out var centre) ? centre.Name : string.Empty,
                        ClassArmId = arm.Id,
                        ClassName = string.IsNullOrWhiteSpace(arm.ClassLabel) ? arm.ClassName.ToString() : arm.ClassLabel,
                        ClassArmName = arm.ArmName.ToString(),
                        FormMasterId = formMaster?.Id ?? arm.FormMaster,
                        FormMasterName = formMaster == null ? string.Empty : FullName(formMaster.SurName, formMaster.FirstName, formMaster.MiddleName),
                        EnrolledLearners = learnersByArm.GetValueOrDefault(arm.Id),
                        ExpectedSubmissions = expected.Count,
                        SubmittedDates = submittedCount,
                        NeverSubmitted = submittedCount == 0 && expected.Count > 0,
                        MissingDates = expected.Where(day => !submittedDates.Contains(day)).ToList(),
                    };
                })
                .Concat(staffRows)
                .OrderBy(status => status.CentreName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(status => status.IsStaffRegister)
                .ThenBy(status => status.ClassName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(status => status.ClassArmName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        // ------------------------------------------------------------------------------------
        // Learning outcomes
        // ------------------------------------------------------------------------------------

        /// <summary>Subject results folded to one row per centre, subject, level and gender.</summary>
        private sealed class SubjectFact
        {
            public string CentreId { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public short LevelNo { get; set; }
            public short Gender { get; set; }
            public int N { get; set; }
            public double Sum { get; set; }
            public int Passed { get; set; }
            public int A { get; set; }
            public int B { get; set; }
            public int C { get; set; }
            public int D { get; set; }
            public int E { get; set; }
            public int F { get; set; }
        }

        /// <summary>Learners folded to one row per centre, level and gender, from each learner's average.</summary>
        private sealed class LearnerFact
        {
            public string CentreId { get; set; } = string.Empty;
            public short LevelNo { get; set; }
            public short Gender { get; set; }
            public int Learners { get; set; }
            public int NeedSupport { get; set; }
        }

        private sealed class TermOptionRow
        {
            public string Session { get; set; } = string.Empty;
            public short Term { get; set; }
            public bool Complete { get; set; }
        }

        /// <summary>Every recorded subject score for programme centres in one term, joined once.</summary>
        private const string TermScoresCte = """
            WITH s AS MATERIALIZED (
                SELECT ses."SchoolInfoId" AS centre, a."StudentId" AS student, sub."SubjectName" AS subject,
                       cl."SNo" AS lvl, u."Gender" AS gender,
                       (a."Ca1" + a."Ca2" + a."Ca3" + a."Exams")::float8 AS total
                FROM "StudentAccessments" a
                JOIN "YearlyAndTermlyAssessmentStatus" t ON t."Id" = a."YearlyAndTermlyStatusId"
                JOIN "AcademicSessions" ses ON ses."Id" = t."SessionId"
                JOIN "Subjects" sub ON sub."Id" = a."SubjectId"
                JOIN "ClassArms" ar ON ar."Id" = a."ClassArmId"
                JOIN "Classes" cl ON cl."Id" = ar."ClassId"
                JOIN "AspNetUsers" u ON u."Id" = a."StudentId"
                WHERE ses."SchoolInfoId" = ANY(@centres) AND ses."Session" = @session AND t."TermStatus" = @term
            )
            """;

        private static object[] TermParameters(List<string> centreIds, CommissionTermOptionDto term) =>
        [
            new NpgsqlParameter("centres", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = centreIds.ToArray() },
            new NpgsqlParameter("session", NpgsqlDbType.Text) { Value = term.Session },
            new NpgsqlParameter("term", NpgsqlDbType.Smallint) { Value = (short)term.Term },
            new NpgsqlParameter("pass", NpgsqlDbType.Double) { Value = DefaultPassMark },
        ];

        /// <summary>One row shape for both fact kinds, so a single statement can return them.</summary>
        private sealed class OutcomeFactRow
        {
            public short Kind { get; set; }
            public string CentreId { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public short LevelNo { get; set; }
            public short Gender { get; set; }
            public int N { get; set; }
            public double Sum { get; set; }
            public int Passed { get; set; }
            public int A { get; set; }
            public int B { get; set; }
            public int C { get; set; }
            public int D { get; set; }
            public int E { get; set; }
            public int F { get; set; }
            public int Learners { get; set; }
            public int NeedSupport { get; set; }
        }

        /// <summary>
        /// Subject facts (kind 0) and learner facts (kind 1) for one term from a single pass over the
        /// scores: the CTE is materialised once and both groupings read it.
        /// </summary>
        private async Task<(List<SubjectFact> Subjects, List<LearnerFact> Learners)> LoadOutcomeFactsAsync(
            List<string> centreIds, CommissionTermOptionDto term, CancellationToken ct)
        {
            const string sql = TermScoresCte + """
                SELECT 0::smallint AS "Kind", centre AS "CentreId", subject AS "Subject",
                       lvl::smallint AS "LevelNo", gender::smallint AS "Gender",
                       COUNT(*)::int AS "N", SUM(total) AS "Sum",
                       COUNT(*) FILTER (WHERE total >= @pass)::int AS "Passed",
                       COUNT(*) FILTER (WHERE total >= 70)::int AS "A",
                       COUNT(*) FILTER (WHERE total >= 60 AND total < 70)::int AS "B",
                       COUNT(*) FILTER (WHERE total >= 50 AND total < 60)::int AS "C",
                       COUNT(*) FILTER (WHERE total >= 45 AND total < 50)::int AS "D",
                       COUNT(*) FILTER (WHERE total >= 40 AND total < 45)::int AS "E",
                       COUNT(*) FILTER (WHERE total < 40)::int AS "F",
                       0 AS "Learners", 0 AS "NeedSupport"
                FROM s
                GROUP BY centre, subject, lvl, gender
                UNION ALL
                SELECT 1::smallint, centre, '', lvl::smallint, gender::smallint,
                       0, 0::float8, 0, 0, 0, 0, 0, 0, 0,
                       COUNT(*)::int, COUNT(*) FILTER (WHERE average < @pass)::int
                FROM (
                    SELECT centre, student, MAX(lvl) AS lvl, MAX(gender) AS gender, AVG(total) AS average
                    FROM s
                    GROUP BY centre, student
                ) per_learner
                GROUP BY centre, lvl, gender
                """;
            var rows = await _db.Database.SqlQueryRaw<OutcomeFactRow>(sql, TermParameters(centreIds, term)).ToListAsync(ct);

            var subjects = rows.Where(r => r.Kind == 0).Select(r => new SubjectFact
            {
                CentreId = r.CentreId, Subject = r.Subject, LevelNo = r.LevelNo, Gender = r.Gender,
                N = r.N, Sum = r.Sum, Passed = r.Passed, A = r.A, B = r.B, C = r.C, D = r.D, E = r.E, F = r.F,
            }).ToList();
            var learners = rows.Where(r => r.Kind == 1).Select(r => new LearnerFact
            {
                CentreId = r.CentreId, LevelNo = r.LevelNo, Gender = r.Gender,
                Learners = r.Learners, NeedSupport = r.NeedSupport,
            }).ToList();
            return (subjects, learners);
        }

        public async Task<CommissionOutcomesDto> GetOutcomesAsync(string? session, byte? term, CancellationToken ct = default)
        {
            var centres = await LoadCentresAsync(ct);
            var ids = centres.Keys.ToList();
            var options = await LoadTermOptionsAsync(ids, ct);

            var selected = options.FirstOrDefault(option => option.Session == session && option.Term == term)
                ?? options.FirstOrDefault(option => option.Complete)
                ?? options.FirstOrDefault();

            var result = new CommissionOutcomesDto { AvailableTerms = options, Term = selected, PassMark = DefaultPassMark };
            if (selected == null)
            {
                return result;
            }

            var previous = PreviousTerm(options, selected);
            result.PreviousTerm = previous;

            var (facts, learners) = await LoadOutcomeFactsAsync(ids, selected, ct);
            if (facts.Count == 0)
            {
                return result;
            }

            var learnersByCentre = learners
                .GroupBy(row => row.CentreId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(row => row.Learners), StringComparer.OrdinalIgnoreCase);

            var totalN = facts.Sum(row => row.N);
            result.CentresReporting = facts.Select(row => row.CentreId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            result.LearnersAssessed = learners.Sum(row => row.Learners);
            result.ScoresRecorded = totalN;
            result.AverageScore = Round1(facts.Sum(row => row.Sum) / Math.Max(1, totalN));
            result.PassRate = Rate(facts.Sum(row => row.Passed), totalN);
            result.LearnersNeedingSupport = learners.Sum(row => row.NeedSupport);

            if (previous != null)
            {
                var (previousFacts, _) = await LoadOutcomeFactsAsync(ids, previous, ct);
                var previousN = previousFacts.Sum(row => row.N);
                if (previousN > 0)
                {
                    result.PreviousAverageScore = Round1(previousFacts.Sum(row => row.Sum) / previousN);
                    result.PreviousPassRate = Rate(previousFacts.Sum(row => row.Passed), previousN);
                }
            }

            result.Grades = new[]
            {
                new CommissionGradeBandDto { Grade = "A", Range = "70–100", Count = facts.Sum(row => row.A) },
                new CommissionGradeBandDto { Grade = "B", Range = "60–69", Count = facts.Sum(row => row.B) },
                new CommissionGradeBandDto { Grade = "C", Range = "50–59", Count = facts.Sum(row => row.C) },
                new CommissionGradeBandDto { Grade = "D", Range = "45–49", Count = facts.Sum(row => row.D) },
                new CommissionGradeBandDto { Grade = "E", Range = "40–44", Count = facts.Sum(row => row.E) },
                new CommissionGradeBandDto { Grade = "F", Range = "0–39", Count = facts.Sum(row => row.F) },
            };

            result.Centres = facts
                .GroupBy(row => row.CentreId, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    centres.TryGetValue(g.Key, out var centre);
                    var subjects = g
                        .GroupBy(row => row.Subject, StringComparer.OrdinalIgnoreCase)
                        .Select(sg => (Subject: sg.Key, Average: sg.Sum(r => r.Sum) / sg.Sum(r => r.N)))
                        .OrderByDescending(x => x.Average)
                        .ToList();
                    return new CommissionOutcomeRowDto
                    {
                        Key = g.Key,
                        Name = centre?.Name ?? string.Empty,
                        Detail = centre == null ? string.Empty : $"{centre.State} · {centre.Zone}",
                        Learners = learnersByCentre.GetValueOrDefault(g.Key),
                        AverageScore = Round1(g.Sum(row => row.Sum) / g.Sum(row => row.N)),
                        PassRate = Rate(g.Sum(row => row.Passed), g.Sum(row => row.N)),
                        StrongestSubject = subjects.First().Subject,
                        WeakestSubject = subjects.Last().Subject,
                    };
                })
                .OrderByDescending(row => row.AverageScore)
                .ToList();

            result.Subjects = facts
                .GroupBy(row => row.Subject, StringComparer.OrdinalIgnoreCase)
                .Select(g => OutcomeRow(g.Key, g.Key, string.Empty, g.Sum(row => row.N), g))
                .OrderByDescending(row => row.AverageScore)
                .ToList();

            result.Genders = facts
                .GroupBy(row => row.Gender)
                .Select(g => OutcomeRow(
                    g.Key.ToString(),
                    (Gender)g.Key == Gender.Female ? "Girls" : "Boys",
                    string.Empty,
                    learners.Where(l => l.Gender == g.Key).Sum(l => l.Learners),
                    g))
                .OrderBy(row => row.Name)
                .ToList();

            result.Levels = facts
                .GroupBy(row => row.LevelNo)
                .OrderBy(g => g.Key)
                .Select(g => OutcomeRow(
                    g.Key.ToString(),
                    $"Level {g.Key}",
                    string.Empty,
                    learners.Where(l => l.LevelNo == g.Key).Sum(l => l.Learners),
                    g))
                .ToList();

            result.States = facts
                .GroupBy(row => centres.TryGetValue(row.CentreId, out var c) ? c.State : string.Empty, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Key.Length > 0)
                .Select(g => OutcomeRow(
                    g.Key,
                    g.Key,
                    centres.Values.FirstOrDefault(c => c.State == g.Key)?.Zone ?? string.Empty,
                    g.Select(row => row.CentreId).Distinct(StringComparer.OrdinalIgnoreCase).Sum(id => learnersByCentre.GetValueOrDefault(id)),
                    g))
                .OrderByDescending(row => row.AverageScore)
                .ToList();

            return result;
        }

        private static CommissionOutcomeRowDto OutcomeRow(
            string key, string name, string detail, int learners, IEnumerable<SubjectFact> rows)
        {
            var list = rows as IList<SubjectFact> ?? rows.ToList();
            var n = list.Sum(row => row.N);
            return new CommissionOutcomeRowDto
            {
                Key = key,
                Name = name,
                Detail = detail,
                Learners = learners,
                AverageScore = Round1(list.Sum(row => row.Sum) / Math.Max(1, n)),
                PassRate = Rate(list.Sum(row => row.Passed), n),
            };
        }

        private sealed record SubjectAggregate(string CentreId, string Subject, int N, double Sum, int Passed);

        private async Task<List<SubjectAggregate>> LoadSubjectAggregatesAsync(
            List<string> centreIds, CommissionTermOptionDto term, CancellationToken ct)
        {
            var (facts, _) = await LoadOutcomeFactsAsync(centreIds, term, ct);
            return facts
                .GroupBy(row => (row.CentreId, row.Subject))
                .Select(g => new SubjectAggregate(g.Key.CentreId, g.Key.Subject, g.Sum(r => r.N), g.Sum(r => r.Sum), g.Sum(r => r.Passed)))
                .ToList();
        }

        /// <summary>
        /// Terms that have any scores at programme centres; a term is complete once examinations are
        /// recorded. Index probes per term, so this does not scan the score table.
        /// </summary>
        private async Task<List<CommissionTermOptionDto>> LoadTermOptionsAsync(List<string> centreIds, CancellationToken ct)
        {
            const string sql = """
                SELECT ses."Session" AS "Session", t."TermStatus"::smallint AS "Term",
                       BOOL_OR(EXISTS (SELECT 1 FROM "StudentAccessments" a
                                       WHERE a."YearlyAndTermlyStatusId" = t."Id" AND a."Exams" > 0)) AS "Complete"
                FROM "YearlyAndTermlyAssessmentStatus" t
                JOIN "AcademicSessions" ses ON ses."Id" = t."SessionId"
                WHERE ses."SchoolInfoId" = ANY(@centres)
                  AND EXISTS (SELECT 1 FROM "StudentAccessments" a WHERE a."YearlyAndTermlyStatusId" = t."Id")
                GROUP BY 1, 2
                """;
            var rows = await _db.Database
                .SqlQueryRaw<TermOptionRow>(
                    sql,
                    new NpgsqlParameter("centres", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = centreIds.ToArray() })
                .ToListAsync(ct);

            return rows
                .OrderByDescending(row => row.Session, StringComparer.Ordinal)
                .ThenByDescending(row => row.Term)
                .Select(row => new CommissionTermOptionDto
                {
                    Session = row.Session,
                    Term = (byte)row.Term,
                    Label = $"{TermLabel((TermStatus)row.Term)} {row.Session}",
                    Complete = row.Complete,
                })
                .ToList();
        }

        private static CommissionTermOptionDto? PreviousTerm(List<CommissionTermOptionDto> options, CommissionTermOptionDto term)
        {
            var index = options.FindIndex(option => option.Session == term.Session && option.Term == term.Term);
            return index >= 0 && index + 1 < options.Count ? options[index + 1] : null;
        }

        // ------------------------------------------------------------------------------------
        // Centre drill-down
        // ------------------------------------------------------------------------------------

        public async Task<CommissionCentreDetailDto?> GetCentreDetailAsync(
            string centreId, DateTime? from, DateTime? to, CancellationToken ct = default)
        {
            var centres = await LoadCentresAsync(ct);
            if (!centres.TryGetValue(centreId ?? string.Empty, out var centre))
            {
                return null;
            }

            var today = DateTime.Today;
            var (fromDate, toDate) = ResolveRange(from, to, today);
            var ids = new List<string> { centre.Id };

            var head = await _db.PrincipalDetails.AsNoTracking()
                .Where(row => row.SchoolInfoId == centre.Id && row.Status == TimeStatus.Current)
                .Select(row => row.HeadsName)
                .FirstOrDefaultAsync(ct);

            var currentTerm = await (
                    from term in _db.YearlyAndTermlyAssessmentStatus.AsNoTracking()
                    join session in _db.AcademicSessions.AsNoTracking() on term.SessionId equals session.Id
                    where session.SchoolInfoId == centre.Id && term.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                    select new { session.Session, term.TermStatus, term.CreatedDate })
                .FirstOrDefaultAsync(ct);

            var roll = await (
                    from student in _db.StudentsDetails.AsNoTracking()
                    join user in _db.Users.AsNoTracking() on student.UserId equals user.Id
                    join profile in _db.ProgrammeLearnerProfiles.AsNoTracking() on student.UserId equals profile.StudentId into profiles
                    from profile in profiles.DefaultIfEmpty()
                    where student.SchoolInfoId == centre.Id && student.TimeStatus == TimeStatus.Current
                    select new
                    {
                        student.UserId,
                        student.IdNo,
                        student.ClassArmId,
                        user.FirstName,
                        user.MiddleName,
                        user.SurName,
                        user.Gender,
                        Category = profile != null ? (LearnerProgrammeCategory?)profile.Category : null,
                    })
                .ToListAsync(ct);

            var learnerRates = await _db.LearnerDailyAttendances.AsNoTracking()
                .Where(m => m.SchoolInfoId == centre.Id && m.AttendanceDate >= fromDate && m.AttendanceDate <= toDate)
                .GroupBy(m => m.StudentId)
                .Select(g => new { StudentId = g.Key, Total = g.Count(), Present = g.Count(m => m.Status != DailyAttendanceStatus.Absent) })
                .ToDictionaryAsync(row => row.StudentId, StringComparer.OrdinalIgnoreCase, ct);

            var registerDays = await _db.LearnerDailyAttendances.AsNoTracking()
                .Where(m => m.SchoolInfoId == centre.Id && m.AttendanceDate >= fromDate && m.AttendanceDate <= toDate)
                .Select(m => m.AttendanceDate)
                .Distinct()
                .CountAsync(ct);

            DateTime? lastRegister = await _db.LearnerDailyAttendances.AsNoTracking()
                .Where(m => m.SchoolInfoId == centre.Id && m.AttendanceDate <= today)
                .Select(m => (DateTime?)m.AttendanceDate)
                .MaxAsync(ct);

            var facilitators = await (
                    from employee in _db.ClassTeachers.AsNoTracking()
                    join user in _db.Users.AsNoTracking() on employee.UserId equals user.Id
                    where employee.SchoolId == centre.Id && employee.TimeStatus == TimeStatus.Current
                    select new
                    {
                        employee.Id,
                        employee.EmployeeIdNo,
                        employee.Decipline,
                        employee.EmployeeQualificationStatus,
                        user.FirstName,
                        user.MiddleName,
                        user.SurName,
                        user.Gender,
                    })
                .ToListAsync(ct);

            var facilitatorMarks = await _db.FacilitatorDailyAttendances.AsNoTracking()
                .Where(m => m.SchoolInfoId == centre.Id && m.AttendanceDate >= fromDate && m.AttendanceDate <= toDate)
                .GroupBy(m => m.EmployeeId)
                .Select(g => new
                {
                    EmployeeId = g.Key,
                    Total = g.Count(),
                    Absent = g.Count(m => m.Status == DailyAttendanceStatus.Absent),
                    LastAbsent = g.Where(m => m.Status == DailyAttendanceStatus.Absent).Max(m => (DateTime?)m.AttendanceDate),
                })
                .ToDictionaryAsync(row => row.EmployeeId, StringComparer.OrdinalIgnoreCase, ct);

            var learnerTrend = await _db.LearnerDailyAttendances.AsNoTracking()
                .Where(m => m.SchoolInfoId == centre.Id && m.AttendanceDate >= fromDate && m.AttendanceDate <= toDate)
                .GroupBy(m => m.AttendanceDate)
                .Select(g => new
                {
                    Date = g.Key,
                    Total = g.Count(),
                    Present = g.Count(m => m.Status != DailyAttendanceStatus.Absent),
                    Absent = g.Count(m => m.Status == DailyAttendanceStatus.Absent),
                })
                .ToListAsync(ct);

            var facilitatorTrend = await _db.FacilitatorDailyAttendances.AsNoTracking()
                .Where(m => m.SchoolInfoId == centre.Id && m.AttendanceDate >= fromDate && m.AttendanceDate <= toDate)
                .GroupBy(m => m.AttendanceDate)
                .Select(g => new { Date = g.Key, Total = g.Count(), Present = g.Count(m => m.Status != DailyAttendanceStatus.Absent) })
                .ToDictionaryAsync(row => row.Date, ct);

            var (atRisk, _, _) = await LoadAtRiskAsync(ids, fromDate, toDate, centres, null, ct);
            var atRiskIds = atRisk.Select(row => row.StudentId).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var armStatuses = await BuildClassArmStatusesAsync(ids, centres, fromDate, toDate, today, null, null, ct);
            var lastStaffRegister = await LoadLastRegisterDatesAsync(ids, today, ct, staffRegister: true);
            var armLabels = await LoadArmLabelsAsync(roll.Select(r => r.ClassArmId).Distinct().ToList(), ct);
            var transitions = await LoadTransitionsAsync(ids, ct);

            var options = await LoadTermOptionsAsync(ids, ct);
            var lastComplete = options.FirstOrDefault(option => option.Complete);
            var subjectOutcomes = new List<CommissionOutcomeRowDto>();
            CommissionOutcomeRowDto? lastTermOutcome = null;
            if (lastComplete != null)
            {
                var subjects = await LoadSubjectAggregatesAsync(ids, lastComplete, ct);
                if (subjects.Count > 0)
                {
                    subjectOutcomes = subjects
                        .Select(row => new CommissionOutcomeRowDto
                        {
                            Key = row.Subject,
                            Name = row.Subject,
                            Learners = row.N,
                            AverageScore = Round1(row.Sum / row.N),
                            PassRate = Rate(row.Passed, row.N),
                        })
                        .OrderByDescending(row => row.AverageScore)
                        .ToList();

                    var learnersAssessed = (await LoadOutcomeFactsAsync(ids, lastComplete, ct)).Learners.Sum(row => row.Learners);
                    lastTermOutcome = new CommissionOutcomeRowDto
                    {
                        Key = centre.Id,
                        Name = centre.Name,
                        Learners = learnersAssessed,
                        AverageScore = Round1(subjects.Sum(row => row.Sum) / subjects.Sum(row => row.N)),
                        PassRate = Rate(subjects.Sum(row => row.Passed), subjects.Sum(row => row.N)),
                        StrongestSubject = subjectOutcomes.First().Name,
                        WeakestSubject = subjectOutcomes.Last().Name,
                    };
                }
            }

            var totalMarks = learnerRates.Values.Sum(r => r.Total);
            var presentMarks = learnerRates.Values.Sum(r => r.Present);
            var facilitatorTotal = facilitatorMarks.Values.Sum(r => r.Total);
            var facilitatorAbsent = facilitatorMarks.Values.Sum(r => r.Absent);
            var classRegister = RegisterStanding(
                lastRegister == null ? new Dictionary<string, DateTime>() : new Dictionary<string, DateTime> { [centre.Id] = lastRegister.Value },
                centre.Id, today, expected: true);
            var staffRegister = RegisterStanding(lastStaffRegister, centre.Id, today, expected: facilitators.Count > 0);

            return new CommissionCentreDetailDto
            {
                CentreId = centre.Id,
                CentreName = centre.Name,
                RegistrationNumber = centre.RegistrationNumber,
                CentreType = centre.CentreType,
                Address = centre.Address,
                Community = centre.Community,
                Lga = centre.Lga,
                State = centre.State,
                Zone = centre.Zone,
                Head = head ?? centre.Proprietor,
                Telephone = centre.Telephone,
                YearEstablished = centre.YearEstablished,
                CurrentSession = currentTerm?.Session ?? string.Empty,
                CurrentTerm = currentTerm == null ? string.Empty : TermLabel(currentTerm.TermStatus),
                FromDate = fromDate,
                ToDate = toDate,
                Learners = roll.Count,
                FemaleLearners = roll.Count(r => r.Gender == Gender.Female),
                Facilitators = facilitators.Count,
                DaysRegistered = registerDays,
                ExpectedDays = armStatuses.Count == 0 ? 0 : armStatuses.Max(s => s.ExpectedSubmissions),
                LastRegisterDate = lastRegister,
                LastStaffRegisterDate = staffRegister.Last,
                ClassRegisterOverdue = classRegister.Overdue,
                StaffRegisterOverdue = staffRegister.Overdue,
                RegisterOverdue = classRegister.Overdue || staffRegister.Overdue,
                LearnerAttendanceRate = Rate(presentMarks, totalMarks),
                FacilitatorAttendanceRate = Rate(facilitatorTotal - facilitatorAbsent, facilitatorTotal),
                Transitions = transitions,
                LastTermOutcome = lastTermOutcome,
                LastTermLabel = lastComplete?.Label ?? string.Empty,
                Levels = roll
                    .GroupBy(r => r.ClassArmId, StringComparer.OrdinalIgnoreCase)
                    .Select(g =>
                    {
                        var marks = g.Select(r => learnerRates.GetValueOrDefault(r.UserId)).Where(r => r != null).ToList();
                        var status = armStatuses.FirstOrDefault(s => s.ClassArmId == g.Key);
                        return new CommissionCentreLevelDto
                        {
                            ClassArmId = g.Key,
                            Label = armLabels.GetValueOrDefault(g.Key) ?? string.Empty,
                            FormMaster = status?.FormMasterName ?? string.Empty,
                            Learners = g.Count(),
                            Female = g.Count(r => r.Gender == Gender.Female),
                            Male = g.Count(r => r.Gender == Gender.Male),
                            AttendanceRate = Rate(marks.Sum(r => r!.Present), marks.Sum(r => r!.Total)),
                        };
                    })
                    .OrderBy(level => level.Label, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                FacilitatorsList = facilitators
                    .Select(f =>
                    {
                        facilitatorMarks.TryGetValue(f.Id, out var marks);
                        return new CommissionCentreFacilitatorDto
                        {
                            EmployeeId = f.Id,
                            StaffNumber = f.EmployeeIdNo,
                            FullName = FullName(f.SurName, f.FirstName, f.MiddleName),
                            Gender = f.Gender.ToString(),
                            Discipline = f.Decipline,
                            Qualification = QualificationLabel(f.EmployeeQualificationStatus),
                            DaysRegistered = marks?.Total ?? 0,
                            DaysAbsent = marks?.Absent ?? 0,
                            AttendanceRate = Rate((marks?.Total ?? 0) - (marks?.Absent ?? 0), marks?.Total ?? 0),
                            LastAbsentDate = marks?.LastAbsent,
                        };
                    })
                    .OrderBy(f => f.AttendanceRate)
                    .ToList(),
                AtRiskLearners = atRisk,
                Roll = roll
                    .Select(r =>
                    {
                        learnerRates.TryGetValue(r.UserId, out var marks);
                        return new CommissionCentreLearnerDto
                        {
                            StudentId = r.UserId,
                            FullName = FullName(r.SurName, r.FirstName, r.MiddleName),
                            IdentificationNo = r.IdNo,
                            Gender = r.Gender.ToString(),
                            Level = armLabels.GetValueOrDefault(r.ClassArmId) ?? string.Empty,
                            Category = r.Category == null ? string.Empty : CategoryLabel(r.Category.Value),
                            AttendanceRate = Rate(marks?.Present ?? 0, marks?.Total ?? 0),
                            AtRisk = atRiskIds.Contains(r.UserId),
                        };
                    })
                    .OrderBy(r => r.Level, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => r.FullName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                DailyTrend = learnerTrend
                    .OrderBy(point => point.Date)
                    .Select(point => new CommissionDayPointDto
                    {
                        Date = point.Date,
                        LearnersPresent = point.Present,
                        LearnersAbsent = point.Absent,
                        LearnerAttendanceRate = Rate(point.Present, point.Total),
                        FacilitatorAttendanceRate = facilitatorTrend.TryGetValue(point.Date, out var f) ? Rate(f.Present, f.Total) : 0,
                    })
                    .ToList(),
                SubjectOutcomes = subjectOutcomes,
            };
        }

        // ------------------------------------------------------------------------------------
        // Learner records
        // ------------------------------------------------------------------------------------

        public async Task<IReadOnlyList<CommissionLearnerSearchRowDto>> SearchLearnersAsync(
            string query, CancellationToken ct = default)
        {
            var tokens = (query ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => token.Length >= 2)
                .Take(4)
                .ToList();
            if (tokens.Count == 0)
            {
                return Array.Empty<CommissionLearnerSearchRowDto>();
            }

            var centres = await LoadCentresAsync(ct);
            var ids = centres.Keys.ToList();

            var candidates =
                from student in _db.StudentsDetails.AsNoTracking()
                join user in _db.Users.AsNoTracking() on student.UserId equals user.Id
                where ids.Contains(student.SchoolInfoId)
                select new { student, user };

            foreach (var token in tokens)
            {
                var pattern = $"%{token}%";
                candidates = candidates.Where(row =>
                    EF.Functions.ILike(row.user.FirstName, pattern)
                    || EF.Functions.ILike(row.user.MiddleName, pattern)
                    || EF.Functions.ILike(row.user.SurName, pattern)
                    || EF.Functions.ILike(row.student.IdNo, pattern)
                    || EF.Functions.ILike(row.student.MobileNumber, pattern)
                    || EF.Functions.ILike(row.student.GuardiansName, pattern));
            }

            var matches = await (
                    from row in candidates
                    join profile in _db.ProgrammeLearnerProfiles.AsNoTracking() on row.student.UserId equals profile.StudentId into profiles
                    from profile in profiles.DefaultIfEmpty()
                    orderby row.student.TimeStatus, row.user.SurName, row.user.FirstName
                    select new
                    {
                        row.student.UserId,
                        row.student.IdNo,
                        row.student.SchoolInfoId,
                        row.student.ClassArmId,
                        row.student.DateOfBirth,
                        row.student.TimeStatus,
                        row.user.FirstName,
                        row.user.MiddleName,
                        row.user.SurName,
                        row.user.Gender,
                        Transition = profile != null ? (LearnerTransitionStatus?)profile.TransitionStatus : null,
                    })
                .Take(30)
                .ToListAsync(ct);

            var armLabels = await LoadArmLabelsAsync(matches.Select(m => m.ClassArmId).Distinct().ToList(), ct);
            var today = DateTime.Today;

            return matches
                .Select(m => new CommissionLearnerSearchRowDto
                {
                    StudentId = m.UserId,
                    FullName = FullName(m.SurName, m.FirstName, m.MiddleName),
                    IdentificationNo = m.IdNo,
                    Gender = m.Gender.ToString(),
                    Age = AgeOn(m.DateOfBirth, today),
                    CentreId = m.SchoolInfoId,
                    CentreName = centres.TryGetValue(m.SchoolInfoId, out var c) ? c.Name : string.Empty,
                    State = c?.State ?? string.Empty,
                    Level = armLabels.GetValueOrDefault(m.ClassArmId) ?? string.Empty,
                    Status = m.TimeStatus == TimeStatus.Current
                        ? "On roll"
                        : TransitionLabel(m.Transition ?? LearnerTransitionStatus.Withdrawn),
                })
                .ToList();
        }

        /// <summary>Upper bound on one leavers list; the dashboard filters by outcome to stay within it.</summary>
        private const int LeaversListLimit = 1000;

        public async Task<IReadOnlyList<CommissionLeaverDto>> GetLeaversAsync(string? outcome, CancellationToken ct = default)
        {
            var centres = await LoadCentresAsync(ct);
            var ids = centres.Keys.ToList();

            LearnerTransitionStatus? wanted = (outcome ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "formal" => LearnerTransitionStatus.TransitionedToFormalSchool,
                "vocational" => LearnerTransitionStatus.TransitionedToVocational,
                "withdrawn" => LearnerTransitionStatus.Withdrawn,
                _ => null,
            };

            var query =
                from profile in _db.ProgrammeLearnerProfiles.AsNoTracking()
                join student in _db.StudentsDetails.AsNoTracking() on profile.StudentId equals student.UserId
                join user in _db.Users.AsNoTracking() on student.UserId equals user.Id
                where ids.Contains(profile.SchoolInfoId) && profile.TransitionStatus != LearnerTransitionStatus.Active
                select new { profile, student, user };
            if (wanted != null)
            {
                query = query.Where(row => row.profile.TransitionStatus == wanted);
            }

            var rows = await query
                .OrderByDescending(row => row.profile.TransitionDate)
                .ThenBy(row => row.user.SurName)
                .Take(LeaversListLimit)
                .Select(row => new
                {
                    row.student.UserId,
                    row.student.IdNo,
                    row.student.SchoolInfoId,
                    row.student.ClassArmId,
                    row.user.FirstName,
                    row.user.MiddleName,
                    row.user.SurName,
                    row.user.Gender,
                    row.profile.Category,
                    row.profile.TransitionStatus,
                    row.profile.TransitionDestination,
                    row.profile.TransitionDate,
                })
                .ToListAsync(ct);

            var armLabels = await LoadArmLabelsAsync(rows.Select(r => r.ClassArmId).Distinct().ToList(), ct);

            return rows
                .Select(r => new CommissionLeaverDto
                {
                    StudentId = r.UserId,
                    FullName = FullName(r.SurName, r.FirstName, r.MiddleName),
                    IdentificationNo = r.IdNo,
                    Gender = r.Gender.ToString(),
                    CentreId = r.SchoolInfoId,
                    CentreName = centres.TryGetValue(r.SchoolInfoId, out var c) ? c.Name : string.Empty,
                    State = c?.State ?? string.Empty,
                    LastLevel = armLabels.GetValueOrDefault(r.ClassArmId) ?? string.Empty,
                    Category = CategoryLabel(r.Category),
                    Outcome = r.TransitionStatus switch
                    {
                        LearnerTransitionStatus.TransitionedToFormalSchool => "formal",
                        LearnerTransitionStatus.TransitionedToVocational => "vocational",
                        _ => "withdrawn",
                    },
                    OutcomeLabel = TransitionLabel(r.TransitionStatus),
                    Destination = r.TransitionDestination,
                    LeftOn = r.TransitionDate,
                })
                .ToList();
        }

        public async Task<CommissionLearnerRecordDto?> GetLearnerRecordAsync(string studentId, CancellationToken ct = default)
        {
            var centres = await LoadCentresAsync(ct);

            var learner = await (
                    from student in _db.StudentsDetails.AsNoTracking()
                    join user in _db.Users.AsNoTracking() on student.UserId equals user.Id
                    where student.UserId == studentId
                    select new { student, user })
                .FirstOrDefaultAsync(ct);

            if (learner == null || !centres.TryGetValue(learner.student.SchoolInfoId, out var centre))
            {
                return null;
            }

            var s = learner.student;
            var u = learner.user;
            var today = DateTime.Today;

            var profile = await _db.ProgrammeLearnerProfiles.AsNoTracking()
                .FirstOrDefaultAsync(row => row.StudentId == studentId, ct);

            var originState = profile?.StateOfOriginId == null
                ? string.Empty
                : await _db.States.AsNoTracking().Where(st => st.Id == profile.StateOfOriginId).Select(st => st.Name).FirstOrDefaultAsync(ct) ?? string.Empty;
            var originLga = profile?.LocalGovernmentOfOriginId == null
                ? string.Empty
                : await _db.LocalGovernments.AsNoTracking().Where(l => l.Id == profile.LocalGovernmentOfOriginId).Select(l => l.Name).FirstOrDefaultAsync(ct) ?? string.Empty;

            var armLabels = await LoadArmLabelsAsync(new List<string> { s.ClassArmId }, ct);

            // Every term the learner has scores or register marks in.
            var scoreRows = await (
                    from score in _db.StudentAccessments.AsNoTracking()
                    join subject in _db.Subjects.AsNoTracking() on score.SubjectId equals subject.Id
                    where score.StudentId == studentId
                    select new { score.YearlyAndTermlyStatusId, score.ClassArmId, subject.SubjectName, score.Ca1, score.Ca2, score.Ca3, score.Exams })
                .ToListAsync(ct);

            var markRows = await _db.LearnerDailyAttendances.AsNoTracking()
                .Where(m => m.StudentId == studentId)
                .Select(m => new { m.YearlyAndTermlyStatusId, m.ClassArmId, m.AttendanceDate, m.Status, m.Remark })
                .ToListAsync(ct);

            var termIds = scoreRows.Select(r => r.YearlyAndTermlyStatusId)
                .Concat(markRows.Select(r => r.YearlyAndTermlyStatusId))
                .Distinct()
                .ToList();

            var terms = await (
                    from term in _db.YearlyAndTermlyAssessmentStatus.AsNoTracking()
                    join session in _db.AcademicSessions.AsNoTracking() on term.SessionId equals session.Id
                    where termIds.Contains(term.Id)
                    select new { term.Id, term.TermStatus, term.TermlyAccessmentStatus, session.Session })
                .ToListAsync(ct);

            var comments = await _db.StudentsAttendanceReports.AsNoTracking()
                .Where(r => r.StudentId == studentId)
                .ToDictionaryAsync(r => r.YearlyAndTermlyStatusId, StringComparer.OrdinalIgnoreCase, ct);

            // Position in class: every classmate's average for the same arm and term.
            var armTerms = scoreRows.Select(r => new { r.ClassArmId, r.YearlyAndTermlyStatusId }).Distinct().ToList();
            var classAverages = new Dictionary<string, List<(string StudentId, double Average)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var armTerm in armTerms)
            {
                var averages = await _db.StudentAccessments.AsNoTracking()
                    .Where(score => score.ClassArmId == armTerm.ClassArmId && score.YearlyAndTermlyStatusId == armTerm.YearlyAndTermlyStatusId)
                    .GroupBy(score => score.StudentId)
                    .Select(g => new { StudentId = g.Key, Average = g.Average(score => score.Ca1 + score.Ca2 + score.Ca3 + score.Exams) })
                    .ToListAsync(ct);
                classAverages[armTerm.YearlyAndTermlyStatusId] = averages.Select(a => (a.StudentId, (double)a.Average)).ToList();
            }

            var allArmIds = scoreRows.Select(r => r.ClassArmId).Concat(markRows.Select(r => r.ClassArmId)).Distinct().ToList();
            var termArmLabels = await LoadArmLabelsAsync(allArmIds, ct);

            var termDtos = terms
                .OrderBy(t => t.Session, StringComparer.Ordinal)
                .ThenBy(t => t.TermStatus)
                .Select(t =>
                {
                    var subjects = scoreRows.Where(r => r.YearlyAndTermlyStatusId == t.Id)
                        .OrderBy(r => r.SubjectName, StringComparer.OrdinalIgnoreCase)
                        .Select(r =>
                        {
                            var total = (double)(r.Ca1 + r.Ca2 + r.Ca3 + r.Exams);
                            return new CommissionLearnerSubjectDto
                            {
                                Subject = r.SubjectName,
                                Ca1 = r.Ca1,
                                Ca2 = r.Ca2,
                                Ca3 = r.Ca3,
                                Exam = r.Exams,
                                Total = Round1(total),
                                Grade = GradeFor(total),
                            };
                        })
                        .ToList();

                    var marks = markRows.Where(r => r.YearlyAndTermlyStatusId == t.Id).ToList();
                    var present = marks.Count(m => m.Status != DailyAttendanceStatus.Absent);
                    var complete = t.TermlyAccessmentStatus == TermlyAccessmentStatus.Previous;
                    var average = subjects.Count == 0 ? 0 : subjects.Average(x => x.Total);
                    var armId = scoreRows.FirstOrDefault(r => r.YearlyAndTermlyStatusId == t.Id)?.ClassArmId
                        ?? marks.FirstOrDefault()?.ClassArmId
                        ?? string.Empty;

                    var position = 0;
                    var classSize = 0;
                    if (complete && classAverages.TryGetValue(t.Id, out var classmates))
                    {
                        classSize = classmates.Count;
                        position = 1 + classmates.Count(c => c.Average > average + 0.0001);
                    }

                    comments.TryGetValue(t.Id, out var comment);

                    return new CommissionLearnerTermDto
                    {
                        Session = t.Session,
                        Term = TermLabel(t.TermStatus),
                        Level = termArmLabels.GetValueOrDefault(armId) ?? string.Empty,
                        Complete = complete,
                        DaysOpen = marks.Count,
                        DaysPresent = present,
                        AttendanceRate = Rate(present, marks.Count),
                        TotalScore = Round1(subjects.Sum(x => x.Total)),
                        AverageScore = Round1(average),
                        Grade = complete && subjects.Count > 0 ? GradeFor(average) : string.Empty,
                        Position = position,
                        ClassSize = classSize,
                        TeacherComment = comment?.TeacherComments ?? string.Empty,
                        HeadComment = comment?.SchoolHeadComments ?? string.Empty,
                        Subjects = subjects,
                    };
                })
                .ToList();

            var currentTermId = terms.FirstOrDefault(t => t.TermlyAccessmentStatus == TermlyAccessmentStatus.Current)?.Id;
            var currentMarks = markRows
                .Where(m => m.YearlyAndTermlyStatusId == currentTermId)
                .OrderBy(m => m.AttendanceDate)
                .ToList();
            var streak = 0;
            for (var i = currentMarks.Count - 1; i >= 0 && currentMarks[i].Status == DailyAttendanceStatus.Absent; i--)
            {
                streak++;
            }

            var levelLabel = armLabels.GetValueOrDefault(s.ClassArmId) ?? string.Empty;

            return new CommissionLearnerRecordDto
            {
                StudentId = s.UserId,
                FullName = FullName(u.SurName, u.FirstName, u.MiddleName),
                IdentificationNo = s.IdNo,
                Gender = u.Gender.ToString(),
                DateOfBirth = s.DateOfBirth,
                Age = AgeOn(s.DateOfBirth, today),
                PlaceOfBirth = s.PlaceOfBirth,
                CentreId = centre.Id,
                CentreName = centre.Name,
                CentreAddress = centre.Address,
                State = centre.State,
                Level = levelLabel,
                StartLevel = s.StartClass,
                AdmissionDate = s.DateOfAdmsn,
                DischargeDate = s.DateOfDischarge,
                Status = s.TimeStatus == TimeStatus.Current
                    ? "On roll"
                    : TransitionLabel(profile?.TransitionStatus ?? LearnerTransitionStatus.Withdrawn),
                Category = profile == null ? string.Empty : CategoryLabel(profile.Category),
                StateOfOrigin = TidyStateName(originState),
                LgaOfOrigin = originLga,
                Tsangaya = profile?.Tsangaya ?? string.Empty,
                QuranJuzMemorised = profile?.QuranJuzMemorised,
                PriorSchooling = profile?.PriorSchooling ?? string.Empty,
                TransitionStatus = TransitionLabel(profile?.TransitionStatus ?? LearnerTransitionStatus.Active),
                TransitionDate = profile?.TransitionDate,
                TransitionDestination = profile?.TransitionDestination ?? string.Empty,
                GuardianName = s.GuardiansName,
                GuardianRelationship = s.Relationship,
                GuardianPhone = s.MobileNumber,
                GuardianAltPhone = s.HomeNumber,
                GuardianAddress = s.GuardianAddress,
                GuardianOccupation = s.Occupation,
                CurrentTermDaysRegistered = currentMarks.Count,
                CurrentTermDaysPresent = currentMarks.Count(m => m.Status != DailyAttendanceStatus.Absent),
                CurrentTermDaysAbsent = currentMarks.Count(m => m.Status == DailyAttendanceStatus.Absent),
                CurrentTermDaysLate = currentMarks.Count(m => m.Status == DailyAttendanceStatus.Late),
                CurrentAbsenceStreak = streak,
                CurrentTermAttendanceRate = Rate(
                    currentMarks.Count(m => m.Status != DailyAttendanceStatus.Absent), currentMarks.Count),
                Terms = termDtos,
                RecentRegister = markRows
                    .OrderByDescending(m => m.AttendanceDate)
                    .Take(15)
                    .Select(m => new CommissionRegisterMarkDto
                    {
                        Date = m.AttendanceDate,
                        Status = m.Status.ToString(),
                        Remark = m.Remark,
                    })
                    .ToList(),
            };
        }

        // ------------------------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------------------------

        private static (DateTime From, DateTime To) ResolveRange(DateTime? from, DateTime? to, DateTime today)
        {
            var toDate = (to ?? today).Date;
            var fromDate = (from ?? toDate.AddDays(-29)).Date;
            return fromDate > toDate ? (toDate, fromDate) : (fromDate, toDate);
        }

        /// <summary>School days after <paramref name="last"/> up to and including today with no register.</summary>
        private static int SchoolDaysMissedSince(DateTime last, DateTime today)
            => last.Date >= today.Date ? 0 : SchoolCalendar.SchoolDaysBetween(last.Date.AddDays(1), today).Count;

        private static double Rate(int part, int total) => total <= 0 ? 0d : Math.Round(part * 100d / total, 1);

        private static double Round1(double value) => Math.Round(value, 1);

        private static int AgeOn(DateTime dateOfBirth, DateTime today)
        {
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age))
            {
                age--;
            }

            return Math.Max(0, age);
        }

        private static string FullName(string? surName, string? firstName, string? middleName)
            => string.Join(" ", new[] { surName, firstName, middleName }.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();

        /// <summary>Same bands as the printed report sheet.</summary>
        private static string GradeFor(double total) => total switch
        {
            >= 70 => "A",
            >= 60 => "B",
            >= 50 => "C",
            >= 45 => "D",
            >= 40 => "E",
            _ => "F",
        };

        private static string TermLabel(TermStatus term) => term switch
        {
            TermStatus.First => "First Term",
            TermStatus.Second => "Second Term",
            _ => "Third Term",
        };

        private static string ZoneName(string region) => region.Trim() switch
        {
            "Northern Central" => "North Central",
            _ => region.Trim(),
        };

        private static string TidyStateName(string state)
            => string.IsNullOrWhiteSpace(state) ? string.Empty : char.ToUpperInvariant(state[0]) + state[1..];

        private static string CategoryLabel(LearnerProgrammeCategory category) => category switch
        {
            LearnerProgrammeCategory.Almajiri => "Almajiri learner",
            LearnerProgrammeCategory.OutOfSchoolNeverEnrolled => "Out-of-school (never enrolled)",
            LearnerProgrammeCategory.OutOfSchoolDroppedOut => "Out-of-school (dropped out)",
            LearnerProgrammeCategory.DisplacedChild => "Displaced child",
            _ => category.ToString(),
        };

        private static string TransitionLabel(LearnerTransitionStatus status) => status switch
        {
            LearnerTransitionStatus.Active => "Active on roll",
            LearnerTransitionStatus.TransitionedToFormalSchool => "Transitioned to formal school",
            LearnerTransitionStatus.TransitionedToVocational => "Transitioned to vocational training",
            LearnerTransitionStatus.Withdrawn => "Withdrawn",
            _ => status.ToString(),
        };

        private static string QualificationLabel(EmployeeQualificationStatus status) => status switch
        {
            EmployeeQualificationStatus.DegreeInEducation => "B.Ed",
            EmployeeQualificationStatus.DegreeInSingleHonour => "B.Sc/B.A",
            EmployeeQualificationStatus.PgdEducation => "PGDE",
            EmployeeQualificationStatus.Nce => "NCE",
            EmployeeQualificationStatus.Diploma => "Diploma",
            EmployeeQualificationStatus.MasterInEducation => "M.Ed",
            EmployeeQualificationStatus.MasterInSingleHonour => "M.Sc/M.A",
            _ => "Other",
        };
    }
}
