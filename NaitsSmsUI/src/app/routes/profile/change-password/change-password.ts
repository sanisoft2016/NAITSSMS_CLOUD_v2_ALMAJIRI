import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { BaseComponent, GenericService } from '@shared';

type ChangePasswordResponse = {
  status?: string;
  Status?: string;
  data?: string;
  Data?: string;
};

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const newPassword = group.get('newPassword')?.value;
  const confirmPassword = group.get('confirmPassword')?.value;
  if (!newPassword || !confirmPassword) {
    return null;
  }

  return newPassword === confirmPassword ? null : { passwordsMismatch: true };
}

@Component({
  selector: 'app-change-password',
  standalone: true,
  templateUrl: './change-password.html',
  styleUrl: './change-password.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
})
export class ChangePassword extends BaseComponent {
  private readonly api = inject(GenericService<unknown>);
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<ChangePassword, boolean>);

  readonly form = this.fb.nonNullable.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

  constructor() {
    super();
  }

  cancel(): void {
    this.dialogRef.close(false);
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || !this.startSubmitting()) {
      return;
    }

    const { currentPassword, newPassword } = this.form.getRawValue();
    this.withRequestState(
      this.api.create<ChangePasswordResponse>('/user/post-change-password', {
        currentPassword,
        newPassword,
      }),
      {
        useLoading: false,
        clearError: true,
        errorMessage: 'Failed to change password.',
        notifyOnError: 'none',
        rethrow: true,
      },
    ).subscribe({
      next: response => {
        this.stopSubmitting();
        const status = String(response?.status ?? response?.Status ?? '').toLowerCase();
        const message = String(response?.data ?? response?.Data ?? '').trim();
        if (status === 'success') {
          this.showAlert(message || 'Password changed successfully.', { type: 'success' });
          this.dialogRef.close(true);
          return;
        }

        this.showAlert(message || 'Failed to change password.', { type: 'error' });
        this.cdr.markForCheck();
      },
      error: error => {
        this.stopSubmitting();
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to change password.'), {
          type: 'error',
        });
        this.cdr.markForCheck();
      },
    });
  }
}
