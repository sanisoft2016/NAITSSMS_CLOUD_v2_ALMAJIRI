import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { CommonModule } from '@angular/common';
import { ClassRegistryService, CurrentClass } from '@shared/services';
import { ClassDto } from '@shared/interfaces';

@Component({
  selector: 'app-edit-class-modal',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatSelectModule,
  ],
  template: `
    <h2 mat-dialog-title>Edit Class Registration</h2>
    <mat-dialog-content>
      <form [formGroup]="editForm" class="edit-class-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Serial Number (SNo)</mat-label>
          <input matInput formControlName="sNo" readonly />
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Class Name</mat-label>
          <mat-select formControlName="className" required>
            @for (option of classNameOptions; track option.value) {
              <mat-option [value]="option.value">{{ option.label }}</mat-option>
            }
          </mat-select>
          @if (editForm.get('className')?.hasError('required') && editForm.get('className')?.touched) {
            <mat-error>Class Name is required</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Class Label</mat-label>
          <input matInput formControlName="classLabel" required />
          @if (editForm.get('classLabel')?.hasError('required') && editForm.get('classLabel')?.touched) {
            <mat-error>Class Label is required</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="onCancel()">Close</button>
      <button
        mat-raised-button
        color="primary"
        type="button"
        (click)="onSubmit()"
        [disabled]="editForm.invalid || isSubmitting"
      >
        Submit
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .edit-class-form {
        display: flex;
        flex-direction: column;
        min-width: 400px;
        padding: 20px 0;
      }

      .full-width {
        width: 100%;
        margin-bottom: 16px;
      }

      mat-dialog-content {
        max-height: 60vh;
        overflow-y: auto;
      }
    `,
  ],
})
export class EditClassModalComponent implements OnInit {
  private readonly dialogRef = inject(MatDialogRef<EditClassModalComponent>);
  private readonly data = inject<ClassDto>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly classService = inject(ClassRegistryService);
  private readonly cdr = inject(ChangeDetectorRef);

  editForm!: FormGroup;
  isSubmitting = false;

  readonly classNameOptions: Array<{ value: CurrentClass; label: string }> = [
    { value: CurrentClass.PlayClass, label: 'Play Class' },
    { value: CurrentClass.PreNursery, label: 'Pre Nursery' },
    { value: CurrentClass.Nursery1, label: 'Nursery 1' },
    { value: CurrentClass.Nursery2, label: 'Nursery 2' },
    { value: CurrentClass.Primary1, label: 'Primary 1' },
    { value: CurrentClass.Primary2, label: 'Primary 2' },
    { value: CurrentClass.Primary3, label: 'Primary 3' },
    { value: CurrentClass.Primary4, label: 'Primary 4' },
    { value: CurrentClass.Primary5, label: 'Primary 5' },
    { value: CurrentClass.Primary6, label: 'Primary 6' },
    { value: CurrentClass.Jss1, label: 'JSS 1' },
    { value: CurrentClass.Jss2, label: 'JSS 2' },
    { value: CurrentClass.Jss3, label: 'JSS 3' },
    { value: CurrentClass.Ss1, label: 'SS 1' },
    { value: CurrentClass.Ss2, label: 'SS 2' },
    { value: CurrentClass.Ss3, label: 'SS 3' },
  ];

  ngOnInit(): void {
    this.initForm();
  }

  private initForm(): void {
    this.editForm = this.fb.group({
      id: [this.data.id],
      className: [this.resolveClassName(this.data.className), Validators.required],
      classLabel: [this.data.classLabel ?? '', Validators.required],
      sNo: [this.data.sNo],
      schoolInfoId: [this.data.schoolInfoId],
    });
  }

  private resolveClassName(value: CurrentClass | string | number | null | undefined): CurrentClass {
    if (typeof value === 'number' && Number.isFinite(value)) {
      return value as CurrentClass;
    }

    const numeric = Number(value);
    if (Number.isFinite(numeric) && String(value ?? '').trim() !== '') {
      return numeric as CurrentClass;
    }

    const normalized = String(value ?? '').trim();
    if (!normalized) {
      return CurrentClass.PlayClass;
    }

    const byLabel = this.classNameOptions.find(
      option => option.label === normalized || String(option.value) === normalized,
    );
    if (byLabel) {
      return byLabel.value;
    }

    const enumKey = Object.keys(CurrentClass).find(
      key => key.toLowerCase() === normalized.toLowerCase(),
    ) as keyof typeof CurrentClass | undefined;

    if (enumKey !== undefined) {
      const enumValue = CurrentClass[enumKey];
      return typeof enumValue === 'number' ? enumValue : CurrentClass.PlayClass;
    }

    return CurrentClass.PlayClass;
  }

  onSubmit(): void {
    this.editForm.markAllAsTouched();
    if (this.editForm.invalid || this.isSubmitting) {
      return;
    }

    this.isSubmitting = true;
    const raw = this.editForm.getRawValue();
    const classDto: ClassDto = {
      id: String(raw.id ?? this.data.id ?? '').trim(),
      className: this.resolveClassName(raw.className),
      classLabel: String(raw.classLabel ?? '').trim(),
      sNo: Number(raw.sNo ?? this.data.sNo ?? 0),
      schoolInfoId: String(raw.schoolInfoId ?? this.data.schoolInfoId ?? '').trim(),
    };

    if (!classDto.id) {
      this.isSubmitting = false;
      alert('Unable to resolve the class record id.');
      this.cdr.markForCheck();
      return;
    }

    if (!classDto.classLabel) {
      classDto.classLabel = CurrentClass[classDto.className] ?? String(classDto.className);
    }

    this.classService.updateClass(classDto).subscribe({
      next: result => {
        if (result) {
          this.dialogRef.close({ success: true, message: 'Class updated successfully' });
          return;
        }

        this.isSubmitting = false;
        alert('Failed! The selected class name or label already exists.');
        this.cdr.markForCheck();
      },
      error: (error: unknown) => {
        console.error('Error updating class:', error);
        this.isSubmitting = false;
        const httpError = error as { error?: { message?: string }; status?: number };
        const message = httpError.error?.message
          ?? (httpError.status === 409
            ? 'Failed! The selected class name or label already exists.'
            : 'An error occurred while updating the class.');
        alert(message);
        this.cdr.markForCheck();
      },
    });
  }

  onCancel(): void {
    this.dialogRef.close();
  }
}
