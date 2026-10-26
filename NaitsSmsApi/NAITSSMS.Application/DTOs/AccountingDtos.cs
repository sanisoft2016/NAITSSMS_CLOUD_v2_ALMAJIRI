using System;
using System.Collections.Generic;
using NAITSSMS.Domain.Enum;

namespace NAITSSMS.Application.DTOs
{
    public class AccountingDashboardSummaryDto
    {
        /// <summary>False when no Current academic term exists (legacy empty summary string).</summary>
        public bool HasData { get; set; }

        public string? CurrentTermId { get; set; }
        public string? CurrentSessionId { get; set; }

        public int ClearedStudents { get; set; }
        public double TotalPayments { get; set; }
        public int PartlyClearedStudents { get; set; }
        public double PartPayments { get; set; }
        public double PartOutstanding { get; set; }
        public int UnclearedStudents { get; set; }
        public double Outstanding { get; set; }

        public double SummaryTotalPayments => TotalPayments + PartPayments;
        public double SummaryTotalOutstanding => PartOutstanding + Outstanding;
    }

    public class AccountingIncomeStatementDto
    {
        public short Year { get; set; }
        public byte StartMonth { get; set; }
        public byte EndMonth { get; set; }

        public double OtherExpenses { get; set; }
        public double SalaryPayment { get; set; }
        public double TotalExpenditure { get; set; }
        public double TotalIncome { get; set; }
        public double NetProfit { get; set; }
        public double OtherIncome { get; set; }
        public double FixedFeePayment { get; set; }
        public double OptionalFeePayment { get; set; }

        /// <summary>Legacy GetIncomeStatement &amp;-joined currency string (indexes 0–7).</summary>
        public string LegacyDelimited { get; set; } = string.Empty;
    }

    public class AccountingIncomeStatementDefaultsDto
    {
        public short Year { get; set; }
        public byte Month { get; set; }
        public List<SelectListItem> Years { get; set; } = new();
        public List<SelectListItem> Months { get; set; } = new();
        public AccountingIncomeStatementDto? Statement { get; set; }
    }

    /// <summary>Legacy SetSchoolFeesDto (string amounts as shown in the form).</summary>
    public class AccountingSetSchoolFeesDto
    {
        public string Id { get; set; } = string.Empty;
        public string SchoolId { get; set; } = string.Empty;
        public string YearlyAndTermlyStatusId { get; set; } = string.Empty;

        public string FreshPlayClass { get; set; } = string.Empty;
        public string FreshPreNursery { get; set; } = string.Empty;
        public string FreshNursery { get; set; } = string.Empty;
        public string FreshPrimary { get; set; } = string.Empty;
        public string FreshJss { get; set; } = string.Empty;
        public string FreshScience { get; set; } = string.Empty;
        public string FreshArt { get; set; } = string.Empty;
        public string FreshCommercial { get; set; } = string.Empty;

        public string RetnPlayClass { get; set; } = string.Empty;
        public string RetnPreNursery { get; set; } = string.Empty;
        public string RetnNursery { get; set; } = string.Empty;
        public string RetnPrimary { get; set; } = string.Empty;
        public string RetnJss { get; set; } = string.Empty;
        public string RetnScience { get; set; } = string.Empty;
        public string RetnArt { get; set; } = string.Empty;
        public string RetnCommercial { get; set; } = string.Empty;
    }

    public class AccountingSetSchoolFeesLoadDto
    {
        /// <summary>False when no Current academic term (legacy returns null model).</summary>
        public bool HasCurrentTerm { get; set; }

        public AccountingSetSchoolFeesDto? Fees { get; set; }
    }

    public class AccountingSetSchoolFeesSaveResultDto
    {
        /// <summary>Legacy AddOrUpdateFeePrices: 3 = no current term, 4 = success.</summary>
        public byte Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Legacy LoadStudentsForRegistarationFees GET.</summary>
    public class AccountingNewStudentFeesLoadDto
    {
        /// <summary>False when GetFreshiersList returns null (fees not set).</summary>
        public bool FeesConfigured { get; set; }

        /// <summary>Legacy ShowMessage when list is empty.</summary>
        public string? InfoMessage { get; set; }

        public List<AccountingNewStudentFeesStudentDto> Students { get; set; } = new();
    }

