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
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

import {
  AccountingApiService,
  AccountingSelectOption,
  AccountingTermlyPaymentReport,
  AccountingTermlyReportRow,
  formatNaira,
} from './accounting.service';
import { writeAccountingOptionId } from '../../theme/sidemenu/accounting-menu.config';
import {
  EXECUTIVE_REPORT_DETAIL_PRINT_STYLES,
  openReceiptPrintWindow,
} from './accounting-receipt-print';

type DetailStudentRow = {
  admisnNo: string;
  fullName: string;
  classLabel: string;
  classInt: number;
  arm: string;
  paymentHtml: SafeHtml;
};

type DetailModalState = {
  headLabel: string;
  actualFeeLabel: string;
  paymentHeading: string;
  rows: DetailStudentRow[];
  totalHtml: SafeHtml;
};

const CLASS_CATEGORY_LABELS: Record<number, string> = {
  0: 'PlayClass',
  1: 'PreNursery',
  2: 'Nursery',
  3: 'Primary',
  4: 'Jss',
  5: 'Science',
  6: 'Art',
  7: 'Commercial',
  8: 'None',
};

const ROW_STATUS_LABELS: Record<number, string> = {
  0: 'Fresher',
  1: 'Returning',
  2: 'Previous',
};

@Component({
  selector: 'app-accounting-executive-report',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule],
  templateUrl: './accounting-executive-report.html',
  styleUrl: './accounting-executive-report.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingExecutiveReportPage implements OnInit {
  private readonly api = inject(AccountingApiService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly loading = signal(true);
  readonly reportLoading = signal(false);
  readonly termsLoading = signal(false);
  readonly error = signal('');
  readonly sessions = signal<AccountingSelectOption[]>([]);
  readonly terms = signal<AccountingSelectOption[]>([]);
  readonly sessionId = signal('-1');
  readonly termId = signal('-1');
  /** Legacy ResultStatus: 1 = Termly (default), 0 = Yearly (term hidden). */
  readonly resultType = signal(1);
  readonly report = signal<AccountingTermlyPaymentReport | null>(null);
  readonly showDetailModal = signal(false);
  readonly detail = signal<DetailModalState | null>(null);

  readonly formatNaira = formatNaira;

  /** Legacy DisplayFor(double) — plain numeric, no ₦ / commas. */
  readonly formatPlain = (amount: number | null | undefined): string => {
    const value = Number(amount ?? 0);
    if (!Number.isFinite(value)) {
      return '0';
    }
    return Number.isInteger(value) ? String(value) : String(value);
  };

  readonly showTermFilter = computed(() => this.resultType() === 1);

  readonly reportTitle = computed(() => {
    const rows = this.report()?.termlyReportOfReturning ?? [];
    if (!rows.length) {
      return '';
    }
    const raw = String(rows[0].termAndSession ?? '');
    const parts = raw.split(',').map(p => p.trim()).filter(Boolean);
    // Legacy: list[0] + " Term, " + list[1]  →  "Second Term, 2025/2026 Academic Session Audit Report"
    if (parts.length >= 2) {
      const termPart = /term$/i.test(parts[0]) ? parts[0] : `${parts[0]} Term`;
      return `${termPart}, ${parts[1]} Academic Session Audit Report`;
    }
    return `${raw} Academic Session Audit Report`;
  });

  readonly orderedRows = computed(() => {
    const rows = [...(this.report()?.termlyReportOfReturning ?? [])];
    return rows.sort(
      (a, b) => this.classCategoryValue(a.classCategory) - this.classCategoryValue(b.classCategory),
    );
  });

  readonly totals = computed(() => {
    const rows = this.report()?.termlyReportOfReturning ?? [];
    const sum = (pick: (r: AccountingTermlyReportRow) => number) =>
      rows.reduce((acc, r) => acc + Number(pick(r) || 0), 0);
    return {
      clearedStuds: sum(r => r.clearedStuds),
      discount: sum(r => r.discount),
      takeHome1: sum(r => r.takeHome1),
      partlyClearedStuds: sum(r => r.partlyClearedStuds),
      takeHome2: sum(r => r.takeHome2),
      outstanding1: sum(r => r.outstanding1),
      unClearStuds: sum(r => r.unClearStuds),
      outstanding2: sum(r => r.outstanding2),
      totalPayment: sum(r => r.takeHome1) + sum(r => r.takeHome2),
      totalDiscount: sum(r => r.discount),
      totalOutstanding: sum(r => r.outstanding1) + sum(r => r.outstanding2),
    };
  });

  readonly paymentTypeTotals = computed(() => {
    const list = this.report()?.paymentTypeTotal ?? [];
    const pick = (type: number) =>
      Number(list.find(x => Number(x.paymentType) === type)?.totalPayment ?? 0);
    return {
      cash: pick(0),
      teller: pick(1),
      transfer: pick(2),
    };
  });

  ngOnInit(): void {
    writeAccountingOptionId('0');
    this.loading.set(true);
    this.api.getExecutiveReportDefaults().subscribe({
      next: defaults => {
        const sessions = this.normalizeOptions(defaults?.sessions);
        const terms = this.normalizeOptions(defaults?.terms);
        this.sessions.set(sessions);
        this.terms.set(terms);
        // Legacy DropDownList: first option selected; report loads only on Term change.
        if (sessions.length) {
          this.sessionId.set(String(sessions[0].value ?? '-1'));
        }
        if (terms.length) {
          this.termId.set(String(terms[0].value ?? '-1'));
        }
        this.report.set(null);
        this.loading.set(false);
      },
      error: err => {
        this.error.set(err?.error?.message || err?.message || 'Failed to load executive report.');
        this.loading.set(false);
      },
    });
  }

  onResultTypeChange(raw: string | number): void {
    const value = Number(raw);
    this.resultType.set(value);
    this.report.set(null);
    this.error.set('');
  }

  onSessionChange(raw: string): void {
    const sessionId = String(raw ?? '-1');
    this.sessionId.set(sessionId);
    this.termId.set('-1');
    this.report.set(null);
    this.error.set('');

    if (sessionId === '-1' || this.resultType() === 0) {
      this.terms.set([]);
      return;
    }

    this.termsLoading.set(true);
    this.api.getExecutiveReportTerms(sessionId).subscribe({
      next: terms => {
        const normalized = this.normalizeOptions(terms);
        this.terms.set(normalized);
        this.termsLoading.set(false);
        // Legacy TermDropdownForSelectedSession: replace Term dropdown; wait for Term change.
        if (normalized.length) {
          this.termId.set(String(normalized[0].value ?? '-1'));
        }
      },
      error: err => {
        this.termsLoading.set(false);
        this.error.set(err?.error?.message || err?.message || 'Failed to load terms.');
      },
    });
  }

  onTermChange(raw: string): void {
    const termId = String(raw ?? '-1');
    this.termId.set(termId);
    this.error.set('');
    if (termId === '-1' || this.sessionId() === '-1') {
      this.report.set(null);
      return;
    }
    this.loadTermlyReport(termId);
  }

  classCategoryLabel(value: number | string | null | undefined): string {
    if (typeof value === 'string' && value.trim() !== '' && Number.isNaN(Number(value))) {
      return value;
    }
    return CLASS_CATEGORY_LABELS[this.classCategoryValue(value)] ?? String(value ?? '');
  }

  rowStatusLabel(value: number | string | null | undefined): string {
    if (typeof value === 'string' && value.trim() !== '' && Number.isNaN(Number(value))) {
      return value;
    }
    const n = Number(value);
    return ROW_STATUS_LABELS[n] ?? String(value ?? '');
  }

  openDetail(parameters: string | null | undefined, headLabel: string): void {
    const raw = String(parameters ?? '').trim();
    if (!raw) {
      return;
    }
    this.detail.set(this.buildDetailModal(raw, headLabel));
    this.showDetailModal.set(true);
  }

  closeDetailModal(): void {
    this.showDetailModal.set(false);
    this.detail.set(null);
  }

  printDetail(): void {
    const el = document.getElementById('exec-report-detail-print');
    if (!el) {
      return;
    }
    openReceiptPrintWindow(
      el.outerHTML,
      'Executive Report Detail',
      EXECUTIVE_REPORT_DETAIL_PRINT_STYLES,
    );
  }

  private loadTermlyReport(termId: string): void {
    this.reportLoading.set(true);
    this.api.getExecutiveReportTermly(termId).subscribe({
      next: report => {
        this.report.set({
          termlyReportOfReturning: report?.termlyReportOfReturning ?? [],
          paymentTypeTotal: report?.paymentTypeTotal ?? [],
        });
        this.reportLoading.set(false);
      },
      error: err => {
        this.reportLoading.set(false);
        this.report.set(null);
        this.error.set(err?.error?.message || err?.message || 'Failed to load termly report.');
      },
    });
  }

  /** Legacy ExecutiveReport.cshtml detail() — parse ~ / X / - tokens. */
  private buildDetailModal(parameters: string, headLabel: string): DetailModalState {
    const list = parameters.split(',');
    const studentInfo: DetailStudentRow[] = [];
    let paymentHeading = 'Payment Fees';
    let actualFee = '';
    let payValue1Total = 0;
    let payValue2Total = 0;
    let totalPayment = 0;

    for (const entry of list) {
      const raw = (entry || '').trim();
      if (!raw) {
        continue;
      }
      const item = raw.split('~');
      if (item.length < 4 || item[3] == null) {
        continue;
      }
      const feesToken = String(item[3]).trim();
      if (!feesToken) {
        continue;
      }

      let paymentHtml = '';
      if (feesToken.includes('X')) {
        paymentHeading = 'Payment / Outstanding';
        const splitPayment = feesToken.split('X');
        const paid = Number(splitPayment[0] || 0);
        const outstanding = Number(splitPayment[1] || 0);
        const realFees = paid + outstanding;
        actualFee = formatNaira(realFees);
        payValue1Total += paid;
        payValue2Total += outstanding;
        paymentHtml = `${formatNaira(paid)} / <span class="text-danger">${formatNaira(outstanding)}</span>`;
      } else if (feesToken.includes('-')) {
        paymentHeading = 'Fees / Discount / Paid';
        const paymentStuff = feesToken.split('-');
        const feesAmt = Number(paymentStuff[0] || 0);
        const discountAmt = Number(paymentStuff[1] || 0);
        actualFee = formatNaira(feesAmt);
        const actualPayment = feesAmt !== 0 ? formatNaira(feesAmt) : '';
        const discount =
          discountAmt !== 0
            ? `<span style="background-color:yellow">${formatNaira(discountAmt)}</span>`
            : formatNaira(0);
        const net = feesAmt - discountAmt;
        payValue1Total += feesAmt;
        payValue2Total += discountAmt;
        paymentHtml = `${actualPayment} - ${discount}  = ${formatNaira(net)}`;
        totalPayment += feesAmt;
      } else {
        paymentHeading = 'Payment Fees';
        const fee = Number(feesToken);
        actualFee = formatNaira(fee);
        paymentHtml = formatNaira(fee);
        totalPayment += fee;
      }

      const classAndArm = String(item[2] ?? '').split('?');
      const classPart = classAndArm[0] ?? '';
      const lastChar = classPart.length ? classPart[classPart.length - 1] : '';
      studentInfo.push({
        admisnNo: item[0] ?? '',
        fullName: item[1] ?? '',
        classLabel: classPart,
        classInt: Number(classAndArm[1] || 0),
        arm: lastChar,
        paymentHtml: this.sanitizer.bypassSecurityTrustHtml(paymentHtml),
      });
    }

    studentInfo.sort((a, b) => a.classInt - b.classInt || a.arm.localeCompare(b.arm));

    let totalHtml: string;
    if (payValue1Total !== 0) {
      if (paymentHeading === 'Payment / Outstanding') {
        totalHtml = `${formatNaira(payValue1Total)} / ${formatNaira(payValue2Total)}`;
      } else {
        const net = payValue1Total - payValue2Total;
        totalHtml = `${formatNaira(payValue1Total)} - <span style="background-color:yellow">${formatNaira(payValue2Total)}</span> = ${formatNaira(net)}`;
      }
    } else {
      totalHtml = formatNaira(totalPayment);
    }

    return {
      headLabel,
      actualFeeLabel: actualFee,
      paymentHeading,
      rows: studentInfo,
      totalHtml: this.sanitizer.bypassSecurityTrustHtml(totalHtml),
    };
  }

  private classCategoryValue(value: number | string | null | undefined): number {
    if (typeof value === 'number') {
      return value;
    }
    const asNum = Number(value);
    if (Number.isFinite(asNum)) {
      return asNum;
    }
    const key = String(value ?? '').trim().toLowerCase();
    const match = Object.entries(CLASS_CATEGORY_LABELS).find(
      ([, label]) => label.toLowerCase() === key,
    );
    return match ? Number(match[0]) : 0;
  }

  private normalizeOptions(raw: unknown): AccountingSelectOption[] {
    if (!Array.isArray(raw)) {
      return [];
    }
    return raw.map((item: Record<string, unknown>) => ({
      text: String(item['text'] ?? item['Text'] ?? ''),
      value: String(item['value'] ?? item['Value'] ?? ''),
      selected: Boolean(item['selected'] ?? item['Selected']),
    }));
  }
}
