using NAITSSMS.Application.DTOs;
using NAITSSMS.Domain.Enum;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.IService
{
    public interface IAccountingService
    {
        Task<AccountingDashboardSummaryDto> GetDashboardSummaryAsync(
            string userId,
            string? termId = null,
            CancellationToken ct = default);

        Task<AccountingIncomeStatementDefaultsDto> GetIncomeStatementDefaultsAsync(
            string userId,
            CancellationToken ct = default);

        Task<AccountingIncomeStatementDto> GetIncomeStatementAsync(
            string userId,
            short year,
            byte startMonth,
            byte endMonth,
            CancellationToken ct = default);

        Task<IList<SelectListItem>> GetTermsDropdownForSessionAsync(
            string sessionId,
            CancellationToken ct = default);

        /// <summary>Legacy RegistrationService.GetClassLevelSchoolFees.</summary>
        Task<AccountingSetSchoolFeesLoadDto> GetClassLevelSchoolFeesAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy RegistrationService.AddOrUpdateFeePrices (returns 3 or 4).</summary>
        Task<AccountingSetSchoolFeesSaveResultDto> AddOrUpdateFeePricesAsync(
            string userId,
            AccountingSetSchoolFeesDto model,
            CancellationToken ct = default);

        /// <summary>Legacy RegistrationService.GetFreshiersList.</summary>
        Task<AccountingNewStudentFeesLoadDto> GetFreshiersListAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy RegistrationService.PayForSelectedFreshiersRegistration.</summary>
        Task<object> PayForSelectedFreshiersRegistrationAsync(
            string userId,
            IList<StudentIdsAndClassCategoryBto> studentIds,
            CancellationToken ct = default);

        /// <summary>Legacy RegistrationService.GetSelectedStudentFreshPaymentHistoryById.</summary>
        Task<List<StudentTermlyFees2Nd>> GetSelectedStudentFreshPaymentHistoryByIdAsync(
            string studentLevelPaymentId,
            CancellationToken ct = default);

        /// <summary>Legacy RegistrationService.SingleStudentCurrentTermsFeePartPayment (+ controller enrich).</summary>
        Task<SingleStudentTermlyFeesReturnData> SingleStudentFreshPartPaymentAsync(
            string userId,
            AccountingFreshPartPaymentRequestDto request,
            CancellationToken ct = default);

        /// <summary>Legacy RegistrationService.GetReturningStudentsList.</summary>
        Task<AccountingReturningStudentFeesLoadDto> GetReturningStudentsListAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy RegistrationService.PayForSelectedReturningStudents.</summary>
        Task<object> PayForSelectedReturningStudentsAsync(
            string userId,
            IList<StudentIdsAndClassCategoryBto> studentIds,
            CancellationToken ct = default);

        /// <summary>Legacy RegistrationService.GetSelectedReturningStudentPaymentHistoryById.</summary>
        Task<List<StudentTermlyFees2Nd>> GetSelectedReturningStudentPaymentHistoryByIdAsync(
            string userId,
            string studentLevelPaymentId,
            CancellationToken ct = default);

        /// <summary>Legacy StudentFees/ExecutiveReport sessions (+ terms when sessions &gt; 1).</summary>
        Task<AccountingExecutiveReportDefaultsDto> GetExecutiveReportDefaultsAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy StudentFees/TermDropdownForSelectedSession (Value = YearlyAndTermlyStatus.Id + Select/-1).</summary>
        Task<IList<SelectListItem>> GetExecutiveReportTermsAsync(
            string sessionId,
            CancellationToken ct = default);

        /// <summary>Legacy RegistrationService.GetTermlyReportByTermId.</summary>
        Task<TermlyPaymentReportDto> GetTermlyReportByTermIdAsync(
            string userId,
            string selectedTermId,
            CancellationToken ct = default);

        /// <summary>Legacy RegistrationService.GetStudentByParameter (Student Payment History search).</summary>
        Task<List<StudentFieldExtraction4Accounting2ndDto>> GetStudentByParameterAsync(
            string userId,
            string searchParameter,
            CancellationToken ct = default);

        /// <summary>Legacy RegistrationService.GetSelectedStudentPaymentHistoryById.</summary>
        Task<List<StudentAcademicProgressHistoryStrokPaymentDto>> GetSelectedStudentPaymentHistoryByIdAsync(
            string userId,
            string studentId,
            CancellationToken ct = default);

        /// <summary>Legacy AccountingService.CreateOptionalFeesCategory.</summary>
        Task<AccountingCreateOptionalFeesCategoryResultDto> CreateOptionalFeesCategoryAsync(
            string userId,
            string categoryName,
            CancellationToken ct = default);

        /// <summary>Legacy AccountingService.CreateIncomeCategory.</summary>
        Task<AccountingCreateIncomeCategoryResultDto> CreateIncomeCategoryAsync(
            string userId,
            string categoryName,
            CancellationToken ct = default);

        /// <summary>Legacy AccountingService.CreateExpenditureCategory.</summary>
        Task<AccountingCreateExpenditureCategoryResultDto> CreateExpenditureCategoryAsync(
            string userId,
            string categoryName,
            CancellationToken ct = default);

        /// <summary>Legacy EmployeeIncome/RegisterIncome GET.</summary>
        Task<AccountingRegisterIncomeFormDto> GetRegisterIncomeFormAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy AccountingService.CreateIncome.</summary>
        Task<AccountingCreateIncomeResultDto> CreateIncomeAsync(
            string userId,
            AccountingRegisterIncomeDto model,
            CancellationToken ct = default);

        /// <summary>Legacy EmployeeIncome/RegisterExpenses GET.</summary>
        Task<AccountingRegisterExpenditureFormDto> GetRegisterExpenditureFormAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy AccountingService.CreateExpenditure.</summary>
        Task<AccountingCreateExpenditureResultDto> CreateExpenditureAsync(
            string userId,
            AccountingRegisterExpenditureDto model,
            CancellationToken ct = default);

        /// <summary>Legacy AccountingService.GetAllEmployees (EmployeeList).</summary>
        Task<List<AccountingEmployeeListItemDto>> GetAllEmployeesAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy SetIncomeStructure GET.</summary>
        Task<AccountingEmployeeIncomeStructureDto?> GetSetIncomeStructureFormAsync(
            string userId,
            string employeeId,
            CancellationToken ct = default);

        /// <summary>Legacy GetEmployeeByIdNo + QualificationStaus (ViewIncomeStructure).</summary>
        Task<AccountingEmployeeIncomeStructureDto?> GetEmployeeIncomeStructureAsync(
            string userId,
            string employeeId,
            CancellationToken ct = default);

        /// <summary>Legacy CreateEmployeeStructure (SetIncomeStructure POST).</summary>
        Task<AccountingEmployeeIncomeStructureMutationResultDto> CreateEmployeeStructureAsync(
            string userId,
            AccountingEmployeeIncomeStructureDto model,
            CancellationToken ct = default);

        /// <summary>Legacy DeleteIncomeStructure (true → status 1).</summary>
        Task<AccountingEmployeeIncomeStructureMutationResultDto> DeleteIncomeStructureAsync(
            string userId,
            string employeeId,
            CancellationToken ct = default);

        /// <summary>Legacy GetMonthlyPaymentSlip.</summary>
        Task<AccountingMonthlyPaymentSlipDto> GetMonthlyPaymentSlipAsync(
            string userId,
            DateTime paymentDate,
            CancellationToken ct = default);

        /// <summary>Legacy CreateMonthlyPayment (SubmitMonthlyPayment → Json(1)).</summary>
        Task<AccountingMonthlyPayrollMutationResultDto> CreateMonthlyPaymentAsync(
            string userId,
            List<AccountingMonthlyPaymentSaveDto> monthlyPayment,
            CancellationToken ct = default);

        /// <summary>Legacy ConfirmMonthlyPayment (level id).</summary>
        Task<AccountingMonthlyPayrollMutationResultDto> ConfirmMonthlyPaymentAsync(
            string userId,
            string levelId,
            CancellationToken ct = default);

        /// <summary>Legacy SubmitPaymentForAsingleEmployee (Json(0)).</summary>
        Task<AccountingMonthlyPayrollMutationResultDto> SubmitPaymentForASingleEmployeeAsync(
            string userId,
            AccountingMonthlyPaymentSaveDto model,
            CancellationToken ct = default);

        /// <summary>Legacy DeleteRecentlyGeneratedPayment.</summary>
        Task<AccountingMonthlyPayrollMutationResultDto> DeleteRecentlyGeneratedPaymentAsync(
            string userId,
            string monthId,
            CancellationToken ct = default);

        /// <summary>Legacy GetCommentOfAnEmployee.</summary>
        Task<string> GetEmployeeMonthlyPaymentCommentAsync(
            string userId,
            string employeeMonthlyId,
            CancellationToken ct = default);

        /// <summary>Legacy SaveComments.</summary>
        Task<AccountingMonthlyPayrollMutationResultDto> SaveEmployeeMonthlyPaymentCommentAsync(
            string userId,
            string employeeMonthlyId,
            string commentInput,
            CancellationToken ct = default);

        /// <summary>Legacy GetYearAndMonthDropdownForSalary / SalaryPaymentReport.</summary>
        Task<AccountingSalaryReportDefaultsDto> GetSalaryReportDefaultsAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy GetMonthDropDownForSalary / MonthDropdownForSelectedYearSalary.</summary>
        Task<List<SelectListItem>> GetSalaryReportMonthsAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy GetSalaryReportForSelectedPeriod.</summary>
        Task<List<AccountingMainSalaryReportDto>> GetSalaryReportForSelectedPeriodAsync(
            string userId,
            short year,
            byte startMonth,
            byte endMonth,
            CancellationToken ct = default);

        /// <summary>Legacy EmployeeIncome/IncomeCapturingIndex.</summary>
        Task<AccountingIncomeCapturingIndexDto> GetIncomeCapturingIndexAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy GetCurrentWeekIncome.</summary>
        Task<List<AccountingIncomeItemDto>> GetCurrentWeekIncomeAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy GetIncomeReportForSelectedPeriod.</summary>
        Task<List<AccountingIncomeItemDto>> GetIncomeReportForSelectedPeriodAsync(
            string userId,
            short year,
            byte startMonth,
            byte endMonth,
            CancellationToken ct = default);

        /// <summary>Legacy GetAnItemOfIncomeById + GetIncomeCategoryListForEditForm.</summary>
        Task<AccountingIncomeItemEditFormDto?> GetIncomeItemForEditAsync(
            string userId,
            string id,
            CancellationToken ct = default);

        /// <summary>Legacy UpdateIncomeItem.</summary>
        Task<AccountingIncomeItemMutationResultDto> UpdateIncomeItemAsync(
            string userId,
            AccountingIncomeItemDto model,
            CancellationToken ct = default);

        /// <summary>Legacy DeleteAnItemOfIncomeById.</summary>
        Task<AccountingIncomeItemMutationResultDto> DeleteIncomeItemAsync(
            string userId,
            string id,
            CancellationToken ct = default);

        /// <summary>Legacy ConfirmRecentIncomeList.</summary>
        Task<AccountingIncomeItemMutationResultDto> ConfirmRecentIncomeListAsync(
            string userId,
            string weekId,
            CancellationToken ct = default);

        /// <summary>Legacy EmployeeIncome/ExpenditureCapturingIndex.</summary>
        Task<AccountingExpenditureCapturingIndexDto> GetExpenditureCapturingIndexAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy GetCurrentWeekExpense.</summary>
        Task<List<AccountingIncomeItemDto>> GetCurrentWeekExpenseAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy GetExpenditureReportForSelectedPeriod.</summary>
        Task<List<AccountingIncomeItemDto>> GetExpenditureReportForSelectedPeriodAsync(
            string userId,
            short year,
            byte startMonth,
            byte endMonth,
            CancellationToken ct = default);

        /// <summary>Legacy GetAnItemOfExpensesById + GetExpenditureCategoryListForEditForm.</summary>
        Task<AccountingIncomeItemEditFormDto?> GetExpenseItemForEditAsync(
            string userId,
            string id,
            CancellationToken ct = default);

        /// <summary>Legacy UpdateExpensesItem.</summary>
        Task<AccountingIncomeItemMutationResultDto> UpdateExpenseItemAsync(
            string userId,
            AccountingIncomeItemDto model,
            CancellationToken ct = default);

        /// <summary>Legacy DeleteAnItemOfExpensesById.</summary>
        Task<AccountingIncomeItemMutationResultDto> DeleteExpenseItemAsync(
            string userId,
            string id,
            CancellationToken ct = default);

        /// <summary>Legacy ConfirmRecentExpenditureList.</summary>
        Task<AccountingIncomeItemMutationResultDto> ConfirmRecentExpenditureListAsync(
            string userId,
            string weekId,
            CancellationToken ct = default);

        /// <summary>Legacy EmployeeIncome/IncomeCategoryIndex (list + year/month/category dropdowns).</summary>
        Task<AccountingIncomeCategoryIndexDto> GetIncomeCategoryIndexAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy GetIncomeCategoryList.</summary>
        Task<List<AccountingIncomeCategoryDto>> GetIncomeCategoryListAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy UpdateIncomeCategory (EditIncomeCategory).</summary>
        Task<AccountingIncomeCategoryMutationResultDto> UpdateIncomeCategoryAsync(
            string userId,
            AccountingIncomeCategoryDto model,
            CancellationToken ct = default);

        /// <summary>Legacy DeleteIncomeCategoryById (true → status 0).</summary>
        Task<AccountingIncomeCategoryMutationResultDto> DeleteIncomeCategoryByIdAsync(
            string userId,
            string id,
            CancellationToken ct = default);

        /// <summary>Legacy GetMonthDropDownForIncome / MonthDropdownForSelectedYearIncome.</summary>
        Task<List<SelectListItem>> GetMonthDropdownForIncomeAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy GetCategoryIncomeReport.</summary>
        Task<List<AccountingIncomeItemDto>> GetCategoryIncomeReportAsync(
            string userId,
            short year,
            byte startMonth,
            byte endMonth,
            string categoryId,
            CancellationToken ct = default);

        /// <summary>Legacy GetIncomeCategoryAnalysisForSelectedPeriod.</summary>
        Task<List<AccountingIncomeItemDto>> GetIncomeCategoryAnalysisForSelectedPeriodAsync(
            string userId,
            short year,
            byte startMonth,
            byte endMonth,
            string? categoryId,
            int sortType,
            int numberToPick,
            CancellationToken ct = default);

        /// <summary>Legacy EmployeeIncome/ExpenditureCategoryIndex.</summary>
        Task<AccountingExpenditureCategoryIndexDto> GetExpenditureCategoryIndexAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy GetExpensesCategoryList.</summary>
        Task<List<AccountingExpenditureCategoryDto>> GetExpenditureCategoryListAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy UpdateExpenditureCategory (EditExpenditureCategory).</summary>
        Task<AccountingExpenditureCategoryMutationResultDto> UpdateExpenditureCategoryAsync(
            string userId,
            AccountingExpenditureCategoryDto model,
            CancellationToken ct = default);

        /// <summary>Legacy DeleteExpenseCategoryById (true → status 0).</summary>
        Task<AccountingExpenditureCategoryMutationResultDto> DeleteExpenditureCategoryByIdAsync(
            string userId,
            string id,
            CancellationToken ct = default);

        /// <summary>Legacy GetMonthDropDownForExpenditure / MonthDropdownForSelectedYearExpenditure.</summary>
        Task<List<SelectListItem>> GetMonthDropdownForExpenditureAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy GetCategoryExpenseReport.</summary>
        Task<List<AccountingIncomeItemDto>> GetCategoryExpenseReportAsync(
            string userId,
            short year,
            byte startMonth,
            byte endMonth,
            string categoryId,
            CancellationToken ct = default);

        /// <summary>Legacy GetExpenditureCategoryAnalysisForSelectedPeriod.</summary>
        Task<List<AccountingIncomeItemDto>> GetExpenditureCategoryAnalysisForSelectedPeriodAsync(
            string userId,
            short year,
            byte startMonth,
            byte endMonth,
            string? categoryId,
            int sortType,
            int numberToPick,
            CancellationToken ct = default);

        /// <summary>Legacy AccountingService.GetOptionalFeesCategoryList.</summary>
        Task<List<AccountingOptionalFeesCategoryDto>> GetOptionalFeesCategoryListAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy AccountingService.UpdateOptionalFeeCategory.</summary>
        Task<AccountingOptionalFeesCategoryMutationResultDto> UpdateOptionalFeeCategoryAsync(
            string userId,
            AccountingOptionalFeesCategoryDto model,
            CancellationToken ct = default);

        /// <summary>Legacy AccountingService.DeleteOptionalFeeCategoryById (true → status 0).</summary>
        Task<AccountingOptionalFeesCategoryMutationResultDto> DeleteOptionalFeeCategoryByIdAsync(
            string userId,
            string id,
            CancellationToken ct = default);

        /// <summary>Legacy AccountingService.CreateOptionalFeeItem (RegisterOptionalFees POST).</summary>
        Task<AccountingCreateOptionalFeesItemResultDto> CreateOptionalFeeItemAsync(
            string userId,
            AccountingOptionalFeesItemCreateDto model,
            CancellationToken ct = default);

        /// <summary>Legacy AccountingService.GetOptionalFeeItemList / RegistrationService.GetOptionalItemsByCategoryId.</summary>
        Task<List<AccountingOptionalFeesItemDto>> GetOptionalFeeItemListAsync(
            string userId,
            string? categoryId = null,
            CancellationToken ct = default);

        /// <summary>Legacy AccountingService.GetOptionalFeeItemById.</summary>
        Task<AccountingOptionalFeesItemDto?> GetOptionalFeeItemByIdAsync(
            string userId,
            string itemId,
            CancellationToken ct = default);

        /// <summary>Legacy AccountingService.UpdateOptionalFeeItem.</summary>
        Task<AccountingOptionalFeesItemMutationResultDto> UpdateOptionalFeeItemAsync(
            string userId,
            AccountingOptionalFeesItemUpdateDto model,
            CancellationToken ct = default);

        /// <summary>Legacy AccountingService.DeleteOptionalFeeItemById (true → status 0).</summary>
        Task<AccountingOptionalFeesItemMutationResultDto> DeleteOptionalFeeItemByIdAsync(
            string userId,
            string id,
            CancellationToken ct = default);

        /// <summary>Legacy StudentOptionalChargesAssignmentIndex GET.</summary>
        Task<AccountingOptionalChargesAssignmentFormDto> GetOptionalChargesAssignmentFormAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy RenderArmsDropdown.</summary>
        Task<List<SelectListItem>> GetOptionalChargesAssignmentArmsAsync(
            string userId,
            string classId,
            CancellationToken ct = default);

        /// <summary>Legacy RenderStudentInTheSelectedArm / RenderCategoryStudentsInTheSelectedArm.</summary>
        Task<AccountingOptionalChargesAssignmentStudentsDto> GetOptionalChargesAssignmentStudentsAsync(
            string userId,
            string armId,
            string categoryId,
            byte catStatus = 1,
            CancellationToken ct = default);

        /// <summary>Legacy AssignOptionalFeeToArmStudentsOrStudentsInCategory.</summary>
        Task<AccountingOptionalChargesAssignmentResultDto> AssignOptionalChargesAsync(
            string userId,
            AccountingOptionalChargesAssignmentRequestDto model,
            CancellationToken ct = default);

        /// <summary>Legacy UnAssignOptionalFeeToArmStudentsOrStudentsInCategory.</summary>
        Task<AccountingOptionalChargesAssignmentResultDto> UnassignOptionalChargesAsync(
            string userId,
            AccountingOptionalChargesAssignmentRequestDto model,
            CancellationToken ct = default);

        /// <summary>Legacy StudentOptionalPaymentIndex ResultType=0 form (sessions + TermStatus terms).</summary>
        Task<AccountingOptionalPaymentDefaultersFormDto> GetOptionalPaymentDefaultersFormAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy TermEnumTypeDropdownForSelectedSession (Value = TermStatus int).</summary>
        Task<IList<SelectListItem>> GetOptionalPaymentDefaultersTermsAsync(
            string userId,
            string sessionId,
            CancellationToken ct = default);

        /// <summary>Legacy StudentTermlyPaymentDefaulters.</summary>
        Task<AccountingOptionalPaymentDefaultersDto> GetTermlyOptionalPaymentDefaultersAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            CancellationToken ct = default);

        /// <summary>Legacy StudentSessionalPaymentDefaulters.</summary>
        Task<AccountingOptionalPaymentDefaultersDto> GetYearlyOptionalPaymentDefaultersAsync(
            string userId,
            string sessionId,
            CancellationToken ct = default);

        /// <summary>Legacy StudentTermlyPaymentReport.</summary>
        Task<AccountingOptionalPaymentReportDto> GetTermlyOptionalPaymentReportAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            CancellationToken ct = default);

        /// <summary>Legacy StudentSessionalPaymentReport.</summary>
        Task<AccountingOptionalPaymentReportDto> GetYearlyOptionalPaymentReportAsync(
            string userId,
            string sessionId,
            CancellationToken ct = default);

        /// <summary>Legacy StudentOptionalPaymentIndex / ResultTypeMenu dropdowns.</summary>
        Task<AccountingOptionalPaymentHomeFormDto> GetOptionalPaymentHomeFormAsync(
            string userId,
            CancellationToken ct = default);

        /// <summary>Legacy LoadForClassArmPaymentProcessing.</summary>
        Task<AccountingOptionalPaymentProcessingDto> LoadOptionalPaymentProcessingByArmAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            CurrentClass currentClass,
            ArmName armName,
            string sessionText,
            CancellationToken ct = default);

        /// <summary>Legacy LoadForPerClassPaymentProcessing.</summary>
        Task<AccountingOptionalPaymentProcessingDto> LoadOptionalPaymentProcessingByClassAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            CurrentClass currentClass,
            string sessionText,
            CancellationToken ct = default);

        /// <summary>Legacy LoadForAllStudentsPaymentProcessing.</summary>
        Task<AccountingOptionalPaymentProcessingDto> LoadOptionalPaymentProcessingAllAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            string sessionText,
            CancellationToken ct = default);

        /// <summary>Legacy StudentOptionalPaymentHistory.</summary>
        Task<AccountingOptionalPaymentReportDto> GetStudentOptionalPaymentHistoryAsync(
            string userId,
            string studentId,
            CancellationToken ct = default);

        /// <summary>Legacy GetRangePaymentReport.</summary>
        Task<AccountingOptionalRangeReportDto> GetOptionalPaymentRangeReportAsync(
            string userId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken ct = default);

        /// <summary>Legacy GetOptionalPaymentCategoryItemReport.</summary>
        Task<AccountingOptionalRangeReportDto> GetOptionalPaymentCategoryRangeReportAsync(
            string userId,
            DateTime startDate,
            DateTime endDate,
            string categoryId,
            CancellationToken ct = default);
    }
}
