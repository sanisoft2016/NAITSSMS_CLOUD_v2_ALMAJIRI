using AutoMapper;
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
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.Service
{
    public class SessionAndTermService : IService.ISessionAndTermService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        private readonly ISchoolScopeResolver _schoolScopeResolver;

        public SessionAndTermService(IMapper mapper, IUnitOfWork uow, ISchoolScopeResolver schoolScopeResolver)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _mapper = mapper;
            _schoolScopeResolver = schoolScopeResolver ?? throw new ArgumentNullException(nameof(schoolScopeResolver));
        }

        // Method 122: Get Academic Sessions
        public async Task<SessionsAndTerm> GetAcademicSessions(string userId, CancellationToken ct = default)
        {
            var academicSessionRepository = _uow.Repository<TxAcademicSession>();
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);

            var sessions = await academicSessionRepository.WhereAsync(
                s => s.SchoolInfoId == schoolId, ct);

            var yearlyAndTermlyStatusRepository = _uow.Repository<TxYearlyAndTermlyStatus>();

            // Legacy GetSessionsAndTerm: Current term only, scoped to this school.
            // (Previously `||`/`&&` precedence dropped the school filter for Current rows.)
            var currentTermObj = await yearlyAndTermlyStatusRepository.FindAsync(
                t => t.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                     && t.Session!.SchoolInfoId == schoolId,
                ct,
                t => t.Session!);
            var currentTerm = currentTermObj?.TermStatus;

           var sessionList = _mapper.Map<IEnumerable<AcademicSessionDto>>(sessions.OrderByDescending(s => s.Session));

            return new SessionsAndTerm { CurrentTerm = currentTerm, Sessions = sessionList.ToList() };
        }

        // Method 123: Delete Academic Session
        public async Task<bool> DeleteAcademicSession(string sessionId, CancellationToken ct = default)
        {
            var yearlyAndTermlyRepository = _uow.Repository<TxYearlyAndTermlyStatus>();

            // Check if session has any terms
            var hasTerms = await yearlyAndTermlyRepository.FindAsync(
                t => t.SessionId == sessionId, ct);

            if (hasTerms != null)
                return false; // Cannot delete session with terms

            var academicSessionRepository = _uow.Repository<TxAcademicSession>();
            await academicSessionRepository.DeleteAsync(sessionId, ct);
            await _uow.SaveChangesAsync(ct);

            return true;
        }

        // Method 124: Get Terms By Session
        public async Task<IEnumerable<YearlyAndTermlyStatusDto>> GetTermsBySession(
            string sessionId,
            CancellationToken ct = default)
        {
            var yearlyAndTermlyRepository = _uow.Repository<TxYearlyAndTermlyStatus>();

            var terms = await yearlyAndTermlyRepository.WhereAsync(
                t => t.SessionId == sessionId, ct);

            return terms
                .OrderBy(t => t.TermStatus)
                .Select(MapYearlyAndTermlyStatus)
                .ToList();
        }

        public async Task<IList<SelectListItem>> GetSessionsDropdown(string userId, CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var academicSessionRepository = _uow.Repository<TxAcademicSession>();
            var sessions = (await academicSessionRepository.WhereAsync(s => s.SchoolInfoId == schoolId, ct))
                .OrderByDescending(s => s.Session)
                .Select(s => new SelectListItem { Text = s.Session, Value = s.Id })
                .ToList();

            if (sessions.Count == 1)
            {
                var only = sessions[0];
                return new List<SelectListItem>
                {
                    new SelectListItem { Text = "Select", Value = "-1" },
                    only,
                };
            }

            return sessions;
        }

        public async Task<IList<SelectListItem>> GetTermsDropdownForSession(string sessionId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || sessionId == "-1")
            {
                return new List<SelectListItem>();
            }

            var yearlyAndTermlyRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
            var terms = await yearlyAndTermlyRepository.WhereAsync(
                t => t.SessionId == sessionId, ct);

            var items = terms
                .OrderBy(t => t.TermStatus)
                .Select(term => new SelectListItem
                {
                    Text = term.TermStatus.ToString(),
                    Value = ((int)term.TermStatus).ToString(),
                })
                .ToList();

            if (items.Count == 1)
            {
                var only = items[0];
                return new List<SelectListItem>
                {
                    new SelectListItem { Text = "Select", Value = "-1" },
                    only,
                };
            }

            return items;
        }

        private static YearlyAndTermlyStatusDto MapYearlyAndTermlyStatus(TxYearlyAndTermlyStatus term)
        {
            return new YearlyAndTermlyStatusDto
            {
                CreatedDate = term.CreatedDate,
                TermlyAccessmentStatus = term.TermlyAccessmentStatus,
                TermStatus = term.TermStatus,
                CasCategory = term.CasCategory,
                PerCasValue = term.PerCasValue,
                MinPassMark = term.MinPassMark,
                NextTermBeginsDate = term.NextTermBeginsDate,
                AssessmtEndDate = term.AssessmtEndDate,
                TotalAttendance = term.TotalAttendance,
                Session = term.Session == null
                    ? null
                    : new AcademicSessionDto
                    {
                        Id = term.Session.Id,
                        Session = term.Session.Session,
                        SchoolInfoId = term.Session.SchoolInfoId,
                        Status = term.Session.Status,
                    },
            };
        }

        // Method 104: Get Sessions And Term
        public async Task<SessionsAndTerm> GetSessionsAndTerm(string userId, CancellationToken ct = default)
        {
            var academicSessionRepository = _uow.Repository<TxAcademicSession>();
            var yearlyAndTermlyRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);

            var sessions = await academicSessionRepository.WhereAsync(
                s => s.SchoolInfoId == schoolId, ct);

            var currentTerm = await yearlyAndTermlyRepository.FindAsync(
                t => t.Session!.SchoolInfoId == schoolId &&
                     t.TermlyAccessmentStatus == TermlyAccessmentStatus.Current, ct, t => t.Session!);

            return new SessionsAndTerm
            {
                Sessions = _mapper.Map<IList<AcademicSessionDto>>(sessions),
                CurrentTerm = currentTerm?.TermStatus
            };
        }

        // Method 105: Create Academic Session
        public async Task<bool> CreateAcademicSession(string userId, AcademicSessionDto model, CancellationToken ct = default)
        {
            var academicSessionRepository = _uow.Repository<TxAcademicSession>();
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);

            var duplicate = await academicSessionRepository.FindAsync(
                a => a.Session == model.Session && a.SchoolInfoId == schoolId, ct);
            if (duplicate != null)
            {
                return false;
            }

            var currentSession = await academicSessionRepository.FindAsync(
                p => p.Status == TimeStatus.Current && p.SchoolInfoId == schoolId, ct);
            if (currentSession != null)
            {
                currentSession.Status = TimeStatus.Previous;
                academicSessionRepository.Update(currentSession);
            }

            var sessionEntity = _mapper.Map<TxAcademicSession>(model);
            sessionEntity.Id = Guid.NewGuid().ToString();
            sessionEntity.SchoolInfoId = schoolId;
            sessionEntity.Status = TimeStatus.Current;

            academicSessionRepository.Add(sessionEntity);
            await _uow.SaveChangesAsync(ct);

            return true;
        }

        // Method 106: Update Academic Session Info
        public async Task<bool> UpdateAcademicSessionInfo(string userId, AcademicSessionDto model, CancellationToken ct = default)
        {
            var academicSessionRepository = _uow.Repository<TxAcademicSession>();
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);

            var existing = await academicSessionRepository.FindAsync(model.Id, ct);
            if (existing == null || !string.Equals(existing.SchoolInfoId, schoolId, StringComparison.Ordinal))
            {
                return false;
            }

            var duplicate = await academicSessionRepository.FindAsync(
                a => a.Session == model.Session && a.SchoolInfoId == schoolId, ct);
            if (duplicate != null && !string.Equals(duplicate.Id, model.Id, StringComparison.Ordinal))
            {
                return false;
            }

            if (model.Status == TimeStatus.Current)
            {
                var currentSession = await academicSessionRepository.FindAsync(
                    c => c.Status == TimeStatus.Current && c.SchoolInfoId == schoolId, ct);
                if (currentSession != null && !string.Equals(currentSession.Id, model.Id, StringComparison.Ordinal))
                {
                    currentSession.Status = TimeStatus.Previous;
                    academicSessionRepository.Update(currentSession);
                }
            }

            existing.Session = model.Session;
            existing.Status = model.Status;
            existing.SchoolInfoId = schoolId;
            academicSessionRepository.Update(existing);
            await _uow.SaveChangesAsync(ct);

            return true;
        }
        public async Task<PrepareEnvironmentForStudentAssessmentReturnDto> PrepareAssessmentEnvironmentForTheWholeStudents(
           string userId,
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
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var allStudents = await studentDetailRepository.WhereAsync(
                t =>
                    (t.Status == Domain.Enum.StudentStatus.Fresher ||
                     t.Status == Domain.Enum.StudentStatus.Returning) &&
                    t.SchoolInfoId == schoolId,
                ct,
                r => r.ClassArm!.Classs!,
                u => u.User!,
                g => g.SchoolInfo!);

            var response = await StudentNewAssessmentPreparation(schoolId, requestStatus, allStudents.ToList(), ct);
            if (!string.IsNullOrEmpty(response.Message) &&
                response.Message.StartsWith("You first need to run", StringComparison.Ordinal))
            {
                var yearlyAndTermlyStatusRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
                //var obj = new TxYearlyAndTermlyStatus { SessionId = "", TermStatus = }
            }
            return response;
        }

        public async Task<PrepareEnvironmentForStudentAssessmentReturnDto> PrepareAssessmentEnvironmentForStudentsAsync(
            string schoolId,
            IReadOnlyList<TxStudentDetail> students,
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

            return await StudentNewAssessmentPreparation(schoolId, requestStatus, students.ToList(), ct);
        }

        private async Task<PrepareEnvironmentForStudentAssessmentReturnDto> StudentNewAssessmentPreparation(
            string schoolId,
            byte? requestStatus,
            List<TxStudentDetail> allStudents,
            CancellationToken ct = default)
        {
            // Always reject empty student sets up front. The legacy empty-check below only ran when
            // the school had zero assessments yet, so selecting JSS 1 / SS 1 with no Fresher/Returning
            // students still returned "Success. Note: the current term is ..." after assessments existed.
            if (allStudents == null || allStudents.Count == 0)
            {
                return new PrepareEnvironmentForStudentAssessmentReturnDto
                {
                    Message = "Sorry! No Fresher/Returning student was found in the selected class.",
                    TotalStudentListToPdf = new List<TotalStudentListToPdfDto>()
                };
            }

            // Legacy CreateEnvironmentForStudentsNewAssessment (used by
            // PrepareAssessmentEnvironmentForTheWholeStudent concat 0,0).
            var studentAssessmentRepository = _uow.Repository<StudenXtAccessment>();
            var assessmentCount = await studentAssessmentRepository.CountAsync(
                a => a.Employee!.SchoolId == schoolId,
                ct);

            if (assessmentCount <= 0)
            {
                var classCategory = allStudents.First().ClassCategory;
                // Avoid Enum.ToString() inside EF expression trees (not always translatable).
                var matchingStatuses = new List<SubjectStatus> { SubjectStatus.General, SubjectStatus.AllSeniors };
                if (Enum.TryParse<SubjectStatus>(classCategory.ToString(), ignoreCase: true, out var parsedStatus))
                {
                    matchingStatuses.Add(parsedStatus);
                }

                var subjectExists = await _uow.Repository<RxSubject>().FindAsync(
                    x =>
                        x.SubjectCategory != null &&
                        x.SubjectCategory.SchoolId == schoolId &&
                        matchingStatuses.Contains(x.SubjectCategory.SubjectStatus),
                    ct,
                    x => x.SubjectCategory!);

                if (subjectExists == null)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message = "Failed! Subjects needs to be created for the class(es) you have selected."
                    };
                }
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
            TermStatus? currentTermStatus = yearlyAndTermlyAssessmentStatus?.TermStatus;

            if (yearlyAndTermlyAssessmentStatus == null)
            {
                // Legacy: can only be null after running promotion.
                if (currentAcademicSession == null)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message = "You need to set the current Academic Session to continue."
                    };
                }

                var createNewTerm = await CreateNewTermForNewAssessmentIstAsync(
                    currentAcademicSession.Id,
                    schoolId,
                    allStudents,
                    ct);

                if (createNewTerm.Message != "Success")
                {
                    createNewTerm.Message =
                        "Unable to complete the process." +
                        " Note: the current term is '" + createNewTerm.CurrentTermStatus + " term'";
                    return createNewTerm;
                }

                yearlyAndTermlyAssessmentStatusId = createNewTerm.YearlyAndTermlyStatusId ?? string.Empty;
                currentTermStatus = createNewTerm.CurrentTermStatus;
            }
            else
            {
                yearlyAndTermlyAssessmentStatusId = yearlyAndTermlyAssessmentStatus.Id;
                currentTermStatus = yearlyAndTermlyAssessmentStatus.TermStatus;

                var hasTeacherAssignments = await teacherClassArmAssignmentRepository.CountAsync(
                    t => t.Employee!.SchoolId == schoolId,
                    ct);

                if (hasTeacherAssignments <= 0)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message =
                            "Failed! For you need to first assign form-masters to their appropriate class-arm before using this feature. Note: the current term is '" +
                            currentTermStatus + " term'."
                    };
                }

                var uncompletedTeacherAssignments = await teacherClassArmAssignmentRepository.WhereAsync(
                    t =>
                        t.Employee!.SchoolId == schoolId &&
                        t.ArmTermlyAssessmentStatus == ArmTermlyAssessmentStatus.Uncompleted,
                    ct,
                    t => t.Employee!.User!,
                    t => t.Arm!.Classs!);

                // Legacy CreateEnvironment uses current term status (not last assessment date).
                var lastTermsName = currentTermStatus ?? TermStatus.First;

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
                        Message = report.Count == 0
                            ? "Success! Note the current term is '" + currentTermStatus + " term'"
                            : string.Empty
                    };
                }

                if (uncompletedTeacherAssignments.Count == 0)
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

                    var currentTermAndId = await CreateNewTermForNewAssessment2ndAsync(
                        currentAcademicSession.Id,
                        yearlyAndTermlyAssessmentStatus,
                        schoolId,
                        lastTermsName,
                        ct);

                    var currentTermAndIdList = currentTermAndId.Split(':');
                    currentTermStatus = Enum.Parse<TermStatus>(currentTermAndIdList[0]);
                    yearlyAndTermlyAssessmentStatusId = currentTermAndIdList[1];
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
                createOrUpdateNewAssessment.CurrentTermStatus = currentTermStatus ?? TermStatus.First;
                createOrUpdateNewAssessment.Message =
                    "Success. Note: the current term is '" + currentTermStatus + " term'.";
            }
            else if (!string.IsNullOrEmpty(createOrUpdateNewAssessment.Message) &&
                     !createOrUpdateNewAssessment.Message.Contains("current term", StringComparison.OrdinalIgnoreCase))
            {
                createOrUpdateNewAssessment.Message =
                    createOrUpdateNewAssessment.Message +
                    " Note: the current term is: '" + currentTermStatus + " term'";
            }

            return createOrUpdateNewAssessment;
        }

        /// <summary>
        /// Legacy CreateNewTermForNewAssessmentIst — after promotion, open the next term on the current session.
        /// </summary>
        private async Task<PrepareEnvironmentForStudentAssessmentReturnDto> CreateNewTermForNewAssessmentIstAsync(
            string sessionId,
            string schoolId,
            List<TxStudentDetail> allStudents,
            CancellationToken ct)
        {
            var schoolRepository = _uow.Repository<SchoolTyInfo, string>();
            var school = await schoolRepository.FindAsync(
                c => c.SchoolRegistrationOfficerId == schoolId,
                ct);
            if (school == null)
            {
                return new PrepareEnvironmentForStudentAssessmentReturnDto
                {
                    Message = "Unable to complete the process."
                };
            }

            school.SystemImplementationType = FreePlugIn.Encrypt(FreePlugIn.GetUniqueSystemId() + ":0", true);
            schoolRepository.Update(school);

            var yearlyAndTermlyAssessmentStatusRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
            var priorTerms = await yearlyAndTermlyAssessmentStatusRepository.WhereAsync(
                t => t.Session!.SchoolInfoId == schoolId,
                ct,
                t => t.Session!);

            var lastYearlyAndTermlyAssessmentStatus = priorTerms
                .OrderByDescending(g => g.CreatedDate)
                .ThenBy(g => g.Id)
                .FirstOrDefault();

            if (lastYearlyAndTermlyAssessmentStatus == null)
            {
                return new PrepareEnvironmentForStudentAssessmentReturnDto
                {
                    Message = "Unable to determine the previous assessment status."
                };
            }

            var currentTerm = NextTermStatus(lastYearlyAndTermlyAssessmentStatus.TermStatus);
            var yearlyAndTermlyAssessmentModel = new TxYearlyAndTermlyStatus
            {
                Id = Guid.NewGuid().ToString(),
                TermStatus = currentTerm,
                SessionId = sessionId,
                CreatedDate = DateTime.UtcNow,
                TermlyAccessmentStatus = TermlyAccessmentStatus.Current,
                CasCategory = lastYearlyAndTermlyAssessmentStatus.CasCategory,
                PerCasValue = lastYearlyAndTermlyAssessmentStatus.PerCasValue,
                MinPassMark = lastYearlyAndTermlyAssessmentStatus.MinPassMark,
                PrincipalId = lastYearlyAndTermlyAssessmentStatus.PrincipalId,
                HeadmasterId = lastYearlyAndTermlyAssessmentStatus.HeadmasterId
            };

            yearlyAndTermlyAssessmentStatusRepository.Add(yearlyAndTermlyAssessmentModel);
            await _uow.SaveChangesAsync(ct);

            var teacherClassArmAssignmentRepository = _uow.Repository<TeacherClassArmTxAssignment>();
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

            var studentDetailRepository = _uow.Repository<TxStudentDetail>();
            AddStudentAcademicProgressHistory(allStudents, studentDetailRepository, yearlyAndTermlyAssessmentModel.Id);

            // Legacy also forces Returning on every student after progress-history insert.
            foreach (var eachStudent in allStudents)
            {
                eachStudent.Status = StudentStatus.Returning;
                studentDetailRepository.Update(eachStudent);
            }

            await _uow.SaveChangesAsync(ct);

            return new PrepareEnvironmentForStudentAssessmentReturnDto
            {
                YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentModel.Id,
                CurrentTermStatus = currentTerm,
                Message = "Success"
            };
        }

        /// <summary>
        /// Legacy CreateNewTermForNewAssessment2nd — close completed term and open the next.
        /// Returns "TermStatus:YearlyAndTermlyStatusId".
        /// </summary>
        private async Task<string> CreateNewTermForNewAssessment2ndAsync(
            string sessionId,
            TxYearlyAndTermlyStatus yearlyAndTermlyAssessmentStatus,
            string schoolId,
            TermStatus lastTermsName,
            CancellationToken ct)
        {
            var yearlyAndTermlyAssessmentStatusRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
            var teacherClassArmAssignmentRepository = _uow.Repository<TeacherClassArmTxAssignment>();

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

            await _uow.SaveChangesAsync(ct);

            var currentTerm = NextTermStatus(lastTermsName);
            var yearlyAndTermlyAssessmentModel = new TxYearlyAndTermlyStatus
            {
                Id = Guid.NewGuid().ToString(),
                TermStatus = currentTerm,
                SessionId = sessionId,
                CreatedDate = DateTime.UtcNow,
                TermlyAccessmentStatus = TermlyAccessmentStatus.Current,
                CasCategory = yearlyAndTermlyAssessmentStatus.CasCategory,
                PerCasValue = yearlyAndTermlyAssessmentStatus.PerCasValue,
                MinPassMark = yearlyAndTermlyAssessmentStatus.MinPassMark,
                PrincipalId = yearlyAndTermlyAssessmentStatus.PrincipalId,
                HeadmasterId = yearlyAndTermlyAssessmentStatus.HeadmasterId
            };

            yearlyAndTermlyAssessmentStatusRepository.Add(yearlyAndTermlyAssessmentModel);
            await _uow.SaveChangesAsync(ct);

            return currentTerm + ":" + yearlyAndTermlyAssessmentModel.Id;
        }

        private async Task<PrepareEnvironmentForStudentAssessmentReturnDto> CreateOrUpdateNewAssessment(
            List<TxStudentDetail> allStudents,
            IGenericRepository<StudenXtAccessment> studentAssessmentRepository,
            string schoolId,
            string yearlyAndTermlyAssessmentStatusId,
            CancellationToken ct = default)
        {
            if (allStudents.Count == 0)
            {
                return new PrepareEnvironmentForStudentAssessmentReturnDto
                {
                    Message = "Sorry! No Fresher/Returning student was found in the selected class.",
                    YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
                    TotalStudentListToPdf = new List<TotalStudentListToPdfDto>()
                };
            }

            // Defense in depth: never create/update assessments for Previous students.
            allStudents = allStudents
                .Where(s => s.Status == StudentStatus.Fresher || s.Status == StudentStatus.Returning)
                .ToList();
            if (allStudents.Count == 0)
            {
                return new PrepareEnvironmentForStudentAssessmentReturnDto
                {
                    Message = "Sorry! No Fresher/Returning student was found in the selected class.",
                    YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
                    TotalStudentListToPdf = new List<TotalStudentListToPdfDto>()
                };
            }

            var subjectRepository = _uow.Repository<RxSubject>();
            var classSubjectArmAssignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var subjectStudentAssignmentRepository = _uow.Repository<SubjectStudentTxAssignment>();
            var affectiveAndPsychomotorAssessmentRepository = _uow.Repository<AffectiveAndPsychomoTxtorAssessment>();

            var currentSubjects = await subjectRepository.WhereAsync(
                s =>
                    s.SubjectCategory!.SchoolId == schoolId &&
                    s.TimeStatus == TimeStatus.Current,
                ct,
                s => s.SubjectCategory!);

            var teacherAssignments = await classSubjectArmAssignmentRepository.WhereAsync(
                t => t.Employee!.SchoolId == schoolId,
                ct);

            var electiveAssignments = await subjectStudentAssignmentRepository.WhereAsync(
                d =>
                    d.StudentDetail!.SchoolInfoId == schoolId &&
                    d.Subject!.TimeStatus == TimeStatus.Current,
                ct,
                d => d.StudentDetail!,
                d => d.Subject!,
                d => d.Subject!.SubjectCategory!);

            var currentAssessments = await studentAssessmentRepository.WhereAsync(
                a =>
                    a.Employee!.SchoolId == schoolId &&
                    a.YearlyAndTermlyStatus!.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                a => a.Student!,
                a => a.Employee!,
                a => a.Subject!.SubjectCategory!,
                a => a.YearlyAndTermlyStatus!);

            var teacherAssignmentsByKey = teacherAssignments
                .GroupBy(a => CreateLookupKey(a.ArmId, a.SubjectId), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().EmployeeId, StringComparer.OrdinalIgnoreCase);

            var electiveAssignmentsByStudent = electiveAssignments
                .GroupBy(a => a.StudentId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(a => MapSubject(a.Subject!))
                        .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                        .Select(s => s.First())
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);

            var currentAssessmentsByKey = currentAssessments
                .GroupBy(a => CreateLookupKey(a.StudentId, a.SubjectId), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var seniorSubjectCache = new Dictionary<ClassCategoryStatus, List<SubjectDto>>();
            var basicSubjectCache = new Dictionary<CurrentClass, List<SubjectDto>>();

            foreach (var eachClass in allStudents
                         .Where(s => s.ClassArm != null)
                         .GroupBy(s => s.ClassArm!.ClassId))
            {
                var firstStudent = eachClass.First();

                if (firstStudent.CurrentClass is CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3)
                {
                    foreach (var eachDept in eachClass.GroupBy(s => s.ClassCategory))
                    {
                        var classSubjectCollection = GetSeniorCurrentSubjects(
                            currentSubjects,
                            eachDept.Key,
                            seniorSubjectCache);

                        var returnString = ProceedFromHereForUpdateSeniorStudent(
                            eachDept,
                            classSubjectCollection,
                            yearlyAndTermlyAssessmentStatusId,
                            studentAssessmentRepository,
                            teacherAssignmentsByKey,
                            electiveAssignmentsByStudent,
                            currentAssessmentsByKey);

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
                    var classSubjectCollection = GetBasicCurrentSubjects(
                        currentSubjects,
                        firstStudent.ClassArm!.Classs!.ClassName,
                        basicSubjectCache);

                    var returnString = ProceedFromHereForUpdateBasicStudents(
                        eachClass,
                        classSubjectCollection,
                        yearlyAndTermlyAssessmentStatusId,
                        studentAssessmentRepository,
                        schoolId,
                        teacherAssignmentsByKey,
                        electiveAssignmentsByStudent,
                        currentAssessmentsByKey);

                    if (returnString != "Succeed")
                    {
                        return new PrepareEnvironmentForStudentAssessmentReturnDto
                        {
                            Message = returnString
                        };
                    }
                }
            }

            foreach (var item in currentAssessments.Where(
                         t => t.Student!.Status == StudentStatus.Previous &&
                              t.Student!.SchoolInfoId == schoolId))
            {
                if (Math.Abs(item.Ca1 + item.Ca2 + item.Ca3 + item.Exams) < 0.5)
                {
                    await studentAssessmentRepository.DeleteAsync(item.Id, ct);
                }
            }

            foreach (var item in currentAssessments.Where(
                         t => t.Subject!.TimeStatus == TimeStatus.Previous &&
                              t.Subject!.SubjectCategory!.SchoolId == schoolId))
            {
                if (Math.Abs(item.Ca1 + item.Ca2 + item.Ca3 + item.Exams) < 0.5)
                {
                    await studentAssessmentRepository.DeleteAsync(item.Id, ct);
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
                        await affectiveAndPsychomotorAssessmentRepository.DeleteAsync(psychomotorAssessment.Id, ct);
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

        private string ProceedFromHereForUpdateSeniorStudent(
            IGrouping<ClassCategoryStatus, TxStudentDetail> eachClass,
            List<SubjectDto> classSubjectCollection,
            string yearlyAndTermlyAssessmentStatusId,
            IGenericRepository<StudenXtAccessment> studentAssessmentRepository,
            IReadOnlyDictionary<string, string> teacherAssignmentsByKey,
            IReadOnlyDictionary<string, List<SubjectDto>> electiveAssignmentsByStudent,
            IDictionary<string, StudenXtAccessment> currentAssessmentsByKey)
        {
            foreach (var eachStudent in eachClass)
            {
                var subjectsForStudent = MergeStudentSubjects(
                    classSubjectCollection,
                    electiveAssignmentsByStudent,
                    eachStudent.Id);

                foreach (var eachSubject in subjectsForStudent)
                {
                    var teacherKey = CreateLookupKey(eachStudent.ClassArmId, eachSubject.Id);
                    if (!teacherAssignmentsByKey.TryGetValue(teacherKey, out var teacherId))
                    {
                        return "'" + eachSubject.SubjectName + "' has not been assigned to a teacher in " +
                               (eachStudent.ClassArm?.Classs?.ClassLabel ?? string.Empty) +
                               (eachStudent.ClassArm?.ArmName.ToString() ?? string.Empty) + ".";
                    }

                    var assessmentKey = CreateLookupKey(eachStudent.Id, eachSubject.Id);
                    if (currentAssessmentsByKey.TryGetValue(assessmentKey, out var eachStudentAssessment))
                    {
                        eachStudentAssessment.EmployeeId = teacherId;
                        studentAssessmentRepository.Update(eachStudentAssessment);
                    }
                    else
                    {
                        var assessment = new StudenXtAccessment
                        {
                            Id = Guid.NewGuid().ToString(),
                            EmployeeId = teacherId,
                            SubjectId = eachSubject.Id,
                            StudentId = eachStudent.Id,
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

            return "Succeed";
        }

        private string ProceedFromHereForUpdateBasicStudents(
            IGrouping<string, TxStudentDetail> eachClass,
            List<SubjectDto> classSubjectCollection,
            string yearlyAndTermlyAssessmentStatusId,
            IGenericRepository<StudenXtAccessment> studentAssessmentRepository,
            string schoolId,
            IReadOnlyDictionary<string, string> teacherAssignmentsByKey,
            IReadOnlyDictionary<string, List<SubjectDto>> electiveAssignmentsByStudent,
            IDictionary<string, StudenXtAccessment> currentAssessmentsByKey)
        {
            foreach (var eachArmStudents in eachClass.GroupBy(g => g.ClassArmId))
            {
                foreach (var eachStudent in eachArmStudents)
                {
                    var subjectsForStudent = eachStudent.ClassArm?.Classs?.ClassName is CurrentClass.PreNursery or CurrentClass.PlayClass
                        ? new List<SubjectDto>(classSubjectCollection)
                        : MergeStudentSubjects(classSubjectCollection, electiveAssignmentsByStudent, eachStudent.Id);

                    foreach (var eachSubject in subjectsForStudent)
                    {
                        var teacherKey = CreateLookupKey(eachStudent.ClassArmId, eachSubject.Id);
                        if (!teacherAssignmentsByKey.TryGetValue(teacherKey, out var teacherId))
                        {
                            return "'" + eachSubject.SubjectName + "' has not been assigned to a teacher in " +
                                   (eachArmStudents.First().ClassArm?.Classs?.ClassLabel ?? string.Empty) +
                                   (eachArmStudents.First().ClassArm?.ArmName.ToString() ?? string.Empty) + ".";
                        }

                        var assessmentKey = CreateLookupKey(eachStudent.Id, eachSubject.Id);
                        if (currentAssessmentsByKey.TryGetValue(assessmentKey, out var eachStudentAssessment))
                        {
                            // Legacy Find already scoped by Employee.SchoolId == schoolId via the
                            // current-assessment query; update the assigned teacher in place.
                            eachStudentAssessment.EmployeeId = teacherId;
                            studentAssessmentRepository.Update(eachStudentAssessment);
                        }
                        else
                        {
                            var assessment = new StudenXtAccessment
                            {
                                Id = Guid.NewGuid().ToString(),
                                EmployeeId = teacherId,
                                SubjectId = eachSubject.Id,
                                StudentId = eachStudent.Id,
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

        private static List<SubjectDto> GetSeniorCurrentSubjects(
            IEnumerable<RxSubject> currentSubjects,
            ClassCategoryStatus studentStatus,
            IDictionary<ClassCategoryStatus, List<SubjectDto>> cache)
        {
            if (cache.TryGetValue(studentStatus, out var cachedSubjects))
            {
                return cachedSubjects;
            }

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

            var subjects = currentSubjects
                .Where(s => s.SubjectCategory != null && allowedStatuses.Contains(s.SubjectCategory.SubjectStatus))
                .Select(MapSubject)
                .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                .Select(s => s.First())
                .ToList();

            cache[studentStatus] = subjects;
            return subjects;
        }

        private static List<SubjectDto> GetBasicCurrentSubjects(
            IEnumerable<RxSubject> currentSubjects,
            CurrentClass className,
            IDictionary<CurrentClass, List<SubjectDto>> cache)
        {
            if (cache.TryGetValue(className, out var cachedSubjects))
            {
                return cachedSubjects;
            }

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

            var subjects = currentSubjects
                .Where(s => s.SubjectCategory != null && allowedStatuses.Contains(s.SubjectCategory.SubjectStatus))
                .Select(MapSubject)
                .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                .Select(s => s.First())
                .ToList();

            cache[className] = subjects;
            return subjects;
        }

        private static List<SubjectDto> MergeStudentSubjects(
            IEnumerable<SubjectDto> baseSubjects,
            IReadOnlyDictionary<string, List<SubjectDto>> electiveAssignmentsByStudent,
            string studentId)
        {
            var mergedSubjects = new List<SubjectDto>();
            var seenSubjectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var subject in baseSubjects)
            {
                if (seenSubjectIds.Add(subject.Id))
                {
                    mergedSubjects.Add(subject);
                }
            }

            if (electiveAssignmentsByStudent.TryGetValue(studentId, out var electiveSubjects))
            {
                foreach (var subject in electiveSubjects)
                {
                    if (seenSubjectIds.Add(subject.Id))
                    {
                        mergedSubjects.Add(subject);
                    }
                }
            }

            return mergedSubjects;
        }

        private static string CreateLookupKey(string firstPart, string secondPart)
        {
            return firstPart + "::" + secondPart;
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

        private static TotalStudentListToPdfDto MapTotalStudentListToPdf(TxStudentDetail student)
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
