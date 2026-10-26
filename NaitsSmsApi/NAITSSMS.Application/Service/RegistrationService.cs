using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
namespace NAITSSMS.Application.Service
{
    public partial class RegistrationService : IRegistrationService
    {
        private const string HttpContextItemKey_SchoolRegOfficerId = "__SchoolRegistrationOfficerId";
        private const string HttpContextItemKey_TeacherModel = "__CurrentTeacherModel";
        private const string HttpContextItemKey_SchoolSerialNo = "__SchoolSerialNo";
        private const string HttpContextItemKey_SystemImplementationStatus = "__SystemImplementationStatus";

        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        private readonly ISchoolScopeResolver _schoolScopeResolver;
        private readonly ISessionAndTermService _sessionAndTermService;
        private readonly IClassAndArmService _classAndArmService;
        private readonly ITeachersAndOthersService _teachersAndOthersService;

        public RegistrationService(
            IUnitOfWork uow,
            IMapper mapper,
            IHttpContextAccessor httpContextAccessor,
            ISchoolScopeResolver schoolScopeResolver,
            ISessionAndTermService sessionAndTermService,
            IClassAndArmService classAndArmService,
            ITeachersAndOthersService teachersAndOthersService)
        {
            _uow = uow;
            _mapper = mapper;
            _schoolScopeResolver = schoolScopeResolver ?? throw new ArgumentNullException(nameof(schoolScopeResolver));
            _sessionAndTermService = sessionAndTermService ?? throw new ArgumentNullException(nameof(sessionAndTermService));
            _classAndArmService = classAndArmService ?? throw new ArgumentNullException(nameof(classAndArmService));
            _teachersAndOthersService = teachersAndOthersService ?? throw new ArgumentNullException(nameof(teachersAndOthersService));
        }

        public async Task<SchoolInfoDto> GetSystemInfoForSms(string userIdOrSchoolId, CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userIdOrSchoolId, ct);
            var repo = _uow.Repository<SchoolTyInfo, string>();
            var schoolInfo = await repo.FindAsync(c => c.SchoolRegistrationOfficerId == schoolId, ct, s => s.SchoolRegistrationOfficer.User);
            if (schoolInfo == null)
            {
                throw new InvalidOperationException("School registration profile was not found for the current user.");
            }

            var model = _mapper.Map<SchoolInfoDto>(schoolInfo);
            if (model == null)
            {
                throw new InvalidOperationException("Unable to map school registration profile for SMS.");
            }

            var token = model.SmsApiToken;
            try
            {
                if (!string.IsNullOrWhiteSpace(model.SmsApiToken))
                {
                    token = FreePlugIn.Decrypt(model.SmsApiToken, true);
                }
            }
            catch
            {
                // leave token as is
            }
            model.SmsApiToken = token;
            model.SmsApiKey = FreePlugIn.ResolveDisplayedSmsApiKey(model.SmsApiKey);

            if (schoolInfo.SchoolRegistrationOfficer?.User != null)
            {
                model.SchoolRegistrationOfficer ??= new SchoolRegistrationOfficerDto();
                model.SchoolRegistrationOfficer.User = new UserDto
                {
                    Id = schoolInfo.SchoolRegistrationOfficer.User.Id,
                };
            }

            return model;
        }

