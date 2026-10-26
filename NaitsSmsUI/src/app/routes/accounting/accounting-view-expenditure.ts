import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDatepickerModule } from '@angular/material/datepicker';

import {
  AccountingApiService,
  AccountingIncomeItem,
  AccountingSelectOption,
  applyFormatAsCurrency,
  formatNaira,
} from './accounting.service';
import {
  consumeAccountingFlashSuccess,
  consumeAccountingFlashWarn,
  consumeAccountingExpenditureConfirmNeeded,
  writeAccountingOptionId,
} from '../../theme/sidemenu/accounting-menu.config';

type PageMode = 'recent' | 'period';

type PeriodMonthGroup = {
  month: number;
  year: number;
  monthLabel: string;
  total: number;
  weeks: PeriodWeekGroup[];
};

type PeriodWeekGroup = {
  weekKey: string;
  weekLabel: string;
  total: number;
  items: AccountingIncomeItem[];
};

const MONTH_NAMES = [
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
];

@Component({
  selector: 'app-accounting-view-expenditure',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule, MatDatepickerModule],
  templateUrl: './accounting-view-expenditure.html',
  styleUrl: './accounting-view-expenditure.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingViewExpenditurePage implements OnInit {
  private readonly api = inject(AccountingApiService);

  readonly loading = signal(true);
  readonly reportLoading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly flashSuccess = signal('');
  readonly flashWarn = signal('');

  readonly mode = signal<PageMode>('recent');
  readonly showConfirm = signal(false);

  readonly years = signal<AccountingSelectOption[]>([]);
  readonly months = signal<AccountingSelectOption[]>([]);
  readonly items = signal<AccountingIncomeItem[]>([]);
  readonly currentWeekId = signal('');
  readonly periodItems = signal<AccountingIncomeItem[]>([]);

  readonly year = signal('-1');
  readonly startMonth = signal('-1');
  readonly endMonth = signal('-1');

  readonly showEditModal = signal(false);
  readonly editId = signal('');
  readonly editWeekId = signal('');
  readonly editCategoryId = signal('');
  readonly editDetails = signal('');
  readonly editCostDisplay = signal('');
  readonly editCapturedDate = signal<Date | null>(null);
  readonly editCategories = signal<AccountingSelectOption[]>([]);

  readonly periodTotal = computed(() =>
    this.periodItems().reduce((sum, row) => sum + (Number(row.cost) || 0), 0),
  );

  readonly periodGroups = computed(() => this.buildPeriodGroups(this.periodItems()));

  readonly applyFormatAsCurrency = applyFormatAsCurrency;

  formatCost(amount: number | null | undefined): string {
    return formatNaira(amount).replace(/^₦\s?/, '').replace(/^£\s?/, '');
  }

  ngOnInit(): void {
    writeAccountingOptionId('2');
    this.flashSuccess.set(consumeAccountingFlashSuccess());
    this.flashWarn.set(consumeAccountingFlashWarn());
    this.showConfirm.set(consumeAccountingExpenditureConfirmNeeded());
    this.loadIndex();
  }

  refreshRecent(): void {
    this.mode.set('recent');
    this.periodItems.set([]);
    this.showConfirm.set(false);
    this.loadIndex();
  }

  onYearChange(): void {
    const selected = this.year();
    if (selected === '-1') {
      return;
    }

    this.api.getExpenditureCategoryMonths().subscribe({
      next: rows => {
        const months = Array.isArray(rows) ? rows : [];
        this.months.set(months);
        if (months.length > 0) {
          this.startMonth.set(months[0].value);
          this.endMonth.set(months[0].value);
        }
        this.tryLoadPeriodReport();
      },
      error: () => this.tryLoadPeriodReport(),
    });
  }

  onPeriodFilterChange(): void {
    this.tryLoadPeriodReport();
  }

  confirmExpenses(): void {
    const weekId = this.currentWeekId();
    if (!weekId) {
      return;
    }
    if (
      !window.confirm(
        "Are you sure you have confirmed all these list of expenses? If 'Yes' click 'Ok' to continue.",
      )
    ) {
      return;
    }

    this.api.confirmExpenditureWeek(weekId).subscribe({
      next: result => {
        if (Number(result?.status) === 0) {
          window.alert(result.message || 'Confirmation Successful.');
          this.showConfirm.set(false);
          this.loadIndex();
          return;
        }
        window.alert(result?.message || 'Failed! Unable to complete the process.');
      },
      error: err => {
        window.alert(err?.error?.message || err?.message || 'Failed! Unable to complete the process.');
      },
    });
  }

  openEdit(item: AccountingIncomeItem): void {
    this.api.getExpenseItemForEdit(item.id).subscribe({
      next: form => {
        const row = form?.item;
        if (!row) {
          window.alert('Failed! Unable to complete the process.');
          return;
        }
        this.editId.set(row.id);
        this.editWeekId.set(row.weekId);
        this.editCategoryId.set(row.itemCategoryId);
        this.editDetails.set(row.details || '');
        this.editCostDisplay.set(applyFormatAsCurrency(String(row.cost ?? '')));
        this.editCapturedDate.set(parseLocalDate(row.capturedDate));
        this.editCategories.set(form.categories ?? []);
        this.showEditModal.set(true);
      },
      error: err => {
        window.alert(err?.error?.message || err?.message || 'Failed! Unable to complete the process.');
      },
    });
  }

  closeEditModal(): void {
    this.showEditModal.set(false);
  }

  onEditCostInput(raw: string): void {
    this.editCostDisplay.set(applyFormatAsCurrency(raw));
  }

  saveEditedItem(): void {
    const details = this.editDetails().trim();
    if (details.length > 150) {
      window.alert('Failed! The detail you have entered is out of range.');
      return;
    }

    const costRaw = this.editCostDisplay().replace(/,/g, '');
    const cost = Number(costRaw);
    if (!Number.isFinite(cost) || cost === 0 || costRaw === '') {
      window.alert('Failed! You need to enter the amount to continue.');
      return;
    }

    const captured = this.editCapturedDate();
    if (!captured) {
      window.alert("The Item' Date field is required.");
      return;
    }

    this.saving.set(true);
    this.api
      .updateExpenseItem({
        id: this.editId(),
        weekId: this.editWeekId(),
        itemCategoryId: this.editCategoryId(),
        categoryName: '',
        details,
        cost,
        capturedDate: toIsoDateOnly(captured),
        weekOfMonth: '',
      })
      .subscribe({
        next: result => {
          this.saving.set(false);
          if (Number(result?.status) === 0) {
            window.alert(result.message || 'The changes has succcessfully been saved.');
            this.closeEditModal();
            if (this.mode() === 'period') {
              this.tryLoadPeriodReport();
            } else {
              this.loadIndex(false);
            }
            return;
          }
          window.alert(result?.message || 'Failed! Unable to complete the process.');
        },
        error: err => {
          this.saving.set(false);
          window.alert(
            err?.error?.message || err?.message || 'Failed! Unable to complete the process.',
          );
        },
      });
  }

  deleteItem(item: AccountingIncomeItem): void {
    const label = item.categoryName || item.itemCategoryId || 'this item';
    if (
      !window.confirm(
        `Are you sure you want to DELETE  "${label}"? If 'Yes',  click 'Ok' to continue.`,
      )
    ) {
      return;
    }

    this.api.deleteExpenseItem(item.id).subscribe({
      next: result => {
        if (Number(result?.status) === 0) {
          window.alert(result.message || 'Record successfully Deleted.');
          this.loadIndex(false);
          return;
        }
        window.alert(result?.message || 'Failed! Unable to complete the process.');
      },
      error: err => {
        window.alert(
          err?.error?.message || err?.message || 'Failed! Unable to complete the process.',
        );
      },
    });
  }

  formatLongDate(value: string | undefined): string {
    const date = parseLocalDate(value);
    const month = MONTH_NAMES[date.getMonth() + 1] || '';
    return `${date.getDate()} ${month}, ${date.getFullYear()}`;
  }

  formatDayMonth(value: string | undefined): string {
    const date = parseLocalDate(value);
    const month = MONTH_NAMES[date.getMonth() + 1] || '';
    return `${date.getDate()} ${month}`;
  }

  private loadIndex(showSpinner = true): void {
    if (showSpinner) {
      this.loading.set(true);
    }
    this.error.set('');
    this.api.getExpenditureCapturingIndex().subscribe({
      next: data => {
        this.years.set(data?.years ?? []);
        this.months.set(data?.months ?? []);
        this.items.set(data?.items ?? []);
        this.currentWeekId.set(data?.currentWeekId || data?.items?.[0]?.weekId || '');

        if (!this.year() || this.year() === '-1') {
          const firstYear = (data?.years ?? []).find(y => y.value !== '-1');
          if (firstYear) {
            this.year.set(firstYear.value);
          }
        }
        if (!this.startMonth() || this.startMonth() === '-1') {
          const firstMonth = (data?.months ?? []).find(m => m.value !== '-1');
          if (firstMonth) {
            this.startMonth.set(firstMonth.value);
            this.endMonth.set(firstMonth.value);
          }
        }

        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.items.set([]);
        this.error.set(err?.error?.message || err?.message || 'Failed to load expenditure items.');
      },
    });
  }

  private tryLoadPeriodReport(): void {
    const year = this.year();
    const startMonth = this.startMonth();
    const endMonth = this.endMonth();
    if (year === '-1' || startMonth === '-1' || endMonth === '-1') {
      return;
    }

    this.mode.set('period');
    this.reportLoading.set(true);
    this.api.getExpenditurePeriodReport({ year, startMonth, endMonth }).subscribe({
      next: rows => {
        this.periodItems.set(Array.isArray(rows) ? rows : []);
        this.reportLoading.set(false);
      },
      error: err => {
        this.reportLoading.set(false);
        this.periodItems.set([]);
        this.error.set(err?.error?.message || err?.message || 'Failed to load expenditure report.');
      },
    });
  }

  private buildPeriodGroups(items: AccountingIncomeItem[]): PeriodMonthGroup[] {
    const byMonth = new Map<string, AccountingIncomeItem[]>();
    for (const item of items) {
      const date = parseLocalDate(item.capturedDate);
      const key = `${date.getFullYear()}-${date.getMonth() + 1}`;
      const list = byMonth.get(key) ?? [];
      list.push(item);
      byMonth.set(key, list);
    }

    const groups: PeriodMonthGroup[] = [];
    for (const [, monthItems] of byMonth) {
      const first = monthItems[0];
      const date = parseLocalDate(first.capturedDate);
      const month = date.getMonth() + 1;
      const year = date.getFullYear();
      const monthLabel = MONTH_NAMES[month] || String(month);

      const byWeek = new Map<string, AccountingIncomeItem[]>();
      for (const item of monthItems) {
        const weekKey = item.weekOfMonth || item.weekId || 'Unknown';
        const list = byWeek.get(weekKey) ?? [];
        list.push(item);
        byWeek.set(weekKey, list);
      }

      const weeks: PeriodWeekGroup[] = [...byWeek.entries()]
        .sort(([a], [b]) => a.localeCompare(b))
        .map(([weekKey, weekItems]) => ({
          weekKey,
          weekLabel: `${weekItems[0]?.weekOfMonth || weekKey} Week of ${monthLabel}`,
          total: weekItems.reduce((s, r) => s + (Number(r.cost) || 0), 0),
          items: weekItems,
        }));

      groups.push({
        month,
        year,
        monthLabel,
        total: monthItems.reduce((s, r) => s + (Number(r.cost) || 0), 0),
        weeks,
      });
    }

    return groups;
  }
}

function parseLocalDate(value: string | Date | null | undefined): Date {
  if (value instanceof Date && !Number.isNaN(value.getTime())) {
    return new Date(value.getFullYear(), value.getMonth(), value.getDate());
  }
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}/.test(value)) {
    const [y, m, d] = value.slice(0, 10).split('-').map(Number);
    return new Date(y, (m ?? 1) - 1, d ?? 1);
  }
  const date = value ? new Date(value) : new Date();
  if (Number.isNaN(date.getTime())) {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), now.getDate());
  }
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

function toIsoDateOnly(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}
