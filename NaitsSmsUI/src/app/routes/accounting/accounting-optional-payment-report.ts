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
  AccountingOptionalDefaulterCharge,
  AccountingOptionalFeesCategory,
  AccountingOptionalPaymentReport,
  AccountingOptionalRangeCharge,
  AccountingOptionalRangeReport,
  AccountingPaymentHistoryStudent,
  AccountingSelectOption,
  formatNaira,
} from './accounting.service';
import { writeAccountingOptionId } from '../../theme/sidemenu/accounting-menu.config';

type ListItemView = {
  charge: AccountingOptionalDefaulterCharge;
  counter: number;
  paid: number;
  discount: number;
  outstanding: number;
  paymentRows: { paymentDate: string; paymentTypeLabel: string; amount: number; discount: number; reason: string }[];
};

type ListTermGroup = {
  key: string;
  termLabel: string;
  session: string;
  items: ListItemView[];
};

type ListStudentGroup = {
  studentId: string;
  fullName: string;
  idNo: string;
  totalCharges: number;
  totalPayment: number;
  totalDiscount: number;
  totalOutstanding: number;
  terms: ListTermGroup[];
};

type ListArmGroup = {
  classArmId: string;
  label: string;
  students: ListStudentGroup[];
};

type ListClassGroup = {
  classId: string;
  classLabel: string;
  arms: ListArmGroup[];
};

type HistorySessionGroup = {
  sessionId: string;
  session: string;
  terms: ListTermGroup[];
};

type RangeStudentRow = {
  studentId: string;
  fullName: string;
  idNo: string;
  charges: number;
  payment: number;
  discount: number;
  outstanding: number;
};

type RangeArmGroup = {
  classArmId: string;
  label: string;
  students: RangeStudentRow[];
};

type RangeClassGroup = {
  classId: string;
  classLabel: string;
  arms: RangeArmGroup[];
};

type RangeTermGroup = {
  termKey: string;
  termLabel: string;
  classes: RangeClassGroup[];
};

type RangeYearGroup = {
  year: number;
  terms: RangeTermGroup[];
};

