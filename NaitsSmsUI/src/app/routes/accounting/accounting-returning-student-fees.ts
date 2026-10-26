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
import { Router } from '@angular/router';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { provideDateFnsAdapter } from '@angular/material-date-fns-adapter';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { environment } from '@env/environment';

import {
  AccountingApiService,
  AccountingFreshPartPaymentReceipt,
  AccountingFreshPartPaymentRow,
  AccountingReturningStudentFeesStudent,
  AccountingPaymentReceipt,
  AccountingPayFreshierItem,
  FeesPaymentStatus,
  applyFormatAsCurrency,
  formatPaymentDatePreviewFromDate,
  formatReceiptPrintedOnDate,
  toPaymentDatePayload,
} from './accounting.service';
import {
  writeAccountingFlashWarn,
  writeAccountingOptionId,
} from '../../theme/sidemenu/accounting-menu.config';
import { openReceiptPrintWindow } from './accounting-receipt-print';

type RowDraft = {
  selected: boolean;
  paymentId: string;
  paymentDate: Date | null;
};

type PreviewRow = {
  sNo: number;
  admisnNo: string;
  fullName: string;
  classArm: string;
  paymentId: string;
  paymentDate: string;
};

type PartPaymentDraft = {
  paymentAmount: string;
  paymentType: number;
  receiptType: number;
  paymentId: string;
  discount: string;
  reasonForDiscount: string;
  paymentDate: Date | null;
  showReason: boolean;
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
};

@Component({
  selector: 'app-accounting-returning-student-fees',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule, MatDatepickerModule],
  templateUrl: './accounting-returning-student-fees.html',
  styleUrl: './accounting-returning-student-fees.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    provideDateFnsAdapter({
      parse: { dateInput: 'dd/MM/yyyy' },
      display: {
        dateInput: 'dd/MM/yyyy',
        monthYearLabel: 'MMM yyyy',
        dateA11yLabel: 'dd/MM/yyyy',
        monthYearA11yLabel: 'MMMM yyyy',
      },
    }),
  ],
})
export class AccountingReturningStudentFeesPage implements OnInit {
  private readonly api = inject(AccountingApiService);
  private readonly router = inject(Router);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly apiOrigin = environment.serverUrl.replace(/\/api\/?$/, '');

  /** Exposed for template (legacy FormatAsCurrency). */
  readonly applyFormatAsCurrency = applyFormatAsCurrency;

  readonly loading = signal(true);
  readonly paying = signal(false);
  readonly error = signal('');
  readonly infoMessage = signal('');
  readonly students = signal<AccountingReturningStudentFeesStudent[]>([]);
  readonly rowDrafts = signal<Record<string, RowDraft>>({});

  readonly paymentType = signal(0);
  readonly receiptType = signal(0);
  readonly searchTerm = signal('');

  readonly showPreviewModal = signal(false);
  readonly previewRows = signal<PreviewRow[]>([]);
  readonly previewPaymentTypeLabel = signal('');

  readonly showPartModal = signal(false);
  readonly partLoading = signal(false);
  readonly partSaving = signal(false);
  readonly partStudent = signal<AccountingReturningStudentFeesStudent | null>(null);
  readonly partRows = signal<AccountingFreshPartPaymentRow[]>([]);
  /** Per term-row drafts keyed by studentAcademicLevelId (legacy: independent row controls). */
  readonly partDrafts = signal<Record<string, PartPaymentDraft>>({});

  readonly showReceiptModal = signal(false);
  readonly receiptHtml = signal<SafeHtml>('');
  readonly bulkReceipts = signal<AccountingPaymentReceipt[]>([]);
  readonly receiptIndex = signal(0);

