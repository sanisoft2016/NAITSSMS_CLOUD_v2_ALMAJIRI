import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { finalize } from 'rxjs/operators';

import { GenericService } from '@shared';

export type SubordinateAccountantDetails = {
  id?: string;
  employeeStatus?: string;
  userId?: string;
  schoolId?: string;
  schoolInfo?: { schoolName?: string; SchoolName?: string };
  SchoolInfo?: { schoolName?: string; SchoolName?: string };
  user?: {
    id?: string;
    userName?: string;
    fullName?: string;
    firstName?: string;
    middleName?: string;
    surName?: string;
    accessStatus?: number | string;
  };
  User?: {
    id?: string;
    userName?: string;
    UserName?: string;
    fullName?: string;
    FullName?: string;
    firstName?: string;
    FirstName?: string;
    middleName?: string;
    MiddleName?: string;
    surName?: string;
    SurName?: string;
    accessStatus?: number | string;
    AccessStatus?: number | string;
  };
};

export type SubordinateAccountantDialogData = {
  schoolId: string;
  details: SubordinateAccountantDetails;
};

@Component({
  selector: 'app-subordinate-accountant-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './subordinate-accountant-dialog.html',
  styleUrl: './subordinate-accountant-dialog.scss',
})
export class SubordinateAccountantDialog {
  private readonly dialogRef = inject(MatDialogRef<SubordinateAccountantDialog, boolean>);
  private readonly data = inject<SubordinateAccountantDialogData>(MAT_DIALOG_DATA);
  private readonly api = inject(GenericService<any>);
  private readonly fb = inject(FormBuilder);

  readonly schoolId = this.data.schoolId;
  details = this.data.details;
  editing = false;
  busy = false;
  message: string | null = null;

  readonly editForm = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    middleName: [''],
    surName: ['', Validators.required],
    password: [''],
    confirmPassword: [''],
  });

  get schoolName(): string {
    const info = this.details.schoolInfo ?? this.details.SchoolInfo;
    return String(info?.schoolName ?? info?.SchoolName ?? '-');
  }

  get employeeName(): string {
    const user = this.details.user ?? this.details.User;
    const full =
      user?.fullName ??
      (user as { FullName?: string } | undefined)?.FullName ??
      [
        user?.firstName ?? (user as { FirstName?: string } | undefined)?.FirstName,
        user?.middleName ?? (user as { MiddleName?: string } | undefined)?.MiddleName,
        user?.surName ?? (user as { SurName?: string } | undefined)?.SurName,
      ]
        .filter(Boolean)
        .join(' ')
        .trim();
    return full || '-';
  }

  get userName(): string {
    const user = this.details.user ?? this.details.User;
    return String(
      user?.userName ?? (user as { UserName?: string } | undefined)?.UserName ?? '-',
    );
  }

  get employeeStatus(): string {
    return String(this.details.employeeStatus ?? '-');
  }

  get userId(): string {
    const user = this.details.user ?? this.details.User;
    return String(user?.id ?? this.details.userId ?? '');
  }

  get employeeId(): string {
    return String(this.details.id ?? '');
  }

  get isActive(): boolean {
    const user = this.details.user ?? this.details.User;
    const status = user?.accessStatus ?? (user as { AccessStatus?: number | string } | undefined)?.AccessStatus;
    return status === 0 || status === '0' || String(status).toLowerCase() === 'active';
  }

  startEdit(): void {
    const parts = this.employeeName.split(/\s+/).filter(Boolean);
    this.editForm.reset({
      firstName: parts[0] ?? '',
      middleName: parts.length === 3 ? parts[1] : '',
      surName: parts.length >= 2 ? parts[parts.length - 1] : '',
      password: '',
      confirmPassword: '',
    });
    this.editing = true;
    this.message = null;
  }

  cancelEdit(): void {
    this.editing = false;
    this.message = null;
  }

  saveChanges(): void {
    if (this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      this.message = 'First Name and Sur Name must be entered to continue.';
      return;
    }

    const raw = this.editForm.getRawValue();
    if (
      (raw.password.trim() && raw.password !== raw.confirmPassword) ||
      (raw.confirmPassword.trim() && raw.confirmPassword !== raw.password)
    ) {
      this.message = 'Password mis-matched.';
      return;
    }

    this.busy = true;
    this.message = null;
    this.api
      .create<boolean>(
        `/user-registration/update-subordinate-accountant-by-schoolId/${encodeURIComponent(this.schoolId)}`,
        {
          schoolRegId: this.schoolId,
          firstName: raw.firstName.trim(),
          middleName: (raw.middleName ?? '').trim(),
          surName: raw.surName.trim(),
          password: raw.password.trim() || null,
          confirmPassword: raw.confirmPassword.trim() || null,
        },
      )
      .pipe(finalize(() => (this.busy = false)))
      .subscribe({
        next: ok => {
          if (ok) {
            this.dialogRef.close(true);
            return;
          }
          this.message = 'Failed! Unable to complete the process.';
        },
        error: () => {
          this.message = 'Failed! Unable to complete the process.';
        },
      });
  }

  toggleAccess(): void {
    const id = this.userId;
    if (!id || this.busy) return;

    const path = this.isActive
      ? `/user-registration/disable-subordinate-accountant/${encodeURIComponent(id)}`
      : `/user-registration/enable-subordinate-accountant/${encodeURIComponent(id)}`;

    this.busy = true;
    this.message = null;
    this.api
      .getAnObj<boolean>(path)
      .pipe(finalize(() => (this.busy = false)))
      .subscribe({
        next: ok => {
          if (ok) {
            this.dialogRef.close(true);
            return;
          }
          this.message = 'Failed! We were unable to complete the process.';
        },
        error: () => {
          this.message = 'Failed! We were unable to complete the process.';
        },
      });
  }

  deleteAccountant(): void {
    const id = this.employeeId;
    if (!id || this.busy) return;
    if (!confirm(`Are you sure you want to DELETE "${this.employeeName}"? If 'Yes', click 'Ok' to continue.`)) {
      return;
    }

    this.busy = true;
    this.message = null;
    this.api
      .create<boolean>(
        `/user-registration/delete-subordinate-accountant/${encodeURIComponent(id)}`,
        {},
      )
      .pipe(finalize(() => (this.busy = false)))
      .subscribe({
        next: ok => {
          if (ok) {
            this.dialogRef.close(true);
            return;
          }
          this.message = 'Failed! Unable to complete the process.';
        },
        error: () => {
          this.message = 'Failed! Unable to complete the process.';
        },
      });
  }

  close(): void {
    this.dialogRef.close(false);
  }
}
