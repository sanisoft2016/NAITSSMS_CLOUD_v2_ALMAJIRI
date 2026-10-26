import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs/operators';

import { BaseComponent, GenericService, SignatureCropper, SignatureCropResult } from '@shared';

type CreateAdminAccountantResult = {
  success?: boolean;
  message?: string;
  code?: string;
  redirectPath?: string;
};

@Component({
  selector: 'app-create-admin-accountant',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    SignatureCropper,
  ],
  templateUrl: './create-admin-accountant.html',
  styleUrl: './create-admin-accountant.scss',
})
export class CreateAdminAccountant extends BaseComponent implements OnInit {
  private readonly api = inject(GenericService<any>);
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  schoolId = '';
  signatureDataUrl = '';
  signatureError: string | null = null;
  private signaturePending = false;
  private signatureCropped = false;

  readonly genders = [
    { value: 0, label: 'Male' },
    { value: 1, label: 'Female' },
  ];

  readonly form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    middleName: [''],
    surName: ['', Validators.required],
    email: [''],
    phoneNumber: [''],
    dateOfBirth: [null as Date | null],
    gender: [0, Validators.required],
  });

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.schoolId = String(this.activatedRoute.snapshot.queryParamMap.get('schoolId') ?? '').trim();
    if (!this.schoolId) {
      this.showAlert('School id is required to create Admin Accountant.', { type: 'warning' });
      this.router.navigate(['/supper-admin/school-list']);
    }
  }

  goBack(): void {
    this.router.navigate(['/supper-admin']);
  }

  onSignatureImageSelected(): void {
    this.signatureError = null;
    this.signaturePending = true;
    this.signatureCropped = false;
    this.signatureDataUrl = '';
  }

  onSignatureCropped(result: SignatureCropResult): void {
    this.signatureError = null;
    this.signaturePending = false;
    this.signatureCropped = true;
    this.signatureDataUrl = result.dataUrl;
  }

  onSignatureCleared(): void {
    this.signatureError = null;
    this.signaturePending = false;
    this.signatureCropped = false;
    this.signatureDataUrl = '';
  }

  submit(): void {
    if (!this.schoolId) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.showAlert('First Name and Sur Name must be entered to continue.', { type: 'warning' });
      return;
    }

    if (this.signaturePending && !this.signatureCropped) {
      this.signatureError = 'Please crop the image before submitting.';
      this.showAlert(this.signatureError, { type: 'warning' });
      return;
    }

    if (this.signatureError) {
      this.showAlert(this.signatureError, { type: 'warning' });
      return;
    }

    const raw = this.form.getRawValue();
    const payload = {
      firstName: raw.firstName.trim(),
      middleName: (raw.middleName ?? '').trim(),
      surName: raw.surName.trim(),
      email: (raw.email ?? '').trim(),
      phoneNumber: (raw.phoneNumber ?? '').trim(),
      dateOfBirth: raw.dateOfBirth ? new Date(raw.dateOfBirth).toISOString() : null,
      gender: Number(raw.gender),
      signatureImageString: this.signatureDataUrl || null,
    };

    if (!this.startSubmitting()) return;

    this.withRequestState(
      this.api.create<CreateAdminAccountantResult>(
        `/user-registration/create-admin-accountant-by-schoolId/${encodeURIComponent(this.schoolId)}`,
        payload,
      ),
      {
        errorMessage: 'Failed! We were unable to complete the process.',
        notifyOnError: 'none',
        rethrow: false,
        useLoading: false,
        onError: error => {
          const body =
            error && typeof error === 'object' && 'error' in error
              ? (error as { error?: Record<string, unknown> }).error
              : undefined;
          const apiMessage =
            (typeof body?.['message'] === 'string' && body['message']) ||
            (typeof body?.['Message'] === 'string' && body['Message']) ||
            (body?.['errors'] && typeof body['errors'] === 'object'
              ? Object.values(body['errors'] as Record<string, string[]>)
                  .flat()
                  .join(' ')
              : null) ||
            'Failed! We were unable to complete the process.';
          this.showAlert(String(apiMessage), { type: 'warning' });
        },
      },
    )
      .pipe(finalize(() => this.stopSubmitting()))
      .subscribe(result => {
        const message = result?.message || 'Failed! Unable to complete the process.';
        if (result?.success) {
          this.showAlert(message, { type: 'success' });
          this.router.navigateByUrl(result.redirectPath || '/supper-admin');
          return;
        }

        this.showAlert(message, { type: 'warning' });
        if (result?.redirectPath && (result.code === 'term_not_ready' || result.code === 'employee_failed')) {
          this.router.navigateByUrl(result.redirectPath);
        }
      });
  }
}