    public class AccountingNewStudentFeesStudentDto
    {
        public string StudentLevelPaymentId { get; set; } = string.Empty;
        public string AdmisionNo { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string ClassLabel { get; set; } = string.Empty;
        public string ArmName { get; set; } = string.Empty;
        public string CurrentClass { get; set; } = string.Empty;
        public ClassCategoryStatus ClassCategoryStatus { get; set; }
        public FeesPaymentStatus CurrentTermPaymentStatus { get; set; }
    }

    public class AccountingPayFreshiersRequestDto
    {
        public List<StudentIdsAndClassCategoryBto> StudentIds { get; set; } = new();
    }

    public class AccountingFreshPartPaymentRequestDto
    {
        public string StudentLevelPaymentId { get; set; } = string.Empty;
        public double HisFee { get; set; }
        public double ActualFees { get; set; }
        public string AdmisnNo { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string TermAndSession { get; set; } = string.Empty;
        public string ClassAndArm { get; set; } = string.Empty;
        public string PaymentId { get; set; } = string.Empty;
        public PaymentType PaymentType { get; set; }
        public double Discount { get; set; }
        public string ReasonForDiscount { get; set; } = string.Empty;
        public DateTime? PaymentDate { get; set; }
    }

    /// <summary>Legacy LoadStudentsForTermlyFees GET (GetReturningStudentsList).</summary>
    public class AccountingReturningStudentFeesLoadDto
    {
        public bool FeesConfigured { get; set; }
        public string? InfoMessage { get; set; }
        public List<AccountingReturningStudentFeesStudentDto> Students { get; set; } = new();
    }

    public class AccountingReturningStudentFeesStudentDto
    {
        public string StudentLevelPaymentId { get; set; } = string.Empty;
        public string AdmisionNo { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string ClassLabel { get; set; } = string.Empty;
        public string ArmName { get; set; } = string.Empty;
        public string CurrentClass { get; set; } = string.Empty;
        public ClassCategoryStatus ClassCategoryStatus { get; set; }
        public FeesPaymentStatus CurrentTermPaymentStatus { get; set; }
        public FeesPaymentStatus OtherTermsPaymentStatus { get; set; }
    }

    public class AccountingPayReturningStudentsRequestDto
    {
        public List<StudentIdsAndClassCategoryBto> StudentIds { get; set; } = new();
    }

    /// <summary>Legacy StudentFees/ExecutiveReport GET (sessions + optional first-session terms).</summary>
    public class AccountingExecutiveReportDefaultsDto
    {
        public List<SelectListItem> Sessions { get; set; } = new();
        public List<SelectListItem> Terms { get; set; } = new();
    }

    /// <summary>Legacy StudentFees/SelectedStudentPaymentHistory wrapper.</summary>
    public class AccountingStudentPaymentHistoryLoadDto
    {
        public string AdmsnNo { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public List<StudentAcademicProgressHistoryStrokPaymentDto> Terms { get; set; } = new();
    }