        /// <summary>
        /// Legacy StudentsAutomaticPromotion: school-wide promote-all (no pass-mark gate),
        /// close current session/term, and open the next academic session.
        /// </summary>
        public async Task StudentsAutomaticPromotion(string userId, CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var studentRepository = _uow.Repository<TxStudentDetail>();
            var armRepository = _uow.Repository<AxClassArm>();
            var classRepository = _uow.Repository<RxClasss>();
            var academicSessionRepository = _uow.Repository<TxAcademicSession>();
            var yearlyAndTermlyStatusRepository = _uow.Repository<TxYearlyAndTermlyStatus>();

            var studentList = (await studentRepository.WhereAsync(
                b => b.SchoolInfoId == schoolId && b.Status != StudentStatus.Previous,
                ct,
                b => b.ClassArm!.Classs!)).ToList();

            foreach (var eachStudent in studentList)
            {
                if (eachStudent.ClassArm?.Classs == null)
                {
                    continue;
                }

                if (eachStudent.ClassArm.Classs.ClassName == CurrentClass.Ss3)
                {
                    await MarkStudentPreviousAndClearUnscoredCurrentTermAssessmentsAsync(
                        eachStudent,
                        studentRepository,
                        ct);
                    continue;
                }

                var nextClass = NextEnum(eachStudent.ClassArm.Classs.ClassName);
                var currentStudentArm = eachStudent.ClassArm.ArmName;

                var nextClassArm = await armRepository.FindAsync(
                    a => a.ArmName == currentStudentArm
                         && a.Classs != null
                         && a.Classs.ClassName == nextClass
                         && a.Classs.SchoolInfoId == schoolId,
                    ct,
                    a => a.Classs!);

                var execusionPoint = 0;
                if (execusionPoint == 0 && nextClassArm == null)
                {
                    var schoolClasses = await classRepository.WhereAsync(c => c.SchoolInfoId == schoolId, ct);
                    if (schoolClasses.Count == 0)
                    {
                        throw new InvalidOperationException("No classes were found for this school.");
                    }

                    var highestClass = schoolClasses
                        .OrderBy(v => v.ClassName)
                        .Last()
                        .ClassName;

                    // Handle the case where classes are not serial (legacy while-loop).
                    while (execusionPoint == 0 && nextClassArm == null)
                    {
                        if (nextClass >= highestClass)
                        {
                            await MarkStudentPreviousAndClearUnscoredCurrentTermAssessmentsAsync(
                                eachStudent,
                                studentRepository,
                                ct);
                            execusionPoint = 1;
                        }
                        else
                        {
                            nextClassArm = await armRepository.FindAsync(
                                a => a.ArmName == currentStudentArm
                                     && a.Classs != null
                                     && a.Classs.ClassName == nextClass
                                     && a.Classs.SchoolInfoId == schoolId,
                                ct,
                                a => a.Classs!);

                            if (nextClassArm == null)
                            {
                                nextClass = NextEnum(nextClass);
                            }
                        }
                    }
                }

                if (execusionPoint == 1) // Signifies the final class
                {
                    continue;
                }

                if (nextClassArm == null)
                {
                    continue;
                }

                eachStudent.CurrentClass = nextClass;
                if (nextClass == CurrentClass.Ss1)
                {
                    eachStudent.ClassCategory = ClassCategoryStatus.Art;
                }

                eachStudent.ClassArmId = nextClassArm.Id;
                studentRepository.Update(eachStudent);
            }

            var currentSession = await academicSessionRepository.FindAsync(
                r => r.Status == TimeStatus.Current && r.SchoolInfoId == schoolId, ct);
            if (currentSession == null)
            {
                throw new InvalidOperationException("No current academic session was found for this school.");
            }

            currentSession.Status = TimeStatus.Previous;

            var currentTermStatus = await yearlyAndTermlyStatusRepository.FindAsync(
                y => y.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                     && y.Session!.SchoolInfoId == schoolId,
                ct,
                y => y.Session!);
            if (currentTermStatus == null)
            {
                throw new InvalidOperationException("No current term was found for this school.");
            }

            currentTermStatus.TermlyAccessmentStatus = TermlyAccessmentStatus.Previous;

            academicSessionRepository.Update(currentSession);
            yearlyAndTermlyStatusRepository.Update(currentTermStatus);

            var sessionParts = currentSession.Session.Split('/');
            var sessionFirstComonent = int.Parse(sessionParts[0].Trim()) + 1;
            var sessionSecondComonent = int.Parse(sessionParts[1].Trim()) + 1;
            var newSessionLabel = sessionFirstComonent + "/" + sessionSecondComonent;

            academicSessionRepository.Add(new TxAcademicSession
            {
                Id = Guid.NewGuid().ToString(),
                Session = newSessionLabel,
                Status = TimeStatus.Current,
                SchoolInfoId = currentSession.SchoolInfoId
            });

            await _uow.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Marks a student Previous (alumni) and drops empty current-term assessment shells.
        /// Scored rows remain for historical results; create paths must not target Previous students.
        /// </summary>
        private async Task MarkStudentPreviousAndClearUnscoredCurrentTermAssessmentsAsync(
            TxStudentDetail student,
            IGenericRepository<TxStudentDetail> studentRepository,
            CancellationToken ct)
        {
            student.Status = StudentStatus.Previous;
            studentRepository.Update(student);

            var studentId = string.IsNullOrWhiteSpace(student.UserId) ? student.Id : student.UserId;
            if (string.IsNullOrWhiteSpace(studentId))
            {
                return;
            }

            var assessmentRepository = _uow.Repository<StudenXtAccessment>();
            var currentTermRows = await assessmentRepository.WhereAsync(
                a =>
                    a.StudentId == studentId &&
                    a.YearlyAndTermlyStatus != null &&
                    a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                a => a.YearlyAndTermlyStatus!);

            foreach (var row in currentTermRows)
            {
                if (Math.Abs(row.Ca1 + row.Ca2 + row.Ca3 + row.Exams) < 0.5f)
                {
                    await assessmentRepository.DeleteAsync(row.Id, ct);
                }
            }
        }

        private static T NextEnum<T>(T src) where T : struct, Enum
        {
            var arr = (T[])Enum.GetValues(src.GetType());
            var j = Array.IndexOf(arr, src) + 1;
            return arr.Length == j ? arr[0] : arr[j];
        }

        /// <summary>
        /// Legacy ConfirmStudentsPromotion: pass-mark gated school-wide promotion,
        /// then close current session/term and open the next academic session.
        /// </summary>
        public async Task<string> ConfirmStudentsPromotion(string userId, CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var registrationOfficerValidationResult = await StudentsPromotionValidations(userId, ct);
            if (registrationOfficerValidationResult != "Success")
            {
                return "Failed";
            }

            var studentRepository = _uow.Repository<TxStudentDetail>();
            var studentAccessmentRepository = _uow.Repository<StudenXtAccessment>();
            var yearlyAndTermlyStatusRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
            var armRepository = _uow.Repository<AxClassArm>();
            var classRepository = _uow.Repository<RxClasss>();
            var academicSessionRepository = _uow.Repository<TxAcademicSession>();

            var allStudents = (await studentRepository.WhereAsync(
                s => (s.Status == StudentStatus.Fresher || s.Status == StudentStatus.Returning)
                     && s.SchoolInfoId == schoolId,
                ct,
                a => a.ClassArm!.Classs!)).ToList();

            var currentTermRow = await yearlyAndTermlyStatusRepository.FindAsync(
                n => n.Session!.SchoolInfoId == schoolId
                     && n.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                n => n.Session!);
            if (currentTermRow == null || string.IsNullOrWhiteSpace(currentTermRow.SessionId))
            {
                return "Failed";
            }

            var currentSessionId = currentTermRow.SessionId;

            // Pass Mark Calculations (legacy)
            var passMarkList = (await yearlyAndTermlyStatusRepository.WhereAsync(
                n => n.SessionId == currentSessionId, ct))
                .Select(z => z.MinPassMark)
                .ToList();

            float totalPassMark = 0;
            float termCounter = 0;
            foreach (var item in passMarkList)
            {
                var mark = item == 0 ? 0f : item ?? 40f;
                totalPassMark += mark;
                termCounter += 1;
            }

            if (termCounter == 0)
            {
                return "Failed";
            }

            var averagePassMark = totalPassMark / termCounter;

            var sessionAssessments = await studentAccessmentRepository.WhereAsync(
                s => s.YearlyAndTermlyStatus != null
                     && s.YearlyAndTermlyStatus.SessionId == currentSessionId,
                ct,
                s => s.YearlyAndTermlyStatus!);

            var assessmentsByStudent = sessionAssessments
                .GroupBy(s => s.StudentId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var eachStudent in allStudents)
            {
                if (eachStudent.ClassArm?.Classs == null)
                {
                    continue;
                }

                if (!assessmentsByStudent.TryGetValue(eachStudent.Id, out var currentStudentAssessment)
                    || currentStudentAssessment.Count == 0)
                {
                    // Legacy Average() on empty throws; skip is the safe cloud equivalent of "cannot promote".
                    continue;
                }

                var studentSessionAverageScore =
                    currentStudentAssessment.Average(t => t.Ca1 + t.Ca2 + t.Ca3 + t.Exams);
                if (studentSessionAverageScore < averagePassMark)
                {
                    continue;
                }

                if (eachStudent.ClassArm.Classs.ClassName == CurrentClass.Ss3)
                {
                    await MarkStudentPreviousAndClearUnscoredCurrentTermAssessmentsAsync(
                        eachStudent,
                        studentRepository,
                        ct);
                    continue;
                }

                var nextClass = NextEnum(eachStudent.ClassArm.Classs.ClassName);
                var currentStudentArm = eachStudent.ClassArm.ArmName;

                var nextClassArm = await armRepository.FindAsync(
                    a => a.ArmName == currentStudentArm
                         && a.Classs != null
                         && a.Classs.ClassName == nextClass
                         && a.Classs.SchoolInfoId == schoolId,
                    ct,
                    a => a.Classs!);

                var execusionPoint = 0;
                if (execusionPoint == 0 && nextClassArm == null)
                {
                    var schoolClasses = await classRepository.WhereAsync(c => c.SchoolInfoId == schoolId, ct);
                    if (schoolClasses.Count == 0)
                    {
                        return "Failed";
                    }

                    var highestClass = schoolClasses
                        .OrderBy(v => v.ClassName)
                        .Last()
                        .ClassName;

                    while (execusionPoint == 0 && nextClassArm == null)
                    {
                        if (nextClass >= highestClass)
                        {
                            await MarkStudentPreviousAndClearUnscoredCurrentTermAssessmentsAsync(
                                eachStudent,
                                studentRepository,
                                ct);
                            execusionPoint = 1;
                        }
                        else
                        {
                            nextClassArm = await armRepository.FindAsync(
                                a => a.ArmName == currentStudentArm
                                     && a.Classs != null
                                     && a.Classs.ClassName == nextClass
                                     && a.Classs.SchoolInfoId == schoolId,
                                ct,
                                a => a.Classs!);

                            if (nextClassArm == null)
                            {
                                nextClass = NextEnum(nextClass);
                            }
                        }
                    }
                }

                if (execusionPoint == 1) // Signifies the final class
                {
                    continue;
                }

                if (nextClassArm == null)
                {
                    continue;
                }

                eachStudent.CurrentClass = nextClass;
                if (nextClass == CurrentClass.Ss1)
                {
                    // Legacy ConfirmStudentsPromotion uses Science (Automatic uses Art).
                    eachStudent.ClassCategory = ClassCategoryStatus.Science;
                }

                eachStudent.ClassArmId = nextClassArm.Id;
                studentRepository.Update(eachStudent);
            }

            var currentSession = await academicSessionRepository.FindAsync(
                r => r.Status == TimeStatus.Current && r.SchoolInfoId == schoolId, ct);
            if (currentSession == null)
            {
                return "Failed";
            }

            currentSession.Status = TimeStatus.Previous;

            var currentTermStatus = await yearlyAndTermlyStatusRepository.FindAsync(
                y => y.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                     && y.Session!.SchoolInfoId == schoolId,
                ct,
                y => y.Session!);
            if (currentTermStatus == null)
            {
                return "Failed";
            }

            currentTermStatus.TermlyAccessmentStatus = TermlyAccessmentStatus.Previous;

            academicSessionRepository.Update(currentSession);
            yearlyAndTermlyStatusRepository.Update(currentTermStatus);

            var sessionParts = currentSession.Session.Split('/');
            var sessionFirstComonent = int.Parse(sessionParts[0].Trim()) + 1;
            var sessionSecondComonent = int.Parse(sessionParts[1].Trim()) + 1;
            var newSession = sessionFirstComonent + "/" + sessionSecondComonent;

            academicSessionRepository.Add(new TxAcademicSession
            {
                Id = Guid.NewGuid().ToString(),
                Session = newSession,
                Status = TimeStatus.Current,
                SchoolInfoId = currentSession.SchoolInfoId
            });

            await _uow.SaveChangesAsync(ct);
            return "Succeeded";
        }

        private async Task<string> CheckFormTeacherAssessmentCompletion(string schoolId, CancellationToken ct)
        {
            var teacherClassArmAssignmentRepository = _uow.Repository<TeacherClassArmTxAssignment>();
            var incomplete = await teacherClassArmAssignmentRepository.WhereAsync(
                t => t.Employee != null
                     && t.Employee.SchoolId == schoolId
                     && t.ArmTermlyAssessmentStatus == ArmTermlyAssessmentStatus.Uncompleted,
                ct,
                t => t.Employee!);

            if (incomplete.Count > 0)
            {
                return "Failed. One or more teacher has not completed their student assessment for this term.";
            }

            return "Success";
        }
        public async Task<SchoolSummaryDto?> GetSchoolSummaryForRegistrarDashboard(string userId, CancellationToken ct = default)
        {
            // STEP 1: Resolve school registration officer scope (legacy GetSchoolIdForSchoolRegistrationOfficer)
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);

            // STEP 2–3: Count current teachers without materializing entities
            var teacherRepository = _uow.Repository<TxEmployee>();
            var teachers = await teacherRepository.CountAsync(
                t => t.SchoolId == schoolId && t.TimeStatus == TimeStatus.Current,
                ct);

            // STEP 4–7: Project only class label/name for KPI breakdown (avoids full student graphs)
            var studentDetailRepository = _uow.Repository<TxStudentDetail>();
            var studentClassRows = await studentDetailRepository.SelectWhereAsync(
                t => t.SchoolInfoId == schoolId &&
                     (t.Status == Domain.Enum.StudentStatus.Fresher || t.Status == Domain.Enum.StudentStatus.Returning) &&
                     t.ClassArm != null &&
                     t.ClassArm.Classs != null,
                t => new
                {
                    ClassName = t.ClassArm!.Classs!.ClassName,
                    ClassLabel = t.ClassArm.Classs.ClassLabel
                },
                ct);

            var students = studentClassRows.Count;
            var classAndStudentInaClass = studentClassRows
                .GroupBy(r => new { r.ClassName, r.ClassLabel })
                .OrderBy(g => g.Key.ClassName)
                .Select(g => new DashboardStudentsNumberAnalysisDto
                {
                    ClassLabel = g.Key.ClassLabel,
                    NumberOfStudents = (short)g.Count()
                })
                .ToList();

            // STEP 8–9: Find current term
            var yearlyAndTermlyAssessmentStatusRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
            var yearlyAndTermlyAssessmentStatus = await yearlyAndTermlyAssessmentStatusRepository.FindAsync(
                f => f.Session != null &&
                     f.Session.SchoolInfoId == schoolId &&
                     f.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                s => s.Session!);

            if (yearlyAndTermlyAssessmentStatus?.Session == null)
            {
                return null;
            }

            var session = yearlyAndTermlyAssessmentStatus.Session.Session;
            var currentTerm = yearlyAndTermlyAssessmentStatus.TermStatus;

            // STEP 14–15: Project slim assessment rows for top-3 averages (no Subject/Employee graphs)
            var studentAccessmentRepository = _uow.Repository<StudenXtAccessment>();
            List<(string StudentId, string ClassId, CurrentClass ClassName, string ClassLabel, ArmName ArmName, string ClassArmId, string IdNo, string? Passport, string FirstName, string MiddleName, string SurName, float Total, int Count)> assessmentRows;

            if (currentTerm == TermStatus.Third)
            {
                var projected = await studentAccessmentRepository.SelectWhereAsync(
                    s => s.YearlyAndTermlyStatus != null &&
                         s.YearlyAndTermlyStatus.SessionId == yearlyAndTermlyAssessmentStatus.SessionId &&
                         s.ClassArm != null &&
                         s.ClassArm.Classs != null &&
                         s.Student != null &&
                         s.Student.User != null,
                    s => new
                    {
                        s.StudentId,
                        ClassId = s.ClassArm!.ClassId,
                        ClassName = s.ClassArm.Classs!.ClassName,
                        ClassLabel = s.ClassArm.Classs.ClassLabel,
                        ArmName = s.ClassArm.ArmName,
                        ClassArmId = s.ClassArmId,
                        IdNo = s.Student!.IdNo,
                        Passport = s.Student.Passport,
                        FirstName = s.Student.User!.FirstName,
                        MiddleName = s.Student.User.MiddleName,
                        SurName = s.Student.User.SurName,
                        Score = s.Ca1 + s.Ca2 + s.Ca3 + s.Exams
                    },
                    ct);
                assessmentRows = projected
                    .GroupBy(r => new { r.StudentId, r.ClassId, r.ClassName, r.ClassLabel, r.ArmName, r.ClassArmId, r.IdNo, r.Passport, r.FirstName, r.MiddleName, r.SurName })
                    .Select(g => (
                        g.Key.StudentId,
                        g.Key.ClassId,
                        g.Key.ClassName,
                        g.Key.ClassLabel,
                        g.Key.ArmName,
                        g.Key.ClassArmId,
                        g.Key.IdNo,
                        g.Key.Passport,
                        g.Key.FirstName,
                        g.Key.MiddleName,
                        g.Key.SurName,
                        g.Sum(x => x.Score),
                        g.Count()))
                    .ToList();
            }
            else
            {
                var projected = await studentAccessmentRepository.SelectWhereAsync(
                    s => s.YearlyAndTermlyStatusId == yearlyAndTermlyAssessmentStatus.Id &&
                         s.ClassArm != null &&
                         s.ClassArm.Classs != null &&
                         s.Student != null &&
                         s.Student.User != null,
                    s => new
                    {
                        s.StudentId,
                        ClassId = s.ClassArm!.ClassId,
                        ClassName = s.ClassArm.Classs!.ClassName,
                        ClassLabel = s.ClassArm.Classs.ClassLabel,
                        ArmName = s.ClassArm.ArmName,
                        ClassArmId = s.ClassArmId,
                        IdNo = s.Student!.IdNo,
                        Passport = s.Student.Passport,
                        FirstName = s.Student.User!.FirstName,
                        MiddleName = s.Student.User.MiddleName,
                        SurName = s.Student.User.SurName,
                        Score = s.Ca1 + s.Ca2 + s.Ca3 + s.Exams
                    },
                    ct);
                assessmentRows = projected
                    .GroupBy(r => new { r.StudentId, r.ClassId, r.ClassName, r.ClassLabel, r.ArmName, r.ClassArmId, r.IdNo, r.Passport, r.FirstName, r.MiddleName, r.SurName })
                    .Select(g => (
                        g.Key.StudentId,
                        g.Key.ClassId,
                        g.Key.ClassName,
                        g.Key.ClassLabel,
                        g.Key.ArmName,
                        g.Key.ClassArmId,
                        g.Key.IdNo,
                        g.Key.Passport,
                        g.Key.FirstName,
                        g.Key.MiddleName,
                        g.Key.SurName,
                        g.Sum(x => x.Score),
                        g.Count()))
                    .ToList();
            }

            var bestStudents = new List<StudentsAccessmentDto>();
            if (assessmentRows.Count > 0)
            {
                foreach (var classGroup in assessmentRows.GroupBy(r => r.ClassId).OrderBy(g => g.First().ClassName))
                {
                    var topThree = classGroup
                        .OrderByDescending(r => r.Count == 0 ? 0 : r.Total / r.Count)
                        .Take(3);

                    foreach (var row in topThree)
                    {
                        var average = row.Count == 0 ? 0f : row.Total / row.Count;
                        bestStudents.Add(new StudentsAccessmentDto
                        {
                            StudentId = row.StudentId,
                            ClassArmId = row.ClassArmId,
                            Ca1 = average,
                            Student = new StudentDetailsDto
                            {
                                Id = row.StudentId,
                                IdNo = row.IdNo,
                                Passport = row.Passport ?? string.Empty,
                                FirstName = row.FirstName ?? string.Empty,
                                MiddleName = row.MiddleName ?? string.Empty,
                                SurName = row.SurName ?? string.Empty,
                                ClassArmId = row.ClassArmId,
                            },
                            ClassArm = new ClassArmDto
                            {
                                Id = row.ClassArmId,
                                ClassId = row.ClassId,
                                ArmName = row.ArmName,
                                Classs = new ClassDto
                                {
                                    Id = row.ClassId,
                                    ClassName = row.ClassName,
                                    ClassLabel = row.ClassLabel,
                                }
                            }
                        });
                    }
                }
            }

            return new SchoolSummaryDto
            {
                Session = session,
                Term = currentTerm.ToString(),
                TotalStudents = students,
                TotalTeachers = teachers,
                StudentsAccessment = bestStudents,
                DashboardStudentsNumberAnalysis = classAndStudentInaClass
            };
        }


