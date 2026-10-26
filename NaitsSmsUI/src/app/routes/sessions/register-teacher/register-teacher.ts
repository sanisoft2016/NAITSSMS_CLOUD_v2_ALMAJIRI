/* eslint-disable @angular-eslint/prefer-standalone */
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { SeoService } from '@core';

import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { BaseComponent, GenericService, SignatureCropper, SignatureCropResult } from '@shared';
import {
  TeacherCreateRequest,
  TeacherCreateResponse,
  TeacherRegistrationEmployeeStatus,
  TeacherRegistrationGender,
} from '@shared';

type Option<T extends string> = { value: T; label: string };

type Gender = TeacherRegistrationGender;
type EmployeeStatus = TeacherRegistrationEmployeeStatus;

@Component({
  standalone: true,
  selector: 'app-register',
  imports: [
    CommonModule,
    ReactiveFormsModule,

    MatCardModule,
    MatButtonModule,
    MatDividerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,

    SignatureCropper,
  ],
  templateUrl: './register-teacher.html',
  styleUrls: ['./register-teacher.scss'],
})
export class RegisterTeacher extends BaseComponent implements OnInit {
  private readonly seo = inject(SeoService);
  registerForm: FormGroup;

  genders: Array<Option<Gender>> = [
    { value: 'Male', label: 'Male' },
    { value: 'Female', label: 'Female' },
  ];

  employeeStatuses: Array<Option<EmployeeStatus>> = [
    { value: 'Teacher', label: 'Teacher' },
    { value: 'Accountant', label: 'Accountant' },
    { value: 'Security', label: 'Security' },
    { value: 'Principal', label: 'Principal' },
    { value: 'Headmaster', label: 'Headmaster' },
    { value: 'Others', label: 'Others' },
  ];

  signatureError: string | null = null;

  private signatureCropped = false;
  private signaturePending = false;
  private croppedSignatureDataUrl: string | null = null;

  constructor(
    private readonly fb: FormBuilder,
    private readonly api: GenericService<unknown>
  ) {
    super();

    this.registerForm = this.fb.group({
      employeeIdNo: [''],
      firstName: ['', Validators.required],
      middleName: [''],
      surName: [''],
      userName: ['', Validators.required],
      email: ['', Validators.email],
      phoneNumber: [''],
      dateOfBirth: [new Date()],
      gender: [this.genders[0]?.value ?? null, Validators.required],
      employeeStatus: [this.employeeStatuses[0]?.value ?? null, Validators.required],

      // set when "Crop & Use" succeeds
      signatureFile: [null],
    });
  }

  get teacherId(): string {
    return this.route.snapshot.queryParamMap.get('teacherId')?.trim() ?? '';
  }

  get isEditMode(): boolean {
    return this.teacherId.length > 0;
  }

  get pageTitle(): string {
    return this.isEditMode ? 'Edit Employee' : "Employees' Registration";
  }

  get submitLabel(): string {
    return this.isEditMode ? 'Save Changes' : 'Submit';
  }

  ngOnInit(): void {
    this.seo.apply({
      title: 'Staff registration',
      description: 'NAITS-ASMS staff registration form.',
      path: '/auth/register-teacher',
      robots: 'noindex,nofollow',
    });
    if (this.isEditMode) {
      void this.loadTeacherForEdit();
    }
  }

  onSignatureImageSelected(): void {
    this.signatureError = null;
    this.signaturePending = true;
    this.signatureCropped = false;
    this.croppedSignatureDataUrl = null;
    this.registerForm.get('signatureFile')?.setValue(null);
  }

  onSignatureCropped(result: SignatureCropResult): void {
    this.signatureError = null;
    this.signaturePending = false;
    this.signatureCropped = true;
    this.croppedSignatureDataUrl = result.dataUrl;
    this.registerForm.get('signatureFile')?.setValue(result.file);
  }

  onSignatureCleared(): void {
    this.signatureError = null;
    this.signaturePending = false;
    this.signatureCropped = false;
    this.croppedSignatureDataUrl = null;
    this.registerForm.get('signatureFile')?.setValue(null);
  }

