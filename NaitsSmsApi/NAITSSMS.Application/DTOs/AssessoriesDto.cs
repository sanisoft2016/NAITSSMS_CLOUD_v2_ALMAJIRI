
using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Application.DTOs
{
    public class CurrentTermStatusAndIdDto
    {
        
    }

    public class SchoolListForSystemAdminDto
    {
        public List<SchoolInfoDto> SchoolInfo { get; set; }
        public int NoOfSchools { get; set; }
    }

    public class StartAndEndDateDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

    }

    public class AcademicTermsAndSchoolDetailsDto
    {
        public SchoolInfoDto SchoolTyInfo { set; get; }
        public List<SelectListItem> TermList { set; get; }
    }

    public class UnAssignedClassArmSubjectDto
    {
        public string SubjectName { get; set; }
        public CurrentClass ClassName { get; set; }
        public string ClassLabel { get; set; }
        public ArmName ArmName { get; set; }
    }
    public class YearAndMonthDropdownForItemDto
    {
        public List<SelectListItem> Years { get; set; }
        public IList<SelectListItem> Months { get; set; }
    }

    public class SelectListItemDto
    {
        public string Text { get; set; }
        public byte Value { get; set; }
    }
    public class YearAndMonthDropdownForSalaryDto
    {
        public List<SelectListItem> Years { get; set; }
        public IList<SelectListItem> Months { get; set; }
    }
    public class SchoolSubordinateAccountantStatusDto
    {
        public string SchoolId { get; set; }
        public bool SubordinateAccountantStatus { get; set; }
    }
    public class StudentCollectionsDto
    {
        public string StudentId { get; set; }
        public ClassCategoryStatus ClassCategory { get; set; }
    }
    public class PaymentTypeTotalDto
    {
        public PaymentType PaymentType { get; set; }
        public double TotalPayment { get; set; }
    }

    public class TermlyPaymentReportDto
    {
        public List<TermlyReportOfReturningDto> TermlyReportOfReturning { get; set; }
        public List<PaymentTypeTotalDto> PaymentTypeTotal { get; set; }

    }

    public class TermlyReportOfReturningDto
    {
        public StudentStatus RowStatus { set; get; }
        public string TermAndSession { set; get; }
        public ClassCategoryStatus  ClassCategory { set; get; }
        public double Fees { set; get; }
        public short ClearedStuds { set; get; }
        public string ClearedStudsDetails { set; get; }
        public double TakeHome1 { set; get; }
        public double Discount { set; get; }
        public short PartlyClearedStuds { set; get; }
        public string PartlyClearedStudsDetails { set; get; }
        public double TakeHome2 { set; get; }
        public double Outstanding1 { set; get; }
        public string TakeHomeAndOutstanding { set; get; }
        public short UnClearStuds { set; get; }
        public string UnClearStudsDetails { set; get; }
        public double Outstanding2 { set; get; }
    }
    public class SingleStudentTermlyFeesReturnData
    {
        public string AdmisnNo { get; set; }
        public string StudentName { get; set; }
        public string ClassAndArm { get; set; }
        public string TermAndSession { get; set; }
        public string SchoolId { get; set; }
        public string SchoolLogo { get; set; }
        public string SchoolName { get; set; }
        public string SchoolAddress { get; set; }
        public string SchoolTel { get; set; }
        [StringLength(40)]
        public string AccountSignature { get; set; } 
        public string PreviousDebt { get; set; }
        public string AmountPaid { get; set; }
        public string OutStandingDebt { get; set; }
        public string Discount { get; set; }
        public string PaymentDate { get; set; }
        public string PaymentId { get; set; }
    }
    public class StudentTermlyFees
    {
        public string StudentId { get; set; }
        //[StringLength(15)]
        //public string IdNo { get; set; }
        //public string FullName { get; set; }      
        //public byte SessionId { get; set; }
        [StringLength(10)]
        public string Session { set; get; }

        //[StringLength(20)]
        //public string ClassLabel { get; set; }
        public int YearlyAndTermlyStatusId { get; set; }

        public TermStatus TermStatus { set; get; }
        public string ActualFees { set; get; }
        public string AmountPaid { set; get; }
        public string Outstanding { set; get; }

    }
    public class StudentTermlyFees2Nd
    {
        public string StudentAcademicLevelId { get; set; }
        [StringLength(10)]
        public string Session { set; get; }
        public TermStatus TermStatus { set; get; }
        public string ActualFees { set; get; }
        public string AmountPaid { set; get; }
        public string Outstanding { set; get; }

    }
    public class StudentTermlyFeesPaymentHistoryDto
    {
        public string StudentAcademicLevelId { get; set; }
        public double AmountPaid { set; get; }
        public string PaymentDate { set; get; }
        public PaymentType PaymentType { get; set; }
    }
    public class StudentAcademicProgressHistoryStrokPaymentDto
    {
        [StringLength(10)]
        public string Session { set; get; }
        public TermStatus TermStatus { set; get; }
        public string StudentAcademicLevelId { get; set; }
        public List<StudentTermlyFeesPaymentHistoryDto> StudentTermlyFeesHistory { get; set; }
        public double Discount { set; get; }
        public double ActualFees { set; get; }
    }

    public class ClassCategory
    {
        public byte Id { get; set; }
        public ClassCategoryStatus ClassCategoryStatus { get; set; }
    }

    public class ReturningStudentFees
    {
        public int Id { get; set; }
        public int YearlyAndTermlyStatusId { get; set; }
        public YearlyAndTermlyStatusDto YearlyAndTermlyStatus { get; set; }
        public ClassCategoryStatus ClassCategory { get; set; }
        public decimal Fees { get; set; }
    }
    public class SchoolFeesDto
    {
        public int Id { get; set; }
        public int YearlyAndTermlyStatusId { get; set; }
        public YearlyAndTermlyStatusDto YearlyAndTermlyStatus { get; set; }
        public ClassCategoryStatus ClassCategory { get; set; }
        public decimal Fees { get; set; }
    }
    public class FreshierRegistrationFees
    {
        public int Id { get; set; }
        public int YearlyAndTermlyStatusId { get; set; }
        public YearlyAndTermlyStatusDto YearlyAndTermlyStatus { get; set; }
        public ClassCategoryStatus ClassCategory { get; set; }
        public decimal Fees { get; set; }
    }

    public class StudentFieldExtractionForPaymentReceiptDto
    {
        public string StudentId { get; set; }

        [StringLength(15)]
        public string AdmisionNo { get; set; }

        public string FullName { get; set; }

        [StringLength(30)]
        public string ClassLabel { get; set; }

        public string TermAndSession { get; set; }
        [StringLength(20)]
        public string Fees { get; set; }
        [StringLength(40)]
        public string SchoolLogo { get; set; }
        public string SchoolAddress { get; set; }
        [StringLength(40)]
        public string AccountSignature { get; set; }
        [StringLength(10)]
        public string PaymentId { get; set; }
        public string PaymentDate { get; set; }


    }
    public class StudentFieldExtraction4Accounting2ndDto
    {
        public string StudentLevelPaymentId { get; set; }
        [StringLength(15)]
        public string AdmisionNo { get; set; }
        public string FullName { get; set; }
        [StringLength(50)]
        public string ClassLabel { get; set; }
        public CurrentClass CurrentClass { set; get; }
        public ClassCategoryStatus ClassCategoryStatus { get; set; }
        public StudentStatus StudentStatus { get; set; }
        //public int CurrentTermId { get; set; }
        public FeesPaymentStatus CurrentTermPaymentStatus { get; set; }
        public FeesPaymentStatus OtherTermsPaymentStatus { get; set; }
        public ArmName ArmName { set; get; }
    }
    public class StudentFieldExtraction4AccountingDto
    {
        public int StudentLevelPaymentId { get; set; }
        [StringLength(15)]
        public string AdmisionNo { get; set; }
        public string FullName { get; set; }
        [StringLength(50)]
        public string ClassLabel { get; set; }
        public CurrentClass CurrentClass { set; get; }
        public ClassCategoryStatus ClassCategoryStatus { get; set; }
        public StudentStatus StudentStatus { get; set; }
        //public int CurrentTermId { get; set; }
        public FeesPaymentStatus CurrentTermPaymentStatus { get; set; }
        public FeesPaymentStatus OtherTermsPaymentStatus { get; set; }
        public ArmName ArmName { set; get; }
    }
    public class StudentFieldExtraction4Accounting2NdDto
    {

        public string StudentId { get; set; }
        [StringLength(15)]
        public string AdmisionNo { get; set; }
        public string FullName { get; set; }
        [StringLength(50)]
        public string ClassLabel { get; set; }
        public CurrentClass CurrentClass { set; get; }
        public ClassCategoryStatus ClassCategoryStatus { get; set; }
        public StudentStatus StudentStatus { get; set; }
        //public int CurrentTermId { get; set; }
        public FeesPaymentStatus CurrentTermPaymentStatus { get; set; }
        public FeesPaymentStatus OtherTermsPaymentStatus { get; set; }
        public ArmName ArmName { set; get; }
    }
    public class AccessVerifyReturnValuesAndMoreDto
    {
        public AccessVerifyReturnValues AccessVerifyReturnValues { get; set; }
        public LoginReturnStatus LoginReturnStatus { get; set; }
    }

    public class AccessVerifyReturnValues
    {
        public string Value1 { get; set; }
        public string Value2 { get; set; }
        public string Value3 { get; set; }
        public string Value4 { get; set; }
    }
    public class SchoolSummaryDto
    {
        public string Session { set; get; }
        public string Term { set; get; }
        public int TotalStudents { set; get; }
        public int TotalTeachers { set; get; }
        public List<StudentsAccessmentDto> StudentsAccessment { set; get; }
        public List<DashboardStudentsNumberAnalysisDto> DashboardStudentsNumberAnalysis { set; get; }
    }

    public class DashboardStudentsNumberAnalysisDto
    {
        public string ClassLabel { set; get; }
        public short NumberOfStudents { set; get; }
    }
    public class StudentsAccessmentCasSettingDto
    {
        public CasSettingDto CasSetting { set; get; }
        public IEnumerable<StudentsAccessmentDto> StudentsAccessment { set; get;}

    }
    public class NumberOfCasSettingDto
    {
        public IEnumerable<SelectListItem> NumberOfCasCategoyDropdown { get; set; }
        public byte? PerCasValue { get; set; }
    }
    public class CasSettingDto
    {
        public CasCategory CasCategory { get; set; }
        [Required]
        public byte? PerCasValue { get; set; }
    }
    public class StudentAssessmentEnvironmentPreparationReturnDto
    {
        public string StatusCode { set; get; }
        public IList<TotalStudentListToPdfDto> TotalStudentListToPdf { set; get; }
    }

    public class TotalStudentListToPdfDto
    {
        public string IdNo { get; set; }
        public string FullName { set; get; }
        [StringLength(20)]
        public string ClassLabel { get; set; }
        public CurrentClass CurrentClass { get; set; }
        public string Arm { set; get; }
        public SchoolInfoDto SchoolInfo { set; get; }
    }

    public class StudentPasswordModelDto
    {
        public string UserId { set; get; }
        public string Password { set; get; }
        public string ConfirmPassword { set; get; }
    }
    public class ArmOrCategoryStudentsInTheSelectedArmDto
    {
        public IList<StudentDetailsDto> StudentsList { set; get; }
        public IList<string> AssignedStudentIds { set; get; }
    }
    public class DropdownForSubjectStudentAssignmentDto
    {
        //public IEnumerable<SelectListItem> SubjectDropdown { get; set; }
        public IEnumerable<SelectListItem> ClassDropdown { get; set; }
        //public IEnumerable<SelectListItem> ArmDropdown { get; set; }
    }
    public class StudentIdsDto
    {
        public string Id { set; get; }
    }
    public class ReturnCreateSchoolRegistrarDto
    {
        public bool Status { set; get; }
        public CreatorStatus CreatorStatus { set; get; }
        public string RegistrationOfficerId { set; get; }
    }
    public class ReturnSchoolInfoDto
    {
        public string ProprietorCode { get; set; }
        public SchoolInfoDto SchoolInfo { get; set; }
    }
    public class ElectiveStatusDto
    {
        public string ElectiveId { get; set; }
        public ElectiveStatusCode ElectiveStatusCode { get; set; }
    }
    public class SubjectsAndClassName
    {
        public CurrentClass ClassName { set; get; }
        public ClassCategoryStatus StudentStatus { set; get; }
        public IEnumerable<SubjectDto> SubjectDtos { set; get; }
        public AccessStatus? AccessStatu { set; get; }
        public IList<ElectiveStatusDto> ElectiveStatus { set; get; }
    }
    public class SubjectIdsBto
    {
        [StringLength(50)]
        public string Id { set; get; }
    }
    public class StudentIdsAndClassCategoryBto
    {
        public string Id { set; get; }
        public string PaymentId { set; get; }
        public PaymentType PaymentType { get; set; }
        public DateTime? PaymentDate { get; set; }
        public ClassCategoryStatus ClassCategoryStatus { get; set; }
        public StudentStatus StudentStatus { get; set; }
    }
    public class StudentFielExtraction2NdDto
    {
        public string StudentId { get; set; }
        public string FullName { get; set; }
        //public ArmName ArmName { set; get; }
        [StringLength(50)]
        public string ClassLabel { get; set; }
    }

    public class PrepareEnvironmentForStudentAssessmentReturnDto
    {
        public string YearlyAndTermlyStatusId { set; get; }
        public TermStatus CurrentTermStatus { set; get; }
        public string Message { get; set; }//TotalStudentListToPdfDto
        public List<UncompletedFormMastersReportDto> UncompletedFormMastersReport { get; set; }
        public IList<TotalStudentListToPdfDto> TotalStudentListToPdf { set; get; }
    }
    public class UncompletedFormMastersReportDto
    {
        public string FormMasterName { get; set; }
        public string UserName { get; set; }
        public string ClassLabelAndArm { get; set; }
    }
    public class ClassLevelSubjectDto
    {
        public int Id { get; set; }
        //public short SNo { get; set; }
        [Required]
        [StringLength(35)]
        [Display(Name = "Subject's Name")]
        public string SubjectName { get; set; }
        public SubjectStatus SubjectStatus { get; set; }
        public TimeStatus TimeStatus { get; set; }

        [StringLength(35)]
        [Display(Name = "Subject's Level Status")]
        public string LevelsName { get; set; }
    }

    public class SubjectUpdateValidationDto
    {
        public int Id { get; set; }
        public SubjectStatus SubjectStatus { get; set; }
    }
    public class SchoolLevelCategoriesLabelDto
    {
        public byte Id { get; set; }
        [StringLength(30)]
        [Display(Name = "General Subjects Category Label")]
        public string? GeneralSujectLabel { get; set; }
        [StringLength(30)]
        [Display(Name = "Elective Subject Label")]
        public string? ElectiveSubjectLabel { get; set; }
        [StringLength(30)]
        [Display(Name = "Play Class Subjects Category Label")]
        public string? PlayClassLabel { get; set; }

        [StringLength(30)]
        [Display(Name = "Pre Nursery Subjects Category Label")]
        public string? PreNurseryLabel { get; set; }
        [StringLength(30)]
        [Display(Name = "Nursery Subjects Category Label")]
        public string? NurseryLabel { get; set; }
        [StringLength(30)]
        [Display(Name = "Primary Subjects Category Label")]
        public string? PrimaryLabel { get; set; }
        [StringLength(30)]
        [Display(Name = "J S S Subjects Category Label")]
        public string? JssLabel { get; set; }

        [StringLength(30)]
        [Display(Name = "S S Subjects Category Label")]
        public string? SsLabel { get; set; }

        [StringLength(30)]
        [Display(Name = "Custom-Level Subjects Category Label")]
        public string? CustomLevelLabel { get; set; }//
        public string? SchoolInfoId { get; set; }
        public string? ReligionSubjectLabel { get; set; }
        public SchoolInfoDto? SchoolInfo { get; set; }
    }

    public class ReturnedArmAndMessageForSubjectClassArmAssignmentDto
    {
       public IList<ClassArmDto> Arms { set; get; }
       public string Message { get; set; }
    }
    public class StudentIdArmIdAndCurrentClassDto
    {
        public int StudentsId { get; set; }
        public int ArmId { get; set; }
        public CurrentClass CurrentClass { get; set; }
    }

    public class StudentAssessmentAndStatusDto
    {
        public ArmTermlyAssessmentStatus ArmTermlyAssessmentStatus { get; set; }
        public IList<StudentDetailsDto> StudentDetails { get; set; }
    }
    public class StudentFielExtractionDto
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public ArmName ArmName { set; get; }
        public CurrentClass CurrentClass { get; set; }
        public string CurrentClassLabel { get; set; }
    }
    public class SessionsAndTerm
    {
        public IList<AcademicSessionDto> Sessions { get; set; }
        public TermStatus? CurrentTerm { get; set; }
    }
    public class ResultProcessingAssessoriesDto
    {
        public DateTime? NextTermBegins { get; set; }
        public string PincipalName { get; set; }
        public string SignatureString { set; get; }
    }
    public class ArmFormMasterDto
    {
        public string FormMastersName { get; set; }
        public string SignatureString { get; set; }
        public string AssignedArmId { get; set; }
    }

    public class AffectiveAndPsychomotorAndTotalAttendanceDto
    {
        public List<AffectiveAndPsychomotorDto> AffectiveAndPsychomotor { set; get; }
        public byte TotalAttendance { get; set; }
    }

    public class AffectiveAndPsychomotorDto
    {
        public byte Id { get; set; }
        [Required]
        [StringLength(25)]
        public string ItemsName { get; set; }

        public byte  TotalAttendance { get; set; }
    }

    public class FormMastersNameDto
    {
        public TermStatus Term { set; get; }
        public string FormMastersName { get; set; }
    }
    public class YearlyResultProcessingsToReportSheetDto
    {
        public List<StudentsAccessmentDto> StudentsAccessment { get; set; }
        public IList<StudentsAttendanceDto> StudentsAttendance { get; set; }
        public string ErrorMsg { get; set; }
        public IList<FormMastersNameDto> ArmFormMaster { get; set; }
    }
    public class ResultProcessingsToReportSheetXtractionForOtherUsersDto
    {
        public IOrderedEnumerable<IGrouping<int, StudentsAccessmentDto>> StudentsAccessment { get; set; }
        public IList<AffectiveAndPsychomotorAssessmentOutputXtractionForOtherUsersDto> AffectiveAndPsychomotorAssessment { get; set; }
        public IList<StudentsAttendanceDto> StudentsAttendance { get; set; }
        public SchoolHeadDto SchoolHeadInfo { get; set; }
        public byte? StatusCode { get; set; }
        [StringLength(15)]
        public string StudentAdmsnNo { get; set; }
        public StudentFielExtractionDto Student { get; set; }
    }
    public class CaResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
    {
        public IOrderedEnumerable<IGrouping<string, StudentsAccessmentDto>> StudentsAccessment { get; set; }
        public SchoolHeadDto SchoolHeadInfo { get; set; }
        public byte? StatusCode { get; set; }
        [StringLength(15)]
        public string StudentAdmsnNo { get; set; }
        public CasSettingDto CasSetting { set; get; }
        public CaAssessmentOutputXtractionForFormTeacherDto AssessmentResultOutputXtractionForFormTeacher { get; set; }


    }
    public class GeneralResultProcessingsForAllStudentsToReportSheetXtractionForFormTeacherCasSettingDto
    {
        public IList<StudentsAccessmentDto> StudentsAccessment { get; set; }
        public IList<AffectiveAndPsychomotorAssessmentOutputXtractionForFormTeacherDto> AffectiveAndPsychomotorAssessment { get; set; }
        public IList<StudentsAttendanceDto> StudentsAttendance { get; set; }
        public IList<SchoolHeadDto> SchoolHeadInfo { get; set; }
        public byte? StatusCode { get; set; }
        [StringLength(15)]
        public string StudentAdmsnNo { get; set; }
        public CasSettingDto CasSetting { set; get; }
    }



    public class UsersSingleStudentResultProcessingsToReportSheetXtractionDto
    {
        public byte? StatusCode { get; set; }
        public string StudentId { get; set; }
        public GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting
       { set; get; }
    }
















    public class GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
    {
        public IList<StudentsAccessmentDto> StudentsAccessment { get; set; }
        public IList<AffectiveAndPsychomotorAssessmentOutputXtractionForFormTeacherDto> AffectiveAndPsychomotorAssessment { get; set; }
        public IList<StudentsAttendanceDto> StudentsAttendance { get; set; }
        public SchoolHeadDto SchoolHeadInfo { get; set; }
        public byte? StatusCode { get; set; }
        [StringLength(15)]
        public string StudentAdmsnNo { get; set; }
        public CasSettingDto CasSetting { set; get; }
    }

    /// <summary>
    /// API payload for legacy RegistryController.PerClassAndThirdTermCumulativeResultProcessing.
    /// </summary>
    public class PerClassAndThirdTermCumulativeResultProcessingDto
    {
        public string Message { get; set; }
        public SchoolInfoDto SchoolInfo { get; set; }
        public IList<StudentsAccessmentDto> StudentsAccessment { get; set; }
        public IList<StudentsAttendanceDto> StudentsAttendance { get; set; }
        public IList<AffectiveAndPsychomotorAssessmentOutputXtractionForFormTeacherDto> AffectiveAndPsychomotorAssessment { get; set; }
        public SchoolHeadDto SchoolHead { get; set; }
        public ArmFormMasterDto ArmFormMaster { get; set; }
        public CasSettingDto CaSetting { get; set; }
        public byte? TotalAttendance { get; set; }
        public byte? StatusCode { get; set; }
    }
    public class ResultProcessingsToReportSheetXtractionForFormTeacherCasSettingDto
    {
        public IOrderedEnumerable<IGrouping<int, StudentsAccessmentDto>> StudentsAccessment { get; set; }
        public IList<AffectiveAndPsychomotorAssessmentOutputXtractionForFormTeacherDto> AffectiveAndPsychomotorAssessment { get; set; }
        public IList<StudentsAttendanceDto> StudentsAttendance { get; set; }
        public SchoolHeadDto SchoolHeadInfo { get; set; }
        public byte? StatusCode { get; set; }
        [StringLength(15)]
        public string StudentAdmsnNo { get; set; }
        public CasSettingDto CasSetting { set; get; }
    }
    public class ResultProcessingsToReportSheetDto
    {
        public IOrderedEnumerable<IGrouping<int, StudentsAccessmentDto>> StudentsAccessment { get; set; }
        public IList<AffectiveAndPsychomotorAssessmentDto> AffectiveAndPsychomotorAssessment { get; set; }
        public IList<StudentsAttendanceDto> StudentsAttendance { get; set; }
        public IList<ArmFormMasterDto> ArmFormMaster { get; set; }
        public ResultProcessingAssessoriesDto ResultProcessingAssessories { get; set; }
        
    }
    public class UpdateAffectiveAndPsychomotorScoreDto
    {
        // The "Score" is intentionally set to be "string" since it later be used to hold Comments
        
        public string ClassArmId { get; set; }
        public ClassArmDto ClassArm { get; set; }
        [StringLength(143)]
        public string AffectiveOrPsychomorId { get; set; }
        public byte ItemId { get; set; }
        public byte Score { get; set; }
    }
    public class CreateAffectiveAndPsychomotorScoreDto
    {
        // The "Score" is intentionally set to be "string" since it later be used to hold Comments
        public byte ItemId { get; set; }
        [StringLength(143)]
        public string Score { get; set; }
    }

    /// <summary>
    /// Cloud-friendly payload for legacy TeacherController.SubmitAffectiveAndPsychomotorScore.
    /// </summary>
    public class FormMasterAffectivePsychomotorSubmitDto
    {
        public string StudentId { get; set; } = string.Empty;
        public string ArmId { get; set; } = string.Empty;
        public byte Attendance { get; set; }
        public string TeacherComment { get; set; } = string.Empty;
        public string SchoolHeadComment { get; set; } = string.Empty;
        public List<CreateAffectiveAndPsychomotorScoreDto> Scores { get; set; } = new();
    }

    //AffectiveAndPsychomotorAssessmentDto

    public class AffectiveAndPsychomotorAssessmentWithAttendanceDto
    {
        public IList<AffectiveAndPsychomotorAssessmentDto> AffectiveAndPsychomotorAssessmentDto { get; set; }
        public byte Attendance { set; get; }
        [StringLength(70)]
        public string   TeacherRemarks { set; get; }
        [StringLength(70)]
        public string SchoolHeadRemarks { set; get; }
        public byte TotalAttendance { get; set; }
    }

    public class StudentsAttendanceDto
    {
        public string Id { get; set; }
        public byte Attendance { get; set; }
        [StringLength(150)]
        public string TeacherComments { get; set; }
        [StringLength(150)]
        public string SchoolHeadComments { get; set; }

        public int YearlyAndTermlyStatusId { get; set; }
        public YearlyAndTermlyStatusDto YearlyAndTermlyStatus { get; set; }
        public string StudentId { get; set; }
        public StudentDetailsDto Student { get; set; }
        public int ClassArmId { get; set; }
        public ClassArmDto ClassArm { get; set; }
    }
    public class AffectiveAndPsychomotorAssessmentOutputXtractionForOtherUsersDto
    {
        [StringLength(40)]
        public string Id { get; set; }
        public byte ItemId { get; set; }//AffectiveAndPsychomotor
        public AffectiveAndPsychomotorDto AffectiveAndPsychomotor { set; get; }
        public byte Score { get; set; }
        public int YearlyAndTermlyAssessmentStatusId { get; set; }
        public YearlyAndTermlyStatusDto YearlyAndTermlyAssessmentStatus { get; set; }
        public string StudentId { get; set; }
        public int FormMasterId { get; set; }
        public ArmFormMasterDto FormMaster { get; set; }
        public int ClassArmId { get; set; }
        public ClassArmDto ClassArm { get; set; }
    }

    public class CaAssessmentOutputXtractionForFormTeacherDto
    {
        [StringLength(40)]
        public string Id { get; set; }
        public int YearlyAndTermlyAssessmentStatusId { get; set; }
        public YearlyAndTermlyStatusDto YearlyAndTermlyAssessmentStatus { get; set; }
        public int FormMasterId { get; set; }
        public ArmFormMasterDto FormMaster { get; set; }
        public string ClassArmId { get; set; }
        public ClassArmDto ClassArm { get; set; }
    }

    public class AffectiveAndPsychomotorAssessmentOutputXtractionForFormTeacherDto
    {
        [StringLength(40)]
        public string Id { get; set; }
        public byte ItemId { get; set; }//AffectiveAndPsychomotor
        public AffectiveAndPsychomotorDto AffectiveAndPsychomotor { set; get; }
        public byte Score { get; set; }
        public string YearlyAndTermlyAssessmentStatusId { get; set; }
        public YearlyAndTermlyStatusDto YearlyAndTermlyAssessmentStatus { get; set; }
        public string StudentId { get; set; }
        public StudentFielExtractionDto Student { get; set; }
        public string FormMasterId { get; set; }
        public ArmFormMasterDto FormMaster { get; set; }
        public string ClassArmId { get; set; }
        public ClassArmDto ClassArm { get; set; }
    }
    public class AffectiveAndPsychomotorAssessmentDto
    {
        [StringLength(40)]
       public string Id { get; set; }
        public byte ItemId { get; set; }//AffectiveAndPsychomotor
        public AffectiveAndPsychomotorDto AffectiveAndPsychomotor { set; get; }
        public byte Score { get; set; }
        public int YearlyAndTermlyAssessmentStatusId { get; set; }
        public YearlyAndTermlyStatusDto YearlyAndTermlyAssessmentStatus { get; set; }
        public string StudentId { get; set; }
        public StudentDetailsDto Student { get; set; }
        public string FormMasterId { get; set; }
        public EmployeeDto FormMaster { get; set; }
        public string ClassArmId { get; set; }
        public ClassArmDto ClassArm { get; set; }
    }
    public class ClassTeacherAssignmentDto
    {
        public IEnumerable<SelectListItem> TeachersName { get; set; }
        public IEnumerable<SelectListItem> ClassName { get; set; }
    }
    public class SubjectTeacherAssignmentDto
    {
        public IEnumerable<SelectListItem> TeachersName { get; set; }
        public IEnumerable<SelectListItem> SubjectsName { get; set; }
        public IEnumerable<SelectListItem> ClassName { get; set; }

    }

    public class ClassAndArmDropDownDto
    {
        public IEnumerable<SelectListItem> ArmName { get; set; }
        public IEnumerable<SelectListItem> ClassName { get; set; }

    }
}