  readonly filteredStudents = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const list = this.students();
    if (!term) {
      return list;
    }
    return list.filter(
      s =>
        s.admisionNo.toLowerCase().includes(term) ||
        s.fullName.toLowerCase().includes(term) ||
        `${s.classLabel} ${s.armName}`.toLowerCase().includes(term),
    );
  });

  readonly showReceiptType = computed(() => this.paymentType() === 0);

  readonly currentReceipt = computed(() => this.bulkReceipts()[this.receiptIndex()] ?? null);

  private receiptHtmlRaw = '';

  ngOnInit(): void {
    writeAccountingOptionId('0');
    this.loadStudents();
  }

  loadStudents(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.getReturningStudentFees().subscribe({
      next: load => {
        if (!load.feesConfigured) {
          writeAccountingFlashWarn('Sorry! School Fees needs to be set first.');
          void this.router.navigateByUrl('/accounting/set-school-fees');
          return;
        }
        this.infoMessage.set(load.infoMessage ?? '');
        this.students.set(load.students ?? []);
        this.initRowDrafts(load.students ?? []);
        this.loading.set(false);
      },
      error: err => {
        this.error.set(err?.error?.message || err?.message || 'Failed to load returning students list.');
        this.loading.set(false);
      },
    });
  }

  onPaymentTypeChange(value: number): void {
    this.paymentType.set(value);
  }

  onReceiptTypeChange(value: number): void {
    this.receiptType.set(value);
    if (value === 1) {
      this.rowDrafts.update(drafts => {
        const next = { ...drafts };
        for (const [id, draft] of Object.entries(next)) {
          if (draft.selected) {
            next[id] = { ...draft, paymentId: '' };
          }
        }
        return next;
      });
    }
  }

  searchStudentHint(): void {
    window.alert("Please press 'Ctrl F' and type what you want to search.");
  }

  isPartPayment(student: AccountingReturningStudentFeesStudent): boolean {
    return this.normalizePaymentStatus(student.currentTermPaymentStatus) === 'PartPayment';
  }

  isPayable(student: AccountingReturningStudentFeesStudent): boolean {
    const status = this.normalizePaymentStatus(student.currentTermPaymentStatus);
    return status === 'None';
  }

  isCurrentCompleted(student: AccountingReturningStudentFeesStudent): boolean {
    return this.normalizePaymentStatus(student.currentTermPaymentStatus) === 'Completed';
  }

  otherStatus(student: AccountingReturningStudentFeesStudent): string {
    return this.normalizePaymentStatus(student.otherTermsPaymentStatus);
  }

  classCategoryLabel(value: number | string): string {
    const key = Number(value);
    return CLASS_CATEGORY_LABELS[key] ?? String(value);
  }

  rowDraft(id: string): RowDraft {
    return this.rowDrafts()[id] ?? { selected: false, paymentId: '', paymentDate: null };
  }

  updateRowDraft(id: string, patch: Partial<RowDraft>): void {
    this.rowDrafts.update(drafts => ({
      ...drafts,
      [id]: { ...this.rowDraft(id), ...patch },
    }));
  }

  submitSelectionsPreview(): void {
    const paymentType = this.paymentType();
    const receiptType = this.receiptType();
    const preview: PreviewRow[] = [];
    let focusField: { id: string; field: 'paymentId' | 'paymentDate' } | null = null;
    let validationMessage = '';
    let checked = false;

    for (const student of this.students()) {
      const draft = this.rowDraft(student.studentLevelPaymentId);
      if (!draft.selected || !this.isPayable(student) || this.isPartPayment(student)) {
        continue;
      }

      checked = true;
      let paymentId = draft.paymentId.trim();
      const paymentDate = draft.paymentDate;
      let formattedDate = paymentDate ? formatPaymentDatePreviewFromDate(paymentDate) : '';

      const needsPaymentId =
        paymentId === '' && paymentType !== 0
          ? true
          : paymentId === '' && paymentType === 0 && receiptType === 0;
      const needsPaymentDate =
        !paymentDate && paymentType !== 0
          ? true
          : !paymentDate && paymentType === 0 && receiptType === 0;

      if (needsPaymentId) {
        focusField = { id: student.studentLevelPaymentId, field: 'paymentId' };
        break;
      }
      if (paymentId.length > 8) {
        validationMessage = "The 'Payment Id' should not be more than 8 characters' lenght.";
        focusField = { id: student.studentLevelPaymentId, field: 'paymentId' };
        break;
      }
      if (needsPaymentDate) {
        focusField = { id: student.studentLevelPaymentId, field: 'paymentDate' };
        break;
      }

      if (paymentType === 0 && receiptType === 1) {
        paymentId = 'Auto';
      }
      if (!paymentDate) {
        formattedDate = 'Today';
      }

      preview.push({
        sNo: preview.length + 1,
        admisnNo: student.admisionNo,
        fullName: student.fullName,
        classArm: `${student.classLabel} ${student.armName}`,
        paymentId: paymentId.trim(),
        paymentDate: formattedDate,
      });
    }

    if (preview.length > 0 && checked) {
      this.previewRows.set(preview);
      this.previewPaymentTypeLabel.set(this.paymentTypeLabel(paymentType));
      this.showPreviewModal.set(true);
      return;
    }

    if (!checked) {
      window.alert('One or more student(s) needs to be selected before you proceed');
      return;
    }
    if (validationMessage) {
      window.alert(validationMessage);
      return;
    }
    window.alert('Sorry! One or more required value were not entered.');
    if (focusField) {
      this.focusRowField(focusField.id, focusField.field);
    }
  }

  submitSelections(): void {
    if (
      !window.confirm(
        "Are you sure you want to pay for those students? If 'Yes',  click 'Ok' to continue.",
      )
    ) {
      return;
    }

    const paymentType = this.paymentType();
    const payload: AccountingPayFreshierItem[] = [];

    for (const student of this.students()) {
      const draft = this.rowDraft(student.studentLevelPaymentId);
      if (!draft.selected || !this.isPayable(student) || this.isPartPayment(student)) {
        continue;
      }

      let paymentId = draft.paymentId.trim();
      if (paymentId === '') {
        paymentId = 'Auto';
      }

      payload.push({
        id: student.studentLevelPaymentId,
        classCategoryStatus: Number(student.classCategoryStatus),
        paymentId,
        paymentType,
        paymentDate: toPaymentDatePayload(draft.paymentDate),
      });
    }

    if (payload.length === 0) {
      window.alert('Failed! One or more subject(s) has to be selected to continue.');
      return;
    }

    this.paying.set(true);
    this.showPreviewModal.set(false);
    this.api.paySelectedReturningStudents(payload).subscribe({
      next: result => {
        this.paying.set(false);
        if (typeof result === 'string') {
          window.alert(result);
          return;
        }
        if (Array.isArray(result) && result.length > 0) {
          this.bulkReceipts.set(result);
          this.receiptIndex.set(0);
          this.renderAllBulkReceipts();
          this.showReceiptModal.set(true);
          this.loadStudents();
        }
      },
      error: err => {
        this.paying.set(false);
        window.alert(
          err?.error?.message || err?.message || 'Failed to process returning student payments.',
        );
      },
    });
  }

  openPartPayment(student: AccountingReturningStudentFeesStudent): void {
    this.partStudent.set(student);
    this.partRows.set([]);
    this.partDrafts.set({});
    this.showPartModal.set(true);
    this.partLoading.set(true);

    this.api.getReturningPartPaymentInfo(student.studentLevelPaymentId).subscribe({
      next: rows => {
        const list = rows ?? [];
        this.partRows.set(list);
        this.partDrafts.set(this.initPartDrafts(list));
        this.partLoading.set(false);
        if (!list.length) {
          this.error.set('Sorry! Unknown error occured.');
        }
      },
      error: err => {
        this.partLoading.set(false);
        this.error.set(err?.error?.message || err?.message || 'Failed to load part payment details.');
      },
    });
  }

  partDraftOf(rowId: string): PartPaymentDraft {
    return this.partDrafts()[rowId] ?? this.emptyPartDraft();
  }

  partFieldId(rowId: string, field: string): string {
    return `part-payment-${field}-${rowId}`;
  }

  onPartPaymentTypeChange(rowId: string, value: number): void {
    this.patchPartDraft(rowId, { paymentType: value });
  }

  onPartReceiptTypeChange(rowId: string, value: number): void {
    this.patchPartDraft(rowId, d => ({
      ...d,
      receiptType: value,
      paymentId: value === 1 ? '' : d.paymentId,
    }));
  }

  onPartDiscountKeyup(rowId: string, raw: string): void {
    const formatted = applyFormatAsCurrency(raw);
    this.patchPartDraft(rowId, {
      discount: formatted,
      showReason: formatted.trim() !== '',
    });
  }

  onPartPaymentAmountChange(rowId: string, raw: string): void {
    this.patchPartDraft(rowId, { paymentAmount: applyFormatAsCurrency(raw) });
  }

  onPartPaymentIdChange(rowId: string, value: string): void {
    this.patchPartDraft(rowId, { paymentId: value });
  }

  onPartReasonChange(rowId: string, value: string): void {
    this.patchPartDraft(rowId, { reasonForDiscount: value });
  }

  onPartPaymentDateChange(rowId: string, value: Date | null): void {
    this.patchPartDraft(rowId, { paymentDate: value });
  }

  submitPartPayment(row: AccountingFreshPartPaymentRow): void {
    const student = this.partStudent();
    if (!student) {
      return;
    }

    const rowId = row.studentAcademicLevelId;
    const draft = this.partDraftOf(rowId);
    const feesValue = draft.paymentAmount.trim();
    const outstanding = this.parseCurrency(row.outstanding);

    if (feesValue === '') {
      this.alertAndFocus('You need to provide fees value to continue.', this.partFieldId(rowId, 'amount'));
      return;
    }

    let paymentId = draft.paymentId.trim();
    const needsPaymentId =
      paymentId === '' && draft.paymentType !== 0
        ? true
        : paymentId === '' && draft.paymentType === 0 && draft.receiptType === 0;
    if (needsPaymentId) {
      this.alertAndFocus('Sorry! Payment Id is required.', this.partFieldId(rowId, 'id'));
      return;
    }
    if (paymentId.length > 8) {
      this.alertAndFocus(
        "The 'Payment Id' should not be more than 8 characters' lenght.",
        this.partFieldId(rowId, 'id'),
      );
      return;
    }
    if (draft.paymentType === 0 && draft.receiptType === 1) {
      paymentId = 'Auto';
    }

    const feesNumeric = this.parseCurrency(feesValue);
    if (feesNumeric > outstanding) {
      this.alertAndFocus(
        `Wrong range. The required value should not be more than ${outstanding}.`,
        this.partFieldId(rowId, 'amount'),
      );
      return;
    }

    const discountRaw = draft.discount.replace(/,/g, '').trim();
    const discount = discountRaw === '' ? 0 : Number(discountRaw);
    if (discountRaw !== '' && !draft.reasonForDiscount.trim()) {
      this.alertAndFocus(
        'Sorry! Reasons for the discount is required.',
        this.partFieldId(rowId, 'reason'),
      );
      return;
    }

    const needsDate =
      !draft.paymentDate && draft.paymentType !== 0
        ? true
        : !draft.paymentDate && draft.paymentType === 0 && draft.receiptType === 0;
    if (needsDate) {
      this.alertAndFocus('Sorry! Transaction date is required.', this.partFieldId(rowId, 'date'));
      return;
    }

    if (discount !== 0 && Math.abs(discount + feesNumeric - outstanding) > 0.01) {
      this.alertAndFocus(
        'Sorry! Payment amount plus the discount amount must be equal to the outstanding.',
        this.partFieldId(rowId, 'amount'),
      );
      return;
    }

    const studentName = student.fullName;
    if (
      !window.confirm(
        `Are you sure "${studentName}" has paid "₦${feesValue}"? If 'Yes',  click 'Ok' to continue.`,
      )
    ) {
      return;
    }

    this.partSaving.set(true);
    this.api
      .submitReturningPartPayment({
        studentLevelPaymentId: row.studentAcademicLevelId,
        hisFee: feesNumeric,
        actualFees: outstanding,
        admisnNo: student.admisionNo,
        studentName: student.fullName,
        termAndSession: `${row.termStatus}, ${row.session}`,
        classAndArm: `${student.classLabel} ${student.armName}`,
        paymentId,
        paymentType: draft.paymentType,
        discount,
        reasonForDiscount: draft.reasonForDiscount,
        paymentDate: toPaymentDatePayload(draft.paymentDate),
      })
      .subscribe({
        next: receipt => {
          this.partSaving.set(false);
          if (!receipt?.schoolId) {
            window.alert('Failed! Unable to complete the process.');
            return;
          }
          this.showPartModal.set(false);
          this.renderPartReceipt(receipt, student);
          this.loadStudents();
        },
        error: err => {
          this.partSaving.set(false);
          window.alert(err?.error?.message || err?.message || 'Failed to save part payment.');
        },
      });
  }

  /** Legacy alert then focus — focus runs after the user dismisses OK. */
  private alertAndFocus(message: string, elementId: string): void {
    window.alert(message);
    queueMicrotask(() => {
      const el = document.getElementById(elementId) as HTMLElement | null;
      el?.focus();
      if (el && 'select' in el && typeof (el as HTMLInputElement).select === 'function') {
        try {
          (el as HTMLInputElement).select();
        } catch {
          /* ignore non-selectable controls */
        }
      }
    });
  }

  closePreviewModal(): void {
    this.showPreviewModal.set(false);
  }

  closePartModal(): void {
    this.showPartModal.set(false);
    this.partDrafts.set({});
  }

  private initPartDrafts(rows: AccountingFreshPartPaymentRow[]): Record<string, PartPaymentDraft> {
    const drafts: Record<string, PartPaymentDraft> = {};
    for (const row of rows) {
      drafts[row.studentAcademicLevelId] = this.emptyPartDraft();
    }
    return drafts;
  }

  private patchPartDraft(
    rowId: string,
    patch: Partial<PartPaymentDraft> | ((draft: PartPaymentDraft) => PartPaymentDraft),
  ): void {
    this.partDrafts.update(map => {
      const current = map[rowId] ?? this.emptyPartDraft();
      const next = typeof patch === 'function' ? patch(current) : { ...current, ...patch };
      return { ...map, [rowId]: next };
    });
  }

  closeReceiptModal(): void {
    this.showReceiptModal.set(false);
    this.bulkReceipts.set([]);
  }

  printReceipt(): void {
    openReceiptPrintWindow(this.receiptHtmlRaw, 'Termly Payment Receipt');
  }

  nextReceipt(): void {
    const max = this.bulkReceipts().length - 1;
    if (this.receiptIndex() < max) {
      this.receiptIndex.update(i => i + 1);
      this.renderBulkReceiptAt(this.receiptIndex());
    }
  }

  prevReceipt(): void {
    if (this.receiptIndex() > 0) {
      this.receiptIndex.update(i => i - 1);
      this.renderBulkReceiptAt(this.receiptIndex());
    }
  }

  private initRowDrafts(students: AccountingReturningStudentFeesStudent[]): void {
    const drafts: Record<string, RowDraft> = {};
    for (const s of students) {
      drafts[s.studentLevelPaymentId] = { selected: false, paymentId: '', paymentDate: null };
    }
    this.rowDrafts.set(drafts);
  }

  private emptyPartDraft(): PartPaymentDraft {
    return {
      paymentAmount: '',
      paymentType: 0,
      receiptType: 0,
      paymentId: '',
      discount: '',
      reasonForDiscount: '',
      paymentDate: null,
      showReason: false,
    };
  }

  private paymentTypeLabel(code: number): string {
    if (code === 0) {
      return 'CASH';
    }
    if (code === 1) {
      return 'TELLER';
    }
    return 'TRANSFER';
  }

  private normalizePaymentStatus(status: FeesPaymentStatus | number | string): string {
    if (typeof status === 'string') {
      return status;
    }
    if (status === 1) {
      return 'PartPayment';
    }
    if (status === 2) {
      return 'Completed';
    }
    return 'None';
  }

  private parseCurrency(value: string): number {
    const cleaned = String(value ?? '')
      .replace(/₦/g, '')
      .replace(/,/g, '')
      .trim();
    const num = Number(cleaned);
    return Number.isFinite(num) ? num : 0;
  }

  private focusRowField(id: string, field: 'paymentId' | 'paymentDate'): void {
    const el = document.getElementById(`${field}-${id}`);
    el?.focus();
  }

  private mediaUrl(folder: 'Signatures' | 'SchLogos', fileName?: string | null): string {
    const raw = String(fileName ?? '').trim();
    if (!raw) {
      return '';
    }
    if (raw.startsWith('data:image') || /^https?:\/\//i.test(raw)) {
      return raw;
    }
    if (raw.startsWith('/')) {
      // Legacy Content paths are rewritten by the API to /school-logos or /Signatures.
      return `${this.apiOrigin}${raw}`;
    }
    // Logos live under wwwroot/school-logos (not SchLogos); signatures under Signatures.
    const pathFolder = folder === 'SchLogos' ? 'school-logos' : folder;
    return `${this.apiOrigin}/${pathFolder}/${encodeURIComponent(raw)}`;
  }

  private renderAllBulkReceipts(): void {
    const receipts = this.bulkReceipts();
    if (!receipts.length) {
      return;
    }
    const printedOn = formatReceiptPrintedOnDate();
    // Legacy LoadStudentsForTermlyFees: up to 3 receipts per A4 `.page`, then page-break.
    const pages: string[] = [];
    for (let i = 0; i < receipts.length; i += 3) {
      const chunk = receipts.slice(i, i + 3);
      const cards = chunk.map(r => this.buildFullPaymentReceiptHtml(r, printedOn)).join('');
      pages.push(`<div class="acct-fresh-receipt-print-page">${cards}</div>`);
    }
    this.setReceiptHtml(
      `<div id="pageContent1" class="acct-fresh-receipt-print-batch">${pages.join('')}</div>`,
    );
  }

  private renderBulkReceiptAt(index: number): void {
    const receipt = this.bulkReceipts()[index];
    if (!receipt) {
      return;
    }
    this.setReceiptHtml(this.buildFullPaymentReceiptHtml(receipt));
  }

  private renderPartReceipt(
    receipt: AccountingFreshPartPaymentReceipt,
    student: AccountingReturningStudentFeesStudent,
  ): void {
    const logo = this.mediaUrl('SchLogos', receipt.schoolLogo);
    const signature = this.mediaUrl('Signatures', receipt.accountSignature);
    const cleared =
      receipt.outStandingDebt === receipt.discount
        ? '<span class="acct-fresh__cleared">Cleared</span>'
        : '<span class="acct-fresh__uncleared">Un-clear</span>';

    this.setReceiptHtml(`
      <div class="acct-fresh-receipt-print">
        <table>
          <tr>
            <td>${logo ? `<img src="${logo}" alt="School logo" style="width:6em;height:6em" />` : ''}</td>
            <td style="padding-left:1em">
              <strong>${receipt.schoolName ?? ''}</strong><br/>
              <strong>Address: </strong>${receipt.schoolAddress ?? ''}<br/>
              <strong>Tel: </strong>${receipt.schoolTel ?? ''}
            </td>
          </tr>
        </table>
        <h3 class="acct-fresh-receipt-print__title">
          Termly Payment Receipt (<span class="acct-fresh-receipt-print__pid">${receipt.paymentId ?? ''}</span>)
        </h3>
        <table class="acct-fresh-receipt-print__grid">
          <tr><th>Student's Name</th><td>${receipt.studentName ?? student.fullName}</td><th>Admission No.</th><td>${receipt.admisnNo ?? student.admisionNo}</td></tr>
          <tr><th>Class / Arm</th><td>${receipt.classAndArm ?? ''}</td><th>Term / Session</th><td>${receipt.termAndSession ?? ''}</td></tr>
          <tr><th>Previous Debt</th><td>${receipt.previousDebt ?? ''}</td><th>Amount Paid</th><td>${receipt.amountPaid ?? ''}</td></tr>
          <tr><th>Outstanding Debt</th><td>${receipt.outStandingDebt ?? ''}</td><th>Discount</th><td>${receipt.discount ?? '₦0.00'}</td></tr>
          <tr><th>Payment Status</th><td>${cleared}</td><th>Payment Date</th><td>${receipt.paymentDate ?? ''}</td></tr>
          <tr><th>Signature / Date</th><td colspan="3">${signature ? `<img src="${signature}" alt="Signature" style="width:8em;height:2em" />` : ''}</td></tr>
        </table>
      </div>
    `);
    this.showReceiptModal.set(true);
  }

  private setReceiptHtml(html: string): void {
    this.receiptHtmlRaw = html;
    this.receiptHtml.set(this.sanitizer.bypassSecurityTrustHtml(html));
  }

  /**
   * Legacy LoadStudentsForTermlyFees.js receipt card (Fees Charge / Cleared / printed-on date).
   * Multiple selections are stacked — same as legacy pageContents loop.
   */
  private buildFullPaymentReceiptHtml(
    receipt: AccountingPaymentReceipt,
    printedOn: string = formatReceiptPrintedOnDate(),
  ): string {
    const logo = this.mediaUrl('SchLogos', this.pickReceiptField(receipt, 'schoolLogo', 'SchoolLogo'));
    const signature = this.mediaUrl(
      'Signatures',
      this.pickReceiptField(receipt, 'accountSignature', 'AccountSignature'),
    );
    const paymentId = this.pickReceiptField(receipt, 'paymentId', 'PaymentId');
    const fullName = this.pickReceiptField(receipt, 'fullName', 'FullName');
    const admisionNo = this.pickReceiptField(receipt, 'admisionNo', 'AdmisionNo');
    const classLabel = this.pickReceiptField(receipt, 'classLabel', 'ClassLabel');
    const termAndSession = this.pickReceiptField(receipt, 'termAndSession', 'TermAndSession');
    const fees = this.pickReceiptField(receipt, 'fees', 'Fees');
    const paymentDate = this.pickReceiptField(receipt, 'paymentDate', 'PaymentDate');
    const schoolAddress = this.pickReceiptField(receipt, 'schoolAddress', 'SchoolAddress');

    return `
      <div class="acct-fresh-receipt-print acct-fresh-receipt-print--card">
        <table class="acct-fresh-receipt-print__header">
          <tr>
            <td class="acct-fresh-receipt-print__logo-cell">${logo ? `<img src="${logo}" alt="School logo" />` : ''}</td>
            <td>${schoolAddress}</td>
          </tr>
        </table>
        <h3 class="acct-fresh-receipt-print__title">
          Termly Payment Receipt (<span class="acct-fresh-receipt-print__pid">${paymentId}</span>)
        </h3>
        <table class="acct-fresh-receipt-print__grid">
          <tr><th>Student's Name</th><td>${fullName}</td><th>Admission No.</th><td>${admisionNo}</td></tr>
          <tr><th>Class / Arm</th><td>${classLabel}</td><th>Term / Session</th><td>${termAndSession}</td></tr>
          <tr><th>Fees Charge</th><td style="color:#e74c3c">${fees}</td><th>Payment Date</th><td>${paymentDate}</td></tr>
          <tr>
            <th>Signature / Date</th>
            <td>${signature ? `<img class="acct-fresh-receipt-print__sig" src="${signature}" alt="Signature" />` : ''}</td>
            <td>${printedOn}</td>
            <td><strong>Status: </strong><span class="acct-fresh__cleared">Cleared</span></td>
          </tr>
        </table>
      </div>
    `;
  }

  private pickReceiptField(
    receipt: AccountingPaymentReceipt,
    camel: keyof AccountingPaymentReceipt,
    pascal: string,
  ): string {
    const fromCamel = receipt[camel];
    if (fromCamel != null && String(fromCamel).trim() !== '') {
      return String(fromCamel);
    }
    const fromPascal = (receipt as unknown as Record<string, unknown>)[pascal];
    return fromPascal != null ? String(fromPascal) : '';
  }
}