        public async Task<bool> ChangeStudentPassword(StudentPasswordModelDto model, CancellationToken ct = default)
        {
            var studentRepository = _uow.Repository<TxStudentDetail>();
            var student = await studentRepository.FindAsync(s => s.UserId == model.UserId, ct, s => s.User!);

            if (student?.User == null)
                return false;

            var userRepository = _uow.Repository<ApplicationUser, string>();
            var user = student.User;

            if (!string.IsNullOrEmpty(model.Password))
            {
                var hasher = new PasswordHasher<ApplicationUser>();
                user.PasswordHash = hasher.HashPassword(user, model.Password);
                userRepository.Update(user);
                await _uow.SaveChangesAsync(ct);
                return true;
            }

            return false;
        }


        public async Task AutoPromotion(CancellationToken ct = default)
        {
            // STEP 1: Get student repository (Legacy: ServiceLocator.Get<IGenericRepository<TxStudentDetails>>())
            var studentRepository = _uow.Repository<TxStudentDetail>();

            // STEP 2: Commented-out lines from legacy (preserved for reference)
            // var employeeModel = CurrentTeacherModel();
            // var schoolId = CurrentTeacherModel()

            // STEP 3: Get class arm repository (Legacy: ServiceLocator.Get<IGenericRepository<AxClassArm, int>>())
            var classArmRepository = _uow.Repository<AxClassArm>();

            // STEP 4: Find SS1 arm ID (Legacy: classArmRepository.Find(a => a.Classs.ClassName == CurrentClass.Ss1, a => a.Classs).Id)
            var ss1Arm = await classArmRepository.FindAsync(
                a => a.Classs != null && a.Classs.ClassName == CurrentClass.Ss1, ct, a => a.Classs!);
            if (ss1Arm?.Classs == null)
            {
                return;
            }

            var ss1ArmId = ss1Arm.Id;

            // STEP 5: Get all JSS3 students (Legacy: studentRepository.Where(b => b.ClassArm.Classs.ClassName == CurrentClass.Jss3, b => b.ClassArm.Classs))
            var collections = await studentRepository.WhereAsync(
                b => b.ClassArm != null &&
                     b.ClassArm.Classs != null &&
                     b.ClassArm.Classs.ClassName == CurrentClass.Jss3, ct,
                b => b.ClassArm!.Classs!);

            // STEP 6: Update each student (Legacy: foreach loop with 3 property updates)
            foreach (var each in collections.Where(student => student.ClassArm?.Classs != null))
            {
                each.ClassArmId = ss1ArmId;                      // Legacy line 1
                each.CurrentClass = CurrentClass.Ss1;            // Legacy line 2
                each.ClassCategory = ClassCategoryStatus.Art;   // Legacy line 3
                studentRepository.Update(each);                  // Legacy line 4
            }

            // STEP 7: Commit changes (Legacy: UnitOfWork.Commit())
            await _uow.SaveChangesAsync(ct);
        }


        public async Task<StudentAssessmentEnvironmentPreparationReturnDto> DoSystemInitialPreparationForStudentAccessment(
            string userId, TermStatus term,
            CancellationToken ct = default)
        {
            // Legacy: GetSchoolIdForSchoolRegistrationOfficer()
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);

            // OPTIMIZATION 2: Batch repository access - get all repos upfront
            var principalRepository = _uow.Repository<SchoolHeadTx>();
            var studentDetailRepository = _uow.Repository<TxStudentDetail>();
            var yearlyAndTermlyAssessmentStatusRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
            var studentAssessmentRepository = _uow.Repository<StudenXtAccessment>();
            var academicSessionRepository = _uow.Repository<TxAcademicSession>();

