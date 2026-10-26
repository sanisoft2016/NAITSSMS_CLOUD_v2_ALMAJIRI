using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Application.Mappings;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Data.Security;
using NAITSSMS.Infrastructure.Repo;
using NAITSSMS.Infrastructure.Unitofwork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace NAITSSMS.Application.Service
{
    // NOTE:
    // - Method names preserved.
    // - Methods now return Task<T> where applicable and accept CancellationToken.
    // - Uses lazy UnitOfWork repositories; avoids ServiceLocator.
    // - Minimizes blocking, uses AsNoTracking reads, and defers SaveChanges to mutations only.
    public class TeachersAndOthersService: ITeachersAndOthersService
    {
        private static readonly TimeSpan ClassArmDropdownCacheTtl = TimeSpan.FromMinutes(10);
        private const string ClassArmDropdownCacheKey = "teachers:class-arm-dropdown";

        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ISchoolScopeResolver _schoolScopeResolver;
        private readonly ISecurityExtensionService _securityExtensionService;
        private readonly IMemoryCache _memoryCache;
       // private readonly IRegistrationService _registrationService;

        public TeachersAndOthersService(
            IUnitOfWork uow,
            IMapper mapper,
            IHttpContextAccessor httpContextAccessor,
            ISchoolScopeResolver schoolScopeResolver,
            ISecurityExtensionService securityExtensionService,
            IMemoryCache memoryCache
           // IRegistrationService registrationService
           )
        {
            _uow = uow;
            _mapper = mapper;
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _schoolScopeResolver = schoolScopeResolver ?? throw new ArgumentNullException(nameof(schoolScopeResolver));
            _securityExtensionService = securityExtensionService ?? throw new ArgumentNullException(nameof(securityExtensionService));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            //_registrationService = registrationService;
        }


        /// <summary>
        /// Optional portal header: switch data to a sibling that shares Contact MobileNumber.
        /// </summary>
        private const string SelectedStudentHeaderName = "X-Selected-Student-Id";

        private string? CurrentUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return user.FindFirstValue(ClaimTypes.PrimarySid)
                ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub")
                ?? user.FindFirstValue("uid")
                ?? user.FindFirstValue("user_id")
                ?? (string.IsNullOrWhiteSpace(user.Identity?.Name) ? null : user.Identity.Name);
        }

        private static string NormalizeMobileNumber(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
            {
                return string.Empty;
            }

            var digits = new string(mobile.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("234", StringComparison.Ordinal) && digits.Length >= 13)
            {
                digits = digits[^10..];
            }
            else if (digits.StartsWith('0') && digits.Length == 11)
            {
                digits = digits[1..];
            }

            return digits;
        }

        private string? ReadSelectedStudentHeader()
        {
            var header = _httpContextAccessor.HttpContext?.Request.Headers[SelectedStudentHeaderName]
                .FirstOrDefault();
            return string.IsNullOrWhiteSpace(header) ? null : header.Trim();
        }

        /// <summary>
        /// Student row for the authenticated login (JWT), ignoring sibling selection.
        /// </summary>
        private async Task<TxStudentDetail?> GetLoggedInPortalStudentAsync(CancellationToken ct)
        {
            var userId = CurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var studentRepo = _uow.Repository<TxStudentDetail>();
            return await studentRepo.FindAsync(
                s => s.UserId == userId,
                ct,
                s => s.SchoolInfo!,
                s => s.ClassArm!.Classs!,
                s => s.User!);
        }

        /// <summary>
        /// Effective portal student: logged-in student, or a same-school sibling that shares
        /// Contact MobileNumber when <c>X-Selected-Student-Id</c> is present and authorized.
        /// </summary>
        private async Task<TxStudentDetail?> ResolvePortalStudentAsync(CancellationToken ct)
        {
            var current = await GetLoggedInPortalStudentAsync(ct);
            if (current == null)
            {
                return null;
            }

            var selectedId = ReadSelectedStudentHeader();
            if (string.IsNullOrWhiteSpace(selectedId)
                || string.Equals(selectedId, current.UserId, StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            var mobile = NormalizeMobileNumber(current.MobileNumber);
            if (string.IsNullOrWhiteSpace(mobile) || string.IsNullOrWhiteSpace(current.SchoolInfoId))
            {
                return current;
            }

            var studentRepo = _uow.Repository<TxStudentDetail>();
            var candidate = await studentRepo.FindAsync(
                s => s.UserId == selectedId,
                ct,
                s => s.SchoolInfo!,
                s => s.ClassArm!.Classs!,
                s => s.User!);

            if (candidate == null
                || !string.Equals(candidate.SchoolInfoId, current.SchoolInfoId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(NormalizeMobileNumber(candidate.MobileNumber), mobile, StringComparison.Ordinal))
            {
                return current;
            }

            return candidate;
        }

        private bool CurrentUserIsInRole(string role)
        {
            return _httpContextAccessor.HttpContext?.User?.IsInRole(role) == true;
        }

        private async Task<string?> ResolveSchoolIdForCurrentUserAsync(CancellationToken ct = default)
        {
            var userId = CurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            if (CurrentUserIsInRole("Registry"))
            {
                var registrySchoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
                if (!string.IsNullOrWhiteSpace(registrySchoolId))
                {
                    return registrySchoolId;
                }
            }

            var teacherSchoolId = (await CurrentTeacherModel(ct))?.SchoolId;
            if (!string.IsNullOrWhiteSpace(teacherSchoolId))
            {
                return teacherSchoolId;
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


        //private string CurrentUserId()
        //{
        //    var principal = Thread.CurrentPrincipal;
        //    var identity = principal?.Identity;
        //    if (identity is System.Security.Claims.ClaimsIdentity claimsIdentity)
        //    {
        //        var userIdClaim = claimsIdentity.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        //        if (!string.IsNullOrWhiteSpace(userIdClaim?.Value)) return userIdClaim.Value;
        //    }
        //    return identity?.Name;
        //}

        // Preserved name; now async
        private async Task<EmployeeDto?> CurrentTeacherModel(CancellationToken ct = default)
        {
            var userId = CurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var repo = _uow.Repository<TxEmployee>();
            var entity = await repo.FindAsync(e => e.UserId == userId, ct, r => r.SchoolInfo);
            if (entity == null)
            {
                return null;
            }

            return _mapper.Map<EmployeeDto>(entity);
        }

        // Preserved name; now async
        public async Task<IEnumerable<TeacherSubjectArmAssignmentDto>> GetTeacherAssignedClassesAndSubjects(CancellationToken ct = default)
        {
            var teacher = await CurrentTeacherModel(ct);
            if (string.IsNullOrWhiteSpace(teacher?.Id))
            {
                return Enumerable.Empty<TeacherSubjectArmAssignmentDto>();
            }

            var repo = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var rows = await repo.WhereAsync(
                t => t.EmployeeId == teacher.Id,
                ct,
                i => i.Arm!.Classs!,
                s => s.Subject!);

            return rows.Select(TeacherAssignmentMapping.MapSubjectArmAssignment).ToList();
        }

        // Preserved name; now async — implementation lives on RegistryOtherMethodService.
        public Task<AccessVerifyReturnValues?> B(string userId, CancellationToken ct = default) =>
            _securityExtensionService.B(userId, ct);

        // Preserved name; now async
        public async Task<UsersSingleStudentResultProcessingsToReportSheetXtractionDto> GetTermlyAssessmentForSelectedStudent(string studentId, string sessionId, TermStatus termStatus, CancellationToken ct = default)
        {
            studentId = (studentId ?? string.Empty).Trim();
            sessionId = (sessionId ?? string.Empty).Trim();
            var schoolId = await ResolveSchoolIdForAssessmentLookup(ct);
            var yearlyRepo = _uow.Repository<TxYearlyAndTermlyStatus>();
            var selectedTerm = await yearlyRepo.FindAsync(x => x.SessionId == sessionId && x.TermStatus == termStatus, ct);

            var assessRepo = _uow.Repository<StudenXtAccessment>();
            // Prefer SessionId FK (same as portal / later queries). Session.Id navigation join can
            // miss rows when the nav is not satisfied even though SessionId matches.
            var focus = await assessRepo.FindAsync(
                s => s.Student.IdNo == studentId &&
                     s.Student.SchoolInfoId == schoolId &&
                     s.YearlyAndTermlyStatus.SessionId == sessionId &&
                     s.YearlyAndTermlyStatus.TermStatus == termStatus,
                ct,
                d => d.ClassArm.Classs,
                v => v.YearlyAndTermlyStatus,
                st => st.Student);

            if (focus == null)
                return new UsersSingleStudentResultProcessingsToReportSheetXtractionDto { StatusCode = 0 };

            var armId = focus.ClassArmId;
            var validationRows = await assessRepo.WhereAsync(
                s => s.ClassArmId == armId &&
                     s.YearlyAndTermlyStatus.SessionId == sessionId &&
                     s.YearlyAndTermlyStatus.TermStatus == termStatus,
                ct);
            if (validationRows == null || validationRows.Count == 0)
                return new UsersSingleStudentResultProcessingsToReportSheetXtractionDto { StatusCode = 1 };

            List<StudenXtAccessment> students;
            if (focus.YearlyAndTermlyStatus.TermStatus == TermStatus.Third)
            {
                // Third term rules: optionally filter to current/active items only
                var requireCurrent = selectedTerm?.TermlyAccessmentStatus == TermlyAccessmentStatus.Current;
                students = await assessRepo.WhereAsync(
                    s => s.ClassArmId == armId &&
                         s.YearlyAndTermlyStatus.SessionId == sessionId &&
                         (!requireCurrent ||
                          (s.Student.Status != StudentStatus.Previous && s.Subject.TimeStatus == TimeStatus.Current)),
                    ct,
                    st => st.Student.User,
                    em => em.Employee.User,
                    su => su.Subject,
                    yt => yt.YearlyAndTermlyStatus,
                    ca => ca.ClassArm.Classs);
            }
            else
            {
                // Legacy admin overload: exact YearlyAndTermlyStatusId for the selected term.
                students = await assessRepo.WhereAsync(
                    s => s.ClassArmId == armId &&
                         s.YearlyAndTermlyStatus.SessionId == sessionId &&
                         s.YearlyAndTermlyStatusId == focus.YearlyAndTermlyStatusId,
                    ct,
                    st => st.Student.User,
                    em => em.Employee.User,
                    su => su.Subject,
                    yt => yt.YearlyAndTermlyStatus,
                    ca => ca.ClassArm.Classs);
            }

            if (students == null || students.Count == 0)
                return new UsersSingleStudentResultProcessingsToReportSheetXtractionDto { StatusCode = 1 };

            var affectRepo = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var affect = await affectRepo.WhereAsync(a =>
                    a.ClassArmId == armId &&
                    a.FormMaster.SchoolId == schoolId &&
                    a.YearlyAndTermlyStatus.SessionId == sessionId &&
                    a.YearlyAndTermlyStatusId == focus.YearlyAndTermlyStatusId,
                ct,
                af => af.AffectiveAndPsychomotor,
                cl => cl.ClassArm.Classs,
                fm => fm.FormMaster.User,
                st => st.Student.User,
                ys => ys.YearlyAndTermlyStatus.Session,
                stc => stc.Student.ClassArm.Classs);

            if (affect == null || affect.Count == 0)
                return new UsersSingleStudentResultProcessingsToReportSheetXtractionDto { StatusCode = 2 };

            var studentRepo = _uow.Repository<TxStudentDetail>();
            var studentObj = await studentRepo.FindAsync(
                s => s.IdNo == studentId && s.SchoolInfoId == schoolId,
                ct,
                c => c.ClassArm.Classs);
            if (studentObj?.ClassArm?.Classs == null)
            {
                return new UsersSingleStudentResultProcessingsToReportSheetXtractionDto
                {
                    StatusCode = 0,
                    StudentId = focus.StudentId,
                };
            }

            var className = studentObj.ClassArm.Classs.ClassName;
            var termStatusEntity = affect
                .Select(a => a.YearlyAndTermlyStatus)
                .FirstOrDefault(y => y != null);
            SchoolHeadDto? head = null;
            if (termStatusEntity != null)
            {
                var schoolHeadId = className is CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3 or
                                               CurrentClass.Jss1 or CurrentClass.Jss2 or CurrentClass.Jss3
                    ? termStatusEntity.PrincipalId
                    : termStatusEntity.HeadmasterId;
                head = await SchoolInfoForTeacherResultProcessing(schoolHeadId, ct);
            }

            var attendance = await LoadReportAttendanceAsync(
                focus.YearlyAndTermlyStatusId,
                armId,
                a => a.Student.ClassArm.Classs.ClassName == className &&
                     a.Student.ClassArm.ArmName == studentObj.ClassArm.ArmName &&
                     a.Student.SchoolInfoId == schoolId &&
                     (a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                      a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing),
                ct);

            // Manual map — AutoMapper fails on YearlyAndTermlyStatus Id type mismatch.
            var mapped = StudentAssessmentMapping.MapList(students);
            if (mapped.Count == 0)
                return new UsersSingleStudentResultProcessingsToReportSheetXtractionDto { StatusCode = 1 };

            var casSetting = StudentAssessmentMapping.ResolveCasSetting(mapped)
                ?? new CasSettingDto
                {
                    CasCategory = mapped.Last().YearlyAndTermlyStatus?.CasCategory ?? default,
                    PerCasValue = mapped.Last().YearlyAndTermlyStatus?.PerCasValue ?? 0,
                };

            var singleReport = new UsersSingleStudentResultProcessingsToReportSheetXtractionDto
            {
                StudentId = studentObj.UserId ?? focus.StudentId,
                GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting =
                    new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
                    {
                        AffectiveAndPsychomotorAssessment = affect.Select(f => new AffectiveAndPsychomotorAssessmentOutputXtractionForFormTeacherDto
                        {
                            ClassArmId = f.ClassArmId,
                            FormMaster = new ArmFormMasterDto
                            {
                                FormMastersName = f.FormMaster?.User?.FullName ?? string.Empty,
                                SignatureString = f.FormMaster?.SignatureImageString,
                                AssignedArmId = f.ClassArmId
                            },
                            Student = new StudentFielExtractionDto
                            {
                                FullName = f.Student?.User?.FullName ?? string.Empty,
                                Id = f.StudentId,
                                CurrentClass = f.ClassArm?.Classs?.ClassName ?? default,
                                ArmName = f.ClassArm?.ArmName ?? default,
                                CurrentClassLabel = f.Student?.ClassArm?.Classs?.ClassLabel
                                    ?? f.ClassArm?.Classs?.ClassLabel
                            },
                            AffectiveAndPsychomotor = StudentAssessmentMapping.MapPsychomotor(f.AffectiveAndPsychomotor),
                            YearlyAndTermlyAssessmentStatus = f.YearlyAndTermlyStatus == null
                                ? null
                                : new YearlyAndTermlyStatusDto
                                {
                                    NextTermBeginsDate = f.YearlyAndTermlyStatus.NextTermBeginsDate,
                                    TotalAttendance = f.YearlyAndTermlyStatus.TotalAttendance,
                                    TermStatus = f.YearlyAndTermlyStatus.TermStatus,
                                    Session = f.YearlyAndTermlyStatus.Session == null
                                        ? null
                                        : new AcademicSessionDto { Session = f.YearlyAndTermlyStatus.Session.Session }
                                },
                            ItemId = f.ItemId,
                            Score = f.Score
                        }).ToList(),
                        StudentsAccessment = mapped,
                        StudentsAttendance = StudentAssessmentMapping.MapAttendanceList(attendance),
                        SchoolHeadInfo = head,
                        CasSetting = casSetting
                    }
            };
            await RelabelSingleReportAsync(singleReport, ct);
            return singleReport;
        }

        // Preserved name; now async
        public async Task<YearlyResultProcessingsToReportSheetDto> GetYearlyAssessmentForSelectedStudent(string studentId, string selectedSessionId, CancellationToken ct = default)
        {
            var validation = await StudentsPromotionValidations(ct);
            if (validation != "Success")
            {
                return new YearlyResultProcessingsToReportSheetDto
                {
                    ErrorMsg = validation == "The school has only done a single term and therefore you cannot run promotion"
                        ? "The school has only done a single term and therefore you cannot compute yearly student result processing."
                        : validation
                };
            }

            var schoolId = await ResolveSchoolIdForAssessmentLookup(ct);
            var studentRepo = _uow.Repository<TxStudentDetail>();
            var student = await studentRepo.FindAsync(s => s.IdNo == studentId && s.SchoolInfoId == schoolId, ct, d => d.ClassArm);
            if (student == null) return new YearlyResultProcessingsToReportSheetDto { ErrorMsg = "Student not found" };

            var armId = student.ClassArm.Id;
            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var rows = await assessRepo.WhereAsync(s =>
                    s.ClassArmId == armId && s.YearlyAndTermlyStatus.SessionId == selectedSessionId,
                ct, st => st.Student, su => su.Subject, ca => ca.ClassArm.Classs, ys => ys.YearlyAndTermlyStatus.Session);

            if (rows == null || rows.Count == 0)
                return new YearlyResultProcessingsToReportSheetDto { ErrorMsg = "No data" };

            var mapped = rows.Select(r => new StudentsAccessmentDto
            {
                YearlyAndTermlyStatus = _mapper.Map<YearlyAndTermlyStatusDto>(r.YearlyAndTermlyStatus),
                ClassArmId = r.ClassArmId,
                Ca1 = r.Ca1,
                Ca2 = r.Ca2,
                Ca3 = r.Ca3,
                Exams = r.Exams,
                Id = r.Id,
                StudentId = r.StudentId,
                Subject = _mapper.Map<SubjectDto>(r.Subject),
                ClassArm = _mapper.Map<ClassArmDto>(r.ClassArm)
            }).ToList();

            var attendanceRepo = _uow.Repository<StudentsAtteXzndanceAndCommentsReport>();
            var attendance = await attendanceRepo.WhereAsync(a => a.ClassArmId == armId && a.YearlyAndTermlyStatus.Session.Status == TimeStatus.Current, ct);

            var affectRepo = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var formMasters = (await affectRepo.WhereAsync(a =>
                    a.ClassArmId == armId && a.YearlyAndTermlyStatus.SessionId == selectedSessionId,
                ct, f => f.FormMaster.User))
                .GroupBy(g => g.YearlyAndTermlyStatus.TermStatus)
                .Select(g => g.First())
                .Select(f => new FormMastersNameDto
                {
                    FormMastersName = f.FormMaster.User.FullName,
                    Term = f.YearlyAndTermlyStatus.TermStatus
                }).ToList();

            return new YearlyResultProcessingsToReportSheetDto
            {
                StudentsAccessment = mapped,
                StudentsAttendance = _mapper.Map<List<StudentsAttendanceDto>>(attendance),
                ArmFormMaster = formMasters,
                ErrorMsg = "Success"
            };
        }

        // Preserved name; now async — legacy TeachersAndOthersService.GetStudentInAnArmForAssessment
        public async Task<StudentAssessmentAndStatusDto> GetStudentInAnArmForAssessment(string armId, CancellationToken ct = default)
        {
            var studentDetailRepository = _uow.Repository<TxStudentDetail>();
            var collection = await studentDetailRepository.WhereAsync(
                s => s.ClassArmId == armId && s.Status != StudentStatus.Previous,
                ct,
                d => d.ClassArm.Classs!,
                d => d.User!);

            var teacherClassArmAssignmentRepository = _uow.Repository<TeacherClassArmTxAssignment>();
            var schoolId = await GetSchoolIdForTeacher(ct);
            var assignedTeacherObj = await teacherClassArmAssignmentRepository.FindAsync(
                s => s.ArmId == armId && s.Employee.SchoolId == schoolId,
                ct);

            var studentAssessmentAndStatusModel = new StudentAssessmentAndStatusDto();
            if (assignedTeacherObj != null)
            {
                studentAssessmentAndStatusModel.ArmTermlyAssessmentStatus = assignedTeacherObj.ArmTermlyAssessmentStatus;
            }

            // Legacy: when the arm has no current students, remove the form-master assignment.
            if ((collection == null || !collection.Any()) && assignedTeacherObj != null)
            {
                teacherClassArmAssignmentRepository.Delete(assignedTeacherObj);
                await _uow.SaveChangesAsync(ct);
            }

            studentAssessmentAndStatusModel.StudentDetails =
                _mapper.Map<List<StudentDetailsDto>>(collection ?? Enumerable.Empty<TxStudentDetail>());
            return studentAssessmentAndStatusModel;
        }

        // Preserved name; now async — legacy TeachersAndOthersService.GetTeacherAssignedArmAsFormMaster
        public async Task<IEnumerable<TeacherClassArmAssignmentDto>> GetTeacherAssignedArmAsFormMaster(CancellationToken ct = default)
        {
            var teacherClassArmAssignmentRepository = _uow.Repository<TeacherClassArmTxAssignment>();
            var userId = CurrentUserId();
            var schoolId = await GetSchoolIdForTeacher(ct);
            var assignedTeacher = await teacherClassArmAssignmentRepository.WhereAsync(
                a => a.Employee != null
                     && a.Employee.SchoolId == schoolId
                     && a.Employee.User != null
                     && a.Employee.User.Id == userId,
                ct,
                a => a.Employee!.User!,
                a => a.Arm!.Classs!);

            return assignedTeacher
                .Select(TeacherAssignmentMapping.MapClassArmAssignment)
                .ToList();
        }

        // Preserved name; now async
        public async Task<StudentsAccessmentCasSettingDto> GetStudentsInAnArmByArmIdAndCaSetting(string armId, string subjectId, CancellationToken ct = default)
        {
            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var rows = await assessRepo.WhereAsync(
                s => s.SubjectId == subjectId && s.Student.ClassArmId == armId &&
                     (s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                      s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing), ct, s => s.Student.User);

            if (rows == null || rows.Count == 0)
                return new StudentsAccessmentCasSettingDto();

            var mapped = rows.Select(a => new StudentsAccessmentDto
            {
                Id = a.Id,
                SubjectId = a.SubjectId,
                ClassArmId = a.ClassArmId,
                YearlyAndTermlyStatusId = a.YearlyAndTermlyStatusId,
                Ca1 = a.Ca1,
                Ca2 = a.Ca2,
                Ca3 = a.Ca3,
                Exams = a.Exams,
                Student = new StudentDetailsDto
                {
                    Id = a.Student.UserId,
                    SNo = a.Student.SNo,
                    IdNo = a.Student.IdNo,
                    CurrentClass = a.Student.CurrentClass,
                    User = new ApplicationUserDto
                    {
                        FirstName = a.Student.User.FirstName,
                        MiddleName = a.Student.User.MiddleName,
                        SurName = a.Student.User.SurName,
                        Gender = a.Student.User.Gender
                    }
                }
            }).ToList();

            var yearlyRepo = _uow.Repository<TxYearlyAndTermlyStatus>();
            var schoolId = await GetSchoolIdForTeacher(ct);
            var status = await yearlyRepo.FindAsync(t =>
                (t.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                 t.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing) &&
                t.Session.SchoolInfoId == schoolId, ct);

            return new StudentsAccessmentCasSettingDto
            {
                StudentsAccessment = mapped,
                CasSetting = new CasSettingDto
                {
                    CasCategory = status.CasCategory,
                    PerCasValue = status.PerCasValue
                }
            };
        }

        // Preserved name; now async
        public async Task<bool> CreateCasAndExamsScore(List<StudentsAccessmentDto> studentsAccessmentScores, CancellationToken ct = default)
        {
            if (studentsAccessmentScores == null || studentsAccessmentScores.Count == 0) return false;

            var teacher = await CurrentTeacherModel(ct);
            if (teacher == null) return false;

            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var now = DateTime.UtcNow;

            // NOTE: In high traffic, consider batching writes and executing within a transaction if required.
            foreach (var accessment in studentsAccessmentScores)
            {
                var existing = await assessRepo.FindAsync(accessment.Id, ct);
                if (existing == null) continue;

                existing.YearlyAndTermlyStatusId = accessment.YearlyAndTermlyStatusId ?? existing.YearlyAndTermlyStatusId;
                existing.Ca1 = accessment.Ca1;
                existing.Ca2 = accessment.Ca2;
                existing.Ca3 = accessment.Ca3;
                existing.Exams = accessment.Exams;
                existing.StudentId = accessment.StudentId ?? existing.StudentId;
                existing.ClassArmId = accessment.ClassArmId ?? existing.ClassArmId;
                existing.EmployeeId = teacher.Id;
                existing.SubjectId = accessment.SubjectId ?? existing.SubjectId;
                existing.LastUpdateDate = now;

                assessRepo.Update(existing);
            }

            await _uow.SaveChangesAsync(ct);
            return true;
        }

        // Preserved name; now async
        public async Task<ClassAndArmDropDownDto> GetClassArmForDropDown(CancellationToken ct = default)
        {
            if (_memoryCache.TryGetValue(ClassArmDropdownCacheKey, out ClassAndArmDropDownDto? cached) && cached != null)
            {
                return cached;
            }

            var classRepo = _uow.Repository<RxClasss>();
            var classArmRepo = _uow.Repository<AxClassArm>();

            var classes = (await classRepo.GetAllAsync(ct))
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.ClassName.ToString() });

            var arms = (await classArmRepo.GetAllAsync(ct))
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.ArmName.ToString() })
                .GroupBy(g => g.Text)
                .Select(g => g.First());

            var result = new ClassAndArmDropDownDto { ClassName = classes, ArmName = arms };
            _memoryCache.Set(ClassArmDropdownCacheKey, result, ClassArmDropdownCacheTtl);
            return result;
        }

        // Helpers (async versions)
        private async Task<string> GetSchoolIdForSchoolRegistrationOfficer(CancellationToken ct = default)
        {
            var userId = CurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return string.Empty;
            }

            return await RegistrySchoolLookup.ResolveSchoolRegistrationOfficerIdAsync(_uow, userId, ct)
                ?? string.Empty;
        }

        private async Task<SchoolTyInfo?> FindRegistrySchoolForSmsAsync(CancellationToken ct = default)
        {
            var userId = CurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            return await RegistrySchoolLookup.FindSchoolForRegistryUserAsync(
                _uow,
                userId,
                ct,
                s => s.SchoolRegistrationOfficer!.User!);
        }

        //private async Task<string> GetSchoolIdForTeacher(CancellationToken ct = default)
        //{
        //    var repo = _uow.Repository<TxEmployee>();
        //    var entity = await repo.FindAsync(e => e.UserId == CurrentUserId(), ct, r => r.SchoolInfo);
        //    return entity?.SchoolId;
        //}

        // Promotion validation preserved name; now async
        public async Task<string> StudentsPromotionValidations(CancellationToken ct = default)
        {
            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var total = await assessRepo.CountAsync(null, ct);
            if (total <= 1)
                return "Failed! You have not done any Students assessment yet";

            var currentSessionRows = await assessRepo.WhereAsync(a => a.YearlyAndTermlyStatus.Session.Status == TimeStatus.Current, ct, y => y.YearlyAndTermlyStatus);
            var termCount = currentSessionRows.GroupBy(g => g.YearlyAndTermlyStatus.TermStatus).Count();

            var sessionRepo = _uow.Repository<TxAcademicSession, short>();
            var sessionsCount = await sessionRepo.CountAsync(null, ct);
            var schoolId = await GetSchoolIdForTeacher(ct);

            if (sessionsCount == 1)
            {
                var firstTermRecord = (await assessRepo.WhereAsync(s => s.Employee.SchoolId == schoolId, ct))
                    .OrderBy(a => a.YearlyAndTermlyStatus.TermStatus)
                    .FirstOrDefault();

                if (firstTermRecord == null) return "Failed! You have not done any Students assessment yet";

                var startTerm = firstTermRecord.YearlyAndTermlyStatus.TermStatus;
                if (startTerm == TermStatus.First)
                    return termCount switch
                    {
                        1 => "The school has only done a single term and therefore you cannot run promotion",
                        2 => "The school has only done two terms and therefore you cannot run promotion; until when its three",
                        3 => "Success",
                        _ => "You have done something terribly wrong"
                    };
                if (startTerm == TermStatus.Second)
                    return termCount == 1
                        ? "The school has only done a single term and therefore you cannot run promotion"
                        : "Success";
                if (startTerm == TermStatus.Third)
                    return termCount == 1 ? "Success" : "You have done something terribly wrong";
            }

            return termCount switch
            {
                1 => "The school has only done a single term and therefore you cannot run promotion",
                2 => "The school has only done two terms and therefore you cannot run promotion; until when its three",
                3 => "Success",
                _ => "You have done something terribly wrong"
            };
        }

        // Remaining methods follow the same async pattern:
        // - Change signatures to Task<T> or Task<bool>
        // - Add optional CancellationToken parameter
        // - Use repository async methods (FindAsync, WhereAsync, CountAsync)
        // - Use _uow.SaveChangesAsync(ct) for mutations

        // Example: simple info getters (preserved names; now async)
        public async Task<SchoolInfoDto> GetSchoolModelForStudentAndParent(CancellationToken ct = default)
        {
            var repo = _uow.Repository<TxStudentDetail>();
            var entity = (await repo.FindAsync(u => u.UserId == CurrentUserId(), ct, s => s.SchoolInfo))?.SchoolInfo;
            return _mapper.Map<SchoolInfoDto>(entity);
        }

        public async Task<SchoolInfoDto> GetSchoolModelForSchoolRegistrationOfficer(CancellationToken ct = default)
        {
            var repo = _uow.Repository<SchoolRegistrationOfficer>();
            var entity = (await repo.FindAsync(u => u.UserId == CurrentUserId(), ct, s => s.SchoolInfo))?.SchoolInfo;
            return _mapper.Map<SchoolInfoDto>(entity);
        }

        public async Task<SchoolInfoDto> GetSchoolModel(CancellationToken ct = default)
        {
            var repo = _uow.Repository<TxEmployee>();
            var entity = (await repo.FindAsync(e => e.UserId == CurrentUserId(), ct, s => s.SchoolInfo))?.SchoolInfo;
            return _mapper.Map<SchoolInfoDto>(entity);
        }

        public async Task<SchoolInfoDto> VerifyAndGetSchoolInfo(CancellationToken ct = default)
        {
            var repo = _uow.Repository<TxStudentDetail>();
            var entity = (await repo.FindAsync(s => s.UserId == CurrentUserId(), ct, x => x.SchoolInfo))?.SchoolInfo;
            return _mapper.Map<SchoolInfoDto>(entity);
        }
        // School models (student/parent)
        //public async Task<SchoolInfoDto> GetSchoolModelForStudentAndParent(CancellationToken ct = default)
        //{
        //    var repo = _uow.Repository<TxStudentDetail>();
        //    var entity = (await repo.FindAsync(u => u.UserId == CurrentUserId(), ct, s => s.SchoolInfo))?.SchoolInfo;
        //    return _mapper.Map<SchoolInfoDto>(entity);
        //}

        //// School models (registration officer)
        //public async Task<SchoolInfoDto> GetSchoolModelForSchoolRegistrationOfficer(CancellationToken ct = default)
        //{
        //    var repo = _uow.Repository<SchoolRegistrationOfficer>();
        //    var entity = (await repo.FindAsync(u => u.UserId == CurrentUserId(), ct, s => s.SchoolInfo))?.SchoolInfo;
        //    return _mapper.Map<SchoolInfoDto>(entity);
        //}

        //// Helpers to get school ids
        //private async Task<string> GetSchoolIdForSchoolRegistrationOfficer(CancellationToken ct = default)
        //{
        //    var repo = _uow.Repository<SchoolRegistrationOfficer, string>();
        //    return (await repo.FindAsync(u => u.UserId == CurrentUserId(), ct))?.Id;
        //}

        private async Task<string> GetSchoolIdForTeacher(CancellationToken ct = default)
        {
            var repo = _uow.Repository<TxEmployee>();
            var entity = await repo.FindAsync(e => e.UserId == CurrentUserId(), ct, r => r.SchoolInfo);
            return entity?.SchoolId;
        }
        // Student subject list (left NotImplemented in original; keeping behavior)
        public Task<List<SubjectDto>> GetStudentSubjectListByStudentId(string admsnNo, string studentAdmsnNo, CancellationToken ct = default)
            => Task.FromException<List<SubjectDto>>(new NotImplementedException());

        // CA setting for arm/subject
        private async Task<SchoolHeadDto> SchoolInfoForTeacherResultProcessing(string schoolHeadId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(schoolHeadId)) return null;
            var repo = _uow.Repository<SchoolHeadTx>();
            var head = await repo.FindAsync(s => s.Id == schoolHeadId, ct, b => b.SchoolHeadCategory);
            if (head == null) return null;
            return new SchoolHeadDto
            {
                HeadsName = head.HeadsName,
                SignatureImageString = head.SignatureImageString,
                SchoolHeadCategoryId = head.SchoolHeadCategoryId,
                HeadStatus = head.SchoolHeadCategory?.HeadStatus ?? default,
                Gender = head.Gender
            };
        }

        // CA extraction by class and arm
        public async Task<CaResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> GetStudentsInAnArmForCaByClassNameAndArmName(CurrentClass clas, ArmName arm, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdForCurrentUserAsync(ct);
            if (string.IsNullOrEmpty(schoolId)) return null;

            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var currentStatus = (await assessRepo.FindAsync(
                s => s.Student.ClassArm.Classs.ClassName == clas && s.Student.ClassArm.ArmName == arm &&
                     s.Student.SchoolInfoId == schoolId &&
                     (s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                      s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing),
                ct, d => d.YearlyAndTermlyStatus.Session))?.YearlyAndTermlyStatus;
            if (currentStatus == null) return null;

            List<StudenXtAccessment> students = currentStatus.TermStatus == TermStatus.Third
                ? await assessRepo.WhereAsync(
                    s => s.Student.ClassArm.Classs.ClassName == clas && s.Student.ClassArm.ArmName == arm &&
                         s.YearlyAndTermlyStatus.SessionId == currentStatus.SessionId &&
                         s.Student.Status != StudentStatus.Previous, ct,
                    st => st.Student.User, em => em.Employee.User, su => su.Subject, yt => yt.YearlyAndTermlyStatus)
                : await assessRepo.WhereAsync(
                    s => s.Student.ClassArm.Classs.ClassName == clas && s.Student.ClassArm.ArmName == arm &&
                         s.YearlyAndTermlyStatusId == currentStatus.Id, ct,
                    st => st.Student.User, em => em.Employee.User, su => su.Subject, yt => yt.YearlyAndTermlyStatus);

            var mapped = _mapper.Map<List<StudentsAccessmentDto>>(students);
            if (mapped == null || !mapped.Any())
                return new CaResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto { StatusCode = 1 };

            var teacherAssignRepo = _uow.Repository<TeacherClassArmTxAssignment>();
            var classTeacher = await teacherAssignRepo.FindAsync(
                a => a.Arm.Classs.ClassName == clas && a.Arm.ArmName == arm && a.Arm.Classs.SchoolInfoId == schoolId, ct, v => v.Employee.User);

            var yearlyRepo = _uow.Repository<TxYearlyAndTermlyStatus>();
            var currentYearly = await yearlyRepo.FindAsync(
                d => d.Session.SchoolInfoId == schoolId && d.TermlyAccessmentStatus == TermlyAccessmentStatus.Current, ct, v => v.Session);

            SchoolHeadDto schoolHead = null;
            if (currentYearly != null)
            {
                var principalId = currentYearly.PrincipalId;
                var headMasterId = currentYearly.HeadmasterId;
                var isSenior = clas is CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3 || clas is CurrentClass.Jss1 or CurrentClass.Jss2 or CurrentClass.Jss3;
                schoolHead = await SchoolInfoForTeacherResultProcessing(isSenior ? principalId : headMasterId, ct);
            }

            var grAndOrd = mapped
                .GroupBy(r => r.StudentId)
                .OrderByDescending(s => s.Sum(r => r.Ca1 + r.Ca2 + r.Ca3 + r.Exams) / s.Count());

            if (classTeacher == null)
            {
                return new CaResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
                {
                    StatusCode = 2,
                    StudentsAccessment = grAndOrd,
                    CasSetting = new CasSettingDto
                    {
                        PerCasValue = mapped.Last().YearlyAndTermlyStatus.PerCasValue,
                        CasCategory = mapped.Last().YearlyAndTermlyStatus.CasCategory
                    }
                };
            }

            return new CaResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
            {
                AssessmentResultOutputXtractionForFormTeacher = new CaAssessmentOutputXtractionForFormTeacherDto
                {
                    FormMaster = new ArmFormMasterDto
                    {
                        FormMastersName = classTeacher.Employee.User.FullName,
                        SignatureString = classTeacher.Employee.SignatureImageString,
                        AssignedArmId = classTeacher.ArmId
                    },
                    YearlyAndTermlyAssessmentStatus = new YearlyAndTermlyStatusDto
                    {
                        NextTermBeginsDate = currentYearly?.NextTermBeginsDate,
                        TotalAttendance = currentYearly?.TotalAttendance ?? 0,
                        TermStatus = currentYearly?.TermStatus ?? TermStatus.First,
                        Session = currentYearly != null ? new AcademicSessionDto { Session = currentYearly.Session.Session } : null
                    }
                },
                StudentsAccessment = grAndOrd,
                SchoolHeadInfo = schoolHead,
                CasSetting = new CasSettingDto
                {
                    CasCategory = mapped.Last().YearlyAndTermlyStatus.CasCategory,
                    PerCasValue = mapped.Last().YearlyAndTermlyStatus.PerCasValue
                }
            };
        }

        // Affective + attendance summary — legacy GetAffectiveAndPsychomotiveForReUpdateWithAttendance
        public async Task<AffectiveAndPsychomotorAssessmentWithAttendanceDto> GetAffectiveAndPsychomotiveForReUpdateWithAttendance(string studentId, CancellationToken ct = default)
        {
            var yearlyRepo = _uow.Repository<TxYearlyAndTermlyStatus>();
            var teacherRepo = _uow.Repository<TxEmployee>();
            var teacher = await teacherRepo.FindAsync(e => e.UserId == CurrentUserId(), ct);
            var schoolId = teacher?.SchoolId;

            if (string.IsNullOrWhiteSpace(schoolId) || string.IsNullOrWhiteSpace(studentId))
            {
                return new AffectiveAndPsychomotorAssessmentWithAttendanceDto
                {
                    AffectiveAndPsychomotorAssessmentDto = new List<AffectiveAndPsychomotorAssessmentDto>(),
                };
            }

            var yearly = await yearlyRepo.FindAsync(
                s => (s.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                      || s.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing)
                     && s.Session != null
                     && s.Session.SchoolInfoId == schoolId,
                ct,
                s => s.Session!);
            if (yearly == null)
            {
                return new AffectiveAndPsychomotorAssessmentWithAttendanceDto
                {
                    AffectiveAndPsychomotorAssessmentDto = new List<AffectiveAndPsychomotorAssessmentDto>(),
                };
            }

            var affectRepo = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var list = await affectRepo.WhereAsync(
                r => r.StudentId == studentId && r.YearlyAndTermlyStatusId == yearly.Id,
                ct,
                r => r.AffectiveAndPsychomotor!,
                r => r.Student!);

            var attendanceRepo = _uow.Repository<StudentsAtteXzndanceAndCommentsReport>();
            var attendanceEntity = await attendanceRepo.FindAsync(
                a => a.StudentId == studentId && a.YearlyAndTermlyStatusId == yearly.Id,
                ct);

            var currentTerm = await yearlyRepo.FindAsync(
                y => y.Session != null
                     && y.Session.SchoolInfoId == schoolId
                     && y.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                y => y.Session!);

            // Manual map — no AutoMapper profile for AffectiveAndPsychomotorAssessmentDto
            // (AutoMapper.Map was throwing Missing type map → HTTP 500).
            var mapped = (list ?? new List<AffectiveAndPsychomoTxtorAssessment>())
                .OrderBy(r => r.ItemId)
                .Select(r => new AffectiveAndPsychomotorAssessmentDto
                {
                    Id = r.Id,
                    ItemId = r.ItemId,
                    Score = r.Score,
                    StudentId = r.StudentId,
                    ClassArmId = r.ClassArmId,
                    FormMasterId = r.FormMasterId,
                    AffectiveAndPsychomotor = r.AffectiveAndPsychomotor == null
                        ? null
                        : new AffectiveAndPsychomotorDto
                        {
                            Id = r.AffectiveAndPsychomotor.Id,
                            ItemsName = r.AffectiveAndPsychomotor.ItemsName,
                        },
                })
                .ToList();
            await AffectiveItemLabels.ApplyByClassArmAsync(
                _uow, mapped.Select(m => ((string?)m.ClassArmId, (AffectiveAndPsychomotorDto?)m.AffectiveAndPsychomotor)), ct);

            return new AffectiveAndPsychomotorAssessmentWithAttendanceDto
            {
                AffectiveAndPsychomotorAssessmentDto = mapped,
                Attendance = attendanceEntity?.Attendance ?? 0,
                TeacherRemarks = attendanceEntity?.TeacherComments ?? string.Empty,
                SchoolHeadRemarks = attendanceEntity?.SchoolHeadComments ?? string.Empty,
                TotalAttendance = currentTerm?.TotalAttendance ?? 0,
            };
        }

        // Legacy RegistrationService.GetAffectiveAndPsychomotorItems
        public async Task<AffectiveAndPsychomotorAndTotalAttendanceDto> GetAffectiveAndPsychomotorItems(CancellationToken ct = default)
        {
            var schoolId = await GetSchoolIdForTeacher(ct);
            var affectiveAndPsychomor = await _uow.Repository<AffectiveXzAndPsychomotor, byte>().GetAllAsync(ct);
            var result = new AffectiveAndPsychomotorAndTotalAttendanceDto
            {
                AffectiveAndPsychomotor = (affectiveAndPsychomor ?? Enumerable.Empty<AffectiveXzAndPsychomotor>())
                    .OrderBy(x => x.Id)
                    .Select(x => new AffectiveAndPsychomotorDto
                    {
                        Id = x.Id,
                        ItemsName = x.ItemsName,
                    })
                    .ToList(),
            };

            if (string.IsNullOrWhiteSpace(schoolId))
            {
                result.TotalAttendance = 255;
                return result;
            }

            var currentYearlyAndTermlyStatus = await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                x => x.Session != null
                     && x.Session.SchoolInfoId == schoolId
                     && x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                x => x.Session!);

            result.TotalAttendance = currentYearlyAndTermlyStatus == null
                ? (byte)255
                : currentYearlyAndTermlyStatus.TotalAttendance ?? 0;
            await AffectiveItemLabels.ApplyForSchoolAsync(_uow, schoolId, result.AffectiveAndPsychomotor, ct);
            return result;
        }

        // Legacy RegistrationService.CreateAffectiveAndPsychomotorScore (cloud-friendly payload)
        public async Task<bool> CreateAffectiveAndPsychomotorScore(FormMasterAffectivePsychomotorSubmitDto model, CancellationToken ct = default)
        {
            if (model == null
                || string.IsNullOrWhiteSpace(model.StudentId)
                || string.IsNullOrWhiteSpace(model.ArmId)
                || model.Scores == null
                || model.Scores.Count == 0)
            {
                return false;
            }

            var teacher = await CurrentTeacherModel(ct);
            if (teacher == null || string.IsNullOrWhiteSpace(teacher.SchoolId))
            {
                return false;
            }

            var schoolId = teacher.SchoolId;
            var yearly = await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                s => (s.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                      || s.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing)
                     && s.Session.SchoolInfoId == schoolId,
                ct);
            if (yearly == null)
            {
                return false;
            }

            var yearlyId = yearly.Id;
            var formMasterId = teacher.Id;
            var studentId = model.StudentId.Trim();
            var armId = model.ArmId.Trim();
            var affectRepo = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var attendanceRepo = _uow.Repository<StudentsAtteXzndanceAndCommentsReport>();

            var existingAny = await affectRepo.FindAsync(
                s => s.StudentId == studentId && s.YearlyAndTermlyStatusId == yearlyId,
                ct);
            var attendance = await attendanceRepo.FindAsync(
                s => s.StudentId == studentId && s.YearlyAndTermlyStatusId == yearlyId && s.Student.ClassArmId == armId,
                ct,
                s => s.Student!);

            if (existingAny != null)
            {
                foreach (var item in model.Scores)
                {
                    if (!byte.TryParse(item.Score, out var score))
                    {
                        continue;
                    }

                    var each = await affectRepo.FindAsync(
                        s => s.StudentId == studentId
                             && s.YearlyAndTermlyStatusId == yearlyId
                             && s.ItemId == item.ItemId
                             && s.ClassArmId == armId,
                        ct);

                    if (each != null)
                    {
                        each.Score = score;
                        affectRepo.Update(each);
                    }
                    else
                    {
                        var assignment = await _uow.Repository<TeacherClassArmTxAssignment>()
                            .FindAsync(t => t.ArmId == armId, ct);
                        affectRepo.Add(new AffectiveAndPsychomoTxtorAssessment
                        {
                            Id = Guid.NewGuid().ToString(),
                            ClassArmId = armId,
                            YearlyAndTermlyStatusId = yearlyId,
                            StudentId = studentId,
                            ItemId = item.ItemId,
                            Score = score,
                            FormMasterId = assignment?.EmployeeId ?? formMasterId,
                        });
                    }
                }

                if (attendance == null)
                {
                    attendanceRepo.Add(new StudentsAtteXzndanceAndCommentsReport
                    {
                        Id = Guid.NewGuid().ToString(),
                        ClassArmId = armId,
                        YearlyAndTermlyStatusId = yearlyId,
                        StudentId = studentId,
                        Attendance = model.Attendance,
                        SchoolHeadComments = model.SchoolHeadComment,
                        TeacherComments = model.TeacherComment,
                    });
                }
                else
                {
                    attendance.Attendance = model.Attendance;
                    attendance.TeacherComments = model.TeacherComment;
                    attendance.SchoolHeadComments = model.SchoolHeadComment;
                    attendanceRepo.Update(attendance);
                }
            }
            else
            {
                foreach (var item in model.Scores)
                {
                    if (!byte.TryParse(item.Score, out var score))
                    {
                        continue;
                    }

                    affectRepo.Add(new AffectiveAndPsychomoTxtorAssessment
                    {
                        Id = Guid.NewGuid().ToString(),
                        ItemId = item.ItemId,
                        Score = score,
                        StudentId = studentId,
                        YearlyAndTermlyStatusId = yearlyId,
                        FormMasterId = formMasterId,
                        ClassArmId = armId,
                    });
                }

                attendanceRepo.Add(new StudentsAtteXzndanceAndCommentsReport
                {
                    Id = Guid.NewGuid().ToString(),
                    StudentId = studentId,
                    Attendance = model.Attendance,
                    TeacherComments = model.TeacherComment,
                    SchoolHeadComments = model.SchoolHeadComment,
                    YearlyAndTermlyStatusId = yearlyId,
                    ClassArmId = armId,
                });
            }

            await _uow.SaveChangesAsync(ct);
            return true;
        }

        // Subject list by arm with status filter
        private async Task<IEnumerable<SubjectDto>> GetSubjectForAnArmByArmId(SubjectStatus basicSubjectStatus, CancellationToken ct = default)
        {
            var subjectRepo = _uow.Repository<RxSubject>();
            var subjects = await subjectRepo.WhereAsync(
                sr => sr.SubjectCategory.SubjectStatus == basicSubjectStatus || sr.SubjectCategory.SubjectStatus == SubjectStatus.General,
                ct, s => s.SubjectCategory);

            if (subjects == null || subjects.Count == 0)
                return Array.Empty<SubjectDto>();

            return _mapper.Map<List<SubjectDto>>(subjects);
        }








        







        // -------- Remaining legacy synchronous methods converted to fully asynchronous variants below --------
        // NOTE: Original method names preserved. Each now returns Task<T> and accepts optional CancellationToken.
        // Remove (or mark obsolete) the old synchronous versions to avoid duplicate definitions.

        public async Task<YearlyResultProcessingsToReportSheetDto> GetStudentYearlyReportSheet(string armId, CancellationToken ct = default)
        {
            var validation = await StudentsPromotionValidations(ct);
            if (validation != "Success")
            {
                return new YearlyResultProcessingsToReportSheetDto
                {
                    ErrorMsg = validation == "The school has only done a single term and therefore you cannot run promotion"
                        ? "The school has only done a single term and therefore you cannot compute yearly student result processing."
                        : validation
                };
            }

            var classArmRepo = _uow.Repository<AxClassArm>();
            var classEntity = await classArmRepo.FindAsync(a => a.Id == armId, ct, s => s.Classs);
            if (classEntity == null)
                return new YearlyResultProcessingsToReportSheetDto { ErrorMsg = "Arm not found" };

            var classId = classEntity.Classs.Id;
            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var rows = await assessRepo.WhereAsync(s =>
                    s.ClassArm.ClassId == classId &&
                    s.YearlyAndTermlyStatus.Session.Status == TimeStatus.Current,
                ct,
                s => s.Student.User,
                s => s.Subject,
                s => s.YearlyAndTermlyStatus.Session,
                s => s.ClassArm.Classs);

            if (rows == null || rows.Count == 0)
                return new YearlyResultProcessingsToReportSheetDto { ErrorMsg = "No data" };

            var mappedAssessments = rows.Select(r => new StudentsAccessmentDto
            {
                YearlyAndTermlyStatus = _mapper.Map<YearlyAndTermlyStatusDto>(r.YearlyAndTermlyStatus),
                ClassArmId = r.ClassArmId,
                Ca1 = r.Ca1,
                Ca2 = r.Ca2,
                Ca3 = r.Ca3,
                Exams = r.Exams,
                Id = r.Id,
                StudentId = r.StudentId,
                Subject = _mapper.Map<SubjectDto>(r.Subject),
                ClassArm = _mapper.Map<ClassArmDto>(r.ClassArm),
                Student = new StudentDetailsDto
                {
                    FirstName = r.Student.User.FirstName,
                    MiddleName = r.Student.User.MiddleName,
                    SurName = r.Student.User.SurName
                }
            }).ToList();

            var attendanceRepo = _uow.Repository<StudentsAtteXzndanceAndCommentsReport>();
            var attendance = await attendanceRepo.WhereAsync(a => a.ClassArmId == armId && a.YearlyAndTermlyStatus.Session.Status == TimeStatus.Current, ct);

            var affectRepo = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var formMasters = (await affectRepo.WhereAsync(a =>
                    a.ClassArmId == armId &&
                    a.YearlyAndTermlyStatus.Session.Status == TimeStatus.Current,
                ct,
                t => t.FormMaster.User))
                .GroupBy(t => t.YearlyAndTermlyStatus.TermStatus)
                .Select(g => g.First())
                .Select(f => new FormMastersNameDto
                {
                    FormMastersName = f.FormMaster.User.FullName,
                    Term = f.YearlyAndTermlyStatus.TermStatus
                }).ToList();

            return new YearlyResultProcessingsToReportSheetDto
            {
                StudentsAccessment = mappedAssessments,
                StudentsAttendance = _mapper.Map<List<StudentsAttendanceDto>>(attendance),
                ArmFormMaster = formMasters,
                ErrorMsg = "Success"
            };
        }

        public async Task<bool> UpdateFeatureImplementationType(byte impleType, string schoolId, CancellationToken ct = default)
        {
            var schoolInfoRepo = _uow.Repository<SchoolTyInfo, string>();
            var school = await schoolInfoRepo.FindAsync(t => t.SchoolRegistrationOfficerId == schoolId, ct);
            if (school == null) return false;

            var systemId = FreePlugIn.GetUniqueSystemId();
            var implementationType = $"{systemId}:{impleType}";
            school.FeatureImplementationType = FreePlugIn.Encrypt(implementationType, true);
            schoolInfoRepo.Update(school);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> ChangeTermlyAssessmentStatus(string conCatParameters, CancellationToken ct = default)
        {
            var parts = conCatParameters.Split(',');
            if (parts.Length != 2) return false;

            var armId = parts[0].Trim();
            var statusToken = parts[1].Trim();
            if (string.IsNullOrWhiteSpace(armId) || string.IsNullOrWhiteSpace(statusToken)) return false;

            ArmTermlyAssessmentStatus status;
            if (byte.TryParse(statusToken, out var statusByte) && Enum.IsDefined(typeof(ArmTermlyAssessmentStatus), statusByte))
            {
                status = (ArmTermlyAssessmentStatus)statusByte;
            }
            else if (!Enum.TryParse(statusToken, ignoreCase: true, out status))
            {
                return false;
            }

            var schoolId = await GetSchoolIdForTeacher(ct);
            var assignRepo = _uow.Repository<TeacherClassArmTxAssignment>();
            var assignment = string.IsNullOrWhiteSpace(schoolId)
                ? await assignRepo.FindAsync(t => t.ArmId == armId, ct)
                : await assignRepo.FindAsync(
                    t => t.ArmId == armId && t.Employee != null && t.Employee.SchoolId == schoolId,
                    ct,
                    t => t.Employee!);

            if (assignment == null) return false;

            assignment.ArmTermlyAssessmentStatus = status;
            assignRepo.Update(assignment);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        public Task<bool> GetStudentResultByStudentId(string studentAdmsnNo, string schoolRegId, CancellationToken ct = default)
            => Task.FromException<bool>(new NotImplementedException());

        public async Task<StudentDetailsDto> GetStudentDetailsByStudentId(CancellationToken ct = default)
        {
            var student = await ResolvePortalStudentAsync(ct);
            return _mapper.Map<StudentDetailsDto>(student);
        }

        public async Task<List<StudentPortalSiblingDto>> GetSiblingStudentsBySharedMobileNumber(CancellationToken ct = default)
        {
            var current = await GetLoggedInPortalStudentAsync(ct);
            if (current == null)
            {
                return new List<StudentPortalSiblingDto>();
            }

            var mobile = NormalizeMobileNumber(current.MobileNumber);
            if (string.IsNullOrWhiteSpace(mobile) || string.IsNullOrWhiteSpace(current.SchoolInfoId))
            {
                return new List<StudentPortalSiblingDto>
                {
                    ToSiblingDto(current, isCurrentLogin: true),
                };
            }

            var studentRepo = _uow.Repository<TxStudentDetail>();
            var schoolId = current.SchoolInfoId;
            var peers = await studentRepo.WhereAsync(
                s => s.SchoolInfoId == schoolId && s.MobileNumber != null && s.MobileNumber != "",
                ct,
                s => s.ClassArm!.Classs!,
                s => s.User!);

            var siblings = peers
                .Where(s => string.Equals(NormalizeMobileNumber(s.MobileNumber), mobile, StringComparison.Ordinal))
                .OrderBy(s => s.User?.SurName ?? string.Empty)
                .ThenBy(s => s.User?.FirstName ?? string.Empty)
                .ThenBy(s => s.IdNo)
                .Select(s => ToSiblingDto(
                    s,
                    isCurrentLogin: string.Equals(s.UserId, current.UserId, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (siblings.Count == 0)
            {
                siblings.Add(ToSiblingDto(current, isCurrentLogin: true));
            }

            return siblings;
        }

        private static StudentPortalSiblingDto ToSiblingDto(TxStudentDetail student, bool isCurrentLogin)
        {
            var first = student.User?.FirstName?.Trim() ?? string.Empty;
            var middle = student.User?.MiddleName?.Trim() ?? string.Empty;
            var sur = student.User?.SurName?.Trim() ?? string.Empty;
            var display = string.Join(' ', new[] { first, middle, sur }.Where(p => !string.IsNullOrWhiteSpace(p)));
            if (string.IsNullOrWhiteSpace(display))
            {
                display = student.IdNo?.Trim() ?? student.UserId;
            }

            var email = student.EmailAddress?.Trim()
                ?? student.User?.Email?.Trim()
                ?? string.Empty;

            return new StudentPortalSiblingDto
            {
                Id = student.UserId,
                IdNo = student.IdNo ?? string.Empty,
                DisplayName = display,
                Email = email,
                ClassLabel = student.ClassArm?.Classs?.ClassLabel ?? string.Empty,
                ArmLabel = student.ClassArm?.ArmName.ToString() ?? string.Empty,
                IsCurrentLogin = isCurrentLogin,
            };
        }

        public async Task<List<SelectListItem>> GetSubjectCategoryForDropDown(CancellationToken ct = default)
        {
            var student = await ResolvePortalStudentAsync(ct);
            if (student?.SchoolInfo == null) return new List<SelectListItem>();

            var subjectRepo = _uow.Repository<RxSubject>();
            var subjects = await subjectRepo.WhereAsync(
                s => s.SubjectCategory.SchoolInfo.SsNo == student.SchoolInfo.SsNo,
                ct,
                s => s.SubjectCategory,
                s => s.SubjectCategory!.SchoolInfo!);

            return subjects
                .GroupBy(r => r.SubjectCategory.SubjectStatus)
                .Select(g => g.First())
                .Select(c =>
                {
                    var status = c.SubjectCategory.SubjectStatus;
                    var configuredLabel = c.SubjectCategory.SubjectStatusLabel?.Trim();
                    return new SelectListItem
                    {
                        Value = status.ToString(),
                        Text = !string.IsNullOrWhiteSpace(configuredLabel)
                            ? configuredLabel
                            : DefaultSubjectStatusLabel(status),
                    };
                })
                .ToList();
        }

        private static string DefaultSubjectStatusLabel(SubjectStatus status) => status switch
        {
            SubjectStatus.PlayClass => "Play Class Subjects",
            SubjectStatus.PreNursery => "Pre Nursery Subjects",
            SubjectStatus.Nursery => "Nursery Subjects",
            SubjectStatus.Primary => "Primary Subjects",
            SubjectStatus.Jss => "JSS Subjects",
            SubjectStatus.General => "General Subjects",
            SubjectStatus.AllSeniors => "All Senior Subjects",
            SubjectStatus.Elective => "Elective Subjects",
            SubjectStatus.Science => "Science Subjects",
            SubjectStatus.Art => "Art Subjects",
            SubjectStatus.Commercial => "Commercial Subjects",
            _ => status.ToString(),
        };

        public async Task<bool> VerifyStudentExistance(CancellationToken ct = default)
        {
            var userId = CurrentUserId();
            var studentRepo = _uow.Repository<TxStudentDetail>();
            var student = await studentRepo.FindAsync(s => s.UserId == userId, ct, s => s.SchoolInfo, s => s.ClassArm.Classs);
            return student != null;
        }

        public async Task<List<SubjectDto>> SeniorClassesSubjectList(ClassCategoryStatus studentStatus, CurrentClass classs, int schoolRegId, CancellationToken ct = default)
        {
            var subjectRepo = _uow.Repository<RxSubject>();
            Expression<Func<RxSubject, bool>> predicate = s =>
                s.SubjectCategory.SchoolInfo.SsNo == schoolRegId &&
                s.TimeStatus == TimeStatus.Current &&
                (s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                 s.SubjectCategory.SubjectStatus == SubjectStatus.AllSeniors ||
                 (studentStatus == ClassCategoryStatus.Science && s.SubjectCategory.SubjectStatus == SubjectStatus.Science) ||
                 (studentStatus == ClassCategoryStatus.Art && s.SubjectCategory.SubjectStatus == SubjectStatus.Art) ||
                 (studentStatus == ClassCategoryStatus.Commercial && s.SubjectCategory.SubjectStatus == SubjectStatus.Commercial));

            var subjects = await subjectRepo.WhereAsync(predicate, ct, d => d.SubjectCategory);
            return _mapper.Map<List<SubjectDto>>(subjects);
        }

        public async Task<List<SubjectDto>> BasicClassesSubjectList(CurrentClass className, int schoolRegId, CancellationToken ct = default)
        {
            var subjectRepo = _uow.Repository<RxSubject>();
            Expression<Func<RxSubject, bool>> predicate = className switch
            {
                CurrentClass.PreNursery => s => s.SubjectCategory.SchoolInfo.SsNo == schoolRegId &&
                                                s.TimeStatus == TimeStatus.Current &&
                                                s.SubjectCategory.SubjectStatus == SubjectStatus.PreNursery,
                CurrentClass.Nursery1 or CurrentClass.Nursery2 => s => s.SubjectCategory.SchoolInfo.SsNo == schoolRegId &&
                                                                       s.TimeStatus == TimeStatus.Current &&
                                                                       (s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                                                                        s.SubjectCategory.SubjectStatus == SubjectStatus.Nursery),
                CurrentClass.Primary1 or CurrentClass.Primary2 or CurrentClass.Primary3 or CurrentClass.Primary4 or CurrentClass.Primary5 or CurrentClass.Primary6
                    => s => s.SubjectCategory.SchoolInfo.SsNo == schoolRegId &&
                            s.TimeStatus == TimeStatus.Current &&
                            (s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                             s.SubjectCategory.SubjectStatus == SubjectStatus.Primary),
                CurrentClass.Jss1 or CurrentClass.Jss2 or CurrentClass.Jss3
                    => s => s.SubjectCategory.SchoolInfo.SsNo == schoolRegId &&
                            s.TimeStatus == TimeStatus.Current &&
                            (s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                             s.SubjectCategory.SubjectStatus == SubjectStatus.Jss),
                _ => s => false
            };

            var subjects = await subjectRepo.WhereAsync(predicate, ct, d => d.SubjectCategory);
            return _mapper.Map<List<SubjectDto>>(subjects);
        }

        public async Task<SubjectsAndClassName> GetLevelsThatOfferThisSubject(string subjectStatus, CancellationToken ct = default)
        {
            var student = await ResolvePortalStudentAsync(ct);
            if (student == null) return new SubjectsAndClassName();

            var subjectRepo = _uow.Repository<RxSubject>();
            var subjects = new List<SubjectDto>();
            var electiveStatusList = new List<ElectiveStatusDto>();

            if (subjectStatus?.Trim() == "AllSubjects")
            {
                var all = await subjectRepo.WhereAsync(r => r.SubjectCategory.SchoolInfo.SsNo == student.SchoolInfo.SsNo, ct, c => c.SubjectCategory);
                subjects.AddRange(_mapper.Map<List<SubjectDto>>(all));
            }
            else if (subjectStatus?.Trim() == "My-subject-list")
            {
                // base subjects
                if (student.ClassCategory is ClassCategoryStatus.Nursery or ClassCategoryStatus.Primary or ClassCategoryStatus.Jss)
                {
                    subjects.AddRange(await BasicClassesSubjectList(student.ClassArm.Classs.ClassName, student.SchoolInfo.SsNo, ct));
                }
                else
                {
                    subjects.AddRange(await SeniorClassesSubjectList(student.ClassCategory, student.CurrentClass, student.SchoolInfo.SsNo, ct));
                }

                // electives chosen by current (or selected sibling) student
                var assignmentRepo = _uow.Repository<SubjectStudentTxAssignment>();
                var electives = await assignmentRepo.WhereAsync(d =>
                        d.StudentDetail.UserId == student.UserId &&
                        d.StudentDetail.SchoolInfo.SsNo == student.SchoolInfo.SsNo,
                    ct,
                    s => s.Subject.SubjectCategory);

                subjects.AddRange(_mapper.Map<List<SubjectDto>>(electives.Select(e => e.Subject)));

                // Build elective status list (compulsory/optional)
                var studentRepo = _uow.Repository<TxStudentDetail>();
                var classArmStudents = await studentRepo.WhereAsync(n => n.ClassArmId == student.ClassArmId, ct);
                var allElectiveAssignments = await assignmentRepo.WhereAsync(d => d.StudentDetail.ClassArmId == student.ClassArmId, ct);

                if (allElectiveAssignments.Any())
                {
                    foreach (var group in allElectiveAssignments.GroupBy(g => g.SubjectId))
                    {
                        var grouped = group.ToList();
                        ElectiveStatusCode code;

                        if (grouped.Count == classArmStudents.Count)
                        {
                            code = ElectiveStatusCode.CompulsaryToSomeStudents;
                        }
                        else
                        {
                            var cat = student.ClassCategory;
                            var categoryCount = classArmStudents.Count(c => c.ClassCategory == cat);
                            var categoryElectiveCount = grouped.Count(d => d.StudentDetail.ClassCategory == cat);

                            code = categoryElectiveCount == categoryCount
                                ? ElectiveStatusCode.CompulsaryToSomeStudents
                                : ElectiveStatusCode.OptionalToAllStudents;
                        }

                        electiveStatusList.Add(new ElectiveStatusDto
                        {
                            ElectiveId = group.Key,
                            ElectiveStatusCode = code
                        });
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(subjectStatus))
            {
                if (Enum.TryParse(subjectStatus, out SubjectStatus parsed))
                {
                    var filtered = await subjectRepo.WhereAsync(r =>
                            r.SubjectCategory.SubjectStatus == parsed &&
                            r.SubjectCategory.SchoolInfo.SsNo == student.SchoolInfo.SsNo,
                        ct,
                        c => c.SubjectCategory);
                    subjects.AddRange(_mapper.Map<List<SubjectDto>>(filtered));
                }
            }

            return new SubjectsAndClassName
            {
                SubjectDtos = subjects,
                StudentStatus = student.ClassCategory,
                ClassName = student.ClassArm.Classs.ClassName,
                ElectiveStatus = electiveStatusList,
                AccessStatu = student.SchoolInfo.ElectiveAccessStatus
            };
        }

        public async Task<string> CreateSubjectStudentAssignment(List<SubjectIdsBto> subjectIds, CancellationToken ct = default)
        {
            var student = await ResolvePortalStudentAsync(ct);
            if (student == null) return "Failed";
            if (student.Status == StudentStatus.Previous)
                return "Failed! Previous students cannot select elective subjects or generate assessments.";
            if (student.ClassArm.Classs.ClassName == CurrentClass.PreNursery)
                return $"Failed! {student.ClassArm.Classs.ClassLabel} students do not offer elective subjects";

            var assignmentRepo = _uow.Repository<SubjectStudentTxAssignment>();
            var subjectRepo = _uow.Repository<RxSubject>();
            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var teacherAssignRepo = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var yearlyRepo = _uow.Repository<TxYearlyAndTermlyStatus>();

            foreach (var sid in subjectIds)
            {
                //if (!int.TryParse(sid.Id, out var subjectId)) continue;
                var subjectId = sid.Id;
                var subject = await subjectRepo.FindAsync(r => r.Id == subjectId, ct, k => k.SubjectCategory);
                if (subject == null) continue;

                var exists = await assignmentRepo.FindAsync(d =>
                        d.StudentId == student.UserId &&
                        d.SubjectId == subjectId &&
                        d.StudentDetail.SchoolInfo.SsNo == student.SchoolInfo.SsNo,
                    ct);

                if (exists == null)
                {
                    var status = subject.SubjectCategory.SubjectStatus;
                    var allowed = student.ClassCategory switch
                    {
                        ClassCategoryStatus.Nursery or ClassCategoryStatus.Primary or ClassCategoryStatus.Jss
                            => status == SubjectStatus.Elective,
                        ClassCategoryStatus.Science => status is SubjectStatus.Art or SubjectStatus.Commercial or SubjectStatus.Elective,
                        ClassCategoryStatus.Art => status is SubjectStatus.Science or SubjectStatus.Commercial or SubjectStatus.Elective,
                        ClassCategoryStatus.Commercial => status is SubjectStatus.Science or SubjectStatus.Art or SubjectStatus.Elective,
                        _ => false
                    };
                    if (!allowed)
                        return $"Failed! {student.ClassArm.Classs.ClassLabel} students do not offer {subject.SubjectName}";

                    assignmentRepo.Add(new SubjectStudentTxAssignment
                    {
                        Id = Guid.NewGuid().ToString(),
                        StudentId = student.UserId,
                        SubjectId = subjectId
                    });
                }

                var yearly = await yearlyRepo.FindAsync(y =>
                    y.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                    y.Session.SchoolInfoId == student.SchoolInfoId, ct);

                var teacherAssignment = await teacherAssignRepo.FindAsync(t =>
                    t.ArmId == student.ClassArmId && t.SubjectId == subjectId, ct);

                if (yearly?.Id != null && teacherAssignment?.EmployeeId != null)
                {
                    var assessExists = await assessRepo.FindAsync(a =>
                        a.StudentId == student.UserId &&
                        a.ClassArmId == student.ClassArmId &&
                        a.SubjectId == subjectId &&
                        a.YearlyAndTermlyStatusId == yearly.Id, ct);

                    if (assessExists == null)
                    {
                        assessRepo.Add(new StudenXtAccessment
                        {
                            Id = Guid.NewGuid().ToString(),
                            ClassArmId = student.ClassArmId,
                            StudentId = student.UserId,
                            EmployeeId = teacherAssignment.EmployeeId,
                            SubjectId = subjectId,
                            Ca1 = 0,
                            Ca2 = 0,
                            Ca3 = 0,
                            Exams = 0,
                            LastUpdateDate = DateTime.UtcNow,
                            YearlyAndTermlyStatusId = yearly.Id
                        });
                    }
                }
            }

            await _uow.SaveChangesAsync(ct);
            return "succeed";
        }

        public async Task<bool> RemoveSubjectStudentAssignment(List<SubjectIdsBto> subjectIds, CancellationToken ct = default)
        {
            var student = await ResolvePortalStudentAsync(ct);
            if (student == null) return false;

            var studentUserId = student.UserId;
            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var assignmentRepo = _uow.Repository<SubjectStudentTxAssignment>();

            foreach (var sid in subjectIds)
            {
                //if (!int.TryParse(sid.Id, out var subjectId)) continue;

                var subjectId = sid.Id;

                var assess = await assessRepo.FindAsync(s =>
                    s.SubjectId == subjectId &&
                    s.Student.UserId == studentUserId &&
                    s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current, ct);
                if (assess != null) assessRepo.Delete(assess);

                var elect = await assignmentRepo.FindAsync(d => d.SubjectId == subjectId && d.StudentDetail.UserId == studentUserId, ct);
                if (elect != null) assignmentRepo.Delete(elect);
            }

            await _uow.SaveChangesAsync(ct);
            return true;
        }

        public async Task<IList<SelectListItem>> GetTermsDropdown(CancellationToken ct = default)
        {
            var schoolId = await SchoolIdForStudentOrParent(ct);
            var statusRepo = _uow.Repository<TxYearlyAndTermlyStatus>();
            var statuses = await statusRepo.WhereAsync(r => r.Session.SchoolInfoId == schoolId, ct);

            return statuses
                .GroupBy(f => f.TermStatus)
                .Select(v => v.First())
                .Select(r => new SelectListItem { Text = r.TermStatus.ToString(), Value = ((int)r.TermStatus).ToString() })
                .ToList();
        }

        public async Task<YearlyResultProcessingsToReportSheetDto> GetYearlyAssessmentForSelectedStudent(string selectedSessionId, CancellationToken ct = default)
        {
            var validation = await StudentsPromotionValidations(ct);
            if (validation != "Success")
            {
                return new YearlyResultProcessingsToReportSheetDto
                {
                    ErrorMsg = validation == "The school has only done a single term and therefore you cannot run promotion"
                        ? "The school has only done a single term and therefore you cannot compute yearly student result processing."
                        : validation
                };
            }

            var schoolId = await ResolveSchoolIdForAssessmentLookup(ct);
            var student = await ResolvePortalStudentAsync(ct);
            if (student == null || !string.Equals(student.SchoolInfoId, schoolId, StringComparison.OrdinalIgnoreCase))
            {
                return new YearlyResultProcessingsToReportSheetDto { ErrorMsg = "Student not found" };
            }

            return await GetYearlyAssessmentForSelectedStudent(student.IdNo, selectedSessionId, ct);
        }

        /// <summary>
        /// Legacy TeachersAndOthersService.GetTermlyAssessmentForSelectedStudent(int sessionId, TermStatus termStatus)
        /// — current logged-in student/parent self-service result sheet.
        /// Same algorithm/mapping as the IdNo overload (sequential queries; no shared-DbContext parallelism).
        /// </summary>
        public async Task<UsersSingleStudentResultProcessingsToReportSheetXtractionDto> GetTermlyAssessmentForSelectedStudent(string sessionId, TermStatus termStatus, CancellationToken ct = default)
        {
            var yearlyRepo = _uow.Repository<TxYearlyAndTermlyStatus>();
            var selectedTerm = await yearlyRepo.FindAsync(x => x.SessionId == sessionId && x.TermStatus == termStatus, ct);
            var studentModel = await ResolvePortalStudentAsync(ct);
            if (studentModel?.ClassArm?.Classs == null)
            {
                return new UsersSingleStudentResultProcessingsToReportSheetXtractionDto
                {
                    StatusCode = 3,
                    GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting =
                        new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto(),
                    StudentId = string.Empty,
                };
            }

            // Legacy: studentId = studentModel.Id (NotMapped alias of UserId).
            var studentId = studentModel.Id;
            var clas = studentModel.ClassArm.Classs.ClassName;
            var arm = studentModel.ClassArm.ArmName;
            var schoolInfoId = studentModel.SchoolInfoId;

            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var focus = await assessRepo.FindAsync(
                s => s.StudentId == studentModel.UserId
                     && s.YearlyAndTermlyStatus.SessionId == sessionId
                     && s.YearlyAndTermlyStatus.TermStatus == termStatus,
                ct,
                d => d.ClassArm.Classs,
                v => v.YearlyAndTermlyStatus);

            if (focus == null || string.IsNullOrWhiteSpace(focus.ClassArmId))
            {
                return new UsersSingleStudentResultProcessingsToReportSheetXtractionDto
                {
                    StatusCode = 3,
                    GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting =
                        new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto(),
                    StudentId = studentId,
                };
            }

            var armId = focus.ClassArmId;

            // Arm-wide rows required for class position (legacy).
            List<StudenXtAccessment> students;
            if (termStatus == TermStatus.Third)
            {
                var requireCurrent = selectedTerm?.TermlyAccessmentStatus == TermlyAccessmentStatus.Current;
                students = await assessRepo.WhereAsync(
                    s => s.ClassArmId == armId
                         && s.YearlyAndTermlyStatus.SessionId == sessionId
                         && (!requireCurrent
                             || (s.Student.Status != StudentStatus.Previous && s.Subject.TimeStatus == TimeStatus.Current)),
                    ct,
                    st => st.Student.User,
                    em => em.Employee.User,
                    su => su.Subject,
                    yt => yt.YearlyAndTermlyStatus,
                    ca => ca.ClassArm.Classs);
            }
            else
            {
                students = await assessRepo.WhereAsync(
                    s => s.ClassArmId == armId
                         && s.YearlyAndTermlyStatus.SessionId == sessionId
                         && s.YearlyAndTermlyStatus.TermStatus == termStatus,
                    ct,
                    st => st.Student.User,
                    em => em.Employee.User,
                    su => su.Subject,
                    yt => yt.YearlyAndTermlyStatus,
                    ca => ca.ClassArm.Classs);
            }

            if (students == null || students.Count == 0)
            {
                return new UsersSingleStudentResultProcessingsToReportSheetXtractionDto
                {
                    StatusCode = 1,
                    GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting =
                        new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto(),
                };
            }

            var yearlyAndTermlyStatusId = !string.IsNullOrWhiteSpace(selectedTerm?.Id)
                ? selectedTerm!.Id
                : focus.YearlyAndTermlyStatusId;

            var affectRepo = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var affect = await affectRepo.WhereAsync(
                a => a.ClassArmId == armId
                     && a.FormMaster.SchoolId == schoolInfoId
                     && a.YearlyAndTermlyStatus.SessionId == sessionId
                     && a.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId,
                ct,
                af => af.AffectiveAndPsychomotor,
                cl => cl.ClassArm.Classs,
                fm => fm.FormMaster.User,
                st => st.Student.User,
                ys => ys.YearlyAndTermlyStatus.Session,
                stc => stc.Student.ClassArm.Classs);

            if (affect == null || affect.Count == 0)
            {
                return new UsersSingleStudentResultProcessingsToReportSheetXtractionDto
                {
                    StatusCode = 2,
                    GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting =
                        new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto(),
                };
            }

            var attendance = await LoadReportAttendanceAsync(
                yearlyAndTermlyStatusId,
                armId,
                a => a.Student.ClassArm.Classs.ClassName == clas
                     && a.Student.ClassArm.ArmName == arm
                     && a.Student.SchoolInfoId == schoolInfoId
                     && (a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                         || a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing),
                ct);

            var mapped = StudentAssessmentMapping.MapList(students);
            if (mapped.Count == 0)
            {
                return new UsersSingleStudentResultProcessingsToReportSheetXtractionDto
                {
                    StatusCode = 1,
                    GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting =
                        new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto(),
                };
            }

            SchoolHeadDto? schoolHead = null;
            var schoolHeadId = clas is CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3
                or CurrentClass.Jss1 or CurrentClass.Jss2 or CurrentClass.Jss3
                ? selectedTerm?.PrincipalId
                : selectedTerm?.HeadmasterId;
            if (!string.IsNullOrWhiteSpace(schoolHeadId))
            {
                schoolHead = await SchoolInfoForTeacherResultProcessing(schoolHeadId, ct);
            }

            var casSetting = StudentAssessmentMapping.ResolveCasSetting(mapped)
                ?? new CasSettingDto
                {
                    CasCategory = mapped.Last().YearlyAndTermlyStatus?.CasCategory ?? default,
                    PerCasValue = mapped.Last().YearlyAndTermlyStatus?.PerCasValue ?? 0,
                };

            var singleReport = new UsersSingleStudentResultProcessingsToReportSheetXtractionDto
            {
                StudentId = studentId,
                GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting =
                    new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
                    {
                        AffectiveAndPsychomotorAssessment = affect.Select(f => new AffectiveAndPsychomotorAssessmentOutputXtractionForFormTeacherDto
                        {
                            ClassArmId = f.ClassArmId,
                            FormMaster = new ArmFormMasterDto
                            {
                                FormMastersName = f.FormMaster?.User?.FullName ?? string.Empty,
                                SignatureString = f.FormMaster?.SignatureImageString,
                                AssignedArmId = f.ClassArmId
                            },
                            Student = new StudentFielExtractionDto
                            {
                                FullName = f.Student?.User?.FullName ?? string.Empty,
                                Id = f.StudentId,
                                CurrentClass = f.ClassArm?.Classs?.ClassName ?? default,
                                ArmName = f.ClassArm?.ArmName ?? default,
                                CurrentClassLabel = f.Student?.ClassArm?.Classs?.ClassLabel
                                    ?? f.ClassArm?.Classs?.ClassLabel
                            },
                            AffectiveAndPsychomotor = StudentAssessmentMapping.MapPsychomotor(f.AffectiveAndPsychomotor),
                            YearlyAndTermlyAssessmentStatus = f.YearlyAndTermlyStatus == null
                                ? null
                                : new YearlyAndTermlyStatusDto
                                {
                                    NextTermBeginsDate = f.YearlyAndTermlyStatus.NextTermBeginsDate,
                                    TotalAttendance = f.YearlyAndTermlyStatus.TotalAttendance,
                                    TermStatus = f.YearlyAndTermlyStatus.TermStatus,
                                    Session = f.YearlyAndTermlyStatus.Session == null
                                        ? null
                                        : new AcademicSessionDto { Session = f.YearlyAndTermlyStatus.Session.Session }
                                },
                            ItemId = f.ItemId,
                            Score = f.Score
                        }).ToList(),
                        StudentsAccessment = mapped,
                        StudentsAttendance = StudentAssessmentMapping.MapAttendanceList(attendance),
                        SchoolHeadInfo = schoolHead,
                        CasSetting = casSetting,
                    },
            };
            await RelabelSingleReportAsync(singleReport, ct);
            return singleReport;
        }

        /// <summary>
        /// Attendance and comment rows for a report sheet. Uses the term being printed when the arm has
        /// rows for it, so a past term's sheet shows that term's days present and remarks. Otherwise
        /// falls back to the current term, which is all the legacy flow ever read.
        /// </summary>
        private async Task<List<StudentsAtteXzndanceAndCommentsReport>> LoadReportAttendanceAsync(
            string printedTermId,
            string printedArmId,
            System.Linq.Expressions.Expression<Func<StudentsAtteXzndanceAndCommentsReport, bool>> currentTermFallback,
            CancellationToken ct)
        {
            var repo = _uow.Repository<StudentsAtteXzndanceAndCommentsReport>();
            var forPrintedTerm = await repo.WhereAsync(
                a => a.YearlyAndTermlyStatusId == printedTermId && a.ClassArmId == printedArmId,
                ct);

            return forPrintedTerm.Count > 0 ? forPrintedTerm : await repo.WhereAsync(currentTermFallback, ct);
        }

        /// <summary>Applies a centre's own affective/psychomotor wording to a single report sheet.</summary>
        private Task RelabelSingleReportAsync(UsersSingleStudentResultProcessingsToReportSheetXtractionDto report, CancellationToken ct)
            => AffectiveItemLabels.ApplyByClassArmAsync(
                _uow,
                (report.GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting?.AffectiveAndPsychomotorAssessment
                    ?? Array.Empty<AffectiveAndPsychomotorAssessmentOutputXtractionForFormTeacherDto>())
                    .Select(a => ((string?)a.ClassArmId, (AffectiveAndPsychomotorDto?)a.AffectiveAndPsychomotor)),
                ct);

        public async Task<SchoolInfoDto> GetSchoolModelForStudentOrParent(CancellationToken ct = default)
        {
            var student = await ResolvePortalStudentAsync(ct);
            return _mapper.Map<SchoolInfoDto>(student?.SchoolInfo);
        }

        public async Task<bool> ChangeSystemImplementationType(string conCatParameters, CancellationToken ct = default)
        {
            var parts = conCatParameters.Split(',');
            if (parts.Length < 2) return false;
            var userOrSchoolId = parts[0];
            var newType = parts[1];

            var schoolId = (await RegistrySchoolLookup.ResolveSchoolRegistrationOfficerIdAsync(_uow, userOrSchoolId, ct))
                ?? userOrSchoolId.Trim();
            if (string.IsNullOrWhiteSpace(schoolId)) return false;

            var schoolRepo = _uow.Repository<SchoolTyInfo, string>();
            // Prefer PK lookup (tracked) to avoid AsNoTracking + Attach conflicts in the same request.
            var entity = await schoolRepo.FindAsync(schoolId, ct)
                ?? await schoolRepo.FindAsync(t => t.SchoolRegistrationOfficerId == schoolId, ct);
            if (entity == null) return false;

            // Match legacy SecurityService.UpdateSystemImplementationType.
            entity.SystemImplementationType = FreePlugIn.Encrypt(
                FreePlugIn.GetUniqueSystemId() + ":" + newType, true);
            schoolRepo.Update(entity);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        private async Task<string> SchoolIdForStudentOrParent(CancellationToken ct = default)
        {
            var student = await ResolvePortalStudentAsync(ct);
            return student?.SchoolInfoId;
        }

        /// <summary>
        /// Prefer the current student/parent school id when available so portal lookups use
        /// <c>TxStudentDetail.SchoolInfoId</c> (not a false-positive registry single-school match).
        /// Registry officers still resolve via school-registration profile.
        /// </summary>
        private async Task<string> ResolveSchoolIdForAssessmentLookup(CancellationToken ct = default)
        {
            var studentSchoolId = await SchoolIdForStudentOrParent(ct);
            if (!string.IsNullOrWhiteSpace(studentSchoolId))
            {
                return studentSchoolId.Trim();
            }

            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            return string.IsNullOrWhiteSpace(schoolId) ? string.Empty : schoolId.Trim();
        }

        public async Task<IList<SelectListItem>> GetSessionsDropdown(CancellationToken ct = default)
        {
            var schoolId = await SchoolIdForStudentOrParent(ct);
            var sessionRepo = _uow.Repository<TxAcademicSession, short>();
            var sessions = await sessionRepo.WhereAsync(r => r.SchoolInfoId == schoolId, ct);
            return sessions.Select(r => new SelectListItem { Text = r.Session, Value = r.Id.ToString() }).ToList();
        }

        /// <summary>
        /// Student/parent results: only sessions that have at least one assessment row
        /// for the resolved portal student (honours <c>X-Selected-Student-Id</c> siblings).
        /// </summary>
        public async Task<IList<SelectListItem>> GetSessionsDropdownWithResultsForPortalStudent(CancellationToken ct = default)
        {
            var student = await ResolvePortalStudentAsync(ct);
            if (student == null || string.IsNullOrWhiteSpace(student.UserId))
            {
                return new List<SelectListItem>();
            }

            var schoolId = await SchoolIdForStudentOrParent(ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return new List<SelectListItem>();
            }

            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var assessments = await assessRepo.WhereAsync(
                a => a.StudentId == student.UserId,
                ct,
                a => a.YearlyAndTermlyStatus!);

            if (assessments == null || assessments.Count == 0)
            {
                return new List<SelectListItem>();
            }

            var sessionIdsWithResults = assessments
                .Select(a => a.YearlyAndTermlyStatus?.SessionId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (sessionIdsWithResults.Count == 0)
            {
                return new List<SelectListItem>();
            }

            var sessionRepo = _uow.Repository<TxAcademicSession, short>();
            var sessions = await sessionRepo.WhereAsync(r => r.SchoolInfoId == schoolId, ct);
            return sessions
                .Where(r => sessionIdsWithResults.Contains(r.Id))
                .Select(r => new SelectListItem { Text = r.Session, Value = r.Id.ToString() })
                .ToList();
        }

        public async Task<StudentsAccessmentCasSettingDto> GetStudentsInAnArmByArmIdAndCaSettingForUnEnteredCasScores(string armId, string subjectId, CancellationToken ct = default)
        {
            var cat = await NumberOfCAsInSchool(ct);
            var repo = _uow.Repository<StudenXtAccessment>();

            Expression<Func<StudenXtAccessment, bool>> predicate = cat switch
            {
                CasCategory.OneCA => s => s.SubjectId == subjectId && s.Student.ClassArmId == armId &&
                                          (s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                                           s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing) &&
                                          Math.Abs(s.Ca1) < 0.2,
                CasCategory.TwoCAs => s => s.SubjectId == subjectId && s.Student.ClassArmId == armId &&
                                           (s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                                            s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing) &&
                                           (Math.Abs(s.Ca1) < 0.2 || Math.Abs(s.Ca2) < 0.2),
                _ => s => s.SubjectId == subjectId && s.Student.ClassArmId == armId &&
                          (s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                           s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing) &&
                          (Math.Abs(s.Ca1) < 0.2 || Math.Abs(s.Ca2) < 0.2 || Math.Abs(s.Ca3) < 0.2)
            };

            var rows = await repo.WhereAsync(predicate, ct, s => s.Student.User);
            if (rows == null || rows.Count == 0)
                return new StudentsAccessmentCasSettingDto();

            var yearlyRepo = _uow.Repository<TxYearlyAndTermlyStatus>();
            var schoolId = await GetSchoolIdForTeacher(ct);
            var status = await yearlyRepo.FindAsync(t =>
                (t.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                 t.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing) &&
                t.Session.SchoolInfoId == schoolId, ct);

            return new StudentsAccessmentCasSettingDto
            {
                StudentsAccessment = rows.Select(a => new StudentsAccessmentDto
                {
                    Id = a.Id,
                    SubjectId = a.SubjectId,
                    ClassArmId = a.ClassArmId,
                    YearlyAndTermlyStatusId = a.YearlyAndTermlyStatusId,
                    Ca1 = a.Ca1,
                    Ca2 = a.Ca2,
                    Ca3 = a.Ca3,
                    Exams = a.Exams,
                    Student = _mapper.Map<StudentDetailsDto>(a.Student)
                }).ToList(),
                CasSetting = new CasSettingDto
                {
                    CasCategory = status.CasCategory,
                    PerCasValue = status.PerCasValue
                }
            };
        }

        private async Task<CasCategory> NumberOfCAsInSchool(CancellationToken ct = default)
        {
            var yearlyRepo = _uow.Repository<TxYearlyAndTermlyStatus>();
            var schoolId = await GetSchoolIdForTeacher(ct);
            var entity = await yearlyRepo.FindAsync(t =>
                (t.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                 t.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing) &&
                t.Session.SchoolInfoId == schoolId, ct);
            return entity.CasCategory;
        }

        public async Task<StudentsAccessmentCasSettingDto> GetStudentsInAnArmByArmIdAndCaSettingForUnEnteredCasAndExamsScores(string armId, string subjectId, CancellationToken ct = default)
        {
            var cat = await NumberOfCAsInSchool(ct);
            var repo = _uow.Repository<StudenXtAccessment>();

            Expression<Func<StudenXtAccessment, bool>> predicate = cat switch
            {
                CasCategory.OneCA => s => s.SubjectId == subjectId && s.Student.ClassArmId == armId &&
                                          (s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                                           s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing) &&
                                          (Math.Abs(s.Ca1) < 0.2 || Math.Abs(s.Exams) < 0.2),
                CasCategory.TwoCAs => s => s.SubjectId == subjectId && s.Student.ClassArmId == armId &&
                                           (s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                                            s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing) &&
                                           (Math.Abs(s.Ca1) < 0.2 || Math.Abs(s.Ca2) < 0.2 || Math.Abs(s.Exams) < 0.2),
                _ => s => s.SubjectId == subjectId && s.Student.ClassArmId == armId &&
                          (s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                           s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing) &&
                          (Math.Abs(s.Ca1) < 0.2 || Math.Abs(s.Ca2) < 0.2 || Math.Abs(s.Ca3) < 0.2 || Math.Abs(s.Exams) < 0.2)
            };

            var rows = await repo.WhereAsync(predicate, ct, s => s.Student.User);
            if (rows == null || rows.Count == 0)
                return new StudentsAccessmentCasSettingDto();

            var yearlyRepo = _uow.Repository<TxYearlyAndTermlyStatus>();
            var schoolId = await GetSchoolIdForTeacher(ct);
            var status = await yearlyRepo.FindAsync(t =>
                (t.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                 t.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing) &&
                t.Session.SchoolInfoId == schoolId, ct);

            return new StudentsAccessmentCasSettingDto
            {
                StudentsAccessment = rows.Select(a => new StudentsAccessmentDto
                {
                    Id = a.Id,
                    SubjectId = a.SubjectId,
                    ClassArmId = a.ClassArmId,
                    YearlyAndTermlyStatusId = a.YearlyAndTermlyStatusId,
                    Ca1 = a.Ca1,
                    Ca2 = a.Ca2,
                    Ca3 = a.Ca3,
                    Exams = a.Exams,
                    Student = _mapper.Map<StudentDetailsDto>(a.Student)
                }).ToList(),
                CasSetting = new CasSettingDto
                {
                    CasCategory = status.CasCategory,
                    PerCasValue = status.PerCasValue
                }
            };
        }

        public async Task<StudentAssessmentAndStatusDto> GetUnenteredStudentRemarkByArmId(string armId, CancellationToken ct = default)
        {
            var studentRepo = _uow.Repository<TxStudentDetail>();
            var all = await studentRepo.WhereAsync(s => s.ClassArmId == armId && s.Status != StudentStatus.Previous, ct, c => c.ClassArm.Classs, u => u.User);

            var affectRepo = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var filled = (await affectRepo.WhereAsync(a =>
                    a.ClassArmId == armId &&
                    a.Student.Status != StudentStatus.Previous &&
                    a.Score > 0 &&
                    a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct, c => c.ClassArm.Classs, s => s.Student.User))
                .GroupBy(g => g.StudentId)
                .Select(g => g.First().StudentId)
                .ToList();

            all.RemoveAll(s => filled.Contains(s.UserId));

            var assignRepo = _uow.Repository<TeacherClassArmTxAssignment>();
            var schoolId = await GetSchoolIdForTeacher(ct);
            var assignment = await assignRepo.FindAsync(a => a.ArmId == armId && a.Employee.SchoolId == schoolId, ct);

            return new StudentAssessmentAndStatusDto
            {
                ArmTermlyAssessmentStatus = assignment.ArmTermlyAssessmentStatus,
                StudentDetails = _mapper.Map<List<StudentDetailsDto>>(all)
            };
        }

        public async Task<IEnumerable<TeacherSubjectArmAssignmentDto>> GetUncompletedCAsandExamsScoreEntering(CancellationToken ct = default)
        {
            var teacher = await CurrentTeacherModel(ct);
            if (string.IsNullOrWhiteSpace(teacher?.Id))
            {
                return Enumerable.Empty<TeacherSubjectArmAssignmentDto>();
            }

            var assignmentRepo = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var assignments = await assignmentRepo.WhereAsync(
                t => t.EmployeeId == teacher.Id,
                ct,
                i => i.Arm!.Classs!,
                s => s.Subject!);

            var casCategory = await NumberOfCAsInSchool(ct);
            var assessRepo = _uow.Repository<StudenXtAccessment>();
            List<string> subjectIds = casCategory switch
            {
                CasCategory.OneCA => (await assessRepo.WhereAsync(s =>
                        s.EmployeeId == teacher.Id &&
                        s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        (Math.Abs(s.Ca1) < 0.2 || Math.Abs(s.Exams) < 0.2), ct, d => d.ClassArm.Classs))
                    .GroupBy(c => c.SubjectId).Select(g => g.Key).ToList(),


                CasCategory.TwoCAs => (await assessRepo.WhereAsync(s =>
                        s.EmployeeId == teacher.Id &&
                        s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        (Math.Abs(s.Ca1) < 0.2 || Math.Abs(s.Ca2) < 0.2 || Math.Abs(s.Exams) < 0.2), ct, d => d.ClassArm.Classs))
                    .GroupBy(c => c.SubjectId).Select(g => g.Key).ToList(),
                _ => (await assessRepo.WhereAsync(s =>
                        s.EmployeeId == teacher.Id &&
                        s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        (Math.Abs(s.Ca1) < 0.2 || Math.Abs(s.Ca2) < 0.2 || Math.Abs(s.Ca3) < 0.2 || Math.Abs(s.Exams) < 0.2),
                    ct, d => d.ClassArm.Classs))
                    .GroupBy(c => c.SubjectId).Select(g => g.Key).ToList()
            };

            var result = assignments.Where(a => subjectIds.Contains(a.SubjectId))
                .Select(TeacherAssignmentMapping.MapSubjectArmAssignment)
                .ToList();

            return result;
        }

        public async Task<IEnumerable<TeacherSubjectArmAssignmentDto>> GetUncompletedCAsScoreEntering(CancellationToken ct = default)
        {
            var teacher = await CurrentTeacherModel(ct);
            if (string.IsNullOrWhiteSpace(teacher?.Id))
            {
                return Enumerable.Empty<TeacherSubjectArmAssignmentDto>();
            }

            var assignmentRepo = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var assignments = await assignmentRepo.WhereAsync(
                t => t.EmployeeId == teacher.Id,
                ct,
                i => i.Arm!.Classs!,
                s => s.Subject!);

            var casCategory = await NumberOfCAsInSchool(ct);
            var assessRepo = _uow.Repository<StudenXtAccessment>();
            List<string> subjectIds = casCategory switch
            {
                CasCategory.OneCA => (await assessRepo.WhereAsync(s =>
                        s.EmployeeId == teacher.Id &&
                        s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        Math.Abs(s.Ca1) < 0.2, ct, d => d.ClassArm.Classs))
                    .GroupBy(c => c.SubjectId).Select(g => g.Key).ToList(),
                CasCategory.TwoCAs => (await assessRepo.WhereAsync(s =>
                        s.EmployeeId == teacher.Id &&
                        s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        (Math.Abs(s.Ca1) < 0.2 || Math.Abs(s.Ca2) < 0.2), ct, d => d.ClassArm.Classs))
                    .GroupBy(c => c.SubjectId).Select(g => g.Key).ToList(),
                _ => (await assessRepo.WhereAsync(s =>
                        s.EmployeeId == teacher.Id &&
                        s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        (Math.Abs(s.Ca1) < 0.2 || Math.Abs(s.Ca2) < 0.2 || Math.Abs(s.Ca3) < 0.2), ct, d => d.ClassArm.Classs))
                    .GroupBy(c => c.SubjectId).Select(g => g.Key).ToList()
            };

            return assignments.Where(a => subjectIds.Contains(a.SubjectId))
                .Select(TeacherAssignmentMapping.MapSubjectArmAssignment)
                .ToList();
        }

        public async Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessClassResultByClassName(string sessionId, TermStatus termInEnum, CurrentClass classNameInEnum, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdForCurrentUserAsync(ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return null;
            }

            var classRepo = _uow.Repository<RxClasss>();
            var classEntity = await classRepo.FindAsync(x => x.ClassName == classNameInEnum && x.SchoolInfoId == schoolId, ct);
            if (classEntity == null) return null;

            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var currentStatus = await assessRepo.FindAsync(s =>
                s.ClassArm.ClassId == classEntity.Id &&
                s.YearlyAndTermlyStatus.SessionId == sessionId &&
                s.YearlyAndTermlyStatus.TermStatus == termInEnum, ct, d => d.YearlyAndTermlyStatus.Session);

            var yearlyStatus = currentStatus?.YearlyAndTermlyStatus;
            if (yearlyStatus == null) return null;

            List<StudenXtAccessment> students;
            if (yearlyStatus.TermStatus == TermStatus.Third)
            {
                students = await assessRepo.WhereAsync(s =>
                        s.ClassArm.ClassId == classEntity.Id &&
                        s.YearlyAndTermlyStatus.SessionId == sessionId &&
                        (yearlyStatus.TermlyAccessmentStatus != TermlyAccessmentStatus.Previous
                            ? s.Student.Status != StudentStatus.Previous
                            : true),
                    ct,
                    s => s.Student.User,
                    s => s.Subject,
                    s => s.YearlyAndTermlyStatus,
                    s => s.ClassArm.Classs);
            }
            else
            {
                students = await assessRepo.WhereAsync(s =>
                        s.ClassArm.ClassId == classEntity.Id &&
                        s.YearlyAndTermlyStatusId == yearlyStatus.Id &&
                        (yearlyStatus.TermlyAccessmentStatus != TermlyAccessmentStatus.Previous
                            ? s.Student.Status != StudentStatus.Previous
                            : true),
                    ct,
                    s => s.Student.User,
                    s => s.Subject,
                    s => s.YearlyAndTermlyStatus,
                    s => s.ClassArm.Classs);
            }

            var mapped = _mapper.Map<List<StudentsAccessmentDto>>(students);
            if (mapped == null || mapped.Count == 0)
                return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto { StatusCode = 1 };

            var affectRepo = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var affect = await affectRepo.WhereAsync(a =>
                    a.ClassArm.ClassId == classEntity.Id &&
                    a.YearlyAndTermlyStatusId == yearlyStatus.Id,
                ct,
                a => a.AffectiveAndPsychomotor,
                d => d.ClassArm.Classs,
                c => c.FormMaster.User);

            if (affect == null || !affect.Any())
            {
                return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
                {
                    StatusCode = 2,
                    StudentsAccessment = mapped,
                    CasSetting = new CasSettingDto
                    {
                        PerCasValue = mapped.Last().YearlyAndTermlyStatus.PerCasValue,
                        CasCategory = mapped.Last().YearlyAndTermlyStatus.CasCategory
                    }
                };
            }

            var schoolHead = await SchoolInfoForTeacherResultProcessing(
                classNameInEnum is CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3 or CurrentClass.Jss1 or CurrentClass.Jss2 or CurrentClass.Jss3
                    ? affect.First().YearlyAndTermlyStatus.PrincipalId
                    : affect.First().YearlyAndTermlyStatus.HeadmasterId, ct);

            var attendanceRepo = _uow.Repository<StudentsAtteXzndanceAndCommentsReport>();
            var attendance = await attendanceRepo.WhereAsync(s =>
                s.ClassArm.ClassId == classEntity.Id &&
                s.YearlyAndTermlyStatusId == yearlyStatus.Id, ct);

            return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
            {
                AffectiveAndPsychomotorAssessment = affect.Select(f => new AffectiveAndPsychomotorAssessmentOutputXtractionForFormTeacherDto
                {
                    ClassArmId = f.ClassArmId,
                    FormMaster = new ArmFormMasterDto
                    {
                        FormMastersName = f.FormMaster.User.FullName,
                        SignatureString = f.FormMaster.SignatureImageString,
                        AssignedArmId = f.ClassArmId
                    },
                    StudentId = f.StudentId,
                    YearlyAndTermlyAssessmentStatusId = f.YearlyAndTermlyStatusId,
                    AffectiveAndPsychomotor = _mapper.Map<AffectiveAndPsychomotorDto>(f.AffectiveAndPsychomotor),
                    ItemId = f.ItemId,
                    Score = f.Score
                }).ToList(),
                StudentsAccessment = mapped,
                StudentsAttendance = _mapper.Map<List<StudentsAttendanceDto>>(attendance),
                SchoolHeadInfo = schoolHead,
                CasSetting = new CasSettingDto
                {
                    CasCategory = mapped.Last().YearlyAndTermlyStatus.CasCategory,
                    PerCasValue = mapped.Last().YearlyAndTermlyStatus.PerCasValue
                }
            };
        }

        // Legacy TeachersAndOthersService.ProcessClassArmResultByClassNameAndArmName(clas, arm)
        public async Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessClassArmResultByClassNameAndArmName(CurrentClass clas, ArmName arm, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdForCurrentUserAsync(ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return null!;
            }

            var studentAccessmentRepository = _uow.Repository<StudenXtAccessment>();

            // Legacy: only TermlyAccessmentStatus.Current (not ResultProccessing) for this entry point.
            var yearlyAndTermlyStatus = (await studentAccessmentRepository.FindAsync(
                s =>
                    s.Student.ClassArm.Classs.ClassName == clas &&
                    s.Student.ClassArm.ArmName == arm &&
                    s.Student.SchoolInfoId == schoolId &&
                    s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                d => d.YearlyAndTermlyStatus.Session))?.YearlyAndTermlyStatus;

            if (yearlyAndTermlyStatus == null)
            {
                return null!;
            }

            List<StudenXtAccessment> students;
            if (yearlyAndTermlyStatus.TermStatus == TermStatus.Third)
            {
                students = await studentAccessmentRepository.WhereAsync(
                    s =>
                        s.Student.ClassArm.Classs.ClassName == clas &&
                        s.Student.ClassArm.ArmName == arm &&
                        s.YearlyAndTermlyStatus.SessionId == yearlyAndTermlyStatus.SessionId &&
                        s.Student.Status != StudentStatus.Previous &&
                        s.Subject.TimeStatus == TimeStatus.Current,
                    ct,
                    s => s.Student.User!,
                    s => s.Employee.User!,
                    s => s.Subject!,
                    d => d.YearlyAndTermlyStatus!,
                    x => x.ClassArm.Classs!);
            }
            else
            {
                students = await studentAccessmentRepository.WhereAsync(
                    s =>
                        s.Student.ClassArm.Classs.ClassName == clas &&
                        s.Student.ClassArm.ArmName == arm &&
                        s.YearlyAndTermlyStatusId == yearlyAndTermlyStatus.Id,
                    ct,
                    s => s.Student.User!,
                    s => s.Employee.User!,
                    s => s.Subject!,
                    d => d.YearlyAndTermlyStatus!,
                    x => x.ClassArm.Classs!);
            }

            var mapResult = _mapper.Map<List<StudentsAccessmentDto>>(students);
            if (mapResult == null || !mapResult.Any())
            {
                return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto { StatusCode = 1 };
            }

            var affectiveAndPsychomotorAssessmentRepository = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var currentAffectiveAndPsychomotiveAssessment = await affectiveAndPsychomotorAssessmentRepository.WhereAsync(
                a =>
                    a.Student.ClassArm.Classs.ClassName == clas &&
                    a.Student.ClassArm.ArmName == arm &&
                    a.FormMaster.SchoolId == schoolId &&
                    (a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                     a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing),
                ct,
                a => a.AffectiveAndPsychomotor!,
                d => d.ClassArm.Classs!,
                c => c.FormMaster.User!,
                v => v.Student.User!,
                d => d.YearlyAndTermlyStatus.Session!,
                b => b.Student.ClassArm.Classs!);

            if (currentAffectiveAndPsychomotiveAssessment == null || !currentAffectiveAndPsychomotiveAssessment.Any())
            {
                return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
                {
                    StatusCode = 2,
                    StudentsAccessment = mapResult,
                    CasSetting = new CasSettingDto
                    {
                        PerCasValue = mapResult.Last().YearlyAndTermlyStatus.PerCasValue,
                        CasCategory = mapResult.Last().YearlyAndTermlyStatus.CasCategory
                    }
                };
            }

            SchoolHeadDto? schoolHead = null;
            if (clas is CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3 or
                CurrentClass.Jss1 or CurrentClass.Jss2 or CurrentClass.Jss3)
            {
                var principalId = currentAffectiveAndPsychomotiveAssessment.First().YearlyAndTermlyStatus.PrincipalId;
                if (principalId != null)
                {
                    schoolHead = await SchoolInfoForTeacherResultProcessing(principalId, ct);
                }
            }
            else
            {
                var headMasterId = currentAffectiveAndPsychomotiveAssessment.First().YearlyAndTermlyStatus.HeadmasterId;
                if (headMasterId != null)
                {
                    schoolHead = await SchoolInfoForTeacherResultProcessing(headMasterId, ct);
                }
            }

            var studentsAttendanceReportRepository = _uow.Repository<StudentsAtteXzndanceAndCommentsReport>();
            var currentAttendance = await studentsAttendanceReportRepository.WhereAsync(
                a => a.Student.ClassArm.Classs.ClassName == clas &&
                     a.Student.ClassArm.ArmName == arm &&
                     a.Student.SchoolInfoId == schoolId &&
                     (a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                      a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing),
                ct);

            return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
            {
                AffectiveAndPsychomotorAssessment = currentAffectiveAndPsychomotiveAssessment.Select(
                    f => new AffectiveAndPsychomotorAssessmentOutputXtractionForFormTeacherDto
                    {
                        ClassArmId = f.ClassArmId,
                        FormMaster = new ArmFormMasterDto
                        {
                            FormMastersName = f.FormMaster.User.FullName,
                            SignatureString = f.FormMaster.SignatureImageString,
                            AssignedArmId = f.ClassArmId
                        },
                        Student = new StudentFielExtractionDto
                        {
                            FullName = f.Student.User.FullName,
                            Id = f.StudentId,
                            CurrentClass = f.ClassArm.Classs.ClassName,
                            ArmName = f.ClassArm.ArmName,
                            CurrentClassLabel = f.Student.ClassArm.Classs.ClassLabel
                        },
                        AffectiveAndPsychomotor = _mapper.Map<AffectiveAndPsychomotorDto>(f.AffectiveAndPsychomotor),
                        YearlyAndTermlyAssessmentStatus = new YearlyAndTermlyStatusDto
                        {
                            NextTermBeginsDate = f.YearlyAndTermlyStatus.NextTermBeginsDate,
                            TotalAttendance = f.YearlyAndTermlyStatus.TotalAttendance,
                            TermStatus = f.YearlyAndTermlyStatus.TermStatus,
                            Session = new AcademicSessionDto { Session = f.YearlyAndTermlyStatus.Session.Session }
                        },
                        ItemId = f.ItemId,
                        Score = f.Score
                    }).ToList(),
                StudentsAccessment = mapResult,
                StudentsAttendance = _mapper.Map<List<StudentsAttendanceDto>>(currentAttendance),
                SchoolHeadInfo = schoolHead,
                CasSetting = new CasSettingDto
                {
                    CasCategory = mapResult.Last().YearlyAndTermlyStatus.CasCategory,
                    PerCasValue = mapResult.Last().YearlyAndTermlyStatus.PerCasValue
                }
            };
        }

        public async Task<GeneralResultProcessingsForAllStudentsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessAllResults(CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdForCurrentUserAsync(ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return null;
            }

            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var baseStatus = await assessRepo.FindAsync(s =>
                    s.Student.SchoolInfoId == schoolId &&
                    (s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                     s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing),
                ct, d => d.YearlyAndTermlyStatus.Session);

            var yearly = baseStatus?.YearlyAndTermlyStatus;
            if (yearly == null) return null;

            List<StudenXtAccessment> students = yearly.TermStatus == TermStatus.Third
                ? await assessRepo.WhereAsync(s =>
                        s.YearlyAndTermlyStatus.SessionId == yearly.SessionId &&
                        s.Student.Status != StudentStatus.Previous &&
                        s.Subject.TimeStatus == TimeStatus.Current,
                    ct,
                    s => s.Student.User,
                    s => s.Employee.User,
                    s => s.Subject,
                    s => s.YearlyAndTermlyStatus,
                    s => s.ClassArm.Classs)
                : await assessRepo.WhereAsync(s =>
                        s.YearlyAndTermlyStatusId == yearly.Id,
                    ct,
                    s => s.Student.User,
                    s => s.Employee.User,
                    s => s.Subject,
                    s => s.YearlyAndTermlyStatus,
                    s => s.ClassArm.Classs);

            var mapped = _mapper.Map<List<StudentsAccessmentDto>>(students);
            if (mapped == null || mapped.Count == 0)
                return new GeneralResultProcessingsForAllStudentsToReportSheetXtractionForFormTeacherCasSettingDto { StatusCode = 1 };

            var affectRepo = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var affect = await affectRepo.WhereAsync(a =>
                    a.FormMaster.SchoolId == schoolId &&
                    a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                a => a.AffectiveAndPsychomotor,
                d => d.ClassArm.Classs,
                c => c.FormMaster.User,
                v => v.Student.User,
                d => d.YearlyAndTermlyStatus.Session,
                b => b.Student.ClassArm.Classs);

            if (affect == null || !affect.Any())
            {
                return new GeneralResultProcessingsForAllStudentsToReportSheetXtractionForFormTeacherCasSettingDto
                {
                    StatusCode = 2,
                    StudentsAccessment = mapped,
                    CasSetting = new CasSettingDto
                    {
                        PerCasValue = mapped.Last().YearlyAndTermlyStatus.PerCasValue,
                        CasCategory = mapped.Last().YearlyAndTermlyStatus.CasCategory
                    }
                };
            }

            var schoolHeadList = new List<SchoolHeadDto>();
            var first = affect.First();
            var principalHead = await SchoolInfoForTeacherResultProcessing(first.YearlyAndTermlyStatus.PrincipalId, ct);
            if (principalHead != null) schoolHeadList.Add(principalHead);
            var headTeacher = await SchoolInfoForTeacherResultProcessing(first.YearlyAndTermlyStatus.HeadmasterId, ct);
            if (headTeacher != null) schoolHeadList.Add(headTeacher);

            var attendanceRepo = _uow.Repository<StudentsAtteXzndanceAndCommentsReport>();
            var attendance = await attendanceRepo.WhereAsync(a =>
                    a.Student.SchoolInfoId == schoolId &&
                    (a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                     a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing),
                ct);

            return new GeneralResultProcessingsForAllStudentsToReportSheetXtractionForFormTeacherCasSettingDto
            {
                AffectiveAndPsychomotorAssessment = affect.Select(f => new AffectiveAndPsychomotorAssessmentOutputXtractionForFormTeacherDto
                {
                    ClassArmId = f.ClassArmId,
                    FormMaster = new ArmFormMasterDto
                    {
                        FormMastersName = f.FormMaster.User.FullName,
                        SignatureString = f.FormMaster.SignatureImageString,
                        AssignedArmId = f.ClassArmId
                    },
                    Student = new StudentFielExtractionDto
                    {
                        FullName = f.Student.User.FullName,
                        Id = f.StudentId,
                        CurrentClass = f.ClassArm.Classs.ClassName,
                        ArmName = f.ClassArm.ArmName,
                        CurrentClassLabel = f.Student.ClassArm.Classs.ClassLabel
                    },
                    AffectiveAndPsychomotor = _mapper.Map<AffectiveAndPsychomotorDto>(f.AffectiveAndPsychomotor),
                    YearlyAndTermlyAssessmentStatus = new YearlyAndTermlyStatusDto
                    {
                        NextTermBeginsDate = f.YearlyAndTermlyStatus.NextTermBeginsDate,
                        TotalAttendance = f.YearlyAndTermlyStatus.TotalAttendance,
                        TermStatus = f.YearlyAndTermlyStatus.TermStatus,
                        Session = new AcademicSessionDto { Session = f.YearlyAndTermlyStatus.Session.Session }
                    },
                    ItemId = f.ItemId,
                    Score = f.Score
                }).ToList(),
                StudentsAccessment = mapped,
                StudentsAttendance = _mapper.Map<List<StudentsAttendanceDto>>(attendance),
                SchoolHeadInfo = schoolHeadList,
                CasSetting = new CasSettingDto
                {
                    CasCategory = mapped.Last().YearlyAndTermlyStatus.CasCategory,
                    PerCasValue = mapped.Last().YearlyAndTermlyStatus.PerCasValue
                }
            };
        }

        //public async Task<List<StudentInfoForSmsDto>> GetStudentByAdmsnNoForSms(string admsnNo, CancellationToken ct = default)
        //{
        //    var students = await GetRegistrationServise().GetStudentsByParameterAsync(admsnNo, ct);
        //    return students.Select(r => new StudentInfoForSmsDto
        //    {
        //        Id = r.Id,
        //        AdmsnNo = r.IdNo,
        //        FullName = r.User.FullName,
        //        GuardianName = r.GuardiansName,
        //        TelNo = r.HomeNumber
        //    }).ToList();
        //}

        //public async Task<List<StudentInfoForSmsDto>> GetAllStudentsForSms(CancellationToken ct = default)
        //{
        //    var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
        //    var students = await GetRegistrationServise().GetAllStudentsAsync(schoolId, ct);
        //    return students.Select(v => new StudentInfoForSmsDto
        //    {
        //        Id = v.Id,
        //        Classs = v.ClassArm.Classs.ClassName,
        //        ClassLabel = v.ClassArm.Classs.ClassLabel,
        //        Arm = v.ClassArm.ArmName.ToString(),
        //        AdmsnNo = v.IdNo,
        //        FullName = v.User.FullName,
        //        GuardianName = v.GuardiansName,
        //        TelNo = v.MobileNumber
        //    }).ToList();
        //}

        public Task<NetworkType> GetNetworkType(string telNo, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(telNo))
                return Task.FromResult(NetworkType.InvalidNo);

            var trim = telNo.Trim();
            if (trim.StartsWith("+234"))
                trim = "0" + trim[4..];
            else if (trim.StartsWith("234"))
                trim = "0" + trim[3..];
            else if (trim.StartsWith("00"))
                trim = "0" + trim[2..];

            NetworkType result =
                (trim.StartsWith("0703") || trim.StartsWith("0706") || trim.StartsWith("0803") || trim.StartsWith("0806") ||
                 trim.StartsWith("0810") || trim.StartsWith("0813") || trim.StartsWith("0814") || trim.StartsWith("0816") ||
                 trim.StartsWith("0903"))
                    ? NetworkType.Mtn
                    : (trim.StartsWith("0705") || trim.StartsWith("0805") || trim.StartsWith("0807") || trim.StartsWith("0811") ||
                       trim.StartsWith("0815") || trim.StartsWith("0905"))
                        ? NetworkType.Glo
                        : (trim.StartsWith("0701") || trim.StartsWith("0708") || trim.StartsWith("0802") || trim.StartsWith("0808") ||
                           trim.StartsWith("0812") || trim.StartsWith("0902") || trim.StartsWith("0907"))
                            ? NetworkType.Airtel
                            : (trim.StartsWith("0809") || trim.StartsWith("0817") || trim.StartsWith("0818") || trim.StartsWith("0909"))
                                ? NetworkType.Etisalat
                                : NetworkType.Others;

            return Task.FromResult(result);
        }








        // SMS and CBT methods converted to async, preserving original names

        public async Task<string> GetSmsTokenAndKeyOnly(CancellationToken ct = default)
        {
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var repo = _uow.Repository<SchoolTyInfo, string>();
            var school = await repo.FindAsync(c => c.SchoolRegistrationOfficerId == schoolId, ct, v => v.SchoolRegistrationOfficer.User);
            if (school == null) return string.Empty;

            var smsApiKey = FreePlugIn.GetUniqueSystemId();
            var smsApiKeyEncode = FreePlugIn.Encrypt(smsApiKey, true);

            if (string.IsNullOrEmpty(school.SmsApiToken))
            {
                var newToken = Guid.NewGuid().ToString();
                school.SmsApiToken = newToken;
                school.SmsApiKey = FreePlugIn.Encrypt(Guid.NewGuid().ToString(), true);
                repo.Update(school);
                await _uow.SaveChangesAsync(ct);
                return $"{newToken}:{smsApiKeyEncode}";
            }

            return $"{school.SmsApiToken}:{smsApiKeyEncode}";
        }

        public async Task<string> GetSmsTokenAndKey(CancellationToken ct = default)
        {
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var repo = _uow.Repository<SchoolTyInfo, string>();
            var school = await repo.FindAsync(c => c.SchoolRegistrationOfficerId == schoolId, ct, v => v.SchoolRegistrationOfficer.User);
            
            if (school == null) return string.Empty;

            var smsApiToken = FreePlugIn.Encrypt(Guid.NewGuid().ToString(), true);
            var smsApiKey = FreePlugIn.GetUniqueSystemId();
            var smsApiKeyEncode = FreePlugIn.Encrypt(smsApiKey, true);

            if (string.IsNullOrEmpty(school.SmsApiToken))
            {
                school.SmsApiToken = smsApiToken;
                school.SmsApiKey = FreePlugIn.Encrypt(Guid.NewGuid().ToString(), true);
                repo.Update(school);
                await _uow.SaveChangesAsync(ct);
                return $"{smsApiToken}:{smsApiKeyEncode}";
            }

            return $"{school.SmsApiToken}:{smsApiKeyEncode}";
        }

        public Task<string> GetAndEncodeCredentials(string userId, CancellationToken ct = default) =>
            _securityExtensionService.GetAndEncodeCredentials(userId, ct);

        public Task<SchoolInfoDto> GetSystemInfoForSms(string userId, CancellationToken ct = default) =>
            _securityExtensionService.GetSystemInfoForSms(userId, ct);

        public async Task<string> GetCurrentSmsTokenAndKeyAndNewTokenAndKey(CancellationToken ct = default)
        {
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var repo = _uow.Repository<SchoolTyInfo, string>();
            var school = await repo.FindAsync(c => c.SchoolRegistrationOfficerId == schoolId, ct, v => v.SchoolRegistrationOfficer.User);
            if (school == null || string.IsNullOrWhiteSpace(school.SmsApiToken)) return string.Empty;

            var systemId = FreePlugIn.GetUniqueSystemId();
            var newToken = FreePlugIn.Encrypt(Guid.NewGuid().ToString(), true);
            var returnString = $"{school.SmsApiToken}:{FreePlugIn.Encrypt(systemId, true)}:{newToken}";

            school.SmsApiToken = newToken;
            school.SmsApiKey = FreePlugIn.Encrypt(systemId, true);
            repo.Update(school);

            return returnString;
        }

        public async Task<string> GetAndEncodeCredentialAndSmsBalanceUpdateParameters(float newBalace, float noOfSuccessfullySentSms, float pricePerSms, CancellationToken ct = default)
        {
            var tokenAndKey = await GetSmsTokenAndKey(ct);
            return FreePlugIn.Encrypt($"{tokenAndKey}:{newBalace}:{noOfSuccessfullySentSms}:{pricePerSms}", true);
        }

        public async Task<string> GetAndEncodeCredentialsAndNoOfSmsTimesPages(int noOfSmsRecipient, int noOfSmsPages, CancellationToken ct = default)
        {
            var credential = await GetSmsTokenAndKey(ct);
            return FreePlugIn.Encrypt($"{credential}:{noOfSmsRecipient}:{noOfSmsPages}", true);
        }

        private static string DateTimeToJson(DateTime date) =>
            LegacyJsonDate.ToJson(date);

        private static DateTime DateTimeFromJson(string dateString) =>
            LegacyJsonDate.FromJson(dateString);

        public Task<string> GetAndEncodeCredentialsAndExpiryDate(string userId, CancellationToken ct = default) =>
            _securityExtensionService.GetAndEncodeCredentialsAndExpiryDate(userId, ct);

        public async Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> GetClassThirdTermResult(string sessionId, TermStatus termInEnum, CurrentClass classNameInEnum, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdForCurrentUserAsync(ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return null;
            }

            var classRepo = _uow.Repository<RxClasss>();
            var classEntity = await classRepo.FindAsync(x => x.ClassName == classNameInEnum && x.SchoolInfoId == schoolId, ct);
            if (classEntity == null) return null;

            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var yearlyStatus = (await assessRepo.FindAsync(s =>
                    s.ClassArm.ClassId == classEntity.Id &&
                    s.Student.SchoolInfoId == schoolId &&
                    s.YearlyAndTermlyStatus.SessionId == sessionId &&
                    s.YearlyAndTermlyStatus.TermStatus == termInEnum,
                ct, d => d.YearlyAndTermlyStatus.Session))?.YearlyAndTermlyStatus;

            if (yearlyStatus == null)
                return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto { StatusCode = 1 };

            var students = await assessRepo.WhereAsync(s =>
                    s.ClassArm.ClassId == classEntity.Id &&
                    s.YearlyAndTermlyStatusId == yearlyStatus.Id,
                ct, s => s.Student.User, s => s.Employee.User, s => s.Subject, d => d.YearlyAndTermlyStatus, x => x.ClassArm.Classs);

            var mapResult = _mapper.Map<List<StudentsAccessmentDto>>(students);
            if (mapResult == null || !mapResult.Any())
                return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto { StatusCode = 1 };

            var affectRepo = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var affect = await affectRepo.WhereAsync(a =>
                    a.ClassArm.ClassId == classEntity.Id &&
                    a.YearlyAndTermlyStatusId == yearlyStatus.Id,
                ct, a => a.AffectiveAndPsychomotor, d => d.ClassArm.Classs, c => c.FormMaster.User, v => v.Student.User, d => d.YearlyAndTermlyStatus.Session, b => b.Student.ClassArm.Classs);

            if (affect == null || !affect.Any())
            {
                return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
                {
                    StatusCode = 2,
                    StudentsAccessment = mapResult,
                    CasSetting = new CasSettingDto
                    {
                        PerCasValue = mapResult.Last().YearlyAndTermlyStatus.PerCasValue,
                        CasCategory = mapResult.Last().YearlyAndTermlyStatus.CasCategory
                    }
                };
            }

            var schoolHead = await SchoolInfoForTeacherResultProcessing(
                classNameInEnum is CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3 or CurrentClass.Jss1 or CurrentClass.Jss2 or CurrentClass.Jss3
                    ? affect.First().YearlyAndTermlyStatus.PrincipalId
                    : affect.First().YearlyAndTermlyStatus.HeadmasterId, ct);

            var attendanceRepo = _uow.Repository<StudentsAtteXzndanceAndCommentsReport>();
            var attendance = await attendanceRepo.WhereAsync(a =>
                    a.ClassArm.ClassId == classEntity.Id &&
                    a.YearlyAndTermlyStatusId == yearlyStatus.Id,
                ct);

            return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
            {
                AffectiveAndPsychomotorAssessment = affect.Select(f => new AffectiveAndPsychomotorAssessmentOutputXtractionForFormTeacherDto
                {
                    ClassArmId = f.ClassArmId,
                    FormMaster = new ArmFormMasterDto
                    {
                        FormMastersName = f.FormMaster.User.FullName,
                        SignatureString = f.FormMaster.SignatureImageString,
                        AssignedArmId = f.ClassArmId
                    },
                    Student = new StudentFielExtractionDto
                    {
                        FullName = f.Student.User.FullName,
                        Id = f.StudentId,
                        CurrentClass = f.ClassArm.Classs.ClassName,
                        ArmName = f.ClassArm.ArmName,
                        CurrentClassLabel = f.Student.ClassArm.Classs.ClassLabel
                    },
                    AffectiveAndPsychomotor = _mapper.Map<AffectiveAndPsychomotorDto>(f.AffectiveAndPsychomotor),
                    YearlyAndTermlyAssessmentStatus = new YearlyAndTermlyStatusDto
                    {
                        NextTermBeginsDate = f.YearlyAndTermlyStatus.NextTermBeginsDate,
                        TotalAttendance = f.YearlyAndTermlyStatus.TotalAttendance,
                        TermStatus = f.YearlyAndTermlyStatus.TermStatus,
                        Session = new AcademicSessionDto { Session = f.YearlyAndTermlyStatus.Session.Session }
                    },
                    ItemId = f.ItemId,
                    Score = f.Score
                }).ToList(),
                StudentsAccessment = mapResult,
                StudentsAttendance = _mapper.Map<List<StudentsAttendanceDto>>(attendance),
                SchoolHeadInfo = schoolHead,
                CasSetting = new CasSettingDto
                {
                    CasCategory = mapResult.Last().YearlyAndTermlyStatus.CasCategory,
                    PerCasValue = mapResult.Last().YearlyAndTermlyStatus.PerCasValue
                }
            };
        }

        public async Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessThirdTermClassArmResultByClassNameAndArmName(CurrentClass clas, ArmName arm, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdForCurrentUserAsync(ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return null;
            }

            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var yearlyStatus = (await assessRepo.FindAsync(s =>
                    s.Student.ClassArm.Classs.ClassName == clas &&
                    s.Student.ClassArm.ArmName == arm &&
                    s.Student.SchoolInfoId == schoolId &&
                    s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct, d => d.YearlyAndTermlyStatus.Session))?.YearlyAndTermlyStatus;
            if (yearlyStatus == null) return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto { StatusCode = 1 };

            var students = await assessRepo.WhereAsync(s =>
                    s.Student.ClassArm.Classs.ClassName == clas &&
                    s.Student.ClassArm.ArmName == arm &&
                    s.YearlyAndTermlyStatusId == yearlyStatus.Id,
                ct, s => s.Student.User, s => s.Employee.User, s => s.Subject, d => d.YearlyAndTermlyStatus, x => x.ClassArm.Classs);

            var mapResult = _mapper.Map<List<StudentsAccessmentDto>>(students);
            if (mapResult == null || !mapResult.Any())
                return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto { StatusCode = 1 };

            var affectRepo = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var affect = await affectRepo.WhereAsync(a =>
                    a.Student.ClassArm.Classs.ClassName == clas &&
                    a.Student.ClassArm.ArmName == arm &&
                    a.FormMaster.SchoolId == schoolId &&
                    (a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                     a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing),
                ct, a => a.AffectiveAndPsychomotor, d => d.ClassArm.Classs, c => c.FormMaster.User, v => v.Student.User, d => d.YearlyAndTermlyStatus.Session, b => b.Student.ClassArm.Classs);

            if (affect == null || !affect.Any())
            {
                return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
                {
                    StatusCode = 2,
                    StudentsAccessment = mapResult,
                    CasSetting = new CasSettingDto
                    {
                        PerCasValue = mapResult.Last().YearlyAndTermlyStatus.PerCasValue,
                        CasCategory = mapResult.Last().YearlyAndTermlyStatus.CasCategory
                    }
                };
            }

            var schoolHead = await SchoolInfoForTeacherResultProcessing(
                clas is CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3 or CurrentClass.Jss1 or CurrentClass.Jss2 or CurrentClass.Jss3
                    ? affect.First().YearlyAndTermlyStatus.PrincipalId
                    : affect.First().YearlyAndTermlyStatus.HeadmasterId, ct);

            var attendanceRepo = _uow.Repository<StudentsAtteXzndanceAndCommentsReport>();
            var attendance = await attendanceRepo.WhereAsync(a =>
                    a.Student.ClassArm.Classs.ClassName == clas &&
                    a.Student.ClassArm.ArmName == arm &&
                    a.Student.SchoolInfoId == schoolId &&
                    (a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                     a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing),
                ct);

            return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
            {
                AffectiveAndPsychomotorAssessment = affect.Select(f => new AffectiveAndPsychomotorAssessmentOutputXtractionForFormTeacherDto
                {
                    ClassArmId = f.ClassArmId,
                    FormMaster = new ArmFormMasterDto
                    {
                        FormMastersName = f.FormMaster.User.FullName,
                        SignatureString = f.FormMaster.SignatureImageString,
                        AssignedArmId = f.ClassArmId
                    },
                    Student = new StudentFielExtractionDto
                    {
                        FullName = f.Student.User.FullName,
                        Id = f.StudentId,
                        CurrentClass = f.ClassArm.Classs.ClassName,
                        ArmName = f.ClassArm.ArmName,
                        CurrentClassLabel = f.Student.ClassArm.Classs.ClassLabel
                    },
                    AffectiveAndPsychomotor = _mapper.Map<AffectiveAndPsychomotorDto>(f.AffectiveAndPsychomotor),
                    YearlyAndTermlyAssessmentStatus = new YearlyAndTermlyStatusDto
                    {
                        NextTermBeginsDate = f.YearlyAndTermlyStatus.NextTermBeginsDate,
                        TotalAttendance = f.YearlyAndTermlyStatus.TotalAttendance,
                        TermStatus = f.YearlyAndTermlyStatus.TermStatus,
                        Session = new AcademicSessionDto { Session = f.YearlyAndTermlyStatus.Session.Session }
                    },
                    ItemId = f.ItemId,
                    Score = f.Score
                }).ToList(),
                StudentsAccessment = mapResult,
                StudentsAttendance = _mapper.Map<List<StudentsAttendanceDto>>(attendance),
                SchoolHeadInfo = schoolHead,
                CasSetting = new CasSettingDto
                {
                    CasCategory = mapResult.Last().YearlyAndTermlyStatus.CasCategory,
                    PerCasValue = mapResult.Last().YearlyAndTermlyStatus.PerCasValue
                }
            };
        }

        public Task<object> ProcessClassArmPaymentByClassNameAndArmName(CurrentClass clas, ArmName arm, CancellationToken ct = default)
        {
            return Task.FromException<object>(new NotImplementedException());
        }

        public async Task CreateQuestionsAndAnswer(CbtQuestionAndAnswerDto model, CancellationToken ct = default)
        {
            var repo = _uow.Repository<CbtQuestionAndAnswer>();
            var question = model.Question.Trim();
            var exists = await repo.FindAsync(a => a.Question.Trim().Equals(question, StringComparison.OrdinalIgnoreCase), ct);
            if (exists == null)
            {
                var mapModel = _mapper.Map<CbtQuestionAndAnswer>(model);
                repo.Add(mapModel);
                await _uow.SaveChangesAsync(ct);
            }
        }

        public async Task<List<StudentsAccessmentDto>> ProcessClassBroadSheet(string sessionId, TermStatus termInEnum, CurrentClass classNameInEnum, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdForCurrentUserAsync(ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return new List<StudentsAccessmentDto>();
            }

            var classRepo = _uow.Repository<RxClasss>();
            var classEntity = await classRepo.FindAsync(x => x.ClassName == classNameInEnum && x.SchoolInfoId == schoolId, ct);
            if (classEntity == null) return new List<StudentsAccessmentDto>();

            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var yearlyStatus = (await assessRepo.FindAsync(s =>
                    s.Student.SchoolInfoId == schoolId &&
                    s.ClassArm.ClassId == classEntity.Id &&
                    s.YearlyAndTermlyStatus.SessionId == sessionId &&
                    s.YearlyAndTermlyStatus.TermStatus == termInEnum, ct, d => d.YearlyAndTermlyStatus.Session))?.YearlyAndTermlyStatus;

            if (yearlyStatus == null) return new List<StudentsAccessmentDto>();

            var students = await assessRepo.WhereAsync(s =>
                    s.ClassArm.ClassId == classEntity.Id &&
                    s.YearlyAndTermlyStatus.SessionId == sessionId &&
                    s.YearlyAndTermlyStatus.TermStatus == termInEnum,
                ct, s => s.Student.User, s => s.Student.SchoolInfo, s => s.Employee.User, s => s.Subject, d => d.YearlyAndTermlyStatus, x => x.ClassArm.Classs);

            return _mapper.Map<List<StudentsAccessmentDto>>(students);
        }

        public async Task<List<StudentsAccessmentDto>> ProcessArmBroadSheet(string sessionId, TermStatus termInEnum, CurrentClass nameInEnum, ArmName armInEnum, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdForCurrentUserAsync(ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return new List<StudentsAccessmentDto>();
            }

            var armRepo = _uow.Repository<AxClassArm>();
            var arm = await armRepo.FindAsync(x => x.Classs.ClassName == nameInEnum && x.ArmName == armInEnum && x.Classs.SchoolInfoId == schoolId, ct, v => v.Classs);
            if (arm == null) return new List<StudentsAccessmentDto>();

            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var yearlyStatus = (await assessRepo.FindAsync(s =>
                    s.ClassArmId == arm.Id &&
                    s.YearlyAndTermlyStatus.SessionId == sessionId &&
                    s.YearlyAndTermlyStatus.TermStatus == termInEnum, ct, d => d.YearlyAndTermlyStatus.Session))?.YearlyAndTermlyStatus;

            if (yearlyStatus == null) return new List<StudentsAccessmentDto>();

            var students = await assessRepo.WhereAsync(s =>
                    s.ClassArmId == arm.Id &&
                    s.YearlyAndTermlyStatus.SessionId == sessionId &&
                    s.YearlyAndTermlyStatus.TermStatus == termInEnum,
                ct, s => s.Student.User, s => s.Student.SchoolInfo, s => s.Employee.User, s => s.Subject, d => d.YearlyAndTermlyStatus, x => x.ClassArm.Classs);

            return _mapper.Map<List<StudentsAccessmentDto>>(students);
        }

        public async Task<string> GetSmsTokenAndKeyAndSystemIdAndUserNameAndSchoolName(CancellationToken ct = default)
        {
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var repo = _uow.Repository<SchoolTyInfo, string>();
            var school = await repo.FindAsync(c => c.SchoolRegistrationOfficerId == schoolId, ct, v => v.SchoolRegistrationOfficer.User);
            if (school == null) return string.Empty;

            var smsApiToken = FreePlugIn.Encrypt(Guid.NewGuid().ToString(), true);
            var smsApiKey = FreePlugIn.GetUniqueSystemId();

            if (string.IsNullOrEmpty(school.SmsApiToken))
            {
                school.SmsApiToken = smsApiToken;
                school.SmsApiKey = FreePlugIn.Encrypt(Guid.NewGuid().ToString(), true);
                repo.Update(school);
                await _uow.SaveChangesAsync(ct);
            }

            string decryptedToken;
            try
            {
                decryptedToken = FreePlugIn.Decrypt(school.SmsApiToken, true);
            }
            catch
            {
                school.SmsApiToken = smsApiToken;
                school.SmsApiKey = FreePlugIn.Encrypt(Guid.NewGuid().ToString(), true);
                repo.Update(school);
                await _uow.SaveChangesAsync(ct);
                decryptedToken = FreePlugIn.Decrypt(school.SmsApiToken, true);
            }

            var userName = school.SchoolRegistrationOfficer?.User?.UserName ?? string.Empty;
            return $"{decryptedToken}:{smsApiKey}:{GetSystemId()}:{userName}:{school.SchoolName}:{school.Telephone}:{school.SchoolAddress}";
        }

        public async Task<bool> StudentsGuardianInfoUpdate(List<StudentsGuardianInfoUpdateDto> model, CancellationToken ct = default)
        {
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var studentRepo = _uow.Repository<TxStudentDetail>();
            var studentList = await studentRepo.WhereAsync(x => x.SchoolInfoId == schoolId && x.Status != StudentStatus.Previous, ct, s => s.User, c => c.ClassArm.Classs);

            foreach (var each in model)
            {
                var studentModel = studentList.Find(x => x.UserId == each.StudentId);
                if (studentModel == null) continue;
                studentModel.MobileNumber = each.TelNo;
                studentModel.GuardiansName = each.GuardianName;
                studentRepo.Update(studentModel);
            }
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        public string GetSystemId()
        {
            return CurrentUserId();
        }

        public async Task<SmsUsersDto> GetSchoolInfoForSms(CancellationToken ct = default)
        {
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var repo = _uow.Repository<SchoolTyInfo, string>();
            var schoolInfo = await repo.FindAsync(c => c.SchoolRegistrationOfficerId == schoolId, ct, x => x.SchoolRegistrationOfficer.User);
            if (schoolInfo == null) return new SmsUsersDto();

            return new SmsUsersDto
            {
                UserName = schoolInfo.SchoolRegistrationOfficer.User.UserName,
                Email = schoolInfo.EmailAddress,
                Password = schoolInfo.SchoolRegistrationOfficer.User.PasswordHash,
                SchoolName = schoolInfo.SchoolName,
                PhoneNumber = schoolInfo.Telephone,
                Id = schoolInfo.SchoolRegistrationOfficer.UserId,
                SmsApiToken = schoolInfo.SmsApiToken,
                SmsApiKey = schoolInfo.SmsApiKey
            };
        }

        public async Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessClassArmResultByClassNameAndArmName(string sessionId, TermStatus termInEnum, CurrentClass classNameInEnum, ArmName armInEnum, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolIdForCurrentUserAsync(ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return null;
            }

            var armRepo = _uow.Repository<AxClassArm>();
            var arm = await armRepo.FindAsync(x => x.Classs.ClassName == classNameInEnum && x.ArmName == armInEnum && x.Classs.SchoolInfoId == schoolId, ct, v => v.Classs);
            if (arm == null) return null;

            var assessRepo = _uow.Repository<StudenXtAccessment>();
            var yearlyStatus = (await assessRepo.FindAsync(s =>
                    s.ClassArmId == arm.Id &&
                    s.Student.SchoolInfoId == schoolId &&
                    s.YearlyAndTermlyStatus.SessionId == sessionId &&
                    s.YearlyAndTermlyStatus.TermStatus == termInEnum, ct, d => d.YearlyAndTermlyStatus.Session))?.YearlyAndTermlyStatus;

            if (yearlyStatus == null) return null;

            List<StudenXtAccessment> students;
            if (yearlyStatus.TermStatus == TermStatus.Third)
            {
                students = await assessRepo.WhereAsync(s =>
                        s.ClassArmId == arm.Id &&
                        s.Student.SchoolInfoId == schoolId &&
                        s.YearlyAndTermlyStatus.SessionId == sessionId &&
                        (yearlyStatus.TermlyAccessmentStatus != TermlyAccessmentStatus.Previous
                            ? s.Student.Status != StudentStatus.Previous
                            : true),
                    ct, s => s.Student.User, s => s.Subject, d => d.YearlyAndTermlyStatus.Session, x => x.ClassArm.Classs);
            }
            else
            {
                students = await assessRepo.WhereAsync(s =>
                        s.ClassArmId == arm.Id &&
                        s.Student.SchoolInfoId == schoolId &&
                        s.YearlyAndTermlyStatus.SessionId == sessionId &&
                        s.YearlyAndTermlyStatus.TermStatus == termInEnum &&
                        (yearlyStatus.TermlyAccessmentStatus != TermlyAccessmentStatus.Previous
                            ? s.Student.Status != StudentStatus.Previous
                            : true),
                    ct, s => s.Student.User, s => s.Subject, d => d.YearlyAndTermlyStatus.Session, x => x.ClassArm.Classs);
            }

            var mapped = _mapper.Map<List<StudentsAccessmentDto>>(students);
            if (mapped == null || !mapped.Any())
                return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto { StatusCode = 1 };

            var affectRepo = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var affect = await affectRepo.WhereAsync(a =>
                    a.ClassArmId == arm.Id &&
                    a.YearlyAndTermlyStatus.SessionId == sessionId &&
                    a.YearlyAndTermlyStatus.TermStatus == termInEnum, ct, a => a.AffectiveAndPsychomotor, c => c.FormMaster.User);

            if (affect == null || !affect.Any())
            {
                return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
                {
                    StatusCode = 2,
                    StudentsAccessment = mapped,
                    CasSetting = new CasSettingDto
                    {
                        PerCasValue = mapped.Last().YearlyAndTermlyStatus.PerCasValue,
                        CasCategory = mapped.Last().YearlyAndTermlyStatus.CasCategory
                    }
                };
            }

            var schoolHead = await SchoolInfoForTeacherResultProcessing(
                classNameInEnum is CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3 or CurrentClass.Jss1 or CurrentClass.Jss2 or CurrentClass.Jss3
                    ? affect.First().YearlyAndTermlyStatus.PrincipalId
                    : affect.First().YearlyAndTermlyStatus.HeadmasterId, ct);

            var attendanceRepo = _uow.Repository<StudentsAtteXzndanceAndCommentsReport>();
            var attendance = await attendanceRepo.WhereAsync(a =>
                    a.ClassArmId == arm.Id &&
                    a.YearlyAndTermlyStatus.SessionId == sessionId &&
                    a.YearlyAndTermlyStatus.TermStatus == termInEnum, ct);

            return new GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
            {
                AffectiveAndPsychomotorAssessment = affect.Select(f => new AffectiveAndPsychomotorAssessmentOutputXtractionForFormTeacherDto
                {
                    ClassArmId = f.ClassArmId,
                    FormMaster = new ArmFormMasterDto
                    {
                        FormMastersName = f.FormMaster.User.FullName,
                        SignatureString = f.FormMaster.SignatureImageString,
                        AssignedArmId = f.ClassArmId
                    },
                    StudentId = f.StudentId,
                    YearlyAndTermlyAssessmentStatusId = f.YearlyAndTermlyStatusId,
                    AffectiveAndPsychomotor = _mapper.Map<AffectiveAndPsychomotorDto>(f.AffectiveAndPsychomotor),
                    ItemId = f.ItemId,
                    Score = f.Score
                }).ToList(),
                StudentsAccessment = mapped,
                StudentsAttendance = _mapper.Map<List<StudentsAttendanceDto>>(attendance),
                SchoolHeadInfo = schoolHead,
                CasSetting = new CasSettingDto
                {
                    CasCategory = mapped.Last().YearlyAndTermlyStatus.CasCategory,
                    PerCasValue = mapped.Last().YearlyAndTermlyStatus.PerCasValue
                }
            };
        }

        public async Task<List<CbtQuestionAndAnswerDto>> GetCbtQuestionsByReferenceId(string subjectId, string classId, byte termId, AssessmentType assessmentType, CancellationToken ct = default)
        {
            var repo = _uow.Repository<CbtQuestionAndAnswer>();
            var rows = await repo.WhereAsync(x => x.ClassId == classId && x.SubjectId == subjectId && x.TermId == termId && x.AssessmentType == assessmentType, ct);
            return _mapper.Map<List<CbtQuestionAndAnswerDto>>(rows);
        }

        public async Task<CbtQuestionAndAnswerDto> GetCbtQuestionAndAnswerById(string cbtQuestionAndAnswerId, CancellationToken ct = default)
        {
            var repo = _uow.Repository<CbtQuestionAndAnswer>();
            var entity = await repo.FindAsync(cbtQuestionAndAnswerId, ct);
            return _mapper.Map<CbtQuestionAndAnswerDto>(entity);
        }

        public async Task<List<ContinuousAssessmentTypeDto>> GetListOfTermlyAssessmentCategory(CancellationToken ct = default)
        {
            var schoolId = await GetSchoolIdForTeacher(ct);
            var repo = _uow.Repository<ContinuousAssessmentType, short>();
            var rows = await repo.WhereAsync(x => x.SchoolId == schoolId, ct);
            return _mapper.Map<List<ContinuousAssessmentTypeDto>>(rows);
        }

        public Task<List<SelectListItem>> GetCorrectAssessmentType(AssessmentType assessmentType, CancellationToken ct = default)
        {
            var startClas = new List<SelectListItem>
            {
                new() {Text = assessmentType.ToString(), Value = ((int) assessmentType).ToString()}
            };
            var enumToList = EnumsAndExtensionMethods.GetEnumToList<AssessmentType>();
            startClas.AddRange(from item in enumToList
                               where item != assessmentType
                               select new SelectListItem { Text = item.ToString(), Value = ((int)item).ToString() });
            return Task.FromResult(startClas);
        }

        public Task<List<SelectListItem>> GetCorrectAnswerOption(AnswerOption answer, CancellationToken ct = default)
        {
            var startClas = new List<SelectListItem>
            {
                new() {Text = answer.ToString(), Value = ((int) answer).ToString()}
            };
            var enumToList = EnumsAndExtensionMethods.GetEnumToList<AnswerOption>();
            startClas.AddRange(from item in enumToList
                               where item != answer
                               select new SelectListItem { Text = item.ToString(), Value = ((int)item).ToString() });
            return Task.FromResult(startClas);
        }

        public async Task<List<SelectListItem>> GetListOfTermlyContinuousAssessmentForDropdown(AssessmentType assessmentType, CancellationToken ct = default)
        {
            var list = (await GetListOfTermlyAssessmentCategory(ct))
                .Select(x => new SelectListItem { Text = x.AssessmentType.ToString(), Value = ((int)x.AssessmentType).ToString() })
                .ToList();

            var current = list.FirstOrDefault(x => x.Text == assessmentType.ToString());
            if (current == null) return list;

            var result = new List<SelectListItem> { new() { Text = current.Text, Value = current.Value } };
            result.AddRange(list.Where(x => x.Text != current.Text));
            return result;
        }

        public async Task<List<SelectListItem>> GetClassDropdown(string className, CancellationToken ct = default)
        {
            var classes = (await GetTeacherAssignedClassesAndSubjects(ct))
                .GroupBy(z => z.Arm.ClassId)
                .Select(a => a.First())
                .Select(x => new SelectListItem
                {
                    Text = x.Arm.Classs.ClassLabel,
                    Value = x.Arm.ClassId.ToString()
                }).ToList();

            var currentObject = classes.Find(x => x.Text.Trim() == className.Trim());
            var otherObjects = classes.Where(x => x.Text.Trim() != className.Trim());
            var returnList = new List<SelectListItem>();
            if (currentObject != null)
            {
                returnList.Add(new SelectListItem { Text = currentObject.Text, Value = currentObject.Value });
            }
            returnList.AddRange(otherObjects);
            return returnList;
        }

        public async Task UpdateQuestionsAndAnswer(CbtQuestionAndAnswerDto questionAndAnswer, CancellationToken ct = default)
        {
            var repo = _uow.Repository<CbtQuestionAndAnswer>();
            var mapModel = _mapper.Map<CbtQuestionAndAnswer>(questionAndAnswer);
            repo.Update(mapModel);
            await _uow.SaveChangesAsync(ct);
        }

        public async Task<bool> DeleteQuestion(string questionId, CancellationToken ct = default)
        {
            var repo = _uow.Repository<CbtQuestionAndAnswer>();
            var entity = await repo.FindAsync(sid => sid.Id == questionId, ct);
            if (entity == null) return false;
            repo.Delete(entity);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        public async Task<List<SelectListItem>> GetSubjectDropdown(CancellationToken ct = default)
        {
            var list = (await GetTeacherAssignedClassesAndSubjects(ct))
                .GroupBy(z => z.SubjectId)
                .Select(a => a.First())
                .Select(x => new SelectListItem
                {
                    Text = x.Subject.SubjectName,
                    Value = x.SubjectId.ToString()
                }).ToList();
            return list;
        }

        public async Task<List<CbtQuestionAndAnswerDto>> GetQuestionByParameter(string searchParameter, CancellationToken ct = default)
        {
            var schoolId = await GetSchoolIdForTeacher(ct);
            var repo = _uow.Repository<CbtQuestionAndAnswer>();
            var rows = await repo.WhereAsync(t =>
                t.Classs.SchoolInfoId == schoolId &&
                t.Question.Trim().ToLower().Contains(searchParameter.Trim().ToLower()), ct);
            return _mapper.Map<List<CbtQuestionAndAnswerDto>>(rows);
        }

        public async Task<List<SelectListItem>> GetDefaultListOfTermlyContinuousAssessmentForDropdown(CancellationToken ct = default)
        {
            var list = (await GetListOfTermlyAssessmentCategory(ct))
                .Select(x => new SelectListItem
                { Text = x.AssessmentType.ToString(), Value = x.AssessmentType.ToString() })
                .ToList();
            list.Add(new SelectListItem { Text = "Exams", Value = "Exams" });
            return list;
        }

        public async Task<CbtQuestionAndAnswerDto> GetCbtQuestionAndAnswer2ndById(string cbtQuestionAndAnswerId, CancellationToken ct = default)
        {
            var repo = _uow.Repository<CbtQuestionAndAnswer>();
            var entity = await repo.FindAsync(x => x.Id == cbtQuestionAndAnswerId, ct, r => r.Subject, e => e.Classs);
            return _mapper.Map<CbtQuestionAndAnswerDto>(entity);
        }

        public async Task<List<SelectListItem>> GetDefaultClassDropdown(CancellationToken ct = default)
        {
            var classes = (await GetTeacherAssignedClassesAndSubjects(ct))
                .GroupBy(z => z.Arm.ClassId)
                .Select(a => a.First())
                .Select(x => new SelectListItem
                {
                    Text = x.Arm.Classs.ClassLabel,
                    Value = x.Arm.ClassId.ToString()
                }).ToList();
            return classes;
        }

        public async Task<CbtQuestionAnswerAndSetQuestionDto> GetQuestions(QuestionProcessingActionType actionType, byte termId, string assessmentType, string classId, string subjectId, int noOfQuestions, CancellationToken ct = default)
        {
            var result = new CbtQuestionAnswerAndSetQuestionDto();
            var qaRepo = _uow.Repository<CbtQuestionAndAnswer>();
            var setRepo = _uow.Repository<CbtSetQuestion>();

            if (actionType == QuestionProcessingActionType.GetAlReadyGeneratedQuestions)
            {
                List<CbtSetQuestion> setList;
                if (assessmentType == "Cumulative Exams")
                {
                    setList = await setRepo.WhereAsync(x => x.QuestionAndAnswer.ClassId == classId && x.QuestionAndAnswer.SubjectId == subjectId, ct,
                        a => a.QuestionAndAnswer.Subject, e => e.QuestionAndAnswer.Term, r => r.QuestionAndAnswer.Classs);
                }
                else if (assessmentType == "Exams")
                {
                    setList = await setRepo.WhereAsync(x =>
                        x.QuestionAndAnswer.TermId == termId && x.QuestionAndAnswer.ClassId == classId && x.QuestionAndAnswer.SubjectId == subjectId, ct,
                        a => a.QuestionAndAnswer.Subject, e => e.QuestionAndAnswer.Term, r => r.QuestionAndAnswer.Classs);
                }
                else
                {
                    var typeEnum = (AssessmentType)Enum.Parse(typeof(AssessmentType), assessmentType);
                    setList = await setRepo.WhereAsync(x => x.QuestionAndAnswer.AssessmentType == typeEnum &&
                                                            x.QuestionAndAnswer.TermId == termId &&
                                                            x.QuestionAndAnswer.ClassId == classId &&
                                                            x.QuestionAndAnswer.SubjectId == subjectId, ct,
                        a => a.QuestionAndAnswer.Subject, e => e.QuestionAndAnswer.Term, r => r.QuestionAndAnswer.Classs);
                }
                result.SetQuestion = _mapper.Map<List<CbtSetQuestionDto>>(setList);
            }
            else if (actionType == QuestionProcessingActionType.GetAlReadyGeneratedQuestionsAndBalanceTheRequest)
            {
                var setList = new List<CbtSetQuestion>();
                if (assessmentType == "Cumulative Exams")
                {
                    setList = await setRepo.WhereAsync(x => x.QuestionAndAnswer.ClassId == classId && x.QuestionAndAnswer.SubjectId == subjectId, ct,
                        a => a.QuestionAndAnswer.Subject, e => e.QuestionAndAnswer.Term, r => r.QuestionAndAnswer.Classs);
                }
                else if (assessmentType == "Exams")
                {
                    setList = await setRepo.WhereAsync(x =>
                        x.QuestionAndAnswer.TermId == termId && x.QuestionAndAnswer.ClassId == classId && x.QuestionAndAnswer.SubjectId == subjectId, ct,
                        a => a.QuestionAndAnswer.Subject, e => e.QuestionAndAnswer.Term, r => r.QuestionAndAnswer.Classs);
                }
                else
                {
                    var typeEnum = (AssessmentType)Enum.Parse(typeof(AssessmentType), assessmentType);
                    setList = await setRepo.WhereAsync(x => x.QuestionAndAnswer.AssessmentType == typeEnum &&
                                                            x.QuestionAndAnswer.TermId == termId &&
                                                            x.QuestionAndAnswer.ClassId == classId &&
                                                            x.QuestionAndAnswer.SubjectId == subjectId, ct,
                        a => a.QuestionAndAnswer.Subject, e => e.QuestionAndAnswer.Term, r => r.QuestionAndAnswer.Classs);
                }

                if (setList.Count < noOfQuestions)
                {
                    var recordsToComplete = noOfQuestions - setList.Count;
                    setList = await FillUpRemainingQuestions(recordsToComplete, setList, classId, subjectId, ct);
                }
                if (setList.Count > noOfQuestions)
                {
                    setList = await RemoveExcessQuestions(setList, setRepo, noOfQuestions, ct);
                }
                result.SetQuestion = _mapper.Map<List<CbtSetQuestionDto>>(setList);
            }
            else
            {
                // Fresh generation of questions
                var existingSet = await setRepo.WhereAsync(x => x.QuestionAndAnswer.ClassId == classId && x.QuestionAndAnswer.SubjectId == subjectId, ct);
                foreach (var each in existingSet) setRepo.Delete(each);
                await _uow.SaveChangesAsync(ct);

                List<CbtQuestionAndAnswer> qaList;
                if (assessmentType == "Cumulative Exams")
                {
                    qaList = (await qaRepo.WhereAsync(x => x.ClassId == classId && x.SubjectId == subjectId, ct))
                        .ShuffleMe().Take(noOfQuestions).ToList();
                }
                else if (assessmentType == "Exams")
                {
                    qaList = (await qaRepo.WhereAsync(x => x.TermId == termId && x.ClassId == classId && x.SubjectId == subjectId, ct))
                        .ShuffleMe().Take(noOfQuestions).ToList();
                }
                else
                {
                    var typeEnum = (AssessmentType)Enum.Parse(typeof(AssessmentType), assessmentType);
                    qaList = (await qaRepo.WhereAsync(x => x.AssessmentType == typeEnum && x.TermId == termId && x.ClassId == classId && x.SubjectId == subjectId, ct,
                        a => a.Subject, e => e.Term, r => r.Classs)).ShuffleMe().Take(noOfQuestions).ToList();
                }

                result.QuestionAndAnswer = _mapper.Map<List<CbtQuestionAndAnswerDto>>(qaList);
            }

            return result;
        }

        private async Task<List<CbtSetQuestion>> FillUpRemainingQuestions(int recordsToComplete, List<CbtSetQuestion> cbtSetQuestionList, string classId, string subjectId, CancellationToken ct)
        {
            var setIds = cbtSetQuestionList.Select(b => b.QuestionAndAnswerId).ToHashSet();
            var qaRepo = _uow.Repository<CbtQuestionAndAnswer>();
            var setRepo = _uow.Repository<CbtSetQuestion>();
            var qaList = (await qaRepo.WhereAsync(x => x.ClassId == classId && x.SubjectId == subjectId, ct)).ShuffleMe().ToList();

            foreach (var item in qaList)
            {
                if (recordsToComplete == 0) break;
                if (!setIds.Contains(item.Id))
                {
                    var setEntity = new CbtSetQuestion {Id = item.Id, QuestionAndAnswerId = item.Id };
                    setRepo.Add(setEntity);
                    cbtSetQuestionList.Add(setEntity);
                    recordsToComplete--;
                }
            }
            await _uow.SaveChangesAsync(ct);
            return cbtSetQuestionList;
        }

        private async Task<List<CbtSetQuestion>> RemoveExcessQuestions(List<CbtSetQuestion> cbtSetQuestionList, IGenericRepository<CbtSetQuestion> cbtSetQuestionRepo, int noOfQuestions, CancellationToken ct)
        {
            var recordsToRemove = cbtSetQuestionList.Count - noOfQuestions;
            if (recordsToRemove <= 0) return cbtSetQuestionList;

            // Remove from the end to minimize re-shuffling impact
            for (var i = cbtSetQuestionList.Count - 1; i >= 0 && recordsToRemove > 0; i--)
            {
                var each = cbtSetQuestionList[i];
                cbtSetQuestionList.RemoveAt(i);
                cbtSetQuestionRepo.Delete(each);
                recordsToRemove--;
            }
            await _uow.SaveChangesAsync(ct);
            return cbtSetQuestionList;
        }
    }
}