    /// <summary>Legacy CreateOptionalFeesCategory POST result.</summary>
    public class AccountingCreateOptionalFeesCategoryResultDto
    {
        /// <summary>True when inserted; false when duplicate CategoryName for school.</summary>
        public bool Succeeded { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Legacy CreateIncomeCategory POST result.</summary>
    public class AccountingCreateIncomeCategoryResultDto
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Legacy CreateExpenditureCategory POST result.</summary>
    public class AccountingCreateExpenditureCategoryResultDto
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Legacy ItemCategoryDto for income categories (string Id for cloud).</summary>
    public class AccountingIncomeCategoryDto
    {
        public string Id { get; set; } = string.Empty;
        public string SchoolId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
    }

    /// <summary>Legacy ItemCategoryDto for expenditure categories (string Id for cloud).</summary>
    public class AccountingExpenditureCategoryDto
    {
        public string Id { get; set; } = string.Empty;
        public string SchoolId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
    }

    /// <summary>Legacy Edit/DeleteExpenditureCategory JSON status (0 = ok).</summary>
    public class AccountingExpenditureCategoryMutationResultDto
    {
        public int Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Legacy ExpenditureCategoryIndex ViewBag + Model.</summary>
    public class AccountingExpenditureCategoryIndexDto
    {
        public List<SelectListItem> Years { get; set; } = new();
        public List<SelectListItem> Months { get; set; } = new();
        public List<SelectListItem> CategoryOptions { get; set; } = new();
        public List<AccountingExpenditureCategoryDto> Categories { get; set; } = new();
    }

    /// <summary>Legacy Edit/DeleteIncomeCategory JSON status (0 = ok).</summary>
    public class AccountingIncomeCategoryMutationResultDto
    {
        public int Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Legacy IncomeCategoryIndex ViewBag + Model.</summary>
    public class AccountingIncomeCategoryIndexDto
    {
        public List<SelectListItem> Years { get; set; } = new();
        public List<SelectListItem> Months { get; set; } = new();
        public List<SelectListItem> CategoryOptions { get; set; } = new();
        public List<AccountingIncomeCategoryDto> Categories { get; set; } = new();
    }

    /// <summary>Legacy ItemsDto row for income category period / analysis reports.</summary>
    public class AccountingIncomeItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string ItemCategoryId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public DateTime CapturedDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public string WeekId { get; set; } = string.Empty;
        public string WeekOfMonth { get; set; } = string.Empty;
        public double Cost { get; set; }
        public string Details { get; set; } = string.Empty;
    }

    /// <summary>Legacy IncomeCapturingIndex ViewBag + Model.</summary>
    public class AccountingIncomeCapturingIndexDto
    {
        public List<SelectListItem> Years { get; set; } = new();
        public List<SelectListItem> Months { get; set; } = new();
        public List<AccountingIncomeItemDto> Items { get; set; } = new();
        public string? CurrentWeekId { get; set; }
    }

    /// <summary>Legacy ExpenditureCapturingIndex ViewBag + Model.</summary>
    public class AccountingExpenditureCapturingIndexDto
    {
        public List<SelectListItem> Years { get; set; } = new();
        public List<SelectListItem> Months { get; set; } = new();
        public List<AccountingIncomeItemDto> Items { get; set; } = new();
        public string? CurrentWeekId { get; set; }
    }

    /// <summary>Legacy EditIncomeItem GET payload.</summary>
    public class AccountingIncomeItemEditFormDto
    {
        public AccountingIncomeItemDto Item { get; set; } = new();
        public List<SelectListItem> Categories { get; set; } = new();
    }

    /// <summary>Legacy EditIncomeItem / Delete / Confirm JSON status (0 = ok).</summary>
    public class AccountingIncomeItemMutationResultDto
    {
        public int Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Legacy RegisterIncome GET form (categories + today week banner).</summary>
    public class AccountingRegisterIncomeFormDto
    {
        public bool HasCategories { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CapturedDate { get; set; }
        public string DateDisplay { get; set; } = string.Empty;
        public string WeekPosition { get; set; } = string.Empty;
        public List<SelectListItem> Categories { get; set; } = new();
    }

    /// <summary>Legacy RegisterIncome POST / CreateIncome result.</summary>
    public class AccountingCreateIncomeResultDto
    {
        /// <summary>0 success, 1 closed period, 2 must confirm current week first.</summary>
        public int Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Legacy ItemsDto payload for RegisterIncome POST.</summary>
    public class AccountingRegisterIncomeDto
    {
        public string ItemCategoryId { get; set; } = string.Empty;
        public DateTime CapturedDate { get; set; }
        public string Details { get; set; } = string.Empty;
        public double Cost { get; set; }
    }

    /// <summary>Legacy RegisterExpenses GET form (categories + today week banner).</summary>
    public class AccountingRegisterExpenditureFormDto
    {
        public bool HasCategories { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CapturedDate { get; set; }
        public string DateDisplay { get; set; } = string.Empty;
        public string WeekPosition { get; set; } = string.Empty;
        public List<SelectListItem> Categories { get; set; } = new();
    }

    /// <summary>Legacy RegisterExpenses POST / CreateExpenditure result.</summary>
    public class AccountingCreateExpenditureResultDto
    {
        /// <summary>0 success, 1 closed period, 2 must confirm current week first.</summary>
        public int Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Legacy ItemsDto payload for RegisterExpenses POST.</summary>
    public class AccountingRegisterExpenditureDto
    {
        public string ItemCategoryId { get; set; } = string.Empty;
        public DateTime CapturedDate { get; set; }
        public string Details { get; set; } = string.Empty;
        public double Cost { get; set; }
    }

    /// <summary>Legacy EmployeesXtractionDto for EmployeeList.</summary>
    public class AccountingEmployeeListItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public EmployeeStatus EmployeeStatus { get; set; }
        public string EmployeeStatusLabel { get; set; } = string.Empty;
        public EmployeeIncomeStructureStatus EmployeeIncomeStructureStatus { get; set; }
    }

    /// <summary>Legacy EmployeeIncomeStructureDto (string ids for cloud).</summary>
    public class AccountingEmployeeIncomeStructureDto
    {
        public string Id { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public EmployeeQualificationStatus EmployeeQualificationStatus { get; set; }
        public double BasicSalary { get; set; }
        public double Allowance { get; set; }
        public List<SelectListItem> Qualifications { get; set; } = new();
    }

    /// <summary>Legacy SetIncomeStructure POST / DeleteIncomeStructure result.</summary>
    public class AccountingEmployeeIncomeStructureMutationResultDto
    {
        public int Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Legacy PaymentSlip structure-mode row (from income structures).</summary>
    public class AccountingPayrollStructureRowDto
    {
        public string EmployeeId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string EmployeeIdNo { get; set; } = string.Empty;
        public double BasicSalary { get; set; }
        public double Allowance { get; set; }
    }

    /// <summary>Legacy EmployeesMonthlyPaymentDto row for PaymentSlip2nd.</summary>
    public class AccountingMonthlyPaymentRowDto
    {
        public string Id { get; set; } = string.Empty;
        public string EmployeeMonthlyPaymentLevelId { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string EmployeeIdNo { get; set; } = string.Empty;
        public double BasicSalary { get; set; }
        public double Allowance { get; set; }
        public double Bonus { get; set; }
        public double Deduction { get; set; }
        public EmployeeMonthlyPaymentStatus EmployeeMonthlyPaymentStatus { get; set; }
        public PaymentType PaymentType { get; set; }
        public string Comments { get; set; } = string.Empty;
        public string MonthId { get; set; } = string.Empty;
        public short Year { get; set; }
        public byte Month { get; set; }
        public EmployeeMonthlyPaymentLevel LevelStatus { get; set; }
    }

    /// <summary>Legacy EmployeesMonthlyPaymentMixAndUnConfirmedPaymentIdDto for PaymentSlip.</summary>
    public class AccountingMonthlyPaymentSlipDto
    {
        public string? UnConfirmedMonthlyPaymentId { get; set; }
        public DateTime PaymentDate { get; set; }
        /// <summary>structure = PaymentSlip; payments = PaymentSlip2nd.</summary>
        public string Mode { get; set; } = "structure";
        public List<AccountingPayrollStructureRowDto>? Structures { get; set; }
        public List<AccountingMonthlyPaymentRowDto>? Payments { get; set; }
    }

    /// <summary>Legacy SubmitMonthlyPayment / SubmitPaymentForAsingleEmployee body.</summary>
    public class AccountingMonthlyPaymentSaveDto
    {
        public string Id { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public string EmployeeMonthlyPaymentLevelId { get; set; } = string.Empty;
        public double BasicSalary { get; set; }
        public double Allowance { get; set; }
        public double Bonus { get; set; }
        public double Deduction { get; set; }
        public EmployeeMonthlyPaymentStatus EmployeeMonthlyPaymentStatus { get; set; }
        public PaymentType PaymentType { get; set; }
        public string Comments { get; set; } = string.Empty;
    }

    /// <summary>Legacy monthly payroll mutation JSON (status 1 / 0).</summary>
    public class AccountingMonthlyPayrollMutationResultDto
    {
        public int Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Legacy SubmitAnEmployeePaymentComments body.</summary>
    public class AccountingMonthlyPaymentCommentDto
    {
        public string CommentInput { get; set; } = string.Empty;
    }

    /// <summary>Legacy SubordinateSalaryReportDto (payment method breakdown).</summary>
    public class AccountingSubordinateSalaryReportDto
    {
        public string MainSalaryReportId { get; set; } = string.Empty;
        public int CashNo { get; set; }
        public double CashAmount { get; set; }
        public int TellerNo { get; set; }
        public double TellerAmount { get; set; }
        public int TransferNo { get; set; }
        public double TransferAmount { get; set; }
    }

    /// <summary>Legacy MainSalaryReportDto row.</summary>
    public class AccountingMainSalaryReportDto
    {
        public string Id { get; set; } = string.Empty;
        public string Month { get; set; } = string.Empty;
        public byte MonthNumber { get; set; }
        public int NoOfEmpl { get; set; }
        public double BasicSalary { get; set; }
        public double Allowance { get; set; }
        public double Bonus { get; set; }
        public double Deduction { get; set; }
        public AccountingSubordinateSalaryReportDto SubordinateSalaryReport { get; set; } = new();
    }

    /// <summary>Legacy YearAndMonthDropdownForSalaryDto + SalaryPaymentReport shell.</summary>
    public class AccountingSalaryReportDefaultsDto
    {
        public List<SelectListItem> Years { get; set; } = new();
        public List<SelectListItem> Months { get; set; } = new();
    }

    /// <summary>Legacy ItemCategoryDto for optional fees categories (string Id for cloud).</summary>
    public class AccountingOptionalFeesCategoryDto
    {
        public string Id { get; set; } = string.Empty;
        public string SchoolId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
    }

    /// <summary>Legacy Edit/Delete optional fee category JSON result (0 = ok, 1 = fail).</summary>
    public class AccountingOptionalFeesCategoryMutationResultDto
    {
        public int Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Legacy RegisterOptionalFees / OptionalChargeItemsDto (string ids for cloud).</summary>
    public class AccountingOptionalFeesItemCreateDto
    {
        public string ItemCategoryId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public double Cost { get; set; }
    }

    /// <summary>Legacy CreateOptionalFeeItem return: 0 ok, 1 no current term, 2 duplicate name.</summary>
    public class AccountingCreateOptionalFeesItemResultDto
    {
        public int Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Legacy OptionalChargeItemsDto row for OptionalFeesIndex.</summary>
    public class AccountingOptionalFeesItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string ItemCategoryId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public double Cost { get; set; }
        public string YearlyAndTermlyStatusId { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    /// <summary>Legacy EditOptionalFeeItem POST body.</summary>
    public class AccountingOptionalFeesItemUpdateDto
    {
        public string Id { get; set; } = string.Empty;
        public string ItemCategoryId { get; set; } = string.Empty;
        public string YearlyAndTermlyStatusId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public double Cost { get; set; }
    }

    /// <summary>Legacy UpdateOptionalFeeItem / DeleteOptionalFeeItemById JSON (0 ok).</summary>
    public class AccountingOptionalFeesItemMutationResultDto
    {
        public int Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Legacy StudentOptionalChargesAssignmentIndex initial form.</summary>
    public class AccountingOptionalChargesAssignmentFormDto
    {
        public List<SelectListItem> Categories { get; set; } = new();
        public List<SelectListItem> Classes { get; set; } = new();
        public List<SelectListItem> Arms { get; set; } = new();
    }

    /// <summary>Legacy GetStudentForAnArmForAnOptionalPayment / category filter result.</summary>
    public class AccountingOptionalChargesAssignmentStudentsDto
    {
        public List<AccountingOptionalAssignmentStudentDto> Students { get; set; } = new();
        public List<string> AssignedStudentIds { get; set; } = new();
        public string? Message { get; set; }
    }

    public class AccountingOptionalAssignmentStudentDto
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string IdNo { get; set; } = string.Empty;
        public byte ClassCategory { get; set; }
        public bool Assigned { get; set; }
    }

    /// <summary>Legacy ArmStudentsOrStudentsInCategoryOptionalFeeAssignment / UnAssignment body.</summary>
    public class AccountingOptionalChargesAssignmentRequestDto
    {
        public string ArmId { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public List<string> StudentIds { get; set; } = new();
    }

    public class AccountingOptionalChargesAssignmentResultDto
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Legacy StudentOptionalPaymentIndex ResultType=0 filter form.</summary>
    public class AccountingOptionalPaymentDefaultersFormDto
    {
        public List<SelectListItem> Sessions { get; set; } = new();
        /// <summary>TermStatus enum values (0/1/2) for the default session.</summary>
        public List<SelectListItem> Terms { get; set; } = new();
    }

    /// <summary>Legacy StudentTermlyPaymentDefaulters / StudentSessionalPaymentDefaulters payload.</summary>
    public class AccountingOptionalPaymentDefaultersDto
    {
        public double TotalOutstanding { get; set; }
        public double TotalDiscount { get; set; }
        public List<AccountingOptionalDefaulterChargeDto> Charges { get; set; } = new();
        public List<AccountingOptionalDefaulterDiscountDto> Discounts { get; set; } = new();
    }

    public class AccountingOptionalDefaulterChargeDto
    {
        public string Id { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string IdNo { get; set; } = string.Empty;
        public string ClassId { get; set; } = string.Empty;
        public string ClassLabel { get; set; } = string.Empty;
        public string ClassArmId { get; set; } = string.Empty;
        public string ArmName { get; set; } = string.Empty;
        public string YearlyAndTermlyStatusId { get; set; } = string.Empty;
        public byte TermStatus { get; set; }
        public string TermStatusLabel { get; set; } = string.Empty;
        public string Session { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string OptionalFeesItemId { get; set; } = string.Empty;
        public double Cost { get; set; }
        public List<AccountingOptionalDefaulterPaymentDto> Payments { get; set; } = new();
    }

    public class AccountingOptionalDefaulterPaymentDto
    {
        public DateTime PaymentDate { get; set; }
        public byte PaymentType { get; set; }
        public string PaymentTypeLabel { get; set; } = string.Empty;
        public double Amount { get; set; }
    }

    public class AccountingOptionalDefaulterDiscountDto
    {
        public string StudentTermlyOptionalChargesId { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public double DiscountAmount { get; set; }
        public string ReasonsForDiscount { get; set; } = string.Empty;
    }

    /// <summary>Legacy StudentTermlyPaymentReport / StudentSessionalPaymentReport payload.</summary>
    public class AccountingOptionalPaymentReportDto
    {
        public double TotalPayment { get; set; }
        public double TotalDiscount { get; set; }
        /// <summary>Unique charges that have at least one payment in the filtered set.</summary>
        public List<AccountingOptionalDefaulterChargeDto> Charges { get; set; } = new();
        public List<AccountingOptionalDefaulterDiscountDto> Discounts { get; set; } = new();
    }

    /// <summary>Legacy GetRangePaymentReport / GetOptionalPaymentCategoryItemReport.</summary>
    public class AccountingOptionalRangeReportDto
    {
        public string CategoryLabel { get; set; } = "All Categories";
        public int NumberOfItems { get; set; }
        public double TotalCharges { get; set; }
        public double TotalPayment { get; set; }
        public double TotalDiscount { get; set; }
        public double TotalOutstanding { get; set; }
        public List<AccountingOptionalRangeChargeDto> Charges { get; set; } = new();
        public List<AccountingOptionalDefaulterDiscountDto> Discounts { get; set; } = new();
    }

    public class AccountingOptionalRangeChargeDto
    {
        public string Id { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string IdNo { get; set; } = string.Empty;
        public string ClassId { get; set; } = string.Empty;
        public string ClassLabel { get; set; } = string.Empty;
        public string ClassArmId { get; set; } = string.Empty;
        public string ArmName { get; set; } = string.Empty;
        public string YearlyAndTermlyStatusId { get; set; } = string.Empty;
        public byte TermStatus { get; set; }
        public string TermStatusLabel { get; set; } = string.Empty;
        public string Session { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public double Cost { get; set; }
        public double AmountPaid { get; set; }
        public DateTime AssignedDate { get; set; }
        public int AssignedYear { get; set; }
    }

    /// <summary>Legacy StudentOptionalPaymentIndex / ResultTypeMenu form.</summary>
    public class AccountingOptionalPaymentHomeFormDto
    {
        public List<SelectListItem> Sessions { get; set; } = new();
        /// <summary>TermStatus enum values (0/1/2).</summary>
        public List<SelectListItem> Terms { get; set; } = new();
        /// <summary>Legacy Value = "{(byte)CurrentClass},{ClassLabel}".</summary>
        public List<SelectListItem> Classes { get; set; } = new();
        /// <summary>Unique ArmName values across the school (legacy).</summary>
        public List<SelectListItem> Arms { get; set; } = new();
    }

    /// <summary>Legacy LoadForClassArmPaymentProcessing / PerClass / All outstanding charge row.</summary>
    public class AccountingOptionalPaymentProcessingChargeDto
    {
        public string Id { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string IdNo { get; set; } = string.Empty;
        public string ClassId { get; set; } = string.Empty;
        public string ClassLabel { get; set; } = string.Empty;
        public byte CurrentClass { get; set; }
        public string ClassArmId { get; set; } = string.Empty;
        public string ArmName { get; set; } = string.Empty;
        public string ItemCategoryId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public double Cost { get; set; }
        public double AmountPaid { get; set; }
        public double DiscountTotal { get; set; }
        public double Outstanding { get; set; }
    }

    public class AccountingOptionalPaymentProcessingDto
    {
        public string SessionAndTerm { get; set; } = string.Empty;
        public string? Message { get; set; }
        public List<AccountingOptionalPaymentProcessingChargeDto> Charges { get; set; } = new();
    }
}
