using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Repo;
using NAITSSMS.Infrastructure.Unitofwork;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.Service
{
    /// <summary>
    /// Ports legacy AccountingService / RegistrationService fee-dashboard algorithms
    /// (GetSummaryForAccountingDashboard, GetTermlyReportByTermId aggregates, GetIncomeStatement).
    /// </summary>
    public class AccountingService : IAccountingService
    {
        private readonly IUnitOfWork _uow;
        private readonly ISchoolScopeResolver _schoolScopeResolver;

        public AccountingService(IUnitOfWork uow, ISchoolScopeResolver schoolScopeResolver)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _schoolScopeResolver = schoolScopeResolver ?? throw new ArgumentNullException(nameof(schoolScopeResolver));
        }

        public async Task<AccountingDashboardSummaryDto> GetDashboardSummaryAsync(
            string userId,
            string? termId = null,
            CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var statusRepo = _uow.Repository<TxYearlyAndTermlyStatus>();

            string? resolvedTermId = termId;
            if (string.IsNullOrWhiteSpace(resolvedTermId))
            {
                var current = await statusRepo.FindAsync(
                    x => x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                         && x.Session!.SchoolInfoId == schoolId,
                    ct,
                    x => x.Session!);
                resolvedTermId = current?.Id;
            }

            if (string.IsNullOrWhiteSpace(resolvedTermId))
            {
                return new AccountingDashboardSummaryDto { HasData = false };
            }

            var term = await statusRepo.FindAsync(x => x.Id == resolvedTermId, ct, x => x.Session!);
            var summary = await BuildTermlyFeeSummaryAsync(schoolId, resolvedTermId, ct);
            summary.HasData = true;
            summary.CurrentTermId = resolvedTermId;
            summary.CurrentSessionId = term?.SessionId;
            return summary;
        }

        public async Task<AccountingIncomeStatementDefaultsDto> GetIncomeStatementDefaultsAsync(
            string userId,
            CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var dropdown = await GetYearAndMonthDropdownAsync(schoolId, ct);
            var (year, month) = await GetDateValuesAsync(schoolId, ct);

            // Prefer a year/month that exists in the dropdowns when date defaults are empty.
            if (year == 0)
            {
                year = dropdown.Years
                    .Select(y => short.TryParse(y.Value, out var v) ? v : (short)0)
                    .Where(v => v > 0)
                    .DefaultIfEmpty((short)0)
                    .Max();
            }

            if (month == 0)
            {
                month = (byte)dropdown.Months
                    .Select(m => byte.TryParse(m.Value, out var v) ? v : (byte)0)
                    .Where(v => v > 0)
                    .DefaultIfEmpty((byte)DateTime.UtcNow.Month)
                    .First();
            }

            var result = new AccountingIncomeStatementDefaultsDto
            {
                Year = year,
                Month = month,
                Years = dropdown.Years,
                Months = dropdown.Months,
            };

            foreach (var item in result.Years)
            {
                item.Selected = item.Value == year.ToString();
            }

            foreach (var item in result.Months)
            {
                item.Selected = item.Value == month.ToString();
            }

            if (year > 0 && month is >= 1 and <= 12)
            {
                try
                {
                    result.Statement = await BuildIncomeStatementForSchoolAsync(
                        schoolId,
                        year,
                        month,
                        month,
                        ct);
                }
                catch
                {
                    // Dropdowns still useful even if the first statement query fails.
                    result.Statement = null;
                }
            }

            return result;
        }

        public async Task<AccountingIncomeStatementDto> GetIncomeStatementAsync(
            string userId,
            short year,
            byte startMonth,
            byte endMonth,
            CancellationToken ct = default)
        {
            if (year <= 0 || startMonth is < 1 or > 12 || endMonth is < 1 or > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(year), "Year and months must be valid.");
            }

            // Legacy does not reorder start/end months.
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            return await BuildIncomeStatementForSchoolAsync(schoolId, year, startMonth, endMonth, ct);
        }

        private async Task<AccountingIncomeStatementDto> BuildIncomeStatementForSchoolAsync(
            string schoolId,
            short year,
            byte startMonth,
            byte endMonth,
            CancellationToken ct)
        {
            // Legacy GetIncomeStatement — do NOT swap start/end; Month filters are inclusive as given.
            // Expenditure / other income: CapturedDate.Year/Month (not a UTC date-range).
            var expenses = await _uow.Repository<XyExpenses>().GetAllAsync(
                Filter: d => d.Week != null
                             && d.Week.SchoolId == schoolId
                             && d.CapturedDate.Year == year
                             && d.CapturedDate.Month >= startMonth
                             && d.CapturedDate.Month <= endMonth,
                IncludeProperties: "Week",
                cancellationToken: ct);
            var otherExpenses = expenses.Sum(c => c.Cost);

            var salaryPayment = await SumPaidSalaryForPeriodAsync(schoolId, year, startMonth, endMonth, ct);

            var incomes = await _uow.Repository<XyIncome>().GetAllAsync(
                Filter: d => d.Week != null
                             && d.Week.SchoolId == schoolId
                             && d.CapturedDate.Year == year
                             && d.CapturedDate.Month >= startMonth
                             && d.CapturedDate.Month <= endMonth,
                IncludeProperties: "Week",
                cancellationToken: ct);
            var otherIncome = incomes.Sum(c => c.Cost);

            // Legacy uses calendar dates at midnight on first/last day (Unspecified in SQL Server).
            // Postgres timestamptz requires UTC Kind; keep the same calendar day bounds.
            var startDate = new DateTime(year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = new DateTime(
                year,
                endMonth,
                DateTime.DaysInMonth(year, endMonth),
                0,
                0,
                0,
                DateTimeKind.Utc);

            var feePayments = await _uow.Repository<StudentTyTermlyFeePayment>().GetAllAsync(
                Filter: c => c.StudentAcademicProgressHistory != null
                             && c.StudentAcademicProgressHistory.StudentDetails != null
                             && c.StudentAcademicProgressHistory.StudentDetails.SchoolInfoId == schoolId
                             && c.CreatedDate >= startDate
                             && c.CreatedDate <= endDate,
                IncludeProperties: "StudentAcademicProgressHistory.StudentDetails",
                cancellationToken: ct);
            var fixedFeePayment = feePayments.Sum(x => x.Amount);

            // Legacy optional filter (exact): PaymentDate.Year == year && Month >= startMonth && PaymentDate <= endDate
            var optionalPayments = await _uow.Repository<StudentTermlyOptionalPayment>().GetAllAsync(
                Filter: x => x.PaymentDate.Year == year
                             && x.PaymentDate.Month >= startMonth
                             && x.PaymentDate <= endDate
                             && x.StudentTermlyOptionalCharges != null
                             && x.StudentTermlyOptionalCharges.StudentDetail != null
                             && x.StudentTermlyOptionalCharges.StudentDetail.SchoolInfoId == schoolId,
                IncludeProperties: "StudentTermlyOptionalCharges.StudentDetail",
                cancellationToken: ct);
            var optionalFeePayment = optionalPayments.Sum(c => c.Amount);

            var totalExpenditure = otherExpenses + salaryPayment;
            var totalIncome = fixedFeePayment + otherIncome + optionalFeePayment;
            var netProfit = totalIncome - totalExpenditure;

            var dto = new AccountingIncomeStatementDto
            {
                Year = year,
                StartMonth = startMonth,
                EndMonth = endMonth,
                OtherExpenses = otherExpenses,
                SalaryPayment = salaryPayment,
                TotalExpenditure = totalExpenditure,
                TotalIncome = totalIncome,
                NetProfit = netProfit,
                OtherIncome = otherIncome,
                FixedFeePayment = fixedFeePayment,
                OptionalFeePayment = optionalFeePayment,
            };

            dto.LegacyDelimited =
                FormatNaira(otherExpenses) + "&" +
                FormatNaira(salaryPayment) + "&" +
                FormatNaira(totalExpenditure) + "&" +
                FormatNaira(totalIncome) + "&" +
                FormatNetProfit(netProfit) + "&" +
                FormatNaira(otherIncome) + "&" +
                FormatNaira(fixedFeePayment) + "&" +
                FormatNaira(optionalFeePayment);

            return dto;
        }

        private async Task<double> SumPaidSalaryForPeriodAsync(
            string schoolId,
            short year,
            byte startMonth,
            byte endMonth,
            CancellationToken ct)
        {
            var years = await _uow.Repository<TxYear>().WhereAsync(
                y => y.SchoolId == schoolId && y.Year == year,
                ct);
            var yearIds = years.Select(y => y.Id).ToList();
            if (yearIds.Count == 0)
            {
                return 0;
            }

            var months = await _uow.Repository<TxMonth>().WhereAsync(
                m => yearIds.Contains(m.YearId)
                     && m.Month >= startMonth
                     && m.Month <= endMonth,
                ct);
            var monthIds = months.Select(m => m.Id).ToList();
            if (monthIds.Count == 0)
            {
                return 0;
            }

            var levels = await _uow.Repository<TxEmployeeMonthlyPaymentLevel>().WhereAsync(
                l => monthIds.Contains(l.MonthId),
                ct);
            var levelIds = levels.Select(l => l.Id).ToList();
            if (levelIds.Count == 0)
            {
                return 0;
            }

            var payments = await _uow.Repository<TxEmployeesMonthlyPayment>().WhereAsync(
                p => levelIds.Contains(p.EmployeeMonthlyPaymentLevelId)
                     && p.EmployeeMonthlyPaymentStatus == EmployeeMonthlyPaymentStatus.HasBeenPaid,
                ct);

            return payments.Sum(c => (c.Allowance + c.BasicSalary + c.Bonus) - c.Deduction);
        }

        public async Task<IList<SelectListItem>> GetTermsDropdownForSessionAsync(
            string sessionId,
            CancellationToken ct = default)
        {
            // Legacy RegistrationService.GetTermsDropdownForSelectedSession: Value = YearlyAndTermlyStatus.Id
            if (string.IsNullOrWhiteSpace(sessionId) || sessionId == "-1")
            {
                return new List<SelectListItem>();
            }

            var terms = await _uow.Repository<TxYearlyAndTermlyStatus>().WhereAsync(
                h => h.SessionId == sessionId,
                ct);

            return terms
                .OrderBy(v => v.TermStatus)
                .Select(x => new SelectListItem
                {
                    Text = x.TermStatus.ToString(),
                    Value = x.Id,
                })
                .ToList();
        }

        /// <inheritdoc cref="IAccountingService.GetClassLevelSchoolFeesAsync" />
        public async Task<AccountingSetSchoolFeesLoadDto> GetClassLevelSchoolFeesAsync(
            string userId,
            CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var currentTerm = await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                s => s.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                     && s.Session != null
                     && s.Session.SchoolInfoId == schoolId,
                ct,
                s => s.Session!);

            if (currentTerm == null)
            {
                // Legacy returns null → view shows admin assistance message.
                return new AccountingSetSchoolFeesLoadDto { HasCurrentTerm = false, Fees = null };
            }

            var feesRepo = _uow.Repository<SetSchoolFeesTr>();
            var currentFees = await feesRepo.FindAsync(
                s => s.SchoolId == schoolId && s.YearlyAndTermlyStatusId == currentTerm.Id,
                ct);

            if (currentFees == null)
            {
                var previousFees = await feesRepo.WhereAsync(x => x.SchoolId == schoolId, ct);
                if (previousFees.Count > 0)
                {
                    // Legacy: OrderByDescending(YearlyAndTermlyStatusId). First.
                    // With GUID PKs, CreatedDate is the practical "last fees" order.
                    var lastFeesModel = previousFees
                        .OrderByDescending(x => x.CreatedDate)
                        .ThenByDescending(x => x.Id)
                        .First();

                    var clone = CloneFeesForTerm(lastFeesModel, schoolId, currentTerm.Id, userId);
                    feesRepo.Add(clone);
                    await _uow.SaveChangesAsync(ct);

                    currentFees = await feesRepo.FindAsync(
                        x => x.SchoolId == schoolId && x.YearlyAndTermlyStatusId == currentTerm.Id,
                        ct);
                    return new AccountingSetSchoolFeesLoadDto
                    {
                        HasCurrentTerm = true,
                        Fees = currentFees == null
                            ? new AccountingSetSchoolFeesDto()
                            : MapFeesToFormDto(currentFees, formatCurrency: true),
                    };
                }

                return new AccountingSetSchoolFeesLoadDto
                {
                    HasCurrentTerm = true,
                    Fees = new AccountingSetSchoolFeesDto(),
                };
            }

            return new AccountingSetSchoolFeesLoadDto
            {
                HasCurrentTerm = true,
                Fees = MapFeesToFormDto(currentFees, formatCurrency: true),
            };
        }

        /// <inheritdoc cref="IAccountingService.AddOrUpdateFeePricesAsync" />
        public async Task<AccountingSetSchoolFeesSaveResultDto> AddOrUpdateFeePricesAsync(
            string userId,
            AccountingSetSchoolFeesDto model,
            CancellationToken ct = default)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var feesRepo = _uow.Repository<SetSchoolFeesTr>();

            // Legacy: Find fees where YearlyAndTermlyStatus.TermlyAccessmentStatus == Current && SchoolId
            var currentFeesModel = await feesRepo.FindAsync(
                s => s.SchoolId == schoolId
                     && s.YearlyAndTermlyStatus != null
                     && s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                s => s.YearlyAndTermlyStatus!);

            if (currentFeesModel == null)
            {
                var yearlyAndTermlyStatus = await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                    s => s.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                         && s.Session != null
                         && s.Session.SchoolInfoId == schoolId,
                    ct,
                    s => s.Session!);

                if (yearlyAndTermlyStatus == null)
                {
                    return new AccountingSetSchoolFeesSaveResultDto
                    {
                        Status = 3,
                        Message = "Failed! The current 'academic term' need to be first created.",
                    };
                }

                var entity = new SetSchoolFeesTr
                {
                    Id = Guid.NewGuid().ToString(),
                    SchoolId = schoolId,
                    YearlyAndTermlyStatusId = yearlyAndTermlyStatus.Id,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = userId ?? string.Empty,
                    UpdatedDate = DateTime.UtcNow,
                    UpdatedBy = userId ?? string.Empty,
                };
                ApplyFeeAmounts(entity, model);
                feesRepo.Add(entity);
            }
            else
            {
                ApplyFeeAmounts(currentFeesModel, model);
                currentFeesModel.UpdatedDate = DateTime.UtcNow;
                currentFeesModel.UpdatedBy = userId ?? string.Empty;
                feesRepo.Update(currentFeesModel);
            }

            await _uow.SaveChangesAsync(ct);
            return new AccountingSetSchoolFeesSaveResultDto
            {
                Status = 4,
                Message = "Changes has successfully been effected.",
            };
        }

        private static SetSchoolFeesTr CloneFeesForTerm(
            SetSchoolFeesTr source,
            string schoolId,
            string termId,
            string userId)
        {
            return new SetSchoolFeesTr
            {
                Id = Guid.NewGuid().ToString(),
                SchoolId = schoolId,
                YearlyAndTermlyStatusId = termId,
                FreshPlayClass = source.FreshPlayClass,
                FreshPreNursery = source.FreshPreNursery,
                FreshNursery = source.FreshNursery,
                FreshPrimary = source.FreshPrimary,
                FreshJss = source.FreshJss,
                FreshScience = source.FreshScience,
                FreshArt = source.FreshArt,
                FreshCommercial = source.FreshCommercial,
                RetnPlayClass = source.RetnPlayClass,
                RetnPreNursery = source.RetnPreNursery,
                RetnNursery = source.RetnNursery,
                RetnPrimary = source.RetnPrimary,
                RetnJss = source.RetnJss,
                RetnScience = source.RetnScience,
                RetnArt = source.RetnArt,
                RetnCommercial = source.RetnCommercial,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = userId ?? string.Empty,
                UpdatedDate = DateTime.UtcNow,
                UpdatedBy = userId ?? string.Empty,
            };
        }

        private static void ApplyFeeAmounts(SetSchoolFeesTr entity, AccountingSetSchoolFeesDto model)
        {
            // Legacy double.Parse on posted strings (commas already stripped by submitForm).
            entity.FreshArt = ParseFeeAmount(model.FreshArt);
            entity.FreshJss = ParseFeeAmount(model.FreshJss);
            entity.FreshNursery = ParseFeeAmount(model.FreshNursery);
            entity.FreshPreNursery = ParseFeeAmount(model.FreshPreNursery);
            entity.FreshCommercial = ParseFeeAmount(model.FreshCommercial);
            entity.FreshScience = ParseFeeAmount(model.FreshScience);
            entity.FreshPrimary = ParseFeeAmount(model.FreshPrimary);
            entity.FreshPlayClass = ParseFeeAmount(model.FreshPlayClass);

            entity.RetnArt = ParseFeeAmount(model.RetnArt);
            entity.RetnJss = ParseFeeAmount(model.RetnJss);
            entity.RetnNursery = ParseFeeAmount(model.RetnNursery);
            entity.RetnPreNursery = ParseFeeAmount(model.RetnPreNursery);
            entity.RetnCommercial = ParseFeeAmount(model.RetnCommercial);
            entity.RetnScience = ParseFeeAmount(model.RetnScience);
            entity.RetnPrimary = ParseFeeAmount(model.RetnPrimary);
            entity.RetnPlayClass = ParseFeeAmount(model.RetnPlayClass);
        }

        private static double ParseFeeAmount(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return 0;
            }

            var cleaned = raw.Replace(",", string.Empty).Trim();
            return double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
                ? value
                : double.Parse(cleaned, CultureInfo.GetCultureInfo("en-GB"));
        }

        /// <summary>Legacy controller: ToString("C").Remove(0, 1) → amount without currency symbol.</summary>
        private static string FormatFeeForForm(double amount)
        {
            var culture = CultureInfo.GetCultureInfo("en-GB");
            var formatted = amount.ToString("C", culture);
            return formatted.Length > 1 ? formatted.Substring(1) : formatted;
        }

        private static AccountingSetSchoolFeesDto MapFeesToFormDto(SetSchoolFeesTr entity, bool formatCurrency)
        {
            string F(double v) => formatCurrency ? FormatFeeForForm(v) : v.ToString(CultureInfo.InvariantCulture);

            return new AccountingSetSchoolFeesDto
            {
                Id = entity.Id,
                SchoolId = entity.SchoolId,
                YearlyAndTermlyStatusId = entity.YearlyAndTermlyStatusId,
                FreshPlayClass = F(entity.FreshPlayClass),
                FreshPreNursery = F(entity.FreshPreNursery),
                FreshNursery = F(entity.FreshNursery),
                FreshPrimary = F(entity.FreshPrimary),
                FreshJss = F(entity.FreshJss),
                FreshScience = F(entity.FreshScience),
                FreshArt = F(entity.FreshArt),
                FreshCommercial = F(entity.FreshCommercial),
                RetnPlayClass = F(entity.RetnPlayClass),
                RetnPreNursery = F(entity.RetnPreNursery),
                RetnNursery = F(entity.RetnNursery),
                RetnPrimary = F(entity.RetnPrimary),
                RetnJss = F(entity.RetnJss),
                RetnScience = F(entity.RetnScience),
                RetnArt = F(entity.RetnArt),
                RetnCommercial = F(entity.RetnCommercial),
            };
        }

        /// <inheritdoc cref="IAccountingService.GetFreshiersListAsync" />
        public async Task<AccountingNewStudentFeesLoadDto> GetFreshiersListAsync(
            string userId,
            CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var currentFeeModel = await ResolveCurrentFeeModelForSchoolAsync(schoolId, userId, ct);
            if (currentFeeModel == null)
            {
                return new AccountingNewStudentFeesLoadDto { FeesConfigured = false };
            }

            var currentTermId = currentFeeModel.YearlyAndTermlyStatusId;
            var newStudent = (await _uow.Repository<StudentAcademicTxProgressHistory>().WhereAsync(
                x => x.StudentDetails!.SchoolInfoId == schoolId
                     && x.YearlyAndTermlyStatusId == currentTermId
                     && x.StudentDetails.Status == StudentStatus.Fresher,
                ct,
                s => s.StudentDetails!.User!,
                a => a.StudentDetails!.ClassArm!.Classs!)).ToList();

            if (newStudent.Count == 0)
            {
                return new AccountingNewStudentFeesLoadDto
                {
                    FeesConfigured = true,
                    InfoMessage = "No new student has been registered this term.",
                    Students = new List<AccountingNewStudentFeesStudentDto>(),
                };
            }

            var studentIds = newStudent.Select(s => s.StudentId).Distinct().ToList();
            var progressIds = newStudent.Select(s => s.Id).ToList();
            var allPayments = await _uow.Repository<StudentTyTermlyFeePayment>().GetAllAsync(
                Filter: x => x.StudentAcademicProgressHistory != null
                             && studentIds.Contains(x.StudentAcademicProgressHistory.StudentId),
                IncludeProperties: "StudentAcademicProgressHistory",
                cancellationToken: ct);
            var discounts = (await _uow.Repository<StudentTyTermlyFixedPaymentDiscount>().WhereAsync(
                x => progressIds.Contains(x.StudentAcademicProgressId),
                ct)).ToList();

            var studentFieldExtractionList = new List<AccountingNewStudentFeesStudentDto>();
            foreach (var eachStudent in newStudent)
            {
                var details = eachStudent.StudentDetails;
                if (details?.ClassArm?.Classs == null || details.User == null)
                {
                    continue;
                }

                var hisClass = details.ClassArm.Classs.ClassName;
                var classCategory = ResolveClassCategory(hisClass, details.ClassCategory);
                var hisActualFees = GetFeesForFreshiers(currentFeeModel, classCategory);
                var hisTotalTermPaymentObjects = allPayments
                    .Where(x => x.StudentAcademicProgressHistory?.StudentId == eachStudent.StudentId)
                    .Sum(x => x.Amount);

                if (Math.Abs(hisTotalTermPaymentObjects) < 0.9)
                {
                    studentFieldExtractionList.Add(MapFreshierStudentDto(
                        eachStudent, details, hisClass, classCategory, FeesPaymentStatus.None));
                }
                else if (hisTotalTermPaymentObjects > 0
                         && hisTotalTermPaymentObjects < hisActualFees
                         && discounts.All(z => z.StudentAcademicProgressId != eachStudent.Id))
                {
                    studentFieldExtractionList.Add(MapFreshierStudentDto(
                        eachStudent, details, hisClass, classCategory, FeesPaymentStatus.PartPayment));
                }
            }

            return new AccountingNewStudentFeesLoadDto
            {
                FeesConfigured = true,
                Students = studentFieldExtractionList
                    .OrderBy(p => p.CurrentClass)
                    .ThenBy(d => d.ArmName)
                    .ThenBy(d => d.ClassCategoryStatus)
                    .ToList(),
            };
        }

        /// <inheritdoc cref="IAccountingService.PayForSelectedFreshiersRegistrationAsync" />
        public async Task<object> PayForSelectedFreshiersRegistrationAsync(
            string userId,
            IList<StudentIdsAndClassCategoryBto> studentIds,
            CancellationToken ct = default)
        {
            if (studentIds == null || studentIds.Count == 0)
            {
                return "Sorry! You have previously made payment for this selections.";
            }

            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var employee = await GetEmployeeForUserAsync(userId, ct);
            var signature = employee?.SignatureImageString ?? string.Empty;

            var yearlyAndTermlyAssessmentStatus = await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                s => s.Session != null
                     && s.Session.SchoolInfoId == schoolId
                     && s.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                g => g.Session!);

            if (yearlyAndTermlyAssessmentStatus == null)
            {
                return "Failed! Other registration needs to be first done before here.";
            }

            var setSchoolFeesModel = await _uow.Repository<SetSchoolFeesTr>().FindAsync(
                s => s.SchoolId == schoolId && s.YearlyAndTermlyStatusId == yearlyAndTermlyAssessmentStatus.Id,
                ct);

            if (setSchoolFeesModel == null)
            {
                return "Failed! Other registration needs to be first done before here.";
            }

            var schoolModel = await _uow.Repository<SchoolTyInfo>().FindAsync(
                s => s.SchoolRegistrationOfficerId == schoolId,
                ct);

            var paymentRepo = _uow.Repository<StudentTyTermlyFeePayment>();
            var historyRepo = _uow.Repository<StudentAcademicTxProgressHistory>();
            var returnList = new List<StudentFieldExtractionForPaymentReceiptDto>();

            foreach (var eachId in studentIds)
            {
                if (string.IsNullOrWhiteSpace(eachId.Id))
                {
                    continue;
                }

                var currentStudent = await historyRepo.FindAsync(
                    f => f.Id == eachId.Id,
                    ct,
                    d => d.Classs!,
                    u => u.StudentDetails!.User!,
                    v => v.StudentDetails!.ClassArm!.Classs!);

                if (currentStudent?.StudentDetails?.User == null || currentStudent.Classs == null)
                {
                    continue;
                }

                var classs = currentStudent.StudentDetails.ClassArm?.Classs?.ClassName
                             ?? currentStudent.Classs.ClassName;
                double amount;
                if (classs is not (CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3))
                {
                    var className3 = GetClassCategoryStatus(classs);
                    amount = GetFeesForFreshiers(setSchoolFeesModel, className3);
                }
                else
                {
                    amount = GetFeesForFreshiers(setSchoolFeesModel, currentStudent.StudentDetails.ClassCategory);
                }

                var hisTotalPay = (await paymentRepo.WhereAsync(
                    x => x.StudentAcademicProgressHistoryId == eachId.Id,
                    ct)).Sum(x => x.Amount);

                if (Math.Abs(hisTotalPay) >= 0.9)
                {
                    continue;
                }

                var paymentId = await BuildPaymentIdAsync(eachId.PaymentId, eachId.PaymentType, paymentRepo, ct);
                eachId.PaymentDate = NormalizeToUtc(eachId.PaymentDate ?? DateTime.UtcNow);

                var eachPayment = new StudentTyTermlyFeePayment
                {
                    Id = Guid.NewGuid().ToString(),
                    StudentAcademicProgressHistoryId = eachId.Id,
                    Amount = amount,
                    PaymentId = paymentId,
                    PaymentType = eachId.PaymentType,
                    PaymentDate = eachId.PaymentDate.Value,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = userId ?? string.Empty,
                    UpdatedDate = DateTime.UtcNow,
                    UpdatedBy = userId ?? string.Empty,
                };
                paymentRepo.Add(eachPayment);

                returnList.Add(new StudentFieldExtractionForPaymentReceiptDto
                {
                    AdmisionNo = currentStudent.StudentDetails.IdNo,
                    ClassLabel = currentStudent.Classs.ClassLabel,
                    FullName = currentStudent.StudentDetails.User.FullName,
                    TermAndSession = yearlyAndTermlyAssessmentStatus.TermStatus + ", "
                                     + yearlyAndTermlyAssessmentStatus.Session!.Session,
                    Fees = FormatNaira(amount),
                    SchoolLogo = schoolModel?.SchoolLogoString ?? string.Empty,
                    SchoolAddress =
                        "<h4 style='margin:0; text-align:center; padding:0px'>"
                        + (schoolModel?.SchoolName ?? string.Empty)
                        + "</h4><strong>Address: </strong> "
                        + (schoolModel?.SchoolAddress ?? string.Empty)
                        + " <br /><div class='text-center'><strong>Tel: </strong>"
                        + (schoolModel?.Telephone ?? string.Empty)
                        + "</div>",
                    AccountSignature = signature,
                    PaymentId = paymentId,
                    PaymentDate = eachId.PaymentDate.Value.ToLongDateString(),
                });
            }

            if (returnList.Count == 0)
            {
                return "Sorry! You have previously made payment for this selections.";
            }

            await _uow.SaveChangesAsync(ct);
            return returnList;
        }

        /// <inheritdoc cref="IAccountingService.GetSelectedStudentFreshPaymentHistoryByIdAsync" />
        public async Task<List<StudentTermlyFees2Nd>> GetSelectedStudentFreshPaymentHistoryByIdAsync(
            string studentLevelPaymentId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(studentLevelPaymentId))
            {
                return new List<StudentTermlyFees2Nd>();
            }

            var studentHistoryObject = await _uow.Repository<StudentAcademicTxProgressHistory>().FindAsync(
                z => z.Id == studentLevelPaymentId,
                ct,
                x => x.Classs!,
                d => d.StudentDetails!.ClassArm!.Classs!);

            if (studentHistoryObject?.Classs == null || studentHistoryObject.StudentDetails?.ClassArm?.Classs == null)
            {
                return new List<StudentTermlyFees2Nd>();
            }

            var schoolId = studentHistoryObject.Classs.SchoolInfoId;
            var paymentList = await _uow.Repository<StudentTyTermlyFeePayment>().GetAllAsync(
                Filter: d => d.StudentAcademicProgressHistory != null
                             && d.StudentAcademicProgressHistory.StudentId == studentHistoryObject.StudentId
                             && d.StudentAcademicProgressHistory.YearlyAndTermlyStatus != null
                             && d.StudentAcademicProgressHistory.YearlyAndTermlyStatus.TermlyAccessmentStatus
                             == TermlyAccessmentStatus.Current,
                IncludeProperties: "StudentAcademicProgressHistory.StudentDetails.User,StudentAcademicProgressHistory.YearlyAndTermlyStatus.Session",
                cancellationToken: ct);

            var termFeeModel = await _uow.Repository<SetSchoolFeesTr>().FindAsync(
                s => s.SchoolId == schoolId
                     && s.YearlyAndTermlyStatus != null
                     && s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                x => x.YearlyAndTermlyStatus!.Session!);

            if (termFeeModel?.YearlyAndTermlyStatus?.Session == null)
            {
                return new List<StudentTermlyFees2Nd>();
            }

            var className = studentHistoryObject.StudentDetails.ClassArm.Classs.ClassName;
            var classCategory = ResolveClassCategory(className, studentHistoryObject.StudentDetails.ClassCategory);
            var actualFee = GetFeesForFreshiers(termFeeModel, classCategory);

            if (paymentList.Count > 0)
            {
                var paid = paymentList.Sum(x => x.Amount);
                if (paid < actualFee)
                {
                    var feePaymentDiscount = await _uow.Repository<StudentTyTermlyFixedPaymentDiscount>().FindAsync(
                        z => z.StudentAcademicProgressId == studentHistoryObject.Id,
                        ct);
                    if (feePaymentDiscount == null)
                    {
                        return new List<StudentTermlyFees2Nd>
                        {
                            new()
                            {
                                StudentAcademicLevelId = studentLevelPaymentId,
                                TermStatus = termFeeModel.YearlyAndTermlyStatus.TermStatus,
                                Session = termFeeModel.YearlyAndTermlyStatus.Session.Session,
                                ActualFees = FormatNaira(actualFee),
                                AmountPaid = FormatNaira(paid),
                                Outstanding = FormatNaira(actualFee - paid),
                            },
                        };
                    }
                }

                return new List<StudentTermlyFees2Nd>();
            }

            return new List<StudentTermlyFees2Nd>
            {
                new()
                {
                    StudentAcademicLevelId = studentLevelPaymentId,
                    TermStatus = termFeeModel.YearlyAndTermlyStatus.TermStatus,
                    Session = termFeeModel.YearlyAndTermlyStatus.Session.Session,
                    ActualFees = FormatNaira(actualFee),
                    AmountPaid = "0",
                    Outstanding = FormatNaira(actualFee),
                },
            };
        }

        /// <inheritdoc cref="IAccountingService.SingleStudentFreshPartPaymentAsync" />
        public async Task<SingleStudentTermlyFeesReturnData> SingleStudentFreshPartPaymentAsync(
            string userId,
            AccountingFreshPartPaymentRequestDto request,
            CancellationToken ct = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.StudentLevelPaymentId))
            {
                return new SingleStudentTermlyFeesReturnData();
            }

            var employee = await GetEmployeeForUserAsync(userId, ct);
            var schoolModel = employee?.SchoolInfo;
            if (schoolModel == null || request.HisFee > request.ActualFees)
            {
                return new SingleStudentTermlyFeesReturnData();
            }

            var paymentRepo = _uow.Repository<StudentTyTermlyFeePayment>();
            var paymentId = await BuildPaymentIdAsync(request.PaymentId, request.PaymentType, paymentRepo, ct);

            // Legacy uses DateTime.Now; Postgres timestamptz requires UTC Kind.
            var paymentDateUtc = request.PaymentDate.HasValue
                ? NormalizeToUtc(request.PaymentDate.Value)
                : DateTime.UtcNow;

            var eachPayment = new StudentTyTermlyFeePayment
            {
                Id = Guid.NewGuid().ToString(),
                StudentAcademicProgressHistoryId = request.StudentLevelPaymentId,
                Amount = request.HisFee,
                PaymentId = paymentId,
                PaymentType = request.PaymentType,
                PaymentDate = paymentDateUtc,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = userId ?? string.Empty,
                UpdatedDate = DateTime.UtcNow,
                UpdatedBy = userId ?? string.Empty,
            };
            paymentRepo.Add(eachPayment);

            if (request.Discount > 0)
            {
                var discountRepo = _uow.Repository<StudentTyTermlyFixedPaymentDiscount>();
                discountRepo.Add(new StudentTyTermlyFixedPaymentDiscount
                {
                    Id = Guid.NewGuid().ToString(),
                    StudentAcademicProgressId = request.StudentLevelPaymentId,
                    DiscountAmount = request.Discount,
                    ReasonsForDiscount = request.ReasonForDiscount ?? string.Empty,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = userId ?? string.Empty,
                    UpdatedDate = DateTime.UtcNow,
                    UpdatedBy = userId ?? string.Empty,
                });
            }

            await _uow.SaveChangesAsync(ct);

            return new SingleStudentTermlyFeesReturnData
            {
                SchoolName = schoolModel.SchoolName,
                SchoolLogo = schoolModel.SchoolLogoString,
                SchoolAddress = schoolModel.SchoolAddress,
                SchoolTel = schoolModel.Telephone,
                SchoolId = schoolModel.SchoolRegistrationOfficerId,
                AccountSignature = employee?.SignatureImageString ?? string.Empty,
                PaymentId = paymentId,
                AdmisnNo = request.AdmisnNo,
                StudentName = request.StudentName,
                TermAndSession = request.TermAndSession,
                ClassAndArm = request.ClassAndArm,
                PreviousDebt = FormatNaira(request.ActualFees),
                AmountPaid = FormatNaira(request.HisFee),
                OutStandingDebt = FormatNaira(request.ActualFees - request.HisFee),
                Discount = FormatNaira(request.Discount),
                PaymentDate = request.PaymentDate?.ToLongDateString(),
            };
        }

        /// <inheritdoc cref="IAccountingService.GetReturningStudentsListAsync" />
        public async Task<AccountingReturningStudentFeesLoadDto> GetReturningStudentsListAsync(
            string userId,
            CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var feesRepo = _uow.Repository<SetSchoolFeesTr>();
            var feesPricesList = (await feesRepo.WhereAsync(
                d => d.SchoolId == schoolId,
                ct,
                c => c.YearlyAndTermlyStatus!)).ToList();

            if (feesPricesList.Count == 0)
            {
                return new AccountingReturningStudentFeesLoadDto { FeesConfigured = false };
            }

            var currentFeeModel = feesPricesList.Find(
                s => s.YearlyAndTermlyStatus?.TermlyAccessmentStatus == TermlyAccessmentStatus.Current);
            if (currentFeeModel == null)
            {
                var lastFeesModel = feesPricesList
                    .OrderByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.Id)
                    .First();
                var currentTerm = await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                    s => s.Session != null
                         && s.Session.SchoolInfoId == schoolId
                         && s.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                    ct,
                    s => s.Session!);
                if (currentTerm != null)
                {
                    feesRepo.Add(CloneFeesForTerm(lastFeesModel, schoolId, currentTerm.Id, userId));
                    await _uow.SaveChangesAsync(ct);
                    feesPricesList = (await feesRepo.WhereAsync(
                        d => d.SchoolId == schoolId,
                        ct,
                        c => c.YearlyAndTermlyStatus!)).ToList();
                }
            }

            var historyRepo = _uow.Repository<StudentAcademicTxProgressHistory>();
            var returningStudentInAcademicProgressHistory = (await historyRepo.WhereAsync(
                x => x.StudentDetails!.SchoolInfoId == schoolId
                     && x.YearlyAndTermlyStatus != null
                     && x.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                     && x.StudentDetails.Status == StudentStatus.Returning,
                ct,
                s => s.StudentDetails!.User!,
                x => x.StudentDetails!.ClassArm!,
                a => a.Classs!,
                t => t.YearlyAndTermlyStatus!)).ToList();

            var actualReturningStudents = (await _uow.Repository<TxStudentDetail>().WhereAsync(
                s => s.SchoolInfoId == schoolId && s.Status == StudentStatus.Returning,
                ct,
                s => s.ClassArm!)).ToList();

            if (returningStudentInAcademicProgressHistory.Count != actualReturningStudents.Count
                && returningStudentInAcademicProgressHistory.Count > 0)
            {
                var currentTermId = returningStudentInAcademicProgressHistory.First().YearlyAndTermlyStatusId;
                var existingStudentIds = returningStudentInAcademicProgressHistory
                    .Select(x => x.StudentId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var added = false;
                foreach (var item in actualReturningStudents)
                {
                    if (existingStudentIds.Contains(item.Id) || item.ClassArm == null)
                    {
                        continue;
                    }

                    historyRepo.Add(new StudentAcademicTxProgressHistory
                    {
                        Id = Guid.NewGuid().ToString(),
                        StudentId = item.Id,
                        ClassId = item.ClassArm.ClassId,
                        YearlyAndTermlyStatusId = currentTermId,
                        ClassCategory = item.ClassCategory,
                    });
                    added = true;
                }

                if (added)
                {
                    await _uow.SaveChangesAsync(ct);
                    returningStudentInAcademicProgressHistory = (await historyRepo.WhereAsync(
                        x => x.StudentDetails!.SchoolInfoId == schoolId
                             && x.YearlyAndTermlyStatus != null
                             && x.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                             && x.StudentDetails.Status == StudentStatus.Returning,
                        ct,
                        s => s.StudentDetails!.User!,
                        x => x.StudentDetails!.ClassArm!,
                        a => a.Classs!,
                        t => t.YearlyAndTermlyStatus!)).ToList();
                }
            }

            if (returningStudentInAcademicProgressHistory.Count == 0)
            {
                return new AccountingReturningStudentFeesLoadDto
                {
                    FeesConfigured = true,
                    InfoMessage = "No returning student is found.",
                    Students = new List<AccountingReturningStudentFeesStudentDto>(),
                };
            }

            var schoolInfo = await _uow.Repository<SchoolTyInfo>().FindAsync(
                z => z.SchoolRegistrationOfficerId == schoolId,
                ct);
            var accountingStartTermId = schoolInfo?.AccountingStartTermId;
            var orderedTermIds = await GetChronologicalTermIdsAsync(schoolId, ct);
            var feesByTermId = feesPricesList.ToDictionary(f => f.YearlyAndTermlyStatusId, f => f);
            var discounts = (await _uow.Repository<StudentTyTermlyFixedPaymentDiscount>().GetAllAsync(
                cancellationToken: ct)).ToList();

            var studentFieldExtractionList = new List<AccountingReturningStudentFeesStudentDto>();
            foreach (var eachStudentHistory in returningStudentInAcademicProgressHistory)
            {
                var currentFeePaymentStatus = FeesPaymentStatus.Completed;
                var previousFeePaymentStatus = FeesPaymentStatus.Completed;
                var studentModel = eachStudentHistory.StudentDetails;
                if (studentModel?.User == null || eachStudentHistory.Classs == null)
                {
                    continue;
                }

                var allHistoryForStudent = (await historyRepo.WhereAsync(
                    c => c.StudentId == studentModel.Id,
                    ct,
                    x => x.Classs!,
                    v => v.StudentTermlyFeePayment,
                    d => d.StudentDetails!,
                    y => y.YearlyAndTermlyStatus!.Session!)).ToList();

                var listOfAcademicProgressSoFar = allHistoryForStudent
                    .Where(c => TermIdIsAtOrAfter(c.YearlyAndTermlyStatusId, accountingStartTermId, orderedTermIds)
                                && TermIdIsAtOrAfter(c.YearlyAndTermlyStatusId, studentModel.AdmisnTermId, orderedTermIds))
                    .OrderBy(c => TermChronologyIndex(c.YearlyAndTermlyStatusId, orderedTermIds))
                    .ThenBy(c => c.Id)
                    .ToList();

                if (listOfAcademicProgressSoFar.Count == 0)
                {
                    continue;
                }

                var noOfAcademicProgress = listOfAcademicProgressSoFar.Count;
                var initialFeePaymentObject = listOfAcademicProgressSoFar.First();
                var initialTermId = initialFeePaymentObject.YearlyAndTermlyStatusId;
                feesByTermId.TryGetValue(initialTermId, out var actualFeeModel);

                double initialFeeActualPayment = 0;
                var studentInitialRegStatus = initialFeePaymentObject.StudentDetails?.StudentInitialRegStatus
                                              ?? studentModel.StudentInitialRegStatus;
                var classss = initialFeePaymentObject.Classs?.ClassName ?? eachStudentHistory.Classs.ClassName;
                ClassCategoryStatus actualClassCategory;

                if (studentInitialRegStatus == StudentInitialRegistrationStatus.Fresher)
                {
                    if (classss is not (CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3))
                    {
                        var className3 = GetClassCategoryStatus(classss);
                        initialFeeActualPayment = actualFeeModel == null
                            ? 0
                            : GetFeesForFreshiers(actualFeeModel, className3);
                        actualClassCategory = GetClassCategoryStatus(eachStudentHistory.Classs.ClassName);
                    }
                    else
                    {
                        initialFeeActualPayment = actualFeeModel == null
                            ? 0
                            : GetFeesForFreshiers(
                                actualFeeModel,
                                initialFeePaymentObject.StudentDetails?.ClassCategory ?? studentModel.ClassCategory);
                        actualClassCategory = studentModel.ClassCategory;
                    }
                }
                else if (classss is not (CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3))
                {
                    var className3 = GetClassCategoryStatus(classss);
                    initialFeeActualPayment = actualFeeModel == null
                        ? 0
                        : GetFeesForReturningStudent(actualFeeModel, className3);
                    actualClassCategory = GetClassCategoryStatus(eachStudentHistory.Classs.ClassName);
                }
                else
                {
                    initialFeeActualPayment = actualFeeModel == null
                        ? 0
                        : GetFeesForReturningStudent(
                            actualFeeModel,
                            initialFeePaymentObject.StudentDetails?.ClassCategory ?? studentModel.ClassCategory);
                    actualClassCategory = studentModel.ClassCategory;
                }

                var totalPayment = (initialFeePaymentObject.StudentTermlyFeePayment ?? new List<StudentTyTermlyFeePayment>())
                    .Sum(x => x.Amount);
                if (totalPayment < initialFeeActualPayment)
                {
                    var studentHistoryId = initialFeePaymentObject.Id;
                    var feePaymentDiscount = discounts.FirstOrDefault(z => z.StudentAcademicProgressId == studentHistoryId);
                    if (noOfAcademicProgress == 1)
                    {
                        if (feePaymentDiscount != null)
                        {
                            currentFeePaymentStatus = FeesPaymentStatus.Completed;
                        }
                        else if (totalPayment > 0)
                        {
                            currentFeePaymentStatus = FeesPaymentStatus.PartPayment;
                        }
                        else if (Math.Abs(totalPayment) < 0.9)
                        {
                            currentFeePaymentStatus = FeesPaymentStatus.None;
                        }
                    }
                    else if (feePaymentDiscount != null)
                    {
                        previousFeePaymentStatus = FeesPaymentStatus.Completed;
                    }
                    else if (totalPayment > 0)
                    {
                        previousFeePaymentStatus = FeesPaymentStatus.PartPayment;
                    }
                    else if (Math.Abs(totalPayment) < 0.9)
                    {
                        previousFeePaymentStatus = FeesPaymentStatus.None;
                    }
                }

                listOfAcademicProgressSoFar.RemoveAt(0);

                if (listOfAcademicProgressSoFar.Count > 0)
                {
                    var currentAcademicHistoryPaymentStatus = listOfAcademicProgressSoFar
                        .OrderBy(x => TermChronologyIndex(x.YearlyAndTermlyStatusId, orderedTermIds))
                        .ThenBy(x => x.Id)
                        .Last();

                    feesByTermId.TryGetValue(
                        currentAcademicHistoryPaymentStatus.YearlyAndTermlyStatusId,
                        out actualFeeModel);

                    double returningStudentFee;
                    if (classss is not (CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3))
                    {
                        var classCategory = GetClassCategoryStatus(
                            listOfAcademicProgressSoFar.First().Classs?.ClassName ?? classss);
                        returningStudentFee = actualFeeModel == null
                            ? 0
                            : GetFeesForReturningStudent(actualFeeModel, classCategory);
                    }
                    else
                    {
                        returningStudentFee = actualFeeModel == null
                            ? 0
                            : GetFeesForReturningStudent(
                                actualFeeModel,
                                currentAcademicHistoryPaymentStatus.StudentDetails?.ClassCategory
                                ?? studentModel.ClassCategory);
                    }

                    var totalPartPayment =
                        (currentAcademicHistoryPaymentStatus.StudentTermlyFeePayment ?? new List<StudentTyTermlyFeePayment>())
                        .Sum(x => x.Amount);

                    if (Math.Abs(totalPartPayment - returningStudentFee) < 0.7)
                    {
                        currentFeePaymentStatus = FeesPaymentStatus.Completed;
                    }
                    else if (totalPartPayment >= 0 && totalPartPayment < returningStudentFee)
                    {
                        var studentAcademicProgressId = eachStudentHistory.Id;
                        var hasAnyDiscountThisTerm = discounts.FirstOrDefault(
                            v => v.StudentAcademicProgressId == studentAcademicProgressId);

                        // Legacy operator precedence: DiscountAmount ?? (0 + totalPartPayment)
                        if (hasAnyDiscountThisTerm == null && totalPartPayment > 0)
                        {
                            currentFeePaymentStatus = FeesPaymentStatus.PartPayment;
                        }
                        else if (hasAnyDiscountThisTerm == null && Math.Abs(totalPartPayment) < 0.5)
                        {
                            currentFeePaymentStatus = FeesPaymentStatus.None;
                        }
                        else if (Math.Abs(
                                     (hasAnyDiscountThisTerm?.DiscountAmount ?? 0 + totalPartPayment)
                                     - returningStudentFee) < 0.9)
                        {
                            currentFeePaymentStatus = FeesPaymentStatus.Completed;
                        }
                    }

                    listOfAcademicProgressSoFar.Remove(currentAcademicHistoryPaymentStatus);

                    if (listOfAcademicProgressSoFar.Count > 0
                        && previousFeePaymentStatus == FeesPaymentStatus.Completed)
                    {
                        foreach (var each in listOfAcademicProgressSoFar)
                        {
                            feesByTermId.TryGetValue(each.YearlyAndTermlyStatusId, out actualFeeModel);
                            if (classss is not (CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3))
                            {
                                var classCategory = GetClassCategoryStatus(
                                    listOfAcademicProgressSoFar.First().Classs?.ClassName ?? classss);
                                returningStudentFee = actualFeeModel == null
                                    ? 0
                                    : GetFeesForReturningStudent(actualFeeModel, classCategory);
                            }
                            else
                            {
                                returningStudentFee = actualFeeModel == null
                                    ? 0
                                    : GetFeesForReturningStudent(
                                        actualFeeModel,
                                        each.StudentDetails?.ClassCategory ?? studentModel.ClassCategory);
                            }

                            totalPartPayment = (each.StudentTermlyFeePayment ?? new List<StudentTyTermlyFeePayment>())
                                .Sum(x => x.Amount);
                            if (Math.Abs(totalPartPayment - returningStudentFee) < 0.7)
                            {
                                previousFeePaymentStatus = FeesPaymentStatus.Completed;
                            }
                            else if (totalPartPayment >= 0 && totalPartPayment < returningStudentFee)
                            {
                                var hasAnyDiscountThisTerm = discounts.FirstOrDefault(
                                    v => v.StudentAcademicProgressId == eachStudentHistory.Id);
                                if (hasAnyDiscountThisTerm == null && totalPartPayment > 0)
                                {
                                    previousFeePaymentStatus = FeesPaymentStatus.PartPayment;
                                    break;
                                }

                                if (hasAnyDiscountThisTerm == null && Math.Abs(totalPartPayment) < 0.5)
                                {
                                    previousFeePaymentStatus = FeesPaymentStatus.None;
                                    break;
                                }

                                if (Math.Abs(
                                        (hasAnyDiscountThisTerm?.DiscountAmount ?? 0 + totalPartPayment)
                                        - returningStudentFee) < 0.9)
                                {
                                    previousFeePaymentStatus = FeesPaymentStatus.Completed;
                                }
                            }
                            else
                            {
                                previousFeePaymentStatus = FeesPaymentStatus.Completed;
                            }
                        }
                    }
                }
                else
                {
                    previousFeePaymentStatus = FeesPaymentStatus.Completed;
                }

                var otherPaymentStatus = previousFeePaymentStatus;
                if (otherPaymentStatus != FeesPaymentStatus.Completed
                    || currentFeePaymentStatus != FeesPaymentStatus.Completed)
                {
                    studentFieldExtractionList.Add(new AccountingReturningStudentFeesStudentDto
                    {
                        FullName = studentModel.User.FullName,
                        ArmName = studentModel.ClassArm?.ArmName.ToString() ?? string.Empty,
                        ClassLabel = eachStudentHistory.Classs.ClassLabel,
                        CurrentClass = eachStudentHistory.Classs.ClassName.ToString(),
                        AdmisionNo = studentModel.IdNo,
                        StudentLevelPaymentId = eachStudentHistory.Id,
                        ClassCategoryStatus = actualClassCategory,
                        CurrentTermPaymentStatus = currentFeePaymentStatus,
                        OtherTermsPaymentStatus = otherPaymentStatus,
                    });
                }
            }

            return new AccountingReturningStudentFeesLoadDto
            {
                FeesConfigured = true,
                InfoMessage = studentFieldExtractionList.Count == 0
                    ? "No returning student is found."
                    : null,
                Students = studentFieldExtractionList
                    .OrderBy(p => p.CurrentClass)
                    .ThenBy(d => d.ArmName)
                    .ThenBy(d => d.ClassCategoryStatus)
                    .ToList(),
            };
        }

        /// <inheritdoc cref="IAccountingService.PayForSelectedReturningStudentsAsync" />
        public async Task<object> PayForSelectedReturningStudentsAsync(
            string userId,
            IList<StudentIdsAndClassCategoryBto> studentIds,
            CancellationToken ct = default)
        {
            if (studentIds == null || studentIds.Count == 0)
            {
                return "Sorry! The selected student(s) has already paid.";
            }

            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var employee = await GetEmployeeForUserAsync(userId, ct);
            var signature = employee?.SignatureImageString ?? string.Empty;

            var yearlyAndTermlyAssessmentStatus = await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                s => s.Session != null
                     && s.Session.SchoolInfoId == schoolId
                     && s.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                b => b.Session!);

            if (yearlyAndTermlyAssessmentStatus == null)
            {
                return "Failed! Other registration needs to be first done before here.";
            }

            var setSchoolFeesModel = await _uow.Repository<SetSchoolFeesTr>().FindAsync(
                s => s.SchoolId == schoolId && s.YearlyAndTermlyStatusId == yearlyAndTermlyAssessmentStatus.Id,
                ct);
            if (setSchoolFeesModel == null)
            {
                return "Failed! Other registration needs to be first done before here.";
            }

            var schoolModel = await _uow.Repository<SchoolTyInfo>().FindAsync(
                s => s.SchoolRegistrationOfficerId == schoolId,
                ct);
            var paymentRepo = _uow.Repository<StudentTyTermlyFeePayment>();
            var historyRepo = _uow.Repository<StudentAcademicTxProgressHistory>();
            var returnList = new List<StudentFieldExtractionForPaymentReceiptDto>();

            foreach (var eachId in studentIds)
            {
                if (string.IsNullOrWhiteSpace(eachId.Id))
                {
                    continue;
                }

                var currentStudent = await historyRepo.FindAsync(
                    f => f.Id == eachId.Id,
                    ct,
                    d => d.Classs!,
                    u => u.StudentDetails!.User!,
                    a => a.StudentDetails!.ClassArm!);

                if (currentStudent?.StudentDetails?.User == null || currentStudent.Classs == null)
                {
                    continue;
                }

                // Match list / fresher pay: non-SS from class name; SS from StudentDetails.ClassCategory.
                var className = currentStudent.Classs.ClassName;
                var feeCategory = className is CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3
                    ? currentStudent.StudentDetails.ClassCategory
                    : GetClassCategoryStatus(className);
                var amount = GetFeesForReturningStudent(setSchoolFeesModel, feeCategory);

                var hisTotalPay = (await paymentRepo.WhereAsync(
                    x => x.StudentAcademicProgressHistoryId == eachId.Id,
                    ct)).Sum(x => x.Amount);

                // Legacy PayForSelectedReturningStudents: only full unpaid (hisTotalPay == 0).
                if (hisTotalPay != 0)
                {
                    continue;
                }

                var paymentId = await BuildPaymentIdAsync(eachId.PaymentId, eachId.PaymentType, paymentRepo, ct);
                var paymentDateForStore = NormalizeToUtc(eachId.PaymentDate ?? DateTime.UtcNow);
                eachId.PaymentDate = paymentDateForStore;

                paymentRepo.Add(new StudentTyTermlyFeePayment
                {
                    Id = Guid.NewGuid().ToString(),
                    StudentAcademicProgressHistoryId = eachId.Id,
                    Amount = amount,
                    PaymentId = paymentId,
                    PaymentType = eachId.PaymentType,
                    PaymentDate = paymentDateForStore,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = userId ?? string.Empty,
                    UpdatedDate = DateTime.UtcNow,
                    UpdatedBy = userId ?? string.Empty,
                });

                var armName = currentStudent.StudentDetails.ClassArm?.ArmName.ToString() ?? string.Empty;
                var classAndArm = string.IsNullOrWhiteSpace(armName)
                    ? currentStudent.Classs.ClassLabel
                    : currentStudent.Classs.ClassLabel + " " + armName;

                returnList.Add(new StudentFieldExtractionForPaymentReceiptDto
                {
                    AdmisionNo = currentStudent.StudentDetails.IdNo,
                    // Receipt label is "Class / Arm" (legacy JS); include arm like the preview grid.
                    ClassLabel = classAndArm,
                    FullName = currentStudent.StudentDetails.User.FullName,
                    // Numeric term matches part-payment rows (0/1/2), not enum name.
                    TermAndSession = (int)yearlyAndTermlyAssessmentStatus.TermStatus + ", "
                                     + yearlyAndTermlyAssessmentStatus.Session!.Session,
                    Fees = FormatNaira(amount),
                    SchoolLogo = schoolModel?.SchoolLogoString ?? string.Empty,
                    SchoolAddress =
                        "<h4 style='margin:0; text-align:center; padding:0px'>"
                        + (schoolModel?.SchoolName ?? string.Empty)
                        + "</h4><strong>Address: </strong> "
                        + (schoolModel?.SchoolAddress ?? string.Empty)
                        + " <br /><div class='text-center'><strong>Tel: </strong>"
                        + (schoolModel?.Telephone ?? string.Empty)
                        + "</div>",
                    AccountSignature = signature,
                    PaymentId = paymentId,
                    PaymentDate = FormatLongPaymentDate(eachId.PaymentDate ?? paymentDateForStore),
                });
            }

            if (returnList.Count == 0)
            {
                return "Sorry! The selected student(s) has already paid.";
            }

            await _uow.SaveChangesAsync(ct);
            return returnList;
        }

        /// <inheritdoc cref="IAccountingService.GetSelectedReturningStudentPaymentHistoryByIdAsync" />
        public async Task<List<StudentTermlyFees2Nd>> GetSelectedReturningStudentPaymentHistoryByIdAsync(
            string userId,
            string studentLevelPaymentId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(studentLevelPaymentId))
            {
                return new List<StudentTermlyFees2Nd>();
            }

            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var feesCollection = (await _uow.Repository<SetSchoolFeesTr>().WhereAsync(
                s => s.SchoolId == schoolId,
                ct,
                v => v.YearlyAndTermlyStatus!.Session!)).ToList();

            var historyRepo = _uow.Repository<StudentAcademicTxProgressHistory>();
            var studentHistory = await historyRepo.FindAsync(
                z => z.Id == studentLevelPaymentId,
                ct,
                d => d.StudentTermlyFeePayment,
                c => c.StudentDetails!);

            var studentModel = studentHistory?.StudentDetails;
            if (studentModel == null)
            {
                return new List<StudentTermlyFees2Nd>();
            }

            var schoolInfo = await _uow.Repository<SchoolTyInfo>().FindAsync(
                z => z.SchoolRegistrationOfficerId == schoolId,
                ct);
            var accountingStartTermId = schoolInfo?.AccountingStartTermId;
            var orderedTermIds = await GetChronologicalTermIdsAsync(schoolId, ct);

            var allHistory = (await historyRepo.WhereAsync(
                c => c.StudentId == studentModel.Id,
                ct,
                x => x.Classs!,
                v => v.StudentTermlyFeePayment,
                d => d.StudentDetails!,
                y => y.YearlyAndTermlyStatus!.Session!)).ToList();

            var listOfPaymentsSoFar = allHistory
                .Where(c => TermIdIsAtOrAfter(c.YearlyAndTermlyStatusId, accountingStartTermId, orderedTermIds)
                            && TermIdIsAtOrAfter(c.YearlyAndTermlyStatusId, studentModel.AdmisnTermId, orderedTermIds))
                .OrderBy(c => TermChronologyIndex(c.YearlyAndTermlyStatusId, orderedTermIds))
                .ToList();

            if (listOfPaymentsSoFar.Count == 0)
            {
                return new List<StudentTermlyFees2Nd>();
            }

            var terminalFeesDetails = new List<StudentTermlyFees2Nd>();
            var initialFeePaymentObject = listOfPaymentsSoFar.First();
            var actualFeeModel = feesCollection.Find(x => x.YearlyAndTermlyStatusId == initialFeePaymentObject.YearlyAndTermlyStatusId);

            double initialFeeActualPayment = 0;
            var studentInitialRegStatus = initialFeePaymentObject.StudentDetails?.StudentInitialRegStatus
                                          ?? studentModel.StudentInitialRegStatus;
            var classss = initialFeePaymentObject.Classs?.ClassName ?? CurrentClass.PlayClass;

            if (studentInitialRegStatus == StudentInitialRegistrationStatus.Fresher)
            {
                if (classss is not (CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3))
                {
                    var className3 = GetClassCategoryStatus(classss);
                    initialFeeActualPayment = actualFeeModel == null
                        ? 0
                        : GetFeesForFreshiers(actualFeeModel, className3);
                }
                else
                {
                    initialFeeActualPayment = actualFeeModel == null
                        ? 0
                        : GetFeesForFreshiers(
                            actualFeeModel,
                            initialFeePaymentObject.StudentDetails?.ClassCategory ?? studentModel.ClassCategory);
                }
            }
            else if (classss is not (CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3))
            {
                var className3 = GetClassCategoryStatus(classss);
                initialFeeActualPayment = actualFeeModel == null
                    ? 0
                    : GetFeesForReturningStudent(actualFeeModel, className3);
            }
            else
            {
                initialFeeActualPayment = actualFeeModel == null
                    ? 0
                    : GetFeesForReturningStudent(
                        actualFeeModel,
                        initialFeePaymentObject.StudentDetails?.ClassCategory ?? studentModel.ClassCategory);
            }

            var totalPayment = (initialFeePaymentObject.StudentTermlyFeePayment ?? new List<StudentTyTermlyFeePayment>())
                .Sum(x => x.Amount);
            if (totalPayment < initialFeeActualPayment)
            {
                var feePaymentDiscount = await _uow.Repository<StudentTyTermlyFixedPaymentDiscount>().FindAsync(
                    z => z.StudentAcademicProgressId == initialFeePaymentObject.Id,
                    ct);
                if (feePaymentDiscount == null)
                {
                    terminalFeesDetails.Add(new StudentTermlyFees2Nd
                    {
                        StudentAcademicLevelId = initialFeePaymentObject.Id,
                        TermStatus = initialFeePaymentObject.YearlyAndTermlyStatus?.TermStatus ?? default,
                        Session = initialFeePaymentObject.YearlyAndTermlyStatus?.Session?.Session ?? string.Empty,
                        ActualFees = FormatNaira(initialFeeActualPayment),
                        AmountPaid = FormatNaira(totalPayment),
                        Outstanding = FormatNaira(initialFeeActualPayment - totalPayment),
                    });
                }
            }

            listOfPaymentsSoFar.RemoveAt(0);
            foreach (var each in listOfPaymentsSoFar)
            {
                actualFeeModel = feesCollection.Find(x => x.YearlyAndTermlyStatusId == each.YearlyAndTermlyStatusId);
                double returningStudentFee;
                if (classss is not (CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3))
                {
                    var classCategory = GetClassCategoryStatus(
                        listOfPaymentsSoFar.First().Classs?.ClassName ?? classss);
                    returningStudentFee = actualFeeModel == null
                        ? 0
                        : GetFeesForReturningStudent(actualFeeModel, classCategory);
                }
                else
                {
                    returningStudentFee = actualFeeModel == null
                        ? 0
                        : GetFeesForReturningStudent(
                            actualFeeModel,
                            each.StudentDetails?.ClassCategory ?? studentModel.ClassCategory);
                }

                var paid = (each.StudentTermlyFeePayment ?? new List<StudentTyTermlyFeePayment>()).Sum(x => x.Amount);
                if (paid < returningStudentFee)
                {
                    var feePaymentDiscount = await _uow.Repository<StudentTyTermlyFixedPaymentDiscount>().FindAsync(
                        z => z.StudentAcademicProgressId == each.Id,
                        ct);
                    if (feePaymentDiscount == null)
                    {
                        terminalFeesDetails.Add(new StudentTermlyFees2Nd
                        {
                            StudentAcademicLevelId = each.Id,
                            TermStatus = each.YearlyAndTermlyStatus?.TermStatus ?? default,
                            Session = each.YearlyAndTermlyStatus?.Session?.Session ?? string.Empty,
                            ActualFees = FormatNaira(returningStudentFee),
                            AmountPaid = FormatNaira(paid),
                            Outstanding = FormatNaira(returningStudentFee - paid),
                        });
                    }
                }
            }

            return terminalFeesDetails;
        }

        private async Task<List<string>> GetChronologicalTermIdsAsync(string schoolId, CancellationToken ct)
        {
            var terms = (await _uow.Repository<TxYearlyAndTermlyStatus>().WhereAsync(
                t => t.Session != null && t.Session.SchoolInfoId == schoolId,
                ct,
                t => t.Session!)).ToList();

            return terms
                .OrderBy(t => t.Session!.Session)
                .ThenBy(t => t.TermStatus)
                .ThenBy(t => t.CreatedDate)
                .Select(t => t.Id)
                .ToList();
        }

        private static int TermChronologyIndex(string termId, IList<string> orderedTermIds)
        {
            var idx = orderedTermIds.IndexOf(termId);
            return idx < 0 ? int.MaxValue : idx;
        }

        /// <summary>
        /// GUID-safe equivalent of legacy int comparison YearlyAndTermlyStatusId &gt;= thresholdTermId.
        /// </summary>
        private static bool TermIdIsAtOrAfter(string termId, string? thresholdTermId, IList<string> orderedTermIds)
        {
            if (string.IsNullOrWhiteSpace(thresholdTermId))
            {
                return true;
            }

            var termIdx = orderedTermIds.IndexOf(termId);
            var thresholdIdx = orderedTermIds.IndexOf(thresholdTermId);
            if (termIdx >= 0 && thresholdIdx >= 0)
            {
                return termIdx >= thresholdIdx;
            }

            // Fallback when term not in school list: same string compare as legacy GUID store.
            return string.CompareOrdinal(termId, thresholdTermId) >= 0;
        }

        /// <summary>Npgsql timestamptz requires UTC Kind.</summary>
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

            // Date-only payloads (yyyy-MM-dd) arrive as Unspecified — keep the calendar day in UTC.
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        /// <summary>
        /// Receipt PaymentDate string — use calendar Y/M/D so UTC midnight from ISO does not shift the day.
        /// </summary>
        private static string FormatLongPaymentDate(DateTime value)
        {
            var culture = CultureInfo.GetCultureInfo("en-GB");
            return new DateTime(value.Year, value.Month, value.Day).ToString("D", culture);
        }

        private async Task<SetSchoolFeesTr?> ResolveCurrentFeeModelForSchoolAsync(
            string schoolId,
            string userId,
            CancellationToken ct)
        {
            var feesRepo = _uow.Repository<SetSchoolFeesTr>();
            var feesPricesList = (await feesRepo.WhereAsync(
                d => d.SchoolId == schoolId,
                ct,
                c => c.YearlyAndTermlyStatus!)).ToList();

            if (feesPricesList.Count == 0)
            {
                return null;
            }

            var currentFeeModel = feesPricesList.Find(
                d => d.YearlyAndTermlyStatus?.TermlyAccessmentStatus == TermlyAccessmentStatus.Current);

            if (currentFeeModel != null)
            {
                return currentFeeModel;
            }

            var lastFeesModel = feesPricesList
                .OrderByDescending(x => x.CreatedDate)
                .ThenByDescending(x => x.Id)
                .First();

            var yearlyAndTermlyAssessmentStatus = await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                s => s.Session != null
                     && s.Session.SchoolInfoId == schoolId
                     && s.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                ct,
                s => s.Session!);

            if (yearlyAndTermlyAssessmentStatus == null)
            {
                return null;
            }

            var clone = CloneFeesForTerm(lastFeesModel, schoolId, yearlyAndTermlyAssessmentStatus.Id, userId);
            feesRepo.Add(clone);
            await _uow.SaveChangesAsync(ct);

            return await feesRepo.FindAsync(
                x => x.YearlyAndTermlyStatusId == yearlyAndTermlyAssessmentStatus.Id,
                ct,
                x => x.YearlyAndTermlyStatus!);
        }

        private async Task<TxEmployee?> GetEmployeeForUserAsync(string userId, CancellationToken ct)
        {
            return await _uow.Repository<TxEmployee>().FindAsync(
                e => e.UserId == userId,
                ct,
                e => e.SchoolInfo!);
        }

        private static AccountingNewStudentFeesStudentDto MapFreshierStudentDto(
            StudentAcademicTxProgressHistory eachStudent,
            TxStudentDetail details,
            CurrentClass hisClass,
            ClassCategoryStatus classCategory,
            FeesPaymentStatus paymentStatus)
        {
            return new AccountingNewStudentFeesStudentDto
            {
                FullName = details.User!.FullName,
                ArmName = details.ClassArm!.ArmName.ToString(),
                ClassLabel = details.ClassArm.Classs!.ClassLabel,
                CurrentClass = hisClass.ToString(),
                AdmisionNo = details.IdNo,
                StudentLevelPaymentId = eachStudent.Id,
                ClassCategoryStatus = classCategory,
                CurrentTermPaymentStatus = paymentStatus,
            };
        }

        private static async Task<string> BuildPaymentIdAsync(
            string? rawPaymentId,
            PaymentType paymentType,
            IGenericRepository<StudentTyTermlyFeePayment> paymentRepo,
            CancellationToken ct)
        {
            var paymentId = rawPaymentId?.Trim() ?? string.Empty;
            if (paymentId == "Auto" || string.IsNullOrEmpty(paymentId))
            {
                return "CA" + await PaymentIdGenAsync(8, paymentRepo, ct);
            }

            return paymentType switch
            {
                PaymentType.Cash => "CA" + paymentId,
                PaymentType.Teller => "TE" + paymentId,
                _ => "TR" + paymentId,
            };
        }

        private static async Task<string> PaymentIdGenAsync(
            int codeLength,
            IGenericRepository<StudentTyTermlyFeePayment> paymentRepo,
            CancellationToken ct)
        {
            const string chars = "123456789ABCDEFGHJKLMNPQRSTUVWXYabdeghnrt";
            var random = Random.Shared;

            while (true)
            {
                var code = new string(Enumerable.Range(0, codeLength)
                    .Select(_ => chars[random.Next(chars.Length)])
                    .ToArray());

                var existing = await paymentRepo.FindAsync(t => t.PaymentId == "CA" + code, ct);
                if (existing == null)
                {
                    return code;
                }
            }
        }

        /// <inheritdoc cref="IAccountingService.GetExecutiveReportDefaultsAsync" />
        public async Task<AccountingExecutiveReportDefaultsDto> GetExecutiveReportDefaultsAsync(
            string userId,
            CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var sessions = (await _uow.Repository<TxAcademicSession>().WhereAsync(
                    s => s.SchoolInfoId == schoolId,
                    ct))
                .OrderByDescending(s => s.Session)
                .Select(s => new SelectListItem { Text = s.Session, Value = s.Id })
                .ToList();

            var result = new AccountingExecutiveReportDefaultsDto { Sessions = sessions };

            // Legacy StudentFeesController.ExecutiveReport Select/-1 reorder.
            if (sessions.Count == 1)
            {
                var only = sessions[0];
                result.Sessions = new List<SelectListItem>
                {
                    new() { Text = "Select", Value = "-1" },
                    only,
                };
            }
            else if (sessions.Count > 1)
            {
                result.Terms = (await GetExecutiveReportTermsAsync(sessions[0].Value, ct)).ToList();
            }

            return result;
        }

        /// <inheritdoc cref="IAccountingService.GetExecutiveReportTermsAsync" />
        public async Task<IList<SelectListItem>> GetExecutiveReportTermsAsync(
            string sessionId,
            CancellationToken ct = default)
        {
            var terms = (await GetTermsDropdownForSessionAsync(sessionId, ct)).ToList();
            if (terms.Count == 1)
            {
                var only = terms[0];
                return new List<SelectListItem>
                {
                    new() { Text = "Select", Value = "-1" },
                    only,
                };
            }

            return terms;
        }

        /// <inheritdoc cref="IAccountingService.GetTermlyReportByTermIdAsync" />
        public async Task<TermlyPaymentReportDto> GetTermlyReportByTermIdAsync(
            string userId,
            string selectedTermId,
            CancellationToken ct = default)
        {
            var empty = new TermlyPaymentReportDto
            {
                TermlyReportOfReturning = new List<TermlyReportOfReturningDto>(),
                PaymentTypeTotal = new List<PaymentTypeTotalDto>(),
            };

            if (string.IsNullOrWhiteSpace(selectedTermId) || selectedTermId == "-1")
            {
                return empty;
            }

            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var currentFeesObject = await _uow.Repository<SetSchoolFeesTr>().FindAsync(
                d => d.SchoolId == schoolId && d.YearlyAndTermlyStatusId == selectedTermId,
                ct,
                x => x.YearlyAndTermlyStatus!.Session!);

            if (currentFeesObject?.YearlyAndTermlyStatus?.Session == null)
            {
                return empty;
            }

            var termAndSession =
                currentFeesObject.YearlyAndTermlyStatus.TermStatus + ", " +
                currentFeesObject.YearlyAndTermlyStatus.Session.Session;

            // Legacy early-exit: no payments for the term → empty report (even if students exist).
            var currentTermListOfStudentsPayment = (await _uow.Repository<StudentTyTermlyFeePayment>().WhereAsync(
                x => x.StudentAcademicProgressHistory!.YearlyAndTermlyStatusId == selectedTermId
                     && x.StudentAcademicProgressHistory.StudentDetails!.SchoolInfoId == schoolId,
                ct,
                d => d.StudentAcademicProgressHistory!.StudentDetails!.ClassArm!.Classs!,
                z => z.StudentAcademicProgressHistory!.StudentDetails!.ClassArm!,
                x => x.StudentAcademicProgressHistory!.StudentDetails!.User!)).ToList();

            if (currentTermListOfStudentsPayment.Count == 0)
            {
                return empty;
            }

            var listOfStudentDuringTheSelectedTerm =
                (await _uow.Repository<StudentAcademicTxProgressHistory>().WhereAsync(
                    x => x.YearlyAndTermlyStatusId == selectedTermId
                         && x.StudentDetails!.SchoolInfoId == schoolId,
                    ct,
                    y => y.StudentDetails!.User!,
                    g => g.StudentDetails!.ClassArm!.Classs!,
                    z => z.Classs!,
                    v => v.YearlyAndTermlyStatus!.Session!,
                    p => p.StudentTermlyFeePayment)).ToList();

            var discounts = (await _uow.Repository<StudentTyTermlyFixedPaymentDiscount>().WhereAsync(
                x => x.StudentAcademicProgressHistory!.YearlyAndTermlyStatusId == selectedTermId,
                ct,
                v => v.StudentAcademicProgressHistory!)).ToList();

            var freshBuckets = CreateCategoryBuckets();
            var returnBuckets = CreateCategoryBuckets();

            double cashPaymentTypeTotal = 0;
            double tellerPaymentTypeTotal = 0;
            double transferPaymentTypeTotal = 0;

            var paymentsByProgressId = currentTermListOfStudentsPayment
                .GroupBy(x => x.StudentAcademicProgressHistoryId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var eachStudent in listOfStudentDuringTheSelectedTerm)
            {
                var details = eachStudent.StudentDetails;
                if (details?.ClassArm?.Classs == null || details.User == null)
                {
                    continue;
                }

                var hisClass = details.ClassArm.Classs.ClassName;
                var classCategory = ResolveClassCategory(hisClass, details.ClassCategory);
                var anyDiscountForCurrentStudent = discounts
                    .FirstOrDefault(x =>
                        x.StudentAcademicProgressHistory != null
                        && x.StudentAcademicProgressHistory.YearlyAndTermlyStatusId == selectedTermId
                        && x.StudentAcademicProgressHistory.StudentId == eachStudent.StudentId)
                    ?.DiscountAmount ?? 0;

                var isFresher = details.AdmisnTermId == selectedTermId
                    && details.StudentInitialRegStatus == StudentInitialRegistrationStatus.Fresher;

                paymentsByProgressId.TryGetValue(eachStudent.Id, out var currentStudentPaymentList);
                currentStudentPaymentList ??= new List<StudentTyTermlyFeePayment>();

                if (isFresher)
                {
                    var hisFees = GetFeesForFreshiers(currentFeesObject, classCategory);

                    if (currentStudentPaymentList.Count > 0)
                    {
                        var currentStudentTotalPayment = currentStudentPaymentList.Sum(b => b.Amount);
                        AccumulatePaymentTypeTotals(
                            currentStudentPaymentList,
                            ref cashPaymentTypeTotal,
                            ref tellerPaymentTypeTotal,
                            ref transferPaymentTypeTotal);

                        var detailStudent = currentStudentPaymentList[0].StudentAcademicProgressHistory?.StudentDetails
                            ?? details;
                        var detailClass = detailStudent.ClassArm?.Classs;
                        if (detailClass == null || detailStudent.User == null || detailStudent.ClassArm == null)
                        {
                            continue;
                        }

                        var bucket = freshBuckets[classCategory];
                        if (Math.Abs((currentStudentTotalPayment + anyDiscountForCurrentStudent) - hisFees) < 0.5)
                        {
                            RecordCleared(
                                bucket,
                                classCategory,
                                currentStudentTotalPayment,
                                anyDiscountForCurrentStudent,
                                BuildDetailEntry(
                                    detailStudent.IdNo,
                                    detailStudent.User.FullName,
                                    detailClass.ClassLabel,
                                    detailStudent.ClassArm.ArmName,
                                    detailClass.ClassName,
                                    hisFees + "-" + anyDiscountForCurrentStudent));
                        }
                        else if (currentStudentTotalPayment < hisFees)
                        {
                            RecordPartlyCleared(
                                bucket,
                                classCategory,
                                currentStudentTotalPayment,
                                hisFees - currentStudentTotalPayment,
                                BuildDetailEntry(
                                    detailStudent.IdNo,
                                    detailStudent.User.FullName,
                                    detailClass.ClassLabel,
                                    detailStudent.ClassArm.ArmName,
                                    detailClass.ClassName,
                                    currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)));
                        }
                    }
                    else
                    {
                        // Fresher with none payment — outstanding uses category fee table amounts.
                        var unclearFee = GetFeesForFreshiers(currentFeesObject, classCategory);
                        RecordUncleared(
                            freshBuckets[classCategory],
                            classCategory,
                            unclearFee,
                            BuildDetailEntry(
                                details.IdNo,
                                details.User.FullName,
                                details.ClassArm.Classs.ClassLabel,
                                details.ClassArm.ArmName,
                                details.ClassArm.Classs.ClassName,
                                unclearFee.ToString(CultureInfo.CurrentCulture)));
                    }
                }
                else
                {
                    // Returning student
                    if (currentStudentPaymentList.Count > 0)
                    {
                        AccumulatePaymentTypeTotals(
                            currentStudentPaymentList,
                            ref cashPaymentTypeTotal,
                            ref tellerPaymentTypeTotal,
                            ref transferPaymentTypeTotal);

                        var detailStudent = currentStudentPaymentList[0].StudentAcademicProgressHistory?.StudentDetails
                            ?? details;
                        var detailClass = detailStudent.ClassArm?.Classs;
                        if (detailClass == null || detailStudent.User == null || detailStudent.ClassArm == null)
                        {
                            continue;
                        }

                        classCategory = ResolveClassCategory(hisClass, detailStudent.ClassCategory);
                        var hisFees = GetFeesForReturningStudent(currentFeesObject, classCategory);
                        var currentStudentTotalPayment = currentStudentPaymentList.Sum(b => b.Amount);
                        var bucket = returnBuckets[classCategory];

                        if (Math.Abs((currentStudentTotalPayment + anyDiscountForCurrentStudent) - hisFees) < 0.7)
                        {
                            RecordCleared(
                                bucket,
                                classCategory,
                                currentStudentTotalPayment,
                                anyDiscountForCurrentStudent,
                                BuildDetailEntry(
                                    detailStudent.IdNo,
                                    detailStudent.User.FullName,
                                    detailClass.ClassLabel,
                                    detailStudent.ClassArm.ArmName,
                                    detailClass.ClassName,
                                    hisFees + "-" + anyDiscountForCurrentStudent));
                        }
                        else if (currentStudentTotalPayment < hisFees)
                        {
                            RecordPartlyCleared(
                                bucket,
                                classCategory,
                                currentStudentTotalPayment,
                                hisFees - currentStudentTotalPayment,
                                BuildDetailEntry(
                                    detailStudent.IdNo,
                                    detailStudent.User.FullName,
                                    detailClass.ClassLabel,
                                    detailStudent.ClassArm.ArmName,
                                    detailClass.ClassName,
                                    currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)));
                        }
                    }
                    else
                    {
                        // Returning with none payment.
                        // Legacy PlayClass branch keys off StudentDetails.ClassCategory == PlayClass;
                        // other categories use resolved classCatStatus.
                        ClassCategoryStatus classCatStatus;
                        double hisSchoolFees;
                        if (hisClass is not (CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3))
                        {
                            classCatStatus = GetClassCategoryStatus(hisClass);
                            hisSchoolFees = GetFeesForReturningStudent(currentFeesObject, classCatStatus);
                        }
                        else
                        {
                            classCatStatus = details.ClassCategory;
                            hisSchoolFees = GetFeesForReturningStudent(currentFeesObject, classCatStatus);
                        }

                        var bucketKey = details.ClassCategory == ClassCategoryStatus.PlayClass
                            ? ClassCategoryStatus.PlayClass
                            : classCatStatus;

                        if (!returnBuckets.ContainsKey(bucketKey) || bucketKey == ClassCategoryStatus.None)
                        {
                            continue;
                        }

                        RecordUncleared(
                            returnBuckets[bucketKey],
                            classCatStatus,
                            hisSchoolFees,
                            BuildDetailEntry(
                                details.IdNo,
                                details.User.FullName,
                                details.ClassArm.Classs.ClassLabel,
                                details.ClassArm.ArmName,
                                details.ClassArm.Classs.ClassName,
                                hisSchoolFees.ToString(CultureInfo.CurrentCulture)));
                    }
                }
            }

            var rows = new List<TermlyReportOfReturningDto>();
            foreach (var category in ReportCategoryOrder)
            {
                if (freshBuckets.TryGetValue(category, out var fresh) && fresh.HasData)
                {
                    rows.Add(ToReportRow(
                        StudentStatus.Fresher,
                        fresh,
                        GetFeesForFreshiers(currentFeesObject, category),
                        termAndSession));
                }

                if (returnBuckets.TryGetValue(category, out var retn) && retn.HasData)
                {
                    rows.Add(ToReportRow(
                        StudentStatus.Returning,
                        retn,
                        GetFeesForReturningStudent(currentFeesObject, category),
                        termAndSession));
                }
            }

            return new TermlyPaymentReportDto
            {
                TermlyReportOfReturning = rows,
                PaymentTypeTotal = new List<PaymentTypeTotalDto>
                {
                    new() { PaymentType = PaymentType.Cash, TotalPayment = cashPaymentTypeTotal },
                    new() { PaymentType = PaymentType.Teller, TotalPayment = tellerPaymentTypeTotal },
                    new() { PaymentType = PaymentType.Transfer, TotalPayment = transferPaymentTypeTotal },
                },
            };
        }

        /// <inheritdoc cref="IAccountingService.GetStudentByParameterAsync" />
        public async Task<List<StudentFieldExtraction4Accounting2ndDto>> GetStudentByParameterAsync(
            string userId,
            string searchParameter,
            CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var raw = (searchParameter ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<StudentFieldExtraction4Accounting2ndDto>();
            }

            List<TxStudentDetail> matches;
            if (raw.Contains('/'))
            {
                matches = (await _uow.Repository<TxStudentDetail>().WhereAsync(
                    t => t.SchoolInfoId == schoolId && t.IdNo == raw,
                    ct,
                    t => t.User!,
                    x => x.ClassArm!.Classs!)).ToList();
            }
            else
            {
                var needle = raw.ToLowerInvariant();
                matches = (await _uow.Repository<TxStudentDetail>().WhereAsync(
                    t => t.SchoolInfoId == schoolId
                         && t.User != null
                         && (
                             (t.User.UserName != null && t.User.UserName.Trim().ToLower().Contains(needle))
                             || (t.User.FirstName != null && t.User.FirstName.Trim().ToLower().Contains(needle))
                             || (t.User.MiddleName != null && t.User.MiddleName.Trim().ToLower().Contains(needle))
                             || (t.User.SurName != null && t.User.SurName.Trim().ToLower().Contains(needle))),
                    ct,
                    t => t.User!,
                    x => x.ClassArm!.Classs!)).ToList();
            }

            return matches
                .Where(x => x.User != null && x.ClassArm?.Classs != null)
                .Select(x => new StudentFieldExtraction4Accounting2ndDto
                {
                    FullName = x.User!.FullName,
                    ArmName = x.ClassArm!.ArmName,
                    ClassLabel = x.ClassArm.Classs!.ClassLabel,
                    CurrentClass = x.ClassArm.Classs.ClassName,
                    AdmisionNo = x.IdNo,
                    StudentLevelPaymentId = x.Id,
                    ClassCategoryStatus = x.ClassCategory,
                    StudentStatus = x.Status,
                })
                .ToList();
        }

        /// <inheritdoc cref="IAccountingService.GetSelectedStudentPaymentHistoryByIdAsync" />
        public async Task<List<StudentAcademicProgressHistoryStrokPaymentDto>> GetSelectedStudentPaymentHistoryByIdAsync(
            string userId,
            string studentId,
            CancellationToken ct = default)
        {
            // Legacy RegistrationService.GetSelectedStudentPaymentHistoryById — preserve structure exactly
            // (including subsequent-term discount lookup against the initial registration progress id).
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);

            var feePaymentDiscount = (await _uow.Repository<StudentTyTermlyFixedPaymentDiscount>().WhereAsync(
                z => z.StudentAcademicProgressHistory != null
                     && z.StudentAcademicProgressHistory.StudentId == studentId,
                ct,
                z => z.StudentAcademicProgressHistory!)).ToList();

            var feesCollection = (await _uow.Repository<SetSchoolFeesTr>().WhereAsync(
                s => s.SchoolId == schoolId,
                ct,
                v => v.YearlyAndTermlyStatus!.Session!)).ToList();

            var terminalFeesDetails = new List<StudentAcademicProgressHistoryStrokPaymentDto>();
            var paymentList = (await _uow.Repository<StudentTyTermlyFeePayment>().WhereAsync(
                d => d.StudentAcademicProgressHistory != null
                     && d.StudentAcademicProgressHistory.StudentId == studentId,
                ct,
                s => s.StudentAcademicProgressHistory!.StudentDetails!.User!,
                x => x.StudentAcademicProgressHistory!.YearlyAndTermlyStatus!.Session!,
                g => g.StudentAcademicProgressHistory!.Classs!)).ToList();

            if (paymentList.Count == 0)
            {
                return terminalFeesDetails;
            }

            var initialFeePaymentObject = paymentList
                .OrderBy(d => d.StudentAcademicProgressHistory!.YearlyAndTermlyStatusId)
                .First();
            var initialTermId = initialFeePaymentObject.StudentAcademicProgressHistory!.YearlyAndTermlyStatusId;
            var actualFeeModel = feesCollection.Find(x => x.YearlyAndTermlyStatusId == initialTermId);
            double initialFeeActualPayment = 0;
            var studentInitialRegStatus =
                initialFeePaymentObject.StudentAcademicProgressHistory.StudentDetails!.StudentInitialRegStatus;
            var classss = initialFeePaymentObject.StudentAcademicProgressHistory.Classs!.ClassName;

            if (actualFeeModel != null)
            {
                if (studentInitialRegStatus == StudentInitialRegistrationStatus.Fresher)
                {
                    if (classss is not (CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3))
                    {
                        var className3 = GetClassCategoryStatus(
                            paymentList.First().StudentAcademicProgressHistory!.Classs!.ClassName);
                        initialFeeActualPayment = GetFeesForFreshiers(actualFeeModel, className3);
                    }
                    else
                    {
                        initialFeeActualPayment = GetFeesForFreshiers(
                            actualFeeModel,
                            paymentList.First().StudentAcademicProgressHistory!.StudentDetails!.ClassCategory);
                    }
                }
                else
                {
                    if (classss is not (CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3))
                    {
                        var className3 = GetClassCategoryStatus(
                            paymentList.First().StudentAcademicProgressHistory!.Classs!.ClassName);
                        initialFeeActualPayment = GetFeesForReturningStudent(actualFeeModel, className3);
                    }
                    else
                    {
                        initialFeeActualPayment = GetFeesForReturningStudent(
                            actualFeeModel,
                            paymentList.First().StudentAcademicProgressHistory!.StudentDetails!.ClassCategory);
                    }
                }
            }

            var numberOfRegistrationPartPayment = paymentList
                .Where(c => c.StudentAcademicProgressHistory!.YearlyAndTermlyStatusId == initialTermId)
                .ToList();
            var currentAcademicProgressId =
                numberOfRegistrationPartPayment.First().StudentAcademicProgressHistoryId;

            var discount = feePaymentDiscount
                .Find(v => v.StudentAcademicProgressId == currentAcademicProgressId)
                ?.DiscountAmount;
            var discountValue = discount;

            var dateCulture = CultureInfo.GetCultureInfo("en-GB");
            terminalFeesDetails.Add(new StudentAcademicProgressHistoryStrokPaymentDto
            {
                StudentAcademicLevelId = initialFeePaymentObject.StudentAcademicProgressHistory.StudentId,
                TermStatus = initialFeePaymentObject.StudentAcademicProgressHistory.YearlyAndTermlyStatus!.TermStatus,
                Session = initialFeePaymentObject.StudentAcademicProgressHistory.YearlyAndTermlyStatus.Session!.Session,
                ActualFees = initialFeeActualPayment,
                Discount = discountValue ?? 0,
                StudentTermlyFeesHistory = numberOfRegistrationPartPayment
                    .Select(z => new StudentTermlyFeesPaymentHistoryDto
                    {
                        StudentAcademicLevelId = z.StudentAcademicProgressHistoryId,
                        PaymentType = z.PaymentType,
                        PaymentDate = z.PaymentDate.ToString("D", dateCulture),
                        AmountPaid = z.Amount,
                    })
                    .ToList(),
            });

            foreach (var eachPayment in numberOfRegistrationPartPayment)
            {
                paymentList.Remove(eachPayment);
            }

            if (paymentList.Count > 0)
            {
                var groupPaymentList = paymentList.GroupBy(
                    g => g.StudentAcademicProgressHistory!.YearlyAndTermlyStatusId);

                foreach (var eachPayment in groupPaymentList)
                {
                    actualFeeModel = feesCollection.Find(
                        x => x.YearlyAndTermlyStatusId
                             == eachPayment.First().StudentAcademicProgressHistory!.YearlyAndTermlyStatusId);
                    var actualFeesPayment = 0d;
                    classss = eachPayment.First().StudentAcademicProgressHistory!.Classs!.ClassName;
                    if (actualFeeModel != null)
                    {
                        if (classss is not (CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3))
                        {
                            var className3 = GetClassCategoryStatus(classss);
                            actualFeesPayment = GetFeesForReturningStudent(actualFeeModel, className3);
                        }
                        else
                        {
                            actualFeesPayment = GetFeesForReturningStudent(
                                actualFeeModel,
                                eachPayment.First().StudentAcademicProgressHistory!.StudentDetails!.ClassCategory);
                        }
                    }

                    var eachPaymentToList = eachPayment.ToList();
                    // Legacy: subsequent terms still look up discount with initial registration progress id.
                    discount = feePaymentDiscount
                        .Find(v => v.StudentAcademicProgressId == currentAcademicProgressId)
                        ?.DiscountAmount ?? 0;
                    discountValue = discount;
                    terminalFeesDetails.Add(new StudentAcademicProgressHistoryStrokPaymentDto
                    {
                        StudentAcademicLevelId =
                            eachPaymentToList.First().StudentAcademicProgressHistory!.StudentId,
                        TermStatus = eachPaymentToList.First()
                            .StudentAcademicProgressHistory!.YearlyAndTermlyStatus!.TermStatus,
                        Session = eachPaymentToList.First()
                            .StudentAcademicProgressHistory!.YearlyAndTermlyStatus!.Session!.Session,
                        ActualFees = actualFeesPayment,
                        Discount = discountValue ?? 0,
                        StudentTermlyFeesHistory = eachPaymentToList
                            .Select(z => new StudentTermlyFeesPaymentHistoryDto
                            {
                                StudentAcademicLevelId = z.StudentAcademicProgressHistoryId,
                                PaymentType = z.PaymentType,
                                PaymentDate = z.PaymentDate.ToString("D", dateCulture),
                                AmountPaid = z.Amount,
                            })
                            .ToList(),
                    });
                }
            }

            return terminalFeesDetails;
        }

        /// <inheritdoc cref="IAccountingService.CreateOptionalFeesCategoryAsync" />
        public async Task<AccountingCreateOptionalFeesCategoryResultDto> CreateOptionalFeesCategoryAsync(
            string userId,
            string categoryName,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.CreateOptionalFeesCategory
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var name = (categoryName ?? string.Empty).Trim();

            var schoolCategories = (await _uow.Repository<XyOPtionalChargeCategory>().WhereAsync(
                x => x.SchoolId == schoolId,
                ct)).ToList();

            var existing = schoolCategories.FirstOrDefault(x =>
                string.Equals(
                    (x.CategoryName ?? string.Empty).Trim(),
                    name,
                    StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                return new AccountingCreateOptionalFeesCategoryResultDto
                {
                    Succeeded = false,
                    Message = "Failed! This category already exist.",
                };
            }

            var entity = new XyOPtionalChargeCategory
            {
                Id = Guid.NewGuid().ToString(),
                SchoolId = schoolId,
                CategoryName = name,
            };
            _uow.Repository<XyOPtionalChargeCategory>().Add(entity);
            await _uow.SaveChangesAsync(ct);

            return new AccountingCreateOptionalFeesCategoryResultDto
            {
                Succeeded = true,
                Message = "Category has successfully been created.",
            };
        }

        /// <inheritdoc cref="IAccountingService.CreateIncomeCategoryAsync" />
        public async Task<AccountingCreateIncomeCategoryResultDto> CreateIncomeCategoryAsync(
            string userId,
            string categoryName,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.CreateIncomeCategory
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var name = (categoryName ?? string.Empty).Trim();

            var schoolCategories = (await _uow.Repository<XyIncomeCategory>().WhereAsync(
                x => x.SchoolId == schoolId,
                ct)).ToList();

            var existing = schoolCategories.FirstOrDefault(x =>
                string.Equals(
                    (x.CategoryName ?? string.Empty).Trim(),
                    name,
                    StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                return new AccountingCreateIncomeCategoryResultDto
                {
                    Succeeded = false,
                    Message = "Failed! This category already exist.",
                };
            }

            var entity = new XyIncomeCategory
            {
                Id = Guid.NewGuid().ToString(),
                SchoolId = schoolId,
                CategoryName = name,
            };
            _uow.Repository<XyIncomeCategory>().Add(entity);
            await _uow.SaveChangesAsync(ct);

            return new AccountingCreateIncomeCategoryResultDto
            {
                Succeeded = true,
                Message = "Category has successfully been created.",
            };
        }

        /// <inheritdoc cref="IAccountingService.CreateExpenditureCategoryAsync" />
        public async Task<AccountingCreateExpenditureCategoryResultDto> CreateExpenditureCategoryAsync(
            string userId,
            string categoryName,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.CreateExpenditureCategory
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var name = (categoryName ?? string.Empty).Trim();

            var schoolCategories = (await _uow.Repository<XyExpensesCategory>().WhereAsync(
                x => x.SchoolId == schoolId,
                ct)).ToList();

            var existing = schoolCategories.FirstOrDefault(x =>
                string.Equals(
                    (x.CategoryName ?? string.Empty).Trim(),
                    name,
                    StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                return new AccountingCreateExpenditureCategoryResultDto
                {
                    Succeeded = false,
                    Message = "Failed! This category already exist.",
                };
            }

            var entity = new XyExpensesCategory
            {
                Id = Guid.NewGuid().ToString(),
                SchoolId = schoolId,
                CategoryName = name,
            };
            _uow.Repository<XyExpensesCategory>().Add(entity);
            await _uow.SaveChangesAsync(ct);

            return new AccountingCreateExpenditureCategoryResultDto
            {
                Succeeded = true,
                Message = "Category has successfully been created.",
            };
        }

        /// <inheritdoc cref="IAccountingService.GetRegisterIncomeFormAsync" />
        public async Task<AccountingRegisterIncomeFormDto> GetRegisterIncomeFormAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy EmployeeIncomeController.RegisterIncome GET
            var categories = await GetIncomeCategoryListAsync(userId, ct);
            var today = DateTime.Now;
            var weekOfMonth = EnumsAndExtensionMethods.GetWeekOfMonth(today);
            var weekPosition = weekOfMonth switch
            {
                1 => "1st",
                2 => "2nd",
                3 => "3rd",
                _ => $"{weekOfMonth}th",
            };

            if (categories.Count == 0)
            {
                return new AccountingRegisterIncomeFormDto
                {
                    HasCategories = false,
                    Message = "Sorry! one or more income category needs to be created first.",
                    CapturedDate = today,
                    DateDisplay = today.ToLongDateString(),
                    WeekPosition = weekPosition,
                };
            }

            return new AccountingRegisterIncomeFormDto
            {
                HasCategories = true,
                CapturedDate = today,
                DateDisplay = today.ToLongDateString(),
                WeekPosition = weekPosition,
                Categories = categories
                    .Select(v => new SelectListItem
                    {
                        Value = v.Id,
                        Text = v.CategoryName,
                    })
                    .ToList(),
            };
        }

        /// <inheritdoc cref="IAccountingService.CreateIncomeAsync" />
        public async Task<AccountingCreateIncomeResultDto> CreateIncomeAsync(
            string userId,
            AccountingRegisterIncomeDto model,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.CreateIncome
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            model ??= new AccountingRegisterIncomeDto();

            var capturedLocal = model.CapturedDate == default ? DateTime.Now : model.CapturedDate;
            var capturedDate = new DateTime(
                capturedLocal.Year,
                capturedLocal.Month,
                capturedLocal.Day,
                0,
                0,
                0,
                DateTimeKind.Utc);

            var actualWeek = EnumsAndExtensionMethods.GetWeekOfMonth(capturedLocal);
            var weekOfMonth = MapWeekOfMonth(actualWeek);

            var schoolIncomes = await _uow.Repository<XyIncome>().GetAllAsync(
                Filter: c => c.Week != null && c.Week.SchoolId == schoolId,
                IncludeProperties: "Week",
                cancellationToken: ct);

            var checkIfItsNewWeek = schoolIncomes.FirstOrDefault(c =>
                c.CapturedDate.Month == capturedDate.Month
                && c.CapturedDate.Year == capturedDate.Year
                && c.Week != null
                && c.Week.WeekOfMonth == weekOfMonth
                && c.Week.TimeStatus == TimeStatus.Current);

            if (checkIfItsNewWeek != null)
            {
                var weekId = schoolIncomes
                    .FirstOrDefault(c =>
                        c.CapturedDate.Month == capturedDate.Month
                        && c.CapturedDate.Year == capturedDate.Year
                        && c.Week != null
                        && c.Week.TimeStatus == TimeStatus.Current)
                    ?.WeekId;

                if (string.IsNullOrWhiteSpace(weekId))
                {
                    return new AccountingCreateIncomeResultDto
                    {
                        Status = 1,
                        Message =
                            "Sorry! You have already entered and closed Income entering within the selected period.",
                    };
                }

                await InsertIncomeAsync(
                    userId,
                    weekId,
                    model.ItemCategoryId,
                    model.Details,
                    model.Cost,
                    capturedDate,
                    ct);

                return new AccountingCreateIncomeResultDto
                {
                    Status = 0,
                    Message = "The item has successfully been saved.",
                };
            }

            var isLastWeekClosed = schoolIncomes.FirstOrDefault(c =>
                c.Week != null && c.Week.TimeStatus == TimeStatus.Current);

            if (isLastWeekClosed != null)
            {
                return new AccountingCreateIncomeResultDto
                {
                    Status = 2,
                    Message =
                        "Failed! You need to first confirm the list of recent Income before you can add new one.",
                };
            }

            var weekModel = new XyIncomeWeek
            {
                Id = Guid.NewGuid().ToString(),
                WeekOfMonth = weekOfMonth,
                TimeStatus = TimeStatus.Current,
                SchoolId = schoolId,
            };
            _uow.Repository<XyIncomeWeek>().Add(weekModel);
            await _uow.SaveChangesAsync(ct);

            var currentWeek = await _uow.Repository<XyIncomeWeek>().FindAsync(
                c => c.SchoolId == schoolId && c.TimeStatus == TimeStatus.Current,
                ct);
            var newWeekId = currentWeek?.Id ?? weekModel.Id;

            await InsertIncomeAsync(
                userId,
                newWeekId,
                model.ItemCategoryId,
                model.Details,
                model.Cost,
                capturedDate,
                ct);

            return new AccountingCreateIncomeResultDto
            {
                Status = 0,
                Message = "The item has successfully been saved.",
            };
        }

        /// <inheritdoc cref="IAccountingService.GetRegisterExpenditureFormAsync" />
        public async Task<AccountingRegisterExpenditureFormDto> GetRegisterExpenditureFormAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy EmployeeIncomeController.RegisterExpenses GET
            var categories = await GetExpenditureCategoryListAsync(userId, ct);
            var today = DateTime.Now;
            var weekOfMonth = EnumsAndExtensionMethods.GetWeekOfMonth(today);
            var weekPosition = weekOfMonth switch
            {
                1 => "1st",
                2 => "2nd",
                3 => "3rd",
                _ => $"{weekOfMonth}th",
            };

            if (categories.Count == 0)
            {
                return new AccountingRegisterExpenditureFormDto
                {
                    HasCategories = false,
                    Message = "Sorry! one or more expenses category needs to be created first.",
                    CapturedDate = today,
                    DateDisplay = today.ToLongDateString(),
                    WeekPosition = weekPosition,
                };
            }

            return new AccountingRegisterExpenditureFormDto
            {
                HasCategories = true,
                CapturedDate = today,
                DateDisplay = today.ToLongDateString(),
                WeekPosition = weekPosition,
                Categories = categories
                    .Select(v => new SelectListItem
                    {
                        Value = v.Id,
                        Text = v.CategoryName,
                    })
                    .ToList(),
            };
        }

        /// <inheritdoc cref="IAccountingService.CreateExpenditureAsync" />
        public async Task<AccountingCreateExpenditureResultDto> CreateExpenditureAsync(
            string userId,
            AccountingRegisterExpenditureDto model,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.CreateExpenditure
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            model ??= new AccountingRegisterExpenditureDto();

            var capturedLocal = model.CapturedDate == default ? DateTime.Now : model.CapturedDate;
            var capturedDate = new DateTime(
                capturedLocal.Year,
                capturedLocal.Month,
                capturedLocal.Day,
                0,
                0,
                0,
                DateTimeKind.Utc);

            var actualWeek = EnumsAndExtensionMethods.GetWeekOfMonth(capturedLocal);
            var weekOfMonth = MapWeekOfMonth(actualWeek);

            var schoolExpenses = await _uow.Repository<XyExpenses>().GetAllAsync(
                Filter: c => c.Week != null && c.Week.SchoolId == schoolId,
                IncludeProperties: "Week",
                cancellationToken: ct);

            var checkIfItsNewWeek = schoolExpenses.FirstOrDefault(c =>
                c.CapturedDate.Month == capturedDate.Month
                && c.CapturedDate.Year == capturedDate.Year
                && c.Week != null
                && c.Week.WeekOfMonth == weekOfMonth
                && c.Week.TimeStatus == TimeStatus.Current);

            if (checkIfItsNewWeek != null)
            {
                var weekId = schoolExpenses
                    .FirstOrDefault(c =>
                        c.CapturedDate.Month == capturedDate.Month
                        && c.CapturedDate.Year == capturedDate.Year
                        && c.Week != null
                        && c.Week.TimeStatus == TimeStatus.Current)
                    ?.WeekId;

                if (string.IsNullOrWhiteSpace(weekId))
                {
                    return new AccountingCreateExpenditureResultDto
                    {
                        Status = 1,
                        Message =
                            "Sorry! You have already entered and closed expenditure entering within the selected period.",
                    };
                }

                await InsertExpenseAsync(
                    userId,
                    weekId,
                    model.ItemCategoryId,
                    model.Details,
                    model.Cost,
                    capturedDate,
                    ct);

                return new AccountingCreateExpenditureResultDto
                {
                    Status = 0,
                    Message = "The item has successfully been saved.",
                };
            }

            var isLastWeekClosed = schoolExpenses.FirstOrDefault(c =>
                c.Week != null && c.Week.TimeStatus == TimeStatus.Current);

            if (isLastWeekClosed != null)
            {
                return new AccountingCreateExpenditureResultDto
                {
                    Status = 2,
                    Message =
                        "Failed! You need to first confirm the list of recent expenses before you can add new one.",
                };
            }

            var weekModel = new XyExpenditureWeek
            {
                Id = Guid.NewGuid().ToString(),
                WeekOfMonth = weekOfMonth,
                TimeStatus = TimeStatus.Current,
                SchoolId = schoolId,
            };
            _uow.Repository<XyExpenditureWeek>().Add(weekModel);
            await _uow.SaveChangesAsync(ct);

            var currentWeek = await _uow.Repository<XyExpenditureWeek>().FindAsync(
                c => c.SchoolId == schoolId && c.TimeStatus == TimeStatus.Current,
                ct);
            var newWeekId = currentWeek?.Id ?? weekModel.Id;

            await InsertExpenseAsync(
                userId,
                newWeekId,
                model.ItemCategoryId,
                model.Details,
                model.Cost,
                capturedDate,
                ct);

            return new AccountingCreateExpenditureResultDto
            {
                Status = 0,
                Message = "The item has successfully been saved.",
            };
        }

        /// <inheritdoc cref="IAccountingService.GetAllEmployeesAsync" />
        public async Task<List<AccountingEmployeeListItemDto>> GetAllEmployeesAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.GetAllEmployees
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var employees = await _uow.Repository<TxEmployee>().GetAllAsync(
                Filter: v => v.SchoolId == schoolId && v.TimeStatus != TimeStatus.Previous,
                IncludeProperties: "User",
                cancellationToken: ct);

            return employees
                .Select(x => new AccountingEmployeeListItemDto
                {
                    Id = x.Id,
                    FullName = x.User?.FullName ?? string.Empty,
                    EmployeeStatus = x.EmployeeStatus,
                    EmployeeStatusLabel = x.EmployeeStatus.ToString(),
                    EmployeeIncomeStructureStatus = x.EmployeeIncomeStructureStatus,
                })
                .OrderBy(x => x.FullName)
                .ToList();
        }

        /// <inheritdoc cref="IAccountingService.GetSetIncomeStructureFormAsync" />
        public async Task<AccountingEmployeeIncomeStructureDto?> GetSetIncomeStructureFormAsync(
            string userId,
            string employeeId,
            CancellationToken ct = default)
        {
            // Legacy EmployeeIncomeController.SetIncomeStructure GET
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                return null;
            }

            var employee = await _uow.Repository<TxEmployee>().FindAsync(
                x => x.Id == employeeId && x.SchoolId == schoolId,
                ct,
                x => x.User!);
            if (employee == null)
            {
                return null;
            }

            return new AccountingEmployeeIncomeStructureDto
            {
                Id = string.Empty,
                EmployeeId = employee.Id,
                FullName = employee.User?.FullName ?? string.Empty,
                EmployeeQualificationStatus = EmployeeQualificationStatus.DegreeInEducation,
                BasicSalary = 0,
                Allowance = 0,
                Qualifications = BuildAllQualificationOptions(),
            };
        }

        /// <inheritdoc cref="IAccountingService.GetEmployeeIncomeStructureAsync" />
        public async Task<AccountingEmployeeIncomeStructureDto?> GetEmployeeIncomeStructureAsync(
            string userId,
            string employeeId,
            CancellationToken ct = default)
        {
            // Legacy GetEmployeeByIdNo + QualificationStaus
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                return null;
            }

            var employee = await _uow.Repository<TxEmployee>().FindAsync(
                x => x.Id == employeeId && x.SchoolId == schoolId,
                ct,
                x => x.User!,
                x => x.EmployeeIncomeStructure!);
            if (employee?.EmployeeIncomeStructure == null)
            {
                return null;
            }

            var structure = employee.EmployeeIncomeStructure;
            return new AccountingEmployeeIncomeStructureDto
            {
                Id = structure.Id,
                EmployeeId = employee.Id,
                FullName = employee.User?.FullName ?? string.Empty,
                EmployeeQualificationStatus = employee.EmployeeQualificationStatus,
                BasicSalary = structure.BasicSalary,
                Allowance = structure.Allowance,
                Qualifications = BuildQualificationOptions(employee.EmployeeQualificationStatus),
            };
        }

        /// <inheritdoc cref="IAccountingService.CreateEmployeeStructureAsync" />
        public async Task<AccountingEmployeeIncomeStructureMutationResultDto> CreateEmployeeStructureAsync(
            string userId,
            AccountingEmployeeIncomeStructureDto model,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.CreateEmployeeStructure
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            model ??= new AccountingEmployeeIncomeStructureDto();
            var employeeId = string.IsNullOrWhiteSpace(model.EmployeeId) ? model.Id : model.EmployeeId;
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                return new AccountingEmployeeIncomeStructureMutationResultDto
                {
                    Status = 0,
                    Message = "Failed! Subject Name exist against another record.",
                };
            }

            var employee = await _uow.Repository<TxEmployee>().FindAsync(
                x => x.Id == employeeId && x.SchoolId == schoolId,
                ct);
            if (employee == null)
            {
                return new AccountingEmployeeIncomeStructureMutationResultDto
                {
                    Status = 0,
                    Message = "Failed! Subject Name exist against another record.",
                };
            }

            var structure = await _uow.Repository<TxEmployeeIncomeStructure>().FindAsync(
                x => x.Id == employeeId,
                ct);
            if (structure == null)
            {
                _uow.Repository<TxEmployeeIncomeStructure>().Add(new TxEmployeeIncomeStructure
                {
                    Id = employeeId,
                    BasicSalary = model.BasicSalary,
                    Allowance = model.Allowance,
                });
            }
            else
            {
                structure.BasicSalary = model.BasicSalary;
                structure.Allowance = model.Allowance;
                _uow.Repository<TxEmployeeIncomeStructure>().Update(structure);
            }

            employee.EmployeeIncomeStructureStatus = EmployeeIncomeStructureStatus.HasSet;
            employee.EmployeeQualificationStatus = model.EmployeeQualificationStatus;
            _uow.Repository<TxEmployee>().Update(employee);
            await _uow.SaveChangesAsync(ct);

            return new AccountingEmployeeIncomeStructureMutationResultDto
            {
                Status = 1,
                Message = "The changes has been succcessfully saved.",
            };
        }

        /// <inheritdoc cref="IAccountingService.DeleteIncomeStructureAsync" />
        public async Task<AccountingEmployeeIncomeStructureMutationResultDto> DeleteIncomeStructureAsync(
            string userId,
            string employeeId,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.DeleteIncomeStructure
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                return new AccountingEmployeeIncomeStructureMutationResultDto
                {
                    Status = 0,
                    Message =
                        "Failed! Payment(s) has been made upon this employee and therefore we cannot delete his payment structure.",
                };
            }

            var employee = await _uow.Repository<TxEmployee>().FindAsync(
                x => x.Id == employeeId && x.SchoolId == schoolId,
                ct);
            if (employee == null)
            {
                return new AccountingEmployeeIncomeStructureMutationResultDto
                {
                    Status = 0,
                    Message =
                        "Failed! Payment(s) has been made upon this employee and therefore we cannot delete his payment structure.",
                };
            }

            var hasSalary = await _uow.Repository<TxEmployeesMonthlyPayment>().FindAsync(
                x => x.EmployeeId == employeeId,
                ct);
            if (hasSalary != null)
            {
                return new AccountingEmployeeIncomeStructureMutationResultDto
                {
                    Status = 0,
                    Message =
                        "Failed! Payment(s) has been made upon this employee and therefore we cannot delete his payment structure.",
                };
            }

            employee.EmployeeIncomeStructureStatus = EmployeeIncomeStructureStatus.HasNotSet;
            _uow.Repository<TxEmployee>().Update(employee);

            var structure = await _uow.Repository<TxEmployeeIncomeStructure>().FindAsync(
                x => x.Id == employeeId,
                ct);
            if (structure != null)
            {
                _uow.Repository<TxEmployeeIncomeStructure>().Delete(structure);
            }

            await _uow.SaveChangesAsync(ct);

            return new AccountingEmployeeIncomeStructureMutationResultDto
            {
                Status = 1,
                Message = "Payment structure deleted successfully.",
            };
        }

        /// <inheritdoc cref="IAccountingService.GetMonthlyPaymentSlipAsync" />
        public async Task<AccountingMonthlyPaymentSlipDto> GetMonthlyPaymentSlipAsync(
            string userId,
            DateTime paymentDate,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.GetMonthlyPaymentSlip
            var year = paymentDate.Year;
            var month = paymentDate.Month;
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);

            var previousPayments = await _uow.Repository<TxEmployeesMonthlyPayment>().GetAllAsync(
                Filter: c =>
                    c.EmployeeMonthlyPaymentLevel != null
                    && c.EmployeeMonthlyPaymentLevel.Month != null
                    && c.EmployeeMonthlyPaymentLevel.Month.Year != null
                    && c.EmployeeMonthlyPaymentLevel.Month.Month == month
                    && c.EmployeeMonthlyPaymentLevel.Month.Year.Year == year
                    && c.EmployeeMonthlyPaymentLevel.Month.Year.SchoolId == schoolId,
                IncludeProperties: "Employee.User,EmployeeMonthlyPaymentLevel.Month.Year",
                cancellationToken: ct);

            if (previousPayments.Any())
            {
                return new AccountingMonthlyPaymentSlipDto
                {
                    PaymentDate = paymentDate,
                    Mode = "payments",
                    Structures = null,
                    Payments = previousPayments.Select(MapMonthlyPaymentRow).ToList(),
                    UnConfirmedMonthlyPaymentId = previousPayments
                        .First()
                        .EmployeeMonthlyPaymentLevelId,
                };
            }

            var issuedLevel = await _uow.Repository<TxEmployeeMonthlyPaymentLevel>().FindAsync(
                c =>
                    c.EmployeeMonthlyPaymentLevel == EmployeeMonthlyPaymentLevel.HasIssuedPaymentSlip
                    && c.Month != null
                    && c.Month.Year != null
                    && c.Month.Year.SchoolId == schoolId,
                ct,
                x => x.Month!,
                x => x.Month!.Year!);

            if (issuedLevel == null)
            {
                var yearEntity = await EnsurePayrollYearAsync(schoolId, (short)year, ct);
                var monthEntity = await _uow.Repository<TxMonth>().FindAsync(
                    c => c.Month == month && c.YearId == yearEntity.Id,
                    ct,
                    x => x.Year!);
                if (monthEntity == null)
                {
                    monthEntity = new TxMonth
                    {
                        Id = Guid.NewGuid().ToString(),
                        Month = (byte)month,
                        YearId = yearEntity.Id,
                    };
                    _uow.Repository<TxMonth>().Add(monthEntity);
                    await _uow.SaveChangesAsync(ct);
                }

                // Legacy only inserts level when month was newly created; ensure level always exists.
                var levelForMonth = await _uow.Repository<TxEmployeeMonthlyPaymentLevel>().FindAsync(
                    x =>
                        x.MonthId == monthEntity.Id
                        && x.EmployeeMonthlyPaymentLevel == EmployeeMonthlyPaymentLevel.HasIssuedPaymentSlip,
                    ct);
                if (levelForMonth == null)
                {
                    levelForMonth = new TxEmployeeMonthlyPaymentLevel
                    {
                        Id = Guid.NewGuid().ToString(),
                        EmployeeMonthlyPaymentLevel = EmployeeMonthlyPaymentLevel.HasIssuedPaymentSlip,
                        MonthId = monthEntity.Id,
                    };
                    _uow.Repository<TxEmployeeMonthlyPaymentLevel>().Add(levelForMonth);
                    await _uow.SaveChangesAsync(ct);
                }

                return new AccountingMonthlyPaymentSlipDto
                {
                    PaymentDate = paymentDate,
                    Mode = "structure",
                    Structures = await EmployeesMonthlyPaymentByIncomeStructureAsync(schoolId, ct),
                    Payments = null,
                    UnConfirmedMonthlyPaymentId = levelForMonth.Id,
                };
            }

            var draftPayments = await _uow.Repository<TxEmployeesMonthlyPayment>().GetAllAsync(
                Filter: d =>
                    d.EmployeeMonthlyPaymentLevel != null
                    && d.EmployeeMonthlyPaymentLevel.Month != null
                    && d.EmployeeMonthlyPaymentLevel.Month.Year != null
                    && d.EmployeeMonthlyPaymentLevel.Month.Year.SchoolId == schoolId
                    && d.EmployeeMonthlyPaymentLevel.EmployeeMonthlyPaymentLevel
                        == EmployeeMonthlyPaymentLevel.HasIssuedPaymentSlip,
                IncludeProperties: "Employee.User,EmployeeMonthlyPaymentLevel.Month.Year",
                cancellationToken: ct);

            if (draftPayments.Any())
            {
                return new AccountingMonthlyPaymentSlipDto
                {
                    PaymentDate = paymentDate,
                    Mode = "payments",
                    Structures = null,
                    Payments = draftPayments.Select(MapMonthlyPaymentRow).ToList(),
                    UnConfirmedMonthlyPaymentId = draftPayments.First().EmployeeMonthlyPaymentLevelId,
                };
            }

            // Remap existing empty HasIssued draft to requested year/month (legacy in-place mutate).
            if (issuedLevel.Month?.Year != null)
            {
                issuedLevel.Month.Year.Year = (short)year;
                _uow.Repository<TxYear>().Update(issuedLevel.Month.Year);
                issuedLevel.Month.Month = (byte)month;
                _uow.Repository<TxMonth>().Update(issuedLevel.Month);
                await _uow.SaveChangesAsync(ct);
            }

            return new AccountingMonthlyPaymentSlipDto
            {
                PaymentDate = paymentDate,
                Mode = "structure",
                Structures = await EmployeesMonthlyPaymentByIncomeStructureAsync(schoolId, ct),
                Payments = null,
                UnConfirmedMonthlyPaymentId = issuedLevel.Id,
            };
        }

        /// <inheritdoc cref="IAccountingService.CreateMonthlyPaymentAsync" />
        public async Task<AccountingMonthlyPayrollMutationResultDto> CreateMonthlyPaymentAsync(
            string userId,
            List<AccountingMonthlyPaymentSaveDto> monthlyPayment,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.CreateMonthlyPayment
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            monthlyPayment ??= new List<AccountingMonthlyPaymentSaveDto>();

            var hasIssued = await _uow.Repository<TxEmployeeMonthlyPaymentLevel>().FindAsync(
                x =>
                    x.Month != null
                    && x.Month.Year != null
                    && x.Month.Year.SchoolId == schoolId
                    && x.EmployeeMonthlyPaymentLevel == EmployeeMonthlyPaymentLevel.HasIssuedPaymentSlip,
                ct,
                x => x.Month!,
                x => x.Month!.Year!);
            if (hasIssued == null)
            {
                return new AccountingMonthlyPayrollMutationResultDto
                {
                    Status = 0,
                    Message = "Failed! Subject Name exist against another record.",
                };
            }

            var existingUnderIssued = await _uow.Repository<TxEmployeesMonthlyPayment>().FindAsync(
                z =>
                    z.EmployeeMonthlyPaymentLevel != null
                    && z.EmployeeMonthlyPaymentLevel.Month != null
                    && z.EmployeeMonthlyPaymentLevel.Month.Year != null
                    && z.EmployeeMonthlyPaymentLevel.Month.Year.SchoolId == schoolId
                    && z.EmployeeMonthlyPaymentLevel.EmployeeMonthlyPaymentLevel
                        == EmployeeMonthlyPaymentLevel.HasIssuedPaymentSlip,
                ct,
                d => d.EmployeeMonthlyPaymentLevel!);

            if (existingUnderIssued != null)
            {
                foreach (var item in monthlyPayment)
                {
                    if (string.IsNullOrWhiteSpace(item.Id))
                    {
                        continue;
                    }

                    var entity = await _uow.Repository<TxEmployeesMonthlyPayment>().FindAsync(
                        x => x.Id == item.Id,
                        ct);
                    if (entity == null)
                    {
                        continue;
                    }

                    var preservedComments = entity.Comments;
                    entity.EmployeeMonthlyPaymentLevelId = existingUnderIssued.EmployeeMonthlyPaymentLevelId;
                    entity.EmployeeId = item.EmployeeId;
                    entity.BasicSalary = item.BasicSalary;
                    entity.Allowance = item.Allowance;
                    entity.Bonus = item.Bonus;
                    entity.Deduction = item.Deduction;
                    entity.EmployeeMonthlyPaymentStatus = item.EmployeeMonthlyPaymentStatus;
                    entity.PaymentType = item.PaymentType;
                    entity.Comments = preservedComments;
                    entity.UpdatedDate = DateTime.UtcNow;
                    entity.UpdatedBy = userId ?? string.Empty;
                    _uow.Repository<TxEmployeesMonthlyPayment>().Update(entity);
                }
            }
            else
            {
                var now = DateTime.UtcNow;
                foreach (var item in monthlyPayment)
                {
                    if (string.IsNullOrWhiteSpace(item.EmployeeId))
                    {
                        continue;
                    }

                    _uow.Repository<TxEmployeesMonthlyPayment>().Add(
                        new TxEmployeesMonthlyPayment
                        {
                            Id = Guid.NewGuid().ToString(),
                            EmployeeId = item.EmployeeId,
                            EmployeeMonthlyPaymentLevelId = hasIssued.Id,
                            BasicSalary = item.BasicSalary,
                            Allowance = item.Allowance,
                            Bonus = item.Bonus,
                            Deduction = item.Deduction,
                            EmployeeMonthlyPaymentStatus = item.EmployeeMonthlyPaymentStatus,
                            PaymentType = item.PaymentType,
                            Comments = item.Comments ?? string.Empty,
                            CreatedDate = now,
                            CreatedBy = userId ?? string.Empty,
                            UpdatedDate = now,
                            UpdatedBy = userId ?? string.Empty,
                        });
                }
            }

            await _uow.SaveChangesAsync(ct);
            return new AccountingMonthlyPayrollMutationResultDto
            {
                Status = 1,
                Message = "The changes has been succcessfully saved.",
            };
        }

        /// <inheritdoc cref="IAccountingService.ConfirmMonthlyPaymentAsync" />
        public async Task<AccountingMonthlyPayrollMutationResultDto> ConfirmMonthlyPaymentAsync(
            string userId,
            string levelId,
            CancellationToken ct = default)
        {
            // Legacy ConfirmMonthlyPayment — param is level id
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(levelId))
            {
                return new AccountingMonthlyPayrollMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Subject Name exist against another record.",
                };
            }

            var level = await _uow.Repository<TxEmployeeMonthlyPaymentLevel>().FindAsync(
                x =>
                    x.Id == levelId
                    && x.Month != null
                    && x.Month.Year != null
                    && x.Month.Year.SchoolId == schoolId,
                ct,
                x => x.Month!,
                x => x.Month!.Year!);
            if (level == null)
            {
                return new AccountingMonthlyPayrollMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Subject Name exist against another record.",
                };
            }

            level.EmployeeMonthlyPaymentLevel = EmployeeMonthlyPaymentLevel.HasConfirmedPayment;
            _uow.Repository<TxEmployeeMonthlyPaymentLevel>().Update(level);
            await _uow.SaveChangesAsync(ct);

            return new AccountingMonthlyPayrollMutationResultDto
            {
                Status = 0,
                Message = "Confirmation Successful.",
            };
        }

        /// <inheritdoc cref="IAccountingService.SubmitPaymentForASingleEmployeeAsync" />
        public async Task<AccountingMonthlyPayrollMutationResultDto> SubmitPaymentForASingleEmployeeAsync(
            string userId,
            AccountingMonthlyPaymentSaveDto model,
            CancellationToken ct = default)
        {
            // Legacy SubmitPaymentForAsingleEmployee — preserve Comments from DB
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            model ??= new AccountingMonthlyPaymentSaveDto();
            if (string.IsNullOrWhiteSpace(model.Id))
            {
                return new AccountingMonthlyPayrollMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Subject Name exist against another record.",
                };
            }

            var entity = await _uow.Repository<TxEmployeesMonthlyPayment>().FindAsync(
                x =>
                    x.Id == model.Id
                    && x.Employee != null
                    && x.Employee.SchoolId == schoolId,
                ct,
                x => x.Employee!);
            if (entity == null)
            {
                return new AccountingMonthlyPayrollMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Subject Name exist against another record.",
                };
            }

            var preservedComments = entity.Comments;
            entity.EmployeeId = model.EmployeeId;
            entity.EmployeeMonthlyPaymentLevelId = string.IsNullOrWhiteSpace(model.EmployeeMonthlyPaymentLevelId)
                ? entity.EmployeeMonthlyPaymentLevelId
                : model.EmployeeMonthlyPaymentLevelId;
            entity.BasicSalary = model.BasicSalary;
            entity.Allowance = model.Allowance;
            entity.Bonus = model.Bonus;
            entity.Deduction = model.Deduction;
            entity.EmployeeMonthlyPaymentStatus = model.EmployeeMonthlyPaymentStatus;
            entity.PaymentType = model.PaymentType;
            entity.Comments = preservedComments;
            entity.UpdatedDate = DateTime.UtcNow;
            entity.UpdatedBy = userId ?? string.Empty;
            _uow.Repository<TxEmployeesMonthlyPayment>().Update(entity);
            await _uow.SaveChangesAsync(ct);

            return new AccountingMonthlyPayrollMutationResultDto
            {
                Status = 0,
                Message = "The changes has been succcessfully saved.",
            };
        }

        /// <inheritdoc cref="IAccountingService.DeleteRecentlyGeneratedPaymentAsync" />
        public async Task<AccountingMonthlyPayrollMutationResultDto> DeleteRecentlyGeneratedPaymentAsync(
            string userId,
            string monthId,
            CancellationToken ct = default)
        {
            // Legacy DeleteRecentlyGeneratedPayment
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(monthId))
            {
                return new AccountingMonthlyPayrollMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Subject Name exist against another record.",
                };
            }

            var month = await _uow.Repository<TxMonth>().FindAsync(
                x => x.Id == monthId && x.Year != null && x.Year.SchoolId == schoolId,
                ct,
                x => x.Year!);
            if (month == null)
            {
                return new AccountingMonthlyPayrollMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Subject Name exist against another record.",
                };
            }

            var recentPayment = await _uow.Repository<TxEmployeesMonthlyPayment>().GetAllAsync(
                Filter: x =>
                    x.EmployeeMonthlyPaymentLevel != null
                    && x.EmployeeMonthlyPaymentLevel.MonthId == monthId,
                IncludeProperties: "EmployeeMonthlyPaymentLevel",
                cancellationToken: ct);

            foreach (var item in recentPayment)
            {
                _uow.Repository<TxEmployeesMonthlyPayment>().Delete(item);
            }

            await _uow.SaveChangesAsync(ct);
            return new AccountingMonthlyPayrollMutationResultDto
            {
                Status = 0,
                Message = "Recently generated preview has successfully been deleted.",
            };
        }

        /// <inheritdoc cref="IAccountingService.GetEmployeeMonthlyPaymentCommentAsync" />
        public async Task<string> GetEmployeeMonthlyPaymentCommentAsync(
            string userId,
            string employeeMonthlyId,
            CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(employeeMonthlyId))
            {
                return string.Empty;
            }

            var row = await _uow.Repository<TxEmployeesMonthlyPayment>().FindAsync(
                x =>
                    x.Id == employeeMonthlyId
                    && x.Employee != null
                    && x.Employee.SchoolId == schoolId,
                ct,
                x => x.Employee!);
            return row?.Comments ?? string.Empty;
        }

        /// <inheritdoc cref="IAccountingService.SaveEmployeeMonthlyPaymentCommentAsync" />
        public async Task<AccountingMonthlyPayrollMutationResultDto> SaveEmployeeMonthlyPaymentCommentAsync(
            string userId,
            string employeeMonthlyId,
            string commentInput,
            CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(employeeMonthlyId))
            {
                return new AccountingMonthlyPayrollMutationResultDto
                {
                    Status = 1,
                    Message = "Comment is required before you continue.",
                };
            }

            var comment = commentInput ?? string.Empty;
            if (string.IsNullOrWhiteSpace(comment))
            {
                return new AccountingMonthlyPayrollMutationResultDto
                {
                    Status = 1,
                    Message = "Comment is required before you continue.",
                };
            }

            if (comment.Length > 170)
            {
                return new AccountingMonthlyPayrollMutationResultDto
                {
                    Status = 1,
                    Message = "Comment is out of range. The maximum is 170",
                };
            }

            var row = await _uow.Repository<TxEmployeesMonthlyPayment>().FindAsync(
                x =>
                    x.Id == employeeMonthlyId
                    && x.Employee != null
                    && x.Employee.SchoolId == schoolId,
                ct,
                x => x.Employee!);
            if (row == null)
            {
                return new AccountingMonthlyPayrollMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Subject Name exist against another record.",
                };
            }

            row.Comments = comment;
            _uow.Repository<TxEmployeesMonthlyPayment>().Update(row);
            await _uow.SaveChangesAsync(ct);

            return new AccountingMonthlyPayrollMutationResultDto
            {
                Status = 0,
                Message = "Comment has been submited successfully.",
            };
        }

        /// <inheritdoc cref="IAccountingService.GetSalaryReportDefaultsAsync" />
        public async Task<AccountingSalaryReportDefaultsDto> GetSalaryReportDefaultsAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy GetYearAndMonthDropdownForSalary + SalaryPaymentReport Select padding
            var years = await GetYearDropdownForSalaryAsync(userId, ct);
            var months = await GetMonthDropDownForSalaryAsync(userId, ct);

            if (years.Count == 1)
            {
                years.Add(new SelectListItem { Text = "Select", Value = "-1" });
            }

            if (months.Count == 1)
            {
                months.Add(new SelectListItem { Text = "Select", Value = "-1" });
            }

            return new AccountingSalaryReportDefaultsDto
            {
                Years = years,
                Months = months,
            };
        }

        /// <inheritdoc cref="IAccountingService.GetSalaryReportMonthsAsync" />
        public async Task<List<SelectListItem>> GetSalaryReportMonthsAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy MonthDropdownForSelectedYearSalary (year param ignored)
            var months = await GetMonthDropDownForSalaryAsync(userId, ct);
            if (months.Count == 1)
            {
                months.Add(new SelectListItem { Text = "Select", Value = "-1" });
            }

            return months;
        }

        /// <inheritdoc cref="IAccountingService.GetSalaryReportForSelectedPeriodAsync" />
        public async Task<List<AccountingMainSalaryReportDto>> GetSalaryReportForSelectedPeriodAsync(
            string userId,
            short year,
            byte startMonth,
            byte endMonth,
            CancellationToken ct = default)
        {
            // Legacy GetSalaryReportForSelectedPeriod
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var payments = await _uow.Repository<TxEmployeesMonthlyPayment>().GetAllAsync(
                Filter: d =>
                    d.EmployeeMonthlyPaymentLevel != null
                    && d.EmployeeMonthlyPaymentLevel.Month != null
                    && d.EmployeeMonthlyPaymentLevel.Month.Year != null
                    && d.EmployeeMonthlyPaymentLevel.Month.Year.SchoolId == schoolId
                    && d.EmployeeMonthlyPaymentLevel.Month.Year.Year == year
                    && d.EmployeeMonthlyPaymentLevel.Month.Month >= startMonth
                    && d.EmployeeMonthlyPaymentLevel.Month.Month <= endMonth
                    && d.EmployeeMonthlyPaymentStatus == EmployeeMonthlyPaymentStatus.HasBeenPaid,
                IncludeProperties: "EmployeeMonthlyPaymentLevel.Month.Year",
                cancellationToken: ct);

            var report = payments.GroupBy(v => v.EmployeeMonthlyPaymentLevel!.MonthId);
            var salaryReportList = new List<AccountingMainSalaryReportDto>();
            foreach (var each in report)
            {
                var first = each.First();
                var monthNumber = first.EmployeeMonthlyPaymentLevel?.Month?.Month ?? 0;
                var monthId = first.EmployeeMonthlyPaymentLevel?.MonthId ?? each.Key;
                salaryReportList.Add(
                    new AccountingMainSalaryReportDto
                    {
                        Id = monthId,
                        Month = EnumsAndExtensionMethods.GetMonth(monthNumber),
                        MonthNumber = monthNumber,
                        NoOfEmpl = each.Count(),
                        BasicSalary = each.Sum(x => x.BasicSalary),
                        Allowance = each.Sum(a => a.Allowance),
                        Bonus = each.Sum(x => x.Bonus),
                        Deduction = each.Sum(x => x.Deduction),
                        SubordinateSalaryReport = new AccountingSubordinateSalaryReportDto
                        {
                            MainSalaryReportId = monthId,
                            CashNo = each.Count(x => x.PaymentType == PaymentType.Cash),
                            CashAmount = each
                                .Where(x => x.PaymentType == PaymentType.Cash)
                                .Sum(c => (c.Allowance + c.BasicSalary + c.Bonus) - c.Deduction),
                            TellerNo = each.Count(x => x.PaymentType == PaymentType.Teller),
                            TellerAmount = each
                                .Where(x => x.PaymentType == PaymentType.Teller)
                                .Sum(c => (c.Allowance + c.BasicSalary + c.Bonus) - c.Deduction),
                            TransferNo = each.Count(x => x.PaymentType == PaymentType.Transfer),
                            TransferAmount = each
                                .Where(x => x.PaymentType == PaymentType.Transfer)
                                .Sum(c => (c.Allowance + c.BasicSalary + c.Bonus) - c.Deduction),
                        },
                    });
            }

            return salaryReportList.OrderBy(x => x.MonthNumber).ToList();
        }

        private async Task<List<SelectListItem>> GetYearDropdownForSalaryAsync(
            string userId,
            CancellationToken ct)
        {
            // Legacy GetYearDropdownForSalary
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var payments = await _uow.Repository<TxEmployeesMonthlyPayment>().GetAllAsync(
                Filter: n =>
                    n.Employee != null
                    && n.Employee.SchoolId == schoolId
                    && n.EmployeeMonthlyPaymentLevel != null
                    && n.EmployeeMonthlyPaymentLevel.EmployeeMonthlyPaymentLevel
                        == EmployeeMonthlyPaymentLevel.HasConfirmedPayment,
                IncludeProperties: "Employee,EmployeeMonthlyPaymentLevel.Month.Year",
                cancellationToken: ct);

            return payments
                .Where(v => v.EmployeeMonthlyPaymentLevel?.Month?.Year != null)
                .OrderByDescending(v => v.EmployeeMonthlyPaymentLevel!.Month!.Year!.Year)
                .GroupBy(c => c.EmployeeMonthlyPaymentLevel!.Month!.Year!.Year)
                .Select(c => c.First())
                .Select(b => new SelectListItem
                {
                    Text = b.EmployeeMonthlyPaymentLevel!.Month!.Year!.Year.ToString(),
                    Value = b.EmployeeMonthlyPaymentLevel!.Month!.Year!.Year.ToString(),
                })
                .ToList();
        }

        private async Task<List<SelectListItem>> GetMonthDropDownForSalaryAsync(
            string userId,
            CancellationToken ct)
        {
            // Legacy GetMonthDropDownForSalary
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var payments = await _uow.Repository<TxEmployeesMonthlyPayment>().GetAllAsync(
                Filter: f =>
                    f.Employee != null
                    && f.Employee.SchoolId == schoolId
                    && f.EmployeeMonthlyPaymentLevel != null
                    && f.EmployeeMonthlyPaymentLevel.EmployeeMonthlyPaymentLevel
                        == EmployeeMonthlyPaymentLevel.HasConfirmedPayment,
                IncludeProperties: "Employee,EmployeeMonthlyPaymentLevel.Month",
                cancellationToken: ct);

            var monthList = payments
                .Where(x => x.EmployeeMonthlyPaymentLevel?.Month != null)
                .OrderByDescending(x => x.EmployeeMonthlyPaymentLevel!.Month!.Month)
                .GroupBy(c => c.EmployeeMonthlyPaymentLevel!.Month!.Month)
                .Select(c => c.First())
                .Select(b => new SelectListItem
                {
                    Value = b.EmployeeMonthlyPaymentLevel!.Month!.Month.ToString(),
                    Text = b.EmployeeMonthlyPaymentLevel!.Month!.Month.ToString(),
                })
                .ToList();

            foreach (var each in monthList)
            {
                if (byte.TryParse(each.Text, out var monthValue))
                {
                    each.Text = EnumsAndExtensionMethods.GetMonth(monthValue);
                }
            }

            return monthList;
        }

        private async Task<List<AccountingPayrollStructureRowDto>> EmployeesMonthlyPaymentByIncomeStructureAsync(
            string schoolId,
            CancellationToken ct)
        {
            // Legacy EmployeesMonthlyPaymentByIncomeStructure
            var structures = await _uow.Repository<TxEmployeeIncomeStructure>().GetAllAsync(
                Filter: d => d.Employee != null && d.Employee.SchoolId == schoolId,
                IncludeProperties: "Employee.User",
                cancellationToken: ct);

            return structures
                .Select(s => new AccountingPayrollStructureRowDto
                {
                    EmployeeId = s.Id,
                    FullName = s.Employee?.User?.FullName ?? string.Empty,
                    EmployeeIdNo = string.IsNullOrWhiteSpace(s.Employee?.EmployeeIdNo)
                        ? "No IdNo"
                        : s.Employee!.EmployeeIdNo,
                    BasicSalary = s.BasicSalary,
                    Allowance = s.Allowance,
                })
                .OrderBy(x => x.FullName)
                .ToList();
        }

        private async Task<TxYear> EnsurePayrollYearAsync(string schoolId, short year, CancellationToken ct)
        {
            var selectedYear = await _uow.Repository<TxYear>().FindAsync(
                c => c.Year == year && c.SchoolId == schoolId,
                ct);
            if (selectedYear != null)
            {
                return selectedYear;
            }

            selectedYear = new TxYear
            {
                Id = Guid.NewGuid().ToString(),
                SchoolId = schoolId,
                Year = year,
            };
            _uow.Repository<TxYear>().Add(selectedYear);
            await _uow.SaveChangesAsync(ct);
            return selectedYear;
        }

        private static AccountingMonthlyPaymentRowDto MapMonthlyPaymentRow(TxEmployeesMonthlyPayment item)
        {
            var level = item.EmployeeMonthlyPaymentLevel;
            var month = level?.Month;
            var year = month?.Year;
            return new AccountingMonthlyPaymentRowDto
            {
                Id = item.Id,
                EmployeeMonthlyPaymentLevelId = item.EmployeeMonthlyPaymentLevelId,
                EmployeeId = item.EmployeeId,
                FullName = item.Employee?.User?.FullName ?? string.Empty,
                EmployeeIdNo = string.IsNullOrWhiteSpace(item.Employee?.EmployeeIdNo)
                    ? "No IdNo"
                    : item.Employee!.EmployeeIdNo,
                BasicSalary = item.BasicSalary,
                Allowance = item.Allowance,
                Bonus = item.Bonus,
                Deduction = item.Deduction,
                EmployeeMonthlyPaymentStatus = item.EmployeeMonthlyPaymentStatus,
                PaymentType = item.PaymentType,
                Comments = item.Comments ?? string.Empty,
                MonthId = month?.Id ?? string.Empty,
                Year = year?.Year ?? 0,
                Month = month?.Month ?? 0,
                LevelStatus = level?.EmployeeMonthlyPaymentLevel
                    ?? EmployeeMonthlyPaymentLevel.HasIssuedPaymentSlip,
            };
        }

        private static List<SelectListItem> BuildAllQualificationOptions() =>
            EnumsAndExtensionMethods.GetEnumToList<EmployeeQualificationStatus>()
                .Select(q => new SelectListItem
                {
                    Text = q.GetName(),
                    Value = ((int)q).ToString(),
                })
                .ToList();

        /// <summary>Legacy QualificationStaus — current first, then remaining with GetName labels.</summary>
        private static List<SelectListItem> BuildQualificationOptions(
            EmployeeQualificationStatus current)
        {
            var status = new List<SelectListItem>
            {
                new() { Text = current.ToString(), Value = ((int)current).ToString() },
            };
            status.AddRange(
                EnumsAndExtensionMethods.GetEnumToList<EmployeeQualificationStatus>()
                    .Where(item => item != current)
                    .Select(item => new SelectListItem
                    {
                        Text = item.ToString(),
                        Value = ((int)item).ToString(),
                    }));

            foreach (var item in status)
            {
                if (byte.TryParse(item.Value, out var raw))
                {
                    item.Text = ((EmployeeQualificationStatus)raw).GetName();
                }
            }

            return status;
        }

        /// <inheritdoc cref="IAccountingService.GetCurrentWeekIncomeAsync" />
        public async Task<List<AccountingIncomeItemDto>> GetCurrentWeekIncomeAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.GetCurrentWeekIncome
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var incomes = await _uow.Repository<XyIncome>().GetAllAsync(
                Filter: c =>
                    c.Week != null
                    && c.Week.SchoolId == schoolId
                    && c.Week.TimeStatus == TimeStatus.Current,
                IncludeProperties: "Week,ItemCategory",
                cancellationToken: ct);

            return incomes
                .OrderByDescending(x => x.CreatedDate)
                .Select(MapIncomeItem)
                .ToList();
        }

        /// <inheritdoc cref="IAccountingService.GetIncomeCapturingIndexAsync" />
        public async Task<AccountingIncomeCapturingIndexDto> GetIncomeCapturingIndexAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy EmployeeIncomeController.IncomeCapturingIndex
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var items = await GetCurrentWeekIncomeAsync(userId, ct);
            var dropDown = await GetYearAndMonthDropdownForIncomeAsync(schoolId, ct);
            var years = dropDown.Years ?? new List<SelectListItem>();
            var months = dropDown.Months?.ToList() ?? new List<SelectListItem>();

            if (years.Count == 1)
            {
                years.Add(new SelectListItem { Text = "Select", Value = "-1" });
            }

            if (months.Count == 1)
            {
                months.Add(new SelectListItem { Text = "Select", Value = "-1" });
            }

            return new AccountingIncomeCapturingIndexDto
            {
                Years = years,
                Months = months,
                Items = items,
                CurrentWeekId = items.FirstOrDefault()?.WeekId,
            };
        }

        /// <inheritdoc cref="IAccountingService.GetIncomeReportForSelectedPeriodAsync" />
        public async Task<List<AccountingIncomeItemDto>> GetIncomeReportForSelectedPeriodAsync(
            string userId,
            short year,
            byte startMonth,
            byte endMonth,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.GetIncomeReportForSelectedPeriod
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var report = await _uow.Repository<XyIncome>().GetAllAsync(
                Filter: d =>
                    d.Week != null
                    && d.Week.SchoolId == schoolId
                    && d.CapturedDate.Year == year
                    && d.CapturedDate.Month >= startMonth
                    && d.CapturedDate.Month <= endMonth,
                IncludeProperties: "Week,ItemCategory",
                cancellationToken: ct);

            return report.Select(MapIncomeItem).ToList();
        }

        /// <inheritdoc cref="IAccountingService.GetIncomeItemForEditAsync" />
        public async Task<AccountingIncomeItemEditFormDto?> GetIncomeItemForEditAsync(
            string userId,
            string id,
            CancellationToken ct = default)
        {
            // Legacy EditIncomeItem GET + GetIncomeCategoryListForEditForm
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            var entity = await _uow.Repository<XyIncome>().FindAsync(
                c => c.Id == id && c.Week != null && c.Week.SchoolId == schoolId,
                ct,
                c => c.Week!,
                c => c.ItemCategory!);
            if (entity == null)
            {
                return null;
            }

            var categories = await GetIncomeCategoryListAsync(userId, ct);
            var currentId = entity.ItemCategoryId;
            var options = new List<SelectListItem>();
            var current = categories.FirstOrDefault(c => c.Id == currentId);
            if (current != null)
            {
                options.Add(new SelectListItem { Text = current.CategoryName, Value = current.Id });
            }

            options.AddRange(
                categories
                    .Where(c => c.Id != currentId)
                    .Select(c => new SelectListItem { Text = c.CategoryName, Value = c.Id }));

            return new AccountingIncomeItemEditFormDto
            {
                Item = MapIncomeItem(entity),
                Categories = options,
            };
        }

        /// <inheritdoc cref="IAccountingService.UpdateIncomeItemAsync" />
        public async Task<AccountingIncomeItemMutationResultDto> UpdateIncomeItemAsync(
            string userId,
            AccountingIncomeItemDto model,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.UpdateIncomeItem
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (model == null || string.IsNullOrWhiteSpace(model.Id))
            {
                return new AccountingIncomeItemMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            var entity = await _uow.Repository<XyIncome>().FindAsync(
                c => c.Id == model.Id && c.Week != null && c.Week.SchoolId == schoolId,
                ct,
                c => c.Week!);
            if (entity == null)
            {
                return new AccountingIncomeItemMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            var capturedLocal = model.CapturedDate == default ? entity.CapturedDate : model.CapturedDate;
            entity.CapturedDate = new DateTime(
                capturedLocal.Year,
                capturedLocal.Month,
                capturedLocal.Day,
                0,
                0,
                0,
                DateTimeKind.Utc);
            entity.ItemCategoryId = model.ItemCategoryId ?? entity.ItemCategoryId;
            entity.Details = model.Details ?? string.Empty;
            entity.Cost = model.Cost;
            if (!string.IsNullOrWhiteSpace(model.WeekId))
            {
                entity.WeekId = model.WeekId;
            }

            entity.UpdatedDate = DateTime.UtcNow;
            entity.UpdatedBy = userId ?? string.Empty;
            _uow.Repository<XyIncome>().Update(entity);
            await _uow.SaveChangesAsync(ct);

            return new AccountingIncomeItemMutationResultDto
            {
                Status = 0,
                Message = "The changes has succcessfully been saved.",
            };
        }

        /// <inheritdoc cref="IAccountingService.DeleteIncomeItemAsync" />
        public async Task<AccountingIncomeItemMutationResultDto> DeleteIncomeItemAsync(
            string userId,
            string id,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.DeleteAnItemOfIncomeById
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(id))
            {
                return new AccountingIncomeItemMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            var entity = await _uow.Repository<XyIncome>().FindAsync(
                c => c.Id == id && c.Week != null && c.Week.SchoolId == schoolId,
                ct,
                c => c.Week!);
            if (entity == null)
            {
                return new AccountingIncomeItemMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            _uow.Repository<XyIncome>().Delete(entity);
            await _uow.SaveChangesAsync(ct);

            return new AccountingIncomeItemMutationResultDto
            {
                Status = 0,
                Message = "Record successfully Deleted.",
            };
        }

        /// <inheritdoc cref="IAccountingService.ConfirmRecentIncomeListAsync" />
        public async Task<AccountingIncomeItemMutationResultDto> ConfirmRecentIncomeListAsync(
            string userId,
            string weekId,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.ConfirmRecentIncomeList
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(weekId))
            {
                return new AccountingIncomeItemMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            var week = await _uow.Repository<XyIncomeWeek>().FindAsync(
                w => w.Id == weekId && w.SchoolId == schoolId,
                ct);
            if (week == null)
            {
                return new AccountingIncomeItemMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            week.TimeStatus = TimeStatus.Previous;
            _uow.Repository<XyIncomeWeek>().Update(week);
            await _uow.SaveChangesAsync(ct);

            return new AccountingIncomeItemMutationResultDto
            {
                Status = 0,
                Message = "Confirmation Successful.",
            };
        }

        /// <inheritdoc cref="IAccountingService.GetCurrentWeekExpenseAsync" />
        public async Task<List<AccountingIncomeItemDto>> GetCurrentWeekExpenseAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.GetCurrentWeekExpense
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var expenses = await _uow.Repository<XyExpenses>().GetAllAsync(
                Filter: c =>
                    c.Week != null
                    && c.Week.SchoolId == schoolId
                    && c.Week.TimeStatus == TimeStatus.Current,
                IncludeProperties: "Week,ItemCategory",
                cancellationToken: ct);

            return expenses
                .OrderByDescending(x => x.CreatedDate)
                .Select(MapExpenseItem)
                .ToList();
        }

        /// <inheritdoc cref="IAccountingService.GetExpenditureCapturingIndexAsync" />
        public async Task<AccountingExpenditureCapturingIndexDto> GetExpenditureCapturingIndexAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy EmployeeIncomeController.ExpenditureCapturingIndex
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var items = await GetCurrentWeekExpenseAsync(userId, ct);
            var dropDown = await GetYearAndMonthDropdownForExpenditureAsync(schoolId, ct);
            var years = dropDown.Years ?? new List<SelectListItem>();
            var months = dropDown.Months?.ToList() ?? new List<SelectListItem>();

            if (years.Count == 1)
            {
                years.Add(new SelectListItem { Text = "Select", Value = "-1" });
            }

            if (months.Count == 1)
            {
                months.Add(new SelectListItem { Text = "Select", Value = "-1" });
            }

            return new AccountingExpenditureCapturingIndexDto
            {
                Years = years,
                Months = months,
                Items = items,
                CurrentWeekId = items.FirstOrDefault()?.WeekId,
            };
        }

        /// <inheritdoc cref="IAccountingService.GetExpenditureReportForSelectedPeriodAsync" />
        public async Task<List<AccountingIncomeItemDto>> GetExpenditureReportForSelectedPeriodAsync(
            string userId,
            short year,
            byte startMonth,
            byte endMonth,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.GetExpenditureReportForSelectedPeriod
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var report = await _uow.Repository<XyExpenses>().GetAllAsync(
                Filter: d =>
                    d.Week != null
                    && d.Week.SchoolId == schoolId
                    && d.CapturedDate.Year == year
                    && d.CapturedDate.Month >= startMonth
                    && d.CapturedDate.Month <= endMonth,
                IncludeProperties: "Week,ItemCategory",
                cancellationToken: ct);

            return report.Select(MapExpenseItem).ToList();
        }

        /// <inheritdoc cref="IAccountingService.GetExpenseItemForEditAsync" />
        public async Task<AccountingIncomeItemEditFormDto?> GetExpenseItemForEditAsync(
            string userId,
            string id,
            CancellationToken ct = default)
        {
            // Legacy EditExpenseItem GET + GetExpenditureCategoryListForEditForm
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            var entity = await _uow.Repository<XyExpenses>().FindAsync(
                c => c.Id == id && c.Week != null && c.Week.SchoolId == schoolId,
                ct,
                c => c.Week!,
                c => c.ItemCategory!);
            if (entity == null)
            {
                return null;
            }

            var categories = await GetExpenditureCategoryListAsync(userId, ct);
            var currentId = entity.ItemCategoryId;
            var options = new List<SelectListItem>();
            var current = categories.FirstOrDefault(c => c.Id == currentId);
            if (current != null)
            {
                options.Add(new SelectListItem { Text = current.CategoryName, Value = current.Id });
            }

            options.AddRange(
                categories
                    .Where(c => c.Id != currentId)
                    .Select(c => new SelectListItem { Text = c.CategoryName, Value = c.Id }));

            return new AccountingIncomeItemEditFormDto
            {
                Item = MapExpenseItem(entity),
                Categories = options,
            };
        }

        /// <inheritdoc cref="IAccountingService.UpdateExpenseItemAsync" />
        public async Task<AccountingIncomeItemMutationResultDto> UpdateExpenseItemAsync(
            string userId,
            AccountingIncomeItemDto model,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.UpdateExpensesItem
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (model == null || string.IsNullOrWhiteSpace(model.Id))
            {
                return new AccountingIncomeItemMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            var entity = await _uow.Repository<XyExpenses>().FindAsync(
                c => c.Id == model.Id && c.Week != null && c.Week.SchoolId == schoolId,
                ct,
                c => c.Week!);
            if (entity == null)
            {
                return new AccountingIncomeItemMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            var capturedLocal = model.CapturedDate == default ? entity.CapturedDate : model.CapturedDate;
            entity.CapturedDate = new DateTime(
                capturedLocal.Year,
                capturedLocal.Month,
                capturedLocal.Day,
                0,
                0,
                0,
                DateTimeKind.Utc);
            entity.ItemCategoryId = model.ItemCategoryId ?? entity.ItemCategoryId;
            entity.Details = model.Details ?? string.Empty;
            entity.Cost = model.Cost;
            if (!string.IsNullOrWhiteSpace(model.WeekId))
            {
                entity.WeekId = model.WeekId;
            }

            entity.UpdatedDate = DateTime.UtcNow;
            entity.UpdatedBy = userId ?? string.Empty;
            _uow.Repository<XyExpenses>().Update(entity);
            await _uow.SaveChangesAsync(ct);

            return new AccountingIncomeItemMutationResultDto
            {
                Status = 0,
                Message = "The changes has succcessfully been saved.",
            };
        }

        /// <inheritdoc cref="IAccountingService.DeleteExpenseItemAsync" />
        public async Task<AccountingIncomeItemMutationResultDto> DeleteExpenseItemAsync(
            string userId,
            string id,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.DeleteAnItemOfExpensesById
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(id))
            {
                return new AccountingIncomeItemMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            var entity = await _uow.Repository<XyExpenses>().FindAsync(
                c => c.Id == id && c.Week != null && c.Week.SchoolId == schoolId,
                ct,
                c => c.Week!);
            if (entity == null)
            {
                return new AccountingIncomeItemMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            _uow.Repository<XyExpenses>().Delete(entity);
            await _uow.SaveChangesAsync(ct);

            return new AccountingIncomeItemMutationResultDto
            {
                Status = 0,
                Message = "Record successfully Deleted.",
            };
        }

        /// <inheritdoc cref="IAccountingService.ConfirmRecentExpenditureListAsync" />
        public async Task<AccountingIncomeItemMutationResultDto> ConfirmRecentExpenditureListAsync(
            string userId,
            string weekId,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.ConfirmRecentExpenditureList
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(weekId))
            {
                return new AccountingIncomeItemMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            var week = await _uow.Repository<XyExpenditureWeek>().FindAsync(
                w => w.Id == weekId && w.SchoolId == schoolId,
                ct);
            if (week == null)
            {
                return new AccountingIncomeItemMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            week.TimeStatus = TimeStatus.Previous;
            _uow.Repository<XyExpenditureWeek>().Update(week);
            await _uow.SaveChangesAsync(ct);

            return new AccountingIncomeItemMutationResultDto
            {
                Status = 0,
                Message = "Confirmation Successful.",
            };
        }

        private async Task InsertIncomeAsync(
            string userId,
            string weekId,
            string itemCategoryId,
            string? details,
            double cost,
            DateTime capturedDate,
            CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var expenseModel = new XyIncome
            {
                Id = Guid.NewGuid().ToString(),
                WeekId = weekId,
                ItemCategoryId = itemCategoryId ?? string.Empty,
                Details = details ?? string.Empty,
                Cost = cost,
                CapturedDate = capturedDate,
                CreatedDate = now,
                CreatedBy = userId ?? string.Empty,
                UpdatedDate = now,
                UpdatedBy = userId ?? string.Empty,
            };
            _uow.Repository<XyIncome>().Add(expenseModel);
            await _uow.SaveChangesAsync(ct);
        }

        private async Task InsertExpenseAsync(
            string userId,
            string weekId,
            string itemCategoryId,
            string? details,
            double cost,
            DateTime capturedDate,
            CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var expenseModel = new XyExpenses
            {
                Id = Guid.NewGuid().ToString(),
                WeekId = weekId,
                ItemCategoryId = itemCategoryId ?? string.Empty,
                Details = details ?? string.Empty,
                Cost = cost,
                CapturedDate = capturedDate,
                CreatedDate = now,
                CreatedBy = userId ?? string.Empty,
                UpdatedDate = now,
                UpdatedBy = userId ?? string.Empty,
            };
            _uow.Repository<XyExpenses>().Add(expenseModel);
            await _uow.SaveChangesAsync(ct);
        }

        private static WeekOfMonth MapWeekOfMonth(int actualWeek) =>
            actualWeek switch
            {
                1 => WeekOfMonth.First,
                2 => WeekOfMonth.Second,
                3 => WeekOfMonth.Third,
                4 => WeekOfMonth.Fourth,
                5 => WeekOfMonth.Fift,
                _ => WeekOfMonth.Sixth,
            };

        /// <inheritdoc cref="IAccountingService.GetIncomeCategoryListAsync" />
        public async Task<List<AccountingIncomeCategoryDto>> GetIncomeCategoryListAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.GetIncomeCategoryList
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var expensesCategoryList = (await _uow.Repository<XyIncomeCategory>().WhereAsync(
                x => x.SchoolId == schoolId,
                ct)).ToList();

            if (expensesCategoryList.Count == 0)
            {
                return new List<AccountingIncomeCategoryDto>();
            }

            return expensesCategoryList
                .Select(v => new AccountingIncomeCategoryDto
                {
                    Id = v.Id,
                    CategoryName = v.CategoryName,
                    SchoolId = v.SchoolId,
                })
                .ToList();
        }

        /// <inheritdoc cref="IAccountingService.GetIncomeCategoryIndexAsync" />
        public async Task<AccountingIncomeCategoryIndexDto> GetIncomeCategoryIndexAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy EmployeeIncomeController.IncomeCategoryIndex
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var categories = await GetIncomeCategoryListAsync(userId, ct);

            var categoryOptions = categories
                .Select(v => new SelectListItem
                {
                    Value = v.Id,
                    Text = v.CategoryName,
                })
                .ToList();

            var dropDown = await GetYearAndMonthDropdownForIncomeAsync(schoolId, ct);
            var years = dropDown.Years ?? new List<SelectListItem>();
            var months = dropDown.Months?.ToList() ?? new List<SelectListItem>();

            if (years.Count == 1)
            {
                years.Add(new SelectListItem { Text = "Select", Value = "-1" });
            }

            if (months.Count == 1)
            {
                months.Add(new SelectListItem { Text = "Select", Value = "-1" });
            }

            return new AccountingIncomeCategoryIndexDto
            {
                Years = years,
                Months = months,
                CategoryOptions = categoryOptions,
                Categories = categories,
            };
        }

        /// <inheritdoc cref="IAccountingService.UpdateIncomeCategoryAsync" />
        public async Task<AccountingIncomeCategoryMutationResultDto> UpdateIncomeCategoryAsync(
            string userId,
            AccountingIncomeCategoryDto model,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.UpdateIncomeCategory
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (model == null || string.IsNullOrWhiteSpace(model.Id))
            {
                return new AccountingIncomeCategoryMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            var entity = await _uow.Repository<XyIncomeCategory>().FindAsync(
                x => x.Id == model.Id && x.SchoolId == schoolId,
                ct);
            if (entity == null)
            {
                return new AccountingIncomeCategoryMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            entity.CategoryName = model.CategoryName ?? string.Empty;
            _uow.Repository<XyIncomeCategory>().Update(entity);
            await _uow.SaveChangesAsync(ct);

            return new AccountingIncomeCategoryMutationResultDto
            {
                Status = 0,
                Message = "The changes has been succcessfully saved.",
            };
        }

        /// <inheritdoc cref="IAccountingService.DeleteIncomeCategoryByIdAsync" />
        public async Task<AccountingIncomeCategoryMutationResultDto> DeleteIncomeCategoryByIdAsync(
            string userId,
            string id,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.DeleteIncomeCategoryById
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(id))
            {
                return new AccountingIncomeCategoryMutationResultDto
                {
                    Status = 1,
                    Message =
                        "Failed! The category has income item(s) to it and therefore, we can not delete it.",
                };
            }

            var entity = await _uow.Repository<XyIncomeCategory>().FindAsync(
                x => x.Id == id && x.SchoolId == schoolId,
                ct);
            if (entity == null)
            {
                return new AccountingIncomeCategoryMutationResultDto
                {
                    Status = 1,
                    Message =
                        "Failed! The category has income item(s) to it and therefore, we can not delete it.",
                };
            }

            var linkedIncome = await _uow.Repository<XyIncome>().FindAsync(
                c => c.ItemCategoryId == id,
                ct);
            if (linkedIncome != null)
            {
                return new AccountingIncomeCategoryMutationResultDto
                {
                    Status = 1,
                    Message =
                        "Failed! The category has income item(s) to it and therefore, we can not delete it.",
                };
            }

            _uow.Repository<XyIncomeCategory>().Delete(entity);
            await _uow.SaveChangesAsync(ct);

            return new AccountingIncomeCategoryMutationResultDto
            {
                Status = 0,
                Message = "Record deleted Successfully.",
            };
        }

        /// <inheritdoc cref="IAccountingService.GetMonthDropdownForIncomeAsync" />
        public async Task<List<SelectListItem>> GetMonthDropdownForIncomeAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy MonthDropdownForSelectedYearIncome → GetMonthDropDownForIncome
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var months = await GetMonthDropDownForIncomeAsync(schoolId, ct);
            if (months.Count == 1)
            {
                months.Add(new SelectListItem { Text = "Select", Value = "-1" });
            }

            return months;
        }

        /// <inheritdoc cref="IAccountingService.GetCategoryIncomeReportAsync" />
        public async Task<List<AccountingIncomeItemDto>> GetCategoryIncomeReportAsync(
            string userId,
            short year,
            byte startMonth,
            byte endMonth,
            string categoryId,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.GetCategoryIncomeReport
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(categoryId) || categoryId == "-1")
            {
                return new List<AccountingIncomeItemDto>();
            }

            var report = await _uow.Repository<XyIncome>().GetAllAsync(
                Filter: d =>
                    d.Week != null
                    && d.Week.SchoolId == schoolId
                    && d.CapturedDate.Year == year
                    && d.CapturedDate.Month >= startMonth
                    && d.CapturedDate.Month <= endMonth
                    && d.ItemCategoryId == categoryId,
                IncludeProperties: "Week,ItemCategory",
                cancellationToken: ct);

            return report.Select(MapIncomeItem).ToList();
        }

        /// <inheritdoc cref="IAccountingService.GetIncomeCategoryAnalysisForSelectedPeriodAsync" />
        public async Task<List<AccountingIncomeItemDto>> GetIncomeCategoryAnalysisForSelectedPeriodAsync(
            string userId,
            short year,
            byte startMonth,
            byte endMonth,
            string? categoryId,
            int sortType,
            int numberToPick,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.GetIncomeCategoryAnalysisForSelectedPeriod
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var allIncomes = await _uow.Repository<XyIncome>().GetAllAsync(
                Filter: d =>
                    d.Week != null
                    && d.Week.SchoolId == schoolId
                    && d.CapturedDate.Year == year
                    && d.CapturedDate.Month >= startMonth
                    && d.CapturedDate.Month <= endMonth,
                IncludeProperties: "Week,ItemCategory",
                cancellationToken: ct);

            IEnumerable<XyIncome> filtered = allIncomes;
            var isAllCategories = string.IsNullOrWhiteSpace(categoryId)
                                  || categoryId == "0"
                                  || categoryId == "-1";
            if (!isAllCategories)
            {
                filtered = allIncomes.Where(d => d.ItemCategoryId == categoryId);
            }

            var ordered = sortType == 0
                ? filtered.OrderByDescending(s => s.Cost)
                : filtered.OrderBy(s => s.Cost);

            return ordered.Take(numberToPick).Select(MapIncomeItem).ToList();
        }

        /// <inheritdoc cref="IAccountingService.GetExpenditureCategoryListAsync" />
        public async Task<List<AccountingExpenditureCategoryDto>> GetExpenditureCategoryListAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.GetExpensesCategoryList
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var list = (await _uow.Repository<XyExpensesCategory>().WhereAsync(
                x => x.SchoolId == schoolId,
                ct)).ToList();

            if (list.Count == 0)
            {
                return new List<AccountingExpenditureCategoryDto>();
            }

            return list
                .Select(v => new AccountingExpenditureCategoryDto
                {
                    Id = v.Id,
                    CategoryName = v.CategoryName,
                    SchoolId = v.SchoolId,
                })
                .ToList();
        }

        /// <inheritdoc cref="IAccountingService.GetExpenditureCategoryIndexAsync" />
        public async Task<AccountingExpenditureCategoryIndexDto> GetExpenditureCategoryIndexAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy EmployeeIncomeController.ExpenditureCategoryIndex
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var categories = await GetExpenditureCategoryListAsync(userId, ct);

            var categoryOptions = categories
                .Select(v => new SelectListItem
                {
                    Value = v.Id,
                    Text = v.CategoryName,
                })
                .ToList();

            var dropDown = await GetYearAndMonthDropdownForExpenditureAsync(schoolId, ct);
            var years = dropDown.Years ?? new List<SelectListItem>();
            var months = dropDown.Months?.ToList() ?? new List<SelectListItem>();

            if (years.Count == 1)
            {
                years.Add(new SelectListItem { Text = "Select", Value = "-1" });
            }

            if (months.Count == 1)
            {
                months.Add(new SelectListItem { Text = "Select", Value = "-1" });
            }

            return new AccountingExpenditureCategoryIndexDto
            {
                Years = years,
                Months = months,
                CategoryOptions = categoryOptions,
                Categories = categories,
            };
        }

        /// <inheritdoc cref="IAccountingService.UpdateExpenditureCategoryAsync" />
        public async Task<AccountingExpenditureCategoryMutationResultDto> UpdateExpenditureCategoryAsync(
            string userId,
            AccountingExpenditureCategoryDto model,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.UpdateExpenditureCategory
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (model == null || string.IsNullOrWhiteSpace(model.Id))
            {
                return new AccountingExpenditureCategoryMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            var entity = await _uow.Repository<XyExpensesCategory>().FindAsync(
                x => x.Id == model.Id && x.SchoolId == schoolId,
                ct);
            if (entity == null)
            {
                return new AccountingExpenditureCategoryMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            entity.CategoryName = model.CategoryName ?? string.Empty;
            _uow.Repository<XyExpensesCategory>().Update(entity);
            await _uow.SaveChangesAsync(ct);

            return new AccountingExpenditureCategoryMutationResultDto
            {
                Status = 0,
                Message = "The changes has been succcessfully saved.",
            };
        }

        /// <inheritdoc cref="IAccountingService.DeleteExpenditureCategoryByIdAsync" />
        public async Task<AccountingExpenditureCategoryMutationResultDto> DeleteExpenditureCategoryByIdAsync(
            string userId,
            string id,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.DeleteExpenseCategoryById
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(id))
            {
                return new AccountingExpenditureCategoryMutationResultDto
                {
                    Status = 1,
                    Message =
                        "Failed! The category has expenditure item(s) to it and therefore, we can not delete it.",
                };
            }

            var entity = await _uow.Repository<XyExpensesCategory>().FindAsync(
                x => x.Id == id && x.SchoolId == schoolId,
                ct);
            if (entity == null)
            {
                return new AccountingExpenditureCategoryMutationResultDto
                {
                    Status = 1,
                    Message =
                        "Failed! The category has expenditure item(s) to it and therefore, we can not delete it.",
                };
            }

            var linked = await _uow.Repository<XyExpenses>().FindAsync(
                c => c.ItemCategoryId == id,
                ct);
            if (linked != null)
            {
                return new AccountingExpenditureCategoryMutationResultDto
                {
                    Status = 1,
                    Message =
                        "Failed! The category has expenditure item(s) to it and therefore, we can not delete it.",
                };
            }

            _uow.Repository<XyExpensesCategory>().Delete(entity);
            await _uow.SaveChangesAsync(ct);

            return new AccountingExpenditureCategoryMutationResultDto
            {
                Status = 0,
                Message = "Record deleted Successfully.",
            };
        }

        /// <inheritdoc cref="IAccountingService.GetMonthDropdownForExpenditureAsync" />
        public async Task<List<SelectListItem>> GetMonthDropdownForExpenditureAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy MonthDropdownForSelectedYearExpenditure → GetMonthDropDownForExpenditure
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var months = await GetMonthDropDownForExpenditureAsync(schoolId, ct);
            if (months.Count == 1)
            {
                months.Add(new SelectListItem { Text = "Select", Value = "-1" });
            }

            return months;
        }

        /// <inheritdoc cref="IAccountingService.GetCategoryExpenseReportAsync" />
        public async Task<List<AccountingIncomeItemDto>> GetCategoryExpenseReportAsync(
            string userId,
            short year,
            byte startMonth,
            byte endMonth,
            string categoryId,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.GetCategoryExpenseReport
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(categoryId) || categoryId == "-1")
            {
                return new List<AccountingIncomeItemDto>();
            }

            var report = await _uow.Repository<XyExpenses>().GetAllAsync(
                Filter: d =>
                    d.Week != null
                    && d.Week.SchoolId == schoolId
                    && d.CapturedDate.Year == year
                    && d.CapturedDate.Month >= startMonth
                    && d.CapturedDate.Month <= endMonth
                    && d.ItemCategoryId == categoryId,
                IncludeProperties: "Week,ItemCategory",
                cancellationToken: ct);

            return report.Select(MapExpenseItem).ToList();
        }

        /// <inheritdoc cref="IAccountingService.GetExpenditureCategoryAnalysisForSelectedPeriodAsync" />
        public async Task<List<AccountingIncomeItemDto>> GetExpenditureCategoryAnalysisForSelectedPeriodAsync(
            string userId,
            short year,
            byte startMonth,
            byte endMonth,
            string? categoryId,
            int sortType,
            int numberToPick,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.GetExpenditureCategoryAnalysisForSelectedPeriod
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var allExpenses = await _uow.Repository<XyExpenses>().GetAllAsync(
                Filter: d =>
                    d.Week != null
                    && d.Week.SchoolId == schoolId
                    && d.CapturedDate.Year == year
                    && d.CapturedDate.Month >= startMonth
                    && d.CapturedDate.Month <= endMonth,
                IncludeProperties: "Week,ItemCategory",
                cancellationToken: ct);

            IEnumerable<XyExpenses> filtered = allExpenses;
            if (!string.IsNullOrWhiteSpace(categoryId) && categoryId != "0")
            {
                filtered = allExpenses.Where(x => x.ItemCategoryId == categoryId);
            }

            if (numberToPick <= 0)
            {
                return new List<AccountingIncomeItemDto>();
            }

            var ordered = sortType == 0
                ? filtered.OrderByDescending(s => s.Cost)
                : filtered.OrderBy(s => s.Cost);

            return ordered.Take(numberToPick).Select(MapExpenseItem).ToList();
        }

        private static AccountingIncomeItemDto MapIncomeItem(XyIncome v) => new()
        {
            Id = v.Id,
            ItemCategoryId = v.ItemCategoryId,
            CategoryName = v.ItemCategory?.CategoryName ?? string.Empty,
            CapturedDate = v.CapturedDate,
            CreatedDate = v.CreatedDate,
            WeekId = v.WeekId,
            WeekOfMonth = v.Week?.WeekOfMonth.ToString() ?? string.Empty,
            Cost = v.Cost,
            Details = v.Details,
        };

        private static AccountingIncomeItemDto MapExpenseItem(XyExpenses v) => new()
        {
            Id = v.Id,
            ItemCategoryId = v.ItemCategoryId,
            CategoryName = v.ItemCategory?.CategoryName ?? string.Empty,
            CapturedDate = v.CapturedDate,
            CreatedDate = v.CreatedDate,
            WeekId = v.WeekId,
            WeekOfMonth = v.Week?.WeekOfMonth.ToString() ?? string.Empty,
            Cost = v.Cost,
            Details = v.Details,
        };

        /// <summary>Legacy GetYearAndMonthDropdownForIncome.</summary>
        private async Task<YearAndMonthDropdownForItemDto> GetYearAndMonthDropdownForIncomeAsync(
            string schoolId,
            CancellationToken ct)
        {
            var months = await GetMonthDropDownForIncomeAsync(schoolId, ct);
            var years = await GetYearDropdownForIncomeAsync(schoolId, ct);
            return new YearAndMonthDropdownForItemDto
            {
                Months = months,
                Years = years,
            };
        }

        /// <summary>Legacy GetYearAndMonthDropdownForExpenditure.</summary>
        private async Task<YearAndMonthDropdownForItemDto> GetYearAndMonthDropdownForExpenditureAsync(
            string schoolId,
            CancellationToken ct)
        {
            var months = await GetMonthDropDownForExpenditureAsync(schoolId, ct);
            var years = await GetYearDropdownForExpenditureAsync(schoolId, ct);
            return new YearAndMonthDropdownForItemDto
            {
                Months = months,
                Years = years,
            };
        }

        /// <summary>Legacy GetYearDropdownForExpenditure.</summary>
        private async Task<List<SelectListItem>> GetYearDropdownForExpenditureAsync(
            string schoolId,
            CancellationToken ct)
        {
            var expenses = await _uow.Repository<XyExpenses>().GetAllAsync(
                Filter: n => n.Week != null && n.Week.SchoolId == schoolId,
                IncludeProperties: "Week",
                cancellationToken: ct);

            return expenses
                .OrderByDescending(v => v.CapturedDate.Year)
                .GroupBy(c => c.CapturedDate.Year)
                .Select(c => c.First())
                .Select(b => new SelectListItem
                {
                    Text = b.CapturedDate.Year.ToString(),
                    Value = b.CapturedDate.Year.ToString(),
                })
                .ToList();
        }

        /// <summary>Legacy GetMonthDropDownForExpenditure.</summary>
        private async Task<List<SelectListItem>> GetMonthDropDownForExpenditureAsync(
            string schoolId,
            CancellationToken ct)
        {
            var expenses = await _uow.Repository<XyExpenses>().GetAllAsync(
                Filter: f => f.Week != null && f.Week.SchoolId == schoolId,
                IncludeProperties: "Week",
                cancellationToken: ct);

            var monthList = expenses
                .OrderByDescending(x => x.CapturedDate.Month)
                .GroupBy(c => c.CapturedDate.Month)
                .Select(c => c.First())
                .Select(b => new SelectListItem
                {
                    Value = b.CapturedDate.Month.ToString(),
                    Text = b.CapturedDate.Month.ToString(),
                })
                .ToList();

            foreach (var each in monthList)
            {
                if (byte.TryParse(each.Text, out var monthValue))
                {
                    each.Text = EnumsAndExtensionMethods.GetMonth(monthValue);
                }
            }

            return monthList;
        }

        /// <summary>Legacy GetYearDropdownForIncome.</summary>
        private async Task<List<SelectListItem>> GetYearDropdownForIncomeAsync(
            string schoolId,
            CancellationToken ct)
        {
            var incomes = await _uow.Repository<XyIncome>().GetAllAsync(
                Filter: n => n.Week != null && n.Week.SchoolId == schoolId,
                IncludeProperties: "Week",
                cancellationToken: ct);

            return incomes
                .OrderByDescending(v => v.CapturedDate.Year)
                .GroupBy(c => c.CapturedDate.Year)
                .Select(c => c.First())
                .Select(b => new SelectListItem
                {
                    Text = b.CapturedDate.Year.ToString(),
                    Value = b.CapturedDate.Year.ToString(),
                })
                .ToList();
        }

        /// <summary>Legacy GetMonthDropDownForIncome.</summary>
        private async Task<List<SelectListItem>> GetMonthDropDownForIncomeAsync(
            string schoolId,
            CancellationToken ct)
        {
            var incomes = await _uow.Repository<XyIncome>().GetAllAsync(
                Filter: f => f.Week != null && f.Week.SchoolId == schoolId,
                IncludeProperties: "Week",
                cancellationToken: ct);

            var expensesList = incomes
                .OrderByDescending(x => x.CapturedDate.Month)
                .GroupBy(c => c.CapturedDate.Month)
                .Select(c => c.First())
                .Select(b => new SelectListItem
                {
                    Value = b.CapturedDate.Month.ToString(),
                    Text = b.CapturedDate.Month.ToString(),
                })
                .ToList();

            foreach (var each in expensesList)
            {
                if (byte.TryParse(each.Text, out var monthValue))
                {
                    each.Text = EnumsAndExtensionMethods.GetMonth(monthValue);
                }
            }

            return expensesList;
        }

        /// <inheritdoc cref="IAccountingService.GetOptionalFeesCategoryListAsync" />
        public async Task<List<AccountingOptionalFeesCategoryDto>> GetOptionalFeesCategoryListAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.GetOptionalFeesCategoryList
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var expensesCategoryList = (await _uow.Repository<XyOPtionalChargeCategory>().WhereAsync(
                x => x.SchoolId == schoolId,
                ct)).ToList();

            if (expensesCategoryList.Count == 0)
            {
                return new List<AccountingOptionalFeesCategoryDto>();
            }

            return expensesCategoryList
                .Select(v => new AccountingOptionalFeesCategoryDto
                {
                    Id = v.Id,
                    CategoryName = v.CategoryName,
                    SchoolId = v.SchoolId,
                })
                .ToList();
        }

        /// <inheritdoc cref="IAccountingService.UpdateOptionalFeeCategoryAsync" />
        public async Task<AccountingOptionalFeesCategoryMutationResultDto> UpdateOptionalFeeCategoryAsync(
            string userId,
            AccountingOptionalFeesCategoryDto model,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.UpdateOptionalFeeCategory
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (model == null || string.IsNullOrWhiteSpace(model.Id))
            {
                return new AccountingOptionalFeesCategoryMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            var entity = await _uow.Repository<XyOPtionalChargeCategory>().FindAsync(
                x => x.Id == model.Id && x.SchoolId == schoolId,
                ct);
            if (entity == null)
            {
                return new AccountingOptionalFeesCategoryMutationResultDto
                {
                    Status = 1,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            entity.CategoryName = (model.CategoryName ?? string.Empty).Trim();
            _uow.Repository<XyOPtionalChargeCategory>().Update(entity);
            await _uow.SaveChangesAsync(ct);

            return new AccountingOptionalFeesCategoryMutationResultDto
            {
                Status = 0,
                Message = "The changes has been succcessfully saved.",
            };
        }

        /// <inheritdoc cref="IAccountingService.DeleteOptionalFeeCategoryByIdAsync" />
        public async Task<AccountingOptionalFeesCategoryMutationResultDto> DeleteOptionalFeeCategoryByIdAsync(
            string userId,
            string id,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.DeleteOptionalFeeCategoryById
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(id))
            {
                return new AccountingOptionalFeesCategoryMutationResultDto
                {
                    Status = 1,
                    Message =
                        "Failed! The category has optional-fee item(s) to it and therefore, we can not delete it.",
                };
            }

            var entity = await _uow.Repository<XyOPtionalChargeCategory>().FindAsync(
                x => x.Id == id && x.SchoolId == schoolId,
                ct);
            if (entity == null)
            {
                return new AccountingOptionalFeesCategoryMutationResultDto
                {
                    Status = 1,
                    Message =
                        "Failed! The category has optional-fee item(s) to it and therefore, we can not delete it.",
                };
            }

            var linkedItem = await _uow.Repository<OptionalXyChargeItems>().FindAsync(
                c => c.ItemCategoryId == id,
                ct);
            if (linkedItem != null)
            {
                return new AccountingOptionalFeesCategoryMutationResultDto
                {
                    Status = 1,
                    Message =
                        "Failed! The category has optional-fee item(s) to it and therefore, we can not delete it.",
                };
            }

            _uow.Repository<XyOPtionalChargeCategory>().Delete(entity);
            await _uow.SaveChangesAsync(ct);

            return new AccountingOptionalFeesCategoryMutationResultDto
            {
                Status = 0,
                Message = "Record deleted Successfully.",
            };
        }

        /// <inheritdoc cref="IAccountingService.CreateOptionalFeeItemAsync" />
        public async Task<AccountingCreateOptionalFeesItemResultDto> CreateOptionalFeeItemAsync(
            string userId,
            AccountingOptionalFeesItemCreateDto model,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.CreateOptionalFeeItem
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var itemName = (model?.ItemName ?? string.Empty).Trim();
            var itemCategoryId = (model?.ItemCategoryId ?? string.Empty).Trim();
            var cost = model?.Cost ?? 0d;

            var category = await _uow.Repository<XyOPtionalChargeCategory>().FindAsync(
                x => x.Id == itemCategoryId && x.SchoolId == schoolId,
                ct);
            if (category == null)
            {
                return new AccountingCreateOptionalFeesItemResultDto
                {
                    Status = 2,
                    Message = "Failed! The Item name you have entered already exist.",
                };
            }

            var currentTermModel = await _uow.Repository<TxYearlyAndTermlyStatus>().FindAsync(
                x => x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current
                     && x.Session!.SchoolInfoId == schoolId,
                ct,
                x => x.Session!);

            if (currentTermModel == null)
            {
                return new AccountingCreateOptionalFeesItemResultDto
                {
                    Status = 1,
                    Message = "All terms' status are 'previous'. Run new assessment preparation before this.",
                };
            }

            var doesItemAlreadyExist = await _uow.Repository<OptionalXyChargeItems>().FindAsync(
                x => x.ItemName == itemName
                     && x.ItemCategoryId == itemCategoryId
                     && x.YearlyAndTermlyStatusId == currentTermModel.Id,
                ct);
            if (doesItemAlreadyExist != null)
            {
                return new AccountingCreateOptionalFeesItemResultDto
                {
                    Status = 2,
                    Message = "Failed! The Item name you have entered already exist.",
                };
            }

            var now = DateTime.UtcNow;
            var newItem = new OptionalXyChargeItems
            {
                Id = Guid.NewGuid().ToString(),
                ItemCategoryId = itemCategoryId,
                ItemName = itemName,
                Cost = cost,
                YearlyAndTermlyStatusId = currentTermModel.Id,
                CreatedDate = now,
                UpdatedDate = now,
                CreatedBy = userId ?? string.Empty,
                UpdatedBy = userId ?? string.Empty,
            };
            _uow.Repository<OptionalXyChargeItems>().Add(newItem);
            await _uow.SaveChangesAsync(ct);

            // Legacy: after insert, auto-assign to students already linked to this category.
            var categoryItemIds = (await _uow.Repository<OptionalXyChargeItems>().WhereAsync(
                x => x.ItemCategoryId == itemCategoryId,
                ct)).Select(x => x.Id).ToList();

            var assignmentsForCategory = categoryItemIds.Count == 0
                ? new List<StudentOPtionalChargesAssignment>()
                : await _uow.Repository<StudentOPtionalChargesAssignment>().WhereAsync(
                    x => categoryItemIds.Contains(x.OptionalFeesItemId),
                    ct);

            var uniqueStudents = assignmentsForCategory
                .GroupBy(x => x.StudentId)
                .Select(g => g.First())
                .ToList();

            foreach (var each in uniqueStudents)
            {
                _uow.Repository<StudentOPtionalChargesAssignment>().Add(
                    new StudentOPtionalChargesAssignment
                    {
                        Id = Guid.NewGuid().ToString(),
                        OptionalFeesItemId = newItem.Id,
                        StudentId = each.StudentId,
                    });
            }

            var currentTermCategoryItemIds = (await _uow.Repository<OptionalXyChargeItems>().WhereAsync(
                x => x.ItemCategoryId == itemCategoryId
                     && x.YearlyAndTermlyStatusId == currentTermModel.Id,
                ct)).Select(x => x.Id).ToList();

            var list23 = currentTermCategoryItemIds.Count == 0
                ? new List<StudentTermlyOptionalCharges>()
                : await _uow.Repository<StudentTermlyOptionalCharges>().WhereAsync(
                    f => currentTermCategoryItemIds.Contains(f.OptionalFeesItemId),
                    ct);

            if (list23.Count > 0)
            {
                var uniqueItem = list23
                    .GroupBy(x => x.StudentId)
                    .Select(g => g.First())
                    .ToList();

                foreach (var each in uniqueItem)
                {
                    _uow.Repository<StudentTermlyOptionalCharges>().Add(
                        new StudentTermlyOptionalCharges
                        {
                            Id = Guid.NewGuid().ToString(),
                            ClassArmId = each.ClassArmId,
                            StudentId = each.StudentId,
                            OptionalFeesItemId = newItem.Id,
                            AsignedDate = DateTime.UtcNow,
                        });
                }
            }

            if (uniqueStudents.Count > 0 || list23.Count > 0)
            {
                await _uow.SaveChangesAsync(ct);
            }

            return new AccountingCreateOptionalFeesItemResultDto
            {
                Status = 0,
                Message = "The item has successfully been saved.",
            };
        }

        /// <inheritdoc cref="IAccountingService.GetOptionalFeeItemListAsync" />
        public async Task<List<AccountingOptionalFeesItemDto>> GetOptionalFeeItemListAsync(
            string userId,
            string? categoryId = null,
            CancellationToken ct = default)
        {
            // Legacy GetOptionalFeeItemList / GetOptionalItemsByCategoryId
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var items = string.IsNullOrWhiteSpace(categoryId)
                ? await _uow.Repository<OptionalXyChargeItems>().WhereAsync(
                    x => x.ItemCategory != null && x.ItemCategory.SchoolId == schoolId,
                    ct,
                    x => x.ItemCategory!)
                : await _uow.Repository<OptionalXyChargeItems>().WhereAsync(
                    x => x.ItemCategory != null
                         && x.ItemCategory.SchoolId == schoolId
                         && x.ItemCategoryId == categoryId,
                    ct,
                    x => x.ItemCategory!);

            return items
                .OrderByDescending(x => x.CreatedDate)
                .Select(MapOptionalFeesItem)
                .ToList();
        }

        /// <inheritdoc cref="IAccountingService.GetOptionalFeeItemByIdAsync" />
        public async Task<AccountingOptionalFeesItemDto?> GetOptionalFeeItemByIdAsync(
            string userId,
            string itemId,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.GetOptionalFeeItemById (+ school scope)
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var item = await _uow.Repository<OptionalXyChargeItems>().FindAsync(
                x => x.Id == itemId
                     && x.ItemCategory != null
                     && x.ItemCategory.SchoolId == schoolId,
                ct,
                x => x.ItemCategory!);
            return item == null ? null : MapOptionalFeesItem(item);
        }

        /// <inheritdoc cref="IAccountingService.UpdateOptionalFeeItemAsync" />
        public async Task<AccountingOptionalFeesItemMutationResultDto> UpdateOptionalFeeItemAsync(
            string userId,
            AccountingOptionalFeesItemUpdateDto model,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.UpdateOptionalFeeItem
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (model == null || string.IsNullOrWhiteSpace(model.Id))
            {
                return FailItemMutation("Failed! Unable to complete the process.");
            }

            var itemName = (model.ItemName ?? string.Empty).Trim();
            var itemCategoryId = (model.ItemCategoryId ?? string.Empty).Trim();
            var termId = (model.YearlyAndTermlyStatusId ?? string.Empty).Trim();

            var existingDuplicate = await _uow.Repository<OptionalXyChargeItems>().FindAsync(
                x => x.ItemName == itemName
                     && x.ItemCategoryId == itemCategoryId
                     && x.YearlyAndTermlyStatusId == termId,
                ct);
            if (existingDuplicate != null && existingDuplicate.Id != model.Id)
            {
                return new AccountingOptionalFeesItemMutationResultDto
                {
                    Status = 2,
                    Message = "Failed! Unable to complete the process.",
                };
            }

            var doesTheCategoryIdChanged = await _uow.Repository<OptionalXyChargeItems>().FindAsync(
                x => x.Id == model.Id
                     && x.ItemCategory != null
                     && x.ItemCategory.SchoolId == schoolId,
                ct,
                x => x.ItemCategory!);
            if (doesTheCategoryIdChanged == null)
            {
                return FailItemMutation("Failed! Unable to complete the process.");
            }

            if (doesTheCategoryIdChanged.ItemCategoryId != itemCategoryId)
            {
                var hasPaymentBeenMadeUponTheOptionalCharge =
                    await _uow.Repository<StudentTermlyOptionalPayment>().FindAsync(
                        x => x.StudentTermlyOptionalCharges != null
                             && x.StudentTermlyOptionalCharges.OptionalFeesItemId == model.Id,
                        ct,
                        x => x.StudentTermlyOptionalCharges!);
                if (hasPaymentBeenMadeUponTheOptionalCharge != null)
                {
                    return new AccountingOptionalFeesItemMutationResultDto
                    {
                        Status = 3,
                        Message = "Failed! Unable to complete the process.",
                    };
                }

                var list2 = await _uow.Repository<StudentOPtionalChargesAssignment>().WhereAsync(
                    x => x.OptionalFeesItemId == model.Id,
                    ct);
                foreach (var each in list2)
                {
                    _uow.Repository<StudentOPtionalChargesAssignment>().Delete(each);
                }

                var list234 = await _uow.Repository<StudentTermlyOptionalCharges>().WhereAsync(
                    f => f.OptionalFeesItemId == model.Id,
                    ct);
                foreach (var each in list234)
                {
                    _uow.Repository<StudentTermlyOptionalCharges>().Delete(each);
                }

                doesTheCategoryIdChanged.ItemCategoryId = itemCategoryId;
                doesTheCategoryIdChanged.ItemName = itemName;
                doesTheCategoryIdChanged.Cost = model.Cost;
                doesTheCategoryIdChanged.YearlyAndTermlyStatusId = string.IsNullOrWhiteSpace(termId)
                    ? doesTheCategoryIdChanged.YearlyAndTermlyStatusId
                    : termId;
                doesTheCategoryIdChanged.UpdatedDate = DateTime.UtcNow;
                doesTheCategoryIdChanged.UpdatedBy = userId ?? string.Empty;
                _uow.Repository<OptionalXyChargeItems>().Update(doesTheCategoryIdChanged);
                await _uow.SaveChangesAsync(ct);

                var categoryItemIds = (await _uow.Repository<OptionalXyChargeItems>().WhereAsync(
                    x => x.ItemCategoryId == itemCategoryId,
                    ct)).Select(x => x.Id).ToList();

                var hasItAlreadyBeenAssigned = categoryItemIds.Count == 0
                    ? null
                    : await _uow.Repository<StudentOPtionalChargesAssignment>().FindAsync(
                        x => categoryItemIds.Contains(x.OptionalFeesItemId),
                        ct);

                if (hasItAlreadyBeenAssigned != null)
                {
                    var list = await _uow.Repository<StudentOPtionalChargesAssignment>().WhereAsync(
                        x => categoryItemIds.Contains(x.OptionalFeesItemId),
                        ct);
                    foreach (var each in list.GroupBy(x => x.StudentId).Select(g => g.First()))
                    {
                        _uow.Repository<StudentOPtionalChargesAssignment>().Add(
                            new StudentOPtionalChargesAssignment
                            {
                                Id = Guid.NewGuid().ToString(),
                                OptionalFeesItemId = model.Id,
                                StudentId = each.StudentId,
                            });
                    }

                    var termCategoryItemIds = (await _uow.Repository<OptionalXyChargeItems>().WhereAsync(
                        x => x.ItemCategoryId == itemCategoryId
                             && x.YearlyAndTermlyStatusId == doesTheCategoryIdChanged.YearlyAndTermlyStatusId,
                        ct)).Select(x => x.Id).ToList();

                    var list23 = termCategoryItemIds.Count == 0
                        ? new List<StudentTermlyOptionalCharges>()
                        : await _uow.Repository<StudentTermlyOptionalCharges>().WhereAsync(
                            f => termCategoryItemIds.Contains(f.OptionalFeesItemId),
                            ct);

                    if (list23.Count > 0)
                    {
                        foreach (var each in list23.GroupBy(x => x.StudentId).Select(g => g.First()))
                        {
                            _uow.Repository<StudentTermlyOptionalCharges>().Add(
                                new StudentTermlyOptionalCharges
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    ClassArmId = each.ClassArmId,
                                    StudentId = each.StudentId,
                                    OptionalFeesItemId = model.Id,
                                    AsignedDate = DateTime.UtcNow,
                                });
                        }
                    }

                    await _uow.SaveChangesAsync(ct);
                }
            }
            else
            {
                doesTheCategoryIdChanged.ItemName = itemName;
                doesTheCategoryIdChanged.Cost = model.Cost;
                doesTheCategoryIdChanged.UpdatedDate = DateTime.UtcNow;
                doesTheCategoryIdChanged.UpdatedBy = userId ?? string.Empty;
                _uow.Repository<OptionalXyChargeItems>().Update(doesTheCategoryIdChanged);
                await _uow.SaveChangesAsync(ct);
            }

            return new AccountingOptionalFeesItemMutationResultDto
            {
                Status = 0,
                Message = "The changes has been succcessfully saved.",
            };
        }

        /// <inheritdoc cref="IAccountingService.DeleteOptionalFeeItemByIdAsync" />
        public async Task<AccountingOptionalFeesItemMutationResultDto> DeleteOptionalFeeItemByIdAsync(
            string userId,
            string id,
            CancellationToken ct = default)
        {
            // Legacy AccountingService.DeleteOptionalFeeItemById
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(id))
            {
                return FailItemMutation(
                    "Failed! The category has optional-fee item(s) to it and therefore, we can not delete it.");
            }

            var entity = await _uow.Repository<OptionalXyChargeItems>().FindAsync(
                x => x.Id == id
                     && x.ItemCategory != null
                     && x.ItemCategory.SchoolId == schoolId,
                ct,
                x => x.ItemCategory!);
            if (entity == null)
            {
                return FailItemMutation(
                    "Failed! The category has optional-fee item(s) to it and therefore, we can not delete it.");
            }

            var status = await _uow.Repository<StudentTermlyOptionalPayment>().FindAsync(
                c => c.StudentTermlyOptionalCharges != null
                     && c.StudentTermlyOptionalCharges.OptionalFeesItemId == id,
                ct,
                c => c.StudentTermlyOptionalCharges!);
            if (status != null)
            {
                return FailItemMutation(
                    "Failed! The category has optional-fee item(s) to it and therefore, we can not delete it.");
            }

            var list2 = await _uow.Repository<StudentOPtionalChargesAssignment>().WhereAsync(
                x => x.OptionalFeesItemId == id,
                ct);
            foreach (var each in list2)
            {
                _uow.Repository<StudentOPtionalChargesAssignment>().Delete(each);
            }

            var list3 = await _uow.Repository<StudentTermlyOptionalCharges>().WhereAsync(
                c => c.OptionalFeesItemId == id,
                ct);
            foreach (var each in list3)
            {
                _uow.Repository<StudentTermlyOptionalCharges>().Delete(each);
            }

            _uow.Repository<OptionalXyChargeItems>().Delete(entity);
            await _uow.SaveChangesAsync(ct);

            return new AccountingOptionalFeesItemMutationResultDto
            {
                Status = 0,
                Message = "Record deleted Successfully.",
            };
        }

        private static AccountingOptionalFeesItemDto MapOptionalFeesItem(OptionalXyChargeItems item) =>
            new()
            {
                Id = item.Id,
                ItemCategoryId = item.ItemCategoryId,
                CategoryName = item.ItemCategory?.CategoryName ?? string.Empty,
                ItemName = item.ItemName,
                Cost = item.Cost,
                YearlyAndTermlyStatusId = item.YearlyAndTermlyStatusId,
                CreatedDate = item.CreatedDate,
            };

        private static AccountingOptionalFeesItemMutationResultDto FailItemMutation(string message) =>
            new()
            {
                Status = 1,
                Message = message,
            };

        /// <inheritdoc cref="IAccountingService.GetOptionalChargesAssignmentFormAsync" />
        public async Task<AccountingOptionalChargesAssignmentFormDto> GetOptionalChargesAssignmentFormAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy StudentFeesController.StudentOptionalChargesAssignmentIndex
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var categories = (await _uow.Repository<XyOPtionalChargeCategory>().WhereAsync(
                    x => x.SchoolId == schoolId,
                    ct))
                .OrderBy(x => x.CategoryName)
                .Select(x => new SelectListItem { Text = x.CategoryName, Value = x.Id })
                .ToList();

            if (categories.Count == 0)
            {
                categories.Add(new SelectListItem { Text = "No Optional Fee category", Value = "-1" });
            }

            var classes = await GetSchoolClassDropdownAsync(schoolId, ct);
            var arms = new List<SelectListItem>();
            if (classes.Count > 0)
            {
                arms = await GetSchoolArmDropdownAsync(schoolId, classes[0].Value, ct);
            }

            return new AccountingOptionalChargesAssignmentFormDto
            {
                Categories = categories,
                Classes = classes,
                Arms = arms,
            };
        }

        /// <inheritdoc cref="IAccountingService.GetOptionalChargesAssignmentArmsAsync" />
        public async Task<List<SelectListItem>> GetOptionalChargesAssignmentArmsAsync(
            string userId,
            string classId,
            CancellationToken ct = default)
        {
            // Legacy RenderArmsDropdown
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            return await GetSchoolArmDropdownAsync(schoolId, classId, ct);
        }

        /// <inheritdoc cref="IAccountingService.GetOptionalChargesAssignmentStudentsAsync" />
        public async Task<AccountingOptionalChargesAssignmentStudentsDto> GetOptionalChargesAssignmentStudentsAsync(
            string userId,
            string armId,
            string categoryId,
            byte catStatus = 1,
            CancellationToken ct = default)
        {
            // Legacy GetStudentForAnArmForAnOptionalPayment (+ category filter student list)
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(armId) || armId == "0")
            {
                return new AccountingOptionalChargesAssignmentStudentsDto();
            }

            var arm = await _uow.Repository<AxClassArm>().FindAsync(
                a => a.Id == armId && a.Classs != null && a.Classs.SchoolInfoId == schoolId,
                ct,
                a => a.Classs!);
            if (arm == null)
            {
                return new AccountingOptionalChargesAssignmentStudentsDto
                {
                    Message = "No student in the selected class-arm.",
                };
            }

            var studentsQuery = await _uow.Repository<TxStudentDetail>().WhereAsync(
                s => s.ClassArmId == armId
                     && s.SchoolInfoId == schoolId
                     && s.Status != StudentStatus.Previous,
                ct,
                s => s.User!);

            if (catStatus == 2)
            {
                studentsQuery = studentsQuery.Where(s => s.ClassCategory == ClassCategoryStatus.Art).ToList();
            }
            else if (catStatus == 3)
            {
                studentsQuery = studentsQuery.Where(s => s.ClassCategory == ClassCategoryStatus.Science).ToList();
            }
            else if (catStatus == 4)
            {
                studentsQuery = studentsQuery.Where(s => s.ClassCategory == ClassCategoryStatus.Commercial).ToList();
            }

            var assignedIds = new List<string>();
            if (!string.IsNullOrWhiteSpace(categoryId) && categoryId != "-1")
            {
                var assignments = await _uow.Repository<StudentOPtionalChargesAssignment>().WhereAsync(
                    s => s.StudentDetail != null
                         && s.StudentDetail.ClassArmId == armId
                         && s.OptionalFeesItem != null
                         && s.OptionalFeesItem.ItemCategoryId == categoryId,
                    ct,
                    s => s.StudentDetail!,
                    s => s.OptionalFeesItem!);
                assignedIds = assignments.Select(r => r.StudentId).Distinct().ToList();
            }

            var assignedSet = assignedIds.ToHashSet(StringComparer.Ordinal);
            var students = studentsQuery
                .OrderBy(x => x.ClassCategory)
                .ThenBy(x => x.User?.FullName)
                .Select(x => new AccountingOptionalAssignmentStudentDto
                {
                    Id = x.UserId,
                    FullName = x.User?.FullName ?? string.Empty,
                    IdNo = x.IdNo,
                    ClassCategory = (byte)x.ClassCategory,
                    Assigned = assignedSet.Contains(x.UserId),
                })
                .ToList();

            if (students.Count == 0)
            {
                return new AccountingOptionalChargesAssignmentStudentsDto
                {
                    Students = students,
                    AssignedStudentIds = assignedIds,
                    Message = catStatus > 1
                        ? "No student in the selected category in the selected class-arm."
                        : "No student in the selected class-arm.",
                };
            }

            return new AccountingOptionalChargesAssignmentStudentsDto
            {
                Students = students,
                AssignedStudentIds = assignedIds,
            };
        }

        /// <inheritdoc cref="IAccountingService.AssignOptionalChargesAsync" />
        public async Task<AccountingOptionalChargesAssignmentResultDto> AssignOptionalChargesAsync(
            string userId,
            AccountingOptionalChargesAssignmentRequestDto model,
            CancellationToken ct = default)
        {
            // Legacy AssignOptionalFeeToArmStudentsOrStudentsInCategory
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var armId = (model?.ArmId ?? string.Empty).Trim();
            var categoryId = (model?.CategoryId ?? string.Empty).Trim();
            var studentIds = (model?.StudentIds ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (string.IsNullOrWhiteSpace(armId) || armId == "0" || string.IsNullOrWhiteSpace(categoryId) || categoryId == "-1")
            {
                return new AccountingOptionalChargesAssignmentResultDto
                {
                    Succeeded = false,
                    Message = "Failed! Un-able to complete the process.",
                };
            }

            var arm = await _uow.Repository<AxClassArm>().FindAsync(
                a => a.Id == armId && a.Classs != null && a.Classs.SchoolInfoId == schoolId,
                ct,
                a => a.Classs!);
            var category = await _uow.Repository<XyOPtionalChargeCategory>().FindAsync(
                c => c.Id == categoryId && c.SchoolId == schoolId,
                ct);
            if (arm == null || category == null)
            {
                return new AccountingOptionalChargesAssignmentResultDto
                {
                    Succeeded = false,
                    Message = "Failed! Un-able to complete the process.",
                };
            }

            var listOfOptionalFeesItemsOnTheSelectedCategory =
                await _uow.Repository<OptionalXyChargeItems>().WhereAsync(
                    c => c.ItemCategoryId == categoryId,
                    ct);

            foreach (var eachItem in listOfOptionalFeesItemsOnTheSelectedCategory)
            {
                foreach (var eachStudentId in studentIds)
                {
                    var hasItAlreadyBeenAssigned =
                        await _uow.Repository<StudentOPtionalChargesAssignment>().FindAsync(
                            x => x.OptionalFeesItemId == eachItem.Id && x.StudentId == eachStudentId,
                            ct);
                    if (hasItAlreadyBeenAssigned == null)
                    {
                        _uow.Repository<StudentOPtionalChargesAssignment>().Add(
                            new StudentOPtionalChargesAssignment
                            {
                                Id = Guid.NewGuid().ToString(),
                                OptionalFeesItemId = eachItem.Id,
                                StudentId = eachStudentId,
                            });
                    }

                    var hasItAlreadyBeenAssigned2 =
                        await _uow.Repository<StudentTermlyOptionalCharges>().FindAsync(
                            x => x.OptionalFeesItemId == eachItem.Id && x.StudentId == eachStudentId,
                            ct);
                    if (hasItAlreadyBeenAssigned2 == null)
                    {
                        _uow.Repository<StudentTermlyOptionalCharges>().Add(
                            new StudentTermlyOptionalCharges
                            {
                                Id = Guid.NewGuid().ToString(),
                                OptionalFeesItemId = eachItem.Id,
                                StudentId = eachStudentId,
                                ClassArmId = armId,
                                AsignedDate = DateTime.UtcNow,
                            });
                    }
                }
            }

            var studentsInTheClassArm = await _uow.Repository<StudentOPtionalChargesAssignment>().WhereAsync(
                x => x.StudentDetail != null
                     && x.StudentDetail.ClassArmId == armId
                     && x.OptionalFeesItem != null
                     && x.OptionalFeesItem.ItemCategoryId == categoryId,
                ct,
                x => x.StudentDetail!,
                x => x.OptionalFeesItem!);
            foreach (var eachItem in studentsInTheClassArm)
            {
                if (studentIds.All(id => id != eachItem.StudentId))
                {
                    _uow.Repository<StudentOPtionalChargesAssignment>().Delete(eachItem);
                }
            }

            var studentsInTheClassArm2 = await _uow.Repository<StudentTermlyOptionalCharges>().WhereAsync(
                x => x.StudentDetail != null
                     && x.StudentDetail.ClassArmId == armId
                     && x.OptionalFeesItem != null
                     && x.OptionalFeesItem.ItemCategoryId == categoryId,
                ct,
                x => x.StudentDetail!,
                x => x.OptionalFeesItem!);
            foreach (var eachItem in studentsInTheClassArm2)
            {
                if (studentIds.All(id => id != eachItem.StudentId))
                {
                    _uow.Repository<StudentTermlyOptionalCharges>().Delete(eachItem);
                }
            }

            await _uow.SaveChangesAsync(ct);
            return new AccountingOptionalChargesAssignmentResultDto
            {
                Succeeded = true,
                Message = "Your selections has Successfully been updated",
            };
        }

        /// <inheritdoc cref="IAccountingService.UnassignOptionalChargesAsync" />
        public async Task<AccountingOptionalChargesAssignmentResultDto> UnassignOptionalChargesAsync(
            string userId,
            AccountingOptionalChargesAssignmentRequestDto model,
            CancellationToken ct = default)
        {
            // Legacy UnAssignOptionalFeeToArmStudentsOrStudentsInCategory
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var armId = (model?.ArmId ?? string.Empty).Trim();
            var categoryId = (model?.CategoryId ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(armId) || armId == "0" || string.IsNullOrWhiteSpace(categoryId) || categoryId == "-1")
            {
                return new AccountingOptionalChargesAssignmentResultDto
                {
                    Succeeded = false,
                    Message = "Failed! Un-able to complete the process.",
                };
            }

            var arm = await _uow.Repository<AxClassArm>().FindAsync(
                a => a.Id == armId && a.Classs != null && a.Classs.SchoolInfoId == schoolId,
                ct,
                a => a.Classs!);
            var category = await _uow.Repository<XyOPtionalChargeCategory>().FindAsync(
                c => c.Id == categoryId && c.SchoolId == schoolId,
                ct);
            if (arm == null || category == null)
            {
                return new AccountingOptionalChargesAssignmentResultDto
                {
                    Succeeded = false,
                    Message = "Failed! Un-able to complete the process.",
                };
            }

            var offeringStudentsInTheClassArm = await _uow.Repository<StudentOPtionalChargesAssignment>().WhereAsync(
                x => x.StudentDetail != null
                     && x.StudentDetail.ClassArmId == armId
                     && x.OptionalFeesItem != null
                     && x.OptionalFeesItem.ItemCategoryId == categoryId,
                ct,
                x => x.StudentDetail!,
                x => x.OptionalFeesItem!);
            foreach (var eachItem in offeringStudentsInTheClassArm)
            {
                _uow.Repository<StudentOPtionalChargesAssignment>().Delete(eachItem);
            }

            var offeringStudentsInTheClassArm2 = await _uow.Repository<StudentTermlyOptionalCharges>().WhereAsync(
                x => x.StudentDetail != null
                     && x.StudentDetail.ClassArmId == armId
                     && x.OptionalFeesItem != null
                     && x.OptionalFeesItem.ItemCategoryId == categoryId,
                ct,
                x => x.StudentDetail!,
                x => x.OptionalFeesItem!);
            foreach (var eachItem in offeringStudentsInTheClassArm2)
            {
                _uow.Repository<StudentTermlyOptionalCharges>().Delete(eachItem);
            }

            await _uow.SaveChangesAsync(ct);
            return new AccountingOptionalChargesAssignmentResultDto
            {
                Succeeded = true,
                Message = "succeed",
            };
        }

        /// <inheritdoc cref="IAccountingService.GetOptionalPaymentDefaultersFormAsync" />
        public async Task<AccountingOptionalPaymentDefaultersFormDto> GetOptionalPaymentDefaultersFormAsync(
            string userId,
            CancellationToken ct = default)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var sessions = (await _uow.Repository<TxAcademicSession>().WhereAsync(
                    s => s.SchoolInfoId == schoolId,
                    ct))
                .OrderByDescending(s => s.Session)
                .Select(s => new SelectListItem { Text = s.Session, Value = s.Id })
                .ToList();

            var result = new AccountingOptionalPaymentDefaultersFormDto { Sessions = sessions };
            if (sessions.Count == 1)
            {
                var only = sessions[0];
                result.Sessions = new List<SelectListItem>
                {
                    new() { Text = "Select", Value = "-1" },
                    only,
                };
            }

            // Legacy ResultTypeMenu uses session.Last() after OrderByDescending(Value) — newest first here.
            if (sessions.Count > 0)
            {
                result.Terms = (await GetOptionalPaymentDefaultersTermsInternalAsync(
                    sessions[0].Value,
                    ct)).ToList();
            }

            return result;
        }

        /// <inheritdoc cref="IAccountingService.GetOptionalPaymentDefaultersTermsAsync" />
        public async Task<IList<SelectListItem>> GetOptionalPaymentDefaultersTermsAsync(
            string userId,
            string sessionId,
            CancellationToken ct = default)
        {
            _ = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            return await GetOptionalPaymentDefaultersTermsInternalAsync(sessionId, ct);
        }

        /// <inheritdoc cref="IAccountingService.GetTermlyOptionalPaymentDefaultersAsync" />
        public async Task<AccountingOptionalPaymentDefaultersDto> GetTermlyOptionalPaymentDefaultersAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            CancellationToken ct = default)
        {
            // Legacy StudentTermlyPaymentDefaulters
            return await BuildOptionalPaymentDefaultersAsync(
                userId,
                sessionId,
                termStatus,
                filterByTerm: true,
                ct);
        }

        /// <inheritdoc cref="IAccountingService.GetYearlyOptionalPaymentDefaultersAsync" />
        public async Task<AccountingOptionalPaymentDefaultersDto> GetYearlyOptionalPaymentDefaultersAsync(
            string userId,
            string sessionId,
            CancellationToken ct = default)
        {
            // Legacy StudentSessionalPaymentDefaulters
            return await BuildOptionalPaymentDefaultersAsync(
                userId,
                sessionId,
                termStatus: default,
                filterByTerm: false,
                ct);
        }

        /// <inheritdoc cref="IAccountingService.GetTermlyOptionalPaymentReportAsync" />
        public async Task<AccountingOptionalPaymentReportDto> GetTermlyOptionalPaymentReportAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            CancellationToken ct = default)
        {
            // Legacy StudentTermlyPaymentReport
            return await BuildOptionalPaymentReportAsync(
                userId,
                sessionId,
                termStatus,
                filterByTerm: true,
                ct);
        }

        /// <inheritdoc cref="IAccountingService.GetYearlyOptionalPaymentReportAsync" />
        public async Task<AccountingOptionalPaymentReportDto> GetYearlyOptionalPaymentReportAsync(
            string userId,
            string sessionId,
            CancellationToken ct = default)
        {
            // Legacy StudentSessionalPaymentReport
            return await BuildOptionalPaymentReportAsync(
                userId,
                sessionId,
                termStatus: default,
                filterByTerm: false,
                ct);
        }

        /// <inheritdoc cref="IAccountingService.GetOptionalPaymentHomeFormAsync" />
        public async Task<AccountingOptionalPaymentHomeFormDto> GetOptionalPaymentHomeFormAsync(
            string userId,
            CancellationToken ct = default)
        {
            // Legacy StudentOptionalPaymentIndex + ResultTypeMenu dropdowns
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var sessions = (await _uow.Repository<TxAcademicSession>().WhereAsync(
                    s => s.SchoolInfoId == schoolId,
                    ct))
                .OrderByDescending(s => s.Session)
                .Select(s => new SelectListItem { Text = s.Session, Value = s.Id })
                .ToList();

            if (sessions.Count == 1)
            {
                var only = sessions[0];
                sessions = new List<SelectListItem>
                {
                    new() { Text = "Select", Value = "-1" },
                    only,
                };
            }

            var terms = sessions.Count > 0
                ? (await GetOptionalPaymentDefaultersTermsInternalAsync(sessions[0].Value, ct)).ToList()
                : new List<SelectListItem>();

            // Legacy StudentIndexClassAndArmDropDown: Value = "{(byte)CurrentClass},{ClassLabel}"
            var classes = (await _uow.Repository<RxClasss>().WhereAsync(
                    c => c.SchoolInfoId == schoolId,
                    ct))
                .OrderBy(c => c.ClassName)
                .Select(c => new SelectListItem
                {
                    Text = c.ClassLabel,
                    Value = ((byte)c.ClassName) + "," + c.ClassLabel,
                })
                .ToList();

            // Legacy: unique ArmName values across the school
            var arms = (await _uow.Repository<AxClassArm>().WhereAsync(
                    a => a.Classs != null && a.Classs.SchoolInfoId == schoolId,
                    ct,
                    a => a.Classs!))
                .GroupBy(a => a.ArmName)
                .Select(g => g.First())
                .OrderBy(a => a.ArmName)
                .Select(a => new SelectListItem
                {
                    Text = a.ArmName.ToString(),
                    Value = ((byte)a.ArmName).ToString(),
                })
                .ToList();

            if (arms.Count == 1)
            {
                var only = arms[0];
                arms = new List<SelectListItem>
                {
                    new() { Text = "Select", Value = "-1" },
                    only,
                };
            }

            return new AccountingOptionalPaymentHomeFormDto
            {
                Sessions = sessions,
                Terms = terms,
                Classes = classes,
                Arms = arms,
            };
        }

        /// <inheritdoc cref="IAccountingService.LoadOptionalPaymentProcessingByArmAsync" />
        public async Task<AccountingOptionalPaymentProcessingDto> LoadOptionalPaymentProcessingByArmAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            CurrentClass currentClass,
            ArmName armName,
            string sessionText,
            CancellationToken ct = default)
        {
            // Legacy LoadForClassArmPaymentProcessing
            return await BuildOptionalPaymentProcessingAsync(
                userId,
                sessionId,
                termStatus,
                sessionText,
                currentClass,
                armName,
                filterByClass: true,
                filterByArm: true,
                ct);
        }

        /// <inheritdoc cref="IAccountingService.LoadOptionalPaymentProcessingByClassAsync" />
        public async Task<AccountingOptionalPaymentProcessingDto> LoadOptionalPaymentProcessingByClassAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            CurrentClass currentClass,
            string sessionText,
            CancellationToken ct = default)
        {
            // Legacy LoadForPerClassPaymentProcessing
            return await BuildOptionalPaymentProcessingAsync(
                userId,
                sessionId,
                termStatus,
                sessionText,
                currentClass,
                armName: default,
                filterByClass: true,
                filterByArm: false,
                ct);
        }

        /// <inheritdoc cref="IAccountingService.LoadOptionalPaymentProcessingAllAsync" />
        public async Task<AccountingOptionalPaymentProcessingDto> LoadOptionalPaymentProcessingAllAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            string sessionText,
            CancellationToken ct = default)
        {
            // Legacy LoadForAllStudentsPaymentProcessing
            return await BuildOptionalPaymentProcessingAsync(
                userId,
                sessionId,
                termStatus,
                sessionText,
                currentClass: default,
                armName: default,
                filterByClass: false,
                filterByArm: false,
                ct);
        }

        private async Task<AccountingOptionalPaymentProcessingDto> BuildOptionalPaymentProcessingAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            string sessionText,
            CurrentClass currentClass,
            ArmName armName,
            bool filterByClass,
            bool filterByArm,
            CancellationToken ct)
        {
            var result = new AccountingOptionalPaymentProcessingDto
            {
                SessionAndTerm = termStatus + ", " + (sessionText ?? string.Empty),
            };

            if (string.IsNullOrWhiteSpace(sessionId) || sessionId == "-1")
            {
                return result;
            }

            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var list = await _uow.Repository<StudentTermlyOptionalCharges>().GetAllAsync(
                Filter: x => x.OptionalFeesItem != null
                             && x.OptionalFeesItem.YearlyAndTermlyStatus != null
                             && x.OptionalFeesItem.YearlyAndTermlyStatus.Session != null
                             && x.OptionalFeesItem.YearlyAndTermlyStatus.Session.SchoolInfoId == schoolId
                             && x.OptionalFeesItem.YearlyAndTermlyStatus.SessionId == sessionId
                             && x.OptionalFeesItem.YearlyAndTermlyStatus.TermStatus == termStatus
                             && x.ClassArm != null
                             && x.ClassArm.Classs != null
                             && (!filterByClass || x.ClassArm.Classs.ClassName == currentClass)
                             && (!filterByArm || x.ClassArm.ArmName == armName),
                IncludeProperties:
                "StudentDetail.User,StudentTermlyOptionalPaymentList,OptionalFeesItem.ItemCategory,ClassArm.Classs",
                cancellationToken: ct);

            var returnList = new List<AccountingOptionalPaymentProcessingChargeDto>();
            foreach (var eachItem in list)
            {
                var charge = eachItem.OptionalFeesItem?.Cost ?? 0;
                var payment = eachItem.StudentTermlyOptionalPaymentList?.Sum(c => c.Amount) ?? 0;
                var discountList = await _uow.Repository<StudentTermlyOptionalPaymentDiscount>().WhereAsync(
                    x => x.StudentTermlyOptionalChargesId == eachItem.Id,
                    ct);
                var discount = discountList.Count > 0 ? discountList.Sum(x => x.DiscountAmount) : 0.0;
                // Legacy: if (charge > payment + discount) include
                if (charge > payment + discount)
                {
                    returnList.Add(new AccountingOptionalPaymentProcessingChargeDto
                    {
                        Id = eachItem.Id,
                        StudentId = eachItem.StudentId,
                        FullName = eachItem.StudentDetail?.User?.FullName ?? string.Empty,
                        IdNo = eachItem.StudentDetail?.IdNo ?? string.Empty,
                        ClassId = eachItem.ClassArm?.ClassId ?? string.Empty,
                        ClassLabel = eachItem.ClassArm?.Classs?.ClassLabel ?? string.Empty,
                        CurrentClass = (byte)(eachItem.ClassArm?.Classs?.ClassName ?? default),
                        ClassArmId = eachItem.ClassArmId,
                        ArmName = eachItem.ClassArm?.ArmName.ToString() ?? string.Empty,
                        ItemCategoryId = eachItem.OptionalFeesItem?.ItemCategoryId ?? string.Empty,
                        CategoryName = eachItem.OptionalFeesItem?.ItemCategory?.CategoryName ?? string.Empty,
                        ItemName = eachItem.OptionalFeesItem?.ItemName ?? string.Empty,
                        Cost = charge,
                        AmountPaid = payment,
                        DiscountTotal = discount,
                        Outstanding = charge - payment - discount,
                    });
                }
            }

            result.Charges = returnList;
            if (returnList.Count == 0 && filterByArm)
            {
                result.Message =
                    "No optional charge has been assigned to any student in the selected class-arm in the selected session and term.";
            }

            return result;
        }

        /// <inheritdoc cref="IAccountingService.GetStudentOptionalPaymentHistoryAsync" />
        public async Task<AccountingOptionalPaymentReportDto> GetStudentOptionalPaymentHistoryAsync(
            string userId,
            string studentId,
            CancellationToken ct = default)
        {
            // Legacy StudentOptionalPaymentHistory
            _ = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            if (string.IsNullOrWhiteSpace(studentId))
            {
                return new AccountingOptionalPaymentReportDto();
            }

            var charges = await _uow.Repository<StudentTermlyOptionalCharges>().GetAllAsync(
                Filter: x => x.StudentId == studentId,
                IncludeProperties:
                "StudentDetail.User,StudentTermlyOptionalPaymentList,OptionalFeesItem.YearlyAndTermlyStatus.Session,ClassArm.Classs",
                cancellationToken: ct);

            var discounts = await _uow.Repository<StudentTermlyOptionalPaymentDiscount>().WhereAsync(
                x => x.StudentTermlyOptionalCharges != null
                     && x.StudentTermlyOptionalCharges.StudentId == studentId,
                ct);

            var mappedCharges = charges.Select(MapOptionalDefaulterCharge).ToList();
            var mappedDiscounts = discounts.Select(d => new AccountingOptionalDefaulterDiscountDto
            {
                StudentTermlyOptionalChargesId = d.StudentTermlyOptionalChargesId,
                StudentId = studentId,
                DiscountAmount = d.DiscountAmount,
                ReasonsForDiscount = d.ReasonsForDiscount ?? string.Empty,
            }).ToList();

            return new AccountingOptionalPaymentReportDto
            {
                TotalPayment = mappedCharges.Sum(c => c.Payments.Sum(p => p.Amount)),
                TotalDiscount = mappedDiscounts.Sum(d => d.DiscountAmount),
                Charges = mappedCharges,
                Discounts = mappedDiscounts,
            };
        }

        /// <inheritdoc cref="IAccountingService.GetOptionalPaymentRangeReportAsync" />
        public async Task<AccountingOptionalRangeReportDto> GetOptionalPaymentRangeReportAsync(
            string userId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken ct = default)
        {
            // Legacy GetRangePaymentReport
            return await BuildOptionalPaymentRangeReportAsync(
                userId,
                startDate,
                endDate,
                categoryId: null,
                ct);
        }

        /// <inheritdoc cref="IAccountingService.GetOptionalPaymentCategoryRangeReportAsync" />
        public async Task<AccountingOptionalRangeReportDto> GetOptionalPaymentCategoryRangeReportAsync(
            string userId,
            DateTime startDate,
            DateTime endDate,
            string categoryId,
            CancellationToken ct = default)
        {
            // Legacy GetOptionalPaymentCategoryItemReport
            return await BuildOptionalPaymentRangeReportAsync(
                userId,
                startDate,
                endDate,
                categoryId,
                ct);
        }

        private async Task<AccountingOptionalRangeReportDto> BuildOptionalPaymentRangeReportAsync(
            string userId,
            DateTime startDate,
            DateTime endDate,
            string? categoryId,
            CancellationToken ct)
        {
            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);
            var start = NormalizeToUtc(startDate.Date);
            // Legacy: actualEndDate = endDate.AddDays(1)
            var actualEnd = NormalizeToUtc(endDate.Date.AddDays(1));

            var filterByCategory = !string.IsNullOrWhiteSpace(categoryId) && categoryId != "-1";

            var charges = await _uow.Repository<StudentTermlyOptionalCharges>().GetAllAsync(
                Filter: x => x.AsignedDate >= start
                             && x.AsignedDate <= actualEnd
                             && x.OptionalFeesItem != null
                             && x.OptionalFeesItem.ItemCategory != null
                             && x.OptionalFeesItem.ItemCategory.SchoolId == schoolId
                             && (!filterByCategory || x.OptionalFeesItem.ItemCategoryId == categoryId),
                IncludeProperties:
                "StudentDetail.User,StudentTermlyOptionalPaymentList,OptionalFeesItem.ItemCategory,OptionalFeesItem.YearlyAndTermlyStatus.Session,ClassArm.Classs",
                cancellationToken: ct);

            var chargeIds = charges.Select(c => c.Id).ToHashSet();
            var discountEntities = chargeIds.Count == 0
                ? new List<StudentTermlyOptionalPaymentDiscount>()
                : await _uow.Repository<StudentTermlyOptionalPaymentDiscount>().WhereAsync(
                    d => chargeIds.Contains(d.StudentTermlyOptionalChargesId),
                    ct);

            var mappedCharges = charges.Select(c =>
            {
                var term = c.OptionalFeesItem?.YearlyAndTermlyStatus;
                var termStatus = term?.TermStatus ?? default;
                var paid = c.StudentTermlyOptionalPaymentList?.Sum(p => p.Amount) ?? 0;
                return new AccountingOptionalRangeChargeDto
                {
                    Id = c.Id,
                    StudentId = c.StudentId,
                    FullName = c.StudentDetail?.User?.FullName ?? string.Empty,
                    IdNo = c.StudentDetail?.IdNo ?? string.Empty,
                    ClassId = c.ClassArm?.ClassId ?? string.Empty,
                    ClassLabel = c.ClassArm?.Classs?.ClassLabel ?? string.Empty,
                    ClassArmId = c.ClassArmId,
                    ArmName = c.ClassArm?.ArmName.ToString() ?? string.Empty,
                    YearlyAndTermlyStatusId = c.OptionalFeesItem?.YearlyAndTermlyStatusId ?? string.Empty,
                    TermStatus = (byte)termStatus,
                    TermStatusLabel = termStatus.ToString(),
                    Session = term?.Session?.Session ?? string.Empty,
                    SessionId = term?.SessionId ?? string.Empty,
                    CategoryName = c.OptionalFeesItem?.ItemCategory?.CategoryName ?? string.Empty,
                    Cost = c.OptionalFeesItem?.Cost ?? 0,
                    AmountPaid = paid,
                    AssignedDate = c.AsignedDate,
                    AssignedYear = c.AsignedDate.Year,
                };
            }).ToList();

            var discounts = discountEntities.Select(d => new AccountingOptionalDefaulterDiscountDto
            {
                StudentTermlyOptionalChargesId = d.StudentTermlyOptionalChargesId,
                StudentId = charges.FirstOrDefault(c => c.Id == d.StudentTermlyOptionalChargesId)?.StudentId
                            ?? string.Empty,
                DiscountAmount = d.DiscountAmount,
                ReasonsForDiscount = d.ReasonsForDiscount ?? string.Empty,
            }).ToList();

            var totalCharges = mappedCharges.Sum(x => x.Cost);
            var totalPayment = mappedCharges.Sum(x => x.AmountPaid);
            var totalDiscount = discounts.Sum(x => x.DiscountAmount);
            var categoryLabel = filterByCategory
                ? (mappedCharges.FirstOrDefault()?.CategoryName ?? "Category")
                : "All Categories";

            return new AccountingOptionalRangeReportDto
            {
                CategoryLabel = categoryLabel,
                NumberOfItems = mappedCharges.Count,
                TotalCharges = totalCharges,
                TotalPayment = totalPayment,
                TotalDiscount = totalDiscount,
                TotalOutstanding = totalCharges - (totalPayment + totalDiscount),
                Charges = mappedCharges,
                Discounts = discounts,
            };
        }

        private async Task<IList<SelectListItem>> GetOptionalPaymentDefaultersTermsInternalAsync(
            string sessionId,
            CancellationToken ct)
        {
            // Legacy GetTermsEnumTypeDropdownForSelectedSession: Value = (int)TermStatus
            if (string.IsNullOrWhiteSpace(sessionId) || sessionId == "-1")
            {
                return new List<SelectListItem>();
            }

            var terms = await _uow.Repository<TxYearlyAndTermlyStatus>().WhereAsync(
                h => h.SessionId == sessionId,
                ct);

            var list = terms
                .OrderBy(v => v.TermStatus)
                .Select(x => new SelectListItem
                {
                    Text = x.TermStatus.ToString(),
                    Value = ((int)x.TermStatus).ToString(),
                })
                .ToList();

            if (list.Count == 1)
            {
                var only = list[0];
                return new List<SelectListItem>
                {
                    new() { Text = "Select", Value = "-1" },
                    only,
                };
            }

            return list;
        }

        private async Task<AccountingOptionalPaymentDefaultersDto> BuildOptionalPaymentDefaultersAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            bool filterByTerm,
            CancellationToken ct)
        {
            var empty = new AccountingOptionalPaymentDefaultersDto();
            if (string.IsNullOrWhiteSpace(sessionId) || sessionId == "-1")
            {
                return empty;
            }

            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);

            var list = await _uow.Repository<StudentTermlyOptionalCharges>().GetAllAsync(
                Filter: x => x.OptionalFeesItem != null
                             && x.OptionalFeesItem.YearlyAndTermlyStatus != null
                             && x.OptionalFeesItem.YearlyAndTermlyStatus.Session != null
                             && x.OptionalFeesItem.YearlyAndTermlyStatus.Session.SchoolInfoId == schoolId
                             && x.OptionalFeesItem.YearlyAndTermlyStatus.SessionId == sessionId
                             && (!filterByTerm
                                 || x.OptionalFeesItem.YearlyAndTermlyStatus.TermStatus == termStatus),
                IncludeProperties:
                "StudentDetail.User,StudentTermlyOptionalPaymentList,OptionalFeesItem.YearlyAndTermlyStatus.Session,ClassArm.Classs",
                cancellationToken: ct);

            var returnList = new List<StudentTermlyOptionalCharges>();
            var returnDiscountList = new List<StudentTermlyOptionalPaymentDiscount>();

            foreach (var each in list)
            {
                var discountList = await _uow.Repository<StudentTermlyOptionalPaymentDiscount>().WhereAsync(
                    x => x.StudentTermlyOptionalChargesId == each.Id,
                    ct);
                var dicount = discountList.Count > 0 ? discountList.Sum(x => x.DiscountAmount) : 0.0;
                var cost = each.OptionalFeesItem?.Cost ?? 0;
                var paid = each.StudentTermlyOptionalPaymentList?.Sum(x => x.Amount) ?? 0;

                // Legacy: OptionalFeesItem.Cost > (payments.Sum(Amount) + discounts.Sum(DiscountAmount))
                if (cost > paid + dicount)
                {
                    returnList.Add(each);
                    returnDiscountList.AddRange(discountList);
                }
            }

            var charges = returnList.Select(MapOptionalDefaulterCharge).ToList();
            var discounts = returnDiscountList.Select(d => new AccountingOptionalDefaulterDiscountDto
            {
                StudentTermlyOptionalChargesId = d.StudentTermlyOptionalChargesId,
                StudentId = returnList.FirstOrDefault(c => c.Id == d.StudentTermlyOptionalChargesId)?.StudentId
                            ?? string.Empty,
                DiscountAmount = d.DiscountAmount,
                ReasonsForDiscount = d.ReasonsForDiscount ?? string.Empty,
            }).ToList();

            var totalCharge = charges.Sum(x => x.Cost);
            var totalPayment = charges.Sum(c => c.Payments.Sum(p => p.Amount));
            var totalDiscount = discounts.Sum(x => x.DiscountAmount);

            return new AccountingOptionalPaymentDefaultersDto
            {
                TotalDiscount = totalDiscount,
                TotalOutstanding = totalCharge - totalDiscount - totalPayment,
                Charges = charges,
                Discounts = discounts,
            };
        }

        private async Task<AccountingOptionalPaymentReportDto> BuildOptionalPaymentReportAsync(
            string userId,
            string sessionId,
            TermStatus termStatus,
            bool filterByTerm,
            CancellationToken ct)
        {
            var empty = new AccountingOptionalPaymentReportDto();
            if (string.IsNullOrWhiteSpace(sessionId) || sessionId == "-1")
            {
                return empty;
            }

            var schoolId = await _schoolScopeResolver.ResolveSchoolIdAsync(userId, ct);

            // Legacy StudentTermlyPaymentReport / StudentSessionalPaymentReport:
            // load payments (and discounts) filtered by charge item session (+ term).
            // Query from charges first to avoid EF include cycles on Payment → Charge → PaymentList.
            var chargesInScope = await _uow.Repository<StudentTermlyOptionalCharges>().GetAllAsync(
                Filter: x => x.OptionalFeesItem != null
                             && x.OptionalFeesItem.YearlyAndTermlyStatus != null
                             && x.OptionalFeesItem.YearlyAndTermlyStatus.Session != null
                             && x.OptionalFeesItem.YearlyAndTermlyStatus.Session.SchoolInfoId == schoolId
                             && x.OptionalFeesItem.YearlyAndTermlyStatus.SessionId == sessionId
                             && (!filterByTerm
                                 || x.OptionalFeesItem.YearlyAndTermlyStatus.TermStatus == termStatus),
                IncludeProperties:
                "StudentDetail.User,StudentTermlyOptionalPaymentList,OptionalFeesItem.YearlyAndTermlyStatus.Session,ClassArm.Classs",
                cancellationToken: ct);

            // Legacy model is the payment list — only charges that have payments appear.
            var chargesWithPayments = chargesInScope
                .Where(c => c.StudentTermlyOptionalPaymentList != null
                            && c.StudentTermlyOptionalPaymentList.Count > 0)
                .ToList();

            // Legacy loads discounts by session/term independently (all charges in scope).
            var allChargeIdsInScope = chargesInScope.Select(c => c.Id).ToHashSet();
            var discountEntities = allChargeIdsInScope.Count == 0
                ? new List<StudentTermlyOptionalPaymentDiscount>()
                : await _uow.Repository<StudentTermlyOptionalPaymentDiscount>().WhereAsync(
                    x => allChargeIdsInScope.Contains(x.StudentTermlyOptionalChargesId),
                    ct);

            var chargeStudentLookup = chargesInScope.ToDictionary(c => c.Id, c => c.StudentId);

            var charges = chargesWithPayments.Select(MapOptionalDefaulterCharge).ToList();
            var discounts = discountEntities.Select(d => new AccountingOptionalDefaulterDiscountDto
            {
                StudentTermlyOptionalChargesId = d.StudentTermlyOptionalChargesId,
                StudentId = chargeStudentLookup.TryGetValue(d.StudentTermlyOptionalChargesId, out var sid)
                    ? sid
                    : string.Empty,
                DiscountAmount = d.DiscountAmount,
                ReasonsForDiscount = d.ReasonsForDiscount ?? string.Empty,
            }).ToList();

            var totalPayment = chargesWithPayments
                .SelectMany(c => c.StudentTermlyOptionalPaymentList)
                .Sum(p => p.Amount);

            return new AccountingOptionalPaymentReportDto
            {
                TotalPayment = totalPayment,
                TotalDiscount = discounts.Sum(x => x.DiscountAmount),
                Charges = charges,
                Discounts = discounts,
            };
        }

        private static AccountingOptionalDefaulterChargeDto MapOptionalDefaulterCharge(
            StudentTermlyOptionalCharges charge)
        {
            var term = charge.OptionalFeesItem?.YearlyAndTermlyStatus;
            var termStatus = term?.TermStatus ?? default;
            return new AccountingOptionalDefaulterChargeDto
            {
                Id = charge.Id,
                StudentId = charge.StudentId,
                FullName = charge.StudentDetail?.User?.FullName ?? string.Empty,
                IdNo = charge.StudentDetail?.IdNo ?? string.Empty,
                ClassId = charge.ClassArm?.ClassId ?? string.Empty,
                ClassLabel = charge.ClassArm?.Classs?.ClassLabel ?? string.Empty,
                ClassArmId = charge.ClassArmId,
                ArmName = charge.ClassArm?.ArmName.ToString() ?? string.Empty,
                YearlyAndTermlyStatusId = charge.OptionalFeesItem?.YearlyAndTermlyStatusId ?? string.Empty,
                TermStatus = (byte)termStatus,
                TermStatusLabel = termStatus.ToString(),
                Session = term?.Session?.Session ?? string.Empty,
                ItemName = charge.OptionalFeesItem?.ItemName ?? string.Empty,
                OptionalFeesItemId = charge.OptionalFeesItemId,
                Cost = charge.OptionalFeesItem?.Cost ?? 0,
                Payments = (charge.StudentTermlyOptionalPaymentList ?? Array.Empty<StudentTermlyOptionalPayment>())
                    .OrderBy(p => p.PaymentDate)
                    .Select(p => new AccountingOptionalDefaulterPaymentDto
                    {
                        PaymentDate = p.PaymentDate,
                        PaymentType = (byte)p.PaymentType,
                        PaymentTypeLabel = p.PaymentType.ToString(),
                        Amount = p.Amount,
                    })
                    .ToList(),
            };
        }

        private async Task<List<SelectListItem>> GetSchoolClassDropdownAsync(string schoolId, CancellationToken ct)
        {
            var classes = await _uow.Repository<RxClasss>().WhereAsync(
                c => c.SchoolInfoId == schoolId,
                ct);
            return classes
                .OrderBy(c => c.ClassName)
                .Select(c => new SelectListItem { Value = c.Id, Text = c.ClassLabel })
                .ToList();
        }

        private async Task<List<SelectListItem>> GetSchoolArmDropdownAsync(
            string schoolId,
            string classId,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(classId))
            {
                return new List<SelectListItem>
                {
                    new() { Text = "The selected Class does not contain Arm", Value = "0" },
                };
            }

            var classModel = await _uow.Repository<RxClasss>().FindAsync(
                c => c.Id == classId && c.SchoolInfoId == schoolId,
                ct);
            if (classModel == null)
            {
                return new List<SelectListItem>
                {
                    new() { Text = "The selected Class does not contain Arm", Value = "0" },
                };
            }

            var arms = (await _uow.Repository<AxClassArm>().WhereAsync(
                    a => a.ClassId == classId,
                    ct))
                .OrderBy(a => a.ArmName)
                .Select(a => new SelectListItem
                {
                    Value = a.Id,
                    Text = a.ArmName.ToString(),
                })
                .ToList();

            if (arms.Count == 0)
            {
                arms.Add(new SelectListItem { Text = "The selected Class does not contain Arm", Value = "0" });
            }
            else if (arms.Count == 1)
            {
                // Legacy: prepend Select/0 when only one arm
                var only = arms[0];
                arms = new List<SelectListItem>
                {
                    new() { Text = "Select", Value = "0" },
                    only,
                };
            }

            return arms;
        }

        private static readonly ClassCategoryStatus[] ReportCategoryOrder =
        {
            ClassCategoryStatus.PlayClass,
            ClassCategoryStatus.PreNursery,
            ClassCategoryStatus.Nursery,
            ClassCategoryStatus.Primary,
            ClassCategoryStatus.Jss,
            ClassCategoryStatus.Science,
            ClassCategoryStatus.Art,
            ClassCategoryStatus.Commercial,
        };

        private sealed class TermlyCategoryBucket
        {
            public ClassCategoryStatus Category { get; set; } = ClassCategoryStatus.None;
            public bool HasData => Category != ClassCategoryStatus.None;

            public int ClearedCount;
            public string ClearedDetails = string.Empty;
            public double ClearedTakeHome;
            public double Discount;

            public int PartCount;
            public string PartDetails = string.Empty;
            public double PartTakeHome;
            public double PartOutstanding;

            public int UnclearCount;
            public string UnclearDetails = string.Empty;
            public double UnclearOutstanding;
        }

        private static Dictionary<ClassCategoryStatus, TermlyCategoryBucket> CreateCategoryBuckets()
        {
            var map = new Dictionary<ClassCategoryStatus, TermlyCategoryBucket>();
            foreach (var category in ReportCategoryOrder)
            {
                map[category] = new TermlyCategoryBucket();
            }

            return map;
        }

        private static void AccumulatePaymentTypeTotals(
            IEnumerable<StudentTyTermlyFeePayment> payments,
            ref double cash,
            ref double teller,
            ref double transfer)
        {
            foreach (var item in payments)
            {
                if (item.PaymentType == PaymentType.Cash)
                {
                    cash += item.Amount;
                }
                else if (item.PaymentType == PaymentType.Teller)
                {
                    teller += item.Amount;
                }
                else if (item.PaymentType == PaymentType.Transfer)
                {
                    transfer += item.Amount;
                }
            }
        }

        private static string BuildDetailEntry(
            string idNo,
            string fullName,
            string classLabel,
            ArmName armName,
            CurrentClass className,
            string feesToken) =>
            idNo + "~" + fullName + "~" + classLabel + armName + "?" + (byte)className + "~" + feesToken;

        private static void AppendDetail(ref string target, string entry) =>
            target = string.IsNullOrEmpty(target) ? entry : target + "," + entry;

        private static void RecordCleared(
            TermlyCategoryBucket bucket,
            ClassCategoryStatus category,
            double paid,
            double discount,
            string detail)
        {
            bucket.Category = category;
            bucket.ClearedCount += 1;
            bucket.ClearedTakeHome += paid;
            bucket.Discount += discount;
            AppendDetail(ref bucket.ClearedDetails, detail);
        }

        private static void RecordPartlyCleared(
            TermlyCategoryBucket bucket,
            ClassCategoryStatus category,
            double paid,
            double outstanding,
            string detail)
        {
            bucket.Category = category;
            bucket.PartCount += 1;
            bucket.PartTakeHome += paid;
            bucket.PartOutstanding += outstanding;
            AppendDetail(ref bucket.PartDetails, detail);
        }

        private static void RecordUncleared(
            TermlyCategoryBucket bucket,
            ClassCategoryStatus category,
            double outstanding,
            string detail)
        {
            bucket.Category = category;
            bucket.UnclearCount += 1;
            bucket.UnclearOutstanding += outstanding;
            AppendDetail(ref bucket.UnclearDetails, detail);
        }

        private static TermlyReportOfReturningDto ToReportRow(
            StudentStatus rowStatus,
            TermlyCategoryBucket bucket,
            double fees,
            string termAndSession) =>
            new()
            {
                RowStatus = rowStatus,
                ClassCategory = bucket.Category,
                Fees = fees,
                ClearedStuds = (short)bucket.ClearedCount,
                ClearedStudsDetails = bucket.ClearedDetails,
                Discount = bucket.Discount,
                TakeHome1 = bucket.ClearedTakeHome,
                PartlyClearedStuds = (short)bucket.PartCount,
                PartlyClearedStudsDetails = bucket.PartDetails,
                TakeHome2 = bucket.PartTakeHome,
                Outstanding1 = bucket.PartOutstanding,
                UnClearStuds = (short)bucket.UnclearCount,
                UnClearStudsDetails = bucket.UnclearDetails,
                Outstanding2 = bucket.UnclearOutstanding,
                TermAndSession = termAndSession,
            };

        private async Task<AccountingDashboardSummaryDto> BuildTermlyFeeSummaryAsync(
            string schoolId,
            string selectedTermId,
            CancellationToken ct)
        {
            var result = new AccountingDashboardSummaryDto();

            var fees = await _uow.Repository<SetSchoolFeesTr>().FindAsync(
                d => d.SchoolId == schoolId && d.YearlyAndTermlyStatusId == selectedTermId,
                ct);
            if (fees == null)
            {
                return result;
            }

            var progressList = (await _uow.Repository<StudentAcademicTxProgressHistory>().WhereAsync(
                x => x.YearlyAndTermlyStatusId == selectedTermId
                     && x.StudentDetails!.SchoolInfoId == schoolId,
                ct,
                x => x.StudentDetails!.ClassArm!.Classs!,
                x => x.StudentDetails!.User!,
                x => x.StudentTermlyFeePayment)).ToList();

            if (progressList.Count == 0)
            {
                return result;
            }

            var discounts = (await _uow.Repository<StudentTyTermlyFixedPaymentDiscount>().WhereAsync(
                x => x.StudentAcademicProgressHistory!.YearlyAndTermlyStatusId == selectedTermId,
                ct,
                x => x.StudentAcademicProgressHistory!)).ToList();

            foreach (var eachStudent in progressList)
            {
                var details = eachStudent.StudentDetails;
                if (details?.ClassArm?.Classs == null)
                {
                    continue;
                }

                var hisClass = details.ClassArm.Classs.ClassName;
                var classCategory = ResolveClassCategory(hisClass, details.ClassCategory);
                var isFresher = details.AdmisnTermId == selectedTermId
                    && details.StudentInitialRegStatus == StudentInitialRegistrationStatus.Fresher;
                var hisFees = isFresher
                    ? GetFeesForFreshiers(fees, classCategory)
                    : GetFeesForReturningStudent(fees, classCategory);

                var discount = discounts
                    .FirstOrDefault(x => x.StudentAcademicProgressId == eachStudent.Id)
                    ?.DiscountAmount ?? 0;

                var payments = eachStudent.StudentTermlyFeePayment ?? new List<StudentTyTermlyFeePayment>();
                if (payments.Count > 0)
                {
                    var paid = payments.Sum(b => b.Amount);
                    if (Math.Abs((paid + discount) - hisFees) < 0.5)
                    {
                        result.ClearedStudents += 1;
                        result.TotalPayments += paid;
                    }
                    else
                    {
                        result.PartlyClearedStudents += 1;
                        result.PartPayments += paid;
                        result.PartOutstanding += Math.Max(0, hisFees - paid - discount);
                    }
                }
                else
                {
                    result.UnclearedStudents += 1;
                    result.Outstanding += hisFees;
                }
            }

            return result;
        }

        /// <summary>Legacy AccountingService.GetDateValues — year from HasConfirmedPayment slips, else expenses; month = DateTime.Now.Month.</summary>
        private async Task<(short Year, byte Month)> GetDateValuesAsync(string schoolId, CancellationToken ct)
        {
            // Legacy uses DateTime.Now.Month (not max paid month — that branch is commented out).
            byte currentMonth = (byte)DateTime.Now.Month;
            short currentYear = 0;

            // Stepwise (avoid nested Include on Month.Year that previously 500'd defaults).
            var schoolEmployees = await _uow.Repository<TxEmployee>().WhereAsync(
                e => e.SchoolId == schoolId,
                ct);
            var employeeIds = schoolEmployees.Select(e => e.Id).ToList();
            if (employeeIds.Count > 0)
            {
                var payments = await _uow.Repository<TxEmployeesMonthlyPayment>().WhereAsync(
                    p => employeeIds.Contains(p.EmployeeId),
                    ct);
                var levelIds = payments.Select(p => p.EmployeeMonthlyPaymentLevelId).Distinct().ToList();
                if (levelIds.Count > 0)
                {
                    var levels = await _uow.Repository<TxEmployeeMonthlyPaymentLevel>().WhereAsync(
                        l => levelIds.Contains(l.Id)
                             && l.EmployeeMonthlyPaymentLevel == EmployeeMonthlyPaymentLevel.HasConfirmedPayment,
                        ct);
                    var monthIds = levels.Select(l => l.MonthId).Distinct().ToList();
                    if (monthIds.Count > 0)
                    {
                        var months = await _uow.Repository<TxMonth>().WhereAsync(
                            m => monthIds.Contains(m.Id),
                            ct);
                        var yearIds = months.Select(m => m.YearId).Distinct().ToList();
                        if (yearIds.Count > 0)
                        {
                            var years = await _uow.Repository<TxYear>().WhereAsync(
                                y => yearIds.Contains(y.Id) && y.SchoolId == schoolId,
                                ct);
                            if (years.Count > 0)
                            {
                                currentYear = years.Max(y => y.Year);
                            }
                        }
                    }
                }
            }

            if (currentYear == 0)
            {
                var expenses = await _uow.Repository<XyExpenses>().GetAllAsync(
                    Filter: n => n.Week != null && n.Week.SchoolId == schoolId,
                    IncludeProperties: "Week",
                    cancellationToken: ct);
                if (expenses.Count > 0)
                {
                    currentYear = (short)expenses.Max(v => v.CapturedDate.Year);
                }
            }

            return (currentYear, currentMonth);
        }

        /// <summary>Legacy GetYearAndMonthDropdownForBothSalaryAndExpenditure (+ Index OrderByDescending).</summary>
        private async Task<(List<SelectListItem> Years, List<SelectListItem> Months)> GetYearAndMonthDropdownAsync(
            string schoolId,
            CancellationToken ct)
        {
            var yearEntities = (await _uow.Repository<TxYear>().WhereAsync(d => d.SchoolId == schoolId, ct)).ToList();
            var yearListReturnResult = yearEntities
                .Select(c => new SelectListItem { Text = c.Year.ToString(), Value = c.Year.ToString() })
                .GroupBy(x => x.Value)
                .Select(g => g.First())
                .ToList();

            var yearIds = yearEntities.Select(y => y.Id).ToList();
            var monthEntities = yearIds.Count == 0
                ? new List<TxMonth>()
                : (await _uow.Repository<TxMonth>().WhereAsync(d => yearIds.Contains(d.YearId), ct)).ToList();

            var monthDistinct = monthEntities
                .OrderBy(c => c.Id)
                .GroupBy(n => n.Month)
                .Select(v => v.First())
                .Select(c => new SelectListItem { Text = c.Month.ToString(), Value = c.Month.ToString() })
                .ToList();

            var expenses = await _uow.Repository<XyExpenses>().GetAllAsync(
                Filter: f => f.Week != null && f.Week.SchoolId == schoolId,
                IncludeProperties: "Week",
                cancellationToken: ct);

            foreach (var month in expenses.Select(b => b.CapturedDate.Month).Distinct())
            {
                var value = month.ToString();
                if (monthDistinct.All(x => x.Value != value))
                {
                    monthDistinct.Add(new SelectListItem { Text = value, Value = value });
                }
            }

            foreach (var year in expenses.Select(b => b.CapturedDate.Year).Distinct())
            {
                var value = year.ToString();
                if (yearListReturnResult.All(x => x.Value != value))
                {
                    yearListReturnResult.Add(new SelectListItem { Text = value, Value = value });
                }
            }

            // Legacy fills missing months between min..max, then labels via GetMonth (Jan–Dec).
            var monthNumbers = monthDistinct
                .Select(x => byte.TryParse(x.Value, out var m) ? m : (byte)0)
                .Where(m => m is >= 1 and <= 12)
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            var returnMonthSerialList = new List<SelectListItem>();
            if (monthNumbers.Count > 0)
            {
                var monthSerial = monthNumbers.First();
                foreach (var each in monthNumbers)
                {
                    while (each > monthSerial)
                    {
                        returnMonthSerialList.Add(new SelectListItem
                        {
                            Text = FormatMonthAbbreviation(monthSerial),
                            Value = monthSerial.ToString(),
                        });
                        monthSerial++;
                    }

                    returnMonthSerialList.Add(new SelectListItem
                    {
                        Text = FormatMonthAbbreviation(each),
                        Value = each.ToString(),
                    });
                    monthSerial = (byte)(each + 1);
                }
            }

            // Legacy puts DateTime.Now.Year first when present.
            var currentYear = DateTime.Now.Year;
            var currentYearModel = yearListReturnResult.FirstOrDefault(x => x.Value == currentYear.ToString());
            if (currentYearModel != null)
            {
                yearListReturnResult.Remove(currentYearModel);
            }
            else
            {
                currentYearModel = new SelectListItem
                {
                    Value = currentYear.ToString(),
                    Text = currentYear.ToString(),
                };
            }

            var years = new List<SelectListItem> { currentYearModel };
            years.AddRange(yearListReturnResult);

            // AccountingBaseController then OrderByDescending(Value) for both lists.
            years = years
                .OrderByDescending(v => short.TryParse(v.Value, out var y) ? y : short.MinValue)
                .ToList();
            var months = returnMonthSerialList
                .OrderByDescending(v => int.TryParse(v.Value, out var m) ? m : -1)
                .ToList();

            if (years.Count == 1)
            {
                years.Add(new SelectListItem { Text = "Select", Value = "-1" });
            }

            if (months.Count == 1)
            {
                months.Add(new SelectListItem { Text = "Select", Value = "-1" });
            }

            return (years, months);
        }

        private static string FormatMonthAbbreviation(byte month) => month switch
        {
            1 => "Jan",
            2 => "Feb",
            3 => "Mar",
            4 => "Apr",
            5 => "May",
            6 => "Jun",
            7 => "Jul",
            8 => "Aug",
            9 => "Sep",
            10 => "Oct",
            11 => "Nov",
            12 => "Dec",
            _ => month.ToString(),
        };

        private static ClassCategoryStatus ResolveClassCategory(CurrentClass hisClass, ClassCategoryStatus studentCategory)
        {
            if (hisClass is CurrentClass.Ss1 or CurrentClass.Ss2 or CurrentClass.Ss3)
            {
                return studentCategory;
            }

            return GetClassCategoryStatus(hisClass);
        }

        private static ClassCategoryStatus GetClassCategoryStatus(CurrentClass className)
        {
            if (className == CurrentClass.PreNursery)
            {
                return ClassCategoryStatus.PreNursery;
            }

            if (className is CurrentClass.Nursery1 or CurrentClass.Nursery2)
            {
                return ClassCategoryStatus.Nursery;
            }

            if (className is CurrentClass.Primary1 or CurrentClass.Primary2 or CurrentClass.Primary3
                or CurrentClass.Primary4 or CurrentClass.Primary5 or CurrentClass.Primary6)
            {
                return ClassCategoryStatus.Primary;
            }

            if (className is CurrentClass.Jss1 or CurrentClass.Jss2 or CurrentClass.Jss3)
            {
                return ClassCategoryStatus.Jss;
            }

            return ClassCategoryStatus.PlayClass;
        }

        private static double GetFeesForReturningStudent(SetSchoolFeesTr feesModel, ClassCategoryStatus classStatus) =>
            classStatus switch
            {
                ClassCategoryStatus.PlayClass => feesModel.RetnPlayClass,
                ClassCategoryStatus.PreNursery => feesModel.RetnPreNursery,
                ClassCategoryStatus.Nursery => feesModel.RetnNursery,
                ClassCategoryStatus.Primary => feesModel.RetnPrimary,
                ClassCategoryStatus.Jss => feesModel.RetnJss,
                ClassCategoryStatus.Science => feesModel.RetnScience,
                ClassCategoryStatus.Art => feesModel.RetnArt,
                ClassCategoryStatus.Commercial => feesModel.RetnCommercial,
                _ => 0,
            };

        private static double GetFeesForFreshiers(SetSchoolFeesTr feesModel, ClassCategoryStatus classStatus) =>
            classStatus switch
            {
                ClassCategoryStatus.PlayClass => feesModel.FreshPlayClass,
                ClassCategoryStatus.PreNursery => feesModel.FreshPreNursery,
                ClassCategoryStatus.Nursery => feesModel.FreshNursery,
                ClassCategoryStatus.Primary => feesModel.FreshPrimary,
                ClassCategoryStatus.Jss => feesModel.FreshJss,
                ClassCategoryStatus.Science => feesModel.FreshScience,
                ClassCategoryStatus.Art => feesModel.FreshArt,
                ClassCategoryStatus.Commercial => feesModel.FreshCommercial,
                _ => 0,
            };

        private static string FormatNaira(double amount)
        {
            var culture = CultureInfo.GetCultureInfo("en-GB");
            return amount.ToString("C", culture).Replace('£', '₦');
        }

        private static string FormatNetProfit(double netPay)
        {
            if (netPay < 0)
            {
                var abs = Math.Abs(netPay);
                return "<strong>-</strong> (" + FormatNaira(abs) + ")";
            }

            return FormatNaira(netPay);
        }
    }
}
