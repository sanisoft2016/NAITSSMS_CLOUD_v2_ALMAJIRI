using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Enum;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.IService
{
    /// <summary>
    /// Registration Service Interface - current public RegistrationService contract surface
    /// </summary>
    public interface IRegistrationService
    {
        Task<SchoolInfoDto> GetSystemInfoForSms(string userIdOrSchoolId, CancellationToken ct = default);
        Task<SchoolInfoDto> GetSchoolModelForSchoolRegistrationOfficer(string userId, CancellationToken ct = default);
        Task<bool> ChangeSystemImplementationType(string conCatParameters, CancellationToken ct = default);
        Task<string> GetAndEncodeCredentials(string userOrSchoolId, CancellationToken ct = default);
        Task<string> StudentsPromotionValidations(string userSchoolId, CancellationToken ct = default);
        Task<string> ConfirmStudentsPromotion(string userId, CancellationToken ct = default);
        Task StudentsAutomaticPromotion(string userId, CancellationToken ct = default);
        Task<SchoolSummaryDto?> GetSchoolSummaryForRegistrarDashboard(string userId, CancellationToken ct = default);
        Task<bool> ChangeStudentPassword(StudentPasswordModelDto model, CancellationToken ct = default);
        Task AutoPromotion(CancellationToken ct = default);
        Task<StudentAssessmentEnvironmentPreparationReturnDto> DoSystemInitialPreparationForStudentAccessment(
            string userId, TermStatus term,
            CancellationToken ct = default);

        Task<bool> CreatSchoolHead(
            string userId,
            SchoolHeadDto model,
            CancellationToken ct = default);

        Task<IEnumerable<SchoolHeadDto>> GetPrincals(
            string userId,
            CancellationToken ct = default);

        Task<bool> UpdateSchoolHeadInfo(
            string userId,
            SchoolHeadDto model,
            CancellationToken ct = default);

        Task<byte> CreateSchoolHeadAssignment(
            string userId,
            CancellationToken ct = default);

        Task<string> GetSchoolHeadNames(
            string userId,
            CancellationToken ct = default);

        Task<List<SubjectDto>> SeniorClassesSubjectListCurrent(
            string userId,
            ClassCategoryStatus studentStatus,
            CancellationToken ct = default);

        Task<List<SubjectDto>> BasicClassesSubjectListCurrent(
            string userId,
            CurrentClass className,
            CancellationToken ct = default);

        Task<List<SubjectDto>> SeniorClassesSubjectList(
            string userId,
            ClassCategoryStatus studentStatus,
            CurrentClass classs,
            CancellationToken ct = default);

        Task<List<SubjectDto>> BasicClassesSubjectList(
            string userId,
            CurrentClass className,
            CancellationToken ct = default);

        Task<bool> CommercialSubjectUpdateValidation(
            string userId,
            string subjectName,
            int id,
            CancellationToken ct = default);

        Task<IEnumerable<EmployeeDto>> GetAllTeachers(
            string userId,
            CancellationToken ct = default);

        Task<IEnumerable<TeacherClassArmAssignmentDto>> GetClassTeachers(
            string userId,
            CancellationToken ct = default);

        Task<IEnumerable<TeacherSubjectArmAssignmentDto>> GetSubjectTeachers(
            string userId,
            CancellationToken ct = default);

        Task<IEnumerable<EmployeeDto>> GetTeacherByUserName(
            string userId,
            string searchParameter,
            CancellationToken ct = default);

        Task<IEnumerable<TeacherSubjectArmAssignmentDto>> GetSubjectTeacherByUserName(
            string userId,
            string searchParameter,
            CancellationToken ct = default);

        Task<IEnumerable<TeacherClassArmAssignmentDto>> GetFormMasterByUserName(
            string userId,
            string searchParameter,
            CancellationToken ct = default);

        Task<IEnumerable<TeacherSubjectArmAssignmentDto>> UnAssignSubjectTeacher(
            string userId,
            string id,
            CancellationToken ct = default);

        Task<IEnumerable<TeacherClassArmAssignmentDto>> UnAssignClassTeacher(
            string userId,
            string id,
            CancellationToken ct = default);

        Task<bool> EnableUserById(
            string id,
            CancellationToken ct = default);

        Task<bool> DisableUserById(
            string id,
            CancellationToken ct = default);

        Task<TeacherAssignmentOptionsDto> GetSubjectAssignmentOptions(
            string userId,
            CancellationToken ct = default);

        Task<IList<SelectListItem>> GetSubjectAssignmentSubjects(
            string userId,
            string classId,
            CancellationToken ct = default);

        Task<TeacherAssignmentArmResultDto> GetSubjectAssignmentArms(
            string userId,
            string subjectId,
            string classId,
            CancellationToken ct = default);

        Task<TeacherAssignmentArmResultDto> AssignSubjectTeacherToArm(
            string userId,
            string teacherId,
            string subjectId,
            string armId,
            CancellationToken ct = default);

        Task<IList<TeacherUnassignedSubjectRecordDto>> GetSubjectUnAssignedTeachers(
            string userId,
            CancellationToken ct = default);

        Task<TeacherAssignmentOptionsDto> GetClassArmAssignmentOptions(
            string userId,
            CancellationToken ct = default);

        Task<TeacherAssignmentArmResultDto> GetClassArmAssignmentArms(
            string userId,
            string classId,
            CancellationToken ct = default);

        Task<TeacherAssignmentArmResultDto> AssignClassArmTeacher(
            string userId,
            string teacherId,
            string armId,
            CancellationToken ct = default);

        Task<TeacherCbtUsersDto> GetRegisteredTeachersForCbt(
            string userId,
            string searchParameter = null,
            CancellationToken ct = default);

        Task<TeacherCbtUsersDto> AddUserToCbtRole(
            string userId,
            string targetUserId,
            CancellationToken ct = default);

        Task<TeacherCbtUsersDto> RemoveUserFromCbtRole(
            string userId,
            string targetUserId,
            CancellationToken ct = default);

        Task<IEnumerable<TeacherSubjectArmAssignmentDto>> GetTeacherAssignedSubjectsByTeacherId(
            string userId,
            string selectedTeacherId,
            CancellationToken ct = default);

        Task<bool> TransferSubjectTeacherSubjectAssignment(
            string userId,
            IList<SubjectIdsBto> subjectIds,
            CancellationToken ct = default);

        Task<IEnumerable<TeacherClassArmAssignmentDto>> GetTeacherAssignedArmsByTeacherId(
            string userId,
            string selectedTeacherId,
            CancellationToken ct = default);

        Task<bool> TransferArmTeacherArmAssignment(
            string userId,
            IList<SubjectIdsBto> subjectIds,
            CancellationToken ct = default);

        Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessClassResultByClassName(
            string userId,
            string sessionId,
            TermStatus termInEnum,
            CurrentClass classNameInEnum,
            CancellationToken ct = default);

        Task<PerClassAndThirdTermCumulativeResultProcessingDto> PerClassAndThirdTermCumulativeResultProcessing(
            string userId,
            string sessionId,
            TermStatus termInEnum,
            CurrentClass classNameInEnum,
            CancellationToken ct = default);

        Task<IList<SelectListItem>> GetStudentResultsSessionsDropdown(string userId, CancellationToken ct = default);

        Task<IList<SelectListItem>> GetStudentResultsTermsDropdown(string sessionId, CancellationToken ct = default);

        Task<IEnumerable<ClassDto>> GetStudentResultsClasses(string userId, CancellationToken ct = default);

        Task<IEnumerable<ClassArmDto>> GetStudentResultsClassArms(string classId, CancellationToken ct = default);

        Task<UsersSingleStudentResultProcessingsToReportSheetXtractionDto> GetTermlyAssessmentForSelectedStudent(
            string userId,
            string studentId,
            string sessionId,
            TermStatus termStatus,
            CancellationToken ct = default);

        Task<YearlyResultProcessingsToReportSheetDto> GetYearlyAssessmentForSelectedStudent(
            string userId,
            string studentId,
            string selectedSessionId,
            CancellationToken ct = default);

        Task<PerClassAndThirdTermCumulativeResultProcessingDto> PerClassThirdTermResultProcessing(
            string userId,
            string sessionId,
            TermStatus termInEnum,
            CurrentClass classNameInEnum,
            CancellationToken ct = default);

        Task<PerClassAndThirdTermCumulativeResultProcessingDto> PerArmAndThirdTermCumulativeResultProcessing(
            string userId,
            string sessionId,
            TermStatus termInEnum,
            CurrentClass classNameInEnum,
            ArmName armInEnum,
            CancellationToken ct = default);

        Task<PerClassAndThirdTermCumulativeResultProcessingDto> PerArmThirdTermResultProcessing(
            string userId,
            CurrentClass classNameInEnum,
            ArmName armInEnum,
            CancellationToken ct = default);

        Task<PerClassAndThirdTermCumulativeResultProcessingDto> GeneralResultProcessing(
            string userId,
            CancellationToken ct = default);

        Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> GetClassThirdTermResult(
            string userId,
            string sessionId,
            TermStatus termInEnum,
            CurrentClass classNameInEnum,
            CancellationToken ct = default);

        Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessClassArmResultByClassNameAndArmName(
            string userId,
            string sessionId,
            TermStatus termInEnum,
            CurrentClass classNameInEnum,
            ArmName armInEnum,
            CancellationToken ct = default);

        Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessThirdTermClassArmResultByClassNameAndArmName(
            string userId,
            CurrentClass classNameInEnum,
            ArmName armInEnum,
            CancellationToken ct = default);

        Task<GeneralResultProcessingsForAllStudentsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessAllResults(
            string userId,
            CancellationToken ct = default);

        Task<BroadSheetReportDto> GetBroadSheetReportForClassAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            CurrentClass className,
            CancellationToken ct = default);

        Task<BroadSheetReportDto> GetBroadSheetReportForArmAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            CurrentClass className,
            ArmName armName,
            CancellationToken ct = default);

        Task<IList<SelectListItem>> GetBroadSheetArmsDropdownAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>
        /// Legacy RegistryController.NewAssessmentHandler page bootstrap (session/term/class/arm).
        /// </summary>
        Task<NewAssessmentHandlerBootstrapDto> GetNewAssessmentHandlerBootstrapAsync(
            string userId,
            CancellationToken ct = default);

        Task<PrepareEnvironmentForStudentAssessmentReturnDto> ProcessNewAssessmentAsync(
            string userId,
            NewAssessmentProcessRequestDto request,
            CancellationToken ct = default);

        /// <summary>
        /// Legacy PrepareAssessmentEnvironmentForTheWholeStudent(concatParameters).
        /// </summary>
        Task<PrepareEnvironmentForStudentAssessmentReturnDto?> PrepareAssessmentEnvironmentForTheWholeStudentAsync(
            string userId,
            string concatParameters,
            CancellationToken ct = default);

        /// <summary>
        /// Legacy PrepareAssessmentEnvironmentForaClassStudents — per-class create/delete subject and psycho/affective.
        /// </summary>
        Task<PrepareEnvironmentForStudentAssessmentReturnDto?> PrepareAssessmentEnvironmentForaClassStudentsAsync(
            string userId,
            string concatParameters,
            CancellationToken ct = default);

        /// <summary>
        /// Legacy PrepareAssessmentEnvironmentForAnArmStudents — per-arm create/delete subject and psycho/affective.
        /// </summary>
        Task<PrepareEnvironmentForStudentAssessmentReturnDto?> PrepareAssessmentEnvironmentForAnArmStudentsAsync(
            string userId,
            string concatParameters,
            CancellationToken ct = default);

        Task<string> GetSystemImplementationStatusAsync(string userId, CancellationToken ct = default);

        Task<byte> GetAppropriatePaymentStatusAsync(string userId, CancellationToken ct = default);

        Task<PrepareEnvironmentForStudentAssessmentReturnDto> GetAssessmentCompletionDefaultedTeachersAsync(
            string userId,
            CancellationToken ct = default);

        // --- Super Admin / SchoolList (legacy SadminController) ---
        Task<SuperAdminDashboardDto> GetSuperAdminDashboard(CancellationToken ct = default);

        Task<SchoolListForSystemAdminDto> GetSchoolListForSuperAdmin(CancellationToken ct = default);
        Task<IEnumerable<SchoolInfoDto>> SearchSchoolByCertainParameter(string searchParameter, int searchStatus, CancellationToken ct = default);
        Task<IEnumerable<SchoolInfoDto>> GetAllRegistrarForSuperAdmin(CancellationToken ct = default);
        Task<bool> DisableSchoolUsersBySchoolRegistrarId(string userId, CancellationToken ct = default);
        Task<bool> EnableSchoolUsersBySchoolRegistrarId(string userId, CancellationToken ct = default);
        Task<SchoolInfoDto?> GetSchoolRegistrarDetailsById(string userId, CancellationToken ct = default);
        Task<AgentInfoDto> GetAgentInfoById(string id, CancellationToken ct = default);
        Task<byte> DeleteSchoolRegistrar(string userId, CancellationToken ct = default);

        // --- Super Admin Edit School Registrar (legacy SadminController.EditSchoolRegistrar) ---
        Task<EditSchoolRegistrarDto?> GetSchoolRegistrarById(string userId, CancellationToken ct = default);
        Task<byte> UpdateSchoolRegistrarInfo(EditSchoolRegistrarDto model, string hostName, CancellationToken ct = default);

        // --- Super Admin LicensingIndex (legacy SadminController.LicensingIndex) ---
        Task<LicensingIndexPageDto?> GetLicensingIndexPage(string schoolId, CancellationToken ct = default);
        Task<LastSubscriptionInfoDto> GetLastSubscriptionInfo(string schoolId, CancellationToken ct = default);
        Task<IReadOnlyList<SubscriptionHistoryItemDto>> GetSubscriptionHistory(string schoolId, CancellationToken ct = default);
        Task<string?> GetSubscriptionKey(CreateSubscriptionKeyRequestDto model, CancellationToken ct = default);
        Task<string?> UpdateNumberOfStudentsForLicensing(string schoolId, short noOfStudents, CancellationToken ct = default);

        // --- Super Admin Accountant (legacy RegistrationController.CreateAdminAccountant) ---
        Task<string?> CheckRenewalOrRegistrationStatus(string schoolId, CancellationToken ct = default);
        Task<short> GetSchoolSnoForSuperAdminOfficer(string schoolId, CancellationToken ct = default);
        Task<bool> CreateSuperAccountantEmployeeRecord(CreateAdminAccountantEmployeeDto model, CancellationToken ct = default);
        Task<bool> CreateSubordinateAccountantEmployeeRecord(CreateAdminAccountantEmployeeDto model, CancellationToken ct = default);
        Task<SchoolSubordinateAccountantStatusDto> GetSchoolAccountantStatus(string userId, CancellationToken ct = default);
        Task<EmployeeDto?> GetSubordinateAccountantDetails(string schoolId, CancellationToken ct = default);
        Task<bool> UpdateSubordinateAccountantInfo(UpdateAdminAccountantDto model, CancellationToken ct = default);
        Task<string?> DeleteSubordinateAccountant(string employeeId, CancellationToken ct = default);
        Task<EmployeeDto?> GetAdminAccountantDetails(string schoolId, CancellationToken ct = default);
        Task<bool> EnableAccountantUserById(string userId, CancellationToken ct = default);
        Task<bool> DisableAccountantUserById(string userId, CancellationToken ct = default);
        Task<bool> UpdateAccountantInfo(UpdateAdminAccountantDto model, CancellationToken ct = default);
        Task<string?> DeleteAllAccountants(string employeeId, CancellationToken ct = default);

        // --- SMS gateway / HomeController callbacks (legacy HomeController.SmsBalance…SubscriptionRewal) ---
        string DecodeCredentials(string credentialEncoded);
        string EncodeCredentials(string credential);
        Task<string> GetSmsBalanceAsync(string token, string key, CancellationToken ct = default);
        Task<string> UpdateSmsBalanceAsync(string encodedParameter, CancellationToken ct = default);
        Task<string> UpdateSmsBalanceExpressAsync(string parameterEncoded, CancellationToken ct = default);
        Task<string> UpdateSmsCredentialAsync(string smsCredential, CancellationToken ct = default);
        Task<bool> VerifyCredentialsAsync(string credentialEncoded, CancellationToken ct = default);
        Task<byte> GetSubscriptionCategoryStatusAsync(string credentialEncoded, CancellationToken ct = default);
        /// <param name="persist">When true, saves immediately (NewSystemMigration). Renewal flow leaves false and commits once.</param>
        Task<string> UpdateNewSystemInfoAsync(string credentialEncoded, bool persist = false, CancellationToken ct = default);
        Task<string> GetSubscriptionRenewalKeyAsync(string credentialEncoded, CancellationToken ct = default);
    }
}