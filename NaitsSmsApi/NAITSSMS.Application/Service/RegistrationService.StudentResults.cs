using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.Mappings;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.Service
{
    public partial class RegistrationService
    {
        private const string NoStudentsMessage = "Sorry! no student in the selected Arm.";
        private const string EnvironmentNotPreparedMessage =
            "Failed! Environment has not been prepared for result processing yet; contact Registration Officer.";

        public Task<IList<SelectListItem>> GetStudentResultsSessionsDropdown(string userId, CancellationToken ct = default)
            => _sessionAndTermService.GetSessionsDropdown(userId, ct);

        public Task<IList<SelectListItem>> GetStudentResultsTermsDropdown(string sessionId, CancellationToken ct = default)
            => _sessionAndTermService.GetTermsDropdownForSession(sessionId, ct);

        public Task<IEnumerable<ClassDto>> GetStudentResultsClasses(string userId, CancellationToken ct = default)
            => _classAndArmService.GetClass(userId, ct);

        public Task<IEnumerable<ClassArmDto>> GetStudentResultsClassArms(string classId, CancellationToken ct = default)
            => _classAndArmService.GetClasssArm(classId, ct);

        public Task<UsersSingleStudentResultProcessingsToReportSheetXtractionDto> GetTermlyAssessmentForSelectedStudent(
            string userId,
            string studentId,
            string sessionId,
            TermStatus termStatus,
            CancellationToken ct = default)
            => _teachersAndOthersService.GetTermlyAssessmentForSelectedStudent(studentId, sessionId, termStatus, ct);

        public Task<YearlyResultProcessingsToReportSheetDto> GetYearlyAssessmentForSelectedStudent(
            string userId,
            string studentId,
            string selectedSessionId,
            CancellationToken ct = default)
            => _teachersAndOthersService.GetYearlyAssessmentForSelectedStudent(studentId, selectedSessionId, ct);

        public async Task<PerClassAndThirdTermCumulativeResultProcessingDto> PerClassAndThirdTermCumulativeResultProcessing(
            string userId,
            string sessionId,
            TermStatus termInEnum,
            CurrentClass classNameInEnum,
            CancellationToken ct = default)
        {
            var studentsReports = await ProcessClassResultByClassName(userId, sessionId, termInEnum, classNameInEnum, ct);
            return await MapClassArmCollectionResultAsync(userId, studentsReports, useStudentsAccessmentTotalAttendance: true, ct);
        }

        public async Task<PerClassAndThirdTermCumulativeResultProcessingDto> PerClassThirdTermResultProcessing(
            string userId,
            string sessionId,
            TermStatus termInEnum,
            CurrentClass classNameInEnum,
            CancellationToken ct = default)
        {
            var studentsReports = await GetClassThirdTermResult(userId, sessionId, termInEnum, classNameInEnum, ct);
            return await MapClassArmCollectionResultAsync(userId, studentsReports, useStudentsAccessmentTotalAttendance: true, ct);
        }

        public async Task<PerClassAndThirdTermCumulativeResultProcessingDto> PerArmAndThirdTermCumulativeResultProcessing(
            string userId,
            string sessionId,
            TermStatus termInEnum,
            CurrentClass classNameInEnum,
            ArmName armInEnum,
            CancellationToken ct = default)
        {
            var studentsReports = await ProcessClassArmResultByClassNameAndArmName(
                userId, sessionId, termInEnum, classNameInEnum, armInEnum, ct);
            return await MapClassArmCollectionResultAsync(userId, studentsReports, useStudentsAccessmentTotalAttendance: true, ct);
        }

        public async Task<PerClassAndThirdTermCumulativeResultProcessingDto> PerArmThirdTermResultProcessing(
            string userId,
            CurrentClass classNameInEnum,
            ArmName armInEnum,
            CancellationToken ct = default)
        {
            var studentsReports = await ProcessThirdTermClassArmResultByClassNameAndArmName(
                userId, classNameInEnum, armInEnum, ct);
            return await MapClassArmCollectionResultAsync(userId, studentsReports, useStudentsAccessmentTotalAttendance: true, ct);
        }

        public async Task<PerClassAndThirdTermCumulativeResultProcessingDto> GeneralResultProcessing(
            string userId,
            CancellationToken ct = default)
        {
            var studentsReports = await ProcessAllResults(userId, ct);
            if (studentsReports == null)
            {
                return new PerClassAndThirdTermCumulativeResultProcessingDto
                {
                    Message = NoStudentsMessage,
                    StudentsAccessment = new List<StudentsAccessmentDto>(),
                };
            }

            if (studentsReports.StatusCode == 1)
            {
                return new PerClassAndThirdTermCumulativeResultProcessingDto
                {
                    Message = EnvironmentNotPreparedMessage,
                    StudentsAccessment = new List<StudentsAccessmentDto>(),
                    StatusCode = 1,
                };
            }

            var result = new PerClassAndThirdTermCumulativeResultProcessingDto
            {
                StudentsAccessment = studentsReports.StudentsAccessment ?? new List<StudentsAccessmentDto>(),
                StudentsAttendance = studentsReports.StudentsAttendance,
                CaSetting = studentsReports.CasSetting,
                StatusCode = studentsReports.StatusCode,
            };

            if (studentsReports.StudentsAccessment != null && studentsReports.StudentsAccessment.Count > 0)
            {
                result.SchoolInfo = await GetSchoolModelForSchoolRegistrationOfficer(userId, ct);

                if (studentsReports.AffectiveAndPsychomotorAssessment != null &&
                    studentsReports.AffectiveAndPsychomotorAssessment.Count > 0)
                {
                    result.SchoolHead = studentsReports.SchoolHeadInfo?.FirstOrDefault();
                    result.ArmFormMaster = studentsReports.AffectiveAndPsychomotorAssessment[0].FormMaster;
                    result.AffectiveAndPsychomotorAssessment = studentsReports.AffectiveAndPsychomotorAssessment;
                    result.TotalAttendance = studentsReports.AffectiveAndPsychomotorAssessment[0]
                        .YearlyAndTermlyAssessmentStatus?
                        .TotalAttendance;
                }
            }

            return result;
        }

        public async Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessClassResultByClassName(
            string userId,
            string sessionId,
            TermStatus termInEnum,
            CurrentClass classNameInEnum,
            CancellationToken ct = default)
        {
            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (schoolId == null)
            {
                return null;
            }

            var classRepo = _uow.Repository<RxClasss>();
            var classEntity = await classRepo.FindAsync(
                x => x.ClassName == classNameInEnum && x.SchoolInfoId == schoolId,
                ct);
            if (classEntity == null)
            {
                return null;
            }

            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var currentStatus = await assessRepo.FindAsync(
                s => s.ClassArm.ClassId == classEntity.Id &&
                     s.YearlyAndTermlyStatus.SessionId == sessionId &&
                     s.YearlyAndTermlyStatus.TermStatus == termInEnum,
                ct,
                d => d.YearlyAndTermlyStatus.Session);

            var yearlyStatus = currentStatus?.YearlyAndTermlyStatus;
            if (yearlyStatus == null)
            {
                return null;
            }

            var excludePrevious = yearlyStatus.TermlyAccessmentStatus != TermlyAccessmentStatus.Previous;
            var mapped = yearlyStatus.TermStatus == TermStatus.Third
                ? await assessRepo.SelectWhereAsync(
                    s => s.ClassArm.ClassId == classEntity.Id &&
                         s.YearlyAndTermlyStatus.SessionId == sessionId &&
                         (!excludePrevious || s.Student.Status != StudentStatus.Previous),
                    StudentResultsReportProjections.Assessment,
                    ct)
                : await assessRepo.SelectWhereAsync(
                    s => s.ClassArm.ClassId == classEntity.Id &&
                         s.YearlyAndTermlyStatusId == yearlyStatus.Id &&
                         (!excludePrevious || s.Student.Status != StudentStatus.Previous),
                    StudentResultsReportProjections.Assessment,
                    ct);

            return await BuildClassArmReportAsync(
                mapped,
                affect => affect.ClassArm.ClassId == classEntity.Id && affect.YearlyAndTermlyStatusId == yearlyStatus.Id,
                attendance => attendance.ClassArm.ClassId == classEntity.Id && attendance.YearlyAndTermlyStatusId == yearlyStatus.Id,
                IsSeniorClass(classNameInEnum) ? yearlyStatus.PrincipalId : yearlyStatus.HeadmasterId,
                ct);
        }

        public async Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> GetClassThirdTermResult(
            string userId,
            string sessionId,
            TermStatus termInEnum,
            CurrentClass classNameInEnum,
            CancellationToken ct = default)
        {
            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (schoolId == null)
            {
                return null;
            }

            var classRepo = _uow.Repository<RxClasss>();
            var classEntity = await classRepo.FindAsync(
                x => x.ClassName == classNameInEnum && x.SchoolInfoId == schoolId,
                ct);
            if (classEntity == null)
            {
                return null;
            }

            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var yearlyStatus = (await assessRepo.FindAsync(
                s => s.ClassArm.ClassId == classEntity.Id &&
                     s.Student.SchoolInfoId == schoolId &&
                     s.YearlyAndTermlyStatus.SessionId == sessionId &&
                     s.YearlyAndTermlyStatus.TermStatus == termInEnum,
                ct,
                d => d.YearlyAndTermlyStatus.Session))?.YearlyAndTermlyStatus;

            if (yearlyStatus == null)
            {
                return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto { StatusCode = 1 };
            }

            var mapped = await assessRepo.SelectWhereAsync(
                s => s.ClassArm.ClassId == classEntity.Id && s.YearlyAndTermlyStatusId == yearlyStatus.Id,
                StudentResultsReportProjections.Assessment,
                ct);

            return await BuildClassArmReportAsync(
                mapped,
                affect => affect.ClassArm.ClassId == classEntity.Id && affect.YearlyAndTermlyStatusId == yearlyStatus.Id,
                attendance => attendance.ClassArm.ClassId == classEntity.Id && attendance.YearlyAndTermlyStatusId == yearlyStatus.Id,
                IsSeniorClass(classNameInEnum) ? yearlyStatus.PrincipalId : yearlyStatus.HeadmasterId,
                ct);
        }

        public async Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessClassArmResultByClassNameAndArmName(
            string userId,
            string sessionId,
            TermStatus termInEnum,
            CurrentClass classNameInEnum,
            ArmName armInEnum,
            CancellationToken ct = default)
        {
            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (schoolId == null)
            {
                return null;
            }

            var armRepo = _uow.Repository<AxClassArm>();
            var arm = await armRepo.FindAsync(
                x => x.Classs.ClassName == classNameInEnum && x.ArmName == armInEnum && x.Classs.SchoolInfoId == schoolId,
                ct,
                v => v.Classs);
            if (arm == null)
            {
                return null;
            }

            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var yearlyStatus = (await assessRepo.FindAsync(
                s => s.ClassArmId == arm.Id &&
                     s.Student.SchoolInfoId == schoolId &&
                     s.YearlyAndTermlyStatus.SessionId == sessionId &&
                     s.YearlyAndTermlyStatus.TermStatus == termInEnum,
                ct,
                d => d.YearlyAndTermlyStatus.Session))?.YearlyAndTermlyStatus;

            if (yearlyStatus == null)
            {
                return null;
            }

            var excludePrevious = yearlyStatus.TermlyAccessmentStatus != TermlyAccessmentStatus.Previous;
            var mapped = yearlyStatus.TermStatus == TermStatus.Third
                ? await assessRepo.SelectWhereAsync(
                    s => s.ClassArmId == arm.Id &&
                         s.Student.SchoolInfoId == schoolId &&
                         s.YearlyAndTermlyStatus.SessionId == sessionId &&
                         (!excludePrevious || s.Student.Status != StudentStatus.Previous),
                    StudentResultsReportProjections.Assessment,
                    ct)
                : await assessRepo.SelectWhereAsync(
                    s => s.ClassArmId == arm.Id &&
                         s.Student.SchoolInfoId == schoolId &&
                         s.YearlyAndTermlyStatus.SessionId == sessionId &&
                         s.YearlyAndTermlyStatus.TermStatus == termInEnum &&
                         (!excludePrevious || s.Student.Status != StudentStatus.Previous),
                    StudentResultsReportProjections.Assessment,
                    ct);

            return await BuildClassArmReportAsync(
                mapped,
                affect => affect.ClassArmId == arm.Id && affect.YearlyAndTermlyStatusId == yearlyStatus.Id,
                attendance => attendance.ClassArmId == arm.Id && attendance.YearlyAndTermlyStatusId == yearlyStatus.Id,
                IsSeniorClass(classNameInEnum) ? yearlyStatus.PrincipalId : yearlyStatus.HeadmasterId,
                ct);
        }

        public async Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessThirdTermClassArmResultByClassNameAndArmName(
            string userId,
            CurrentClass classNameInEnum,
            ArmName armInEnum,
            CancellationToken ct = default)
        {
            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (schoolId == null)
            {
                return null;
            }

            var armRepo = _uow.Repository<AxClassArm>();
            var arm = await armRepo.FindAsync(
                x => x.Classs.ClassName == classNameInEnum &&
                     x.ArmName == armInEnum &&
                     x.Classs.SchoolInfoId == schoolId,
                ct,
                v => v.Classs);
            if (arm == null)
            {
                return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto { StatusCode = 1 };
            }

            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var yearlyStatus = (await assessRepo.FindAsync(
                s => s.ClassArmId == arm.Id &&
                     s.Student.SchoolInfoId == schoolId &&
                     s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                d => d.YearlyAndTermlyStatus.Session))?.YearlyAndTermlyStatus;

            if (yearlyStatus == null)
            {
                return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto { StatusCode = 1 };
            }

            var mapped = await assessRepo.SelectWhereAsync(
                s => s.ClassArmId == arm.Id && s.YearlyAndTermlyStatusId == yearlyStatus.Id,
                StudentResultsReportProjections.Assessment,
                ct);

            return await BuildClassArmReportAsync(
                mapped,
                affect => affect.ClassArmId == arm.Id &&
                          (affect.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                           affect.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing),
                attendance => attendance.ClassArmId == arm.Id &&
                              (attendance.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                               attendance.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing),
                IsSeniorClass(classNameInEnum) ? yearlyStatus.PrincipalId : yearlyStatus.HeadmasterId,
                ct);
        }

        public async Task<GeneralResultProcessingsForAllStudentsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessAllResults(
            string userId,
            CancellationToken ct = default)
        {
            var schoolId = await ResolveRegistrySchoolIdAsync(userId, ct);
            if (schoolId == null)
            {
                return null;
            }

            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var baseStatus = await assessRepo.FindAsync(
                s => s.Student.SchoolInfoId == schoolId &&
                     (s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                      s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing),
                ct,
                d => d.YearlyAndTermlyStatus.Session);

            var yearly = baseStatus?.YearlyAndTermlyStatus;
            if (yearly == null)
            {
                return null;
            }

            var mapped = yearly.TermStatus == TermStatus.Third
                ? await assessRepo.SelectWhereAsync(
                    s => s.YearlyAndTermlyStatus.SessionId == yearly.SessionId &&
                         s.Student.Status != StudentStatus.Previous &&
                         s.Subject.TimeStatus == TimeStatus.Current,
                    StudentResultsReportProjections.Assessment,
                    ct)
                : await assessRepo.SelectWhereAsync(
                    s => s.YearlyAndTermlyStatusId == yearly.Id,
                    StudentResultsReportProjections.Assessment,
                    ct);

            if (mapped.Count == 0)
            {
                return new GeneralResultProcessingsForAllStudentsToReportSheetXtractionForFormTeacherCasSettingDto { StatusCode = 1 };
            }

            var affectRepo = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var affect = await affectRepo.SelectWhereAsync(
                a => a.FormMaster.SchoolId == schoolId &&
                     a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                StudentResultsReportProjections.Affective,
                ct);
            await AffectiveItemLabels.ApplyByClassArmAsync(
                _uow, (affect ?? new()).Select(a => ((string?)a.ClassArmId, (AffectiveAndPsychomotorDto?)a.AffectiveAndPsychomotor)), ct);

            if (affect == null || affect.Count == 0)
            {
                return new GeneralResultProcessingsForAllStudentsToReportSheetXtractionForFormTeacherCasSettingDto
                {
                    StatusCode = 2,
                    StudentsAccessment = mapped,
                    CasSetting = StudentAssessmentMapping.ResolveCasSetting(mapped),
                };
            }

            await AttachFormMasterDtosAsync(affect, ct);

            var schoolHeadList = new List<SchoolHeadDto>();
            var principalHead = await SchoolInfoForTeacherResultProcessing(yearly.PrincipalId, ct);
            if (principalHead != null)
            {
                schoolHeadList.Add(principalHead);
            }

            var headTeacher = await SchoolInfoForTeacherResultProcessing(yearly.HeadmasterId, ct);
            if (headTeacher != null)
            {
                schoolHeadList.Add(headTeacher);
            }

            var attendanceRepo = _uow.Repository<StudentsAtteXzndanceAndCommentsReport>();
            var attendance = await attendanceRepo.SelectWhereAsync(
                a => a.Student.SchoolInfoId == schoolId &&
                     (a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                      a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing),
                StudentResultsReportProjections.Attendance,
                ct);

            return new GeneralResultProcessingsForAllStudentsToReportSheetXtractionForFormTeacherCasSettingDto
            {
                AffectiveAndPsychomotorAssessment = affect,
                StudentsAccessment = mapped,
                StudentsAttendance = attendance,
                SchoolHeadInfo = schoolHeadList,
                CasSetting = StudentAssessmentMapping.ResolveCasSetting(mapped),
            };
        }

        private async Task<string?> ResolveRegistrySchoolIdAsync(string userId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            try
            {
                return await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private async Task<PerClassAndThirdTermCumulativeResultProcessingDto> MapClassArmCollectionResultAsync(
            string userId,
            GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto? studentsReports,
            bool useStudentsAccessmentTotalAttendance,
            CancellationToken ct)
        {
            if (studentsReports == null)
            {
                return new PerClassAndThirdTermCumulativeResultProcessingDto
                {
                    Message = NoStudentsMessage,
                    StudentsAccessment = new List<StudentsAccessmentDto>(),
                };
            }

            if (studentsReports.StatusCode == 1)
            {
                return new PerClassAndThirdTermCumulativeResultProcessingDto
                {
                    Message = EnvironmentNotPreparedMessage,
                    StudentsAccessment = new List<StudentsAccessmentDto>(),
                    StatusCode = 1,
                };
            }

            var result = new PerClassAndThirdTermCumulativeResultProcessingDto
            {
                StudentsAccessment = studentsReports.StudentsAccessment ?? new List<StudentsAccessmentDto>(),
                StudentsAttendance = studentsReports.StudentsAttendance,
                CaSetting = studentsReports.CasSetting,
                StatusCode = studentsReports.StatusCode,
            };

            if (studentsReports.StudentsAccessment != null && studentsReports.StudentsAccessment.Count > 0)
            {
                result.SchoolInfo = await GetSchoolModelForSchoolRegistrationOfficer(userId, ct);

                if (studentsReports.AffectiveAndPsychomotorAssessment != null &&
                    studentsReports.AffectiveAndPsychomotorAssessment.Count > 0)
                {
                    result.SchoolHead = studentsReports.SchoolHeadInfo;
                    result.ArmFormMaster = studentsReports.AffectiveAndPsychomotorAssessment[0].FormMaster;
                    result.AffectiveAndPsychomotorAssessment = studentsReports.AffectiveAndPsychomotorAssessment;
                    // Prefer TermStatus over YearlyAndTermlyStatusId GUID order (GUIDs are not chronological).
                    result.TotalAttendance = useStudentsAccessmentTotalAttendance
                        ? studentsReports.StudentsAccessment
                            .OrderBy(x => x.YearlyAndTermlyStatus?.TermStatus ?? TermStatus.First)
                            .ThenBy(x => x.YearlyAndTermlyStatusId)
                            .Last()
                            .YearlyAndTermlyStatus?
                            .TotalAttendance
                        : studentsReports.AffectiveAndPsychomotorAssessment[0]
                            .YearlyAndTermlyAssessmentStatus?
                            .TotalAttendance;
                }
            }

            return result;
        }

        private async Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> BuildClassArmReportAsync(
            List<StudentsAccessmentDto> mapped,
            System.Linq.Expressions.Expression<Func<AffectiveAndPsychomoTxtorAssessment, bool>> affectPredicate,
            System.Linq.Expressions.Expression<Func<StudentsAtteXzndanceAndCommentsReport, bool>> attendancePredicate,
            string? schoolHeadId,
            CancellationToken ct)
        {
            if (mapped == null || mapped.Count == 0)
            {
                return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto { StatusCode = 1 };
            }

            var affectRepo = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var affect = await affectRepo.SelectWhereAsync(
                affectPredicate,
                StudentResultsReportProjections.Affective,
                ct);
            await AffectiveItemLabels.ApplyByClassArmAsync(
                _uow, (affect ?? new()).Select(a => ((string?)a.ClassArmId, (AffectiveAndPsychomotorDto?)a.AffectiveAndPsychomotor)), ct);

            if (affect == null || affect.Count == 0)
            {
                return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
                {
                    StatusCode = 2,
                    StudentsAccessment = mapped,
                    CasSetting = StudentAssessmentMapping.ResolveCasSetting(mapped),
                };
            }

            await AttachFormMasterDtosAsync(affect, ct);

            var schoolHead = await SchoolInfoForTeacherResultProcessing(schoolHeadId, ct);

            var attendanceRepo = _uow.Repository<StudentsAtteXzndanceAndCommentsReport>();
            var attendance = await attendanceRepo.SelectWhereAsync(
                attendancePredicate,
                StudentResultsReportProjections.Attendance,
                ct);

            return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
            {
                AffectiveAndPsychomotorAssessment = affect,
                StudentsAccessment = mapped,
                StudentsAttendance = attendance,
                SchoolHeadInfo = schoolHead,
                CasSetting = StudentAssessmentMapping.ResolveCasSetting(mapped),
            };
        }

        private async Task AttachFormMasterDtosAsync(
            List<AffectiveAndPsychomotorAssessmentOutputXtractionForFormTeacherDto> affect,
            CancellationToken ct)
        {
            var formMasterIds = affect
                .Select(a => a.FormMasterId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (formMasterIds.Count == 0)
            {
                return;
            }

            var employees = await _uow.Repository<TxEmployee>().SelectWhereAsync(
                e => formMasterIds.Contains(e.Id),
                e => new
                {
                    e.Id,
                    FirstName = e.User != null ? e.User.FirstName : null,
                    MiddleName = e.User != null ? e.User.MiddleName : null,
                    SurName = e.User != null ? e.User.SurName : null,
                    Signature = e.SignatureImageString,
                },
                ct);

            var byId = employees.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var row in affect)
            {
                if (string.IsNullOrWhiteSpace(row.FormMasterId) ||
                    !byId.TryGetValue(row.FormMasterId, out var employee))
                {
                    continue;
                }

                row.FormMaster = new ArmFormMasterDto
                {
                    FormMastersName =
                        ((employee.FirstName ?? string.Empty) + " " +
                         (employee.MiddleName ?? string.Empty) + " " +
                         (employee.SurName ?? string.Empty)).Trim(),
                    SignatureString = employee.Signature ?? string.Empty,
                    AssignedArmId = row.ClassArmId,
                };
            }
        }

        private static bool IsSeniorClass(CurrentClass classNameInEnum)
            => classNameInEnum is CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3
                or CurrentClass.Jss1 or CurrentClass.Jss2 or CurrentClass.Jss3;

        private async Task<SchoolHeadDto> SchoolInfoForTeacherResultProcessing(string schoolHeadId, CancellationToken ct = default)
        {
            if (schoolHeadId == null)
            {
                return null;
            }

            var repo = _uow.Repository<SchoolHeadTx>();
            var head = await repo.FindAsync(s => s.Id == schoolHeadId, ct, b => b.SchoolHeadCategory);
            if (head == null)
            {
                return null;
            }

            return new SchoolHeadDto
            {
                HeadsName = head.HeadsName,
                SignatureImageString = head.SignatureImageString,
                SchoolHeadCategoryId = head.SchoolHeadCategoryId,
                HeadStatus = head.SchoolHeadCategory?.HeadStatus ?? default,
                Gender = head.Gender,
            };
        }
    }
}
