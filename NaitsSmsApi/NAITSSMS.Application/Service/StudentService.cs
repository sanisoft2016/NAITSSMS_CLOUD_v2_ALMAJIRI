using AutoMapper;
using Microsoft.AspNetCore.Http;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Data.Security;
using NAITSSMS.Infrastructure.Repo;
using NAITSSMS.Infrastructure.Unitofwork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.Service
{
    public class StudentService : IStudentService
    {
        private readonly IUnitOfWork _uow;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ISchoolScopeResolver _schoolScopeResolver;
        private readonly IMapper _mapper;
        public StudentService(
            IMapper mapper,
            IUnitOfWork uow,
            IHttpContextAccessor httpContextAccessor,
            ISchoolScopeResolver schoolScopeResolver)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _schoolScopeResolver = schoolScopeResolver ?? throw new ArgumentNullException(nameof(schoolScopeResolver));
        }

        public async Task<string> GetLastStudentAdminssionNumber(CancellationToken ct = default)
        {
            var studentRepository = _uow.Repository<TxStudentDetail>();
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);

            // As in the legacy system: the last registered student is the one whose login was
            // created most recently. SNo is only the student's position within a class arm.
            var students = await studentRepository.SelectWhereAsync(
                s => s.SchoolInfoId == schoolId,
                s => new { s.IdNo, CreatedDate = s.User != null ? s.User.CreatedDate : DateTime.MinValue },
                ct);

            if (!students.Any())
                return "001";

            var lastStudent = students.OrderByDescending(s => s.CreatedDate).FirstOrDefault();
            return lastStudent?.IdNo ?? "001";
        }

        public async Task<byte> CreatStudent(StudentDetailsDto model, CancellationToken ct = default)
        {
            // Simplified implementation - delegates to overload 2
            return await CreatStudent(model, model.ClassArm.ArmName, model.CurrentClass, ct);
        }

        // Method 7: CreatStudent (overload 2) - Complex student creation
        public async Task<byte> CreatStudent(StudentDetailsDto model, ArmName armName, CurrentClass currentClass, CancellationToken ct = default)
        {
            var studentRepository = _uow.Repository<TxStudentDetail>();
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);

            model.DateOfBirth = NormalizeToUtc(model.DateOfBirth);
            model.DateOfAdmsn = NormalizeToUtc(model.DateOfAdmsn);
            if (model.DateOfDischarge.HasValue)
            {
                model.DateOfDischarge = NormalizeToUtc(model.DateOfDischarge.Value);
            }

            SanitizeNullableStringsForPersistence(model);

            // Generate ID if needed
            if (string.IsNullOrWhiteSpace(model.Id))
            {
                model.Id = Guid.NewGuid().ToString();
            }

            // SNo is the student's position within their class arm (as in the legacy system and
            // the registration page): keep a supplied value, otherwise the arm's count + 1.
            if (model.SNo <= 0)
            {
                var armId = model.ClassArmId?.Trim() ?? string.Empty;
                var studentsInArm = await studentRepository.CountAsync(s => s.ClassArmId == armId, ct);
                model.SNo = (short)(studentsInArm + 1);
            }

            model.SchoolInfoId = schoolId;
            model.CurrentClass = currentClass;

            var studentEntity = _mapper.Map<TxStudentDetail>(model);
            studentRepository.Add(studentEntity);
            
            // NOTE: Do NOT call SaveChangesAsync here when called from within a transaction.
            // The controller's transaction will commit all changes atomically.
            // If called from non-transactional context, caller must call SaveChangesAsync.

            return 0; // Success
        }

        private static void SanitizeNullableStringsForPersistence(StudentDetailsDto model)
        {
            model.IdNo = model.IdNo?.Trim() ?? string.Empty;
            model.PlaceOfBirth = model.PlaceOfBirth?.Trim() ?? string.Empty;
            model.BloodGroup = model.BloodGroup?.Trim() ?? string.Empty;
            model.TransferedFromWhere = model.TransferedFromWhere?.Trim() ?? string.Empty;
            model.StartClass = model.StartClass?.Trim() ?? string.Empty;
            model.Passport = model.Passport?.Trim() ?? string.Empty;
            model.GuardiansName = model.GuardiansName?.Trim() ?? string.Empty;
            model.Relationship = model.Relationship?.Trim() ?? string.Empty;
            model.GuardianAddress = model.GuardianAddress?.Trim() ?? string.Empty;
            model.Occupation = model.Occupation?.Trim() ?? string.Empty;
            model.MobileNumber = model.MobileNumber?.Trim() ?? string.Empty;
            model.HomeNumber = model.HomeNumber?.Trim() ?? string.Empty;
            model.EmailAddress = model.EmailAddress?.Trim() ?? string.Empty;
            model.MailingAddress = model.MailingAddress?.Trim() ?? string.Empty;
            model.Country = model.Country?.Trim() ?? string.Empty;
            model.UserId = model.UserId?.Trim() ?? string.Empty;
            model.SchoolInfoId = model.SchoolInfoId?.Trim() ?? string.Empty;
            model.AdmisnTermId = model.AdmisnTermId?.Trim() ?? string.Empty;
        }

        private static DateTime NormalizeToUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            if (value.Kind == DateTimeKind.Local)
            {
                return value.ToUniversalTime();
            }

            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }


        public async Task<int> GetSchoolSerialId(CancellationToken ct = default)
        {
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var schoolInfoRepository = _uow.Repository<SchoolTyInfo>();

            var schoolInfo = await schoolInfoRepository.FindAsync(
                s => s.SchoolRegistrationOfficerId == schoolId, ct);

            return schoolInfo?.SsNo ?? 0;
        }

      
        public async Task<PrepareEnvironmentForStudentAssessmentReturnDto> PrepareAssessmentEnvironmentForTheWholeStudents(
            byte? requestStatus,
            CancellationToken ct = default)
        {
            if (_uow.HasActiveTransaction)
            {
                await _uow.RollbackTransactionAsync();
            }
            else
            {
                _uow.RejectChanges();
            }

            var studentDetailRepository = _uow.Repository<TxStudentDetail>();
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var allStudents = await studentDetailRepository.WhereAsync(
                t =>
                    (t.Status == StudentStatus.Fresher ||
                     t.Status == StudentStatus.Returning) &&
                    t.SchoolInfoId == schoolId,
                ct,
                r => r.ClassArm!.Classs!,
                u => u.User!,
                g => g.SchoolInfo!);

            return await StudentNewAssessmentPreparation(requestStatus, allStudents.ToList(), ct);
        }

        private async Task<string> GetSchoolIdForSchoolRegistrationOfficer(CancellationToken ct = default)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var userId = httpContext?.User?.FindFirstValue(ClaimTypes.PrimarySid)
                ?? httpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext?.User?.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedAccessException("Unable to resolve the current user claim for school registration context.");

            return await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
        }

        private async Task<PrepareEnvironmentForStudentAssessmentReturnDto> StudentNewAssessmentPreparation(
            byte? requestStatus,
            List<TxStudentDetail> allStudents,
            CancellationToken ct = default)
        {
            var studentAssessmentRepository = _uow.Repository<StudenXtAccessment>();
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var assessmentCount = await studentAssessmentRepository.CountAsync(
                a => a.Employee!.SchoolId == schoolId,
                ct);

            if (assessmentCount <= 0)
            {
                return new PrepareEnvironmentForStudentAssessmentReturnDto
                {
                    Message = "You first need to run the first environmental preparation"
                };
            }

            var yearlyAndTermlyAssessmentStatusRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
            var yearlyAndTermlyAssessmentStatus = await yearlyAndTermlyAssessmentStatusRepository.FindAsync(
                t =>
                    (t.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                     t.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing) &&
                    t.Session!.SchoolInfoId == schoolId,
                ct,
                t => t.Session!);

            var academicSessionRepository = _uow.Repository<TxAcademicSession>();
            var currentAcademicSession = await academicSessionRepository.FindAsync(
                s => s.Status == TimeStatus.Current && s.SchoolInfoId == schoolId,
                ct);

            var teacherClassArmAssignmentRepository = _uow.Repository<TeacherClassArmTxAssignment>();
            var studentDetailRepository = _uow.Repository<TxStudentDetail>();

            string yearlyAndTermlyAssessmentStatusId;
            var currentTermStatus = yearlyAndTermlyAssessmentStatus?.TermStatus ?? TermStatus.First;

            if (yearlyAndTermlyAssessmentStatus == null)
            {
                if (currentAcademicSession == null)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message = "You need to set the current Academic Session to continue."
                    };
                }

                var schoolRepository = _uow.Repository<SchoolTyInfo>();
                var school = await schoolRepository.FindAsync(
                    c => c.SchoolRegistrationOfficerId == schoolId,
                    ct);

                if (school != null)
                {
                    school.SystemImplementationType = FreePlugIn.Encrypt(FreePlugIn.GetUniqueSystemId() + ":0", true);
                    schoolRepository.Update(school);
                }

                var assessmentRows = await studentAssessmentRepository.WhereAsync(
                    t => t.Employee!.SchoolId == schoolId,
                    ct,
                    a => a.YearlyAndTermlyStatus!);

                var lastAssessment = assessmentRows.OrderByDescending(g => g.LastUpdateDate).FirstOrDefault();
                if (lastAssessment?.YearlyAndTermlyStatus == null)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message = "Unable to determine the last assessment term."
                    };
                }

                currentTermStatus = NextTermStatus(lastAssessment.YearlyAndTermlyStatus.TermStatus);

                var lastYearlyAndTermlyAssessmentStatus = (await yearlyAndTermlyAssessmentStatusRepository.WhereAsync(
                    t => t.Session!.SchoolInfoId == schoolId,
                        ct,
                    t => t.Session!))
                    .OrderBy(x => x.CreatedDate)
                    .ThenBy(x => x.Id)
                    .LastOrDefault();

                if (lastYearlyAndTermlyAssessmentStatus == null)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message = "Unable to determine the previous assessment status."
                    };
                }

                var yearlyAndTermlyAssessmentModel = new TxYearlyAndTermlyStatus
                {
                    Id = Guid.NewGuid().ToString(),
                    TermStatus = currentTermStatus,
                    SessionId = currentAcademicSession.Id,
                    CreatedDate = DateTime.Now,
                    TermlyAccessmentStatus = TermlyAccessmentStatus.Current,
                    CasCategory = lastYearlyAndTermlyAssessmentStatus.CasCategory,
                    PerCasValue = lastYearlyAndTermlyAssessmentStatus.PerCasValue,
                    MinPassMark = lastYearlyAndTermlyAssessmentStatus.MinPassMark,
                    PrincipalId = lastYearlyAndTermlyAssessmentStatus.PrincipalId,
                    HeadmasterId = lastYearlyAndTermlyAssessmentStatus.HeadmasterId
                };

                yearlyAndTermlyAssessmentStatusRepository.Add(yearlyAndTermlyAssessmentModel);
                yearlyAndTermlyAssessmentStatusId = yearlyAndTermlyAssessmentModel.Id;

                var completedTeacherAssignments = await teacherClassArmAssignmentRepository.WhereAsync(
                    t =>
                        t.Employee!.SchoolId == schoolId &&
                        t.ArmTermlyAssessmentStatus == ArmTermlyAssessmentStatus.Completed,
                    ct);

                foreach (var assignment in completedTeacherAssignments)
                {
                    assignment.ArmTermlyAssessmentStatus = ArmTermlyAssessmentStatus.Uncompleted;
                    teacherClassArmAssignmentRepository.Update(assignment);
                }

                AddStudentAcademicProgressHistory(allStudents, studentDetailRepository, yearlyAndTermlyAssessmentStatusId);
                await _uow.SaveChangesAsync(ct);
            }
            else
            {
                yearlyAndTermlyAssessmentStatusId = yearlyAndTermlyAssessmentStatus.Id;

                var hasTeacherAssignments = await teacherClassArmAssignmentRepository.CountAsync(
                    t => t.Employee!.SchoolId == schoolId,
                    ct);

                if (hasTeacherAssignments <= 0)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message = "Failed! For you need to first assign form-masters to thier appropriate class-arm before using this feature."
                    };
                }

                var uncompletedTeacherAssignments = await teacherClassArmAssignmentRepository.WhereAsync(
                    t =>
                        t.Employee!.SchoolId == schoolId &&
                        t.ArmTermlyAssessmentStatus == ArmTermlyAssessmentStatus.Uncompleted,
                    ct,
                    t => t.Employee!.User!,
                    t => t.Arm!.Classs!);

                var assessmentRows = await studentAssessmentRepository.WhereAsync(
                    a => a.Employee!.SchoolId == schoolId,
                    ct,
                    a => a.YearlyAndTermlyStatus!);

                var lastAssessment = assessmentRows.OrderByDescending(g => g.LastUpdateDate).FirstOrDefault();
                if (lastAssessment?.YearlyAndTermlyStatus == null)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message = "Unable to determine the last assessment term."
                    };
                }

                var lastTermsName = lastAssessment.YearlyAndTermlyStatus.TermStatus;

                if (requestStatus != null)
                {
                    var report = uncompletedTeacherAssignments.Select(r => new UncompletedFormMastersReportDto
                    {
                        FormMasterName =
                            string.Join(
                                " ",
                                new[]
                                {
                                    r.Employee?.User?.FirstName,
                                    r.Employee?.User?.MiddleName,
                                    r.Employee?.User?.SurName
                                }.Where(name => !string.IsNullOrWhiteSpace(name))),
                        UserName = r.Employee?.User?.UserName ?? string.Empty,
                        ClassLabelAndArm = FormatClassLabelAndArm(r.Arm)
                    }).ToList();

                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
                        CurrentTermStatus = yearlyAndTermlyAssessmentStatus.TermStatus,
                        UncompletedFormMastersReport = report,
                        Message = report.Count == 0 ? "Success!" : string.Empty
                    };
                }

                if (!uncompletedTeacherAssignments.Any())
                {
                    if (lastTermsName == TermStatus.Third)
                    {
                        return new PrepareEnvironmentForStudentAssessmentReturnDto
                        {
                            YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
                            CurrentTermStatus = yearlyAndTermlyAssessmentStatus.TermStatus,
                            Message = "Promotion"
                        };
                    }

                    if (currentAcademicSession == null)
                    {
                        return new PrepareEnvironmentForStudentAssessmentReturnDto
                        {
                            Message = "You need to set the current Academic Session to continue."
                        };
                    }

                    yearlyAndTermlyAssessmentStatus.TermlyAccessmentStatus = TermlyAccessmentStatus.Previous;
                    yearlyAndTermlyAssessmentStatusRepository.Update(yearlyAndTermlyAssessmentStatus);

                    var completedTeacherAssignments = await teacherClassArmAssignmentRepository.WhereAsync(
                        t =>
                            t.Employee!.SchoolId == schoolId &&
                            t.ArmTermlyAssessmentStatus == ArmTermlyAssessmentStatus.Completed,
                        ct);

                    foreach (var assignment in completedTeacherAssignments)
                    {
                        assignment.ArmTermlyAssessmentStatus = ArmTermlyAssessmentStatus.Uncompleted;
                        teacherClassArmAssignmentRepository.Update(assignment);
                    }

                    currentTermStatus = NextTermStatus(lastTermsName);
                    var yearlyAndTermlyAssessmentModel = new TxYearlyAndTermlyStatus
                    {
                        Id = Guid.NewGuid().ToString(),
                        TermStatus = currentTermStatus,
                        SessionId = currentAcademicSession.Id,
                        CreatedDate = DateTime.Now,
                        TermlyAccessmentStatus = TermlyAccessmentStatus.Current,
                        CasCategory = yearlyAndTermlyAssessmentStatus.CasCategory,
                        PerCasValue = yearlyAndTermlyAssessmentStatus.PerCasValue,
                        MinPassMark = yearlyAndTermlyAssessmentStatus.MinPassMark,
                        PrincipalId = yearlyAndTermlyAssessmentStatus.PrincipalId,
                        HeadmasterId = yearlyAndTermlyAssessmentStatus.HeadmasterId
                    };

                    yearlyAndTermlyAssessmentStatusRepository.Add(yearlyAndTermlyAssessmentModel);
                    yearlyAndTermlyAssessmentStatusId = yearlyAndTermlyAssessmentModel.Id;

                    AddStudentAcademicProgressHistory(allStudents, studentDetailRepository, yearlyAndTermlyAssessmentStatusId);
                    await _uow.SaveChangesAsync(ct);
                }
            }

            var createOrUpdateNewAssessment = await CreateOrUpdateNewAssessment(
                allStudents,
                studentAssessmentRepository,
                schoolId,
                yearlyAndTermlyAssessmentStatusId,
                ct);

            if (createOrUpdateNewAssessment.Message == "Success")
            {
                createOrUpdateNewAssessment.YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId;
                createOrUpdateNewAssessment.CurrentTermStatus = currentTermStatus;
            }

            return createOrUpdateNewAssessment;
        }

        private async Task<PrepareEnvironmentForStudentAssessmentReturnDto> CreateOrUpdateNewAssessment(
            List<TxStudentDetail> allStudents,
            IGenericRepository<StudenXtAccessment> studentAssessmentRepository,
            string schoolId,
            string yearlyAndTermlyAssessmentStatusId,
            CancellationToken ct = default)
        {
            allStudents = allStudents
                .Where(s => s.Status == StudentStatus.Fresher || s.Status == StudentStatus.Returning)
                .ToList();

            foreach (var eachClass in allStudents.GroupBy(s => s.ClassArm!.ClassId))
            {
                var firstStudent = eachClass.First();

                if (firstStudent.CurrentClass is CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3)
                {
                    foreach (var eachDept in eachClass.GroupBy(s => s.ClassCategory))
                    {
                        List<SubjectDto> classSubjectCollection = eachDept.Key switch
                        {
                            ClassCategoryStatus.Science => await SeniorClassesSubjectListCurrent(ClassCategoryStatus.Science, ct),
                            ClassCategoryStatus.Commercial => await SeniorClassesSubjectListCurrent(ClassCategoryStatus.Commercial, ct),
                            _ => await SeniorClassesSubjectListCurrent(ClassCategoryStatus.Art, ct)
                        };

                        var returnString = await ProceedFromHereForUpdateSeniorStudent(
                            eachDept,
                            classSubjectCollection,
                            yearlyAndTermlyAssessmentStatusId,
                            studentAssessmentRepository,
                            schoolId,
                            ct);

                        if (returnString != "Succeed")
                        {
                            return new PrepareEnvironmentForStudentAssessmentReturnDto
                            {
                                Message = returnString
                            };
                        }
                    }
                }
                else
                {
                    var classSubjectCollection = await BasicClassesSubjectListCurrent(firstStudent.ClassArm!.Classs!.ClassName, ct);
                    var returnString = await ProceedFromHereForUpdateBasicStudents(
                        eachClass,
                        classSubjectCollection,
                        yearlyAndTermlyAssessmentStatusId,
                        studentAssessmentRepository,
                        schoolId,
                        ct);

                    if (returnString != "Succeed")
                    {
                        return new PrepareEnvironmentForStudentAssessmentReturnDto
                        {
                            Message = returnString
                        };
                    }
                }
            }

            var recentPreviousStudents = await studentAssessmentRepository.WhereAsync(
                t =>
                    t.Student!.Status == StudentStatus.Previous &&
                    t.Student!.SchoolInfoId == schoolId &&
                    t.YearlyAndTermlyStatus!.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                t => t.Student!,
                t => t.YearlyAndTermlyStatus!);

            foreach (var item in recentPreviousStudents)
            {
                if (Math.Abs(item.Ca1 + item.Ca2 + item.Ca3 + item.Exams) < 0.5)
                {
                    studentAssessmentRepository.Delete(item);
                }
            }

            var affectiveAndPsychomotorAssessmentRepository = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();

            var recentPreviousSubjects = await studentAssessmentRepository.WhereAsync(
                t =>
                    t.Subject!.TimeStatus == TimeStatus.Previous &&
                    t.Subject!.SubjectCategory!.SchoolId == schoolId &&
                    t.YearlyAndTermlyStatus!.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                t => t.Subject!.SubjectCategory!,
                t => t.YearlyAndTermlyStatus!);

            foreach (var item in recentPreviousSubjects)
            {
                if (Math.Abs(item.Ca1 + item.Ca2 + item.Ca3 + item.Exams) < 0.5)
                {
                    studentAssessmentRepository.Delete(item);
                }
            }

            var recentPreviousStudentAffectiveAndPsychomotorAssessments = await affectiveAndPsychomotorAssessmentRepository.WhereAsync(
                x =>
                    x.Student!.SchoolInfoId == schoolId &&
                    x.Student!.Status == StudentStatus.Previous &&
                    x.YearlyAndTermlyStatus!.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                x => x.Student!,
                x => x.YearlyAndTermlyStatus!);

            foreach (var item in recentPreviousStudentAffectiveAndPsychomotorAssessments.GroupBy(x => x.StudentId))
            {
                if (item.Sum(c => c.Score) == 0)
                {
                    foreach (var psychomotorAssessment in item)
                    {
                        affectiveAndPsychomotorAssessmentRepository.Delete(psychomotorAssessment);
                    }
                }
            }

            await _uow.SaveChangesAsync(ct);

            return new PrepareEnvironmentForStudentAssessmentReturnDto
            {
                Message = "Success",
                YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
                TotalStudentListToPdf = allStudents.Select(MapTotalStudentListToPdf).ToList()
            };
        }

        private async Task<List<SubjectDto>> SeniorClassesSubjectListCurrent(
            ClassCategoryStatus studentStatus,
            CancellationToken ct = default)
        {
            var subjectRepository = _uow.Repository<RxSubject>();
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);

            SubjectStatus[] allowedStatuses = studentStatus switch
            {
                ClassCategoryStatus.Science => new[]
                {
                    SubjectStatus.General,
                    SubjectStatus.AllSeniors,
                    SubjectStatus.Science
                },
                ClassCategoryStatus.Commercial => new[]
                {
                    SubjectStatus.General,
                    SubjectStatus.AllSeniors,
                    SubjectStatus.Commercial
                },
                _ => new[]
                {
                    SubjectStatus.General,
                    SubjectStatus.AllSeniors,
                    SubjectStatus.Art
                }
            };

            var subjects = await subjectRepository.WhereAsync(
                s =>
                    s.SubjectCategory!.SchoolId == schoolId &&
                    s.TimeStatus == TimeStatus.Current &&
                    allowedStatuses.Contains(s.SubjectCategory.SubjectStatus),
                ct,
                s => s.SubjectCategory!);

            return subjects.Select(MapSubject).ToList();
        }

        private async Task<List<SubjectDto>> BasicClassesSubjectListCurrent(
            CurrentClass className,
            CancellationToken ct = default)
        {
            var subjectRepository = _uow.Repository<RxSubject>();
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);

            SubjectStatus[] allowedStatuses = className switch
            {
                CurrentClass.PlayClass => new[] { SubjectStatus.PlayClass },
                CurrentClass.PreNursery => new[] { SubjectStatus.PreNursery },
                CurrentClass.Nursery1 or CurrentClass.Nursery2 => new[]
                {
                    SubjectStatus.General,
                    SubjectStatus.Nursery
                },
                CurrentClass.Primary1 or CurrentClass.Primary2 or CurrentClass.Primary3 or CurrentClass.Primary4 or CurrentClass.Primary5 or CurrentClass.Primary6 => new[]
                {
                    SubjectStatus.General,
                    SubjectStatus.Primary
                },
                CurrentClass.Jss1 or CurrentClass.Jss2 or CurrentClass.Jss3 => new[]
                {
                    SubjectStatus.General,
                    SubjectStatus.Jss
                },
                _ => new[] { SubjectStatus.General }
            };

            var subjects = await subjectRepository.WhereAsync(
                s =>
                    s.SubjectCategory!.SchoolId == schoolId &&
                    s.TimeStatus == TimeStatus.Current &&
                    allowedStatuses.Contains(s.SubjectCategory.SubjectStatus),
                ct,
                s => s.SubjectCategory!);

            return subjects.Select(MapSubject).ToList();
        }

        private async Task<string> ProceedFromHereForUpdateSeniorStudent(
            IGrouping<ClassCategoryStatus, TxStudentDetail> eachClass,
            List<SubjectDto> classSubjectCollection,
            string yearlyAndTermlyAssessmentStatusId,
            IGenericRepository<StudenXtAccessment> studentAssessmentRepository,
            string schoolId,
            CancellationToken ct = default)
        {
            var classSubjectArmAssignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var subjectStudentAssignmentRepository = _uow.Repository<SubjectStudentTxAssignment>();

            foreach (var eachStudent in eachClass)
            {
                var subjectsForStudent = new List<SubjectDto>(classSubjectCollection);
                var electiveSubject = await subjectStudentAssignmentRepository.WhereAsync(
                    d =>
                        d.StudentId == eachStudent.Id &&
                        d.Subject!.TimeStatus == TimeStatus.Current,
                    ct,
                    d => d.Subject!);

                if (electiveSubject.Any())
                {
                    subjectsForStudent.AddRange(electiveSubject.Select(d => MapSubject(d.Subject!)));
                }

                foreach (var eachSubject in subjectsForStudent)
                {
                    var teacher = await classSubjectArmAssignmentRepository.FindAsync(
                        r => r.ArmId == eachStudent.ClassArmId && r.SubjectId == eachSubject.Id,
                        ct);

                    if (teacher == null)
                    {
                        return "'" + eachSubject.SubjectName + "' has not been assigned to a teacher in " +
                               (eachStudent.ClassArm?.Classs?.ClassLabel ?? string.Empty) +
                               (eachStudent.ClassArm?.ArmName.ToString() ?? string.Empty) + ".";
                    }

                    var eachStudentAssessment = await studentAssessmentRepository.FindAsync(
                        n =>
                            n.SubjectId == eachSubject.Id &&
                            n.StudentId == eachStudent.Id &&
                            n.YearlyAndTermlyStatus!.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                        ct);

                    if (eachStudentAssessment != null)
                    {
                        eachStudentAssessment.EmployeeId = teacher.EmployeeId;
                        studentAssessmentRepository.Update(eachStudentAssessment);
                    }
                    else
                    {
                        studentAssessmentRepository.Add(new StudenXtAccessment
                        {
                            Id = Guid.NewGuid().ToString(),
                            EmployeeId = teacher.EmployeeId,
                            SubjectId = eachSubject.Id,
                            StudentId = eachStudent.Id,
                            ClassArmId = eachStudent.ClassArmId,
                            LastUpdateDate = DateTime.Now,
                            YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
                            Ca1 = 0,
                            Ca2 = 0,
                            Ca3 = 0,
                            Exams = 0
                        });
                    }
                }
            }

            return "Succeed";
        }

        private async Task<string> ProceedFromHereForUpdateBasicStudents(
            IGrouping<string, TxStudentDetail> eachClass,
            List<SubjectDto> classSubjectCollection,
            string yearlyAndTermlyAssessmentStatusId,
            IGenericRepository<StudenXtAccessment> studentAssessmentRepository,
            string schoolId,
            CancellationToken ct = default)
        {
            var classSubjectArmAssignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var subjectStudentAssignmentRepository = _uow.Repository<SubjectStudentTxAssignment>();

            foreach (var eachArmStudents in eachClass.GroupBy(g => g.ClassArmId))
            {
                foreach (var eachStudent in eachArmStudents)
                {
                    var subjectsForStudent = new List<SubjectDto>(classSubjectCollection);

                    if (eachStudent.ClassArm?.Classs?.ClassName != CurrentClass.PreNursery &&
                        eachStudent.ClassArm?.Classs?.ClassName != CurrentClass.PlayClass)
                    {
                        var electiveSubject = await subjectStudentAssignmentRepository.WhereAsync(
                            d =>
                                d.StudentId == eachStudent.Id &&
                                d.Subject!.TimeStatus == TimeStatus.Current,
                            ct,
                            d => d.Subject!);

                        if (electiveSubject.Any())
                        {
                            subjectsForStudent.AddRange(electiveSubject.Select(d => MapSubject(d.Subject!)));
                        }
                    }

                    foreach (var eachSubject in subjectsForStudent)
                    {
                        var teacher = await classSubjectArmAssignmentRepository.FindAsync(
                            r => r.ArmId == eachStudent.ClassArmId && r.SubjectId == eachSubject.Id,
                            ct);

                        if (teacher == null)
                        {
                            return "'" + eachSubject.SubjectName + "' has not been assigned to a teacher in " +
                                   (eachArmStudents.First().ClassArm?.Classs?.ClassLabel ?? string.Empty) +
                                   (eachArmStudents.First().ClassArm?.ArmName.ToString() ?? string.Empty) + ".";
                        }

                        var eachStudentAssessment = await studentAssessmentRepository.FindAsync(
                            n =>
                                n.SubjectId == eachSubject.Id &&
                                n.StudentId == eachStudent.Id &&
                                n.Employee!.SchoolId == schoolId &&
                                n.YearlyAndTermlyStatus!.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                            ct);

                        if (eachStudentAssessment != null)
                        {
                            eachStudentAssessment.EmployeeId = teacher.EmployeeId;
                            studentAssessmentRepository.Update(eachStudentAssessment);
                        }
                        else
                        {
                            studentAssessmentRepository.Add(new StudenXtAccessment
                            {
                                Id = Guid.NewGuid().ToString(),
                                EmployeeId = teacher.EmployeeId,
                                SubjectId = eachSubject.Id,
                                StudentId = eachStudent.Id,
                                ClassArmId = eachStudent.ClassArmId,
                                LastUpdateDate = DateTime.Now,
                                YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
                                Ca1 = 0,
                                Ca2 = 0,
                                Ca3 = 0,
                                Exams = 0
                            });
                        }
                    }
                }
            }

            return "Succeed";
        }

        private void AddStudentAcademicProgressHistory(
            IEnumerable<TxStudentDetail> allStudents,
            IGenericRepository<TxStudentDetail> studentDetailRepository,
            string yearlyAndTermlyAssessmentStatusId)
        {
            var studentAcademicProgressHistoryRepository = _uow.Repository<StudentAcademicTxProgressHistory>();

            foreach (var eachStudent in allStudents)
            {
                if (eachStudent.Status == StudentStatus.Fresher)
                {
                    eachStudent.Status = StudentStatus.Returning;
                    studentDetailRepository.Update(eachStudent);
                }

                var currentClass = eachStudent.ClassArm?.Classs?.ClassName ?? eachStudent.CurrentClass;
                var classCategory = currentClass is CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3
                    ? eachStudent.ClassCategory
                    : GetClassCategoryStatus(currentClass);

                studentAcademicProgressHistoryRepository.Add(new StudentAcademicTxProgressHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
                    StudentId = eachStudent.Id,
                    ClassId = eachStudent.ClassArm?.ClassId ?? string.Empty,
                    ClassCategory = classCategory
                });
            }
        }

        private static TermStatus NextTermStatus(TermStatus lastTermsName)
        {
            return lastTermsName switch
            {
                TermStatus.First => TermStatus.Second,
                TermStatus.Second => TermStatus.Third,
                _ => TermStatus.First
            };
        }

        private static ClassCategoryStatus GetClassCategoryStatus(CurrentClass className)
        {
            if (className == CurrentClass.PreNursery)
            {
                return ClassCategoryStatus.PreNursery;
            }

            if (className == CurrentClass.Nursery1 || className == CurrentClass.Nursery2)
            {
                return ClassCategoryStatus.Nursery;
            }

            if (className == CurrentClass.Primary1 ||
                className == CurrentClass.Primary2 ||
                className == CurrentClass.Primary3 ||
                className == CurrentClass.Primary4 ||
                className == CurrentClass.Primary5 ||
                className == CurrentClass.Primary6)
            {
                return ClassCategoryStatus.Primary;
            }

            if (className == CurrentClass.Jss1 || className == CurrentClass.Jss2 || className == CurrentClass.Jss3)
            {
                return ClassCategoryStatus.Jss;
            }

            return ClassCategoryStatus.PlayClass;
        }

        private static SubjectDto MapSubject(RxSubject subject)
        {
            return new SubjectDto
            {
                Id = subject.Id,
                SubjectName = subject.SubjectName,
                TimeStatus = subject.TimeStatus,
                SubjectCategoryId = subject.SubjectCategoryId,
                SubjectCategory = subject.SubjectCategory == null
                    ? new SubjectCategoriesDto()
                    : new SubjectCategoriesDto
                    {
                        Id = short.TryParse(subject.SubjectCategory.Id, out var subjectCategoryId) ? subjectCategoryId : (short)0,
                        SubjectStatus = subject.SubjectCategory.SubjectStatus,
                        SubjectStatusLabel = subject.SubjectCategory.SubjectStatusLabel,
                        SchoolId = subject.SubjectCategory.SchoolId
                    }
            };
        }

        private TotalStudentListToPdfDto MapTotalStudentListToPdf(TxStudentDetail student)
        {
            return new TotalStudentListToPdfDto
            {
                IdNo = student.IdNo,
                FullName = student.User?.FullName ?? string.Empty,
                CurrentClass = student.ClassArm?.Classs?.ClassName ?? student.CurrentClass,
                ClassLabel = student.ClassArm?.Classs?.ClassLabel ?? string.Empty,
                Arm = student.ClassArm?.ArmName.ToString() ?? string.Empty,
                SchoolInfo = MapSchoolInfo(student.SchoolInfo)
            };
        }

        private static SchoolInfoDto MapSchoolInfo(SchoolTyInfo? schoolInfo)
        {
            if (schoolInfo == null)
            {
                return new SchoolInfoDto();
            }

            return new SchoolInfoDto
            {
                SsNo = schoolInfo.SsNo,
                SchoolName = schoolInfo.SchoolName,
                SchoolAddress = schoolInfo.SchoolAddress,
                Telephone = schoolInfo.Telephone,
                PoAddress = schoolInfo.PoAddress,
                EmailAddress = schoolInfo.EmailAddress,
                Website = schoolInfo.Website,
                SchoolLogoString = schoolInfo.SchoolLogoString,
                Motto = schoolInfo.Motto,
                SchoolRegistrationOfficerId = schoolInfo.SchoolRegistrationOfficerId,
                SchoolAdminAccountantStatus = schoolInfo.SchoolAdminAccountantStatus,
                AccountingStartTermId = string.IsNullOrWhiteSpace(schoolInfo.AccountingStartTermId)
                    ? null
                    : int.TryParse(schoolInfo.AccountingStartTermId, out var accountingStartTermId)
                        ? accountingStartTermId
                        : null,
                SystemImplementationType = schoolInfo.SystemImplementationType,
                SmsApiKey = schoolInfo.SmsApiKey,
                SmsApiToken = schoolInfo.SmsApiToken,
                MyStatus = schoolInfo.MyStatus,
                FeatureImplementationType = schoolInfo.FeatureImplementationType
            };
        }

       
        public async Task<List<StudentDetailsDto>> GetAllStudents(string schoolId, CancellationToken ct = default)
        {
            var studentRepository = _uow.Repository<TxStudentDetail>();

            var students = await studentRepository.WhereAsync(
                s => s.SchoolInfoId == schoolId, ct,
                s => s.ClassArm.Classs, s => s.User);

            return _mapper.Map<List<StudentDetailsDto>>(students);
        }
        // Method 244: Distribute Student Evenly
        public async Task DistributeStudentEvenly(CancellationToken ct = default)
        {
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var studentRepository = _uow.Repository<TxStudentDetail>();
            var classArmRepository = _uow.Repository<AxClassArm>();

            var students = await studentRepository.WhereAsync(
                s => s.SchoolInfoId == schoolId &&
                     (s.Status == Domain.Enum.StudentStatus.Fresher || s.Status == Domain.Enum.StudentStatus.Returning), ct,
                s => s.ClassArm.Classs);

            var groupedByClass = students.GroupBy(s => s.ClassArm.ClassId);

            foreach (var classGroup in groupedByClass)
            {
                var classId = classGroup.Key;
                var arms = await classArmRepository.WhereAsync(a => a.ClassId == classId, ct);

                if (!arms.Any()) continue;

                var studentsInClass = classGroup.ToList();
                var armsArray = arms.ToArray();
                var studentsPerArm = studentsInClass.Count / armsArray.Length;
                var remainder = studentsInClass.Count % armsArray.Length;

                int studentIndex = 0;
                for (int armIndex = 0; armIndex < armsArray.Length; armIndex++)
                {
                    var arm = armsArray[armIndex];
                    var count = studentsPerArm + (armIndex < remainder ? 1 : 0);

                    for (int i = 0; i < count && studentIndex < studentsInClass.Count; i++)
                    {
                        studentsInClass[studentIndex].ClassArmId = arm.Id;
                        studentRepository.Update(studentsInClass[studentIndex]);
                        studentIndex++;
                    }
                }
            }

            await _uow.SaveChangesAsync(ct);
        }

        public async Task<List<StudentDetailsDto>> GetStudentsByParameter(
           string searchParameter,
           CancellationToken ct = default)
        {
            var studentRepository = _uow.Repository<TxStudentDetail>();
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);

            var students = await studentRepository.WhereAsync(
                s => s.SchoolInfoId == schoolId &&
                     (s.IdNo.Contains(searchParameter) ||
                      s.User.FirstName.Contains(searchParameter) ||
                      s.User.MiddleName.Contains(searchParameter) ||
                      s.User.SurName.Contains(searchParameter)), ct,
                s => s.ClassArm.Classs, s => s.User);

            return _mapper.Map<List<StudentDetailsDto>>(students);
        }


        public async Task<IList<StudentDetailsDto>> GetRecentlySavedStudents(CancellationToken ct = default)
        {
            var studentDetailRepository = _uow.Repository<TxStudentDetail>();
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);

            var students = await studentDetailRepository.WhereAsync(
                t => (t.Status == Domain.Enum.StudentStatus.Fresher || t.Status == Domain.Enum.StudentStatus.Returning) &&
                     t.SchoolInfoId == schoolId, ct,
                s => s.ClassArm.Classs, s => s.User);

            var recentStudents = students
                .OrderByDescending(s => s.DateOfAdmsn)
                .Take(10)
                .ToList();
            return recentStudents
                .OrderBy(s => s.User?.FirstName)
                .Select(s => new StudentDetailsDto
                {
                    Id = s.UserId,
                    SNo = s.SNo,
                    IdNo = s.IdNo,
                    FirstName = s.User?.FirstName ?? string.Empty,
                    MiddleName = s.User?.MiddleName ?? string.Empty,
                    SurName = s.User?.SurName ?? string.Empty,
                    Gender = s.User?.Gender ?? Gender.Male,
                    CurrentClass = s.CurrentClass,
                    ClassArmId = s.ClassArmId,
                    User = new ApplicationUserDto { UserName = s.User?.UserName ?? string.Empty },

                    ClassArm = s.ClassArm == null ? null : new ClassArmDto
                    {
                        Id = s.ClassArm.Id,
                        ArmName = s.ClassArm.ArmName,
                        ClassId = s.ClassArm.ClassId,
                        Classs = s.ClassArm.Classs == null ? null : new ClassDto
                        {
                            Id = s.ClassArm.Classs.Id,
                            ClassName = s.ClassArm.Classs.ClassName,
                            ClassLabel = s.ClassArm.Classs.ClassLabel,
                        },
                    },
                    ClassCategory = s.ClassCategory,
                    Status = s.Status,
                    SchoolInfoId = s.SchoolInfoId,
                })
                .ToList<StudentDetailsDto>();
        }

        // Legacy RegistrationService.AlterStudentStatusToPrevious
        public async Task<bool> AlterStudentStatusToPrevious(string id, CancellationToken ct = default)
        {
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var studentRepository = _uow.Repository<TxStudentDetail>();
            var student = await studentRepository.FindAsync(id, ct);
            // Students from another school are treated as not found
            if (student == null || student.SchoolInfoId != schoolId)
                return false;

            student.Status = StudentStatus.Previous;
            student.DateOfDischarge = DateTime.UtcNow;
            studentRepository.Update(student);

            // Remove the student's current-term assessment records
            var studentAssessmentRepository = _uow.Repository<StudenXtAccessment>();
            var currentAssessments = await studentAssessmentRepository.WhereAsync(
                z => z.StudentId == id &&
                     z.YearlyAndTermlyStatus!.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct);
            foreach (var each in currentAssessments)
            {
                studentAssessmentRepository.Delete(each);
            }

            var school = await _uow.Repository<SchoolTyInfo>().FindAsync(schoolId, ct);
            if (school != null)
            {
                var superUserName = "SuperAccountant" + school.SsNo;
                var schoolAccountant = await _uow.Repository<TxEmployee>().FindAsync(
                    x => x.SchoolId == schoolId &&
                         x.EmployeeStatus == EmployeeStatus.Accountant &&
                         x.User != null &&
                         x.User.UserName == superUserName,
                    ct);

                if (schoolAccountant != null)
                {
                    var currentTermPayment = await _uow.Repository<StudentTyTermlyFeePayment>().FindAsync(
                        x => x.StudentAcademicProgressHistory!.StudentId == id &&
                             x.StudentAcademicProgressHistory.YearlyAndTermlyStatus!.TermlyAccessmentStatus ==
                             TermlyAccessmentStatus.Current,
                        ct);

                    if (currentTermPayment == null)
                    {
                        // Student has not paid the current school fees: drop the current-term progress history
                        var progressHistoryRepository = _uow.Repository<StudentAcademicTxProgressHistory>();
                        var currentSchoolLevel = await progressHistoryRepository.FindAsync(
                            x => x.StudentId == id &&
                                 x.YearlyAndTermlyStatus!.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                            ct);
                        if (currentSchoolLevel != null)
                        {
                            progressHistoryRepository.Delete(currentSchoolLevel);
                        }
                    }
                }
            }

            await _uow.SaveChangesAsync(ct);
            return true;
        }

        // Method 93
        // Legacy RegistrationService.DeleteStudent. Like legacy, nothing is saved here: the caller removes the
        // StudentOrParent role and commits. Returns false when the student has previous-term history.
        public async Task<bool> DeleteStudent(string id, CancellationToken ct = default)
        {
            var studentRepository = _uow.Repository<TxStudentDetail>();
            var studentAcademicProgressRepository = _uow.Repository<StudentAcademicTxProgressHistory>();

            // Not in legacy: students from another school are treated as not found, before anything is staged.
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var student = await studentRepository.FindAsync(s => s.UserId == id, ct, o => o.User!);
            if (student == null || student.SchoolInfoId != schoolId)
                throw new KeyNotFoundException($"Student '{id}' was not found.");

            var previousStatus = await studentAcademicProgressRepository.FindAsync(
                x => x.StudentId == id &&
                     x.YearlyAndTermlyStatus!.TermlyAccessmentStatus == TermlyAccessmentStatus.Previous, ct);

            var academicHistoryStatus = false;
            if (previousStatus == null)
            {
                var subjectStudentAssignmentRepository = _uow.Repository<SubjectStudentTxAssignment>();
                var assignedSubjects = await subjectStudentAssignmentRepository.WhereAsync(
                    c => c.StudentId == id, ct);
                foreach (var each in assignedSubjects)
                {
                    subjectStudentAssignmentRepository.Delete(each);
                }

                var currentStatus = await studentAcademicProgressRepository.FindAsync(
                    x => x.StudentId == id, ct);
                if (currentStatus != null)
                {
                    studentAcademicProgressRepository.Delete(currentStatus);

                    // Delete current assessment
                    var studentAssessmentRepository = _uow.Repository<StudenXtAccessment>();
                    var currentAssessments = await studentAssessmentRepository.WhereAsync(
                        z => z.StudentId == id, ct);
                    foreach (var each in currentAssessments)
                    {
                        studentAssessmentRepository.Delete(each);
                    }
                }

                academicHistoryStatus = true;
            }

            if (!academicHistoryStatus)
            {
                return false;
            }

            var affectiveRepository = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();
            var psychomotorList = await affectiveRepository.WhereAsync(c => c.StudentId == id, ct);
            foreach (var each in psychomotorList)
            {
                affectiveRepository.Delete(each);
            }

            var attendanceRepository = _uow.Repository<StudentsAtteXzndanceAndCommentsReport>();
            var attendance = await attendanceRepository.FindAsync(c => c.StudentId == id, ct);
            if (attendance != null)
            {
                attendanceRepository.Delete(attendance);
            }

            var studentAsaUser = student.User
                ?? throw new KeyNotFoundException($"Student '{id}' was not found.");

            studentRepository.Delete(student);
            _uow.Repository<ApplicationUser, string>().Delete(studentAsaUser);
            return true;
        }


       


       

        public async Task<IEnumerable<StudentDetailsDto>> GetStudentByAdmissionNo(
    string searchParameter,
    CancellationToken ct = default)
        {
            var studentRepository = _uow.Repository<TxStudentDetail>();
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);

            var students = await studentRepository.WhereAsync(
                s => s.SchoolInfoId == schoolId &&
                     s.IdNo.Trim().ToLower().Contains(searchParameter.Trim().ToLower()), ct,
                s => s.ClassArm.Classs, s => s.User);

            return _mapper.Map<IEnumerable<StudentDetailsDto>>(students);
        }



        public async Task<IList<StudentDetailsDto>> GetStudentForAnArm(string armId, CancellationToken ct = default)
        {
            var repo = _uow.Repository<TxStudentDetail>();
            var students = await repo.WhereAsync(
                s => s.ClassArmId == armId, ct,
                s => s.User,
                s => s.ClassArm,
                s => s.ClassArm.Classs);

            return students
                .OrderBy(s => s.User?.FirstName)
                .Select(s => new StudentDetailsDto
                {
                    Id = s.UserId,
                    SNo = s.SNo,
                    IdNo = s.IdNo,
                    FirstName = s.User?.FirstName ?? string.Empty,
                    MiddleName = s.User?.MiddleName ?? string.Empty,
                    SurName = s.User?.SurName ?? string.Empty,
                    Gender = s.User?.Gender ?? Gender.Male,
                    CurrentClass = s.CurrentClass,
                    ClassArmId = s.ClassArmId,
                    User = new ApplicationUserDto { UserName = s.User?.UserName ?? string.Empty },

                    ClassArm = s.ClassArm == null ? null : new ClassArmDto
                    {
                        Id = s.ClassArm.Id,
                        ArmName = s.ClassArm.ArmName,
                        ClassId = s.ClassArm.ClassId,
                        Classs = s.ClassArm.Classs == null ? null : new ClassDto
                        {
                            Id = s.ClassArm.Classs.Id,
                            ClassName = s.ClassArm.Classs.ClassName,
                            ClassLabel = s.ClassArm.Classs.ClassLabel,
                        },
                    },
                    ClassCategory = s.ClassCategory,
                    Passport = s.Passport,
                    Status = s.Status,
                    SchoolInfoId = s.SchoolInfoId,
                })
                .ToList<StudentDetailsDto>();
        }

        public async Task<IList<StudentDetailsDto>> GetStudentsWithoutPassportByClassLabel(
            string classLabel,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(classLabel))
            {
                return new List<StudentDetailsDto>();
            }

            var normalizedLabel = classLabel.Trim();
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var studentRepository = _uow.Repository<TxStudentDetail>();

            var students = await studentRepository.WhereAsync(
                s => s.SchoolInfoId == schoolId &&
                     s.Status != StudentStatus.Previous &&
                     s.ClassArm != null &&
                     s.ClassArm.Classs != null &&
                     s.ClassArm.Classs.ClassLabel == normalizedLabel &&
                     (s.Passport == null || s.Passport.Trim() == string.Empty),
                ct,
                s => s.User!,
                s => s.ClassArm!,
                s => s.ClassArm!.Classs!);

            return students
                .OrderBy(s => s.User?.FirstName)
                .Select(s => new StudentDetailsDto
                {
                    Id = s.UserId,
                    UserId = s.UserId,
                    SNo = s.SNo,
                    IdNo = s.IdNo,
                    FirstName = s.User?.FirstName ?? string.Empty,
                    MiddleName = s.User?.MiddleName ?? string.Empty,
                    SurName = s.User?.SurName ?? string.Empty,
                    Gender = s.User?.Gender ?? Gender.Male,
                    CurrentClass = s.CurrentClass,
                    ClassArmId = s.ClassArmId,
                    Passport = s.Passport,
                    User = new ApplicationUserDto
                    {
                        Id = s.UserId,
                        UserName = s.User?.UserName ?? string.Empty,
                        FirstName = s.User?.FirstName ?? string.Empty,
                        MiddleName = s.User?.MiddleName ?? string.Empty,
                        SurName = s.User?.SurName ?? string.Empty,
                    },
                    ClassArm = s.ClassArm == null ? null : new ClassArmDto
                    {
                        Id = s.ClassArm.Id,
                        ArmName = s.ClassArm.ArmName,
                        ClassId = s.ClassArm.ClassId,
                        Classs = s.ClassArm.Classs == null ? null : new ClassDto
                        {
                            Id = s.ClassArm.Classs.Id,
                            ClassName = s.ClassArm.Classs.ClassName,
                            ClassLabel = s.ClassArm.Classs.ClassLabel,
                        },
                    },
                    ClassCategory = s.ClassCategory,
                    Status = s.Status,
                    SchoolInfoId = s.SchoolInfoId,
                })
                .ToList();
        }


        public async Task<(string TermId, TermStatus TermStatus)?> GetCurrentTermStatusAndId(CancellationToken ct = default)
        {
            var yearlyAndTermlyRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);

            var currentTerm = await yearlyAndTermlyRepository.FindAsync(
                t => t.Session.SchoolInfoId == schoolId &&
                     t.TermlyAccessmentStatus == TermlyAccessmentStatus.Current, ct, t => t.Session);

            if (currentTerm == null)
                return null;

            return (currentTerm.Id, currentTerm.TermStatus);
        }

        public Task<StudentDetailsDto> GetStudentById(string id, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        private static string FormatClassLabelAndArm(AxClassArm? arm)
        {
            if (arm == null)
            {
                return string.Empty;
            }

            return $"{arm.Classs?.ClassLabel ?? string.Empty}{arm.ArmName}";
        }
    }
}
