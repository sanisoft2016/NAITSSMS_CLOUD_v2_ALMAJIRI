import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

import {
  AccountingApiService,
  AccountingIncomeStatement,
  AccountingSelectOption,
  formatNaira,
  withMonthAbbreviations,
} from './accounting.service';

/** Display model matching legacy Index AJAX itemList[0]–[7] mapping. */
export type IncomeStatementView = {
  otherExpenses: string;
  salaryPayment: string;
  totalExpenditure: string;
  totalIncome: string;
  netProfitHtml: SafeHtml;
  otherIncome: string;
  fixedFeePayment: string;
  optionalFeePayment: string;
};

@Component({
  selector: 'app-accounting-income-statement',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule],
  templateUrl: './accounting-income-statement.html',
  styleUrl: './accounting-income-statement.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingIncomeStatementPage implements OnInit {
  private readonly api = inject(AccountingApiService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly loading = signal(true);
  readonly refreshing = signal(false);
  readonly error = signal('');
  readonly years = signal<AccountingSelectOption[]>([]);
  readonly months = signal<AccountingSelectOption[]>([]);
  readonly year = signal('-1');
  readonly startMonth = signal('-1');
  readonly endMonth = signal('-1');
  readonly view = signal<IncomeStatementView | null>(null);

  ngOnInit(): void {
    this.loading.set(true);
    this.api.getIncomeStatementDefaults().subscribe({
      next: defaults => {
        this.years.set(defaults.years ?? []);
        // Server already uses Jan–Dec; keep mapper as a safe fallback for numeric labels.
        this.months.set(withMonthAbbreviations(defaults.months ?? []));
        this.year.set(String(defaults.year || '-1'));
        this.startMonth.set(String(defaults.month || '-1'));
        this.endMonth.set(String(defaults.month || '-1'));
        this.view.set(defaults.statement ? this.toView(defaults.statement) : null);
        this.loading.set(false);
      },
      error: err => {
        this.error.set(err?.error?.message || err?.message || 'Failed to load income statement.');
        this.loading.set(false);
      },
    });
  }

  /**
   * Legacy AccountingBase/Index.cshtml:
   * $("#startMonth, #endMonth").on("change", ...) — year alone does not reload.
   * Skips when the changed month value is "-1".
   */
  onMonthChange(which: 'start' | 'end', rawValue: string): void {
    if (which === 'start') {
      this.startMonth.set(String(rawValue ?? ''));
    } else {
      this.endMonth.set(String(rawValue ?? ''));
    }

    const changedValue = String(rawValue ?? '');
    if (changedValue === '-1') {
      return;
    }

    this.loadStatementForSelectedPeriod();
  }

  onYearChange(rawValue: string): void {
    // Legacy does not AJAX-reload on year change; keep selection only.
    this.year.set(String(rawValue ?? ''));
  }

  private loadStatementForSelectedPeriod(): void {
    const year = Number(this.year());
    const startMonth = Number(this.startMonth());
    const endMonth = Number(this.endMonth());

    // Legacy still fires the AJAX when the *changed* control is not -1, even if the other
    // month is -1; the server then receives an invalid byte. We only call when both months
    // are valid calendar months (same practical outcome as a successful legacy response).
    if (!Number.isFinite(year) || year <= 0 || year === -1) {
      return;
    }
    if (
      !Number.isFinite(startMonth) ||
      !Number.isFinite(endMonth) ||
      startMonth < 1 ||
      startMonth > 12 ||
      endMonth < 1 ||
      endMonth > 12
    ) {
      return;
    }

    this.refreshing.set(true);
    this.error.set('');
    this.api.getIncomeStatement(year, startMonth, endMonth).subscribe({
      next: statement => {
        // Legacy: $("#pageContent").html(...) from data.split('&')
        this.view.set(this.toView(statement));
        this.refreshing.set(false);
      },
      error: err => {
        this.error.set(
          err?.error?.detail ||
            err?.error?.message ||
            err?.message ||
            'Failed to refresh income statement.',
        );
        this.refreshing.set(false);
      },
    });
  }

  /**
   * Map API DTO using legacy delimited indexes when present
   * (0 expenses, 1 salary, 2 total exp, 3 total income, 4 net, 5 other income, 6 fixed, 7 optional).
   */
  private toView(statement: AccountingIncomeStatement): IncomeStatementView {
    const parts = String(statement.legacyDelimited ?? '').split('&');
    const hasLegacy = parts.length >= 8;

    const otherExpenses = hasLegacy ? parts[0] : formatNaira(statement.otherExpenses);
    const salaryPayment = hasLegacy ? parts[1] : formatNaira(statement.salaryPayment);
    const totalExpenditure = hasLegacy ? parts[2] : formatNaira(statement.totalExpenditure);
    const totalIncome = hasLegacy ? parts[3] : formatNaira(statement.totalIncome);
    const otherIncome = hasLegacy ? parts[5] : formatNaira(statement.otherIncome);
    const fixedFeePayment = hasLegacy ? parts[6] : formatNaira(statement.fixedFeePayment);
    const optionalFeePayment = hasLegacy ? parts[7] : formatNaira(statement.optionalFeePayment);

    let netProfitHtml: SafeHtml;
    if (hasLegacy) {
      const net = parts[4] ?? '';
      // Legacy AJAX: itemList[4].charAt(0) !== "<" → blue, else red
      if (net.charAt(0) === '<') {
        netProfitHtml = this.sanitizer.bypassSecurityTrustHtml(
          `<span style="color:red">${net}</span>`,
        );
      } else {
        netProfitHtml = this.sanitizer.bypassSecurityTrustHtml(
          `<span style="color:blue">${net}</span>`,
        );
      }
    } else if (statement.netProfit < 0) {
      netProfitHtml = this.sanitizer.bypassSecurityTrustHtml(
        `<span style="color:red"><strong>-</strong> (${formatNaira(Math.abs(statement.netProfit))})</span>`,
      );
    } else {
      netProfitHtml = this.sanitizer.bypassSecurityTrustHtml(
        `<span style="color:blue">${formatNaira(statement.netProfit)}</span>`,
      );
    }

    return {
      otherExpenses,
      salaryPayment,
      totalExpenditure,
      totalIncome,
      netProfitHtml,
      otherIncome,
      fixedFeePayment,
      optionalFeePayment,
    };
  }
}
