using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SelectListItem = NAITSSMS.Application.DTOs.SelectListItem;

namespace NAITSSMS.Application.IService
{
    public interface ITeachersAndOthersService
    {
        // Core teacher context
        Task<IEnumerable<TeacherSubjectArmAssignmentDto>> GetTeacherAssignedClassesAndSubjects(CancellationToken ct = default);
        Task<AccessVerifyReturnValues> B(string userId, CancellationToken ct = default);

        // Student term and yearly assessments (by specific student + session)
        Task<UsersSingleStudentResultProcessingsToReportSheetXtractionDto> GetTermlyAssessmentForSelectedStudent(string studentId, string sessionId, TermStatus termStatus, CancellationToken ct = default);
        Task<YearlyResultProcessingsToReportSheetDto> GetYearlyAssessmentForSelectedStudent(string studentId, string selectedSessionId, CancellationToken ct = default);

        // Arm-specific assessment operations
        Task<StudentAssessmentAndStatusDto> GetStudentInAnArmForAssessment(string armId, CancellationToken ct = default);
        Task<IEnumerable<TeacherClassArmAssignmentDto>> GetTeacherAssignedArmAsFormMaster(CancellationToken ct = default);
        Task<StudentsAccessmentCasSettingDto> GetStudentsInAnArmByArmIdAndCaSetting(string armId, string subjectId, CancellationToken ct = default);
        Task<bool> CreateCasAndExamsScore(List<StudentsAccessmentDto> studentsAccessmentScores, CancellationToken ct = default);
        Task<ClassAndArmDropDownDto> GetClassArmForDropDown(CancellationToken ct = default);
        Task<AffectiveAndPsychomotorAssessmentWithAttendanceDto> GetAffectiveAndPsychomotiveForReUpdateWithAttendance(string studentId, CancellationToken ct = default);
        Task<AffectiveAndPsychomotorAndTotalAttendanceDto> GetAffectiveAndPsychomotorItems(CancellationToken ct = default);
        Task<bool> CreateAffectiveAndPsychomotorScore(FormMasterAffectivePsychomotorSubmitDto model, CancellationToken ct = default);

        // School info helpers
        Task<string> StudentsPromotionValidations(CancellationToken ct = default);
        Task<SchoolInfoDto> GetSchoolModelForStudentAndParent(CancellationToken ct = default);
        Task<SchoolInfoDto> GetSchoolModelForSchoolRegistrationOfficer(CancellationToken ct = default);
        Task<SchoolInfoDto> GetSchoolModel(CancellationToken ct = default);
        Task<SchoolInfoDto> VerifyAndGetSchoolInfo(CancellationToken ct = default);

        // Teacher helpers
        Task<YearlyResultProcessingsToReportSheetDto> GetYearlyAssessmentForSelectedStudent(string selectedSessionId, CancellationToken ct = default);
        Task<UsersSingleStudentResultProcessingsToReportSheetXtractionDto> GetTermlyAssessmentForSelectedStudent(string sessionId, TermStatus termStatus, CancellationToken ct = default);
        Task<SchoolInfoDto> GetSchoolModelForStudentOrParent(CancellationToken ct = default);
        Task<bool> ChangeSystemImplementationType(string conCatParameters, CancellationToken ct = default);
        Task<bool> UpdateFeatureImplementationType(byte impleType, string schoolId, CancellationToken ct = default);
        Task<IList<SelectListItem>> GetSessionsDropdown(CancellationToken ct = default);
        /// <summary>
        /// Sessions where the resolved portal student (incl. selected sibling) has assessment rows.
        /// </summary>
        Task<IList<SelectListItem>> GetSessionsDropdownWithResultsForPortalStudent(CancellationToken ct = default);
        Task<IList<SelectListItem>> GetTermsDropdown(CancellationToken ct = default);

        // CA utilities for missing entries
        Task<StudentsAccessmentCasSettingDto> GetStudentsInAnArmByArmIdAndCaSettingForUnEnteredCasScores(string armId, string subjectId, CancellationToken ct = default);
        Task<StudentsAccessmentCasSettingDto> GetStudentsInAnArmByArmIdAndCaSettingForUnEnteredCasAndExamsScores(string armId, string subjectId, CancellationToken ct = default);
        Task<StudentAssessmentAndStatusDto> GetUnenteredStudentRemarkByArmId(string armId, CancellationToken ct = default);
        Task<bool> ChangeTermlyAssessmentStatus(string conCatParameters, CancellationToken ct = default);
        Task<IEnumerable<TeacherSubjectArmAssignmentDto>> GetUncompletedCAsandExamsScoreEntering(CancellationToken ct = default);
        Task<IEnumerable<TeacherSubjectArmAssignmentDto>> GetUncompletedCAsScoreEntering(CancellationToken ct = default);

        // Result processing
        Task<CaResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> GetStudentsInAnArmForCaByClassNameAndArmName(CurrentClass clas, ArmName arm, CancellationToken ct = default);
        Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessClassResultByClassName(string sessionId, TermStatus termInEnum, CurrentClass classNameInEnum, CancellationToken ct = default);
        Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessClassArmResultByClassNameAndArmName(CurrentClass clas, ArmName arm, CancellationToken ct = default);
        Task<GeneralResultProcessingsForAllStudentsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessAllResults(CancellationToken ct = default);
        Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessThirdTermClassArmResultByClassNameAndArmName(CurrentClass clas, ArmName arm, CancellationToken ct = default);
        Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> GetClassThirdTermResult(string sessionId, TermStatus termInEnum, CurrentClass classNameInEnum, CancellationToken ct = default);
        Task<GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto> ProcessClassArmResultByClassNameAndArmName(string sessionId, TermStatus termInEnum, CurrentClass classNameInEnum, ArmName armInEnum, CancellationToken ct = default);
        Task<YearlyResultProcessingsToReportSheetDto> GetStudentYearlyReportSheet(string armId, CancellationToken ct = default);

