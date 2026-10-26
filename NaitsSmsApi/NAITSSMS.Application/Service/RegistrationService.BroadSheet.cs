using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.Service
{
    public partial class RegistrationService
    {
        private const string BroadSheetNoRecordMessage = "No record in this class.";
        private const string BroadSheetNotReadyMessage = "Result is not yet ready.";

        /// <summary>
        /// Slim projected score row for broadsheet (no EF entity graphs).
        /// </summary>
        private sealed class BroadSheetAssessmentRow
        {
            public string StudentId { get; init; } = string.Empty;
            public string ClassArmId { get; init; } = string.Empty;
            public string? SubjectId { get; init; }
            public string SubjectName { get; init; } = string.Empty;
            public float Ca1 { get; init; }
            public float Ca2 { get; init; }
            public float Ca3 { get; init; }
            public float Exams { get; init; }
            public string AdmissionNo { get; init; } = string.Empty;
            public string FirstName { get; init; } = string.Empty;
            public string MiddleName { get; init; } = string.Empty;
            public string SurName { get; init; } = string.Empty;
            public ArmName ArmName { get; init; }
            public string ClassLabel { get; init; } = string.Empty;
        }

        public Task<BroadSheetReportDto> GetBroadSheetReportForClassAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            CurrentClass className,
            CancellationToken ct = default)
            => BuildBroadSheetReportAsync(
                userId,
                sessionId,
                termStatus,
                className,
                armName: null,
                ct);

        public Task<BroadSheetReportDto> GetBroadSheetReportForArmAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            CurrentClass className,
            ArmName armName,
            CancellationToken ct = default)
            => BuildBroadSheetReportAsync(
                userId,
                sessionId,
                termStatus,
                className,
                armName,
                ct);

        public async Task<IList<SelectListItem>> GetBroadSheetArmsDropdownAsync(
            string userId,
            CancellationToken ct = default)
        {
            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return new List<SelectListItem>();
            }

            var classArmRepo = _uow.Repository<AxClassArm>();
            var armNames = await classArmRepo.SelectWhereAsync(
                arm => arm.Classs != null && arm.Classs.SchoolInfoId == schoolId,
                arm => arm.ArmName,
                ct);

            var options = armNames
                .Distinct()
                .OrderBy(name => name)
                .Select(name => new SelectListItem
                {
                    Text = name.ToString(),
                    Value = ((int)name).ToString(),
                })
                .ToList();

            if (options.Count == 1)
            {
                var only = options[0];
                return new List<SelectListItem>
                {
                    new() { Text = "Select", Value = "-1" },
                    only,
                };
            }

            return options;
        }

        private async Task<BroadSheetReportDto> BuildBroadSheetReportAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            CurrentClass className,
            ArmName? armName,
            CancellationToken ct)
        {
            var fetch = await FetchBroadSheetAssessmentsAsync(
                userId,
                sessionId,
                termStatus,
                className,
                armName,
                ct);

            if (fetch.Rows.Count == 0)
            {
                return new BroadSheetReportDto
                {
                    Message = string.IsNullOrWhiteSpace(fetch.NotReadyMessage)
                        ? BroadSheetNoRecordMessage
                        : fetch.NotReadyMessage,
                    ClassLabel = fetch.ClassLabel ?? className.ToString(),
                    SessionLabel = fetch.SessionLabel ?? string.Empty,
                    TermLabel = fetch.TermLabel ?? string.Empty,
                    SchoolInfo = fetch.SchoolInfo,
                };
            }

            var sample = fetch.Rows[0];
            var report = new BroadSheetReportDto
            {
                Message = string.Empty,
                SessionLabel = fetch.SessionLabel ?? "Nil",
                TermLabel = fetch.TermLabel ?? termStatus.ToString(),
                ClassLabel = !string.IsNullOrWhiteSpace(fetch.ClassLabel)
                    ? fetch.ClassLabel!
                    : sample.ClassLabel,
                SchoolInfo = fetch.SchoolInfo,
            };

            var armGroups = fetch.Rows
                .GroupBy(item => item.ClassArmId)
                .OrderBy(group => group.First().ArmName);

            foreach (var armGroup in armGroups)
            {
                report.Arms.Add(BuildBroadSheetArmSection(armGroup.ToList()));
            }

            if (report.Arms.Count == 0)
            {
                report.Message = BroadSheetNoRecordMessage;
            }

            return report;
        }

        private sealed class BroadSheetFetchResult
        {
            public List<BroadSheetAssessmentRow> Rows { get; init; } = new();
            public BroadSheetSchoolInfoDto? SchoolInfo { get; init; }
            public string? SessionLabel { get; init; }
            public string? TermLabel { get; init; }
            public string? ClassLabel { get; init; }
            public string? NotReadyMessage { get; init; }
        }

        private async Task<BroadSheetFetchResult> FetchBroadSheetAssessmentsAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            CurrentClass className,
            ArmName? armName,
            CancellationToken ct)
        {
            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return new BroadSheetFetchResult();
            }

            var schoolInfo = await LoadBroadSheetSchoolInfoAsync(schoolId, ct);
            var assessRepo = _uow.Repository<StudenXtAccessment>();

            if (armName.HasValue)
            {
                var armRepo = _uow.Repository<AxClassArm>();
                var arm = await armRepo.FindAsync(
                    x => x.Classs.ClassName == className &&
                         x.ArmName == armName.Value &&
                         x.Classs.SchoolInfoId == schoolId,
                    ct,
                    v => v.Classs);

                if (arm == null)
                {
                    return new BroadSheetFetchResult { SchoolInfo = schoolInfo };
                }

                var yearlyStatus = (await assessRepo.FindAsync(
                    s => s.ClassArmId == arm.Id &&
                         s.YearlyAndTermlyStatus.SessionId == sessionId &&
                         s.YearlyAndTermlyStatus.TermStatus == termStatus,
                    ct,
                    d => d.YearlyAndTermlyStatus.Session))?.YearlyAndTermlyStatus;

                if (yearlyStatus?.Session == null)
                {
                    return new BroadSheetFetchResult
                    {
                        SchoolInfo = schoolInfo,
                        ClassLabel = arm.Classs?.ClassLabel ?? className.ToString(),
                        NotReadyMessage = BroadSheetNotReadyMessage,
                    };
                }

                var rows = await assessRepo.SelectWhereAsync(
                    s => s.ClassArmId == arm.Id &&
                         s.YearlyAndTermlyStatusId == yearlyStatus.Id,
                    BroadSheetAssessmentProjection,
                    ct);

                return new BroadSheetFetchResult
                {
                    Rows = rows,
                    SchoolInfo = schoolInfo,
                    SessionLabel = yearlyStatus.Session.Session ?? "Nil",
                    TermLabel = yearlyStatus.TermStatus.ToString(),
                    ClassLabel = arm.Classs?.ClassLabel ?? className.ToString(),
                };
            }

            var classRepo = _uow.Repository<RxClasss>();
            var classEntity = await classRepo.FindAsync(
                x => x.ClassName == className && x.SchoolInfoId == schoolId,
                ct);

            if (classEntity == null)
            {
                return new BroadSheetFetchResult { SchoolInfo = schoolInfo };
            }

            var classYearlyStatus = (await assessRepo.FindAsync(
                s => s.ClassArm.ClassId == classEntity.Id &&
                     s.YearlyAndTermlyStatus.SessionId == sessionId &&
                     s.YearlyAndTermlyStatus.TermStatus == termStatus,
                ct,
                d => d.YearlyAndTermlyStatus.Session))?.YearlyAndTermlyStatus;

            if (classYearlyStatus?.Session == null)
            {
                return new BroadSheetFetchResult
                {
                    SchoolInfo = schoolInfo,
                    ClassLabel = classEntity.ClassLabel ?? className.ToString(),
                    NotReadyMessage = BroadSheetNotReadyMessage,
                };
            }

            var classRows = await assessRepo.SelectWhereAsync(
                s => s.ClassArm.ClassId == classEntity.Id &&
                     s.YearlyAndTermlyStatusId == classYearlyStatus.Id,
                BroadSheetAssessmentProjection,
                ct);

            return new BroadSheetFetchResult
            {
                Rows = classRows,
                SchoolInfo = schoolInfo,
                SessionLabel = classYearlyStatus.Session.Session ?? "Nil",
                TermLabel = classYearlyStatus.TermStatus.ToString(),
                ClassLabel = classEntity.ClassLabel ?? className.ToString(),
            };
        }

        private static readonly System.Linq.Expressions.Expression<Func<StudenXtAccessment, BroadSheetAssessmentRow>>
            BroadSheetAssessmentProjection =
                s => new BroadSheetAssessmentRow
                {
                    StudentId = s.StudentId,
                    ClassArmId = s.ClassArmId,
                    SubjectId = s.SubjectId,
                    SubjectName = s.Subject != null ? s.Subject.SubjectName : string.Empty,
                    Ca1 = s.Ca1,
                    Ca2 = s.Ca2,
                    Ca3 = s.Ca3,
                    Exams = s.Exams,
                    AdmissionNo = s.Student != null ? s.Student.IdNo : string.Empty,
                    FirstName = s.Student != null && s.Student.User != null ? s.Student.User.FirstName : string.Empty,
                    MiddleName = s.Student != null && s.Student.User != null ? s.Student.User.MiddleName : string.Empty,
                    SurName = s.Student != null && s.Student.User != null ? s.Student.User.SurName : string.Empty,
                    ArmName = s.ClassArm != null ? s.ClassArm.ArmName : default,
                    ClassLabel = s.ClassArm != null && s.ClassArm.Classs != null
                        ? s.ClassArm.Classs.ClassLabel
                        : string.Empty,
                };

        private async Task<BroadSheetSchoolInfoDto?> LoadBroadSheetSchoolInfoAsync(
            string schoolId,
            CancellationToken ct)
        {
            var rows = await _uow.Repository<SchoolTyInfo, string>().SelectWhereAsync(
                s => s.SchoolRegistrationOfficerId == schoolId,
                s => new BroadSheetSchoolInfoDto
                {
                    SchoolName = s.SchoolName ?? string.Empty,
                    SchoolAddress = s.SchoolAddress ?? string.Empty,
                    Motto = s.Motto ?? string.Empty,
                    SchoolLogoString = s.SchoolLogoString ?? string.Empty,
                },
                ct);

            return rows.FirstOrDefault();
        }

        private static BroadSheetArmSectionDto BuildBroadSheetArmSection(IReadOnlyList<BroadSheetAssessmentRow> armAssessments)
        {
            var sample = armAssessments[0];
            var subjects = armAssessments
                .GroupBy(item => item.SubjectId)
                .Select(group => group.First().SubjectName ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            var studentGroups = armAssessments
                .GroupBy(item => item.StudentId)
                .OrderByDescending(group => group.Sum(SubjectTotal))
                .ToList();

            var averages = studentGroups
                .Select(group => StudentAverage(group.ToList()))
                .ToList();

            var section = new BroadSheetArmSectionDto
            {
                ArmLabel = $"{sample.ClassLabel}{sample.ArmName}",
                Subjects = subjects,
                StudentCount = studentGroups.Count,
                ClassHighestAverage = averages.Count > 0 ? Round1(averages.Max()) : 0,
                ClassLowestAverage = averages.Count > 0 ? Round1(averages.Min()) : 0,
                SubjectAverageScore = armAssessments.Count > 0
                    ? Round1(armAssessments.Sum(SubjectTotal) / armAssessments.Count)
                    : 0,
            };

            var classPositionCounter = 0;
            var difference = 0;
            double lastOverallAverageScore = 0;
            var serialNumber = 0;

            foreach (var studentGroup in studentGroups)
            {
                serialNumber++;
                var rows = studentGroup.ToList();
                var total = Round1(rows.Sum(SubjectTotal));
                var average = Round1(StudentAverage(rows));

                var lastClassPosition = classPositionCounter;
                classPositionCounter++;

                if (Math.Abs(lastOverallAverageScore - average) <= 0.001 && average > 0)
                {
                    classPositionCounter = lastClassPosition;
                    difference++;
                }
                else
                {
                    if (difference > 0)
                    {
                        classPositionCounter += difference;
                        difference = 0;
                    }
                }

                lastOverallAverageScore = average;

                section.Students.Add(new BroadSheetStudentRowDto
                {
                    SerialNumber = serialNumber,
                    AdmissionNo = rows[0].AdmissionNo ?? string.Empty,
                    FullName = BuildBroadSheetFullName(rows[0]),
                    Scores = BuildSubjectScores(rows, subjects),
                    SubjectCount = rows.Count,
                    Total = total,
                    Average = average,
                    Position = FormatBroadSheetPosition(classPositionCounter),
                });
            }

            return section;
        }

        private static string BuildBroadSheetFullName(BroadSheetAssessmentRow row)
            => $"{row.FirstName} {row.MiddleName} {row.SurName}".Trim();

        private static IList<BroadSheetSubjectScoreDto> BuildSubjectScores(
            IReadOnlyList<BroadSheetAssessmentRow> studentRows,
            IReadOnlyList<string> subjects)
        {
            var scoreLookup = studentRows
                .Where(row => !string.IsNullOrWhiteSpace(row.SubjectName))
                .GroupBy(row => row.SubjectName)
                .ToDictionary(
                    group => group.Key,
                    group => Round1(SubjectTotal(group.First())),
                    StringComparer.OrdinalIgnoreCase);

            var scores = new List<BroadSheetSubjectScoreDto>(subjects.Count);
            foreach (var subject in subjects)
            {
                if (!scoreLookup.TryGetValue(subject, out var value))
                {
                    scores.Add(new BroadSheetSubjectScoreDto
                    {
                        DisplayValue = "-",
                        IsFail = false,
                    });
                    continue;
                }

                scores.Add(new BroadSheetSubjectScoreDto
                {
                    DisplayValue = value.ToString(CultureInfo.InvariantCulture),
                    IsFail = value < 40d,
                });
            }

            return scores;
        }

        private static double SubjectTotal(BroadSheetAssessmentRow row)
            => row.Ca1 + row.Ca2 + row.Ca3 + row.Exams;

        private static double StudentAverage(IReadOnlyList<BroadSheetAssessmentRow> rows)
            => rows.Count == 0 ? 0 : rows.Sum(SubjectTotal) / rows.Count;

        private static double Round1(double value)
            => Math.Round(value, 1, MidpointRounding.AwayFromZero);

        private static string FormatBroadSheetPosition(int classPositionCounter)
        {
            if (classPositionCounter <= 0)
            {
                return classPositionCounter.ToString(CultureInfo.InvariantCulture);
            }

            var classPosition = classPositionCounter.ToString(CultureInfo.InvariantCulture);
            if (classPositionCounter > 20)
            {
                return AppendOrdinalSuffix(classPosition);
            }

            return classPositionCounter switch
            {
                1 => "1st",
                2 => "2nd",
                3 => "3rd",
                _ => $"{classPosition}th",
            };
        }

        private static string AppendOrdinalSuffix(string classPosition)
        {
            if (string.IsNullOrEmpty(classPosition))
            {
                return classPosition;
            }

            var lastDigit = classPosition[^1];
            return lastDigit switch
            {
                '1' => $"{classPosition}st",
                '2' => $"{classPosition}nd",
                '3' => $"{classPosition}rd",
                _ => $"{classPosition}th",
            };
        }
    }
}
