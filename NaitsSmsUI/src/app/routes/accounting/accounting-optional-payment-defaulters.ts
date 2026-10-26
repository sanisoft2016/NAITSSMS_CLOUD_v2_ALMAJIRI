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
  AccountingOptionalPaymentDefaulters,
  AccountingOptionalPaymentProcessing,
  AccountingOptionalPaymentProcessingCharge,
  AccountingSelectOption,
  formatNaira,
} from './accounting.service';
import { writeAccountingOptionId } from '../../theme/sidemenu/accounting-menu.config';

type DefaulterItemView = {
  charge: AccountingOptionalDefaulterCharge;
  counter: number;
  discount: number;
  outstanding: number;
  paid: number;
  paymentDiscount: number;
  reason: string;
};

type DefaulterTermGroup = {
  key: string;
  termLabel: string;
  session: string;
  items: DefaulterItemView[];
};

type DefaulterStudentGroup = {
  studentId: string;
  fullName: string;
  idNo: string;
  totalCharges: number;
  totalPayment: number;
  totalDiscount: number;
  totalOutstanding: number;
  terms: DefaulterTermGroup[];
};

type DefaulterArmGroup = {
  classArmId: string;
  label: string;
  students: DefaulterStudentGroup[];
};

type DefaulterClassGroup = {
  classId: string;
  classLabel: string;
  arms: DefaulterArmGroup[];
};

type ProcessingCategoryGroup = {
  categoryId: string;
  categoryName: string;
  totalCost: number;
  items: AccountingOptionalPaymentProcessingCharge[];
};

type ProcessingStudentGroup = {
  studentId: string;
  fullName: string;
  idNo: string;
  categories: ProcessingCategoryGroup[];
};

type ProcessingArmGroup = {
  classArmId: string;
  label: string;
  students: ProcessingStudentGroup[];
};

type ProcessingClassGroup = {
  classId: string;
  classLabel: string;
  arms: ProcessingArmGroup[];
};

