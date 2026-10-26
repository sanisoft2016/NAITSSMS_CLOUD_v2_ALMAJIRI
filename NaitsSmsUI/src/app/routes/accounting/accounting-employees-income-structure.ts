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
  AccountingEmployeeIncomeStructure,
  AccountingEmployeeListItem,
  applyFormatAsCurrency,
  formatNaira,
} from './accounting.service';
import { writeAccountingOptionId } from '../../theme/sidemenu/accounting-menu.config';

/** Legacy EmployeeStatus.Teacher = 0 */
const EMPLOYEE_STATUS_TEACHER = 0;
/** Legacy EmployeeIncomeStructureStatus.HasSet = 1 */
const STRUCTURE_HAS_SET = 1;

type ModalMode = 'set' | 'view';

@Component({
  selector: 'app-accounting-employees-income-structure',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule],
  templateUrl: './accounting-employees-income-structure.html',
  styleUrl: './accounting-employees-income-structure.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingEmployeesIncomeStructurePage implements OnInit {
  private readonly api = inject(AccountingApiService);

  readonly loading = signal(true);
  readonly modalLoading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly flashSuccess = signal('');
  readonly flashWarn = signal('');

  readonly employees = signal<AccountingEmployeeListItem[]>([]);

  readonly showModal = signal(false);
  readonly modalMode = signal<ModalMode>('set');
  readonly structureId = signal('');
  readonly employeeId = signal('');
  readonly fullName = signal('');
  readonly qualification = signal(0);
  readonly qualifications = signal<{ text: string; value: string }[]>([]);
  readonly basicSalaryDisplay = signal('');
  readonly allowanceDisplay = signal('');

  readonly totalDisplay = computed(() => {
    const basicRaw = this.basicSalaryDisplay().trim();
    const allowanceRaw = this.allowanceDisplay().trim();
    if (!basicRaw && !allowanceRaw) {
      return '';
    }
    const basic = this.parseAmount(basicRaw);
    const allowance = this.parseAmount(allowanceRaw);
    const total = (Number.isFinite(basic) ? basic : 0) + (Number.isFinite(allowance) ? allowance : 0);
    return this.formatAmount(total);
  });

  readonly modalTitle = computed(() =>
    this.modalMode() === 'set'
      ? 'Employee income structure capturing page.'
      : 'You can make changes to employee income structure.',
  );

  ngOnInit(): void {
    writeAccountingOptionId('1');
    this.loadEmployees();
  }

  isTeacher(row: AccountingEmployeeListItem): boolean {
    return Number(row.employeeStatus) === EMPLOYEE_STATUS_TEACHER;
  }

  hasStructure(row: AccountingEmployeeListItem): boolean {
    return Number(row.employeeIncomeStructureStatus) === STRUCTURE_HAS_SET;
  }

  statusLabel(row: AccountingEmployeeListItem): string {
    return row.employeeStatusLabel || String(row.employeeStatus);
  }

  /** Legacy ToString("C").Substring(1). */
  formatAmount(amount: number | null | undefined): string {
    const formatted = formatNaira(amount);
    return formatted.replace(/^₦\s?/, '').replace(/^£\s?/, '');
  }

  onBasicSalaryInput(raw: string): void {
    this.basicSalaryDisplay.set(applyFormatAsCurrency(raw));
  }

  onAllowanceInput(raw: string): void {
    this.allowanceDisplay.set(applyFormatAsCurrency(raw));
  }

  recalculateTotal(): void {
    // Recompute via computed; nudge displays through formatting.
    if (this.basicSalaryDisplay()) {
      this.basicSalaryDisplay.set(applyFormatAsCurrency(this.basicSalaryDisplay()));
    }
    if (this.allowanceDisplay()) {
      this.allowanceDisplay.set(applyFormatAsCurrency(this.allowanceDisplay()));
    }
  }

  openSetStructure(row: AccountingEmployeeListItem): void {
    this.error.set('');
    this.flashWarn.set('');
    this.modalMode.set('set');
    this.showModal.set(true);
    this.modalLoading.set(true);
    this.resetFormFields(row.id, row.fullName);

    this.api.getSetIncomeStructureForm(row.id).subscribe({
      next: form => {
        this.applyForm(form, 'set');
        this.modalLoading.set(false);
      },
      error: () => {
        this.modalLoading.set(false);
        this.showModal.set(false);
        this.error.set('An error occurred while loading the form.');
      },
    });
  }

  openViewStructure(row: AccountingEmployeeListItem): void {
    this.error.set('');
    this.flashWarn.set('');
    this.modalMode.set('view');
    this.showModal.set(true);
    this.modalLoading.set(true);
    this.resetFormFields(row.id, row.fullName);

    this.api.getEmployeeIncomeStructure(row.id).subscribe({
      next: form => {
        this.applyForm(form, 'view');
        this.modalLoading.set(false);
      },
      error: () => {
        this.modalLoading.set(false);
        this.showModal.set(false);
        this.error.set('An error occurred while loading the income structure.');
      },
    });
  }

  closeModal(): void {
    this.showModal.set(false);
    this.saving.set(false);
    this.modalLoading.set(false);
  }

  submitStructure(): void {
    // Legacy SubmitEmployeeForm required-field checks
    if (!this.basicSalaryDisplay().trim()) {
      window.alert('Sorry! One or more required value(s) need to be provided.');
      return;
    }
    if (!this.allowanceDisplay().trim()) {
      window.alert('Sorry! One or more required value(s) need to be provided.');
      return;
    }

    const basicSalary = this.parseAmount(this.basicSalaryDisplay());
    const allowance = this.parseAmount(this.allowanceDisplay());
    if (!Number.isFinite(basicSalary) || !Number.isFinite(allowance)) {
      window.alert('Sorry! One or more required value(s) need to be provided.');
      return;
    }

    this.saving.set(true);
    this.api
      .saveEmployeeIncomeStructure({
        id: this.structureId() || this.employeeId(),
        employeeId: this.employeeId(),
        employeeQualificationStatus: Number(this.qualification()),
        basicSalary,
        allowance,
      })
      .subscribe({
        next: result => {
          this.saving.set(false);
          if (result?.status === 1) {
            window.alert(result.message || 'The changes has been succcessfully saved.');
            this.closeModal();
            this.flashSuccess.set(result.message || 'The changes has been succcessfully saved.');
            this.loadEmployees();
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

  deleteStructure(row: AccountingEmployeeListItem): void {
    const confirmed = window.confirm(
      `Are you sure you want to DELETE this User "${row.fullName}"? \n If 'Yes',  click 'Ok' to continue.`,
    );
    if (!confirmed) {
      return;
    }

    this.error.set('');
    this.flashSuccess.set('');
    this.flashWarn.set('');
    this.api.deleteEmployeeIncomeStructure(row.id).subscribe({
      next: result => {
        if (result?.status === 1) {
          window.alert(result.message || 'Payment structure deleted successfully.');
          this.flashSuccess.set(result.message || 'Payment structure deleted successfully.');
          this.loadEmployees();
        } else {
          window.alert(
            result?.message ||
              'Failed! Payment(s) has been made upon this employee and therefore we cannot delete his payment structure.',
          );
        }
      },
      error: () => {
        window.alert(
          'Failed! Payment(s) has been made upon this employee and therefore we cannot delete his payment structure.',
        );
      },
    });
  }

  private loadEmployees(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.getEmployeesIncomeStructure().subscribe({
      next: rows => {
        this.employees.set(rows ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.employees.set([]);
        this.loading.set(false);
        this.error.set('An error occurred while loading employees.');
      },
    });
  }

  private resetFormFields(employeeId: string, fullName: string): void {
    this.structureId.set('');
    this.employeeId.set(employeeId);
    this.fullName.set(fullName);
    this.qualification.set(0);
    this.qualifications.set([]);
    this.basicSalaryDisplay.set('');
    this.allowanceDisplay.set('');
  }

  private applyForm(form: AccountingEmployeeIncomeStructure, mode: ModalMode): void {
    this.structureId.set(form.id ?? '');
    this.employeeId.set(form.employeeId || this.employeeId());
    this.fullName.set(form.fullName || this.fullName());
    this.qualification.set(Number(form.employeeQualificationStatus) || 0);
    this.qualifications.set(
      (form.qualifications ?? []).map(q => ({
        text: q.text,
        value: String(q.value),
      })),
    );

    if (mode === 'view') {
      this.basicSalaryDisplay.set(this.formatAmount(form.basicSalary));
      this.allowanceDisplay.set(this.formatAmount(form.allowance));
    } else {
      this.basicSalaryDisplay.set('');
      this.allowanceDisplay.set('');
    }
  }

  private parseAmount(raw: string): number {
    const cleaned = String(raw ?? '')
      .replace(/,/g, '')
      .trim();
    if (!cleaned) {
      return 0;
    }
    return parseFloat(cleaned);
  }
}