@Component({
  selector: 'app-accounting-optional-payment-report',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule],
  templateUrl: './accounting-optional-payment-report.html',
  styleUrl: './accounting-optional-payment-report.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingOptionalPaymentReportPage implements OnInit {
  private readonly api = inject(AccountingApiService);

  readonly formatNaira = formatNaira;

  readonly loading = signal(true);
  readonly termsLoading = signal(false);
  readonly contentLoading = signal(false);
  readonly searching = signal(false);
  readonly error = signal('');

  /** Legacy ResultType: 0 list, 4 individual history, 5 periodic */
  readonly resultType = signal(0);
  /** Legacy ResultStatus for list: 1 Termly, 0 Yearly */
  readonly sizeMode = signal(1);

  readonly sessionId = signal('');
  readonly termStatus = signal('');
  readonly sessions = signal<AccountingSelectOption[]>([]);
  readonly terms = signal<AccountingSelectOption[]>([]);

  readonly searchParameter = signal('');
  readonly searchStudents = signal<AccountingPaymentHistoryStudent[]>([]);
  readonly hasSearched = signal(false);

  readonly startDate = signal('');
  readonly endDate = signal('');
  /** 0 = All Category, 1 = Selected Category */
  readonly categoryMode = signal(0);
  readonly categoryId = signal('-1');
  readonly categories = signal<AccountingOptionalFeesCategory[]>([]);

  readonly listReport = signal<AccountingOptionalPaymentReport | null>(null);
  readonly historyReport = signal<AccountingOptionalPaymentReport | null>(null);
  readonly historyStudentLabel = signal('');
  readonly rangeReport = signal<AccountingOptionalRangeReport | null>(null);

  readonly showTermFilter = computed(() => this.resultType() === 0 && this.sizeMode() === 1);
  readonly showCategoryPicker = computed(() => this.resultType() === 5 && this.categoryMode() === 1);

  readonly listClassGroups = computed(() => this.buildListGroups(this.listReport()));
  readonly historySessions = computed(() => this.buildHistorySessions(this.historyReport()));
  readonly historyTotals = computed(() => {
    const hist = this.historyReport();
    if (!hist?.charges?.length) {
      return { charges: 0, payment: 0, discount: 0, outstanding: 0, fullName: '', idNo: '' };
    }
    const charges = hist.charges.reduce((s, c) => s + (c.cost || 0), 0);
    const payment = hist.totalPayment || 0;
    const discount = hist.totalDiscount || 0;
    return {
      charges,
      payment,
      discount,
      outstanding: charges - (payment + discount),
      fullName: hist.charges[0]?.fullName ?? '',
      idNo: hist.charges[0]?.idNo ?? '',
    };
  });
  readonly rangeYears = computed(() => this.buildRangeYears(this.rangeReport()));

  ngOnInit(): void {
    writeAccountingOptionId('5');
    this.api.getOptionalPaymentDefaultersForm().subscribe({
      next: form => {
        this.sessions.set(form.sessions ?? []);
        this.terms.set(form.terms ?? []);
        const firstSession = (form.sessions ?? []).find(s => s.value && s.value !== '-1');
        if (firstSession) {
          this.sessionId.set(firstSession.value);
        }
        const firstTerm = (form.terms ?? []).find(t => t.value && t.value !== '-1');
        if (firstTerm) {
          this.termStatus.set(firstTerm.value);
        }
        this.loading.set(false);
        this.maybeLoadListReport();
      },
      error: () => {
        this.error.set('Unable to load optional payment report form.');
        this.loading.set(false);
      },
    });

    this.api.getOptionalFeesCategories().subscribe({
      next: cats => this.categories.set(cats ?? []),
      error: () => undefined,
    });
  }

  onResultTypeChange(value: string | number): void {
    this.resultType.set(Number(value));
    this.clearContent();
    this.error.set('');
    if (this.resultType() === 0) {
      this.maybeLoadListReport();
    }
  }

  onSizeChange(value: string | number): void {
    this.sizeMode.set(Number(value));
    this.listReport.set(null);
    this.error.set('');
    this.maybeLoadListReport();
  }

  onSessionChange(value: string): void {
    this.sessionId.set(value);
    this.listReport.set(null);
    this.error.set('');

    if (!value || value === '-1') {
      this.terms.set([]);
      this.termStatus.set('');
      return;
    }

    if (this.sizeMode() === 0) {
      this.loadYearlyList(value);
      return;
    }

    this.termsLoading.set(true);
    this.api.getOptionalPaymentDefaultersTerms(value).subscribe({
      next: terms => {
        this.terms.set(terms ?? []);
        const first = (terms ?? []).find(t => t.value && t.value !== '-1');
        this.termStatus.set(first?.value ?? '');
        this.termsLoading.set(false);
        this.maybeLoadListReport();
      },
      error: () => {
        this.terms.set([]);
        this.termStatus.set('');
        this.termsLoading.set(false);
        this.error.set('Unable to load terms for the selected session.');
      },
    });
  }

  onTermChange(value: string): void {
    this.termStatus.set(value);
    this.listReport.set(null);
    this.error.set('');
    this.maybeLoadListReport();
  }

  runStudentSearch(): void {
    const parameter = this.searchParameter().trim();
    if (!parameter) {
      alert('Search Parameter must be provided to continue.');
      return;
    }
    this.searching.set(true);
    this.hasSearched.set(true);
    this.historyReport.set(null);
    this.error.set('');
    this.api.searchStudentsForPaymentHistory(parameter).subscribe({
      next: students => {
        this.searchStudents.set(students ?? []);
        this.searching.set(false);
      },
      error: () => {
        this.searchStudents.set([]);
        this.searching.set(false);
        this.error.set('Unable to search students.');
      },
    });
  }

  openStudentHistory(student: AccountingPaymentHistoryStudent): void {
    const studentId = student.studentLevelPaymentId || (student as { id?: string }).id || '';
    if (!studentId) {
      return;
    }
    this.contentLoading.set(true);
    this.historyStudentLabel.set(`${student.fullName} (${student.admisionNo})`);
    this.api.getStudentOptionalPaymentHistory(studentId).subscribe({
      next: data => {
        this.historyReport.set(data);
        this.contentLoading.set(false);
      },
      error: () => {
        this.historyReport.set(null);
        this.contentLoading.set(false);
        this.error.set('Unable to load optional payment history for the student.');
      },
    });
  }

  onCategoryModeChange(value: string | number): void {
    this.categoryMode.set(Number(value));
    this.rangeReport.set(null);
    if (this.categoryMode() === 0) {
      this.categoryId.set('-1');
    }
  }

  loadRangeReport(): void {
    const start = this.startDate().trim();
    const end = this.endDate().trim();
    if (!start) {
      alert('Start Date is required.');
      return;
    }
    if (!end) {
      alert('End Date is required.');
      return;
    }
    if (this.categoryMode() === 1 && (!this.categoryId() || this.categoryId() === '-1')) {
      alert('Please select a category.');
      return;
    }

    this.contentLoading.set(true);
    this.rangeReport.set(null);
    this.error.set('');
    this.api
      .getOptionalPaymentRangeReport({
        startDate: start,
        endDate: end,
        categoryId: this.categoryMode() === 1 ? this.categoryId() : undefined,
      })
      .subscribe({
        next: data => {
          this.rangeReport.set(data);
          this.contentLoading.set(false);
        },
        error: () => {
          this.rangeReport.set(null);
          this.contentLoading.set(false);
          this.error.set('Unable to load periodic optional payment report.');
        },
      });
  }

  private clearContent(): void {
    this.listReport.set(null);
    this.historyReport.set(null);
    this.rangeReport.set(null);
    this.searchStudents.set([]);
    this.hasSearched.set(false);
  }

  private maybeLoadListReport(): void {
    if (this.resultType() !== 0) {
      return;
    }
    const sessionId = this.sessionId();
    if (!sessionId || sessionId === '-1') {
      return;
    }
    if (this.sizeMode() === 0) {
      this.loadYearlyList(sessionId);
      return;
    }
    const termStatus = this.termStatus();
    if (!termStatus || termStatus === '-1') {
      return;
    }
    this.loadTermlyList(sessionId, termStatus);
  }

  private loadYearlyList(sessionId: string): void {
    this.contentLoading.set(true);
    this.api.getYearlyOptionalPaymentReport(sessionId).subscribe({
      next: data => {
        this.listReport.set(data);
        this.contentLoading.set(false);
      },
      error: () => {
        this.listReport.set(null);
        this.contentLoading.set(false);
        this.error.set('Unable to load yearly optional payment report.');
      },
    });
  }

  private loadTermlyList(sessionId: string, termStatus: string): void {
    this.contentLoading.set(true);
    this.api.getTermlyOptionalPaymentReport(sessionId, termStatus).subscribe({
      next: data => {
        this.listReport.set(data);
        this.contentLoading.set(false);
      },
      error: () => {
        this.listReport.set(null);
        this.contentLoading.set(false);
        this.error.set('Unable to load termly optional payment report.');
      },
    });
  }

  private uniqueByItem(charges: AccountingOptionalDefaulterCharge[]): AccountingOptionalDefaulterCharge[] {
    const map = new Map<string, AccountingOptionalDefaulterCharge>();
    for (const c of charges) {
      const key = c.optionalFeesItemId || c.id;
      if (!map.has(key)) {
        map.set(key, c);
      }
    }
    return [...map.values()];
  }

  private buildListGroups(data: AccountingOptionalPaymentReport | null): ListClassGroup[] {
    if (!data?.charges?.length) {
      return [];
    }
    const discounts = data.discounts ?? [];
    const charges = [...data.charges].sort((a, b) =>
      (a.classId || '').localeCompare(b.classId || ''),
    );

    const byClass = new Map<string, AccountingOptionalDefaulterCharge[]>();
    for (const c of charges) {
      const key = c.classId || '_';
      const bucket = byClass.get(key) ?? [];
      bucket.push(c);
      byClass.set(key, bucket);
    }

    const classes: ListClassGroup[] = [];
    for (const [classId, classCharges] of byClass) {
      const classLabel = classCharges[0]?.classLabel || 'Class';
      const byArm = new Map<string, AccountingOptionalDefaulterCharge[]>();
      for (const c of classCharges) {
        const key = c.classArmId || '_';
        const bucket = byArm.get(key) ?? [];
        bucket.push(c);
        byArm.set(key, bucket);
      }

      const arms: ListArmGroup[] = [];
      for (const [classArmId, armCharges] of byArm) {
        const armName = armCharges[0]?.armName || '';
        const byStudent = new Map<string, AccountingOptionalDefaulterCharge[]>();
        for (const c of armCharges) {
          const key = c.studentId || '_';
          const bucket = byStudent.get(key) ?? [];
          bucket.push(c);
          byStudent.set(key, bucket);
        }

        const students: ListStudentGroup[] = [];
        for (const [studentId, studentCharges] of byStudent) {
          const first = studentCharges[0]!;
          const unique = this.uniqueByItem(studentCharges);
          const totalCharges = unique.reduce((s, x) => s + (x.cost || 0), 0);
          const totalPayment = unique.reduce(
            (s, x) => s + (x.payments ?? []).reduce((p, pay) => p + (pay.amount || 0), 0),
            0,
          );
          const totalDiscount = discounts
            .filter(d => d.studentId === studentId)
            .reduce((s, d) => s + (d.discountAmount || 0), 0);

          const byTerm = new Map<string, AccountingOptionalDefaulterCharge[]>();
          for (const c of [...studentCharges].sort((a, b) => (a.termStatus || 0) - (b.termStatus || 0))) {
            const key = c.yearlyAndTermlyStatusId || '_';
            const bucket = byTerm.get(key) ?? [];
            bucket.push(c);
            byTerm.set(key, bucket);
          }

          const terms: ListTermGroup[] = [];
          for (const [termKey, termCharges] of byTerm) {
            const t0 = termCharges[0]!;
            const items = this.uniqueByItem(termCharges).map((charge, idx) => {
              const paid = (charge.payments ?? []).reduce((s, p) => s + (p.amount || 0), 0);
              const disc = discounts.find(d => d.studentTermlyOptionalChargesId === charge.id);
              const discount = disc?.discountAmount ?? 0;
              const reason = disc?.reasonsForDiscount ?? '';
              return {
                charge,
                counter: idx + 1,
                paid,
                discount,
                outstanding: (charge.cost || 0) - paid - discount,
                paymentRows: (charge.payments ?? []).map((pay, payIdx) => ({
                  paymentDate: pay.paymentDate,
                  paymentTypeLabel: pay.paymentTypeLabel,
                  amount: pay.amount,
                  discount: payIdx === 0 ? discount : 0,
                  reason: payIdx === 0 ? reason : '',
                })),
              };
            });
            terms.push({
              key: termKey,
              termLabel: t0.termStatusLabel || String(t0.termStatus),
              session: t0.session || '',
              items,
            });
          }

          students.push({
            studentId,
            fullName: first.fullName,
            idNo: first.idNo,
            totalCharges,
            totalPayment,
            totalDiscount,
            totalOutstanding: totalCharges - totalPayment - totalDiscount,
            terms,
          });
        }

        arms.push({ classArmId, label: `${classLabel} ${armName}`.trim(), students });
      }

      classes.push({ classId, classLabel, arms });
    }

    return classes;
  }

  private buildHistorySessions(data: AccountingOptionalPaymentReport | null): HistorySessionGroup[] {
    if (!data?.charges?.length) {
      return [];
    }
    const discounts = data.discounts ?? [];
    const bySession = new Map<string, AccountingOptionalDefaulterCharge[]>();
    for (const c of data.charges) {
      const key = c.session || '_';
      const bucket = bySession.get(key) ?? [];
      bucket.push(c);
      bySession.set(key, bucket);
    }

    const sessions: HistorySessionGroup[] = [];
    for (const [session, sessionCharges] of bySession) {
      const byTerm = new Map<string, AccountingOptionalDefaulterCharge[]>();
      for (const c of [...sessionCharges].sort((a, b) => (a.termStatus || 0) - (b.termStatus || 0))) {
        const key = c.yearlyAndTermlyStatusId || '_';
        const bucket = byTerm.get(key) ?? [];
        bucket.push(c);
        byTerm.set(key, bucket);
      }

      const terms: ListTermGroup[] = [];
      let counter = 0;
      for (const [termKey, termCharges] of byTerm) {
        const t0 = termCharges[0]!;
        const items = [...termCharges]
          .sort((a, b) => (a.optionalFeesItemId || a.id).localeCompare(b.optionalFeesItemId || b.id))
          .map(charge => {
            counter += 1;
            const paid = (charge.payments ?? []).reduce((s, p) => s + (p.amount || 0), 0);
            const disc = discounts.find(d => d.studentTermlyOptionalChargesId === charge.id);
            const discount = disc?.discountAmount ?? 0;
            const reason = disc?.reasonsForDiscount ?? '';
            return {
              charge,
              counter,
              paid,
              discount,
              outstanding: (charge.cost || 0) - paid - discount,
              paymentRows: (charge.payments ?? []).map((pay, payIdx) => ({
                paymentDate: pay.paymentDate,
                paymentTypeLabel: pay.paymentTypeLabel,
                amount: pay.amount,
                discount: payIdx === 0 ? discount : 0,
                reason: payIdx === 0 ? reason : '',
              })),
            };
          });
        terms.push({
          key: termKey,
          termLabel: t0.termStatusLabel || String(t0.termStatus),
          session: t0.session || session,
          items,
        });
      }

      sessions.push({ sessionId: session, session, terms });
    }

    return sessions;
  }

  private buildRangeYears(data: AccountingOptionalRangeReport | null): RangeYearGroup[] {
    if (!data?.charges?.length) {
      return [];
    }
    const discounts = data.discounts ?? [];
    const charges = [...data.charges].sort(
      (a, b) =>
        (a.classId || '').localeCompare(b.classId || '') ||
        (a.classArmId || '').localeCompare(b.classArmId || ''),
    );

    const byYear = new Map<number, AccountingOptionalRangeCharge[]>();
    for (const c of charges) {
      const year = c.assignedYear || new Date(c.assignedDate).getFullYear();
      const bucket = byYear.get(year) ?? [];
      bucket.push(c);
      byYear.set(year, bucket);
    }

    const years: RangeYearGroup[] = [];
    for (const year of [...byYear.keys()].sort((a, b) => a - b)) {
      const yearCharges = byYear.get(year)!;
      const byTerm = new Map<string, AccountingOptionalRangeCharge[]>();
      for (const c of yearCharges) {
        const key = c.yearlyAndTermlyStatusId || '_';
        const bucket = byTerm.get(key) ?? [];
        bucket.push(c);
        byTerm.set(key, bucket);
      }

      const terms: RangeTermGroup[] = [];
      for (const [termKey, termCharges] of byTerm) {
        const termLabel = termCharges[0]?.termStatusLabel || 'Term';
        const byClass = new Map<string, AccountingOptionalRangeCharge[]>();
        for (const c of termCharges) {
          const key = c.classId || '_';
          const bucket = byClass.get(key) ?? [];
          bucket.push(c);
          byClass.set(key, bucket);
        }

        const classes: RangeClassGroup[] = [];
        for (const [classId, classCharges] of byClass) {
          const classLabel = classCharges[0]?.classLabel || 'Class';
          const byArm = new Map<string, AccountingOptionalRangeCharge[]>();
          for (const c of classCharges) {
            const key = c.classArmId || '_';
            const bucket = byArm.get(key) ?? [];
            bucket.push(c);
            byArm.set(key, bucket);
          }

          const arms: RangeArmGroup[] = [];
          for (const [classArmId, armCharges] of byArm) {
            const armName = armCharges[0]?.armName || '';
            const byStudent = new Map<string, AccountingOptionalRangeCharge[]>();
            for (const c of armCharges) {
              const key = c.studentId || '_';
              const bucket = byStudent.get(key) ?? [];
              bucket.push(c);
              byStudent.set(key, bucket);
            }

            const students: RangeStudentRow[] = [];
            for (const [studentId, studentCharges] of byStudent) {
              const first = studentCharges[0]!;
              const chargeSum = studentCharges.reduce((s, x) => s + (x.cost || 0), 0);
              const paymentSum = studentCharges.reduce((s, x) => s + (x.amountPaid || 0), 0);
              const discountSum = discounts
                .filter(
                  d =>
                    d.studentId === studentId &&
                    studentCharges.some(c => c.id === d.studentTermlyOptionalChargesId),
                )
                .reduce((s, d) => s + (d.discountAmount || 0), 0);
              students.push({
                studentId,
                fullName: first.fullName,
                idNo: first.idNo,
                charges: chargeSum,
                payment: paymentSum,
                discount: discountSum,
                outstanding: chargeSum - (paymentSum + discountSum),
              });
            }

            arms.push({
              classArmId,
              label: `${classLabel}, ${armName}`,
              students,
            });
          }

          classes.push({ classId, classLabel, arms });
        }

        terms.push({ termKey, termLabel, classes });
      }

      years.push({ year, terms });
    }

    return years;
  }

  formatPaymentDate(value: string | null | undefined): string {
    if (!value) {
      return '';
    }
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) {
      return value;
    }
    return d.toLocaleDateString('en-GB');
  }
}