            // OPTIMIZATION 3: Early validation - check principal registration first
            var hasPrincipalRegistered = await principalRepository.FindAsync(
                r => r.Status == TimeStatus.Current && r.SchoolInfoId == schoolId, ct);

            if (hasPrincipalRegistered == null)
                return new StudentAssessmentEnvironmentPreparationReturnDto
                {
                    StatusCode = "Pricipal needs to be registered first."
                };

            // EF Core DbContext is not thread-safe — fetch sequentially through the shared unit of work.
            var studentRows = await studentDetailRepository.WhereAsync(
                t => (t.Status == Domain.Enum.StudentStatus.Fresher || t.Status == Domain.Enum.StudentStatus.Returning) &&
                     t.SchoolInfoId == schoolId, ct,
                r => r.ClassArm!.Classs!, g => g.SchoolInfo!, s => s.User!);

            var assessmentHistory = await studentAssessmentRepository.WhereAsync(
                a => a.Exams > 40 && a.Employee!.SchoolId == schoolId, ct);

            var checkForAcademicSessionInitialisation = await academicSessionRepository.FindAsync(
                i => i.Status == TimeStatus.Current && i.SchoolInfoId == schoolId, ct);

            var allStudents = studentRows
                .Where(student =>
                    student.ClassArm?.Classs != null &&
                    student.SchoolInfo != null &&
                    student.User != null)
                .ToList();
            var haveTheyEverDoneStudentsAssessment = assessmentHistory.FirstOrDefault();

            // OPTIMIZATION 6: Early exit validation
            if (checkForAcademicSessionInitialisation == null)
            {
                return new StudentAssessmentEnvironmentPreparationReturnDto
                {
                    StatusCode = "Failed! You need to first set the 'Academic Session' before you can continue."
                };
            }

            var academicSessionId = checkForAcademicSessionInitialisation.Id;

            // OPTIMIZATION 7: Check if first-time assessment setup is needed
            if (haveTheyEverDoneStudentsAssessment == null)
            {
                var recentEnvPreparation = await yearlyAndTermlyAssessmentStatusRepository.WhereAsync(
                    s => s.Session != null && s.Session.SchoolInfoId == schoolId, ct);
                var checkRecentEnviromentPreparation = recentEnvPreparation.FirstOrDefault();

                if (checkRecentEnviromentPreparation != null)
                {
                    // OPTIMIZATION 8: Update existing term in one operation
                    checkRecentEnviromentPreparation.CreatedDate = DateTime.UtcNow;
                    checkRecentEnviromentPreparation.TermStatus = term;
                    checkRecentEnviromentPreparation.TermlyAccessmentStatus = TermlyAccessmentStatus.Current;
                    checkRecentEnviromentPreparation.SessionId = academicSessionId;
                    yearlyAndTermlyAssessmentStatusRepository.Update(checkRecentEnviromentPreparation);

                    var termId = checkRecentEnviromentPreparation.Id;
                    var isUpdate = true;
                    // OPTIMIZATION 9: Process students by class - use local function for DRY
                    var result = await ProcessStudentsByClass(
                        allStudents, termId, userId, studentAssessmentRepository, schoolId,
                        isUpdate, ct);

                    if (result != "Succeed")
                        return new StudentAssessmentEnvironmentPreparationReturnDto { StatusCode = result };
                }
                else
                {
                    // OPTIMIZATION 10: Create new term and process students
                    var assessmentDetails = new TxYearlyAndTermlyStatus
                    {
                        Id = Guid.NewGuid().ToString(),
                        CreatedDate = DateTime.UtcNow,
                        TermStatus = term,
                        TermlyAccessmentStatus = TermlyAccessmentStatus.Current,
                        SessionId = academicSessionId
                    };
                    yearlyAndTermlyAssessmentStatusRepository.Add(assessmentDetails);
                    await _uow.SaveChangesAsync(ct);

                    var yearlyAndTermlyStatus = await yearlyAndTermlyAssessmentStatusRepository.FindAsync(
                        r => r.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                             r.Session != null &&
                             r.Session.SchoolInfoId == schoolId, ct);
                    if (yearlyAndTermlyStatus == null)
                    {
                        return new StudentAssessmentEnvironmentPreparationReturnDto
                        {
                            StatusCode = "Failed! Unable to resolve the current assessment environment."
                        };
                    }

                    var result = await ProcessStudentsByClass(
                        allStudents, yearlyAndTermlyStatus.Id, userId, studentAssessmentRepository, schoolId,
                        isUpdate: false, ct);

                    if (result != "Succeed")
                        return new StudentAssessmentEnvironmentPreparationReturnDto { StatusCode = result };
                }

                await _uow.SaveChangesAsync(ct);

                return new StudentAssessmentEnvironmentPreparationReturnDto
                {
                    StatusCode = "Succeed",
                    TotalStudentListToPdf = allStudents.Select(r => new TotalStudentListToPdfDto
                    {
                        IdNo = r.IdNo,
                        FullName = r.User!.FullName,
                        CurrentClass = r.ClassArm!.Classs!.ClassName,
                        ClassLabel = r.ClassArm.Classs.ClassLabel,
                        Arm = r.ClassArm.ArmName.ToString(),
                        SchoolInfo = _mapper.Map<SchoolInfoDto>(r.SchoolInfo!)
                    }).ToList()
                };
            }

            return new StudentAssessmentEnvironmentPreparationReturnDto
            {
                StatusCode = "No need for Initial Preparation Since they have ever done students' result processing. <br /> Please use the 'New Assessment Handler' button at the left bottom hand side."
            };

