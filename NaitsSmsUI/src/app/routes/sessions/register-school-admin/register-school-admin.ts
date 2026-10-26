import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs/operators';
import { SeoService } from '@core';
import { GenericService } from '@shared/services/generic.service';
import { BaseComponent } from '@shared';

@Component({
  selector: 'app-school-admin',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
  ],
  templateUrl: './register-school-admin.html',
  styleUrls: ['./register-school-admin.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterSchoolAdmin extends BaseComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(GenericService<any>);
  private readonly seo = inject(SeoService);

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.seo.apply({
      title: 'Register your school | NAITS-ASMS',
      description:
        'Create a free school administrator account on NAITS-ASMS at ourschoolonline.ng. Start results, attendance, fees and school records without a local server.',
      path: '/auth/register-school-admin',
      keywords:
        'register school NAITS-ASMS, ourschoolonline.ng signup, create school account, Nigeria school software, school administrator',
    });
  }


  readonly form = this.fb.nonNullable.group({
    firstName: ['', [Validators.required]],
    middleName: [''],
    surName: ['', [Validators.required]],
    userName: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    gender: ['', [Validators.required]],
    phoneNumber: ['', [Validators.required]],
    password: ['', [Validators.required]],
    confirmPassword: ['', [Validators.required]],
    schoolName: ['', [Validators.required]],
  });

  readonly genders = [
    { value: 'Male', label: 'Male' },
    { value: 'Female', label: 'Female' },
  ];

  goBack(): void {
    this.router.navigate(['/auth/login']);
  }

  // onSubmit(): void {
  //   this.form.markAllAsTouched();
  //   if (this.form.invalid || !this.startSubmitting()) return;

  //   const value = this.form.getRawValue();

  //   if (value.password !== value.confirmPassword) {
  //     this.stopSubmitting();
  //     this.showAlert('Password and confirm password do not match.');
  //     return;
  //   }

  //   const formData = new FormData();
  //   formData.append('firstName', value.firstName.trim());
  //   formData.append('middleName', value.middleName?.trim() ?? '');
  //   formData.append('surName', value.surName.trim());
  //   formData.append('userName', value.userName.trim());
  //   formData.append('email', value.email.trim());
  //   formData.append('gender', value.gender);
  //   formData.append('phoneNumber', value.phoneNumber.trim());
  //   formData.append('password', value.password);
  //   formData.append('confirmPassword', value.confirmPassword);
  //   formData.append('schoolName', value.schoolName.trim());

  //   this.api
  //     .createWithFile('/user-registration/create-school-registrar', formData)
  //     .pipe(
  //       finalize(() => {
  //         this.stopSubmitting();
  //       })
  //     )
  //     .subscribe({
  //       next: (response: any) => {
  //         if (response?.Status === 'Success') {
  //           this.showAlert(response?.Data?.Message ?? 'Registration successful.');
  //           this.router.navigate(['/auth/login']);
  //           return;
  //         }

  //         this.showAlert(response?.Data ?? 'Registration failed.');
  //       },
  //       error: (error: HttpErrorResponse) => {
  //         const message = this.getHttpErrorMessage(
  //           error,
  //           'An error occurred during registration. Please try again.'
  //         );
  //         this.showAlert(message);
  //       },
  //     });
  // }


  onSubmit(): void {
  this.form.markAllAsTouched();
  if (this.form.invalid || !this.startSubmitting()) return;

  const value = this.form.getRawValue();

  if (value.password !== value.confirmPassword) {
    this.stopSubmitting();
    this.showAlert('Password and confirm password do not match.');
    return;
  }

  const formData = new FormData();
  formData.append('firstName', value.firstName.trim());
  formData.append('middleName', value.middleName?.trim() ?? '');
  formData.append('surName', value.surName.trim());
  formData.append('userName', value.userName.trim());
  formData.append('email', value.email.trim());
  formData.append('gender', value.gender);
  formData.append('phoneNumber', value.phoneNumber.trim());
  formData.append('password', value.password);
  formData.append('confirmPassword', value.confirmPassword);
  formData.append('schoolName', value.schoolName.trim());

  this.withRequestState(
    this.api.createWithFile('/user-registration/create-school-registrar', formData),
    {
      useLoading: false,
      clearError: true,
      errorMessage: 'An error occurred during registration. Please try again.',
      notifyOnError: 'none',
      rethrow: true,
      onError: error => console.error('School registration failed:', error),
    }
  )
    .pipe(
      finalize(() => {
        this.stopSubmitting();
      })
    )
    .subscribe({
      next: (response: any) => {
        const status = String(response?.status ?? response?.Status ?? '').toLowerCase();
        if (status === 'success') {
          const message =
            response?.data?.message ??
            response?.data?.Message ??
            response?.Data?.Message ??
            'Registration successful. Please sign in with your new account.';
          this.showAlert(message);
          this.router.navigateByUrl('/auth/login');
          return;
        }

        this.showAlert(
          response?.data ?? response?.Data ?? 'Registration failed.'
        );
      },
      error: (error: HttpErrorResponse) => {
        const message = this.getHttpErrorMessage(
          error,
          'An error occurred during registration. Please try again.'
        );
        this.showAlert(message);
      },
    });
}


}
