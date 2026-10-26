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
  AccountingOptionalAssignmentStudent,
  AccountingOptionalFeesItem,
  AccountingSelectOption,
} from './accounting.service';
import { writeAccountingOptionId } from '../../theme/sidemenu/accounting-menu.config';

@Component({
  selector: 'app-accounting-optional-charges-assignment',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule],
  templateUrl: './accounting-optional-charges-assignment.html',
  styleUrl: './accounting-optional-charges-assignment.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingOptionalChargesAssignmentPage implements OnInit {
  private readonly api = inject(AccountingApiService);

  readonly loading = signal(true);
  readonly loadingArms = signal(false);
  readonly loadingStudents = signal(false);
  readonly submitting = signal(false);
  readonly error = signal('');
  readonly studentMessage = signal('');

  readonly categories = signal<AccountingSelectOption[]>([]);
  readonly classes = signal<AccountingSelectOption[]>([]);
  readonly arms = signal<AccountingSelectOption[]>([]);

  readonly categoryId = signal('');
  readonly classId = signal('');
  readonly armId = signal('');
  readonly catStatus = signal(1);

  readonly students = signal<AccountingOptionalAssignmentStudent[]>([]);
  readonly selectedIds = signal<Record<string, boolean>>({});
  readonly selectAll = signal(false);

  readonly showItemsModal = signal(false);
  readonly itemsLoading = signal(false);
  readonly categoryItems = signal<AccountingOptionalFeesItem[]>([]);

  readonly categoryLabel = computed(() => {
    const id = this.categoryId();
    return this.categories().find(c => c.value === id)?.text ?? '';
  });

  readonly classLabel = computed(() => {
    const id = this.classId();
    return this.classes().find(c => c.value === id)?.text ?? '';
  });

  readonly armLabel = computed(() => {
    const id = this.armId();
    return this.arms().find(a => a.value === id)?.text ?? '';
  });

  ngOnInit(): void {
    writeAccountingOptionId('5');
    this.loadForm();
  }

  private loadForm(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.getOptionalChargesAssignmentForm().subscribe({
      next: form => {
        const cats = Array.isArray(form?.categories) ? form.categories : [];
        const classes = Array.isArray(form?.classes) ? form.classes : [];
        const arms = Array.isArray(form?.arms) ? form.arms : [];
        this.categories.set(cats);
        this.classes.set(classes);
        this.arms.set(arms);
        this.categoryId.set(cats[0]?.value ?? '');
        this.classId.set(classes[0]?.value ?? '');
        this.armId.set(arms[0]?.value ?? '');
        this.loading.set(false);
        // Legacy: students load only after arm change, not on initial page load.
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err?.error?.message || err?.message || 'Failed to load assignment form.');
      },
    });
  }

  onCategoryChange(value: string): void {
    this.categoryId.set(value);
    // Legacy: category change clears student list only
    this.students.set([]);
    this.selectedIds.set({});
    this.studentMessage.set('');
  }

  onClassChange(value: string): void {
    this.classId.set(value);
    this.students.set([]);
    this.selectedIds.set({});
    this.studentMessage.set('');
    this.loadingArms.set(true);
    this.api.getOptionalChargesAssignmentArms(value).subscribe({
      next: arms => {
        const list = Array.isArray(arms) ? arms : [];
        this.arms.set(list);
        this.armId.set(list[0]?.value ?? '');
        this.loadingArms.set(false);
      },
      error: err => {
        this.loadingArms.set(false);
        this.arms.set([]);
        this.armId.set('');
        this.error.set(err?.error?.message || err?.message || 'Failed to load arms.');
      },
    });
  }

  onArmChange(value: string): void {
    this.armId.set(value);
    this.catStatus.set(1);
    this.loadStudents(1);
  }

  onStudentCategoryChange(value: number | string): void {
    const status = Number(value) || 1;
    this.catStatus.set(status);
    this.loadStudents(status);
  }

  private loadStudents(catStatus: number): void {
    const armId = this.armId();
    const categoryId = this.categoryId();
    if (!armId || armId === '0') {
      this.students.set([]);
      this.selectedIds.set({});
      this.studentMessage.set('');
      return;
    }

    this.loadingStudents.set(true);
    this.studentMessage.set('');
    this.api
      .getOptionalChargesAssignmentStudents({ armId, categoryId, catStatus })
      .subscribe({
        next: result => {
          const rows = Array.isArray(result?.students) ? result.students : [];
          this.students.set(rows);
          const map: Record<string, boolean> = {};
          for (const row of rows) {
            map[row.id] = !!row.assigned;
          }
          this.selectedIds.set(map);
          this.selectAll.set(rows.length > 0 && rows.every(r => map[r.id]));
          this.studentMessage.set(result?.message || '');
          this.loadingStudents.set(false);
        },
        error: err => {
          this.loadingStudents.set(false);
          this.students.set([]);
          this.selectedIds.set({});
          this.studentMessage.set(
            err?.error?.message || err?.message || 'Failed to load students.',
          );
        },
      });
  }

  toggleSelectAll(checked: boolean): void {
    this.selectAll.set(checked);
    const map: Record<string, boolean> = { ...this.selectedIds() };
    for (const row of this.students()) {
      map[row.id] = checked;
    }
    this.selectedIds.set(map);
  }

  toggleStudent(id: string, checked: boolean): void {
    const map = { ...this.selectedIds(), [id]: checked };
    this.selectedIds.set(map);
    const rows = this.students();
    this.selectAll.set(rows.length > 0 && rows.every(r => map[r.id]));
  }

  isSelected(id: string): boolean {
    return !!this.selectedIds()[id];
  }

  submitSelections(): void {
    const armId = this.armId();
    const categoryId = this.categoryId();
    const categoryName = this.categoryLabel();
    const className = this.classLabel();
    const armName = this.armLabel();
    const selected = this.students()
      .filter(s => this.selectedIds()[s.id])
      .map(s => s.id);

    if (!armId || armId === '0' || !categoryId || categoryId === '-1') {
      window.alert('Failed! One or more subject(s) has to be selected to continue.');
      return;
    }

    if (selected.length > 0) {
      if (
        !window.confirm(
          `Are you sure you want to assign "${categoryName}" to "${className}${armName}" Students?`,
        )
      ) {
        return;
      }

      this.submitting.set(true);
      this.api
        .assignOptionalCharges({ armId, categoryId, studentIds: selected })
        .subscribe({
          next: result => {
            this.submitting.set(false);
            if (result?.succeeded) {
              window.alert(result.message || 'Your selections has Successfully been updated');
              this.students.set([]);
              this.selectedIds.set({});
              this.studentMessage.set('');
              return;
            }
            window.alert(result?.message || 'Failed! Un-able to complete the process.');
          },
          error: err => {
            this.submitting.set(false);
            window.alert(
              err?.error?.message || err?.message || 'Failed! Un-able to complete the process.',
            );
          },
        });
      return;
    }

    if (
      !window.confirm(
        `Are you sure you want to Un-assign "${categoryName}" From "${className}${armName}" Students?`,
      )
    ) {
      return;
    }

    this.submitting.set(true);
    this.api.unassignOptionalCharges({ armId, categoryId }).subscribe({
      next: result => {
        this.submitting.set(false);
        if (result?.succeeded) {
          window.alert(result.message === 'succeed' ? 'succeed' : result.message || 'succeed');
          this.students.set([]);
          this.selectedIds.set({});
          this.studentMessage.set('');
          return;
        }
        window.alert(result?.message || 'Failed! Un-able to complete the process.');
      },
      error: err => {
        this.submitting.set(false);
        window.alert(
          err?.error?.message || err?.message || 'Failed! Un-able to complete the process.',
        );
      },
    });
  }

  viewCategoryItems(): void {
    const categoryId = this.categoryId();
    if (!categoryId || categoryId === '-1') {
      return;
    }
    this.showItemsModal.set(true);
    this.itemsLoading.set(true);
    this.categoryItems.set([]);
    this.api.getOptionalFeesItems(categoryId).subscribe({
      next: rows => {
        this.categoryItems.set(Array.isArray(rows) ? rows : []);
        this.itemsLoading.set(false);
      },
      error: () => {
        this.itemsLoading.set(false);
        this.categoryItems.set([]);
      },
    });
  }

  closeItemsModal(): void {
    this.showItemsModal.set(false);
  }

  formatCreatedDate(value: string): string {
    if (!value) {
      return '';
    }
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) {
      return value;
    }
    return d.toLocaleString();
  }
}
