using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Enum;
using System.Security.Claims;

namespace NAITSSMS.API.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AccountingController : ControllerBase
    {
        private readonly IAccountingService _accountingService;
        private readonly ILogger<AccountingController> _logger;

        public AccountingController(IAccountingService accountingService, ILogger<AccountingController> logger)
        {
            _accountingService = accountingService ?? throw new ArgumentNullException(nameof(accountingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private string GetUserId()
        {
            var claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            var userId = claimsIdentity?.FindFirst(ClaimTypes.PrimarySid)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated");
            }

            return userId;
        }

        /// <summary>Legacy StudentFees/StudentFessIndex dashboard tiles (GetSummaryForAccountingDashboard).</summary>
        [HttpGet("dashboard-summary")]
        public async Task<IActionResult> GetDashboardSummary([FromQuery] string? termId, CancellationToken ct)
        {
            try
            {
                var summary = await _accountingService.GetDashboardSummaryAsync(GetUserId(), termId, ct);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading accounting dashboard summary");
                return StatusCode(500, new { message = "An error occurred while loading the accounting dashboard." });
            }
        }

        /// <summary>Legacy AccountingBase/Index defaults (year/month dropdowns + initial statement).</summary>
        [HttpGet("income-statement/defaults")]
        public async Task<IActionResult> GetIncomeStatementDefaults(CancellationToken ct)
        {
            try
            {
                var defaults = await _accountingService.GetIncomeStatementDefaultsAsync(GetUserId(), ct);
                return Ok(defaults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading income statement defaults");
                return StatusCode(500, new
                {
                    message = "An error occurred while loading income statement defaults.",
                    detail = ex.GetBaseException().Message,
                });
            }
        }

        /// <summary>Legacy terms dropdown for fees pages (Value = YearlyAndTermlyStatus.Id).</summary>
        [HttpGet("dropdown/sessions/{sessionId}/terms")]
        public async Task<IActionResult> GetTermsDropdown(string sessionId, CancellationToken ct)
        {
            try
            {
                var terms = await _accountingService.GetTermsDropdownForSessionAsync(sessionId, ct);
                return Ok(terms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading accounting terms dropdown");
                return StatusCode(500, new { message = "An error occurred while loading terms." });
            }
        }

        /// <summary>Legacy EmployeeIncome/IncomeStatementReportForSelectedPeriod.</summary>
        [HttpGet("income-statement")]
        public async Task<IActionResult> GetIncomeStatement(
            [FromQuery] short year,
            [FromQuery] byte startMonth,
            [FromQuery] byte endMonth,
            CancellationToken ct)
        {
            try
            {
                if (year <= 0 || startMonth == 0 || endMonth == 0 || startMonth == 255 || endMonth == 255)
                {
                    return BadRequest(new { message = "year, startMonth and endMonth are required." });
                }

                var statement = await _accountingService.GetIncomeStatementAsync(
                    GetUserId(),
                    year,
                    startMonth,
                    endMonth,
                    ct);
                return Ok(statement);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading income statement");
                return StatusCode(500, new { message = "An error occurred while loading the income statement." });
            }
        }

        /// <summary>Legacy StudentFees/SetSchoolFees GET (GetClassLevelSchoolFees).</summary>
        [HttpGet("set-school-fees")]
        public async Task<IActionResult> GetSetSchoolFees(CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.GetClassLevelSchoolFeesAsync(GetUserId(), ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading set school fees");
                return StatusCode(500, new { message = "An error occurred while loading school fees settings." });
            }
        }

        /// <summary>Legacy StudentFees/SetSchoolFees POST (AddOrUpdateFeePrices).</summary>
        [HttpPost("set-school-fees")]
        public async Task<IActionResult> SaveSetSchoolFees(
            [FromBody] AccountingSetSchoolFeesDto model,
            CancellationToken ct)
        {
            try
            {
                if (model == null)
                {
                    return BadRequest(new { message = "Fee model is required." });
                }

                var result = await _accountingService.AddOrUpdateFeePricesAsync(GetUserId(), model, ct);
                if (result.Status == 4)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving set school fees");
                return StatusCode(500, new { message = "An error occurred while saving school fees settings." });
            }
        }

        /// <summary>Legacy StudentFees/LoadStudentsForRegistarationFees GET (GetFreshiersList).</summary>
        [HttpGet("new-student-fees")]
        public async Task<IActionResult> GetNewStudentFees(CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.GetFreshiersListAsync(GetUserId(), ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading new student fees list");
                return StatusCode(500, new { message = "An error occurred while loading new student fees." });
            }
        }

        /// <summary>Legacy StudentFees/SubmitPaymentForSelectedFreshiers POST.</summary>
        [HttpPost("new-student-fees/pay")]
        public async Task<IActionResult> PaySelectedFreshiers(
            [FromBody] AccountingPayFreshiersRequestDto request,
            CancellationToken ct)
        {
            try
            {
                if (request?.StudentIds == null || request.StudentIds.Count == 0)
                {
                    return BadRequest(new { message = "At least one student must be selected." });
                }

                var result = await _accountingService.PayForSelectedFreshiersRegistrationAsync(
                    GetUserId(),
                    request.StudentIds,
                    ct);

                if (result is string message)
                {
                    return Ok(message);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error paying selected freshers");
                return StatusCode(500, new { message = "An error occurred while processing fresher payments." });
            }
        }

        /// <summary>Legacy SelectedStudentFreshPartPaymentInfo (GetSelectedStudentFreshPaymentHistoryById).</summary>
        [HttpGet("new-student-fees/{studentLevelPaymentId}/part-payment")]
        public async Task<IActionResult> GetFreshPartPaymentInfo(string studentLevelPaymentId, CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.GetSelectedStudentFreshPaymentHistoryByIdAsync(
                    studentLevelPaymentId,
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading fresher part payment info");
                return StatusCode(500, new { message = "An error occurred while loading part payment details." });
            }
        }

        /// <summary>Legacy SubmitFeesForaSingleStudent for freshers (part payment).</summary>
        [HttpPost("new-student-fees/part-payment")]
        public async Task<IActionResult> SubmitFreshPartPayment(
            [FromBody] AccountingFreshPartPaymentRequestDto request,
            CancellationToken ct)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.StudentLevelPaymentId))
                {
                    return BadRequest(new { message = "Student payment id is required." });
                }

                var result = await _accountingService.SingleStudentFreshPartPaymentAsync(
                    GetUserId(),
                    request,
                    ct);

                if (string.IsNullOrWhiteSpace(result.SchoolId))
                {
                    return Ok(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving fresher part payment");
                return StatusCode(500, new
                {
                    message = "An error occurred while saving part payment.",
                    detail = ex.GetBaseException().Message,
                });
            }
        }

        /// <summary>Legacy StudentFees/LoadStudentsForTermlyFees GET (GetReturningStudentsList).</summary>
        [HttpGet("returning-student-fees")]
        public async Task<IActionResult> GetReturningStudentFees(CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.GetReturningStudentsListAsync(GetUserId(), ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading returning student fees list");
                return StatusCode(500, new { message = "An error occurred while loading returning student fees." });
            }
        }

        /// <summary>Legacy StudentFees/SubmitPaymentForSelectedReturningStudents POST.</summary>
        [HttpPost("returning-student-fees/pay")]
        public async Task<IActionResult> PaySelectedReturningStudents(
            [FromBody] AccountingPayReturningStudentsRequestDto request,
            CancellationToken ct)
        {
            try
            {
                if (request?.StudentIds == null || request.StudentIds.Count == 0)
                {
                    return BadRequest(new { message = "At least one student must be selected." });
                }

                var result = await _accountingService.PayForSelectedReturningStudentsAsync(
                    GetUserId(),
                    request.StudentIds,
                    ct);

                if (result is string message)
                {
                    return Ok(message);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error paying selected returning students");
                return StatusCode(500, new { message = "An error occurred while processing returning student payments." });
            }
        }

        /// <summary>Legacy SelectedStudentPaymentInfo (GetSelectedReturningStudentPaymentHistoryById).</summary>
        [HttpGet("returning-student-fees/{studentLevelPaymentId}/part-payment")]
        public async Task<IActionResult> GetReturningPartPaymentInfo(string studentLevelPaymentId, CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.GetSelectedReturningStudentPaymentHistoryByIdAsync(
                    GetUserId(),
                    studentLevelPaymentId,
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading returning student part payment info");
                return StatusCode(500, new { message = "An error occurred while loading part payment details." });
            }
        }

        /// <summary>Legacy SubmitFeesForaSingleStudent (shared with freshers).</summary>
        [HttpPost("returning-student-fees/part-payment")]
        public async Task<IActionResult> SubmitReturningPartPayment(
            [FromBody] AccountingFreshPartPaymentRequestDto request,
            CancellationToken ct)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.StudentLevelPaymentId))
                {
                    return BadRequest(new { message = "Student payment id is required." });
                }

                var result = await _accountingService.SingleStudentFreshPartPaymentAsync(
                    GetUserId(),
                    request,
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving returning student part payment");
                return StatusCode(500, new
                {
                    message = "An error occurred while saving part payment.",
                    detail = ex.GetBaseException().Message,
                });
            }
        }

        /// <summary>Legacy StudentFees/ExecutiveReport GET (sessions + optional terms for first session).</summary>
        [HttpGet("executive-report/defaults")]
        public async Task<IActionResult> GetExecutiveReportDefaults(CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.GetExecutiveReportDefaultsAsync(GetUserId(), ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading executive report defaults");
                return StatusCode(500, new { message = "An error occurred while loading executive report defaults." });
            }
        }

        /// <summary>Legacy StudentFees/TermDropdownForSelectedSession (Value = term Id).</summary>
        [HttpGet("executive-report/terms")]
        public async Task<IActionResult> GetExecutiveReportTerms([FromQuery] string sessionId, CancellationToken ct)
        {
            try
            {
                var terms = await _accountingService.GetExecutiveReportTermsAsync(sessionId, ct);
                return Ok(terms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading executive report terms");
                return StatusCode(500, new { message = "An error occurred while loading terms." });
            }
        }

        /// <summary>Legacy StudentFees/TermlyReport (GetTermlyReportByTermId).</summary>
        [HttpGet("executive-report/termly")]
        public async Task<IActionResult> GetExecutiveReportTermly([FromQuery] string termId, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(termId) || termId == "-1")
                {
                    return BadRequest(new { message = "termId is required." });
                }

                var report = await _accountingService.GetTermlyReportByTermIdAsync(GetUserId(), termId, ct);
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading termly executive report");
                return StatusCode(500, new { message = "An error occurred while loading the termly report." });
            }
        }

        /// <summary>Legacy StudentFees/SearchStudents (GetStudentByParameter).</summary>
        [HttpGet("student-payment-history/search")]
        public async Task<IActionResult> SearchStudentsForPaymentHistory(
            [FromQuery] string userName,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return Ok(new List<StudentFieldExtraction4Accounting2ndDto>());
                }

                var students = await _accountingService.GetStudentByParameterAsync(GetUserId(), userName, ct);
                return Ok(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching students for payment history");
                return StatusCode(500, new { message = "An error occurred while searching students." });
            }
        }

        /// <summary>Legacy StudentFees/SelectedStudentPaymentHistory.</summary>
        [HttpGet("student-payment-history/{studentId}")]
        public async Task<IActionResult> GetSelectedStudentPaymentHistory(
            string studentId,
            [FromQuery] string? admsnNo,
            [FromQuery] string? studentName,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(studentId))
                {
                    return BadRequest(new { message = "studentId is required." });
                }

                var terms = await _accountingService.GetSelectedStudentPaymentHistoryByIdAsync(
                    GetUserId(),
                    studentId,
                    ct);
                return Ok(new AccountingStudentPaymentHistoryLoadDto
                {
                    AdmsnNo = admsnNo ?? string.Empty,
                    StudentName = studentName ?? string.Empty,
                    Terms = terms,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student payment history");
                return StatusCode(500, new { message = "An error occurred while loading payment history." });
            }
        }

        /// <summary>Legacy StudentFees/CreateOptionalFeesCategory POST.</summary>
        [HttpPost("optional-fees-categories")]
        public async Task<IActionResult> CreateOptionalFeesCategory(
            [FromBody] AccountingOptionalFeesCategoryDto model,
            CancellationToken ct)
        {
            try
            {
                // Use AccountingOptionalFeesCategoryDto (not ItemCategoryDto): ItemCategoryDto's
                // non-nullable School/SchoolId trigger [ApiController] 400 on { categoryName } bodies.
                var categoryName = model?.CategoryName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(categoryName))
                {
                    return BadRequest(new { message = "Category Name is required." });
                }

                if (categoryName.Length > 50)
                {
                    return BadRequest(new { message = "Category Name must be at most 50 characters." });
                }

                var result = await _accountingService.CreateOptionalFeesCategoryAsync(
                    GetUserId(),
                    categoryName,
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating optional fees category");
                return StatusCode(500, new { message = "An error occurred while creating the category." });
            }
        }

        /// <summary>Legacy EmployeeIncome/CreateIncomeCategory POST.</summary>
        [HttpPost("income-categories")]
        public async Task<IActionResult> CreateIncomeCategory(
            [FromBody] AccountingIncomeCategoryDto model,
            CancellationToken ct)
        {
            try
            {
                var categoryName = model?.CategoryName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(categoryName))
                {
                    return BadRequest(new { message = "Category Name is required." });
                }

                if (categoryName.Length > 50)
                {
                    return BadRequest(new { message = "Category Name must be at most 50 characters." });
                }

                var result = await _accountingService.CreateIncomeCategoryAsync(
                    GetUserId(),
                    categoryName,
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating income category");
                return StatusCode(500, new { message = "An error occurred while creating the income category." });
            }
        }

        /// <summary>Legacy EmployeeIncome/CreateExpenditureCategory POST.</summary>
        [HttpPost("expenditure-categories")]
        public async Task<IActionResult> CreateExpenditureCategory(
            [FromBody] AccountingExpenditureCategoryDto model,
            CancellationToken ct)
        {
            try
            {
                var categoryName = model?.CategoryName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(categoryName))
                {
                    return BadRequest(new { message = "Category Name is required." });
                }

                if (categoryName.Length > 50)
                {
                    return BadRequest(new { message = "Category Name must be at most 50 characters." });
                }

                var result = await _accountingService.CreateExpenditureCategoryAsync(
                    GetUserId(),
                    categoryName,
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating expenditure category");
                return StatusCode(500, new { message = "An error occurred while creating the expenditure category." });
            }
        }

        /// <summary>Legacy EmployeeIncome/RegisterIncome GET.</summary>
        [HttpGet("income/register-form")]
        public async Task<IActionResult> GetRegisterIncomeForm(CancellationToken ct)
        {
            try
            {
                var form = await _accountingService.GetRegisterIncomeFormAsync(GetUserId(), ct);
                return Ok(form);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading register income form");
                return StatusCode(500, new { message = "An error occurred while loading the income form." });
            }
        }

        /// <summary>Legacy EmployeeIncome/RegisterIncome POST (CreateIncome).</summary>
        [HttpPost("income")]
        public async Task<IActionResult> RegisterIncome(
            [FromBody] AccountingRegisterIncomeDto model,
            CancellationToken ct)
        {
            try
            {
                model ??= new AccountingRegisterIncomeDto();
                if (string.IsNullOrWhiteSpace(model.ItemCategoryId))
                {
                    return BadRequest(new { message = "Income Category is required." });
                }

                if (model.CapturedDate == default)
                {
                    return BadRequest(new { message = "Item' Date is required." });
                }

                var details = model.Details ?? string.Empty;
                if (details.Length > 150)
                {
                    return BadRequest(new { message = "Details must be at most 150 characters." });
                }

                var result = await _accountingService.CreateIncomeAsync(GetUserId(), model, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering income");
                return StatusCode(500, new { message = "An error occurred while saving the income item." });
            }
        }

        /// <summary>Legacy EmployeeIncome/RegisterExpenses GET.</summary>
        [HttpGet("expenditure/register-form")]
        public async Task<IActionResult> GetRegisterExpenditureForm(CancellationToken ct)
        {
            try
            {
                var form = await _accountingService.GetRegisterExpenditureFormAsync(GetUserId(), ct);
                return Ok(form);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading register expenditure form");
                return StatusCode(500, new { message = "An error occurred while loading the expenditure form." });
            }
        }

        /// <summary>Legacy EmployeeIncome/RegisterExpenses POST (CreateExpenditure).</summary>
        [HttpPost("expenditure")]
        public async Task<IActionResult> RegisterExpenditure(
            [FromBody] AccountingRegisterExpenditureDto model,
            CancellationToken ct)
        {
            try
            {
                model ??= new AccountingRegisterExpenditureDto();
                if (string.IsNullOrWhiteSpace(model.ItemCategoryId))
                {
                    return BadRequest(new { message = "Expenses Category is required." });
                }

                if (model.CapturedDate == default)
                {
                    return BadRequest(new { message = "Item' Date is required." });
                }

                var details = model.Details ?? string.Empty;
                if (details.Length > 150)
                {
                    return BadRequest(new { message = "Details must be at most 150 characters." });
                }

                var result = await _accountingService.CreateExpenditureAsync(GetUserId(), model, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering expenditure");
                return StatusCode(500, new { message = "An error occurred while saving the expenditure item." });
            }
        }

        /// <summary>Legacy EmployeeIncome/EmployeeList.</summary>
        [HttpGet("employees-income-structure")]
        public async Task<IActionResult> GetEmployeesIncomeStructure(CancellationToken ct)
        {
            try
            {
                var list = await _accountingService.GetAllEmployeesAsync(GetUserId(), ct);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading employees income structure list");
                return StatusCode(500, new { message = "An error occurred while loading employees." });
            }
        }

        /// <summary>Legacy SetIncomeStructure GET.</summary>
        [HttpGet("employees-income-structure/{employeeId}/set-form")]
        public async Task<IActionResult> GetSetIncomeStructureForm(string employeeId, CancellationToken ct)
        {
            try
            {
                var form = await _accountingService.GetSetIncomeStructureFormAsync(GetUserId(), employeeId, ct);
                if (form == null)
                {
                    return NotFound(new { message = "Employee not found." });
                }

                return Ok(form);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading set income structure form");
                return StatusCode(500, new { message = "An error occurred while loading the form." });
            }
        }

        /// <summary>Legacy ViewIncomeStructure GET.</summary>
        [HttpGet("employees-income-structure/{employeeId}")]
        public async Task<IActionResult> GetEmployeeIncomeStructure(string employeeId, CancellationToken ct)
        {
            try
            {
                var form = await _accountingService.GetEmployeeIncomeStructureAsync(GetUserId(), employeeId, ct);
                if (form == null)
                {
                    return NotFound(new { message = "Income structure not found." });
                }

                return Ok(form);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading employee income structure");
                return StatusCode(500, new { message = "An error occurred while loading the income structure." });
            }
        }

        /// <summary>Legacy SetIncomeStructure POST (CreateEmployeeStructure).</summary>
        [HttpPost("employees-income-structure")]
        public async Task<IActionResult> SetEmployeeIncomeStructure(
            [FromBody] AccountingEmployeeIncomeStructureDto model,
            CancellationToken ct)
        {
            try
            {
                model ??= new AccountingEmployeeIncomeStructureDto();
                var result = await _accountingService.CreateEmployeeStructureAsync(GetUserId(), model, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving employee income structure");
                return StatusCode(500, new { message = "An error occurred while saving the income structure." });
            }
        }

        /// <summary>Legacy DeleteIncomeStructure.</summary>
        [HttpDelete("employees-income-structure/{employeeId}")]
        public async Task<IActionResult> DeleteEmployeeIncomeStructure(string employeeId, CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.DeleteIncomeStructureAsync(GetUserId(), employeeId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting employee income structure");
                return StatusCode(500, new { message = "An error occurred while deleting the income structure." });
            }
        }

        /// <summary>Legacy EmployeeIncome/PaymentSlip.</summary>
        [HttpGet("monthly-payroll/slip")]
        public async Task<IActionResult> GetMonthlyPaymentSlip(
            [FromQuery] DateTime paymentDate,
            CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.GetMonthlyPaymentSlipAsync(GetUserId(), paymentDate, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading monthly payment slip");
                return StatusCode(500, new { message = "An error occurred while loading the payment slip." });
            }
        }

        /// <summary>Legacy SubmitMonthlyPayment (Json 1).</summary>
        [HttpPost("monthly-payroll")]
        public async Task<IActionResult> CreateMonthlyPayment(
            [FromBody] List<AccountingMonthlyPaymentSaveDto> monthlyPayment,
            CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.CreateMonthlyPaymentAsync(
                    GetUserId(),
                    monthlyPayment ?? new List<AccountingMonthlyPaymentSaveDto>(),
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating monthly payment");
                return StatusCode(500, new { message = "An error occurred while saving the monthly payment." });
            }
        }

        /// <summary>Legacy ConfirmRecentMonthlyPayment (level id → Json 0).</summary>
        [HttpPost("monthly-payroll/{levelId}/confirm")]
        public async Task<IActionResult> ConfirmMonthlyPayment(string levelId, CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.ConfirmMonthlyPaymentAsync(GetUserId(), levelId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming monthly payment");
                return StatusCode(500, new { message = "An error occurred while confirming the payment." });
            }
        }

        /// <summary>Legacy SubmitPaymentForAsingleEmployee (Json 0).</summary>
        [HttpPost("monthly-payroll/employee-payment")]
        public async Task<IActionResult> SubmitPaymentForASingleEmployee(
            [FromBody] AccountingMonthlyPaymentSaveDto model,
            CancellationToken ct)
        {
            try
            {
                model ??= new AccountingMonthlyPaymentSaveDto();
                var result = await _accountingService.SubmitPaymentForASingleEmployeeAsync(GetUserId(), model, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee monthly payment");
                return StatusCode(500, new { message = "An error occurred while updating the payment." });
            }
        }

        /// <summary>Legacy DeleteIncomePreview.</summary>
        [HttpDelete("monthly-payroll/preview")]
        public async Task<IActionResult> DeleteMonthlyPaymentPreview(
            [FromQuery] string monthId,
            CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.DeleteRecentlyGeneratedPaymentAsync(
                    GetUserId(),
                    monthId,
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting monthly payment preview");
                return StatusCode(500, new { message = "An error occurred while deleting the preview." });
            }
        }

        /// <summary>Legacy SubmitAnEmployeePaymentComments GET.</summary>
        [HttpGet("monthly-payroll/{id}/comments")]
        public async Task<IActionResult> GetMonthlyPaymentComment(string id, CancellationToken ct)
        {
            try
            {
                var comment = await _accountingService.GetEmployeeMonthlyPaymentCommentAsync(GetUserId(), id, ct);
                return Ok(new { comment });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading monthly payment comment");
                return StatusCode(500, new { message = "An error occurred while loading the comment." });
            }
        }

        /// <summary>Legacy SubmitAnEmployeePaymentComments POST.</summary>
        [HttpPut("monthly-payroll/{id}/comments")]
        public async Task<IActionResult> SaveMonthlyPaymentComment(
            string id,
            [FromBody] AccountingMonthlyPaymentCommentDto body,
            CancellationToken ct)
        {
            try
            {
                body ??= new AccountingMonthlyPaymentCommentDto();
                var result = await _accountingService.SaveEmployeeMonthlyPaymentCommentAsync(
                    GetUserId(),
                    id,
                    body.CommentInput ?? string.Empty,
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving monthly payment comment");
                return StatusCode(500, new { message = "An error occurred while saving the comment." });
            }
        }

        /// <summary>Legacy EmployeeIncome/SalaryPaymentReport.</summary>
        [HttpGet("salary-report/defaults")]
        public async Task<IActionResult> GetSalaryReportDefaults(CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.GetSalaryReportDefaultsAsync(GetUserId(), ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading salary report defaults");
                return StatusCode(500, new { message = "An error occurred while loading salary report filters." });
            }
        }

        /// <summary>Legacy MonthDropdownForSelectedYearSalary.</summary>
        [HttpGet("salary-report/months")]
        public async Task<IActionResult> GetSalaryReportMonths(CancellationToken ct)
        {
            try
            {
                var months = await _accountingService.GetSalaryReportMonthsAsync(GetUserId(), ct);
                return Ok(months);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading salary report months");
                return StatusCode(500, new { message = "An error occurred while loading months." });
            }
        }

        /// <summary>Legacy SalaryReportForSelectedPeriod.</summary>
        [HttpGet("salary-report")]
        public async Task<IActionResult> GetSalaryReportForSelectedPeriod(
            [FromQuery] short year,
            [FromQuery] byte startMonth,
            [FromQuery] byte endMonth,
            CancellationToken ct)
        {
            try
            {
                var report = await _accountingService.GetSalaryReportForSelectedPeriodAsync(
                    GetUserId(),
                    year,
                    startMonth,
                    endMonth,
                    ct);
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading salary report");
                return StatusCode(500, new { message = "An error occurred while loading the salary report." });
            }
        }

        /// <summary>Legacy EmployeeIncome/IncomeCapturingIndex.</summary>
        [HttpGet("income/capturing-index")]
        public async Task<IActionResult> GetIncomeCapturingIndex(CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.GetIncomeCapturingIndexAsync(GetUserId(), ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading income capturing index");
                return StatusCode(500, new { message = "An error occurred while loading income items." });
            }
        }

        /// <summary>Legacy IncomeReportForSelectedPeriod.</summary>
        [HttpGet("income/period-report")]
        public async Task<IActionResult> GetIncomePeriodReport(
            [FromQuery] short year,
            [FromQuery] byte startMonth,
            [FromQuery] byte endMonth,
            CancellationToken ct)
        {
            try
            {
                var list = await _accountingService.GetIncomeReportForSelectedPeriodAsync(
                    GetUserId(),
                    year,
                    startMonth,
                    endMonth,
                    ct);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading income period report");
                return StatusCode(500, new { message = "An error occurred while loading the income report." });
            }
        }

        /// <summary>Legacy EditIncomeItem GET.</summary>
        [HttpGet("income/{id}")]
        public async Task<IActionResult> GetIncomeItem(string id, CancellationToken ct)
        {
            try
            {
                var form = await _accountingService.GetIncomeItemForEditAsync(GetUserId(), id, ct);
                if (form == null)
                {
                    return NotFound(new { message = "Income item not found." });
                }

                return Ok(form);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading income item {Id}", id);
                return StatusCode(500, new { message = "An error occurred while loading the income item." });
            }
        }

        /// <summary>Legacy EditIncomeItem POST.</summary>
        [HttpPut("income/{id}")]
        public async Task<IActionResult> EditIncomeItem(
            string id,
            [FromBody] AccountingIncomeItemDto model,
            CancellationToken ct)
        {
            try
            {
                model ??= new AccountingIncomeItemDto();
                model.Id = id;
                var details = model.Details ?? string.Empty;
                if (details.Length > 150)
                {
                    return BadRequest(new { message = "Failed! The detail you have entered is out of range." });
                }

                if (model.Cost == 0)
                {
                    return BadRequest(new { message = "Failed! You need to enter the amount to continue." });
                }

                var result = await _accountingService.UpdateIncomeItemAsync(GetUserId(), model, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating income item {Id}", id);
                return StatusCode(500, new { message = "An error occurred while updating the income item." });
            }
        }

        /// <summary>Legacy DeleteIncomeItem.</summary>
        [HttpDelete("income/{id}")]
        public async Task<IActionResult> DeleteIncomeItem(string id, CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.DeleteIncomeItemAsync(GetUserId(), id, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting income item {Id}", id);
                return StatusCode(500, new { message = "An error occurred while deleting the income item." });
            }
        }

        /// <summary>Legacy ConfirmIncomeItems.</summary>
        [HttpPost("income/weeks/{weekId}/confirm")]
        public async Task<IActionResult> ConfirmIncomeItems(string weekId, CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.ConfirmRecentIncomeListAsync(GetUserId(), weekId, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming income week {WeekId}", weekId);
                return StatusCode(500, new { message = "An error occurred while confirming income items." });
            }
        }

        /// <summary>Legacy EmployeeIncome/ExpenditureCapturingIndex.</summary>
        [HttpGet("expenditure/capturing-index")]
        public async Task<IActionResult> GetExpenditureCapturingIndex(CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.GetExpenditureCapturingIndexAsync(GetUserId(), ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading expenditure capturing index");
                return StatusCode(500, new { message = "An error occurred while loading expenditure items." });
            }
        }

        /// <summary>Legacy ExpenditureReportForSelectedPeriod.</summary>
        [HttpGet("expenditure/period-report")]
        public async Task<IActionResult> GetExpenditurePeriodReport(
            [FromQuery] short year,
            [FromQuery] byte startMonth,
            [FromQuery] byte endMonth,
            CancellationToken ct)
        {
            try
            {
                var list = await _accountingService.GetExpenditureReportForSelectedPeriodAsync(
                    GetUserId(),
                    year,
                    startMonth,
                    endMonth,
                    ct);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading expenditure period report");
                return StatusCode(500, new { message = "An error occurred while loading the expenditure report." });
            }
        }

        /// <summary>Legacy EditExpenseItem GET.</summary>
        [HttpGet("expenditure/{id}")]
        public async Task<IActionResult> GetExpenseItem(string id, CancellationToken ct)
        {
            try
            {
                var form = await _accountingService.GetExpenseItemForEditAsync(GetUserId(), id, ct);
                if (form == null)
                {
                    return NotFound(new { message = "Expenditure item not found." });
                }

                return Ok(form);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading expenditure item {Id}", id);
                return StatusCode(500, new { message = "An error occurred while loading the expenditure item." });
            }
        }

        /// <summary>Legacy EditExpenseItem POST.</summary>
        [HttpPut("expenditure/{id}")]
        public async Task<IActionResult> EditExpenseItem(
            string id,
            [FromBody] AccountingIncomeItemDto model,
            CancellationToken ct)
        {
            try
            {
                model ??= new AccountingIncomeItemDto();
                model.Id = id;
                var details = model.Details ?? string.Empty;
                if (details.Length > 150)
                {
                    return BadRequest(new { message = "Failed! The detail you have entered is out of range." });
                }

                if (model.Cost == 0)
                {
                    return BadRequest(new { message = "Failed! You need to enter the amount to continue." });
                }

                var result = await _accountingService.UpdateExpenseItemAsync(GetUserId(), model, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating expenditure item {Id}", id);
                return StatusCode(500, new { message = "An error occurred while updating the expenditure item." });
            }
        }

        /// <summary>Legacy DeleteExpenseItem.</summary>
        [HttpDelete("expenditure/{id}")]
        public async Task<IActionResult> DeleteExpenseItem(string id, CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.DeleteExpenseItemAsync(GetUserId(), id, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting expenditure item {Id}", id);
                return StatusCode(500, new { message = "An error occurred while deleting the expenditure item." });
            }
        }

        /// <summary>Legacy ConfirmExpensesItems.</summary>
        [HttpPost("expenditure/weeks/{weekId}/confirm")]
        public async Task<IActionResult> ConfirmExpenseItems(string weekId, CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.ConfirmRecentExpenditureListAsync(
                    GetUserId(),
                    weekId,
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming expenditure week {WeekId}", weekId);
                return StatusCode(500, new { message = "An error occurred while confirming expenditure items." });
            }
        }

        /// <summary>Legacy EmployeeIncome/IncomeCategoryIndex.</summary>
        [HttpGet("income-categories/index")]
        public async Task<IActionResult> GetIncomeCategoryIndex(CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.GetIncomeCategoryIndexAsync(GetUserId(), ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading income category index");
                return StatusCode(500, new { message = "An error occurred while loading income categories." });
            }
        }

        /// <summary>Legacy AccountingService.GetIncomeCategoryList.</summary>
        [HttpGet("income-categories")]
        public async Task<IActionResult> GetIncomeCategories(CancellationToken ct)
        {
            try
            {
                var list = await _accountingService.GetIncomeCategoryListAsync(GetUserId(), ct);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading income categories");
                return StatusCode(500, new { message = "An error occurred while loading income categories." });
            }
        }

        /// <summary>Legacy EmployeeIncome/EditIncomeCategory.</summary>
        [HttpPut("income-categories/{id}")]
        public async Task<IActionResult> EditIncomeCategory(
            string id,
            [FromBody] AccountingIncomeCategoryDto model,
            CancellationToken ct)
        {
            try
            {
                model ??= new AccountingIncomeCategoryDto();
                model.Id = id;
                var name = model.CategoryName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest(new { message = "Category Name is required." });
                }

                if (name.Length > 50)
                {
                    return BadRequest(new { message = "Category length can not be more than 50 letters long." });
                }

                var result = await _accountingService.UpdateIncomeCategoryAsync(GetUserId(), model, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating income category");
                return StatusCode(500, new { message = "An error occurred while updating the income category." });
            }
        }

        /// <summary>Legacy EmployeeIncome/DeleteIncomeCategory.</summary>
        [HttpDelete("income-categories/{id}")]
        public async Task<IActionResult> DeleteIncomeCategory(string id, CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.DeleteIncomeCategoryByIdAsync(GetUserId(), id, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting income category");
                return StatusCode(500, new { message = "An error occurred while deleting the income category." });
            }
        }

        /// <summary>Legacy MonthDropdownForSelectedYearIncome.</summary>
        [HttpGet("income-categories/months")]
        public async Task<IActionResult> GetIncomeCategoryMonths(CancellationToken ct)
        {
            try
            {
                var months = await _accountingService.GetMonthDropdownForIncomeAsync(GetUserId(), ct);
                return Ok(months);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading income months dropdown");
                return StatusCode(500, new { message = "An error occurred while loading months." });
            }
        }

        /// <summary>Legacy IncomeCategoryReportForSelectedPeriod.</summary>
        [HttpGet("income-categories/period-report")]
        public async Task<IActionResult> GetIncomeCategoryPeriodReport(
            [FromQuery] short year,
            [FromQuery] byte startMonth,
            [FromQuery] byte endMonth,
            [FromQuery] string categoryId,
            CancellationToken ct)
        {
            try
            {
                var list = await _accountingService.GetCategoryIncomeReportAsync(
                    GetUserId(),
                    year,
                    startMonth,
                    endMonth,
                    categoryId,
                    ct);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading income category period report");
                return StatusCode(500, new { message = "An error occurred while loading the period report." });
            }
        }

        /// <summary>Legacy IncomeCategoryReportFilterForSelectedPeriod.</summary>
        [HttpGet("income-categories/analysis")]
        public async Task<IActionResult> GetIncomeCategoryAnalysis(
            [FromQuery] short year,
            [FromQuery] byte startMonth,
            [FromQuery] byte endMonth,
            [FromQuery] int sortType,
            [FromQuery] int numberToPick,
            [FromQuery] string? categoryId = null,
            CancellationToken ct = default)
        {
            try
            {
                if (numberToPick <= 0)
                {
                    return BadRequest(new { message = "Number of record to display is required in order to continue." });
                }

                var list = await _accountingService.GetIncomeCategoryAnalysisForSelectedPeriodAsync(
                    GetUserId(),
                    year,
                    startMonth,
                    endMonth,
                    categoryId,
                    sortType,
                    numberToPick,
                    ct);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading income category analysis");
                return StatusCode(500, new { message = "An error occurred while loading the analysis report." });
            }
        }

        /// <summary>Legacy EmployeeIncome/ExpenditureCategoryIndex.</summary>
        [HttpGet("expenditure-categories/index")]
        public async Task<IActionResult> GetExpenditureCategoryIndex(CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.GetExpenditureCategoryIndexAsync(GetUserId(), ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading expenditure category index");
                return StatusCode(500, new { message = "An error occurred while loading expenditure categories." });
            }
        }

        /// <summary>Legacy AccountingService.GetExpensesCategoryList.</summary>
        [HttpGet("expenditure-categories")]
        public async Task<IActionResult> GetExpenditureCategories(CancellationToken ct)
        {
            try
            {
                var list = await _accountingService.GetExpenditureCategoryListAsync(GetUserId(), ct);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading expenditure categories");
                return StatusCode(500, new { message = "An error occurred while loading expenditure categories." });
            }
        }

        /// <summary>Legacy EmployeeIncome/EditExpenditureCategory.</summary>
        [HttpPut("expenditure-categories/{id}")]
        public async Task<IActionResult> EditExpenditureCategory(
            string id,
            [FromBody] AccountingExpenditureCategoryDto model,
            CancellationToken ct)
        {
            try
            {
                model ??= new AccountingExpenditureCategoryDto();
                model.Id = id;
                var name = model.CategoryName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest(new { message = "Category Name is required." });
                }

                if (name.Length > 50)
                {
                    return BadRequest(new { message = "Category length can not be more than 50 letters long." });
                }

                var result = await _accountingService.UpdateExpenditureCategoryAsync(GetUserId(), model, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating expenditure category");
                return StatusCode(500, new { message = "An error occurred while updating the expenditure category." });
            }
        }

        /// <summary>Legacy EmployeeIncome/DeleteExpenditureCategory.</summary>
        [HttpDelete("expenditure-categories/{id}")]
        public async Task<IActionResult> DeleteExpenditureCategory(string id, CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.DeleteExpenditureCategoryByIdAsync(GetUserId(), id, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting expenditure category");
                return StatusCode(500, new { message = "An error occurred while deleting the expenditure category." });
            }
        }

        /// <summary>Legacy MonthDropdownForSelectedYearExpenditure.</summary>
        [HttpGet("expenditure-categories/months")]
        public async Task<IActionResult> GetExpenditureCategoryMonths(CancellationToken ct)
        {
            try
            {
                var months = await _accountingService.GetMonthDropdownForExpenditureAsync(GetUserId(), ct);
                return Ok(months);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading expenditure months dropdown");
                return StatusCode(500, new { message = "An error occurred while loading months." });
            }
        }

        /// <summary>Legacy ExpenditureCategoryReportForSelectedPeriod.</summary>
        [HttpGet("expenditure-categories/period-report")]
        public async Task<IActionResult> GetExpenditureCategoryPeriodReport(
            [FromQuery] short year,
            [FromQuery] byte startMonth,
            [FromQuery] byte endMonth,
            [FromQuery] string categoryId,
            CancellationToken ct)
        {
            try
            {
                var list = await _accountingService.GetCategoryExpenseReportAsync(
                    GetUserId(),
                    year,
                    startMonth,
                    endMonth,
                    categoryId,
                    ct);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading expenditure category period report");
                return StatusCode(500, new { message = "An error occurred while loading the period report." });
            }
        }

        /// <summary>Legacy ExpenditureCategoryReportFilterForSelectedPeriod.</summary>
        [HttpGet("expenditure-categories/analysis")]
        public async Task<IActionResult> GetExpenditureCategoryAnalysis(
            [FromQuery] short year,
            [FromQuery] byte startMonth,
            [FromQuery] byte endMonth,
            [FromQuery] int sortType,
            [FromQuery] int numberToPick,
            [FromQuery] string? categoryId = null,
            CancellationToken ct = default)
        {
            try
            {
                if (numberToPick <= 0)
                {
                    return BadRequest(new { message = "Number of record to display is required in order to continue." });
                }

                var list = await _accountingService.GetExpenditureCategoryAnalysisForSelectedPeriodAsync(
                    GetUserId(),
                    year,
                    startMonth,
                    endMonth,
                    categoryId,
                    sortType,
                    numberToPick,
                    ct);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading expenditure category analysis");
                return StatusCode(500, new { message = "An error occurred while loading the analysis report." });
            }
        }

        /// <summary>Legacy StudentFees/OptionalFeesCategoryIndex (GetOptionalFeesCategoryList).</summary>
        [HttpGet("optional-fees-categories")]
        public async Task<IActionResult> GetOptionalFeesCategories(CancellationToken ct)
        {
            try
            {
                var list = await _accountingService.GetOptionalFeesCategoryListAsync(GetUserId(), ct);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading optional fees categories");
                return StatusCode(500, new { message = "An error occurred while loading categories." });
            }
        }

        /// <summary>Legacy StudentFees/EditOptionalFeeCategory.</summary>
        [HttpPut("optional-fees-categories/{id}")]
        public async Task<IActionResult> EditOptionalFeesCategory(
            string id,
            [FromBody] AccountingOptionalFeesCategoryDto model,
            CancellationToken ct)
        {
            try
            {
                model ??= new AccountingOptionalFeesCategoryDto();
                model.Id = id;
                var name = model.CategoryName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest(new { message = "Category Name is required." });
                }

                if (name.Length > 50)
                {
                    return BadRequest(new { message = "Category length can not be more than 50 letters long." });
                }

                var result = await _accountingService.UpdateOptionalFeeCategoryAsync(GetUserId(), model, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating optional fees category");
                return StatusCode(500, new { message = "An error occurred while updating the category." });
            }
        }

        /// <summary>Legacy StudentFees/DeleteOptionalFeeCategory.</summary>
        [HttpDelete("optional-fees-categories/{id}")]
        public async Task<IActionResult> DeleteOptionalFeesCategory(string id, CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.DeleteOptionalFeeCategoryByIdAsync(GetUserId(), id, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting optional fees category");
                return StatusCode(500, new { message = "An error occurred while deleting the category." });
            }
        }

        /// <summary>Legacy StudentFees/RegisterOptionalFees POST (Create Optional Fees Item).</summary>
        [HttpPost("optional-fees-items")]
        public async Task<IActionResult> CreateOptionalFeesItem(
            [FromBody] AccountingOptionalFeesItemCreateDto model,
            CancellationToken ct)
        {
            try
            {
                model ??= new AccountingOptionalFeesItemCreateDto();
                var itemName = model.ItemName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(model.ItemCategoryId))
                {
                    return BadRequest(new { message = "OPtional Fee Category is required." });
                }

                if (string.IsNullOrWhiteSpace(itemName))
                {
                    return BadRequest(new { message = "Item Name is required." });
                }

                if (itemName.Trim().Length > 30)
                {
                    return BadRequest(new { message = "Item Name must be at most 30 characters." });
                }

                var result = await _accountingService.CreateOptionalFeeItemAsync(GetUserId(), model, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating optional fees item");
                return StatusCode(500, new { message = "An error occurred while creating the optional fees item." });
            }
        }

        /// <summary>Legacy StudentFees/OptionalFeesIndex + CategoryItemList.</summary>
        [HttpGet("optional-fees-items")]
        public async Task<IActionResult> GetOptionalFeesItems(
            [FromQuery] string? categoryId,
            CancellationToken ct)
        {
            try
            {
                var list = await _accountingService.GetOptionalFeeItemListAsync(
                    GetUserId(),
                    categoryId,
                    ct);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading optional fees items");
                return StatusCode(500, new { message = "An error occurred while loading optional fees items." });
            }
        }

        /// <summary>Legacy StudentFees/EditOptionalFeeItem GET.</summary>
        [HttpGet("optional-fees-items/{id}")]
        public async Task<IActionResult> GetOptionalFeesItem(string id, CancellationToken ct)
        {
            try
            {
                var item = await _accountingService.GetOptionalFeeItemByIdAsync(GetUserId(), id, ct);
                if (item == null)
                {
                    return NotFound(new { message = "Optional fee item not found." });
                }

                return Ok(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading optional fees item");
                return StatusCode(500, new { message = "An error occurred while loading the optional fees item." });
            }
        }

        /// <summary>Legacy StudentFees/EditOptionalFeeItem POST.</summary>
        [HttpPut("optional-fees-items/{id}")]
        public async Task<IActionResult> EditOptionalFeesItem(
            string id,
            [FromBody] AccountingOptionalFeesItemUpdateDto model,
            CancellationToken ct)
        {
            try
            {
                model ??= new AccountingOptionalFeesItemUpdateDto();
                model.Id = id;
                var name = model.ItemName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest(new { message = "Value for item name needs to be provided." });
                }

                if (name.Trim().Length > 30)
                {
                    return BadRequest(new { message = "Item Name must be at most 30 characters." });
                }

                if (model.Cost == 0)
                {
                    return BadRequest(new { message = "Value for item cost needs to be provided." });
                }

                var result = await _accountingService.UpdateOptionalFeeItemAsync(GetUserId(), model, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating optional fees item");
                return StatusCode(500, new { message = "An error occurred while updating the optional fees item." });
            }
        }

        /// <summary>Legacy StudentFees/DeleteOptionalFeeItem.</summary>
        [HttpDelete("optional-fees-items/{id}")]
        public async Task<IActionResult> DeleteOptionalFeesItem(string id, CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.DeleteOptionalFeeItemByIdAsync(GetUserId(), id, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting optional fees item");
                return StatusCode(500, new { message = "An error occurred while deleting the optional fees item." });
            }
        }

        /// <summary>Legacy StudentFees/StudentOptionalChargesAssignmentIndex GET.</summary>
        [HttpGet("optional-charges-assignment/form")]
        public async Task<IActionResult> GetOptionalChargesAssignmentForm(CancellationToken ct)
        {
            try
            {
                var form = await _accountingService.GetOptionalChargesAssignmentFormAsync(GetUserId(), ct);
                return Ok(form);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading optional charges assignment form");
                return StatusCode(500, new { message = "An error occurred while loading the assignment form." });
            }
        }

        /// <summary>Legacy StudentFees/RenderArmsDropdown.</summary>
        [HttpGet("optional-charges-assignment/arms")]
        public async Task<IActionResult> GetOptionalChargesAssignmentArms(
            [FromQuery] string classId,
            CancellationToken ct)
        {
            try
            {
                var arms = await _accountingService.GetOptionalChargesAssignmentArmsAsync(
                    GetUserId(),
                    classId ?? string.Empty,
                    ct);
                return Ok(arms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading arms for optional charges assignment");
                return StatusCode(500, new { message = "An error occurred while loading arms." });
            }
        }

        /// <summary>Legacy RenderStudentInTheSelectedArm / RenderCategoryStudentsInTheSelectedArm.</summary>
        [HttpGet("optional-charges-assignment/students")]
        public async Task<IActionResult> GetOptionalChargesAssignmentStudents(
            [FromQuery] string armId,
            [FromQuery] string categoryId,
            [FromQuery] byte catStatus = 1,
            CancellationToken ct = default)
        {
            try
            {
                var result = await _accountingService.GetOptionalChargesAssignmentStudentsAsync(
                    GetUserId(),
                    armId ?? string.Empty,
                    categoryId ?? string.Empty,
                    catStatus,
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading students for optional charges assignment");
                return StatusCode(500, new { message = "An error occurred while loading students." });
            }
        }

        /// <summary>Legacy ArmStudentsOrStudentsInCategoryOptionalFeeAssignment.</summary>
        [HttpPost("optional-charges-assignment/assign")]
        public async Task<IActionResult> AssignOptionalCharges(
            [FromBody] AccountingOptionalChargesAssignmentRequestDto model,
            CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.AssignOptionalChargesAsync(GetUserId(), model, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning optional charges");
                return StatusCode(500, new { message = "An error occurred while assigning optional charges." });
            }
        }

        /// <summary>Legacy ArmStudentsOrStudentsInCategoryOptionalFeeUnAssignment.</summary>
        [HttpPost("optional-charges-assignment/unassign")]
        public async Task<IActionResult> UnassignOptionalCharges(
            [FromBody] AccountingOptionalChargesAssignmentRequestDto model,
            CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.UnassignOptionalChargesAsync(GetUserId(), model, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unassigning optional charges");
                return StatusCode(500, new { message = "An error occurred while unassigning optional charges." });
            }
        }

        /// <summary>Legacy StudentFees/StudentOptionalPaymentIndex ResultType=0 form.</summary>
        [HttpGet("optional-payment-defaulters/form")]
        public async Task<IActionResult> GetOptionalPaymentDefaultersForm(CancellationToken ct)
        {
            try
            {
                var form = await _accountingService.GetOptionalPaymentDefaultersFormAsync(GetUserId(), ct);
                return Ok(form);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading optional payment defaulters form");
                return StatusCode(500, new { message = "An error occurred while loading the defaulters form." });
            }
        }

        /// <summary>Legacy TermEnumTypeDropdownForSelectedSession (TermStatus values).</summary>
        [HttpGet("optional-payment-defaulters/terms")]
        public async Task<IActionResult> GetOptionalPaymentDefaultersTerms(
            [FromQuery] string sessionId,
            CancellationToken ct)
        {
            try
            {
                var terms = await _accountingService.GetOptionalPaymentDefaultersTermsAsync(
                    GetUserId(),
                    sessionId ?? string.Empty,
                    ct);
                return Ok(terms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading terms for optional payment defaulters");
                return StatusCode(500, new { message = "An error occurred while loading terms." });
            }
        }

        /// <summary>Legacy PerTermPaymentDefaulters.</summary>
        [HttpGet("optional-payment-defaulters/termly")]
        public async Task<IActionResult> GetTermlyOptionalPaymentDefaulters(
            [FromQuery] string sessionId,
            [FromQuery] TermStatus termStatus,
            CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.GetTermlyOptionalPaymentDefaultersAsync(
                    GetUserId(),
                    sessionId ?? string.Empty,
                    termStatus,
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading termly optional payment defaulters");
                return StatusCode(500, new { message = "An error occurred while loading payment defaulters." });
            }
        }

        /// <summary>Legacy PerYearPaymentDefaulters.</summary>
        [HttpGet("optional-payment-defaulters/yearly")]
        public async Task<IActionResult> GetYearlyOptionalPaymentDefaulters(
            [FromQuery] string sessionId,
            CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.GetYearlyOptionalPaymentDefaultersAsync(
                    GetUserId(),
                    sessionId ?? string.Empty,
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading yearly optional payment defaulters");
                return StatusCode(500, new { message = "An error occurred while loading payment defaulters." });
            }
        }

        /// <summary>Legacy PerTermPaymentReport.</summary>
        [HttpGet("optional-payment-report/termly")]
        public async Task<IActionResult> GetTermlyOptionalPaymentReport(
            [FromQuery] string sessionId,
            [FromQuery] TermStatus termStatus,
            CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.GetTermlyOptionalPaymentReportAsync(
                    GetUserId(),
                    sessionId ?? string.Empty,
                    termStatus,
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading termly optional payment report");
                return StatusCode(500, new { message = "An error occurred while loading the payment report." });
            }
        }

        /// <summary>Legacy PerYearPaymentReport.</summary>
        [HttpGet("optional-payment-report/yearly")]
        public async Task<IActionResult> GetYearlyOptionalPaymentReport(
            [FromQuery] string sessionId,
            CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.GetYearlyOptionalPaymentReportAsync(
                    GetUserId(),
                    sessionId ?? string.Empty,
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading yearly optional payment report");
                return StatusCode(500, new { message = "An error occurred while loading the payment report." });
            }
        }

        /// <summary>Legacy StudentOptionalPaymentIndex / ResultTypeMenu form.</summary>
        [HttpGet("optional-payment-home/form")]
        public async Task<IActionResult> GetOptionalPaymentHomeForm(CancellationToken ct)
        {
            try
            {
                var form = await _accountingService.GetOptionalPaymentHomeFormAsync(GetUserId(), ct);
                return Ok(form);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading optional payment home form");
                return StatusCode(500, new { message = "An error occurred while loading the payment home form." });
            }
        }

        /// <summary>Legacy PerArmPaymentProcessing / PerClass / All.</summary>
        [HttpGet("optional-payment-home/processing")]
        public async Task<IActionResult> GetOptionalPaymentProcessing(
            [FromQuery] string mode,
            [FromQuery] string sessionId,
            [FromQuery] TermStatus termStatus,
            [FromQuery] string? sessionText = null,
            [FromQuery] byte? currentClass = null,
            [FromQuery] byte? armName = null,
            CancellationToken ct = default)
        {
            try
            {
                var userId = GetUserId();
                var text = sessionText ?? string.Empty;
                AccountingOptionalPaymentProcessingDto result = mode?.ToLowerInvariant() switch
                {
                    "class" => await _accountingService.LoadOptionalPaymentProcessingByClassAsync(
                        userId,
                        sessionId ?? string.Empty,
                        termStatus,
                        (CurrentClass)(currentClass ?? 0),
                        text,
                        ct),
                    "all" => await _accountingService.LoadOptionalPaymentProcessingAllAsync(
                        userId,
                        sessionId ?? string.Empty,
                        termStatus,
                        text,
                        ct),
                    _ => await _accountingService.LoadOptionalPaymentProcessingByArmAsync(
                        userId,
                        sessionId ?? string.Empty,
                        termStatus,
                        (CurrentClass)(currentClass ?? 0),
                        (ArmName)(armName ?? 0),
                        text,
                        ct),
                };
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading optional payment processing");
                return StatusCode(500, new { message = "An error occurred while loading payment processing." });
            }
        }

        /// <summary>Legacy SelectedStudentOptionalPaymentHistory.</summary>
        [HttpGet("optional-payment-report/student-history/{studentId}")]
        public async Task<IActionResult> GetStudentOptionalPaymentHistory(
            string studentId,
            CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.GetStudentOptionalPaymentHistoryAsync(
                    GetUserId(),
                    studentId ?? string.Empty,
                    ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student optional payment history");
                return StatusCode(500, new { message = "An error occurred while loading optional payment history." });
            }
        }

        /// <summary>Legacy PerRangePaymentReport / OptionalPaymentCategoryItemReportForSelectedPeriod.</summary>
        [HttpGet("optional-payment-report/range")]
        public async Task<IActionResult> GetOptionalPaymentRangeReport(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] string? categoryId = null,
            CancellationToken ct = default)
        {
            try
            {
                AccountingOptionalRangeReportDto result;
                if (!string.IsNullOrWhiteSpace(categoryId) && categoryId != "-1" && categoryId != "0")
                {
                    result = await _accountingService.GetOptionalPaymentCategoryRangeReportAsync(
                        GetUserId(),
                        startDate,
                        endDate,
                        categoryId,
                        ct);
                }
                else
                {
                    result = await _accountingService.GetOptionalPaymentRangeReportAsync(
                        GetUserId(),
                        startDate,
                        endDate,
                        ct);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading optional payment range report");
                return StatusCode(500, new { message = "An error occurred while loading the range report." });
            }
        }
    }
}
