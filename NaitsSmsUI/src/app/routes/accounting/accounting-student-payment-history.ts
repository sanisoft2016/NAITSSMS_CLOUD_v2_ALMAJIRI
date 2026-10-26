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
  AccountingPaymentHistoryStudent,
  AccountingStudentPaymentHistoryLoad,
  AccountingStudentPaymentHistoryTerm,
  formatNaira,
} from './accounting.service';
import { writeAccountingOptionId } from '../../theme/sidemenu/accounting-menu.config';

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

const ARM_LABELS: Record<number, string> = {
  0: 'A',
  1: 'B',
  2: 'C',
  3: 'D',
  4: 'E',
  5: 'F',
  6: 'G',
  7: 'H',
  8: 'I',
  9: 'J',
  10: 'K',
  11: 'L',
  12: 'N',
  13: 'O',
  14: 'NoArm',
};

const TERM_LABELS: Record<number, string> = {
  0: 'First',
  1: 'Second',
  2: 'Third',
};

const PAYMENT_TYPE_LABELS: Record<number, string> = {
  0: 'Cash',
  1: 'Teller',
  2: 'Transfer',
};

@Component({
  selector: 'app-accounting-student-payment-history',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule],
  templateUrl: './accounting-student-payment-history.html',
  styleUrl: './accounting-student-payment-history.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingStudentPaymentHistoryPage implements OnInit {
  private readonly api = inject(AccountingApiService);

  readonly formatNaira = formatNaira;

  readonly searchParameter = signal('');
  readonly searching = signal(false);
  readonly hasSearched = signal(false);
  readonly error = signal('');
  readonly students = signal<AccountingPaymentHistoryStudent[]>([]);

  readonly showHistoryModal = signal(false);
  readonly historyLoading = signal(false);
  readonly history = signal<AccountingStudentPaymentHistoryLoad | null>(null);

  readonly orderedStudents = computed(() => {
    const rows = [...this.students()];
    return rows.sort((a, b) => {
      const classCmp = this.classSortValue(a.currentClass) - this.classSortValue(b.currentClass);
      if (classCmp !== 0) {
        return classCmp;
      }
      const armCmp = this.armLabel(a.armName).localeCompare(this.armLabel(b.armName));
      if (armCmp !== 0) {
        return armCmp;
      }
      return this.classCategoryLabel(a.classCategoryStatus).localeCompare(
        this.classCategoryLabel(b.classCategoryStatus),
      );
    });
  });

  ngOnInit(): void {
    writeAccountingOptionId('0');
  }

  searchStudents(): void {
    const parameter = this.searchParameter().trim();
    if (!parameter) {
      window.alert('Search Parameter must be provided to continue.');
      return;
    }

    this.searching.set(true);
    this.error.set('');
    this.hasSearched.set(true);
    this.api.searchStudentsForPaymentHistory(parameter).subscribe({
      next: rows => {
        this.students.set(Array.isArray(rows) ? rows : []);
        this.searching.set(false);
      },
      error: err => {
        this.searching.set(false);
        this.students.set([]);
        this.error.set(err?.error?.message || err?.message || 'Failed to search students.');
      },
    });
  }

  openPaymentHistory(student: AccountingPaymentHistoryStudent): void {
    const studentId = String(student.studentLevelPaymentId ?? '').trim();
    if (!studentId) {
      return;
    }

    this.showHistoryModal.set(true);
    this.historyLoading.set(true);
    this.history.set(null);
    this.api
      .getSelectedStudentPaymentHistory(studentId, student.admisionNo, student.fullName)
      .subscribe({
        next: data => {
          this.history.set({
            admsnNo: data?.admsnNo || student.admisionNo,
            studentName: data?.studentName || student.fullName,
            terms: data?.terms ?? [],
          });
          this.historyLoading.set(false);
        },
        error: err => {
          this.historyLoading.set(false);
          this.error.set(err?.error?.message || err?.message || 'Failed to load payment history.');
          this.showHistoryModal.set(false);
        },
      });
  }

  closeHistoryModal(): void {
    this.showHistoryModal.set(false);
    this.history.set(null);
  }

  classCategoryLabel(value: number | string | null | undefined): string {
    if (typeof value === 'string' && value.trim() !== '' && Number.isNaN(Number(value))) {
      return value;
    }
    const n = Number(value);
    return CLASS_CATEGORY_LABELS[n] ?? String(value ?? '');
  }

  armLabel(value: number | string | null | undefined): string {
    if (typeof value === 'string' && value.trim() !== '' && Number.isNaN(Number(value))) {
      return value;
    }
    const n = Number(value);
    return ARM_LABELS[n] ?? String(value ?? '');
  }

  termLabel(value: number | string | null | undefined): string {
    if (typeof value === 'string' && value.trim() !== '' && Number.isNaN(Number(value))) {
      return value;
    }
    const n = Number(value);
    return TERM_LABELS[n] ?? String(value ?? '');
  }

  paymentTypeLabel(value: number | string | null | undefined): string {
    if (typeof value === 'string' && value.trim() !== '' && Number.isNaN(Number(value))) {
      return value;
    }
    const n = Number(value);
    return PAYMENT_TYPE_LABELS[n] ?? String(value ?? '');
  }

  termTotalPaid(term: AccountingStudentPaymentHistoryTerm): number {
    return (term.studentTermlyFeesHistory ?? []).reduce(
      (acc, row) => acc + Number(row.amountPaid || 0),
      0,
    );
  }

  termOutstanding(term: AccountingStudentPaymentHistoryTerm): number {
    return Number(term.actualFees || 0) - (this.termTotalPaid(term) + Number(term.discount || 0));
  }

  /** Legacy DisplayFor(double) for discount / actual / outstanding / line amounts. */
  formatPlain(amount: number | null | undefined): string {
    const value = Number(amount ?? 0);
    if (!Number.isFinite(value)) {
      return '0';
    }
    return Number.isInteger(value) ? String(value) : String(value);
  }

  private classSortValue(value: number | string | null | undefined): number {
    const n = Number(value);
    return Number.isFinite(n) ? n : 0;
  }
}