            // OPTIMIZATION 12: Local async helper to eliminate code duplication
            async Task<string> ProcessStudentsByClass(
                List<TxStudentDetail> students,
                string termId,
                string userId,
                IGenericRepository<StudenXtAccessment> assessmentRepo,
                string schoolIdParam,
                bool isUpdate,
                CancellationToken cancellationToken)
            {
                foreach (var classGroup in students
                    .Where(student => student.ClassArm?.Classs != null)
                    .GroupBy(student => student.ClassArm!.ClassId))
                {
                    var firstStudent = classGroup.First();

                    // Check if senior secondary (SS1-SS3)
                    if (firstStudent.CurrentClass is CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3)
                    {
                        foreach (var deptGroup in classGroup.GroupBy(s => s.ClassCategory))
                        {
                            // OPTIMIZATION 13: Switch expression for subject collection
                            var classSubjects = deptGroup.Key switch
                            {
                                ClassCategoryStatus.Science => await SeniorClassesSubjectListCurrent(userId, ClassCategoryStatus.Science, cancellationToken),
                                ClassCategoryStatus.Commercial => await SeniorClassesSubjectListCurrent(userId, ClassCategoryStatus.Commercial, cancellationToken),
                                _ => await SeniorClassesSubjectListCurrent(userId, ClassCategoryStatus.Art, cancellationToken)
                            };

                            var returnMsg = isUpdate
                                ? await ProceedFromHereForUpdateSeniorStudent(
                                    deptGroup,
                                    classSubjects,
                                    termId,
                                    assessmentRepo,
                                    schoolIdParam,
                                    cancellationToken)
                                : await ProceedFromHereForInsert(classGroup, classSubjects, termId, assessmentRepo, schoolIdParam);

                            if (returnMsg != "Succeed")
                                return returnMsg;
                        }
                    }
                    else
                    {
                        // Basic classes (Pre-Nursery to JSS3)
                        var classSubjects = await BasicClassesSubjectListCurrent(
                            userId,
                            firstStudent.ClassArm!.Classs!.ClassName,
                            cancellationToken);

                        var returnMsg = isUpdate
                            ? await ProceedFromHereForUpdateBasicStudents(classGroup, classSubjects, termId, assessmentRepo, schoolIdParam)
                            : await ProceedFromHereForInsert(classGroup, classSubjects, termId, assessmentRepo, schoolIdParam);

                        if (returnMsg != "Succeed")
                            return returnMsg;
                    }
                }

                return "Succeed";
            }
        }
        private async Task<string> ProceedFromHereForUpdateBasicStudents(
          IGrouping<string, TxStudentDetail> eachClass,
          List<SubjectDto> classSubjectCollection,
          string termId,
          IGenericRepository<StudenXtAccessment> studentAssessmentRepository,
          string schoolId,
          CancellationToken ct = default)
        {
            // OPTIMIZATION 1: Get all required repositories upfront (no ServiceLocator anti-pattern)
            var classSubjectArmAssignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var subjectStudentAssignmentRepository = _uow.Repository<SubjectStudentTxAssignment>();

            // OPTIMIZATION 2: Process each arm's students
            foreach (var eachArmStudents in eachClass.GroupBy(g => g.ClassArmId))
            {
                var armStudents = eachArmStudents
                    .Where(student => student.ClassArm?.Classs != null)
                    .ToList();
                if (armStudents.Count == 0)
                {
                    continue;
                }

                var firstArmStudent = armStudents.First();

                // OPTIMIZATION 3: Process each student in this arm
                foreach (var eachStudent in armStudents)
                {
                    // OPTIMIZATION 4: Create a copy of subjects for this student (avoid mutating shared collection)
                    var subjectsForStudent = new List<SubjectDto>(classSubjectCollection);
                    var electiveSubjectDtos = new List<SubjectDto>();

                    // OPTIMIZATION 5: Check if student's class allows elective subjects
                    var className = eachStudent.ClassArm!.Classs!.ClassName;
                    var allowsElectives = className != CurrentClass.PreNursery && className != CurrentClass.PlayClass;

                    if (allowsElectives)
                    {
                        // OPTIMIZATION 6: Fetch elective subjects asynchronously with proper includes
                        var electiveAssignments = await subjectStudentAssignmentRepository.WhereAsync(
                            d => d.StudentDetail != null &&
                                 d.StudentDetail.SchoolInfoId == schoolId &&
                                 d.Subject != null &&
                                 d.Subject.TimeStatus == TimeStatus.Current &&
                                 d.StudentDetail.UserId == eachStudent.UserId,
                            ct,
                            d => d.Subject!,
                            d => d.Subject!.SubjectCategory!);

                        // OPTIMIZATION 7: Extract subjects and map to DTOs only if we have results
                        if (electiveAssignments != null && electiveAssignments.Any())
                        {
                            var electiveSubjects = electiveAssignments
                                .Where(assignment => assignment.Subject != null)
                                .Select(assignment => assignment.Subject!)
                                .ToList();
                            electiveSubjectDtos = electiveSubjects.Select(MapSubject).ToList();
                            subjectsForStudent.AddRange(electiveSubjectDtos);
                        }
                    }

                    // OPTIMIZATION 8: Process each subject for this student
                    foreach (var eachSubject in subjectsForStudent)
                    {
                        // OPTIMIZATION 9: Find assigned teacher for this subject-arm combination
                        var teacher = await classSubjectArmAssignmentRepository.FindAsync(
                            r => r.ArmId == eachStudent.ClassArmId && r.SubjectId == eachSubject.Id, ct);

                        if (teacher == null)
                        {
                            // OPTIMIZATION 10: Early return with detailed error message
                            return $"'{eachSubject.SubjectName}' has not been assigned to a teacher in " +
                                                                     $"{firstArmStudent.ClassArm!.Classs!.ClassLabel}{firstArmStudent.ClassArm.ArmName}.";
                        }

                        // OPTIMIZATION 11: Check if assessment record already exists
                        var eachStudentAssessment = await studentAssessmentRepository.FindAsync(
                            n => n.SubjectId == eachSubject.Id &&
                                                                 n.Student != null &&
                                                                 n.Student.UserId == eachStudent.UserId &&
                                                                 n.Employee != null &&
                                                                 n.Employee.SchoolId == schoolId &&
                                                                 n.YearlyAndTermlyStatus != null &&
                                                                 n.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                            ct);

                        if (eachStudentAssessment != null)
                        {
                            // OPTIMIZATION 12: Update existing assessment - only change teacher if needed
                            if (eachStudentAssessment.EmployeeId != teacher.EmployeeId)
                            {
                                eachStudentAssessment.EmployeeId = teacher.EmployeeId;
                                studentAssessmentRepository.Update(eachStudentAssessment);
                            }
                        }
                        else
                        {
                            // OPTIMIZATION 13: Create new assessment record with proper initialization
                            var newAssessment = new StudenXtAccessment
                            {
                                Id = Guid.NewGuid().ToString(),
                                EmployeeId = teacher.EmployeeId,
                                SubjectId = eachSubject.Id,
                                StudentId = eachStudent.UserId,
                                ClassArmId = eachStudent.ClassArmId,
                                LastUpdateDate = DateTime.UtcNow, // Use UTC for consistency
                                YearlyAndTermlyStatusId = termId,
                                Ca1 = 0,
                                Ca2 = 0,
                                Ca3 = 0,
                                Exams = 0
                            };
                            studentAssessmentRepository.Add(newAssessment);
                        }
                    }

                    // OPTIMIZATION 14: Clean up elective subjects from the working collection
                    // (Important: We work with subjectsForStudent, not the shared classSubjectCollection)
                    // This is a no-op since we already work with a copy, but kept for algorithm consistency
                }
            }

            // OPTIMIZATION 15: Persist all changes in a single batch transaction
            await _uow.SaveChangesAsync(ct);

            return "Succeed";
        }

        private async Task<string> ProceedFromHereForInsert(
        IGrouping<string, TxStudentDetail> eachClass,
        List<SubjectDto> classSubjectCollection,
        string termId,
        IGenericRepository<StudenXtAccessment> studentAssessmentRepository,
        string schoolId,
        CancellationToken ct = default)
        {
            var classSubjectArmAssignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var subjectStudentAssignmentRepository = _uow.Repository<SubjectStudentTxAssignment>();

            foreach (var eachArmStudents in eachClass.GroupBy(g => g.ClassArmId))
            {
                var armStudents = eachArmStudents
                    .Where(student => student.ClassArm?.Classs != null)
                    .ToList();
                if (armStudents.Count == 0)
                {
                    continue;
                }

                var firstArmStudent = armStudents.First();

                foreach (var eachStudent in armStudents)
                {
                    var subjectsForStudent = new List<SubjectDto>(classSubjectCollection);

                    // Add elective subjects if not PlayClass or PreNursery
                    var className = eachStudent.ClassArm!.Classs!.ClassName;
                    if (className != CurrentClass.PreNursery && className != CurrentClass.PlayClass)
                    {
                        var electiveSubjects = await subjectStudentAssignmentRepository.WhereAsync(
                            d => d.StudentDetail != null &&
                                 d.StudentDetail.SchoolInfoId == schoolId &&
                                 d.StudentDetail.UserId == eachStudent.UserId &&
                                 d.Subject != null &&
                                 d.Subject.SubjectCategory != null &&
                                 d.Subject.SubjectCategory.SubjectStatus == SubjectStatus.Elective,
                            ct,
                            d => d.Subject!, d => d.Subject!.SubjectCategory!);

                        if (electiveSubjects != null && electiveSubjects.Any())
                        {
                            subjectsForStudent.AddRange(
                                electiveSubjects
                                    .Where(assignment => assignment.Subject != null)
                                    .Select(assignment => MapSubject(assignment.Subject!)));
                        }
                    }

                    foreach (var eachSubject in subjectsForStudent)
                    {
                        var teacher = await classSubjectArmAssignmentRepository.FindAsync(
                            r => r.ArmId == eachStudent.ClassArmId && r.SubjectId == eachSubject.Id, ct);

                        if (teacher != null)
                        {
                            var assessment = new StudenXtAccessment
                            {
                                Id = Guid.NewGuid().ToString(),
                                EmployeeId = teacher.EmployeeId,
                                SubjectId = eachSubject.Id,
                                StudentId = eachStudent.UserId,
                                ClassArmId = eachStudent.ClassArmId,
                                LastUpdateDate = DateTime.UtcNow,
                                YearlyAndTermlyStatusId = termId,
                                Ca1 = 0,
                                Ca2 = 0,
                                Ca3 = 0,
                                Exams = 0
                            };
                            studentAssessmentRepository.Add(assessment);
                        }
                        else
                        {
                            return $"'{eachSubject.SubjectName}' has not been assigned to a teacher in {firstArmStudent.ClassArm!.Classs!.ClassLabel}{firstArmStudent.ClassArm.ArmName}.";
                        }
                    }
                }
            }
            await _uow.SaveChangesAsync(ct);
            return "Succeed";
        }



        // Method 56 (private) - Placeholder implementation
        private async Task<List<SubjectDto>> ElectiveSubjectsCollections(int classId, string schoolId, CancellationToken ct = default)
        {
            var subjectRepository = _uow.Repository<RxSubject>();
            var subjects = await subjectRepository.WhereAsync(
                s => s.SubjectCategory != null &&
                     s.SubjectCategory.SchoolId == schoolId &&
                     s.SubjectCategory.SubjectStatus == SubjectStatus.Elective, ct, s => s.SubjectCategory!);

            return subjects.Select(MapSubject).ToList();
        }

        // Method 57 (private) - Placeholder implementation
        private async Task<List<SubjectDto>> SeniorSubjectList(string userId, int classId, CancellationToken ct = default)
        {
            var subjectRepository = _uow.Repository<RxSubject>();
            var schoolId = userId;

            var subjects = await subjectRepository.WhereAsync(
                 s => s.SubjectCategory != null &&
                     s.SubjectCategory.SchoolId == schoolId &&
                     (s.SubjectCategory.SubjectStatus == SubjectStatus.AllSeniors ||
                      s.SubjectCategory.SubjectStatus == SubjectStatus.Science ||
                      s.SubjectCategory.SubjectStatus == SubjectStatus.Art ||
                      s.SubjectCategory.SubjectStatus == SubjectStatus.Commercial), ct, s => s.SubjectCategory!);

            return subjects.Select(MapSubject).ToList();
        }

        // Method 58
        public async Task<List<SubjectDto>> SeniorClassesSubjectListCurrent(string userId, ClassCategoryStatus studentStatus, CancellationToken ct = default)
        {
            var subjectRepository = _uow.Repository<RxSubject>();
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);

            var subjects = studentStatus switch
            {
                ClassCategoryStatus.Science => await subjectRepository.WhereAsync(
                    s => s.SubjectCategory != null &&
                         s.SubjectCategory.SchoolId == schoolId &&
                         s.TimeStatus == TimeStatus.Current &&
                         (s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                          s.SubjectCategory.SubjectStatus == SubjectStatus.AllSeniors ||
                          s.SubjectCategory.SubjectStatus == SubjectStatus.Science),
                    ct,
                    s => s.SubjectCategory!),
                ClassCategoryStatus.Commercial => await subjectRepository.WhereAsync(
                    s => s.SubjectCategory != null &&
                         s.SubjectCategory.SchoolId == schoolId &&
                         s.TimeStatus == TimeStatus.Current &&
                         (s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                          s.SubjectCategory.SubjectStatus == SubjectStatus.AllSeniors ||
                          s.SubjectCategory.SubjectStatus == SubjectStatus.Commercial),
                    ct,
                    s => s.SubjectCategory!),
                _ => await subjectRepository.WhereAsync(
                    s => s.SubjectCategory != null &&
                         s.SubjectCategory.SchoolId == schoolId &&
                         s.TimeStatus == TimeStatus.Current &&
                         (s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                          s.SubjectCategory.SubjectStatus == SubjectStatus.AllSeniors ||
                          s.SubjectCategory.SubjectStatus == SubjectStatus.Art),
                    ct,
                    s => s.SubjectCategory!),
            };

            return subjects.Select(MapSubject).ToList();
        }

        public async Task<List<SubjectDto>> BasicClassesSubjectListCurrent(string userId, CurrentClass className, CancellationToken ct = default)
        {
            var subjectRepository = _uow.Repository<RxSubject>();
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);

            SubjectStatus subjectStatus = className switch
            {
                CurrentClass.PlayClass => SubjectStatus.PlayClass,
                CurrentClass.PreNursery => SubjectStatus.PreNursery,
                CurrentClass.Nursery1 or CurrentClass.Nursery2 => SubjectStatus.Nursery,
                CurrentClass.Primary1 or CurrentClass.Primary2 or CurrentClass.Primary3 or
                CurrentClass.Primary4 or CurrentClass.Primary5 or CurrentClass.Primary6 => SubjectStatus.Primary,
                CurrentClass.Jss1 or CurrentClass.Jss2 or CurrentClass.Jss3 => SubjectStatus.Jss,
                _ => SubjectStatus.General
            };

            var subjects = await subjectRepository.WhereAsync(
                 s => s.SubjectCategory != null &&
                     s.SubjectCategory.SchoolId == schoolId &&
                     (s.SubjectCategory.SubjectStatus == subjectStatus ||
                      s.SubjectCategory.SubjectStatus == SubjectStatus.General) &&
                     s.TimeStatus == TimeStatus.Current, ct, s => s.SubjectCategory!);

            return subjects.Select(MapSubject).ToList();
        }

        // Method 60
        public async Task<List<SubjectDto>> SeniorClassesSubjectList(string userId, ClassCategoryStatus studentStatus, CurrentClass classs, CancellationToken ct = default)
        {
            var subjectRepository = _uow.Repository<RxSubject>();
            var schoolId = userId;

            SubjectStatus subjectStatus = studentStatus switch
            {
                ClassCategoryStatus.Science => SubjectStatus.Science,
                ClassCategoryStatus.Art => SubjectStatus.Art,
                ClassCategoryStatus.Commercial => SubjectStatus.Commercial,
                _ => SubjectStatus.AllSeniors
            };

            var subjects = await subjectRepository.WhereAsync(
                 s => s.SubjectCategory != null &&
                     s.SubjectCategory.SchoolId == schoolId &&
                     (s.SubjectCategory.SubjectStatus == subjectStatus ||
                      s.SubjectCategory.SubjectStatus == SubjectStatus.AllSeniors ||
                      s.SubjectCategory.SubjectStatus == SubjectStatus.General), ct, s => s.SubjectCategory!);

            return subjects.Select(MapSubject).ToList();
        }

        // Method 61
        public async Task<List<SubjectDto>> BasicClassesSubjectList(string userId, CurrentClass className, CancellationToken ct = default)
        {
            var subjectRepository = _uow.Repository<RxSubject>();
            var schoolId = userId;

            SubjectStatus subjectStatus = className switch
            {
                CurrentClass.PlayClass => SubjectStatus.PlayClass,
                CurrentClass.PreNursery => SubjectStatus.PreNursery,
                CurrentClass.Nursery1 or CurrentClass.Nursery2 => SubjectStatus.Nursery,
                CurrentClass.Primary1 or CurrentClass.Primary2 or CurrentClass.Primary3 or
                CurrentClass.Primary4 or CurrentClass.Primary5 or CurrentClass.Primary6 => SubjectStatus.Primary,
                CurrentClass.Jss1 or CurrentClass.Jss2 or CurrentClass.Jss3 => SubjectStatus.Jss,
                _ => SubjectStatus.General
            };

            var subjects = await subjectRepository.WhereAsync(
                 s => s.SubjectCategory != null &&
                     s.SubjectCategory.SchoolId == schoolId &&
                     (s.SubjectCategory.SubjectStatus == subjectStatus ||
                      s.SubjectCategory.SubjectStatus == SubjectStatus.General), ct, s => s.SubjectCategory!);

            return subjects.Select(MapSubject).ToList();
        }

        // Method 62 (private) - Placeholder implementation (complex assessment logic)
        private async Task<List<UnAssignedClassArmSubjectDto>> UnAssignedClassArmSubjectList(
            IGrouping<string, TxStudentDetail> eachClass,
            List<SubjectDto> classSubjectCollection,
            CancellationToken ct = default)
        {
            await Task.CompletedTask;
            return new List<UnAssignedClassArmSubjectDto>();
        }

        private async Task<string> ProceedFromHereForUpdateSeniorStudent(
            IGrouping<ClassCategoryStatus, TxStudentDetail> eachClass,
            List<SubjectDto> classSubjectCollection,
            string yearlyAndTermlyAssessmentStatusId,
            IGenericRepository<StudenXtAccessment> studentAssessmentRepository,
            string schoolId,
            CancellationToken ct = default)
        {
            var students = eachClass.ToList();
            if (students.Count == 0)
            {
                return "Succeed";
            }

            var classSubjectArmAssignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var subjectStudentAssignmentRepository = _uow.Repository<SubjectStudentTxAssignment>();

            var studentIds = students
                .Select(s => s.UserId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var armIds = students
                .Select(s => s.ClassArmId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var electiveAssignments = studentIds.Count == 0
                ? new List<SubjectStudentTxAssignment>()
                : (await subjectStudentAssignmentRepository.WhereAsync(
                    d => d.StudentDetail != null &&
                        d.StudentDetail.SchoolInfoId == schoolId &&
                        d.Subject != null &&
                        d.Subject.TimeStatus == TimeStatus.Current &&
                         studentIds.Contains(d.StudentDetail.UserId),
                    ct,
                    d => d.StudentDetail!,
                    d => d.Subject!,
                    d => d.Subject!.SubjectCategory!)).ToList();

            var electiveAssignmentsByStudent = electiveAssignments
                .Where(a => !string.IsNullOrWhiteSpace(a.StudentDetail?.UserId) && a.Subject != null)
                .GroupBy(a => a.StudentDetail!.UserId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(a => MapSubject(a.Subject!))
                        .Where(subject => !string.IsNullOrWhiteSpace(subject.Id))
                        .GroupBy(subject => subject.Id, StringComparer.OrdinalIgnoreCase)
                        .Select(subjectGroup => subjectGroup.First())
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);

            var subjectIds = classSubjectCollection
                .Select(subject => subject.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Concat(electiveAssignmentsByStudent.Values.SelectMany(subjects => subjects.Select(subject => subject.Id)))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var teacherAssignments = armIds.Count == 0 || subjectIds.Count == 0
                ? new List<TeachTxerSubjectArmAssignment>()
                : (await classSubjectArmAssignmentRepository.WhereAsync(
                    assignment => armIds.Contains(assignment.ArmId) && subjectIds.Contains(assignment.SubjectId),
                    ct)).ToList();

            var teacherAssignmentsByKey = teacherAssignments
                .GroupBy(
                    assignment => $"{assignment.ArmId}::{assignment.SubjectId}",
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().EmployeeId,
                    StringComparer.OrdinalIgnoreCase);

            var currentAssessments = studentIds.Count == 0 || subjectIds.Count == 0
                ? new List<StudenXtAccessment>()
                : (await studentAssessmentRepository.WhereAsync(
                    assessment => studentIds.Contains(assessment.StudentId) &&
                                  subjectIds.Contains(assessment.SubjectId) &&
                                  assessment.Employee != null &&
                                  assessment.Employee.SchoolId == schoolId &&
                                  assessment.YearlyAndTermlyStatus != null &&
                                  assessment.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                    ct,
                    assessment => assessment.Employee!,
                    assessment => assessment.YearlyAndTermlyStatus!)).ToList();

            var currentAssessmentsByKey = currentAssessments
                .GroupBy(
                    assessment => $"{assessment.StudentId}::{assessment.SubjectId}",
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var eachStudent in students)
            {
                var subjectsForStudent = new List<SubjectDto>(classSubjectCollection.Count + 8);
                var subjectIdsForStudent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var subject in classSubjectCollection)
                {
                    if (!string.IsNullOrWhiteSpace(subject.Id) && subjectIdsForStudent.Add(subject.Id))
                    {
                        subjectsForStudent.Add(subject);
                    }
                }

                if (!string.IsNullOrWhiteSpace(eachStudent.UserId) &&
                    electiveAssignmentsByStudent.TryGetValue(eachStudent.UserId, out var electiveSubjects))
                {
                    foreach (var subject in electiveSubjects)
                    {
                        if (!string.IsNullOrWhiteSpace(subject.Id) && subjectIdsForStudent.Add(subject.Id))
                        {
                            subjectsForStudent.Add(subject);
                        }
                    }
                }

                foreach (var eachSubject in subjectsForStudent)
                {
                    var teacherKey = $"{eachStudent.ClassArmId}::{eachSubject.Id}";
                    if (!teacherAssignmentsByKey.TryGetValue(teacherKey, out var teacherId))
                    {
                        return $"'{eachSubject.SubjectName}' has not been assigned to a teacher in " +
                               $"{eachStudent.ClassArm?.Classs?.ClassLabel ?? string.Empty}" +
                               $"{eachStudent.ClassArm?.ArmName.ToString() ?? string.Empty}.";
                    }

                    var assessmentKey = $"{eachStudent.UserId}::{eachSubject.Id}";
                    if (currentAssessmentsByKey.TryGetValue(assessmentKey, out var eachStudentAssessment))
                    {
                        if (eachStudentAssessment.EmployeeId != teacherId)
                        {
                            eachStudentAssessment.EmployeeId = teacherId;
                            studentAssessmentRepository.Update(eachStudentAssessment);
                        }
                    }
                    else
                    {
                        var assessment = new StudenXtAccessment
                        {
                            Id = Guid.NewGuid().ToString(),
                            EmployeeId = teacherId,
                            SubjectId = eachSubject.Id,
                            StudentId = eachStudent.UserId,
                            ClassArmId = eachStudent.ClassArmId,
                            LastUpdateDate = DateTime.UtcNow,
                            YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
                            Ca1 = 0,
                            Ca2 = 0,
                            Ca3 = 0,
                            Exams = 0
                        };

                        studentAssessmentRepository.Add(assessment);
                        currentAssessmentsByKey[assessmentKey] = assessment;
                    }
                }
            }

            await _uow.SaveChangesAsync(ct);
            return "Succeed";
        }

        // Method 64 (private) - Placeholder implementation (complex assessment logic)
        private async Task<List<UnAssignedClassArmSubjectDto>> ProceedFromHereForBasicStudents(
            IGrouping<string, TxStudentDetail> eachClass,
            List<SubjectDto> classSubjectCollection,
            string schoolId,
            CancellationToken ct = default)
        {
            await Task.CompletedTask;
            return new List<UnAssignedClassArmSubjectDto>();
        }

        public async Task<bool> CreatSchoolHead(string userId, SchoolHeadDto model, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId) || model == null)
            {
                return false;
            }

            var schoolId = await ResolveSchoolScopeIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return false;
            }

            var schoolHeadRepository = _uow.Repository<SchoolHeadTx>();
            var schoolHeadCategoryId = ResolveSchoolHeadCategoryId(model.HeadStatus);

            var currentSchoolHead = await schoolHeadRepository.FindAsync(
                head => head.Status == TimeStatus.Current &&
                        head.SchoolInfoId == schoolId &&
                        head.SchoolHeadCategoryId == schoolHeadCategoryId,
                ct);

            if (currentSchoolHead != null)
            {
                currentSchoolHead.Status = TimeStatus.Previous;
                schoolHeadRepository.Update(currentSchoolHead);
            }

            var schoolHead = new SchoolHeadTx
            {
                Id = Guid.NewGuid().ToString(),
                HeadsName = model.HeadsName?.Trim() ?? string.Empty,
                PhoneNumber = model.PhoneNumber?.Trim() ?? string.Empty,
                SignatureImageString = model.SignatureImageString?.Trim() ?? string.Empty,
                Status = TimeStatus.Current,
                SchoolInfoId = schoolId,
                SchoolHeadCategoryId = schoolHeadCategoryId,
                Gender = model.Gender,
            };

            schoolHeadRepository.Add(schoolHead);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        public async Task<IEnumerable<SchoolHeadDto>> GetPrincals(string userId, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolScopeIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return new List<SchoolHeadDto>();
            }

            var schoolHeadRepository = _uow.Repository<SchoolHeadTx>();
            var schoolHeads = await schoolHeadRepository.WhereAsync(
                head => head.SchoolInfoId == schoolId,
                ct,
                head => head.SchoolHeadCategory!);

            return schoolHeads
                .Select(MapSchoolHead)
                .ToList();
        }

        public async Task<bool> UpdateSchoolHeadInfo(string userId, SchoolHeadDto model, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId) || model == null || string.IsNullOrWhiteSpace(model.Id))
            {
                return false;
            }

            var schoolId = await ResolveSchoolScopeIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return false;
            }

            var schoolHeadRepository = _uow.Repository<SchoolHeadTx>();
            var schoolHead = await schoolHeadRepository.FindAsync(
                head => head.Id == model.Id && head.SchoolInfoId == schoolId,
                ct);

            if (schoolHead == null)
            {
                return false;
            }

            var schoolHeadCategoryId = ResolveSchoolHeadCategoryId(model.HeadStatus);
            if (model.Status == TimeStatus.Current)
            {
                var currentSchoolHead = await schoolHeadRepository.FindAsync(
                    head => head.Status == TimeStatus.Current &&
                            head.SchoolInfoId == schoolId &&
                            head.SchoolHeadCategoryId == schoolHeadCategoryId &&
                            head.Id != model.Id,
                    ct);

                if (currentSchoolHead != null)
                {
                    currentSchoolHead.Status = TimeStatus.Previous;
                    schoolHeadRepository.Update(currentSchoolHead);
                }
            }

            schoolHead.HeadsName = model.HeadsName?.Trim() ?? string.Empty;
            schoolHead.PhoneNumber = model.PhoneNumber?.Trim() ?? string.Empty;
            schoolHead.SignatureImageString = model.SignatureImageString?.Trim() ?? string.Empty;
            schoolHead.Status = model.Status;
            schoolHead.SchoolInfoId = schoolId;
            schoolHead.SchoolHeadCategoryId = schoolHeadCategoryId;
            schoolHead.Gender = model.Gender;

            schoolHeadRepository.Update(schoolHead);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        public async Task<byte> CreateSchoolHeadAssignment(string userId, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolScopeIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return 1;
            }

            var yearlyAndTermlyRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
            var schoolHeadRepository = _uow.Repository<SchoolHeadTx>();

            var currentSchoolHeads = await schoolHeadRepository.WhereAsync(
                head => head.SchoolInfoId == schoolId && head.Status == TimeStatus.Current,
                ct);

            if (currentSchoolHeads == null || currentSchoolHeads.Count == 0)
            {
                return 1;
            }

            var headMasterId = currentSchoolHeads
                .FirstOrDefault(head => head.SchoolHeadCategoryId == ResolveSchoolHeadCategoryId(HeadStatus.HeadTeacher))?.Id;
            var principalId = currentSchoolHeads
                .FirstOrDefault(head => head.SchoolHeadCategoryId == ResolveSchoolHeadCategoryId(HeadStatus.Principal))?.Id;

            var yearlyAndTermlyModel = await yearlyAndTermlyRepository.FindAsync(
                status => status.Session != null &&
                          status.Session.SchoolInfoId == schoolId &&
                          (status.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                           status.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing),
                ct);

            if (yearlyAndTermlyModel == null)
            {
                return 1;
            }

            yearlyAndTermlyModel.HeadmasterId = headMasterId;
            yearlyAndTermlyModel.PrincipalId = principalId;
            yearlyAndTermlyRepository.Update(yearlyAndTermlyModel);
            await _uow.SaveChangesAsync(ct);
            return 0;
        }

        public async Task<string> GetSchoolHeadNames(string userId, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolScopeIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return "Sorry! You have not yet set school head.";
            }

            var schoolHeadRepository = _uow.Repository<SchoolHeadTx>();
            var yearlyAndTermlyRepository = _uow.Repository<TxYearlyAndTermlyStatus>();

            var yearlyAndTermlyModel = await yearlyAndTermlyRepository.FindAsync(
                status => status.Session != null &&
                          status.Session.SchoolInfoId == schoolId &&
                          (status.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                           status.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing),
                ct);

            if (yearlyAndTermlyModel == null)
            {
                return "Sorry! You have not yet set school head.";
            }

            var headTeacher = string.IsNullOrWhiteSpace(yearlyAndTermlyModel.HeadmasterId)
                ? null
                : await schoolHeadRepository.FindAsync(
                    head => head.SchoolInfoId == schoolId && head.Id == yearlyAndTermlyModel.HeadmasterId,
                    ct);

            var principal = string.IsNullOrWhiteSpace(yearlyAndTermlyModel.PrincipalId)
                ? null
                : await schoolHeadRepository.FindAsync(
                    head => head.SchoolInfoId == schoolId && head.Id == yearlyAndTermlyModel.PrincipalId,
                    ct);

            return $"Headmaster's name is: {headTeacher?.HeadsName ?? "No Headmaster"} while Principal's name is: {principal?.HeadsName ?? "No Principal"}";
        }

        public async Task<bool> CommercialSubjectUpdateValidation(string userId, string subjectName, int id, CancellationToken ct = default)
        {
            var subjectRepository = _uow.Repository<RxSubject>();
            var schoolId = userId;

            var subjects = await subjectRepository.WhereAsync(
                sr => sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                      sr.SubjectCategory != null &&
                      sr.SubjectCategory.SchoolId == schoolId, ct, s => s.SubjectCategory!);

            var statusList = subjects
                .Where(subject => subject.SubjectCategory != null)
                .Select(subject => subject.SubjectCategory!.SubjectStatus)
                .ToList();

            if (statusList.Any())
            {
                if (statusList.Contains(SubjectStatus.Art) ||
                    statusList.Contains(SubjectStatus.Science) ||
                    statusList.Contains(SubjectStatus.AllSeniors) ||
                    statusList.Contains(SubjectStatus.General))
                {
                    return false;
                }
            }
            return true;
        }


        public async Task<string> StudentsPromotionValidations(string userorSchoolId, CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userorSchoolId, ct);
            var studentAccessmentRepository = _uow.Repository<StudenXtAccessment>();
            var academicSessionRepository = _uow.Repository<TxAcademicSession>();

            var isStudentAssessmentEverPerformed = await studentAccessmentRepository.CountAsync(
                c => c.Student != null && c.Student.SchoolInfoId == schoolId, ct);
            if (!(isStudentAssessmentEverPerformed > 1))
            {
                return "Failed! You have not done any Students assessment yet";
            }

            var currentSessionAssessment = await studentAccessmentRepository.WhereAsync(
                s => s.YearlyAndTermlyStatus != null
                     && s.YearlyAndTermlyStatus.Session != null
                     && s.YearlyAndTermlyStatus.Session.Status == TimeStatus.Current
                     && s.YearlyAndTermlyStatus.Session.SchoolInfoId == schoolId,
                ct,
                s => s.YearlyAndTermlyStatus!.Session!);

            var numberOfTerm = currentSessionAssessment
                .GroupBy(s => s.YearlyAndTermlyStatus!.TermStatus)
                .Count();

            var academicSessionList = await academicSessionRepository.CountAsync(
                s => s.SchoolInfoId == schoolId, ct);

            if (academicSessionList == 1)
            {
                var startTermRows = await studentAccessmentRepository.WhereAsync(
                    s => s.Employee != null && s.Employee.SchoolId == schoolId,
                    ct,
                    a => a.YearlyAndTermlyStatus!,
                    a => a.Employee!);

                var startTermRecord = startTermRows
                    .Where(a => a.YearlyAndTermlyStatus != null)
                    .OrderBy(a => a.YearlyAndTermlyStatus!.TermStatus)
                    .FirstOrDefault();

                if (startTermRecord?.YearlyAndTermlyStatus == null)
                {
                    return "Failed! You have not done any Students assessment yet";
                }

                var startTerm = startTermRecord.YearlyAndTermlyStatus.TermStatus;
                if (startTerm == TermStatus.First)
                {
                    if (numberOfTerm == 1)
                        return "The school has only done a single term and therefore you cannot run promotion";
                    if (numberOfTerm == 2)
                        return "The school has only done two terms and therefore you cannot run promotion; until when its three";
                    if (numberOfTerm == 3)
                        return await CheckFormTeacherAssessmentCompletion(schoolId, ct);
                }
                else if (startTerm == TermStatus.Second)
                {
                    if (numberOfTerm == 1)
                        return "The school has only done a single term and therefore you cannot run promotion";
                    if (numberOfTerm == 2)
                        return await CheckFormTeacherAssessmentCompletion(schoolId, ct);
                }
                else if (startTerm == TermStatus.Third)
                {
                    if (numberOfTerm == 1)
                        return await CheckFormTeacherAssessmentCompletion(schoolId, ct);
                }
            }

            if (numberOfTerm == 1)
                return "The school has only done a single term and therefore you cannot run promotion";
            if (numberOfTerm == 2)
                return "The school has only done two terms and therefore you cannot run promotion; until when its three";
            if (numberOfTerm == 3)
                return await CheckFormTeacherAssessmentCompletion(schoolId, ct);

            // Legacy recovery when term count is unexpected: drop newest session, restore previous as Current.
            var sessionList = (await academicSessionRepository.WhereAsync(s => s.SchoolInfoId == schoolId, ct))
                .OrderByDescending(x => x.Session)
                .Take(2)
                .ToList();

            if (sessionList.Count < 2)
            {
                return "You need to try again.";
            }

            var newest = sessionList[0];
            var previous = sessionList[1];
            previous.Status = TimeStatus.Current;
            academicSessionRepository.Update(previous);
            await academicSessionRepository.DeleteAsync(newest.Id, ct);
            await _uow.SaveChangesAsync(ct);
            return "You need to try again.";
        }

        public async Task<string> GetSmsTokenAndKey(string userOrSchoolId, CancellationToken ct = default)
        {
            var repo = _uow.Repository<SchoolTyInfo, string>();
            var school = await repo.FindAsync(c => c.SchoolRegistrationOfficerId == userOrSchoolId, ct, v => v.SchoolRegistrationOfficer.User);
            if (school == null) return string.Empty;

            var smsApiToken = FreePlugIn.Encrypt(Guid.NewGuid().ToString(), true);
            var smsApiKey = FreePlugIn.GetUniqueSystemId();
            var smsApiKeyEncode = FreePlugIn.Encrypt(smsApiKey, true);

            if (string.IsNullOrEmpty(school.SmsApiToken))
            {
                school.SmsApiToken = smsApiToken;
                // Store the key this method returns, so the credentials it hands out verify.
                school.SmsApiKey = smsApiKeyEncode;
                repo.Update(school);
                await _uow.SaveChangesAsync(ct);
                return $"{smsApiToken}:{smsApiKeyEncode}";
            }

            return $"{school.SmsApiToken}:{smsApiKeyEncode}";
        }
        public async Task<bool> ChangeSystemImplementationType(string conCatParameters, CancellationToken ct = default)
        {
            var parts = conCatParameters.Split(',');
            if (parts.Length < 2) return false;
            var userOrSchoolId = parts[0];
            var newType = parts[1];

            // Accept either AspNet user id or SchoolRegistrationOfficerId (legacy passes user id).
            var schoolId = await ResolveSchoolScopeIdAsync(userOrSchoolId, ct) ?? userOrSchoolId.Trim();
            if (string.IsNullOrWhiteSpace(schoolId)) return false;

            var schoolRepo = _uow.Repository<SchoolTyInfo, string>();
            // Prefer PK lookup (tracked) to avoid AsNoTracking + Attach conflicts in the same request.
            var entity = await schoolRepo.FindAsync(schoolId, ct)
                ?? await schoolRepo.FindAsync(t => t.SchoolRegistrationOfficerId == schoolId, ct);
            if (entity == null) return false;

            // Match legacy SecurityService.UpdateSystemImplementationType:
            // Encrypt(GetUniqueSystemId() + ":" + typeByte)
            entity.SystemImplementationType = FreePlugIn.Encrypt(
                FreePlugIn.GetUniqueSystemId() + ":" + newType, true);
            schoolRepo.Update(entity);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        public async Task<SchoolInfoDto> GetSchoolModelForSchoolRegistrationOfficer(string userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var officerRepo = _uow.Repository<SchoolRegistrationOfficer>();
            var officerSchool = (await officerRepo.FindAsync(u => u.UserId == userId, ct, s => s.SchoolInfo))?.SchoolInfo;
            if (officerSchool != null)
            {
                return _mapper.Map<SchoolInfoDto>(officerSchool);
            }

            string? schoolId;
            try
            {
                schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(schoolId))
            {
                return null;
            }

            var schoolRepo = _uow.Repository<SchoolTyInfo, string>();
            var school = await schoolRepo.FindAsync(s => s.SchoolRegistrationOfficerId == schoolId, ct);
            return school == null ? null : _mapper.Map<SchoolInfoDto>(school);
        }
        public async Task<string> GetAndEncodeCredentials(string userOrSchoolId, CancellationToken ct = default)
        {
            var schoolId = await ResolveSchoolScopeIdAsync(userOrSchoolId, ct);
            if (string.IsNullOrWhiteSpace(schoolId))
            {
                throw new InvalidOperationException("School registration profile was not found for the current user.");
            }

            var credential = await GetSmsTokenAndKey(schoolId, ct);
            var studentRepo = _uow.Repository<TxStudentDetail>();
            var noOfStudents = await studentRepo.CountAsync(s =>
                (s.Status == StudentStatus.Fresher || s.Status == StudentStatus.Returning) &&
                s.SchoolInfoId == schoolId, ct);

            var schoolInfoRepo = _uow.Repository<SchoolTyInfo, string>();
            var schoolInfo = await schoolInfoRepo.FindAsync(
                c => c.SchoolRegistrationOfficerId == schoolId,
                ct,
                s => s.SchoolRegistrationOfficer.User);
            if (schoolInfo == null)
            {
                throw new InvalidOperationException("School registration profile was not found for the current user.");
            }

            var payload = $"{credential}:{schoolInfo.SchoolAdminAccountantStatus}:{noOfStudents}";
            return FreePlugIn.Encrypt(payload, true);
        }

        private static byte ResolveSchoolHeadCategoryId(HeadStatus headStatus)
        {
            return (byte)(headStatus == HeadStatus.HeadTeacher ? 1 : 2);
        }

        private async Task<string?> ResolveSchoolScopeIdAsync(string userId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            try
            {
                return await RegistrySchoolLookup.ResolveSchoolRegistrationOfficerIdAsync(_uow, userId, ct);
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static HeadStatus ResolveSchoolHeadStatus(byte schoolHeadCategoryId)
        {
            return schoolHeadCategoryId == 1 ? HeadStatus.HeadTeacher : HeadStatus.Principal;
        }

        private static SchoolHeadDto MapSchoolHead(SchoolHeadTx schoolHead)
        {
            var headStatus = schoolHead.SchoolHeadCategory?.HeadStatus ?? ResolveSchoolHeadStatus(schoolHead.SchoolHeadCategoryId);

            return new SchoolHeadDto
            {
                Id = schoolHead.Id,
                HeadsName = schoolHead.HeadsName,
                PhoneNumber = schoolHead.PhoneNumber,
                SignatureImageString = schoolHead.SignatureImageString,
                Status = schoolHead.Status,
                SchoolInfoId = schoolHead.SchoolInfoId,
                SchoolHeadCategoryId = schoolHead.SchoolHeadCategoryId,
                HeadStatus = headStatus,
                Gender = schoolHead.Gender,
                SchoolHeadCategory = new SchoolHeadCategoryDto
                {
                    Id = schoolHead.SchoolHeadCategory?.Id ?? schoolHead.SchoolHeadCategoryId,
                    HeadStatus = headStatus,
                },
            };
        }
    }
}