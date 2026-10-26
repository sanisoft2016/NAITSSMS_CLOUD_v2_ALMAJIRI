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

import {
  AccountingApiService,
  AccountingExpenditureCategory,
  AccountingIncomeItem,
  AccountingSelectOption,
  formatNaira,
} from './accounting.service';
import {
  consumeAccountingFlashSuccess,
  writeAccountingOptionId,
} from '../../theme/sidemenu/accounting-menu.config';

type PageMode = 'list' | 'period' | 'analysis';

type PeriodMonthGroup = {
  month: number;
  year: number;
  monthLabel: string;
  total: number;
  weeks: PeriodWeekGroup[];
};

type PeriodWeekGroup = {
  weekId: string;
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
  selector: 'app-accounting-view-expenditure-category',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule],
  templateUrl: './accounting-view-expenditure-category.html',
  styleUrl: './accounting-view-expenditure-category.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingViewExpenditureCategoryPage implements OnInit {
  private readonly api = inject(AccountingApiService);

  readonly loading = signal(true);
  readonly reportLoading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly flashSuccess = signal('');

  readonly mode = signal<PageMode>('list');
  readonly categories = signal<AccountingExpenditureCategory[]>([]);
  readonly years = signal<AccountingSelectOption[]>([]);
  readonly months = signal<AccountingSelectOption[]>([]);
  readonly categoryOptions = signal<AccountingSelectOption[]>([]);

  readonly year = signal('-1');
  readonly startMonth = signal('-1');
  readonly endMonth = signal('-1');
  readonly itemCategoryId = signal('');

  readonly showCategoryFilter = signal(true);
  readonly analysisCategoryMode = signal('0'); // 0 All, 1 Selected
  readonly sortType = signal('0');
  readonly numberToPick = signal('');
  readonly analysisCategoryId = signal('');

  readonly periodItems = signal<AccountingIncomeItem[]>([]);
  readonly analysisItems = signal<AccountingIncomeItem[]>([]);

  readonly showEditModal = signal(false);
  readonly editId = signal('');
  readonly editSchoolId = signal('');
  readonly editCategoryName = signal('');

  readonly periodTotal = computed(() =>
    this.periodItems().reduce((sum, row) => sum + (Number(row.cost) || 0), 0),
  );

  readonly periodCategoryName = computed(
    () => this.periodItems()[0]?.categoryName || '',
  );

  readonly periodGroups = computed(() => this.buildPeriodGroups(this.periodItems()));

  /** Legacy ToString("C").Substring(1) — amount without currency glyph. */
  formatCost(amount: number | null | undefined): string {
    const formatted = formatNaira(amount);
    return formatted.replace(/^₦\s?/, '').replace(/^£\s?/, '');
  }

  ngOnInit(): void {
    writeAccountingOptionId('2');
    this.flashSuccess.set(consumeAccountingFlashSuccess());
    this.loadIndex();
  }

  refreshPage(): void {
    this.mode.set('list');
    this.showCategoryFilter.set(true);
    this.periodItems.set([]);
    this.analysisItems.set([]);
    this.loadIndex();
  }

  loadReportAnalysis(): void {
    // Legacy LoadReportAnalysisPage — replace page content, remove category1st
    this.mode.set('analysis');
    this.showCategoryFilter.set(false);
    this.analysisItems.set([]);
    this.periodItems.set([]);
    this.analysisCategoryMode.set('0');
    this.sortType.set('0');
    this.numberToPick.set('');
    this.analysisCategoryId.set('');
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
      error: () => {
        this.tryLoadPeriodReport();
      },
    });
  }

  onPeriodFilterChange(): void {
    this.tryLoadPeriodReport();
  }

  onAnalysisCategoryModeChange(): void {
    if (this.analysisCategoryMode() === '0') {
      this.analysisCategoryId.set('');
    } else if (this.categoryOptions().length > 0 && !this.analysisCategoryId()) {
      this.analysisCategoryId.set(this.categoryOptions()[0].value);
    }
  }

  getAnalysisResult(fromButton = false): void {
    const numberToPick = this.numberToPick().trim();
    if (!numberToPick) {
      if (fromButton) {
        window.alert('Number of record to display is required in order to continue.');
      }
      return;
    }

    const year = this.year();
    const startMonth = this.startMonth();
    const endMonth = this.endMonth();
    if (year === '-1' || startMonth === '-1' || endMonth === '-1') {
      if (fromButton) {
        window.alert('Please select Year, Start Month and End Month.');
      }
      return;
    }

    const categoryId =
      this.analysisCategoryMode() === '1' ? this.analysisCategoryId() || '0' : '0';

    this.reportLoading.set(true);
    this.api
      .getExpenditureCategoryAnalysis({
        year,
        startMonth,
        endMonth,
        sortType: this.sortType(),
        numberToPick,
        categoryId,
      })
      .subscribe({
        next: rows => {
          this.analysisItems.set(Array.isArray(rows) ? rows : []);
          this.reportLoading.set(false);
        },
        error: err => {
          this.reportLoading.set(false);
          this.analysisItems.set([]);
          window.alert(
            err?.error?.message || err?.message || 'Failed to load analysis report.',
          );
        },
      });
  }

  openEdit(item: AccountingExpenditureCategory): void {
    this.editId.set(item.id);
    this.editSchoolId.set(item.schoolId);
    this.editCategoryName.set(item.categoryName);
    this.showEditModal.set(true);
  }

  closeEditModal(): void {
    this.showEditModal.set(false);
    this.editId.set('');
    this.editSchoolId.set('');
    this.editCategoryName.set('');
  }

  saveCategoryChanges(): void {
    const name = this.editCategoryName().trim();
    if (name.length > 50) {
      window.alert('Category length can not be more than 50 letters long.');
      return;
    }

    this.saving.set(true);
    this.api
      .updateExpenditureCategory({
        id: this.editId(),
        schoolId: this.editSchoolId(),
        categoryName: name,
      })
      .subscribe({
        next: result => {
          this.saving.set(false);
          if (Number(result?.status) === 0) {
            window.alert(result.message || 'The changes has been succcessfully saved.');
            this.closeEditModal();
            this.loadIndex(false);
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

  deleteCategory(item: AccountingExpenditureCategory): void {
    if (
      !window.confirm(
        "Are you sure you want to delete this category? If 'Yes' click 'Ok' to continue.",
      )
    ) {
      return;
    }

    this.api.deleteExpenditureCategory(item.id).subscribe({
      next: result => {
        if (Number(result?.status) === 0) {
          window.alert(result.message || 'Record deleted Successfully.');
          this.loadIndex(false);
          return;
        }
        window.alert(
          result?.message ||
            'Failed! The category has expenditure item(s) to it and therefore, we can not delete it.',
        );
      },
      error: err => {
        window.alert(
          err?.error?.message ||
            err?.message ||
            'Failed! The category has expenditure item(s) to it and therefore, we can not delete it.',
        );
      },
    });
  }

  formatCapturedDate(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return value;
    }
    const month = MONTH_NAMES[date.getMonth() + 1] || '';
    return `${date.getDate()} ${month}, ${date.getFullYear()}`;
  }

  formatDayMonth(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return value;
    }
    const month = MONTH_NAMES[date.getMonth() + 1] || '';
    return `${date.getDate()} ${month}`;
  }

  analysisHeading(): string {
    const start = this.monthLabel(this.startMonth());
    const end = this.monthLabel(this.endMonth());
    const sortLabel = this.sortType() === '0' ? 'Highest' : 'Lowest';
    const n = this.numberToPick();
    const year = this.year();
    if (this.analysisCategoryMode() === '1' && this.analysisItems().length > 0) {
      const cat = this.analysisItems()[0].categoryName;
      return `The ${sortLabel} ${n} ${cat} expenses within the month of ${start} and ${end} ${year} is as follow`;
    }
    return `The ${sortLabel} ${n} expenses item(s)within the month of ${start} and ${end} ${year} is as follow`;
  }

  private loadIndex(showSpinner = true): void {
    if (showSpinner) {
      this.loading.set(true);
    }
    this.error.set('');
    this.api.getExpenditureCategoryIndex().subscribe({
      next: data => {
        this.years.set(data?.years ?? []);
        this.months.set(data?.months ?? []);
        this.categoryOptions.set(data?.categoryOptions ?? []);
        this.categories.set(data?.categories ?? []);

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
        if (!this.itemCategoryId() && (data?.categoryOptions ?? []).length > 0) {
          this.itemCategoryId.set(data!.categoryOptions[0].value);
        }

        this.loading.set(false);
        if (this.mode() === 'list') {
          // keep list
        }
      },
      error: err => {
        this.loading.set(false);
        this.categories.set([]);
        this.error.set(
          err?.error?.message || err?.message || 'Failed to load expenditure categories.',
        );
      },
    });
  }

  private tryLoadPeriodReport(): void {
    if (this.mode() === 'analysis') {
      return;
    }

    const year = this.year();
    const startMonth = this.startMonth();
    const endMonth = this.endMonth();
    const categoryId = this.itemCategoryId();

    // Legacy: only runs when changed value !== "-1" and categoryId is defined
    if (year === '-1' || startMonth === '-1' || endMonth === '-1' || !categoryId) {
      return;
    }

    this.mode.set('period');
    this.reportLoading.set(true);
    this.api
      .getExpenditureCategoryPeriodReport({ year, startMonth, endMonth, categoryId })
      .subscribe({
        next: rows => {
          this.periodItems.set(Array.isArray(rows) ? rows : []);
          this.reportLoading.set(false);
        },
        error: err => {
          this.reportLoading.set(false);
          this.periodItems.set([]);
          this.error.set(
            err?.error?.message || err?.message || 'Failed to load period report.',
          );
        },
      });
  }

  private monthLabel(value: string): string {
    const n = Number(value);
    if (n >= 1 && n <= 12) {
      return MONTH_NAMES[n];
    }
    const opt = this.months().find(m => m.value === value);
    return opt?.text || value;
  }

  private buildPeriodGroups(items: AccountingIncomeItem[]): PeriodMonthGroup[] {
    const byMonth = new Map<string, AccountingIncomeItem[]>();
    for (const item of items) {
      const date = new Date(item.capturedDate);
      const key = `${date.getFullYear()}-${date.getMonth() + 1}`;
      const list = byMonth.get(key) ?? [];
      list.push(item);
      byMonth.set(key, list);
    }

    const groups: PeriodMonthGroup[] = [];
    for (const [, monthItems] of byMonth) {
      const first = monthItems[0];
      const date = new Date(first.capturedDate);
      const month = date.getMonth() + 1;
      const year = date.getFullYear();
      const monthLabel = MONTH_NAMES[month] || String(month);

      const byWeek = new Map<string, AccountingIncomeItem[]>();
      for (const item of [...monthItems].sort((a, b) =>
        String(a.weekId).localeCompare(String(b.weekId)),
      )) {
        const list = byWeek.get(item.weekId) ?? [];
        list.push(item);
        byWeek.set(item.weekId, list);
      }

      const weeks: PeriodWeekGroup[] = [];
      for (const [weekId, weekItems] of byWeek) {
        weeks.push({
          weekId,
          weekLabel: `${weekItems[0]?.weekOfMonth || ''} Week of ${monthLabel}`,
          total: weekItems.reduce((s, r) => s + (Number(r.cost) || 0), 0),
          items: weekItems,
        });
      }

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
