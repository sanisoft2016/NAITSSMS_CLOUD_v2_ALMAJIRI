using AutoMapper;
using CustRewardMgtSys.Application.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.Service
{
    public class UserRegistrationService: IUserRegistrationService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUnitOfWork _uow;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMapper _mapper;
        public UserRegistrationService(
            IMapper mapper,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            
             IUnitOfWork uow,
            IConfiguration configuration,
            ILogger<UserService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _uow = uow;
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _configuration = configuration;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }
        // Method 98: Update Student Details
        public async Task<string> UpdateStudentDetails(StudentDetailsDto model, ArmName arm, CancellationToken ct = default)
        {
            var studentRepository = _uow.Repository<TxStudentDetail>();
            var userRepository = _uow.Repository<ApplicationUser, string>();

            var student = await studentRepository.FindAsync(x => x.UserId == model.UserId, ct, s => s.User);
            if (student == null)
                return "Student not found";

            var previousCategory = ResolveEffectiveCategory(student.CurrentClass, student.ClassCategory);
            var previousArmId = student.ClassArmId;
            var newCategory = ResolveEffectiveCategory(model.CurrentClass, model.ClassCategory);
            var newArmId = string.IsNullOrWhiteSpace(model.ClassArmId) ? previousArmId : model.ClassArmId.Trim();

            // Update student details
            student.IdNo = model.IdNo;
            student.DateOfBirth = model.DateOfBirth;
            student.PlaceOfBirth = model.PlaceOfBirth;
            student.Religion = model.Religion;
            student.BloodGroup = model.BloodGroup;
            student.DateOfAdmsn = model.DateOfAdmsn;
            student.TransferedFromWhere = model.TransferedFromWhere;
            student.StartClass = model.StartClass;
            student.CurrentClass = model.CurrentClass;
            student.Passport = model.Passport;
            student.ClassArmId = newArmId;
            student.GuardiansName = model.GuardiansName;
            student.Relationship = model.Relationship;
            student.GuardianAddress = model.GuardianAddress;
            student.Occupation = model.Occupation;
            student.MobileNumber = model.MobileNumber;
            student.HomeNumber = model.HomeNumber;
            student.EmailAddress = model.EmailAddress;
            student.MailingAddress = model.MailingAddress;
            student.Country = model.Country;
            student.ClassCategory = newCategory;
            student.Status = model.Status;

            // Update user details
            if (student.User != null)
            {
                student.User.FirstName = model.FirstName;
                student.User.MiddleName = model.MiddleName;
                student.User.SurName = model.SurName;
                student.User.Gender = model.Gender;
                student.User.PhoneNumber = model.StudentMobileNo;
                userRepository.Update(student.User);
            }

            studentRepository.Update(student);

            if (student.Status == StudentStatus.Previous)
            {
                await ClearUnscoredCurrentTermAssessmentsForStudentAsync(student.UserId, ct);
                await _uow.SaveChangesAsync(ct);
                return "Success";
            }

            var syncResult = await SyncStudentAssessmentsOnCategoryOrArmChangeAsync(
                student.UserId,
                previousCategory,
                newCategory,
                previousArmId,
                newArmId,
                ct);
            if (!string.IsNullOrWhiteSpace(syncResult))
            {
                return syncResult;
            }

            await _uow.SaveChangesAsync(ct);

            return "Success";
        }

        public async Task<int> GetSchoolSerialId(CancellationToken ct = default)
        {
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var schoolInfoRepository = _uow.Repository<SchoolTyInfo>();

            var schoolInfo = await schoolInfoRepository.FindAsync(
                s => s.SchoolRegistrationOfficerId == schoolId, ct);

            return schoolInfo?.SsNo ?? 0;
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

        public async Task<bool> IsSubjectAlreadyExist(string subjectName, CancellationToken ct = default)
        {
            var subjectRepository = _uow.Repository<RxSubject>();
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);

            var subject = await subjectRepository.FindAsync(
                sr => sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                      sr.SubjectCategory.SchoolId == schoolId, ct, s => s.SubjectCategory);

            return subject != null;
        }
        public async Task<bool> IsClassAlreadyExist(CurrentClass className, CancellationToken ct = default)
        {
            var classRepository = _uow.Repository<RxClasss>();
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var classEntity = await classRepository.FindAsync(
                s => s.ClassName == className && s.SchoolInfoId == schoolId, ct);
            return classEntity != null;
        }
        // Method 38
        public async Task<bool> IsClassArmAlreadyExist(ArmName armName, string classId, CancellationToken ct = default)
        {
            var classArmRepository = _uow.Repository<AxClassArm>();
            var arm = await classArmRepository.FindAsync(
                s => s.ArmName == armName && s.ClassId == classId, ct);
            return arm != null;
        }
        public async Task<bool> DoesThisUserNameExist(string userName, CancellationToken ct = default)
        {
            var userRepository = _uow.Repository<ApplicationUser, string>();

            var existingUser = await userRepository.FindAsync(
                u => u.UserName.Trim().ToLower() == userName.Trim().ToLower(), ct);

            return existingUser != null;
        }

        public async Task<byte> ValidationChecks(
          string identificationNo,
          CurrentClass currentClass,
          ArmName arm,
          ClassCategoryStatus classCategory,
          CancellationToken ct = default)
        {
            var classArmRepository = _uow.Repository<AxClassArm>();
            var classRepository = _uow.Repository<RxClasss>();
            var studentDetailRepository = _uow.Repository<TxStudentDetail>();
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var normalizedIdentificationNo = identificationNo.Trim().ToLower();

            var existingStudent = await studentDetailRepository.FindAsync(
                s => s.IdNo.Trim().ToLower() == normalizedIdentificationNo && s.SchoolInfoId == schoolId,
                ct);

            if (existingStudent != null)
            {
                return 1;
            }

            // NOTE: Cannot use Task.WhenAll with DbContext - it would run queries concurrently
            // causing "second operation started before previous completed" error.
            // DbContext must process queries sequentially.
            var classExists = await classRepository.FindAsync(
                c => c.SchoolInfoId == schoolId && c.ClassName == currentClass,
                ct);

            var armExists = await classArmRepository.FindAsync(
                a => a.ArmName == arm &&
                     a.Classs != null &&
                     a.Classs.ClassName == currentClass &&
                     a.Classs.SchoolInfoId == schoolId,
                ct);

            if (classExists == null)
            {
                return 2;
            }

            if (armExists == null)
            {
                return 3;
            }

            var isSeniorClass = currentClass == CurrentClass.Ss1 ||
                                currentClass == CurrentClass.Ss2 ||
                                currentClass == CurrentClass.Ss3;

            var isSeniorCategory = classCategory == ClassCategoryStatus.Science ||
                                   classCategory == ClassCategoryStatus.Art ||
                                   classCategory == ClassCategoryStatus.Commercial;

            if (isSeniorClass)
            {
                if (!isSeniorCategory)
                {
                    return 5;
                }
            }
            else if (isSeniorCategory)
            {
                return 5;
            }

            return 4;
        }

        // Method 95
        public async Task<StudentDetailsDto> GetStudentById(string id, CancellationToken ct = default)
        {
            var studentRepository = _uow.Repository<TxStudentDetail>();
            var student = await studentRepository.FindAsync(
                a => a.UserId == id, ct, r => r.ClassArm.Classs, r => r.User);

            if (student == null)
            {
                var result = new StudentDetailsDto();
                return result;
            }
            var studentDto = _mapper.Map<StudentDetailsDto>(student);
            studentDto.FirstName = student.User.FirstName;
            studentDto.MiddleName = student.User.MiddleName;
            studentDto.SurName = student.User.SurName;
            studentDto.EmailAddress = student.EmailAddress;
            studentDto.StudentMobileNo = student.User.PhoneNumber;
            studentDto.Gender = student.User.Gender;
            studentDto.UserId = student.UserId;

            return studentDto;
        }

        public async Task<bool> CreateTeacherRecord(string userId, UserDto2nd model, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId) || model == null || string.IsNullOrWhiteSpace(model.UserId))
            {
                return false;
            }

            var teacherRepository = _uow.Repository<TxEmployee>();

            var teacher = new TxEmployee
            {
                Id = Guid.NewGuid().ToString(),
                FullAddress = model.Addresses?.Trim() ?? string.Empty,
                DateOfBirth = model.DateOfBirth,
                UserId = model.UserId.Trim(),
                SignatureImageString = model.SignatureImageString?.Trim() ?? string.Empty,
                TimeStatus = model.TimeStatus,
                EmployeeStatus = model.EmployeeStatus,
                SchoolId = userId,
                EmployeeIncomeStructureStatus = EmployeeIncomeStructureStatus.HasNotSet,
                EmployeeIdNo = model.EmployeeIdNo?.Trim() ?? string.Empty,
                Decipline = string.Empty,
                EmployeeQualificationStatus = EmployeeQualificationStatus.Others,
            };

            teacherRepository.Add(teacher);
            await _uow.SaveChangesAsync(ct);
            return true;
        }

        public async Task<byte> CreatStudent(StudentDetailsDto model, ArmName armName, CurrentClass currentClass, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(model);

            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var classArmRepository = _uow.Repository<AxClassArm>();
            var studentDetailRepository = _uow.Repository<TxStudentDetail>();
            var yearlyAndTermlyStatusRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
            var studentAcademicProgressHistoryRepository = _uow.Repository<StudentAcademicTxProgressHistory>();
            var studentAssessmentRepository = _uow.Repository<StudenXtAccessment>();
            var teacherSubjectArmAssignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();

            var arm = await classArmRepository.FindAsync(
                a => a.Classs != null &&
                     a.Classs.ClassName == currentClass &&
                     a.ArmName == armName &&
                     a.Classs.SchoolInfoId == schoolId,
                ct,
                a => a.Classs!);

            if (arm?.Classs is null)
            {
                return 0;
            }

            if (!IsSeniorClass(currentClass))
            {
                model.ClassCategory = GetClassCategoryStatus(currentClass);
            }

            var yearlyAndTermlyStatus = await yearlyAndTermlyStatusRepository.FindAsync(
                x => x.Session != null &&
                     x.Session.SchoolInfoId == schoolId &&
                     x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                x => x.Session!);

            if (yearlyAndTermlyStatus is null || string.IsNullOrWhiteSpace(yearlyAndTermlyStatus.Id))
            {
                return 1;
            }

            model.DateOfBirth = NormalizeToUtc(model.DateOfBirth);
            model.DateOfAdmsn = model.DateOfAdmsn == default ? DateTime.UtcNow : NormalizeToUtc(model.DateOfAdmsn);
            if (model.DateOfDischarge.HasValue)
            {
                model.DateOfDischarge = NormalizeToUtc(model.DateOfDischarge.Value);
            }

            model.CurrentClass = arm.Classs.ClassName;
            model.StartClass = arm.Classs.ClassLabel;
            model.ClassArmId = arm.Id;
            model.SchoolInfoId = schoolId;
            model.AdmisnTermId = string.IsNullOrWhiteSpace(model.AdmisnTermId)
                ? yearlyAndTermlyStatus.Id
                : model.AdmisnTermId.Trim();

            if (string.IsNullOrWhiteSpace(model.UserId))
            {
                model.UserId = string.IsNullOrWhiteSpace(model.Id)
                    ? Guid.NewGuid().ToString()
                    : model.Id.Trim();
            }
            else
            {
                model.UserId = model.UserId.Trim();
            }

            model.Id = model.UserId;
            SanitizeNullableStringsForPersistence(model);

            var studentEntity = MapStudentDetail(model);
            studentDetailRepository.Add(studentEntity);

            var studentAcademicProgressHistory = new StudentAcademicTxProgressHistory
            {
                Id = Guid.NewGuid().ToString(),
                YearlyAndTermlyStatusId = yearlyAndTermlyStatus.Id,
                ClassId = arm.ClassId,
                StudentId = studentEntity.UserId,
                ClassCategory = model.ClassCategory
            };
            studentAcademicProgressHistoryRepository.Add(studentAcademicProgressHistory);

            var implementationType = await GetSystemImplementationStatus(ct);
            if (!ShouldCreateStudentAssessments(implementationType))
            {
                return 0;
            }

            var createdAssessments = new List<StudenXtAccessment>();

            void RollbackPendingStudentArtifacts()
            {
                foreach (var assessment in createdAssessments)
                {
                    studentAssessmentRepository.Delete(assessment);
                }

                studentAcademicProgressHistoryRepository.Delete(studentAcademicProgressHistory);
                studentDetailRepository.Delete(studentEntity);
            }

            StudenXtAccessment CreateAssessment(string targetStudentId, string teacherId, string subjectId)
            {
                return new StudenXtAccessment
                {
                    Id = Guid.NewGuid().ToString(),
                    ClassArmId = model.ClassArmId,
                    StudentId = targetStudentId,
                    EmployeeId = teacherId,
                    SubjectId = subjectId,
                    LastUpdateDate = DateTime.UtcNow,
                    Ca1 = 0,
                    Ca2 = 0,
                    Ca3 = 0,
                    Exams = 0,
                    YearlyAndTermlyStatusId = yearlyAndTermlyStatus.Id
                };
            }

            var studentSubjectList = IsSeniorCategory(model.ClassCategory)
                ? await SeniorClassesSubjectListCurrent(model.ClassCategory, ct)
                : await BasicClassesSubjectListCurrent(model.CurrentClass, ct);

            if (!studentSubjectList.Any())
            {
                return 0;
            }

            foreach (var eachSubject in studentSubjectList)
            {
                var classTeacherId = (await teacherSubjectArmAssignmentRepository.FindAsync(
                    x => x.ArmId == model.ClassArmId && x.SubjectId == eachSubject.Id,
                    ct))?.EmployeeId;

                var noOfStudentInTheSelectedArm = IsSeniorCategory(model.ClassCategory)
                    ? await studentDetailRepository.CountAsync(
                        x =>
                            x.ClassArmId == model.ClassArmId &&
                            x.ClassCategory == model.ClassCategory &&
                            x.Status != StudentStatus.Previous,
                        ct)
                    : await studentDetailRepository.CountAsync(
                        x =>
                            x.ClassArmId == model.ClassArmId &&
                            x.Status != StudentStatus.Previous,
                        ct);

                if (noOfStudentInTheSelectedArm == 0)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(classTeacherId))
                {
                    RollbackPendingStudentArtifacts();
                    return 2;
                }

                if (noOfStudentInTheSelectedArm == 1)
                {
                    // Only seed the existing classmate when they are still Fresher/Returning.
                    var firstStudent = IsSeniorCategory(model.ClassCategory)
                        ? await studentDetailRepository.FindAsync(
                            x =>
                                x.ClassArmId == model.ClassArmId &&
                                x.ClassCategory == model.ClassCategory &&
                                x.Status != StudentStatus.Previous,
                            ct)
                        : await studentDetailRepository.FindAsync(
                            x =>
                                x.ClassArmId == model.ClassArmId &&
                                x.Status != StudentStatus.Previous,
                            ct);

                    if (!string.IsNullOrWhiteSpace(firstStudent?.UserId) &&
                        !string.Equals(firstStudent.UserId, studentEntity.UserId, StringComparison.OrdinalIgnoreCase))
                    {
                        var firstStudentAssessment = CreateAssessment(firstStudent.UserId, classTeacherId, eachSubject.Id);
                        studentAssessmentRepository.Add(firstStudentAssessment);
                        createdAssessments.Add(firstStudentAssessment);
                    }
                }

                var newStudentAssessment = CreateAssessment(studentEntity.UserId, classTeacherId, eachSubject.Id);
                studentAssessmentRepository.Add(newStudentAssessment);
                createdAssessments.Add(newStudentAssessment);
            }

            return 0;
        }

        public async Task<string> VerifyTokenUniqueness(string token = "", CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(token))
            {
                token = Guid.NewGuid().ToString();
            }
            var schoolInfoRepository = _uow.Repository<SchoolTyInfo>();
            while (await schoolInfoRepository.FindAsync(s => s.SmsApiToken == token, ct) != null)
            {
                token = Guid.NewGuid().ToString();
            }
            return token;
        }

        public async Task<ReturnCreateSchoolRegistrarDto> CreateSchoolRegistrar(UserDto2nd model, CancellationToken ct = default)
        {
            //ArgumentNullException.ThrowIfNull(model);
            //if (string.IsNullOrWhiteSpace(model.UserId))
            //    throw new ArgumentException("UserId is required.", nameof(model));

            //var principal = _httpContextAccessor.HttpContext?.User;
            //if (principal?.Identity?.IsAuthenticated != true)
            //    throw new UnauthorizedAccessException("User is not authenticated.");

            var schoolRegistrationOfficerRepository = _uow.Repository<SchoolRegistrationOfficer>();
            var agentRepository = _uow.Repository<Agent>();
            var schoolRepository = _uow.Repository<SchoolTyInfo>(); 

                var subjectCategoriesRepository = _uow.Repository<SubjectRxCategories>();

            var existingOfficer = await schoolRegistrationOfficerRepository.FindAsync(
                x => x.UserId == model.UserId,
                ct);

            if (existingOfficer is not null)
            {
                return new ReturnCreateSchoolRegistrarDto
                {
                    CreatorStatus = CreatorStatus.SchoolAdmin, // Default value since principal is not available
                    Status = true,
                    RegistrationOfficerId = existingOfficer.UserId
                };
            }

            var principal = _httpContextAccessor.HttpContext?.User;
            var currentUserId = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            CreatorStatus creatorStatus;
            string agentUserId;

            Agent? actingAgent = null;
            if (!string.IsNullOrWhiteSpace(currentUserId))
            {
                actingAgent = await agentRepository.FindAsync(currentUserId, ct);
            }

            if (actingAgent is not null)
            {
                agentUserId = actingAgent.UserId;
                creatorStatus = principal!.IsInRole("SAOfficer")
                    ? CreatorStatus.SupperAdmin
                    : principal.IsInRole("RemoteAgents")
                        ? CreatorStatus.RemoteAgent
                        : CreatorStatus.LocalAgent;
            }
            else
            {
                // Self-registration (anonymous or non-agent caller): attribute to the platform onboarding agent.
                actingAgent = await ResolveDefaultOnboardingAgentAsync(agentRepository, ct);
                if (actingAgent is null || string.IsNullOrWhiteSpace(actingAgent.UserId))
                {
                    throw new InvalidOperationException(
                        "School self-registration is not available because no onboarding agent (SAOfficer) is configured.");
                }

                agentUserId = actingAgent.UserId;
                creatorStatus = CreatorStatus.SchoolAdmin;
            }

            var officer = new SchoolRegistrationOfficer
            {
                UserId = model.UserId,
                DateOfBirth = model.DateOfBirth,
                FullAddress = model.Addresses ?? string.Empty,
                SignatureImageString = model.SignatureImageString ?? string.Empty,
                TimeStatus = model.TimeStatus,
                AccessStatus = model.AccessStatus,
                AgentUserId = agentUserId
            };

            schoolRegistrationOfficerRepository.Add(officer);

            try
            {
                // Legacy CreateSchoolDefaultByProprietor: serial no, SMS token/key, implementation
                // types and an opening SMS balance. Without the balance row and a key matching this
                // server's system id, SMS balance and subscription renewal fail for the new school.
                var existingSchools = await schoolRepository.GetAllAsync(ct);
                var nextSerialNo = (short)(existingSchools.Select(s => s.SsNo).DefaultIfEmpty((short)0).Max() + 1);

                var systemId = FreePlugIn.GetUniqueSystemId();
                var token = string.IsNullOrWhiteSpace(model.SmsApiToken) ? Guid.NewGuid().ToString() : model.SmsApiToken.Trim();
                var key = string.IsNullOrWhiteSpace(model.SmsApiKey) ? systemId : model.SmsApiKey.Trim();

                var schoolModel = new SchoolTyInfo
                {
                    SsNo = nextSerialNo,
                    SchoolRegistrationOfficerId = officer.UserId,
                    SchoolName = model.SchoolName,
                    SchoolAdminAccountantStatus = SchoolAdminAccountantStatus.None,
                    SmsApiToken = FreePlugIn.Encrypt(token, true),
                    SmsApiKey = FreePlugIn.Encrypt(key, true),
                    SystemImplementationType = FreePlugIn.Encrypt(systemId + ":0", true),
                    FeatureImplementationType = FreePlugIn.Encrypt(key + ":0", true),
                };
                schoolRepository.Add(schoolModel);

                const float openingSmsAmount = 10f;
                _uow.Repository<SmsPurchase>().Add(new SmsPurchase
                {
                    Id = Guid.NewGuid().ToString(),
                    SchoolSmsId = officer.UserId,
                    AmountPurchase = openingSmsAmount,
                    AgentUserId = agentUserId,
                    BalB4Purchase = 0,
                    PaymentDate = DateTime.UtcNow,
                    TimeStatus = TimeStatus.Current,
                });
                _uow.Repository<SmsPaymentBalance>().Add(new SmsPaymentBalance
                {
                    SchoolSmsId = officer.UserId,
                    Amount = openingSmsAmount,
                    PricePerSms = 2.7f,
                });

                SubjectStatus[] ManagedSubjectStatuses =
                {
                    SubjectStatus.PlayClass,
                    SubjectStatus.PreNursery,
                    SubjectStatus.Nursery,
                    SubjectStatus.General,
                    SubjectStatus.Primary,
                    SubjectStatus.Jss,
                    SubjectStatus.AllSeniors,
                    SubjectStatus.Science,
                    SubjectStatus.Art,
                    SubjectStatus.Commercial,
                    SubjectStatus.Elective,
                };
                var subjectCategoryRepository = new List<SubjectRxCategories>();

                foreach (var status in ManagedSubjectStatuses)
                {
                    subjectCategoryRepository.Add(new SubjectRxCategories
                    {
                        Id = Guid.NewGuid().ToString(),
                        SubjectStatus = status,
                        SchoolId = model.UserId,
                        SubjectStatusLabel = string.Empty,
                    });
                }
                subjectCategoriesRepository.AddRange(subjectCategoryRepository);
            }

            catch (Exception ex)
            {
                throw;
            }

            return new ReturnCreateSchoolRegistrarDto
            {
                CreatorStatus = creatorStatus,
                Status = true,
                RegistrationOfficerId = officer.UserId
            };
        }

        private static async Task<Agent?> ResolveDefaultOnboardingAgentAsync(
            IGenericRepository<Agent> agentRepository,
            CancellationToken ct)
        {
            var preferredUserNames = new[] { "SAOFFICER", "COMMISSION.OFFICER" };
            foreach (var userName in preferredUserNames)
            {
                var agent = await agentRepository.FindAsync(
                    x => x.User != null && x.User.NormalizedUserName == userName,
                    ct,
                    x => x.User!);
                if (agent is not null)
                {
                    return agent;
                }
            }

            var agents = await agentRepository.GetAllAsync(ct);
            return agents.FirstOrDefault();
        }

        private async Task<string?> SyncStudentAssessmentsOnCategoryOrArmChangeAsync(
            string studentId,
            ClassCategoryStatus previousCategory,
            ClassCategoryStatus newCategory,
            string previousArmId,
            string newArmId,
            CancellationToken ct)
        {
            var studentDetail = await _uow.Repository<TxStudentDetail>().FindAsync(
                s => s.UserId == studentId,
                ct);
            if (studentDetail == null || studentDetail.Status == StudentStatus.Previous)
            {
                return null;
            }

            var categoryChanged = previousCategory != newCategory;
            var armChanged = !string.Equals(previousArmId, newArmId, StringComparison.OrdinalIgnoreCase);

            if (!categoryChanged && !armChanged)
            {
                return null;
            }

            var implementationType = await GetSystemImplementationStatus(ct);
            if (!ShouldCreateStudentAssessments(implementationType))
            {
                return null;
            }

            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var yearlyAndTermlyStatusRepository = _uow.Repository<TxYearlyAndTermlyStatus>();
            var currentTerm = await yearlyAndTermlyStatusRepository.FindAsync(
                x => x.Session != null &&
                     x.Session.SchoolInfoId == schoolId &&
                     x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                x => x.Session!);

            if (currentTerm is null || string.IsNullOrWhiteSpace(currentTerm.Id))
            {
                return categoryChanged
                    ? "Unable to resolve the current term. Create/activate a session term before changing class category."
                    : null;
            }

            var assessmentRepository = _uow.Repository<StudenXtAccessment>();
            var teacherSubjectArmAssignmentRepository = _uow.Repository<TeachTxerSubjectArmAssignment>();
            var subjectRepository = _uow.Repository<RxSubject>();
            var termId = currentTerm.Id;

            var currentAssessments = await assessmentRepository.WhereAsync(
                s => s.StudentId == studentId && s.YearlyAndTermlyStatusId == termId,
                ct,
                s => s.Subject!,
                s => s.Subject!.SubjectCategory!);

            var deletedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var previousIsSenior = IsSeniorCategory(previousCategory);
            var newIsSenior = IsSeniorCategory(newCategory);

            if (categoryChanged)
            {
                if (previousIsSenior && newIsSenior)
                {
                    // Senior → senior: delete ONLY old department subjects.
                    // Keep AllSeniors / General / Elective.
                    var previousDepartmentStatus = ToDepartmentSubjectStatus(previousCategory);
                    if (previousDepartmentStatus.HasValue)
                    {
                        foreach (var assessment in currentAssessments.Where(a =>
                                     a.Subject?.SubjectCategory?.SubjectStatus == previousDepartmentStatus.Value))
                        {
                            assessmentRepository.Delete(assessment);
                            deletedIds.Add(assessment.Id);
                        }
                    }
                }
                else
                {
                    // Basic-class transitions (legacy UpdateStudentDetails), but never wipe General/Elective.
                    foreach (var assessment in currentAssessments)
                    {
                        var status = assessment.Subject?.SubjectCategory?.SubjectStatus;
                        if (!status.HasValue || IsPreservedSharedSubjectStatus(status.Value))
                        {
                            continue;
                        }

                        var shouldDelete = false;
                        if (newCategory is ClassCategoryStatus.PlayClass or ClassCategoryStatus.PreNursery)
                        {
                            // Legacy wiped all current-term rows for PlayClass/PreNursery targets.
                            shouldDelete = true;
                        }
                        else if (!previousIsSenior)
                        {
                            // Legacy "ExistingValues" (student was basic): wiped all current-term rows.
                            shouldDelete = true;
                        }
                        else
                        {
                            // Senior → basic: legacy deleted previous category subjects (not AllSeniors).
                            var previousDepartmentStatus = ToDepartmentSubjectStatus(previousCategory);
                            shouldDelete = previousDepartmentStatus.HasValue &&
                                           status.Value == previousDepartmentStatus.Value;
                        }

                        if (!shouldDelete)
                        {
                            continue;
                        }

                        assessmentRepository.Delete(assessment);
                        deletedIds.Add(assessment.Id);
                    }
                }
            }

            if (armChanged)
            {
                foreach (var assessment in currentAssessments.Where(a => !deletedIds.Contains(a.Id)))
                {
                    assessment.ClassArmId = newArmId;
                    assessmentRepository.Update(assessment);
                }
            }

            if (categoryChanged)
            {
                SubjectStatus[] statusesToCreate;
                if (previousIsSenior && newIsSenior)
                {
                    var newDepartmentStatus = ToDepartmentSubjectStatus(newCategory);
                    statusesToCreate = newDepartmentStatus.HasValue
                        ? new[] { newDepartmentStatus.Value }
                        : Array.Empty<SubjectStatus>();
                }
                else if (newIsSenior)
                {
                    // Basic → senior (legacy): AllSeniors + department. General/Elective already kept.
                    var newDepartmentStatus = ToDepartmentSubjectStatus(newCategory);
                    statusesToCreate = newDepartmentStatus.HasValue
                        ? new[] { SubjectStatus.AllSeniors, newDepartmentStatus.Value }
                        : new[] { SubjectStatus.AllSeniors };
                }
                else
                {
                    // Basic target (legacy): only level subjects for the new category.
                    var levelStatus = ToBasicLevelSubjectStatus(newCategory);
                    statusesToCreate = levelStatus.HasValue
                        ? new[] { levelStatus.Value }
                        : Array.Empty<SubjectStatus>();
                }

                if (statusesToCreate.Length > 0)
                {
                    var subjectsToCreate = await subjectRepository.WhereAsync(
                        s => s.SubjectCategory != null &&
                             s.SubjectCategory.SchoolId == schoolId &&
                             s.TimeStatus == TimeStatus.Current &&
                             statusesToCreate.Contains(s.SubjectCategory.SubjectStatus),
                        ct,
                        s => s.SubjectCategory!);

                    var existingSubjectIds = currentAssessments
                        .Where(a => !deletedIds.Contains(a.Id))
                        .Select(a => a.SubjectId)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    foreach (var subject in subjectsToCreate)
                    {
                        if (existingSubjectIds.Contains(subject.Id))
                        {
                            continue;
                        }

                        var classTeacherId = (await teacherSubjectArmAssignmentRepository.FindAsync(
                            x => x.ArmId == newArmId && x.SubjectId == subject.Id,
                            ct))?.EmployeeId;

                        if (string.IsNullOrWhiteSpace(classTeacherId))
                        {
                            return $"You need to assign a teacher to '{subject.SubjectName}' in the selected class arm before moving this student.";
                        }

                        assessmentRepository.Add(new StudenXtAccessment
                        {
                            Id = Guid.NewGuid().ToString(),
                            ClassArmId = newArmId,
                            StudentId = studentId,
                            EmployeeId = classTeacherId,
                            SubjectId = subject.Id,
                            LastUpdateDate = DateTime.UtcNow,
                            Ca1 = 0,
                            Ca2 = 0,
                            Ca3 = 0,
                            Exams = 0,
                            YearlyAndTermlyStatusId = termId
                        });
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Removes empty (unscored) current-term subject assessment shells for a student.
        /// Scored rows are kept for historical report/result access after Status becomes Previous.
        /// </summary>
        private async Task ClearUnscoredCurrentTermAssessmentsForStudentAsync(string studentId, CancellationToken ct)
        {
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

        private static ClassCategoryStatus ResolveEffectiveCategory(
            CurrentClass currentClass,
            ClassCategoryStatus classCategory)
        {
            return IsSeniorClass(currentClass)
                ? classCategory
                : GetClassCategoryStatus(currentClass);
        }

        private static SubjectStatus? ToDepartmentSubjectStatus(ClassCategoryStatus classCategory)
        {
            return classCategory switch
            {
                ClassCategoryStatus.Science => SubjectStatus.Science,
                ClassCategoryStatus.Art => SubjectStatus.Art,
                ClassCategoryStatus.Commercial => SubjectStatus.Commercial,
                _ => null
            };
        }

        private static SubjectStatus? ToBasicLevelSubjectStatus(ClassCategoryStatus classCategory)
        {
            return classCategory switch
            {
                ClassCategoryStatus.PlayClass => SubjectStatus.PlayClass,
                ClassCategoryStatus.PreNursery => SubjectStatus.PreNursery,
                ClassCategoryStatus.Nursery => SubjectStatus.Nursery,
                ClassCategoryStatus.Primary => SubjectStatus.Primary,
                ClassCategoryStatus.Jss => SubjectStatus.Jss,
                _ => null
            };
        }

        private static bool IsPreservedSharedSubjectStatus(SubjectStatus status)
        {
            return status is SubjectStatus.General or SubjectStatus.Elective;
        }

        private async Task<string> GetSchoolIdForSchoolRegistrationOfficer(CancellationToken ct = default)
        {
            await Task.CompletedTask;

            var httpContext = _httpContextAccessor.HttpContext;
            var userId = httpContext?.User?.FindFirstValue(ClaimTypes.PrimarySid)
                ?? httpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext?.User?.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("Unable to resolve the current user claim for school registration context.");
            }

            return userId;
        }

        private async Task<string> GetSystemImplementationStatus(CancellationToken ct = default)
        {
            var schoolId = await GetSchoolIdForSchoolRegistrationOfficer(ct);
            var schoolRepository = _uow.Repository<SchoolTyInfo>();
            var school = await schoolRepository.FindAsync(
                x => x.SchoolRegistrationOfficerId == schoolId,
                ct);

            return school?.SystemImplementationType ?? string.Empty;
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
                    s.SubjectCategory != null &&
                    s.SubjectCategory.SchoolId == schoolId &&
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
                    s.SubjectCategory != null &&
                    s.SubjectCategory.SchoolId == schoolId &&
                    s.TimeStatus == TimeStatus.Current &&
                    allowedStatuses.Contains(s.SubjectCategory.SubjectStatus),
                ct,
                s => s.SubjectCategory!);

            return subjects.Select(MapSubject).ToList();
        }

        private static void SanitizeNullableStringsForPersistence(StudentDetailsDto model)
        {
            model.Id = model.Id?.Trim() ?? string.Empty;
            model.IdNo = model.IdNo?.Trim() ?? string.Empty;
            model.FirstName = model.FirstName?.Trim() ?? string.Empty;
            model.MiddleName = model.MiddleName?.Trim() ?? string.Empty;
            model.SurName = model.SurName?.Trim() ?? string.Empty;
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
            model.StudentMobileNo = model.StudentMobileNo?.Trim() ?? string.Empty;
            model.Country = model.Country?.Trim() ?? string.Empty;
            model.UserId = model.UserId?.Trim() ?? string.Empty;
            model.SchoolInfoId = model.SchoolInfoId?.Trim() ?? string.Empty;
            model.AdmisnTermId = model.AdmisnTermId?.Trim() ?? string.Empty;
            model.ClassArmId = model.ClassArmId?.Trim() ?? string.Empty;
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

        private static bool IsSeniorClass(CurrentClass currentClass)
        {
            return currentClass == CurrentClass.Ss1 ||
                   currentClass == CurrentClass.Ss2 ||
                   currentClass == CurrentClass.Ss3;
        }

        private static bool IsSeniorCategory(ClassCategoryStatus classCategory)
        {
            return classCategory == ClassCategoryStatus.Science ||
                   classCategory == ClassCategoryStatus.Art ||
                   classCategory == ClassCategoryStatus.Commercial;
        }

        private static bool ShouldCreateStudentAssessments(string implementationType)
        {
            if (string.IsNullOrWhiteSpace(implementationType))
            {
                return false;
            }

            if (implementationType == FreePlugIn.Encrypt("0", true))
            {
                return true;
            }

            return FreePlugIn.Decrypt(implementationType, true).Contains(":0", StringComparison.Ordinal);
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

        private static TxStudentDetail MapStudentDetail(StudentDetailsDto model)
        {
            return new TxStudentDetail
            {
                UserId = model.UserId,
                SNo = model.SNo,
                IdNo = model.IdNo,
                DateOfBirth = model.DateOfBirth,
                PlaceOfBirth = model.PlaceOfBirth,
                Religion = model.Religion,
                BloodGroup = model.BloodGroup,
                DateOfAdmsn = model.DateOfAdmsn,
                DateOfDischarge = model.DateOfDischarge,
                TransferedFromWhere = model.TransferedFromWhere,
                StartClass = model.StartClass,
                CurrentClass = model.CurrentClass,
                Passport = model.Passport,
                ClassArmId = model.ClassArmId,
                GuardiansName = model.GuardiansName,
                Relationship = model.Relationship,
                GuardianAddress = model.GuardianAddress,
                Occupation = model.Occupation,
                MobileNumber = model.MobileNumber,
                HomeNumber = model.HomeNumber,
                EmailAddress = model.EmailAddress,
                MailingAddress = model.MailingAddress,
                Country = model.Country,
                TimeStatus = TimeStatus.Current,
                SchoolInfoId = model.SchoolInfoId,
                ClassCategory = model.ClassCategory,
                Status = model.Status,
                StudentInitialRegStatus = model.StudentInitialRegStatus,
                AdmisnTermId = model.AdmisnTermId
            };
        }

    }
}