  async onSubmit(): Promise<void> {
    this.registerForm.markAllAsTouched();

    // Legacy behavior: if user picked an image but didn’t crop, block submit
    if (this.signaturePending && !this.signatureCropped) {
      this.signatureError = 'Please crop the image before submitting.';
      this.cdr.markForCheck();
      return;
    }

    if (this.registerForm.invalid || !this.startSubmitting()) {
      this.cdr.markForCheck();
      return;
    }

    try {
      const request = await this.buildTeacherCreateRequest();
      const request$ = this.isEditMode
        ? this.api.update<TeacherCreateResponse>(`/user-registration/update-teacher-registration-byId/${this.teacherId}`, request)
        : this.api.create<TeacherCreateResponse>('/user-registration/create-teacher-registration', request);

      const response = await firstValueFrom(
        this.withRequestState(request$, {
          useLoading: false,
          clearError: true,
          errorMessage: this.isEditMode ? 'Failed to update teacher.' : 'Failed to register teacher.',
          notifyOnError: 'none',
          rethrow: true,
          onError: error => console.error(this.isEditMode ? 'Teacher update failed:' : 'Teacher registration failed:', error),
        })
      );

      this.showAlert(response?.message ?? (this.isEditMode ? 'Teacher details updated successfully.' : 'Teacher Registration Succeeded.'), {
        type: 'success',
      });
      await this.router.navigate(['/admin/teachers']);
    } catch (error) {
      const message = this.getHttpErrorMessage(
        error,
        'Failed! We were unable to complete the process. Contact the system administrator for solutions.'
      );
      this.showAlert(message, { type: 'error' });
    } finally {
      this.stopSubmitting();
      this.cdr.markForCheck();
    }
  }

  private async loadTeacherForEdit(): Promise<void> {
    try {
      var id = this.teacherId;
      const teacher = await firstValueFrom(
        this.withRequestState(this.api.getAnObj<TeacherCreateRequest>(`/user-registration/get-teacher-registration-byId/${id}`), {
          errorMessage: 'Failed to load teacher details.',
          notifyOnError: 'none',
          rethrow: true,
          onError: error => console.error('Failed to load teacher details:', error),
        })
      );

      this.registerForm.patchValue({
        employeeIdNo: teacher.employeeIdNo ?? '',
        firstName: teacher.firstName ?? '',
        middleName: teacher.middleName ?? '',
        surName: teacher.surName ?? '',
        userName: teacher.userName ?? '',
        email: teacher.email ?? '',
        phoneNumber: teacher.phoneNumber ?? '',
        dateOfBirth: teacher.dateOfBirth ? new Date(teacher.dateOfBirth) : new Date(),
        gender: teacher.gender ?? this.genders[0]?.value ?? null,
        employeeStatus: teacher.employeeStatus ?? this.employeeStatuses[0]?.value ?? null,
      });

      this.cdr.markForCheck();
    } catch (error) {
      const message = this.getHttpErrorMessage(error, 'Failed to load teacher details.');
      this.showAlert(message, { type: 'error' });
      await this.router.navigate(['/admin/teachers']);
    }
  }

  private async buildTeacherCreateRequest(): Promise<TeacherCreateRequest> {
    const value = this.registerForm.getRawValue();
    const signatureFile = this.registerForm.get('signatureFile')?.value as File | null;

    return {
      employeeIdNo: this.trimValue(value.employeeIdNo),
      firstName: this.trimValue(value.firstName),
      middleName: this.trimValue(value.middleName),
      surName: this.trimValue(value.surName),
      userName: this.trimValue(value.userName),
      email: this.trimValue(value.email),
      phoneNumber: this.trimValue(value.phoneNumber),
      dateOfBirth: this.normalizeDateValue(value.dateOfBirth),
      gender: value.gender as Gender,
      employeeStatus: value.employeeStatus as EmployeeStatus,
      signatureImageString:
        this.croppedSignatureDataUrl ?? (signatureFile ? await this.blobToDataUrl(signatureFile) : ''),
    };
  }

  private trimValue(value: unknown): string {
    return typeof value === 'string' ? value.trim() : '';
  }

  private normalizeDateValue(value: unknown): string | null {
    if (!value) {
      return null;
    }

    const date = value instanceof Date ? value : new Date(String(value));
    return Number.isNaN(date.getTime()) ? null : date.toISOString();
  }

  private blobToDataUrl(blob: Blob): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result || ''));
      reader.onerror = () => reject(reader.error ?? new Error('Failed to read image payload.'));
      reader.readAsDataURL(blob);
    });
  }
}
