import { Routes } from '@angular/router';

import { AccountingPlaceholder } from './accounting-placeholder';
import { AccountingDashboard } from './accounting-dashboard';
import { AccountingIncomeStatementPage } from './accounting-income-statement';
import { AccountingSetSchoolFeesPage } from './accounting-set-school-fees';
import { AccountingNewStudentFeesPage } from './accounting-new-student-fees';
import { AccountingReturningStudentFeesPage } from './accounting-returning-student-fees';
import { AccountingExecutiveReportPage } from './accounting-executive-report';
import { AccountingStudentPaymentHistoryPage } from './accounting-student-payment-history';
import { AccountingCreateOptionalFeesCategoryPage } from './accounting-create-optional-fees-category';
import { AccountingViewOptionalFeesCategoryPage } from './accounting-view-optional-fees-category';
import { AccountingCreateOptionalFeesItemPage } from './accounting-create-optional-fees-item';
import { AccountingViewOptionalFeesItemsPage } from './accounting-view-optional-fees-items';
import { AccountingOptionalChargesAssignmentPage } from './accounting-optional-charges-assignment';
import { AccountingOptionalPaymentDefaultersPage } from './accounting-optional-payment-defaulters';
import { AccountingOptionalPaymentReportPage } from './accounting-optional-payment-report';
import { AccountingCreateIncomeCategoryPage } from './accounting-create-income-category';
import { AccountingViewIncomeCategoryPage } from './accounting-view-income-category';
import { AccountingRecordIncomePage } from './accounting-record-income';
import { AccountingViewIncomePage } from './accounting-view-income';
import { AccountingEmployeesIncomeStructurePage } from './accounting-employees-income-structure';
import { AccountingMonthlyPayrollPage } from './accounting-monthly-payroll';
import { AccountingSalaryReportPage } from './accounting-salary-report';
import { AccountingCreateExpenditureCategoryPage } from './accounting-create-expenditure-category';
import { AccountingViewExpenditureCategoryPage } from './accounting-view-expenditure-category';
import { AccountingRecordExpenditurePage } from './accounting-record-expenditure';
import { AccountingViewExpenditurePage } from './accounting-view-expenditure';
import { CreateSubordinateAccountant } from '../supper-admin/create-subordinate-accountant/create-subordinate-accountant';

function page(path: string, title: string): Routes[number] {
  return {
    path,
    component: AccountingPlaceholder,
    data: { title },
  };
}

export const routes: Routes = [
  { path: '', component: AccountingDashboard, data: { title: 'My Dashboard' } },
  {
    path: 'income-statement',
    component: AccountingIncomeStatementPage,
    data: { title: 'Income Statement' },
  },
  {
    path: 'set-school-fees',
    component: AccountingSetSchoolFeesPage,
    data: { title: 'Set school fees / registration' },
  },
  {
    path: 'create-subordinate-accountant',
    component: CreateSubordinateAccountant,
    data: { title: 'Create Subordinate Accountant' },
  },
  {
    path: 'new-student-fees',
    component: AccountingNewStudentFeesPage,
    data: { title: 'New Student fees' },
  },
  {
    path: 'returning-student-fees',
    component: AccountingReturningStudentFeesPage,
    data: { title: 'Returning student fees' },
  },
  {
    path: 'executive-report',
    component: AccountingExecutiveReportPage,
    data: { title: 'Executive Report' },
  },
  {
    path: 'student-payment-history',
    component: AccountingStudentPaymentHistoryPage,
    data: { title: 'Student Payment History' },
  },
  {
    path: 'employees-income-structure',
    component: AccountingEmployeesIncomeStructurePage,
    data: { title: 'Employees Income Structure' },
  },
  {
    path: 'monthly-payroll',
    component: AccountingMonthlyPayrollPage,
    data: { title: 'Monthly payroll' },
  },
  {
    path: 'salary-report',
    component: AccountingSalaryReportPage,
    data: { title: 'Salary report' },
  },
  {
    path: 'create-expenditure-category',
    component: AccountingCreateExpenditureCategoryPage,
    data: { title: 'Create Expenditure Category' },
  },
  {
    path: 'view-expenditure-category',
    component: AccountingViewExpenditureCategoryPage,
    data: { title: 'View Expenditure Category' },
  },
  {
    path: 'record-expenditure',
    component: AccountingRecordExpenditurePage,
    data: { title: 'Record Expenditure' },
  },
  {
    path: 'view-expenditure',
    component: AccountingViewExpenditurePage,
    data: { title: 'View Expenditure and more' },
  },
  {
    path: 'create-income-category',
    component: AccountingCreateIncomeCategoryPage,
    data: { title: 'Create Income Category' },
  },
  {
    path: 'view-income-category',
    component: AccountingViewIncomeCategoryPage,
    data: { title: 'View Income Category' },
  },
  {
    path: 'record-income',
    component: AccountingRecordIncomePage,
    data: { title: 'Record Income' },
  },
  {
    path: 'view-income',
    component: AccountingViewIncomePage,
    data: { title: 'View Income and more' },
  },
  {
    path: 'create-optional-fees-category',
    component: AccountingCreateOptionalFeesCategoryPage,
    data: { title: 'Create Optional Fees Category' },
  },
  {
    path: 'view-optional-fees-category',
    component: AccountingViewOptionalFeesCategoryPage,
    data: { title: 'View Optional Fees Category' },
  },
  {
    path: 'create-optional-fees-item',
    component: AccountingCreateOptionalFeesItemPage,
    data: { title: 'Create Optional Fees Item' },
  },
  {
    path: 'view-optional-fees-items',
    component: AccountingViewOptionalFeesItemsPage,
    data: { title: 'View Optional Fees Items' },
  },
  {
    path: 'optional-charges-assignment',
    component: AccountingOptionalChargesAssignmentPage,
    data: { title: 'Student Optional Charges Assignment' },
  },
  {
    path: 'optional-payment-defaulters',
    component: AccountingOptionalPaymentDefaultersPage,
    data: { title: 'Student Optional Payment Defaulters' },
  },
  {
    path: 'optional-payment-report',
    component: AccountingOptionalPaymentReportPage,
    data: { title: 'Student Optional Payment Report' },
  },
];
