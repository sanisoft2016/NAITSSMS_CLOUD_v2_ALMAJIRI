import { LegacySideMenuItem } from './teacher-menu.config';

export type AccountingOptionId = '0' | '1' | '2' | '3' | '4' | '5';

export type AccountingOptionChoice = {
  value: AccountingOptionId;
  label: string;
};

/** Legacy Accounting `_Layout.cshtml` #optionType dropdown. */
export const ACCOUNTING_OPTION_CHOICES: AccountingOptionChoice[] = [
  { value: '0', label: 'Student Fees Management' },
  { value: '5', label: 'Student Optional Fees Management' },
  { value: '4', label: 'Other Income Management' },
  { value: '1', label: 'Employee Payment Management' },
  { value: '2', label: 'Expenditure Management' },
  { value: '3', label: 'Income Statement' },
];

/**
 * Legacy `EmployeeIncome/SideNavContent.cshtml` menus keyed by optionId.
 * optionId "3" (Income Statement) intentionally has no side links.
 */
export const ACCOUNTING_SIDE_MENUS: Record<AccountingOptionId, LegacySideMenuItem[]> = {
  '0': [
    { route: '/accounting', label: 'My Dashboard', icon: 'dashboard', exact: true, nowrap: true },
    {
      route: '/accounting/set-school-fees',
      label: 'Set school fees / registration',
      icon: 'money',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/new-student-fees',
      label: 'New Student fees',
      icon: 'user-plus',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/returning-student-fees',
      label: 'Returning student fees',
      icon: 'users',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/executive-report',
      label: 'Executive Report',
      icon: 'bar-chart',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/student-payment-history',
      label: 'Student Payment History',
      icon: 'history',
      iconSet: 'fa',
      nowrap: true,
    },
  ],
  '1': [
    {
      route: '/accounting/employees-income-structure',
      label: 'Employees Income Structure',
      icon: 'id-card-o',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/monthly-payroll',
      label: 'Monthly payroll',
      icon: 'calendar',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/salary-report',
      label: 'Salary report',
      icon: 'file-text-o',
      iconSet: 'fa',
      nowrap: true,
    },
  ],
  '2': [
    {
      route: '/accounting/create-expenditure-category',
      label: 'Create Expenditure Category',
      icon: 'plus-square',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/view-expenditure-category',
      label: 'View Expenditure Category',
      icon: 'list',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/record-expenditure',
      label: 'Record Expenditure',
      icon: 'pencil-square-o',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/view-expenditure',
      label: 'View Expenditure and more',
      icon: 'folder-open',
      iconSet: 'fa',
      nowrap: true,
    },
  ],
  '3': [],
  '4': [
    {
      route: '/accounting/create-income-category',
      label: 'Create Income Category',
      icon: 'plus-square',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/view-income-category',
      label: 'View Income Category',
      icon: 'list',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/record-income',
      label: 'Record Income',
      icon: 'pencil-square-o',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/view-income',
      label: 'View Income and more',
      icon: 'folder-open',
      iconSet: 'fa',
      nowrap: true,
    },
  ],
  '5': [
    {
      route: '/accounting/create-optional-fees-category',
      label: 'Create Optional Fees Category',
      icon: 'plus-square',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/view-optional-fees-category',
      label: 'View Optional Fees Category',
      icon: 'list',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/create-optional-fees-item',
      label: 'Create Optional Fees Item',
      icon: 'plus',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/view-optional-fees-items',
      label: 'View Optional Fees Items',
      icon: 'th-list',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/optional-charges-assignment',
      label: 'Student Optional Charges Assignment',
      icon: 'link',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/optional-payment-defaulters',
      label: 'Student Optional Payment Defaulters',
      icon: 'exclamation-triangle',
      iconSet: 'fa',
      nowrap: true,
    },
    {
      route: '/accounting/optional-payment-report',
      label: 'Student Optional Payment Report',
      icon: 'file-text-o',
      iconSet: 'fa',
      nowrap: true,
    },
  ],
};

