import { Injectable, inject } from '@angular/core';
import { HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { GenericService } from '@shared';

export type AccountingSelectOption = {
  text: string;
  value: string;
  selected?: boolean;
};

export type AccountingDashboardSummary = {
  hasData: boolean;
  currentTermId?: string | null;
  currentSessionId?: string | null;
  clearedStudents: number;
  totalPayments: number;
  partlyClearedStudents: number;
  partPayments: number;
  partOutstanding: number;
  unclearedStudents: number;
  outstanding: number;
  summaryTotalPayments: number;
  summaryTotalOutstanding: number;
};

export type AccountingIncomeStatement = {
  year: number;
  startMonth: number;
  endMonth: number;
  otherExpenses: number;
  salaryPayment: number;
  totalExpenditure: number;
  totalIncome: number;
  netProfit: number;
  otherIncome: number;
  fixedFeePayment: number;
  optionalFeePayment: number;
  legacyDelimited: string;
};

export type AccountingIncomeStatementDefaults = {
  year: number;
  month: number;
  years: AccountingSelectOption[];
  months: AccountingSelectOption[];
  statement?: AccountingIncomeStatement | null;
};

@Injectable({ providedIn: 'root' })
export class AccountingApiService {
  private readonly api = inject(GenericService<unknown>);

  getDashboardSummary(termId?: string | null): Observable<AccountingDashboardSummary> {
    let params = new HttpParams();
    if (termId) {
      params = params.set('termId', termId);
    }
    return this.api.getAnObj<AccountingDashboardSummary>('/Accounting/dashboard-summary', { params });
  }

  getIncomeStatementDefaults(): Observable<AccountingIncomeStatementDefaults> {
    return this.api.getAnObj<AccountingIncomeStatementDefaults>('/Accounting/income-statement/defaults');
  }

  getIncomeStatement(year: number, startMonth: number, endMonth: number): Observable<AccountingIncomeStatement> {
    const params = new HttpParams()
      .set('year', String(year))
      .set('startMonth', String(startMonth))
      .set('endMonth', String(endMonth));
    return this.api.getAnObj<AccountingIncomeStatement>('/Accounting/income-statement', { params });
  }

  getSessionsDropdown(): Observable<AccountingSelectOption[]> {
    return this.api.get<AccountingSelectOption[]>('/SessionAndTerm/dropdown/sessions');
  }

  getTermsDropdown(sessionId: string): Observable<AccountingSelectOption[]> {
    return this.api.get<AccountingSelectOption[]>(`/Accounting/dropdown/sessions/${sessionId}/terms`);
  }

  getSetSchoolFees(): Observable<AccountingSetSchoolFeesLoad> {
    return this.api.getAnObj<AccountingSetSchoolFeesLoad>('/Accounting/set-school-fees');
  }

  saveSetSchoolFees(model: AccountingSetSchoolFeesForm): Observable<AccountingSetSchoolFeesSaveResult> {
    return this.api.create<AccountingSetSchoolFeesSaveResult>('/Accounting/set-school-fees', model);
  }

  getNewStudentFees(): Observable<AccountingNewStudentFeesLoad> {
    return this.api.getAnObj<AccountingNewStudentFeesLoad>('/Accounting/new-student-fees');
  }

  paySelectedFreshiers(studentIds: AccountingPayFreshierItem[]): Observable<AccountingPayFreshiersResult> {
    return this.api.create<AccountingPayFreshiersResult>('/Accounting/new-student-fees/pay', { studentIds });
  }

  getFreshPartPaymentInfo(studentLevelPaymentId: string): Observable<AccountingFreshPartPaymentRow[]> {
    return this.api.get<AccountingFreshPartPaymentRow[]>(
      `/Accounting/new-student-fees/${encodeURIComponent(studentLevelPaymentId)}/part-payment`,
    );
  }

  submitFreshPartPayment(request: AccountingFreshPartPaymentRequest): Observable<AccountingFreshPartPaymentReceipt> {
    return this.api.create<AccountingFreshPartPaymentReceipt>('/Accounting/new-student-fees/part-payment', request);
  }

  getReturningStudentFees(): Observable<AccountingReturningStudentFeesLoad> {
    return this.api.getAnObj<AccountingReturningStudentFeesLoad>('/Accounting/returning-student-fees');
  }

  paySelectedReturningStudents(studentIds: AccountingPayFreshierItem[]): Observable<AccountingPayFreshiersResult> {
    return this.api.create<AccountingPayFreshiersResult>('/Accounting/returning-student-fees/pay', { studentIds });
  }

  getReturningPartPaymentInfo(studentLevelPaymentId: string): Observable<AccountingFreshPartPaymentRow[]> {
    return this.api.get<AccountingFreshPartPaymentRow[]>(
      `/Accounting/returning-student-fees/${encodeURIComponent(studentLevelPaymentId)}/part-payment`,
    );
  }

  submitReturningPartPayment(request: AccountingFreshPartPaymentRequest): Observable<AccountingFreshPartPaymentReceipt> {
    return this.api.create<AccountingFreshPartPaymentReceipt>(
      '/Accounting/returning-student-fees/part-payment',
      request,
    );
  }

  getExecutiveReportDefaults(): Observable<AccountingExecutiveReportDefaults> {
    return this.api.getAnObj<AccountingExecutiveReportDefaults>('/Accounting/executive-report/defaults');
  }

  getExecutiveReportTerms(sessionId: string): Observable<AccountingSelectOption[]> {
    const params = new HttpParams().set('sessionId', sessionId);
    return this.api.get<AccountingSelectOption[]>('/Accounting/executive-report/terms', { params });
  }

  getExecutiveReportTermly(termId: string): Observable<AccountingTermlyPaymentReport> {
    const params = new HttpParams().set('termId', termId);
    return this.api.getAnObj<AccountingTermlyPaymentReport>('/Accounting/executive-report/termly', { params });
  }

  searchStudentsForPaymentHistory(userName: string): Observable<AccountingPaymentHistoryStudent[]> {
    const params = new HttpParams().set('userName', userName);
    return this.api.get<AccountingPaymentHistoryStudent[]>(
      '/Accounting/student-payment-history/search',
      { params },
    );
  }

  getSelectedStudentPaymentHistory(
    studentId: string,
    admsnNo: string,
    studentName: string,
  ): Observable<AccountingStudentPaymentHistoryLoad> {
    const params = new HttpParams()
      .set('admsnNo', admsnNo)
      .set('studentName', studentName);
    return this.api.getAnObj<AccountingStudentPaymentHistoryLoad>(
      `/Accounting/student-payment-history/${encodeURIComponent(studentId)}`,
      { params },
    );
  }

  createOptionalFeesCategory(
    categoryName: string,
  ): Observable<AccountingCreateOptionalFeesCategoryResult> {
    return this.api.create<AccountingCreateOptionalFeesCategoryResult>(
      '/Accounting/optional-fees-categories',
      { categoryName },
    );
  }

  createIncomeCategory(
    categoryName: string,
  ): Observable<AccountingCreateIncomeCategoryResult> {
    return this.api.create<AccountingCreateIncomeCategoryResult>(
      '/Accounting/income-categories',
      { categoryName },
    );
  }

  createExpenditureCategory(
    categoryName: string,
  ): Observable<AccountingCreateExpenditureCategoryResult> {
    return this.api.create<AccountingCreateExpenditureCategoryResult>(
      '/Accounting/expenditure-categories',
      { categoryName },
    );
  }

  getRegisterIncomeForm(): Observable<AccountingRegisterIncomeForm> {
    return this.api.getAnObj<AccountingRegisterIncomeForm>('/Accounting/income/register-form');
  }

  createIncome(model: {
    itemCategoryId: string;
    capturedDate: string;
    details: string;
    cost: number;
  }): Observable<AccountingCreateIncomeResult> {
    return this.api.create<AccountingCreateIncomeResult>('/Accounting/income', model);
  }

  getRegisterExpenditureForm(): Observable<AccountingRegisterExpenditureForm> {
    return this.api.getAnObj<AccountingRegisterExpenditureForm>(
      '/Accounting/expenditure/register-form',
    );
  }

  createExpenditure(model: {
    itemCategoryId: string;
    capturedDate: string;
    details: string;
    cost: number;
  }): Observable<AccountingCreateExpenditureResult> {
    return this.api.create<AccountingCreateExpenditureResult>('/Accounting/expenditure', model);
  }

  getEmployeesIncomeStructure(): Observable<AccountingEmployeeListItem[]> {
    return this.api.get<AccountingEmployeeListItem[]>('/Accounting/employees-income-structure');
  }

  getSetIncomeStructureForm(employeeId: string): Observable<AccountingEmployeeIncomeStructure> {
    return this.api.getAnObj<AccountingEmployeeIncomeStructure>(
      `/Accounting/employees-income-structure/${encodeURIComponent(employeeId)}/set-form`,
    );
  }

  getEmployeeIncomeStructure(employeeId: string): Observable<AccountingEmployeeIncomeStructure> {
    return this.api.getAnObj<AccountingEmployeeIncomeStructure>(
      `/Accounting/employees-income-structure/${encodeURIComponent(employeeId)}`,
    );
  }

  saveEmployeeIncomeStructure(
    model: AccountingEmployeeIncomeStructureSave,
  ): Observable<AccountingEmployeeIncomeStructureMutationResult> {
    return this.api.create<AccountingEmployeeIncomeStructureMutationResult>(
      '/Accounting/employees-income-structure',
      model,
    );
  }

  deleteEmployeeIncomeStructure(
    employeeId: string,
  ): Observable<AccountingEmployeeIncomeStructureMutationResult> {
    return this.api.delete<AccountingEmployeeIncomeStructureMutationResult>(
      `/Accounting/employees-income-structure/${encodeURIComponent(employeeId)}`,
    );
  }

  getMonthlyPaymentSlip(paymentDate: string): Observable<AccountingMonthlyPaymentSlip> {
    const params = new HttpParams().set('paymentDate', paymentDate);
    return this.api.getAnObj<AccountingMonthlyPaymentSlip>('/Accounting/monthly-payroll/slip', {
      params,
    });
  }

  createMonthlyPayment(
    monthlyPayment: AccountingMonthlyPaymentSave[],
  ): Observable<AccountingMonthlyPayrollMutationResult> {
    return this.api.create<AccountingMonthlyPayrollMutationResult>(
      '/Accounting/monthly-payroll',
      monthlyPayment,
    );
  }

  confirmMonthlyPayment(levelId: string): Observable<AccountingMonthlyPayrollMutationResult> {
    return this.api.create<AccountingMonthlyPayrollMutationResult>(
      `/Accounting/monthly-payroll/${encodeURIComponent(levelId)}/confirm`,
      {},
    );
  }

  submitEmployeeMonthlyPayment(
    model: AccountingMonthlyPaymentSave,
  ): Observable<AccountingMonthlyPayrollMutationResult> {
    return this.api.create<AccountingMonthlyPayrollMutationResult>(
      '/Accounting/monthly-payroll/employee-payment',
      model,
    );
  }

  deleteMonthlyPaymentPreview(monthId: string): Observable<AccountingMonthlyPayrollMutationResult> {
    const params = new HttpParams().set('monthId', monthId);
    return this.api.delete<AccountingMonthlyPayrollMutationResult>(
      '/Accounting/monthly-payroll/preview',
      { params },
    );
  }

  getMonthlyPaymentComment(id: string): Observable<{ comment: string }> {
    return this.api.getAnObj<{ comment: string }>(
      `/Accounting/monthly-payroll/${encodeURIComponent(id)}/comments`,
    );
  }

  saveMonthlyPaymentComment(
    id: string,
    commentInput: string,
  ): Observable<AccountingMonthlyPayrollMutationResult> {
    return this.api.update<AccountingMonthlyPayrollMutationResult>(
      `/Accounting/monthly-payroll/${encodeURIComponent(id)}/comments`,
      { commentInput },
    );
  }

  getSalaryReportDefaults(): Observable<AccountingSalaryReportDefaults> {
    return this.api.getAnObj<AccountingSalaryReportDefaults>('/Accounting/salary-report/defaults');
  }

  getSalaryReportMonths(): Observable<AccountingSelectOption[]> {
    return this.api.get<AccountingSelectOption[]>('/Accounting/salary-report/months');
  }

  getSalaryReport(opts: {
    year: number | string;
    startMonth: number | string;
    endMonth: number | string;
  }): Observable<AccountingMainSalaryReportRow[]> {
    const params = new HttpParams()
      .set('year', String(opts.year))
      .set('startMonth', String(opts.startMonth))
      .set('endMonth', String(opts.endMonth));
    return this.api.get<AccountingMainSalaryReportRow[]>('/Accounting/salary-report', { params });
  }

  getIncomeCapturingIndex(): Observable<AccountingIncomeCapturingIndex> {
    return this.api.getAnObj<AccountingIncomeCapturingIndex>('/Accounting/income/capturing-index');
  }

  getIncomePeriodReport(opts: {
    year: number | string;
    startMonth: number | string;
    endMonth: number | string;
  }): Observable<AccountingIncomeItem[]> {
    const params = new HttpParams()
      .set('year', String(opts.year))
      .set('startMonth', String(opts.startMonth))
      .set('endMonth', String(opts.endMonth));
    return this.api.get<AccountingIncomeItem[]>('/Accounting/income/period-report', { params });
  }

  getIncomeItemForEdit(id: string): Observable<AccountingIncomeItemEditForm> {
    return this.api.getAnObj<AccountingIncomeItemEditForm>(
      `/Accounting/income/${encodeURIComponent(id)}`,
    );
  }

  updateIncomeItem(item: AccountingIncomeItem): Observable<AccountingIncomeItemMutationResult> {
    return this.api.update<AccountingIncomeItemMutationResult>(
      `/Accounting/income/${encodeURIComponent(item.id)}`,
      item,
    );
  }

  deleteIncomeItem(id: string): Observable<AccountingIncomeItemMutationResult> {
    return this.api.delete<AccountingIncomeItemMutationResult>(
      `/Accounting/income/${encodeURIComponent(id)}`,
    );
  }

  confirmIncomeWeek(weekId: string): Observable<AccountingIncomeItemMutationResult> {
    return this.api.create<AccountingIncomeItemMutationResult>(
      `/Accounting/income/weeks/${encodeURIComponent(weekId)}/confirm`,
      {},
    );
  }

  getExpenditureCapturingIndex(): Observable<AccountingExpenditureCapturingIndex> {
    return this.api.getAnObj<AccountingExpenditureCapturingIndex>(
      '/Accounting/expenditure/capturing-index',
    );
  }

  getExpenditurePeriodReport(opts: {
    year: number | string;
    startMonth: number | string;
    endMonth: number | string;
  }): Observable<AccountingIncomeItem[]> {
    const params = new HttpParams()
      .set('year', String(opts.year))
      .set('startMonth', String(opts.startMonth))
      .set('endMonth', String(opts.endMonth));
    return this.api.get<AccountingIncomeItem[]>('/Accounting/expenditure/period-report', {
      params,
    });
  }

  getExpenseItemForEdit(id: string): Observable<AccountingIncomeItemEditForm> {
    return this.api.getAnObj<AccountingIncomeItemEditForm>(
      `/Accounting/expenditure/${encodeURIComponent(id)}`,
    );
  }

  updateExpenseItem(item: AccountingIncomeItem): Observable<AccountingIncomeItemMutationResult> {
    return this.api.update<AccountingIncomeItemMutationResult>(
      `/Accounting/expenditure/${encodeURIComponent(item.id)}`,
      item,
    );
  }

  deleteExpenseItem(id: string): Observable<AccountingIncomeItemMutationResult> {
    return this.api.delete<AccountingIncomeItemMutationResult>(
      `/Accounting/expenditure/${encodeURIComponent(id)}`,
    );
  }

  confirmExpenditureWeek(weekId: string): Observable<AccountingIncomeItemMutationResult> {
    return this.api.create<AccountingIncomeItemMutationResult>(
      `/Accounting/expenditure/weeks/${encodeURIComponent(weekId)}/confirm`,
      {},
    );
  }

  getIncomeCategoryIndex(): Observable<AccountingIncomeCategoryIndex> {
    return this.api.getAnObj<AccountingIncomeCategoryIndex>('/Accounting/income-categories/index');
  }

  getIncomeCategories(): Observable<AccountingIncomeCategory[]> {
    return this.api.get<AccountingIncomeCategory[]>('/Accounting/income-categories');
  }

  updateIncomeCategory(
    category: AccountingIncomeCategory,
  ): Observable<AccountingIncomeCategoryMutationResult> {
    return this.api.update<AccountingIncomeCategoryMutationResult>(
      `/Accounting/income-categories/${encodeURIComponent(category.id)}`,
      category,
    );
  }

  deleteIncomeCategory(id: string): Observable<AccountingIncomeCategoryMutationResult> {
    return this.api.delete<AccountingIncomeCategoryMutationResult>(
      `/Accounting/income-categories/${encodeURIComponent(id)}`,
    );
  }

  getIncomeCategoryMonths(): Observable<AccountingSelectOption[]> {
    return this.api.get<AccountingSelectOption[]>('/Accounting/income-categories/months');
  }

  getIncomeCategoryPeriodReport(opts: {
    year: number | string;
    startMonth: number | string;
    endMonth: number | string;
    categoryId: string;
  }): Observable<AccountingIncomeItem[]> {
    const params = new HttpParams()
      .set('year', String(opts.year))
      .set('startMonth', String(opts.startMonth))
      .set('endMonth', String(opts.endMonth))
      .set('categoryId', opts.categoryId);
    return this.api.get<AccountingIncomeItem[]>('/Accounting/income-categories/period-report', {
      params,
    });
  }

  getIncomeCategoryAnalysis(opts: {
    year: number | string;
    startMonth: number | string;
    endMonth: number | string;
    sortType: number | string;
    numberToPick: number | string;
    categoryId?: string | null;
  }): Observable<AccountingIncomeItem[]> {
    let params = new HttpParams()
      .set('year', String(opts.year))
      .set('startMonth', String(opts.startMonth))
      .set('endMonth', String(opts.endMonth))
      .set('sortType', String(opts.sortType))
      .set('numberToPick', String(opts.numberToPick));
    if (opts.categoryId != null && opts.categoryId !== '') {
      params = params.set('categoryId', opts.categoryId);
    }
    return this.api.get<AccountingIncomeItem[]>('/Accounting/income-categories/analysis', {
      params,
    });
  }

  getExpenditureCategoryIndex(): Observable<AccountingExpenditureCategoryIndex> {
    return this.api.getAnObj<AccountingExpenditureCategoryIndex>(
      '/Accounting/expenditure-categories/index',
    );
  }

  getExpenditureCategories(): Observable<AccountingExpenditureCategory[]> {
    return this.api.get<AccountingExpenditureCategory[]>('/Accounting/expenditure-categories');
  }

  updateExpenditureCategory(
    category: AccountingExpenditureCategory,
  ): Observable<AccountingExpenditureCategoryMutationResult> {
    return this.api.update<AccountingExpenditureCategoryMutationResult>(
      `/Accounting/expenditure-categories/${encodeURIComponent(category.id)}`,
      category,
    );
  }

  deleteExpenditureCategory(
    id: string,
  ): Observable<AccountingExpenditureCategoryMutationResult> {
    return this.api.delete<AccountingExpenditureCategoryMutationResult>(
      `/Accounting/expenditure-categories/${encodeURIComponent(id)}`,
    );
  }

  getExpenditureCategoryMonths(): Observable<AccountingSelectOption[]> {
    return this.api.get<AccountingSelectOption[]>('/Accounting/expenditure-categories/months');
  }

  getExpenditureCategoryPeriodReport(opts: {
    year: number | string;
    startMonth: number | string;
    endMonth: number | string;
    categoryId: string;
  }): Observable<AccountingIncomeItem[]> {
    const params = new HttpParams()
      .set('year', String(opts.year))
      .set('startMonth', String(opts.startMonth))
      .set('endMonth', String(opts.endMonth))
      .set('categoryId', opts.categoryId);
    return this.api.get<AccountingIncomeItem[]>('/Accounting/expenditure-categories/period-report', {
      params,
    });
  }

  getExpenditureCategoryAnalysis(opts: {
    year: number | string;
    startMonth: number | string;
    endMonth: number | string;
    sortType: number | string;
    numberToPick: number | string;
    categoryId?: string | null;
  }): Observable<AccountingIncomeItem[]> {
    let params = new HttpParams()
      .set('year', String(opts.year))
      .set('startMonth', String(opts.startMonth))
      .set('endMonth', String(opts.endMonth))
      .set('sortType', String(opts.sortType))
      .set('numberToPick', String(opts.numberToPick));
    if (opts.categoryId != null && opts.categoryId !== '') {
      params = params.set('categoryId', opts.categoryId);
    }
    return this.api.get<AccountingIncomeItem[]>('/Accounting/expenditure-categories/analysis', {
      params,
    });
  }

  getOptionalFeesCategories(): Observable<AccountingOptionalFeesCategory[]> {
    return this.api.get<AccountingOptionalFeesCategory[]>('/Accounting/optional-fees-categories');
  }

  updateOptionalFeesCategory(
    category: AccountingOptionalFeesCategory,
  ): Observable<AccountingOptionalFeesCategoryMutationResult> {
    return this.api.update<AccountingOptionalFeesCategoryMutationResult>(
      `/Accounting/optional-fees-categories/${encodeURIComponent(category.id)}`,
      category,
    );
  }

  deleteOptionalFeesCategory(
    id: string,
  ): Observable<AccountingOptionalFeesCategoryMutationResult> {
    return this.api.delete<AccountingOptionalFeesCategoryMutationResult>(
      `/Accounting/optional-fees-categories/${encodeURIComponent(id)}`,
    );
  }

  createOptionalFeesItem(model: {
    itemCategoryId: string;
    itemName: string;
    cost: number;
  }): Observable<AccountingCreateOptionalFeesItemResult> {
    return this.api.create<AccountingCreateOptionalFeesItemResult>(
      '/Accounting/optional-fees-items',
      model,
    );
  }

  getOptionalFeesItems(categoryId?: string): Observable<AccountingOptionalFeesItem[]> {
    const params = categoryId
      ? new HttpParams().set('categoryId', categoryId)
      : undefined;
    return this.api.get<AccountingOptionalFeesItem[]>('/Accounting/optional-fees-items', {
      params,
    });
  }

  getOptionalFeesItem(id: string): Observable<AccountingOptionalFeesItem> {
    return this.api.getAnObj<AccountingOptionalFeesItem>(
      `/Accounting/optional-fees-items/${encodeURIComponent(id)}`,
    );
  }

  updateOptionalFeesItem(
    item: AccountingOptionalFeesItemUpdate,
  ): Observable<AccountingOptionalFeesItemMutationResult> {
    return this.api.update<AccountingOptionalFeesItemMutationResult>(
      `/Accounting/optional-fees-items/${encodeURIComponent(item.id)}`,
      item,
    );
  }

  deleteOptionalFeesItem(id: string): Observable<AccountingOptionalFeesItemMutationResult> {
    return this.api.delete<AccountingOptionalFeesItemMutationResult>(
      `/Accounting/optional-fees-items/${encodeURIComponent(id)}`,
    );
  }

  getOptionalChargesAssignmentForm(): Observable<AccountingOptionalChargesAssignmentForm> {
    return this.api.getAnObj<AccountingOptionalChargesAssignmentForm>(
      '/Accounting/optional-charges-assignment/form',
    );
  }

  getOptionalChargesAssignmentArms(classId: string): Observable<AccountingSelectOption[]> {
    const params = new HttpParams().set('classId', classId);
    return this.api.get<AccountingSelectOption[]>('/Accounting/optional-charges-assignment/arms', {
      params,
    });
  }

  getOptionalChargesAssignmentStudents(opts: {
    armId: string;
    categoryId: string;
    catStatus?: number;
  }): Observable<AccountingOptionalChargesAssignmentStudents> {
    let params = new HttpParams()
      .set('armId', opts.armId)
      .set('categoryId', opts.categoryId);
    if (opts.catStatus != null) {
      params = params.set('catStatus', String(opts.catStatus));
    }
    return this.api.getAnObj<AccountingOptionalChargesAssignmentStudents>(
      '/Accounting/optional-charges-assignment/students',
      { params },
    );
  }

  assignOptionalCharges(model: {
    armId: string;
    categoryId: string;
    studentIds: string[];
  }): Observable<AccountingOptionalChargesAssignmentResult> {
    return this.api.create<AccountingOptionalChargesAssignmentResult>(
      '/Accounting/optional-charges-assignment/assign',
      model,
    );
  }

  unassignOptionalCharges(model: {
    armId: string;
    categoryId: string;
  }): Observable<AccountingOptionalChargesAssignmentResult> {
    return this.api.create<AccountingOptionalChargesAssignmentResult>(
      '/Accounting/optional-charges-assignment/unassign',
      model,
    );
  }

  getOptionalPaymentDefaultersForm(): Observable<AccountingOptionalPaymentDefaultersForm> {
    return this.api.getAnObj<AccountingOptionalPaymentDefaultersForm>(
      '/Accounting/optional-payment-defaulters/form',
    );
  }

  getOptionalPaymentDefaultersTerms(sessionId: string): Observable<AccountingSelectOption[]> {
    const params = new HttpParams().set('sessionId', sessionId);
    return this.api.get<AccountingSelectOption[]>('/Accounting/optional-payment-defaulters/terms', {
      params,
    });
  }

  getTermlyOptionalPaymentDefaulters(
    sessionId: string,
    termStatus: number | string,
  ): Observable<AccountingOptionalPaymentDefaulters> {
    const params = new HttpParams()
      .set('sessionId', sessionId)
      .set('termStatus', String(termStatus));
    return this.api.getAnObj<AccountingOptionalPaymentDefaulters>(
      '/Accounting/optional-payment-defaulters/termly',
      { params },
    );
  }

  getYearlyOptionalPaymentDefaulters(
    sessionId: string,
  ): Observable<AccountingOptionalPaymentDefaulters> {
    const params = new HttpParams().set('sessionId', sessionId);
    return this.api.getAnObj<AccountingOptionalPaymentDefaulters>(
      '/Accounting/optional-payment-defaulters/yearly',
      { params },
    );
  }

  getTermlyOptionalPaymentReport(
    sessionId: string,
    termStatus: number | string,
  ): Observable<AccountingOptionalPaymentReport> {
    const params = new HttpParams()
      .set('sessionId', sessionId)
      .set('termStatus', String(termStatus));
    return this.api.getAnObj<AccountingOptionalPaymentReport>(
      '/Accounting/optional-payment-report/termly',
      { params },
    );
  }

  getYearlyOptionalPaymentReport(sessionId: string): Observable<AccountingOptionalPaymentReport> {
    const params = new HttpParams().set('sessionId', sessionId);
    return this.api.getAnObj<AccountingOptionalPaymentReport>(
      '/Accounting/optional-payment-report/yearly',
      { params },
    );
  }

  getStudentOptionalPaymentHistory(studentId: string): Observable<AccountingOptionalPaymentReport> {
    return this.api.getAnObj<AccountingOptionalPaymentReport>(
      `/Accounting/optional-payment-report/student-history/${encodeURIComponent(studentId)}`,
    );
  }

  getOptionalPaymentRangeReport(opts: {
    startDate: string;
    endDate: string;
    categoryId?: string;
  }): Observable<AccountingOptionalRangeReport> {
    let params = new HttpParams()
      .set('startDate', opts.startDate)
      .set('endDate', opts.endDate);
    if (opts.categoryId) {
      params = params.set('categoryId', opts.categoryId);
    }
    return this.api.getAnObj<AccountingOptionalRangeReport>(
      '/Accounting/optional-payment-report/range',
      { params },
    );
  }

  getOptionalPaymentHomeForm(): Observable<AccountingOptionalPaymentHomeForm> {
    return this.api.getAnObj<AccountingOptionalPaymentHomeForm>(
      '/Accounting/optional-payment-home/form',
    );
  }

  getOptionalPaymentProcessing(opts: {
    mode: 'arm' | 'class' | 'all';
    sessionId: string;
    termStatus: number | string;
    sessionText?: string;
    currentClass?: number | string;
    armName?: number | string;
  }): Observable<AccountingOptionalPaymentProcessing> {
    let params = new HttpParams()
      .set('mode', opts.mode)
      .set('sessionId', opts.sessionId)
      .set('termStatus', String(opts.termStatus));
    if (opts.sessionText) {
      params = params.set('sessionText', opts.sessionText);
    }
    if (opts.currentClass != null && opts.currentClass !== '') {
      params = params.set('currentClass', String(opts.currentClass));
    }
    if (opts.armName != null && opts.armName !== '') {
      params = params.set('armName', String(opts.armName));
    }
    return this.api.getAnObj<AccountingOptionalPaymentProcessing>(
      '/Accounting/optional-payment-home/processing',
      { params },
    );
  }
}

export type AccountingSetSchoolFeesForm = {
  id: string;
  schoolId: string;
  yearlyAndTermlyStatusId: string;
  freshPlayClass: string;
  freshPreNursery: string;
  freshNursery: string;
  freshPrimary: string;
  freshJss: string;
  freshScience: string;
  freshArt: string;
  freshCommercial: string;
  retnPlayClass: string;
  retnPreNursery: string;
  retnNursery: string;
  retnPrimary: string;
  retnJss: string;
  retnScience: string;
  retnArt: string;
  retnCommercial: string;
};

export type AccountingSetSchoolFeesLoad = {
  hasCurrentTerm: boolean;
  fees?: AccountingSetSchoolFeesForm | null;
};

export type AccountingSetSchoolFeesSaveResult = {
  status: number;
  message: string;
};

/** Legacy FeesPaymentStatus */
export type FeesPaymentStatus = 'None' | 'PartPayment' | 'Completed';

export type AccountingNewStudentFeesStudent = {
  studentLevelPaymentId: string;
  admisionNo: string;
  fullName: string;
  classLabel: string;
  armName: string;
  currentClass: string;
  classCategoryStatus: number;
  currentTermPaymentStatus: FeesPaymentStatus | number;
};

export type AccountingNewStudentFeesLoad = {
  feesConfigured: boolean;
  infoMessage?: string | null;
  students: AccountingNewStudentFeesStudent[];
};

export type AccountingReturningStudentFeesStudent = AccountingNewStudentFeesStudent & {
  otherTermsPaymentStatus: FeesPaymentStatus | number;
};

export type AccountingReturningStudentFeesLoad = {
  feesConfigured: boolean;
  infoMessage?: string | null;
  students: AccountingReturningStudentFeesStudent[];
};

export type AccountingPayFreshierItem = {
  id: string;
  classCategoryStatus: number;
  paymentId: string;
  paymentType: number;
  paymentDate?: string | null;
};

export type AccountingPaymentReceipt = {
  admisionNo: string;
  fullName: string;
  classLabel: string;
  termAndSession: string;
  fees: string;
  schoolLogo: string;
  schoolAddress: string;
  accountSignature: string;
  paymentId: string;
  paymentDate: string;
};

export type AccountingPayFreshiersResult = AccountingPaymentReceipt[] | string;

export type AccountingFreshPartPaymentRow = {
  studentAcademicLevelId: string;
  session: string;
  termStatus: string;
  actualFees: string;
  amountPaid: string;
  outstanding: string;
};

export type AccountingFreshPartPaymentRequest = {
  studentLevelPaymentId: string;
  hisFee: number;
  actualFees: number;
  admisnNo: string;
  studentName: string;
  termAndSession: string;
  classAndArm: string;
  paymentId: string;
  paymentType: number;
  discount: number;
  reasonForDiscount: string;
  paymentDate?: string | null;
};

export type AccountingFreshPartPaymentReceipt = {
  admisnNo?: string;
  studentName?: string;
  classAndArm?: string;
  termAndSession?: string;
  schoolId?: string;
  schoolLogo?: string;
  schoolName?: string;
  schoolAddress?: string;
  schoolTel?: string;
  accountSignature?: string;
  previousDebt?: string;
  amountPaid?: string;
  outStandingDebt?: string;
  discount?: string;
  paymentDate?: string;
  paymentId?: string;
};

/** Legacy TermlyReportOfReturningDto row. */
export type AccountingTermlyReportRow = {
  rowStatus: number | string;
  termAndSession: string;
  classCategory: number | string;
  fees: number;
  clearedStuds: number;
  clearedStudsDetails?: string | null;
  takeHome1: number;
  discount: number;
  partlyClearedStuds: number;
  partlyClearedStudsDetails?: string | null;
  takeHome2: number;
  outstanding1: number;
  takeHomeAndOutstanding?: string | null;
  unClearStuds: number;
  unClearStudsDetails?: string | null;
  outstanding2: number;
};

export type AccountingPaymentTypeTotal = {
  paymentType: number;
  totalPayment: number;
};

/** Legacy TermlyPaymentReportDto. */
export type AccountingTermlyPaymentReport = {
  termlyReportOfReturning: AccountingTermlyReportRow[];
  paymentTypeTotal: AccountingPaymentTypeTotal[];
};

export type AccountingExecutiveReportDefaults = {
  sessions: AccountingSelectOption[];
  terms: AccountingSelectOption[];
};

/** Legacy StudentFieldExtraction4Accounting2ndDto (payment-history search). */
export type AccountingPaymentHistoryStudent = {
  studentLevelPaymentId: string;
  admisionNo: string;
  fullName: string;
  classLabel: string;
  armName: string | number;
  currentClass: string | number;
  classCategoryStatus: number | string;
  studentStatus?: number | string;
};

/** Legacy StudentTermlyFeesPaymentHistoryDto. */
export type AccountingTermlyFeesPaymentHistory = {
  studentAcademicLevelId: string;
  amountPaid: number;
  paymentDate: string;
  paymentType: number | string;
};

/** Legacy StudentAcademicProgressHistoryStrokPaymentDto. */
export type AccountingStudentPaymentHistoryTerm = {
  session: string;
  termStatus: number | string;
  studentAcademicLevelId: string;
  discount: number;
  actualFees: number;
  studentTermlyFeesHistory: AccountingTermlyFeesPaymentHistory[];
};

export type AccountingStudentPaymentHistoryLoad = {
  admsnNo: string;
  studentName: string;
  terms: AccountingStudentPaymentHistoryTerm[];
};

export type AccountingCreateOptionalFeesCategoryResult = {
  succeeded: boolean;
  message: string;
};

export type AccountingCreateIncomeCategoryResult = {
  succeeded: boolean;
  message: string;
};

export type AccountingCreateExpenditureCategoryResult = {
  succeeded: boolean;
  message: string;
};

export type AccountingRegisterIncomeForm = {
  hasCategories: boolean;
  message: string;
  capturedDate: string;
  dateDisplay: string;
  weekPosition: string;
  categories: AccountingSelectOption[];
};

export type AccountingCreateIncomeResult = {
  status: number;
  message: string;
};

export type AccountingRegisterExpenditureForm = {
  hasCategories: boolean;
  message: string;
  capturedDate: string;
  dateDisplay: string;
  weekPosition: string;
  categories: AccountingSelectOption[];
};

export type AccountingCreateExpenditureResult = {
  status: number;
  message: string;
};

/** Legacy EmployeesXtractionDto / EmployeeList. */
export type AccountingEmployeeListItem = {
  id: string;
  fullName: string;
  employeeStatus: number;
  employeeStatusLabel: string;
  employeeIncomeStructureStatus: number;
};

/** Legacy EmployeeIncomeStructureDto. */
export type AccountingEmployeeIncomeStructure = {
  id: string;
  employeeId: string;
  fullName: string;
  employeeQualificationStatus: number;
  basicSalary: number;
  allowance: number;
  qualifications: AccountingSelectOption[];
};

export type AccountingEmployeeIncomeStructureSave = {
  id: string;
  employeeId: string;
  employeeQualificationStatus: number;
  basicSalary: number;
  allowance: number;
};

export type AccountingEmployeeIncomeStructureMutationResult = {
  status: number;
  message: string;
};

export type AccountingPayrollStructureRow = {
  employeeId: string;
  fullName: string;
  employeeIdNo: string;
  basicSalary: number;
  allowance: number;
};

export type AccountingMonthlyPaymentRow = {
  id: string;
  employeeMonthlyPaymentLevelId: string;
  employeeId: string;
  fullName: string;
  employeeIdNo: string;
  basicSalary: number;
  allowance: number;
  bonus: number;
  deduction: number;
  employeeMonthlyPaymentStatus: number;
  paymentType: number;
  comments: string;
  monthId: string;
  year: number;
  month: number;
  levelStatus: number;
};

export type AccountingMonthlyPaymentSlip = {
  unConfirmedMonthlyPaymentId?: string | null;
  paymentDate: string;
  mode: 'structure' | 'payments' | string;
  structures?: AccountingPayrollStructureRow[] | null;
  payments?: AccountingMonthlyPaymentRow[] | null;
};

export type AccountingMonthlyPaymentSave = {
  id?: string;
  employeeId: string;
  employeeMonthlyPaymentLevelId?: string;
  basicSalary: number;
  allowance: number;
  bonus: number;
  deduction: number;
  employeeMonthlyPaymentStatus: number;
  paymentType: number;
  comments?: string;
};

export type AccountingMonthlyPayrollMutationResult = {
  status: number;
  message: string;
};

export type AccountingSubordinateSalaryReport = {
  mainSalaryReportId: string;
  cashNo: number;
  cashAmount: number;
  tellerNo: number;
  tellerAmount: number;
  transferNo: number;
  transferAmount: number;
};

export type AccountingMainSalaryReportRow = {
  id: string;
  month: string;
  monthNumber: number;
  noOfEmpl: number;
  basicSalary: number;
  allowance: number;
  bonus: number;
  deduction: number;
  subordinateSalaryReport: AccountingSubordinateSalaryReport;
};

export type AccountingSalaryReportDefaults = {
  years: AccountingSelectOption[];
  months: AccountingSelectOption[];
};

export type AccountingIncomeCapturingIndex = {
  years: AccountingSelectOption[];
  months: AccountingSelectOption[];
  items: AccountingIncomeItem[];
  currentWeekId?: string | null;
};

export type AccountingExpenditureCapturingIndex = {
  years: AccountingSelectOption[];
  months: AccountingSelectOption[];
  items: AccountingIncomeItem[];
  currentWeekId?: string | null;
};

export type AccountingIncomeItemEditForm = {
  item: AccountingIncomeItem;
  categories: AccountingSelectOption[];
};

export type AccountingIncomeItemMutationResult = {
  status: number;
  message: string;
};

export type AccountingIncomeCategory = {
  id: string;
  schoolId: string;
  categoryName: string;
};

export type AccountingIncomeCategoryMutationResult = {
  status: number;
  message: string;
};

export type AccountingIncomeCategoryIndex = {
  years: AccountingSelectOption[];
  months: AccountingSelectOption[];
  categoryOptions: AccountingSelectOption[];
  categories: AccountingIncomeCategory[];
};

export type AccountingExpenditureCategory = {
  id: string;
  schoolId: string;
  categoryName: string;
};

export type AccountingExpenditureCategoryMutationResult = {
  status: number;
  message: string;
};

export type AccountingExpenditureCategoryIndex = {
  years: AccountingSelectOption[];
  months: AccountingSelectOption[];
  categoryOptions: AccountingSelectOption[];
  categories: AccountingExpenditureCategory[];
};

export type AccountingIncomeItem = {
  id: string;
  itemCategoryId: string;
  categoryName: string;
  capturedDate: string;
  createdDate?: string;
  weekId: string;
  weekOfMonth: string;
  cost: number;
  details: string;
};

export type AccountingOptionalFeesCategory = {
  id: string;
  schoolId: string;
  categoryName: string;
};

export type AccountingOptionalFeesCategoryMutationResult = {
  status: number;
  message: string;
};

/** Legacy CreateOptionalFeeItem: 0 ok, 1 no current term, 2 duplicate. */
export type AccountingCreateOptionalFeesItemResult = {
  status: number;
  message: string;
};

export type AccountingOptionalFeesItem = {
  id: string;
  itemCategoryId: string;
  categoryName: string;
  itemName: string;
  cost: number;
  yearlyAndTermlyStatusId: string;
  createdDate: string;
};

export type AccountingOptionalFeesItemUpdate = {
  id: string;
  itemCategoryId: string;
  yearlyAndTermlyStatusId: string;
  itemName: string;
  cost: number;
};

export type AccountingOptionalFeesItemMutationResult = {
  status: number;
  message: string;
};

export type AccountingOptionalChargesAssignmentForm = {
  categories: AccountingSelectOption[];
  classes: AccountingSelectOption[];
  arms: AccountingSelectOption[];
};

export type AccountingOptionalAssignmentStudent = {
  id: string;
  fullName: string;
  idNo: string;
  classCategory: number;
  assigned: boolean;
};

export type AccountingOptionalChargesAssignmentStudents = {
  students: AccountingOptionalAssignmentStudent[];
  assignedStudentIds: string[];
  message?: string | null;
};

export type AccountingOptionalChargesAssignmentResult = {
  succeeded: boolean;
  message: string;
};

export type AccountingOptionalPaymentDefaultersForm = {
  sessions: AccountingSelectOption[];
  terms: AccountingSelectOption[];
};

export type AccountingOptionalDefaulterPayment = {
  paymentDate: string;
  paymentType: number;
  paymentTypeLabel: string;
  amount: number;
};

export type AccountingOptionalDefaulterDiscount = {
  studentTermlyOptionalChargesId: string;
  studentId: string;
  discountAmount: number;
  reasonsForDiscount: string;
};

export type AccountingOptionalDefaulterCharge = {
  id: string;
  studentId: string;
  fullName: string;
  idNo: string;
  classId: string;
  classLabel: string;
  classArmId: string;
  armName: string;
  yearlyAndTermlyStatusId: string;
  termStatus: number;
  termStatusLabel: string;
  session: string;
  itemName: string;
  optionalFeesItemId?: string;
  cost: number;
  payments: AccountingOptionalDefaulterPayment[];
};

export type AccountingOptionalPaymentDefaulters = {
  totalOutstanding: number;
  totalDiscount: number;
  charges: AccountingOptionalDefaulterCharge[];
  discounts: AccountingOptionalDefaulterDiscount[];
};

export type AccountingOptionalPaymentReport = {
  totalPayment: number;
  totalDiscount: number;
  charges: AccountingOptionalDefaulterCharge[];
  discounts: AccountingOptionalDefaulterDiscount[];
};

export type AccountingOptionalRangeCharge = {
  id: string;
  studentId: string;
  fullName: string;
  idNo: string;
  classId: string;
  classLabel: string;
  classArmId: string;
  armName: string;
  yearlyAndTermlyStatusId: string;
  termStatus: number;
  termStatusLabel: string;
  session: string;
  sessionId: string;
  categoryName: string;
  cost: number;
  amountPaid: number;
  assignedDate: string;
  assignedYear: number;
};

export type AccountingOptionalRangeReport = {
  categoryLabel: string;
  numberOfItems: number;
  totalCharges: number;
  totalPayment: number;
  totalDiscount: number;
  totalOutstanding: number;
  charges: AccountingOptionalRangeCharge[];
  discounts: AccountingOptionalDefaulterDiscount[];
};

export type AccountingOptionalPaymentHomeForm = {
  sessions: AccountingSelectOption[];
  terms: AccountingSelectOption[];
  classes: AccountingSelectOption[];
  arms: AccountingSelectOption[];
};

export type AccountingOptionalPaymentProcessingCharge = {
  id: string;
  studentId: string;
  fullName: string;
  idNo: string;
  classId: string;
  classLabel: string;
  currentClass: number;
  classArmId: string;
  armName: string;
  itemCategoryId: string;
  categoryName: string;
  itemName: string;
  cost: number;
  amountPaid: number;
  discountTotal: number;
  outstanding: number;
};

export type AccountingOptionalPaymentProcessing = {
  sessionAndTerm: string;
  message?: string | null;
  charges: AccountingOptionalPaymentProcessingCharge[];
};

/** Legacy jQueryDateCalender.js formatDate — dd/mm/yyyy → d-Mon-yyyy */
export function formatLegacyPaymentDatePreview(dateString: string): string {
  const parts = dateString.split('/');
  if (parts.length !== 3) {
    return dateString;
  }
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  const day = parseInt(parts[0] ?? '', 10);
  const monthIndex = parseInt(parts[1] ?? '', 10) - 1;
  const year = parts[2];
  if (!Number.isFinite(day) || monthIndex < 0 || monthIndex > 11) {
    return dateString;
  }
  return `${day}-${months[monthIndex]}-${year}`;
}

/** Preview label from a calendar Date (same shape as legacy formatDate). */
export function formatPaymentDatePreviewFromDate(date: Date | null | undefined): string {
  if (!date || Number.isNaN(date.getTime())) {
    return '';
  }
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  return `${date.getDate()}-${months[date.getMonth()]}-${date.getFullYear()}`;
}

/** Date-only payload (avoids toISOString() shifting the calendar day across timezones). */
export function toPaymentDatePayload(date: Date | null | undefined): string | null {
  if (!date || Number.isNaN(date.getTime())) {
    return null;
  }
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/** Legacy currentDate() on termly fee receipts — d-Mon-yyyy. */
export function formatReceiptPrintedOnDate(date: Date = new Date()): string {
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  return `${date.getDate()}-${months[date.getMonth()]}-${date.getFullYear()}`;
}

/** Legacy parseDateAsAppropriate — dd/mm/yyyy */
export function parseLegacyPaymentDate(dateString: string): Date | null {
  const parts = dateString.split('/');
  if (parts.length !== 3) {
    return null;
  }
  const day = parseInt(parts[0] ?? '', 10);
  const month = parseInt(parts[1] ?? '', 10) - 1;
  const year = parseInt(parts[2] ?? '', 10);
  if (!Number.isFinite(day) || !Number.isFinite(month) || !Number.isFinite(year)) {
    return null;
  }
  return new Date(year, month, day);
}

/** Legacy Tools.js formatCurrency */
export function formatCurrencyInput(currencyValue: string): string {
  const raw = String(currencyValue ?? '');
  let dotAndValueAfterIt = '';
  let status = false;
  if (raw.includes('.')) {
    const splitValue = raw.split('.');
    dotAndValueAfterIt = splitValue[1] ?? '';
    status = true;
  }

  const replaceValue = raw.replace(/,/g, '');
  const entryToFloat = parseFloat(replaceValue);
  if (!Number.isFinite(entryToFloat)) {
    return '';
  }

  const formatedValue = entryToFloat.toFixed(2).replace(/(\d)(?=(\d\d\d)+(?!\d))/g, '$1,');
  const splitValue2Nd = formatedValue.split('.');
  let retVal = splitValue2Nd[0] ?? '';

  if (status === true) {
    if (dotAndValueAfterIt !== '') {
      dotAndValueAfterIt = String(parseFloat(dotAndValueAfterIt));
    }
    retVal = `${splitValue2Nd[0]}.${dotAndValueAfterIt}`;
  }
  return retVal;
}

/** Legacy FormatAsCurrency — clear invalid one-char / empty. */
export function applyFormatAsCurrency(value: string): string {
  const pValue = String(value ?? '');
  if (pValue === '' || (Number.isNaN(Number(pValue.replace(/,/g, ''))) && pValue.length === 1) || pValue === 'Na' || pValue === 'N') {
    return '';
  }
  return formatCurrencyInput(pValue);
}

/** Legacy currency display: en-GB then £ → ₦ */
export function formatNaira(amount: number | null | undefined): string {
  const value = Number(amount ?? 0);
  return value.toLocaleString('en-GB', { style: 'currency', currency: 'GBP' }).replace('£', '₦');
}

const MONTH_ABBREVIATIONS = [
  '',
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
] as const;

/** Display 9 → "Sep", 10 → "Oct"; keeps Select/-1 as-is. */
export function formatMonthLabel(monthValue: string | number | null | undefined): string {
  const raw = String(monthValue ?? '').trim();
  if (!raw || raw === '-1') {
    return raw === '-1' ? 'Select' : raw;
  }

  const month = Number(raw);
  if (!Number.isInteger(month) || month < 1 || month > 12) {
    return raw;
  }

  return MONTH_ABBREVIATIONS[month];
}

export function withMonthAbbreviations(options: AccountingSelectOption[]): AccountingSelectOption[] {
  return (options ?? []).map(option => ({
    ...option,
    text: formatMonthLabel(option.value ?? option.text),
  }));
}
