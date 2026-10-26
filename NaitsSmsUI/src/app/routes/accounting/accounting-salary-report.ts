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
  AccountingMainSalaryReportRow,
  AccountingSelectOption,
  formatNaira,
} from './accounting.service';
import { writeAccountingOptionId } from '../../theme/sidemenu/accounting-menu.config';

@Component({
  selector: 'app-accounting-salary-report',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule],
  templateUrl: './accounting-salary-report.html',
  styleUrl: './accounting-salary-report.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingSalaryReportPage implements OnInit {
  private readonly api = inject(AccountingApiService);

  readonly loading = signal(true);
  readonly refreshing = signal(false);
  readonly error = signal('');
  readonly years = signal<AccountingSelectOption[]>([]);
  readonly months = signal<AccountingSelectOption[]>([]);
  readonly year = signal('-1');
  readonly startMonth = signal('-1');
  readonly endMonth = signal('-1');
  readonly rows = signal<AccountingMainSalaryReportRow[]>([]);

  readonly overall = computed(() => {
    const list = this.rows();
    const basic = list.reduce((s, r) => s + (Number(r.basicSalary) || 0), 0);
    const allowance = list.reduce((s, r) => s + (Number(r.allowance) || 0), 0);
    const bonus = list.reduce((s, r) => s + (Number(r.bonus) || 0), 0);
    const deduction = list.reduce((s, r) => s + (Number(r.deduction) || 0), 0);
    const cashNo = list.reduce((s, r) => s + (Number(r.subordinateSalaryReport?.cashNo) || 0), 0);
    const cashAmount = list.reduce(
      (s, r) => s + (Number(r.subordinateSalaryReport?.cashAmount) || 0),
      0,
    );
    const tellerNo = list.reduce(
      (s, r) => s + (Number(r.subordinateSalaryReport?.tellerNo) || 0),
      0,
    );
    const tellerAmount = list.reduce(
      (s, r) => s + (Number(r.subordinateSalaryReport?.tellerAmount) || 0),
      0,
    );
    const transferNo = list.reduce(
      (s, r) => s + (Number(r.subordinateSalaryReport?.transferNo) || 0),
      0,
    );
    const transferAmount = list.reduce(
      (s, r) => s + (Number(r.subordinateSalaryReport?.transferAmount) || 0),
      0,
    );
    return {
      basic,
      allowance,
      bonus,
      deduction,
      gross: basic + allowance + bonus,
      net: basic + allowance + bonus - deduction,
      cashNo,
      cashAmount,
      tellerNo,
      tellerAmount,
      transferNo,
      transferAmount,
    };
  });

  /** Legacy row: ToString("C").Substring(1). Totals: Replace('£','₦'). */
  formatAmount(amount: number | null | undefined): string {
    return formatNaira(amount).replace(/^₦\s?/, '').replace(/^£\s?/, '');
  }

  formatNairaAmount(amount: number | null | undefined): string {
    return formatNaira(amount);
  }

  ngOnInit(): void {
    writeAccountingOptionId('1');
    this.loading.set(true);
    this.api.getSalaryReportDefaults().subscribe({
      next: defaults => {
        const years = defaults.years ?? [];
        const months = defaults.months ?? [];
        this.years.set(years);
        this.months.set(months);
        this.year.set(String(years[0]?.value ?? '-1'));
        this.startMonth.set(String(months[0]?.value ?? '-1'));
        this.endMonth.set(String(months[0]?.value ?? '-1'));
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load salary report filters.');
        this.loading.set(false);
      },
    });
  }

  /** Legacy Year change → reload months only (year param ignored server-side). */
  onYearChange(raw: string): void {
    this.year.set(String(raw ?? ''));
    if (this.year() === '-1') {
      return;
    }

    this.api.getSalaryReportMonths().subscribe({
      next: months => {
        const list = months ?? [];
        this.months.set(list);
        this.startMonth.set(String(list[0]?.value ?? '-1'));
        this.endMonth.set(String(list[0]?.value ?? '-1'));
        this.rows.set([]);
      },
      error: () => {
        this.error.set('Failed to load months.');
      },
    });
  }

  /** Legacy $("#startMonth, #endMonth").on("change") — skip when value is "-1". */
  onMonthChange(which: 'start' | 'end', raw: string): void {
    if (which === 'start') {
      this.startMonth.set(String(raw ?? ''));
    } else {
      this.endMonth.set(String(raw ?? ''));
    }

    const changed = which === 'start' ? this.startMonth() : this.endMonth();
    if (changed === '-1') {
      return;
    }

    this.loadReport();
  }

  private loadReport(): void {
    const year = this.year();
    const startMonth = this.startMonth();
    const endMonth = this.endMonth();
    if (year === '-1' || startMonth === '-1' || endMonth === '-1') {
      return;
    }

    this.refreshing.set(true);
    this.error.set('');
    this.api
      .getSalaryReport({
        year,
        startMonth,
        endMonth,
      })
      .subscribe({
        next: rows => {
          this.rows.set(rows ?? []);
          this.refreshing.set(false);
        },
        error: () => {
          this.rows.set([]);
          this.refreshing.set(false);
          this.error.set('An error occurred while loading the salary report.');
        },
      });
  }
}