export const ACCOUNTING_OPTION_STORAGE_KEY = 'accountingOptionId';

/** One-shot flash after redirect (legacy ShowMessage → TempData). */
export const ACCOUNTING_FLASH_SUCCESS_KEY = 'accountingFlashSuccess';
export const ACCOUNTING_FLASH_WARN_KEY = 'accountingFlashWarn';

export function writeAccountingFlashSuccess(message: string): void {
  try {
    sessionStorage.setItem(ACCOUNTING_FLASH_SUCCESS_KEY, message);
  } catch {
    /* ignore */
  }
}

export function consumeAccountingFlashSuccess(): string {
  try {
    const message = String(sessionStorage.getItem(ACCOUNTING_FLASH_SUCCESS_KEY) ?? '').trim();
    sessionStorage.removeItem(ACCOUNTING_FLASH_SUCCESS_KEY);
    return message;
  } catch {
    return '';
  }
}

export function writeAccountingFlashWarn(message: string): void {
  try {
    sessionStorage.setItem(ACCOUNTING_FLASH_WARN_KEY, message);
  } catch {
    /* ignore */
  }
}

export function consumeAccountingFlashWarn(): string {
  try {
    const message = String(sessionStorage.getItem(ACCOUNTING_FLASH_WARN_KEY) ?? '').trim();
    sessionStorage.removeItem(ACCOUNTING_FLASH_WARN_KEY);
    return message;
  } catch {
    return '';
  }
}

/** Legacy TempData["direction"] = 1 after RegisterIncome needs confirm. */
export const ACCOUNTING_INCOME_CONFIRM_KEY = 'accountingIncomeConfirmDirection';

export function writeAccountingIncomeConfirmNeeded(): void {
  try {
    sessionStorage.setItem(ACCOUNTING_INCOME_CONFIRM_KEY, '1');
  } catch {
    /* ignore */
  }
}

export function consumeAccountingIncomeConfirmNeeded(): boolean {
  try {
    const value = String(sessionStorage.getItem(ACCOUNTING_INCOME_CONFIRM_KEY) ?? '').trim();
    sessionStorage.removeItem(ACCOUNTING_INCOME_CONFIRM_KEY);
    return value === '1';
  } catch {
    return false;
  }
}

/** Legacy TempData["direction"] = 1 after RegisterExpenses needs confirm. */
export const ACCOUNTING_EXPENDITURE_CONFIRM_KEY = 'accountingExpenditureConfirmDirection';

export function writeAccountingExpenditureConfirmNeeded(): void {
  try {
    sessionStorage.setItem(ACCOUNTING_EXPENDITURE_CONFIRM_KEY, '1');
  } catch {
    /* ignore */
  }
}

export function consumeAccountingExpenditureConfirmNeeded(): boolean {
  try {
    const value = String(sessionStorage.getItem(ACCOUNTING_EXPENDITURE_CONFIRM_KEY) ?? '').trim();
    sessionStorage.removeItem(ACCOUNTING_EXPENDITURE_CONFIRM_KEY);
    return value === '1';
  } catch {
    return false;
  }
}

export function readAccountingOptionId(): AccountingOptionId {
  try {
    const raw = sessionStorage.getItem(ACCOUNTING_OPTION_STORAGE_KEY);
    if (raw === '0' || raw === '1' || raw === '2' || raw === '3' || raw === '4' || raw === '5') {
      return raw;
    }
  } catch {
    /* ignore */
  }
  return '0';
}

export function writeAccountingOptionId(optionId: AccountingOptionId): void {
  try {
    sessionStorage.setItem(ACCOUNTING_OPTION_STORAGE_KEY, optionId);
  } catch {
    /* ignore */
  }
}

export function accountingMenuForOption(optionId: AccountingOptionId): LegacySideMenuItem[] {
  return ACCOUNTING_SIDE_MENUS[optionId] ?? ACCOUNTING_SIDE_MENUS['0'];
}
