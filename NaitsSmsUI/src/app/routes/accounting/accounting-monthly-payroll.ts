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
  AccountingMonthlyPaymentRow,
  AccountingMonthlyPaymentSave,
  AccountingPayrollStructureRow,
  applyFormatAsCurrency,
  formatNaira,
} from './accounting.service';
import { writeAccountingOptionId } from '../../theme/sidemenu/accounting-menu.config';

const MONTH_ABBR = [
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

type SlipMode = 'structure' | 'payments';
type ViewMode = 'form' | 'slip' | 'print';

type PayrollDraftRow = {
  id: string;
  employeeId: string;
  fullName: string;
  employeeIdNo: string;
  basicSalary: number;
  allowance: number;
  bonusDisplay: string;
  deductionDisplay: string;
  status: number;
  paymentType: number;
  comments: string;
  monthId: string;
  levelId: string;
};

type PrintCard = {
  fullName: string;
  employeeIdNo: string;
  basicSalary: number;
  allowance: number;
  bonus: number;
  deduction: number;
  grossPay: string;
  netPay: string;
};

const SLIPS_PER_A4_PAGE = 5;

@Component({
  selector: 'app-accounting-monthly-payroll',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule, MatDatepickerModule],
  templateUrl: './accounting-monthly-payroll.html',
  styleUrl: './accounting-monthly-payroll.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingMonthlyPayrollPage implements OnInit {
  private readonly api = inject(AccountingApiService);

  readonly loading = signal(false);
  readonly generating = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly flashSuccess = signal('');

  readonly paymentDate = signal<Date | null>(null);
  readonly viewMode = signal<ViewMode>('form');
  readonly slipMode = signal<SlipMode>('structure');
  readonly levelId = signal('');
  readonly monthId = signal('');
  readonly levelStatus = signal(0);
  readonly slipYear = signal(0);
  readonly slipMonth = signal(0);
  readonly rows = signal<PayrollDraftRow[]>([]);
  readonly printCards = signal<PrintCard[]>([]);

  /** Chunk print slips into A4 pages of 7. */
  readonly printPages = computed(() => {
    const cards = this.printCards();
    const pages: PrintCard[][] = [];
    for (let i = 0; i < cards.length; i += SLIPS_PER_A4_PAGE) {
      pages.push(cards.slice(i, i + SLIPS_PER_A4_PAGE));
    }
    return pages;
  });

  readonly showCommentModal = signal(false);
  readonly commentPaymentId = signal('');
  readonly commentEmployeeName = signal('');
  readonly commentText = signal('');

  readonly showUpdateModal = signal(false);
  readonly updateId = signal('');
  readonly updateEmployeeId = signal('');
  readonly updateLevelId = signal('');
  readonly updateFullName = signal('');
  readonly updateBasic = signal(0);
  readonly updateAllowance = signal(0);
  readonly updateBonusDisplay = signal('');
  readonly updateDeductionDisplay = signal('');
  readonly updatePaymentType = signal(0);

  readonly isConfirmed = computed(
    () => this.levelStatus() === 1 && this.slipMode() === 'payments',
  );

  readonly paymentBanner = computed(() => {
    const month = this.slipMonth();
    const year = this.slipYear();
    if (!month || !year) {
      return '';
    }
    return `Payment for the month of ${MONTH_ABBR[month] || month}, ${year}`;
  });

  readonly summary = computed(() => this.buildSummary(this.rows()));

  readonly overallTotalDisplay = computed(() => this.formatAmount(this.summary().paidNowAmount));

  ngOnInit(): void {
    writeAccountingOptionId('1');
  }

  formatAmount(amount: number | null | undefined): string {
    return formatNaira(amount).replace(/^₦\s?/, '').replace(/^£\s?/, '');
  }

  rowNet(row: PayrollDraftRow): number {
    return (
      row.basicSalary +
      row.allowance +
      this.parseAmount(row.bonusDisplay) -
      this.parseAmount(row.deductionDisplay)
    );
  }

  onBonusInput(index: number, raw: string): void {
    this.patchRow(index, { bonusDisplay: applyFormatAsCurrency(raw) });
  }

  onDeductionInput(index: number, raw: string): void {
    this.patchRow(index, { deductionDisplay: applyFormatAsCurrency(raw) });
  }

  applyCurrency(raw: string): string {
    return applyFormatAsCurrency(raw);
  }

  onStatusChange(index: number, value: string | number): void {
    this.patchRow(index, { status: Number(value) });
  }

  onPaymentTypeChange(index: number, value: string | number): void {
    this.patchRow(index, { paymentType: Number(value) });
  }

  previewSlip(): void {
    this.error.set('');
    this.flashSuccess.set('');
    const date = this.paymentDate();
    if (!date) {
      window.alert('Sorry! Year and month is required to continue.');
      return;
    }

    this.loading.set(true);
    const iso = this.toIsoDate(date);
    this.api.getMonthlyPaymentSlip(iso).subscribe({
      next: slip => {
        this.applySlip(slip);
        this.viewMode.set('slip');
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('An error occurred while loading the payment slip.');
      },
    });
  }

  closeSlip(): void {
    this.viewMode.set('form');
    this.rows.set([]);
    this.printCards.set([]);
  }

  generatePaymentSlip(): void {
    const payload = this.buildSavePayload();
    if (payload === null) {
      return;
    }

    this.generating.set(true);
    this.api.createMonthlyPayment(payload).subscribe({
      next: result => {
        this.generating.set(false);
        if (result?.status !== 1) {
          window.alert(result?.message || 'failed.');
          return;
        }

        const cards: PrintCard[] = this.rows().map(row => {
          const bonus = this.parseAmount(row.bonusDisplay);
          const deduction = this.parseAmount(row.deductionDisplay);
          const gross = row.basicSalary + row.allowance + bonus;
          const net = gross - deduction;
          return {
            fullName: row.fullName,
            employeeIdNo: row.employeeIdNo,
            basicSalary: row.basicSalary,
            allowance: row.allowance,
            bonus,
            deduction,
            grossPay: this.formatAmount(gross),
            netPay: this.formatAmount(net),
          };
        });
        this.printCards.set(cards);
        this.viewMode.set('print');
      },
      error: () => {
        this.generating.set(false);
        window.alert('failed.');
      },
    });
  }

  confirmPayment(): void {
    const levelId = this.levelId();
    if (!levelId) {
      return;
    }
    if (
      !window.confirm(
        "Are you sure you want to confirm the payment? If 'Yes' click 'Ok' to continue.",
      )
    ) {
      return;
    }

    this.api.confirmMonthlyPayment(levelId).subscribe({
      next: result => {
        if (result?.status === 0) {
          window.alert(result.message || 'Confirmation Successful.');
          this.closeSlip();
        } else {
          window.alert(result?.message || 'Failed! Subject Name exist against another record.');
        }
      },
      error: () => {
        window.alert('Failed! Subject Name exist against another record.');
      },
    });
  }

  deletePreview(): void {
    const monthId = this.monthId();
    if (!monthId) {
      return;
    }
    if (
      !window.confirm(
        "Are your sure you want to delete the last monthly payment you have generated? If 'Yes' click OK to continue.",
      )
    ) {
      return;
    }

    this.api.deleteMonthlyPaymentPreview(monthId).subscribe({
      next: result => {
        window.alert(result?.message || 'Recently generated preview has successfully been deleted.');
        this.closeSlip();
      },
      error: () => {
        window.alert('Failed! Subject Name exist against another record.');
      },
    });
  }

  printPage(): void {
    const node = document.getElementById('pageContent1');
    if (!node) {
      return;
    }
    const win = window.open('', '_blank');
    if (!win) {
      return;
    }
    win.document.write(`<!doctype html><html><head><title>Payment Slip</title>
<style>
  @page { size: A4; margin: 10mm; }
  * { box-sizing: border-box; }
  html, body {
    margin: 0;
    padding: 0;
    font-family: Segoe UI, Arial, sans-serif;
    color: #222;
    -webkit-print-color-adjust: exact;
    print-color-adjust: exact;
  }
  .acct-pay-print__page {
    width: 100%;
    height: 277mm;
    min-height: 277mm;
    max-height: 277mm;
    display: flex;
    flex-direction: column;
    justify-content: space-evenly;
    page-break-after: always;
    break-after: page;
    overflow: hidden;
  }
  .acct-pay-print__page:last-child {
    page-break-after: auto;
    break-after: auto;
  }
  .acct-pay-print__card {
    border: 1px solid #333;
    padding: 0.55em 0.75em;
    margin: 0;
    /* Gap is handled by space-evenly; keep a floor of 3× prior 3mm gap */
    flex: 0 0 auto;
  }
  .acct-pay-print__grid {
    width: 100%;
    border-collapse: collapse;
    table-layout: fixed;
  }
  .acct-pay-print__col {
    width: 50%;
    vertical-align: middle;
    padding: 0.2rem 0.65rem;
  }
  .acct-pay-print__col--left {
    border-right: 1px solid #333;
  }
  .acct-pay-print__inner {
    width: 100%;
    border-collapse: collapse;
  }
  .acct-pay-print__inner th {
    text-align: left;
    padding: 0.18rem 0.3rem;
    width: 7.5rem;
    font-size: 11pt;
    font-weight: 700;
  }
  .acct-pay-print__inner td {
    padding: 0.18rem 0.3rem;
    font-size: 11pt;
  }
  .gross { color: #c00; }
  .net { color: #06c; }
</style></head><body>${node.innerHTML}</body></html>`);
    win.document.close();
    win.focus();
    win.print();
    win.close();
  }

  openComments(row: PayrollDraftRow): void {
    this.commentPaymentId.set(row.id);
    this.commentEmployeeName.set(row.fullName);
    this.commentText.set('');
    this.showCommentModal.set(true);
    this.api.getMonthlyPaymentComment(row.id).subscribe({
      next: res => this.commentText.set(res?.comment ?? ''),
      error: () => this.commentText.set(''),
    });
  }

  closeCommentModal(): void {
    this.showCommentModal.set(false);
    this.previewSlip();
  }

  saveComment(): void {
    const text = this.commentText().trim();
    if (!text) {
      window.alert('Comment is required before you continue.');
      return;
    }
    if (text.length > 170) {
      window.alert('Comment is out of range. The maximum is 170');
      return;
    }

    this.saving.set(true);
    this.api.saveMonthlyPaymentComment(this.commentPaymentId(), text).subscribe({
      next: result => {
        this.saving.set(false);
        if (result?.status === 0) {
          window.alert(result.message || 'Comment has been submited successfully.');
          this.closeCommentModal();
        } else {
          window.alert(result?.message || 'Failed! Subject Name exist against another record.');
        }
      },
      error: () => {
        this.saving.set(false);
        window.alert('Failed! Subject Name exist against another record.');
      },
    });
  }

  openUpdatePayment(row: PayrollDraftRow): void {
    this.updateId.set(row.id);
    this.updateEmployeeId.set(row.employeeId);
    this.updateLevelId.set(row.levelId);
    this.updateFullName.set(row.fullName);
    this.updateBasic.set(row.basicSalary);
    this.updateAllowance.set(row.allowance);
    this.updateBonusDisplay.set(row.bonusDisplay || '0.00');
    this.updateDeductionDisplay.set(row.deductionDisplay || '0.00');
    this.updatePaymentType.set(row.paymentType);
    this.showUpdateModal.set(true);
  }

  closeUpdateModal(): void {
    this.showUpdateModal.set(false);
    this.previewSlip();
  }

  submitUpdatePayment(): void {
    if (!this.updateBonusDisplay().trim()) {
      window.alert('Bonus required at least put zero (0)');
      return;
    }
    if (!this.updateDeductionDisplay().trim()) {
      window.alert('Deduction required at least put zero (0)');
      return;
    }

    const model: AccountingMonthlyPaymentSave = {
      id: this.updateId(),
      employeeId: this.updateEmployeeId(),
      employeeMonthlyPaymentLevelId: this.updateLevelId(),
      basicSalary: this.updateBasic(),
      allowance: this.updateAllowance(),
      bonus: this.parseAmount(this.updateBonusDisplay()),
      deduction: this.parseAmount(this.updateDeductionDisplay()),
      employeeMonthlyPaymentStatus: 0,
      paymentType: this.updatePaymentType(),
    };

    this.saving.set(true);
    this.api.submitEmployeeMonthlyPayment(model).subscribe({
      next: result => {
        this.saving.set(false);
        if (result?.status === 0) {
          window.alert(result.message || 'The changes has been succcessfully saved.');
          this.closeUpdateModal();
        } else {
          window.alert(result?.message || 'Failed! Subject Name exist against another record.');
        }
      },
      error: () => {
        this.saving.set(false);
        window.alert('Failed! Subject Name exist against another record.');
      },
    });
  }

  private applySlip(slip: {
    unConfirmedMonthlyPaymentId?: string | null;
    paymentDate: string;
    mode: string;
    structures?: AccountingPayrollStructureRow[] | null;
    payments?: AccountingMonthlyPaymentRow[] | null;
  }): void {
    const mode: SlipMode = slip.mode === 'payments' ? 'payments' : 'structure';
    this.slipMode.set(mode);
    this.levelId.set(slip.unConfirmedMonthlyPaymentId || '');

    const date = this.paymentDate() ?? new Date(slip.paymentDate);
    if (mode === 'structure') {
      const structures = slip.structures ?? [];
      this.slipYear.set(date.getFullYear());
      this.slipMonth.set(date.getMonth() + 1);
      this.levelStatus.set(0);
      this.monthId.set('');
      this.rows.set(
        structures.map(s => ({
          id: '',
          employeeId: s.employeeId,
          fullName: s.fullName,
          employeeIdNo: s.employeeIdNo,
          basicSalary: Number(s.basicSalary) || 0,
          allowance: Number(s.allowance) || 0,
          bonusDisplay: '',
          deductionDisplay: '',
          status: 0,
          paymentType: 0,
          comments: '',
          monthId: '',
          levelId: slip.unConfirmedMonthlyPaymentId || '',
        })),
      );
      return;
    }

    const payments = slip.payments ?? [];
    const first = payments[0];
    this.slipYear.set(first?.year || date.getFullYear());
    this.slipMonth.set(first?.month || date.getMonth() + 1);
    this.levelStatus.set(Number(first?.levelStatus) || 0);
    this.monthId.set(first?.monthId || '');
    this.levelId.set(first?.employeeMonthlyPaymentLevelId || slip.unConfirmedMonthlyPaymentId || '');
    this.rows.set(
      payments.map(p => ({
        id: p.id,
        employeeId: p.employeeId,
        fullName: p.fullName,
        employeeIdNo: p.employeeIdNo,
        basicSalary: Number(p.basicSalary) || 0,
        allowance: Number(p.allowance) || 0,
        bonusDisplay: this.formatAmount(p.bonus),
        deductionDisplay: this.formatAmount(p.deduction),
        status: Number(p.employeeMonthlyPaymentStatus) || 0,
        paymentType: Number(p.paymentType) || 0,
        comments: p.comments || '',
        monthId: p.monthId,
        levelId: p.employeeMonthlyPaymentLevelId,
      })),
    );
  }

  private buildSavePayload(): AccountingMonthlyPaymentSave[] | null {
    return this.rows().map(row => ({
      id: row.id || undefined,
      employeeId: row.employeeId,
      basicSalary: row.basicSalary,
      allowance: row.allowance,
      bonus: this.parseAmount(row.bonusDisplay),
      deduction: this.parseAmount(row.deductionDisplay),
      employeeMonthlyPaymentStatus: row.status,
      paymentType: row.paymentType,
    }));
  }

  private buildSummary(rows: PayrollDraftRow[]) {
    let paidNowCount = 0;
    let paidNowAmount = 0;
    let laterCount = 0;
    let laterAmount = 0;
    let skipCount = 0;
    let skipAmount = 0;
    let cashCount = 0;
    let cashAmount = 0;
    let tellerCount = 0;
    let tellerAmount = 0;
    let transferCount = 0;
    let transferAmount = 0;

    for (const row of rows) {
      const net = this.rowNet(row);
      if (row.status === 0) {
        paidNowCount += 1;
        paidNowAmount += net;
        if (row.paymentType === 0) {
          cashCount += 1;
          cashAmount += net;
        } else if (row.paymentType === 1) {
          tellerCount += 1;
          tellerAmount += net;
        } else {
          transferCount += 1;
          transferAmount += net;
        }
      } else if (row.status === 1) {
        laterCount += 1;
        laterAmount += net;
      } else {
        skipCount += 1;
        skipAmount += net;
      }
    }

    return {
      paidNowCount,
      paidNowAmount,
      laterCount,
      laterAmount,
      skipCount,
      skipAmount,
      cashCount,
      cashAmount,
      tellerCount,
      tellerAmount,
      transferCount,
      transferAmount,
    };
  }

  private patchRow(index: number, patch: Partial<PayrollDraftRow>): void {
    const next = [...this.rows()];
    const current = next[index];
    if (!current) {
      return;
    }
    next[index] = { ...current, ...patch };
    this.rows.set(next);
  }

  private parseAmount(raw: string): number {
    const cleaned = String(raw ?? '')
      .replace(/,/g, '')
      .trim();
    if (!cleaned) {
      return 0;
    }
    const n = parseFloat(cleaned);
    return Number.isFinite(n) ? n : 0;
  }

  private toIsoDate(date: Date): string {
    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, '0');
    const d = String(date.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}T00:00:00`;
  }
}