@Component({
  selector: 'app-accounting-optional-payment-defaulters',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule],
  templateUrl: './accounting-optional-payment-defaulters.html',
  styleUrl: './accounting-optional-payment-defaulters.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingOptionalPaymentDefaultersPage implements OnInit {
  private readonly api = inject(AccountingApiService);

  readonly formatNaira = formatNaira;

  readonly loading = signal(true);
  readonly termsLoading = signal(false);
  readonly contentLoading = signal(false);
  readonly error = signal('');

  /**
   * Legacy ResultType on StudentOptionalPaymentIndex:
   * 1 = Make student payment, 0 = List of students payment defaulters
   */
  readonly resultType = signal(1);

  /** Legacy ResultStatus for defaulters: 1 = Termly, 0 = Yearly */
  readonly defaulterSize = signal(1);

  /** Legacy ResultCollectionType: 2 = Per Arm, 1 = Per Class, 0 = All */
  readonly collectionType = signal(2);

  readonly sessionId = signal('');
  readonly termStatus = signal('');
  readonly classValue = signal('');
  readonly armValue = signal('');

  readonly sessions = signal<AccountingSelectOption[]>([]);
  readonly terms = signal<AccountingSelectOption[]>([]);
  readonly classes = signal<AccountingSelectOption[]>([]);
  readonly arms = signal<AccountingSelectOption[]>([]);

  readonly defaultersReport = signal<AccountingOptionalPaymentDefaulters | null>(null);
  readonly processing = signal<AccountingOptionalPaymentProcessing | null>(null);

  readonly isMakePayment = computed(() => this.resultType() === 1);
  readonly isDefaulters = computed(() => this.resultType() === 0);
  readonly showDefaulterTerm = computed(() => this.isDefaulters() && this.defaulterSize() === 1);
  readonly showClassFilter = computed(
    () => this.isMakePayment() && (this.collectionType() === 1 || this.collectionType() === 2),
  );
  readonly showArmFilter = computed(() => this.isMakePayment() && this.collectionType() === 2);

  readonly defaulterClassGroups = computed(() => this.buildDefaulterGroups(this.defaultersReport()));
  readonly processingClassGroups = computed(() => this.buildProcessingGroups(this.processing()));

  ngOnInit(): void {
    writeAccountingOptionId('5');
    this.api.getOptionalPaymentHomeForm().subscribe({
      next: form => {
        this.sessions.set(form.sessions ?? []);
        this.terms.set(form.terms ?? []);
        this.classes.set(form.classes ?? []);
        this.arms.set(form.arms ?? []);

        const firstSession = (form.sessions ?? []).find(s => s.value && s.value !== '-1');
        if (firstSession) {
          this.sessionId.set(firstSession.value);
        }
        const firstTerm = (form.terms ?? []).find(t => t.value && t.value !== '-1');
        if (firstTerm) {
          this.termStatus.set(firstTerm.value);
        }
        const firstClass = (form.classes ?? []).find(c => c.value && c.value !== '-1');
        if (firstClass) {
          this.classValue.set(firstClass.value);
        }
        const firstArm = (form.arms ?? []).find(a => a.value && a.value !== '-1');
        if (firstArm) {
          this.armValue.set(firstArm.value);
        }

        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to load optional payment home form.');
        this.loading.set(false);
      },
    });
  }

  onResultTypeChange(value: string | number): void {
    this.resultType.set(Number(value));
    this.defaultersReport.set(null);
    this.processing.set(null);
    this.error.set('');
    if (this.resultType() === 0) {
      this.maybeLoadDefaulters();
    }
  }

  onDefaulterSizeChange(value: string | number): void {
    this.defaulterSize.set(Number(value));
    this.defaultersReport.set(null);
    this.error.set('');
    this.maybeLoadDefaulters();
  }

  onCollectionTypeChange(value: string | number): void {
    const mode = Number(value);
    this.collectionType.set(mode);
    this.processing.set(null);
    this.error.set('');

    if (mode === 0) {
      if (
        !confirm(
          "Are you sure you want to process for the whole students of the school? Note: this may require a bigger system to a achieve this. To continue click 'Ok' otherwise click 'Cancel' to terminate.",
        )
      ) {
        this.collectionType.set(2);
        return;
      }
      this.loadProcessing('all');
    }
  }

  onSessionChange(value: string): void {
    this.sessionId.set(value);
    this.defaultersReport.set(null);
    this.processing.set(null);
    this.error.set('');

    if (!value || value === '-1') {
      this.terms.set([]);
      this.termStatus.set('');
      return;
    }

    if (this.isDefaulters() && this.defaulterSize() === 0) {
      this.loadYearlyDefaulters(value);
      return;
    }

    this.termsLoading.set(true);
    this.api.getOptionalPaymentDefaultersTerms(value).subscribe({
      next: terms => {
        this.terms.set(terms ?? []);
        const first = (terms ?? []).find(t => t.value && t.value !== '-1');
        this.termStatus.set(first?.value ?? '');
        this.termsLoading.set(false);
        if (this.isDefaulters()) {
          this.maybeLoadDefaulters();
        }
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
    this.defaultersReport.set(null);
    this.processing.set(null);
    this.error.set('');
    if (this.isDefaulters()) {
      this.maybeLoadDefaulters();
    }
  }

  onClassChange(value: string): void {
    this.classValue.set(value);
    this.processing.set(null);
    this.error.set('');
    if (this.collectionType() === 1) {
      this.loadProcessing('class');
    }
  }

  onArmChange(value: string): void {
    this.armValue.set(value);
    this.processing.set(null);
    this.error.set('');
    if (this.collectionType() === 2 && value && value !== '-1') {
      this.loadProcessing('arm');
    }
  }

  private maybeLoadDefaulters(): void {
    const sessionId = this.sessionId();
    if (!sessionId || sessionId === '-1') {
      return;
    }
    if (this.defaulterSize() === 0) {
      this.loadYearlyDefaulters(sessionId);
      return;
    }
    const termStatus = this.termStatus();
    if (!termStatus || termStatus === '-1') {
      return;
    }
    this.loadTermlyDefaulters(sessionId, termStatus);
  }

  private loadYearlyDefaulters(sessionId: string): void {
    this.contentLoading.set(true);
    this.api.getYearlyOptionalPaymentDefaulters(sessionId).subscribe({
      next: data => {
        this.defaultersReport.set(data);
        this.contentLoading.set(false);
      },
      error: () => {
        this.defaultersReport.set(null);
        this.contentLoading.set(false);
        this.error.set('Unable to load yearly payment defaulters.');
      },
    });
  }

  private loadTermlyDefaulters(sessionId: string, termStatus: string): void {
    this.contentLoading.set(true);
    this.api.getTermlyOptionalPaymentDefaulters(sessionId, termStatus).subscribe({
      next: data => {
        this.defaultersReport.set(data);
        this.contentLoading.set(false);
      },
      error: () => {
        this.defaultersReport.set(null);
        this.contentLoading.set(false);
        this.error.set('Unable to load termly payment defaulters.');
      },
    });
  }

  private loadProcessing(mode: 'arm' | 'class' | 'all'): void {
    const sessionId = this.sessionId();
    const termStatus = this.termStatus();
    if (!sessionId || sessionId === '-1' || !termStatus || termStatus === '-1') {
      return;
    }

    const sessionText = this.sessions().find(s => s.value === sessionId)?.text ?? '';
    const classParts = (this.classValue() || '').split(',');
    const currentClass = classParts[0] ?? '';
    const armName = this.armValue();

    if (mode === 'arm' && (!armName || armName === '-1' || !currentClass)) {
      return;
    }
    if (mode === 'class' && !currentClass) {
      return;
    }

    this.contentLoading.set(true);
    this.api
      .getOptionalPaymentProcessing({
        mode,
        sessionId,
        termStatus,
        sessionText,
        currentClass: mode === 'all' ? undefined : currentClass,
        armName: mode === 'arm' ? armName : undefined,
      })
      .subscribe({
        next: data => {
          this.processing.set(data);
          this.contentLoading.set(false);
        },
        error: () => {
          this.processing.set(null);
          this.contentLoading.set(false);
          this.error.set('Unable to load payment processing list.');
        },
      });
  }

  private buildDefaulterGroups(
    data: AccountingOptionalPaymentDefaulters | null,
  ): DefaulterClassGroup[] {
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

    const classes: DefaulterClassGroup[] = [];
    for (const [classId, classCharges] of byClass) {
      const classLabel = classCharges[0]?.classLabel || 'Class';
      const byArm = new Map<string, AccountingOptionalDefaulterCharge[]>();
      for (const c of classCharges) {
        const key = c.classArmId || '_';
        const bucket = byArm.get(key) ?? [];
        bucket.push(c);
        byArm.set(key, bucket);
      }

      const arms: DefaulterArmGroup[] = [];
      for (const [classArmId, armCharges] of byArm) {
        const armName = armCharges[0]?.armName || '';
        const byStudent = new Map<string, AccountingOptionalDefaulterCharge[]>();
        for (const c of armCharges) {
          const key = c.studentId || '_';
          const bucket = byStudent.get(key) ?? [];
          bucket.push(c);
          byStudent.set(key, bucket);
        }

        const students: DefaulterStudentGroup[] = [];
        for (const [studentId, studentCharges] of byStudent) {
          const first = studentCharges[0]!;
          const totalCharges = studentCharges.reduce((s, x) => s + (x.cost || 0), 0);
          const totalPayment = studentCharges.reduce(
            (s, x) => s + (x.payments ?? []).reduce((p, pay) => p + (pay.amount || 0), 0),
            0,
          );
          const studentDiscounts = discounts.filter(d => d.studentId === studentId);
          const totalDiscount = studentDiscounts.reduce((s, d) => s + (d.discountAmount || 0), 0);

          const byTerm = new Map<string, AccountingOptionalDefaulterCharge[]>();
          const ordered = [...studentCharges].sort(
            (a, b) => (a.termStatus || 0) - (b.termStatus || 0),
          );
          for (const c of ordered) {
            const key = c.yearlyAndTermlyStatusId || '_';
            const bucket = byTerm.get(key) ?? [];
            bucket.push(c);
            byTerm.set(key, bucket);
          }

          const terms: DefaulterTermGroup[] = [];
          for (const [termKey, termCharges] of byTerm) {
            const t0 = termCharges[0]!;
            const items: DefaulterItemView[] = termCharges.map((charge, idx) => {
              const paid = (charge.payments ?? []).reduce((s, p) => s + (p.amount || 0), 0);
              const disc =
                discounts.find(d => d.studentTermlyOptionalChargesId === charge.id) ?? null;
              const discount = disc?.discountAmount ?? 0;
              return {
                charge,
                counter: idx + 1,
                paid,
                discount,
                outstanding: (charge.cost || 0) - paid - discount,
                paymentDiscount: discount,
                reason: disc?.reasonsForDiscount ?? '',
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

        arms.push({
          classArmId,
          label: `${classLabel} ${armName}`.trim(),
          students,
        });
      }

      classes.push({ classId, classLabel, arms });
    }

    return classes;
  }

  private buildProcessingGroups(
    data: AccountingOptionalPaymentProcessing | null,
  ): ProcessingClassGroup[] {
    if (!data?.charges?.length) {
      return [];
    }

    const charges = [...data.charges].sort(
      (a, b) =>
        (a.currentClass || 0) - (b.currentClass || 0) ||
        (a.armName || '').localeCompare(b.armName || ''),
    );

    const byClass = new Map<string, AccountingOptionalPaymentProcessingCharge[]>();
    for (const c of charges) {
      const key = c.classId || '_';
      const bucket = byClass.get(key) ?? [];
      bucket.push(c);
      byClass.set(key, bucket);
    }

    const classes: ProcessingClassGroup[] = [];
    for (const [classId, classCharges] of byClass) {
      const classLabel = classCharges[0]?.classLabel || 'Class';
      const byArm = new Map<string, AccountingOptionalPaymentProcessingCharge[]>();
      for (const c of classCharges) {
        const key = c.classArmId || '_';
        const bucket = byArm.get(key) ?? [];
        bucket.push(c);
        byArm.set(key, bucket);
      }

      const arms: ProcessingArmGroup[] = [];
      for (const [classArmId, armCharges] of byArm) {
        const armName = armCharges[0]?.armName || '';
        const byStudent = new Map<string, AccountingOptionalPaymentProcessingCharge[]>();
        for (const c of armCharges) {
          const key = c.studentId || '_';
          const bucket = byStudent.get(key) ?? [];
          bucket.push(c);
          byStudent.set(key, bucket);
        }

        const students: ProcessingStudentGroup[] = [];
        for (const [studentId, studentCharges] of byStudent) {
          const first = studentCharges[0]!;
          const byCat = new Map<string, AccountingOptionalPaymentProcessingCharge[]>();
          for (const c of studentCharges) {
            const key = c.itemCategoryId || '_';
            const bucket = byCat.get(key) ?? [];
            bucket.push(c);
            byCat.set(key, bucket);
          }

          const categories: ProcessingCategoryGroup[] = [];
          for (const [categoryId, items] of byCat) {
            categories.push({
              categoryId,
              categoryName: items[0]?.categoryName || 'Category',
              totalCost: items.reduce((s, x) => s + (x.cost || 0), 0),
              items,
            });
          }

          students.push({
            studentId,
            fullName: first.fullName,
            idNo: first.idNo,
            categories,
          });
        }

        arms.push({
          classArmId,
          label: `${classLabel} ${armName}`.trim(),
          students,
        });
      }

      classes.push({ classId, classLabel, arms });
    }

    return classes;
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
