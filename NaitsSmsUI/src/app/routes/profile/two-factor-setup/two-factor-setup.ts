import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { BaseComponent, GenericService } from '@shared';

type ApiEnvelope = {
  status?: string;
  Status?: string;
  data?: any;
  Data?: any;
};

@Component({
  selector: 'app-two-factor-setup',
  standalone: true,
  templateUrl: './two-factor-setup.html',
  styleUrl: './two-factor-setup.scss',
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
export class TwoFactorSetup extends BaseComponent implements OnInit {
  private readonly api = inject(GenericService<unknown>);
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<TwoFactorSetup, boolean>);

  readonly enabled = signal(false);
  readonly canManage = signal(false);
  readonly sharedKey = signal('');
  readonly authenticatorUri = signal('');
  readonly qrUrl = signal('');
  readonly setupStarted = signal(false);

  readonly enableForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(12)]],
  });

  readonly disableForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(12)]],
  });

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.loadStatus();
  }

  close(): void {
    this.dialogRef.close(false);
  }

  loadStatus(): void {
    this.api.getAnObj('/user/2fa/status').subscribe({
      next: (response: ApiEnvelope) => {
        const payload = response?.data ?? response?.Data ?? {};
        this.enabled.set(!!(payload.enabled ?? payload.Enabled));
        this.canManage.set(!!(payload.canManage ?? payload.CanManage));
        this.cdr.markForCheck();
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to load 2FA status.'), { type: 'error' });
      },
    });
  }

  startSetup(): void {
    if (!this.startSubmitting()) {
      return;
    }

    this.withRequestState(this.api.create('/user/2fa/setup/start', {}), {
      useLoading: false,
      clearError: true,
      errorMessage: 'Failed to start 2FA setup.',
      notifyOnError: 'none',
      rethrow: true,
    }).subscribe({
      next: (response: ApiEnvelope) => {
        this.stopSubmitting();
        const status = String(response?.status ?? response?.Status ?? '').toLowerCase();
        const payload = response?.data ?? response?.Data ?? {};
        if (status !== 'success') {
          this.showAlert(String(payload ?? 'Failed to start 2FA setup.'), { type: 'error' });
          this.cdr.markForCheck();
          return;
        }

        const key = String(payload.sharedKey ?? payload.SharedKey ?? '');
        const uri = String(payload.authenticatorUri ?? payload.AuthenticatorUri ?? '');
        this.sharedKey.set(key);
        this.authenticatorUri.set(uri);
        this.qrUrl.set(
          uri
            ? `https://api.qrserver.com/v1/create-qr-code/?size=180x180&data=${encodeURIComponent(uri)}`
            : '',
        );
        this.setupStarted.set(true);
        this.enableForm.reset();
        this.cdr.markForCheck();
      },
      error: error => {
        this.stopSubmitting();
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to start 2FA setup.'), { type: 'error' });
        this.cdr.markForCheck();
      },
    });
  }

  enable(): void {
    this.enableForm.markAllAsTouched();
    if (this.enableForm.invalid || !this.startSubmitting()) {
      return;
    }

    const code = this.enableForm.getRawValue().code;
    this.withRequestState(this.api.create('/user/2fa/setup/enable', { code }), {
      useLoading: false,
      clearError: true,
      errorMessage: 'Failed to enable 2FA.',
      notifyOnError: 'none',
      rethrow: true,
    }).subscribe({
      next: (response: ApiEnvelope) => {
        this.stopSubmitting();
        const status = String(response?.status ?? response?.Status ?? '').toLowerCase();
        const payload = response?.data ?? response?.Data ?? {};
        const message = typeof payload === 'string' ? payload : String(payload?.message ?? '');
        if (status !== 'success') {
          this.showAlert(message || 'Failed to enable 2FA.', { type: 'error' });
          this.cdr.markForCheck();
          return;
        }

        this.enabled.set(true);
        this.setupStarted.set(false);
        this.showAlert(message || 'Two-factor authentication enabled.', { type: 'success' });
        this.dialogRef.close(true);
      },
      error: error => {
        this.stopSubmitting();
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to enable 2FA.'), { type: 'error' });
        this.cdr.markForCheck();
      },
    });
  }

  disable(): void {
    this.disableForm.markAllAsTouched();
    if (this.disableForm.invalid || !this.startSubmitting()) {
      return;
    }

    const code = this.disableForm.getRawValue().code;
    this.withRequestState(this.api.create('/user/2fa/disable', { code }), {
      useLoading: false,
      clearError: true,
      errorMessage: 'Failed to disable 2FA.',
      notifyOnError: 'none',
      rethrow: true,
    }).subscribe({
      next: (response: ApiEnvelope) => {
        this.stopSubmitting();
        const status = String(response?.status ?? response?.Status ?? '').toLowerCase();
        const payload = response?.data ?? response?.Data ?? {};
        const message = typeof payload === 'string' ? payload : String(payload?.message ?? '');
        if (status !== 'success') {
          this.showAlert(message || 'Failed to disable 2FA.', { type: 'error' });
          this.cdr.markForCheck();
          return;
        }

        this.enabled.set(false);
        this.showAlert(message || 'Two-factor authentication disabled.', { type: 'success' });
        this.dialogRef.close(true);
      },
      error: error => {
        this.stopSubmitting();
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to disable 2FA.'), { type: 'error' });
        this.cdr.markForCheck();
      },
    });
  }
}