        // SMS helpers
        Task<NetworkType> GetNetworkType(string telNo, CancellationToken ct = default);
        Task<string> GetSmsTokenAndKeyOnly(CancellationToken ct = default);
        Task<string> GetSmsTokenAndKey(CancellationToken ct = default);
        Task<string> GetAndEncodeCredentials(string userId, CancellationToken ct = default);
        Task<SchoolInfoDto> GetSystemInfoForSms(string userId, CancellationToken ct = default);
        Task<string> GetCurrentSmsTokenAndKeyAndNewTokenAndKey(CancellationToken ct = default);
        Task<string> GetAndEncodeCredentialAndSmsBalanceUpdateParameters(float newBalace, float noOfSuccessfullySentSms, float pricePerSms, CancellationToken ct = default);
        Task<string> GetAndEncodeCredentialsAndNoOfSmsTimesPages(int noOfSmsRecipient, int noOfSmsPages, CancellationToken ct = default);
        Task<string> GetAndEncodeCredentialsAndExpiryDate(string userId, CancellationToken ct = default);
        Task<string> GetSmsTokenAndKeyAndSystemIdAndUserNameAndSchoolName(CancellationToken ct = default);
        Task<bool> StudentsGuardianInfoUpdate(List<StudentsGuardianInfoUpdateDto> model, CancellationToken ct = default);
        string GetSystemId();
        Task<SmsUsersDto> GetSchoolInfoForSms(CancellationToken ct = default);

        // CBT methods
        Task CreateQuestionsAndAnswer(CbtQuestionAndAnswerDto model, CancellationToken ct = default);
        Task<List<StudentsAccessmentDto>> ProcessClassBroadSheet(string sessionId, TermStatus termInEnum, CurrentClass classNameInEnum, CancellationToken ct = default);
        Task<List<StudentsAccessmentDto>> ProcessArmBroadSheet(string sessionId, TermStatus termInEnum, CurrentClass nameInEnum, ArmName armInEnum, CancellationToken ct = default);
        Task<List<CbtQuestionAndAnswerDto>> GetCbtQuestionsByReferenceId(string subjectId, string classId, byte termId, AssessmentType assessmentType, CancellationToken ct = default);
        Task<CbtQuestionAndAnswerDto> GetCbtQuestionAndAnswerById(string cbtQuestionAndAnswerId, CancellationToken ct = default);
        Task<List<ContinuousAssessmentTypeDto>> GetListOfTermlyAssessmentCategory(CancellationToken ct = default);
        Task<List<SelectListItem>> GetListOfTermlyContinuousAssessmentForDropdown(AssessmentType assessmentType, CancellationToken ct = default);
        Task<List<SelectListItem>> GetClassDropdown(string className, CancellationToken ct = default);
        Task UpdateQuestionsAndAnswer(CbtQuestionAndAnswerDto questionAndAnswer, CancellationToken ct = default);
        Task<bool> DeleteQuestion(string questionId, CancellationToken ct = default);
        Task<List<SelectListItem>> GetSubjectDropdown(CancellationToken ct = default);
        Task<List<CbtQuestionAndAnswerDto>> GetQuestionByParameter(string searchParameter, CancellationToken ct = default);
        Task<List<SelectListItem>> GetDefaultListOfTermlyContinuousAssessmentForDropdown(CancellationToken ct = default);
        Task<CbtQuestionAndAnswerDto> GetCbtQuestionAndAnswer2ndById(string cbtQuestionAndAnswerId, CancellationToken ct = default);
        Task<List<SelectListItem>> GetDefaultClassDropdown(CancellationToken ct = default);
        Task<CbtQuestionAnswerAndSetQuestionDto> GetQuestions(QuestionProcessingActionType actionType, byte termId, string assessmentType, string classId, string subjectId, int noOfQuestions, CancellationToken ct = default);

        // Not implemented or placeholders (preserved)
        Task<bool> GetStudentResultByStudentId(string studentAdmsnNo, string schoolRegId, CancellationToken ct = default);

        // Student / Parent self-service portal (legacy StudentsAndParentsController)
        Task<StudentDetailsDto> GetStudentDetailsByStudentId(CancellationToken ct = default);
        /// <summary>
        /// Students in the same school that share the logged-in student's Contact MobileNumber.
        /// Used by the portal sibling switcher. Honours X-Selected-Student-Id for IsCurrentLogin only via login user.
        /// </summary>
        Task<List<StudentPortalSiblingDto>> GetSiblingStudentsBySharedMobileNumber(CancellationToken ct = default);
        Task<List<SelectListItem>> GetSubjectCategoryForDropDown(CancellationToken ct = default);
        Task<SubjectsAndClassName> GetLevelsThatOfferThisSubject(string subjectStatus, CancellationToken ct = default);
        Task<string> CreateSubjectStudentAssignment(List<SubjectIdsBto> subjectIds, CancellationToken ct = default);
        Task<bool> RemoveSubjectStudentAssignment(List<SubjectIdsBto> subjectIds, CancellationToken ct = default);

        // Payment processing placeholder
        Task<object> ProcessClassArmPaymentByClassNameAndArmName(CurrentClass clas, ArmName arm, CancellationToken ct = default);
    }
}
