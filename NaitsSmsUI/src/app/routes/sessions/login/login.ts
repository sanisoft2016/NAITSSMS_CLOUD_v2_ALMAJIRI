import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { MtxButtonModule } from '@ng-matero/extensions/button';
import { TranslateModule } from '@ngx-translate/core';

import { AuthService } from '@core/authentication';
import { SeoService } from '@core';
import { BaseComponent } from '@shared';
import {
  isStudentOrParentRole,
  isTeacherLikeRole,
  normalizeCredentialRoles,
} from '@shared/models/UserCredential';
import { writeAccountingOptionId } from '@theme/sidemenu/accounting-menu.config';

@Component({
  selector: 'app-login',
  standalone: true,
  templateUrl: './login.html',
  styleUrl: './login.scss',
  imports: [
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MtxButtonModule,
    TranslateModule,
  ],
})
export class Login extends BaseComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly seo = inject(SeoService);
  readonly cdr = inject(ChangeDetectorRef);

  readonly requiresTwoFactor = signal(false);
  readonly hidePassword = signal(true);
  private twoFactorToken = '';

  constructor() {
    super();
  }

  loginForm = this.fb.nonNullable.group({
    username: ['', [Validators.required]],
    password: ['', [Validators.required]],
    rememberMe: [false],
  });

  twoFactorForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(12)]],
  });

  get username() {
    return this.loginForm.get('username')!;
  }

  get password() {
    return this.loginForm.get('password')!;
  }

  get rememberMe() {
    return this.loginForm.get('rememberMe')!;
  }

  ngOnInit(): void {
    this.seo.apply({
      title: 'Sign in to NAITS-ASMS',
      description:
        'NAITS-ASMS is a secure and easy-to-use school management system for pre-nursery, nursery, primary, and secondary schools. Manage student records, admissions, results, attendance, school fees, payroll, expenses, and administration in one platform.',
      path: '/auth/login',
      keywords:
        'NAITS-ASMS login, ourschoolonline.ng, school management login, Nigeria school software, school results, attendance, school fees',
    });
    this.handleRedirectMessageOnLoad();
  }

  private handleRedirectMessageOnLoad(): void {
    const credential = this.getUserCredential();
    let redirectMsg = this.getRedirectMsg();

    if (credential === null) {
      if (!redirectMsg) {
        return;
      }

      if (redirectMsg.message) {
        this.showNotification('warning', redirectMsg.message);
      }
      return;
    }

    if (redirectMsg?.status === 'Access Denial') {
      if (redirectMsg.message) {
        this.showNotification('warning', redirectMsg.message);
      }

      redirectMsg = { status: null, message: null };
      localStorage.setItem('redirect-msg', JSON.stringify(redirectMsg));
    }
  }

  async login() {
    if (!this.startSubmitting()) return;
    try {
      const response: any = await this.auth.login(
        this.username.value,
        this.password.value,
        this.rememberMe.value
      );

      const status = String(response?.status ?? response?.Status ?? '');
      const payload = response?.data ?? response?.Data ?? {};

      if (status === 'RequiresTwoFactor') {
        this.twoFactorToken = String(payload?.twoFactorToken ?? payload?.TwoFactorToken ?? '');
        this.requiresTwoFactor.set(true);
        this.twoFactorForm.reset();
        this.showNotification(
          'info',
          String(payload?.message ?? payload?.Message ?? 'Enter your authenticator code.'),
        );
        return;
      }

      if (status === 'Success') {
        await this.finishSuccessfulLogin(response);
        return;
      }

      const message =
        typeof payload === 'string'
          ? payload
          : String(payload?.message ?? payload?.Message ?? '').trim();
      this.showNotification('warning', message || 'Login failed: Wrong Username or Password!');
    } catch (errorRes: any) {
      this.handleLoginHttpError(errorRes);
    } finally {
      this.stopSubmitting();
      this.cdr.markForCheck();
    }
  }

  async verifyTwoFactor() {
    this.twoFactorForm.markAllAsTouched();
    if (this.twoFactorForm.invalid || !this.twoFactorToken || !this.startSubmitting()) {
      return;
    }

    try {
      const code = this.twoFactorForm.getRawValue().code;
      const response: any = await this.auth.loginTwoFactor(this.twoFactorToken, code);
      const status = String(response?.status ?? response?.Status ?? '');
      const payload = response?.data ?? response?.Data ?? {};

      if (status === 'Success') {
        await this.finishSuccessfulLogin(response);
        return;
      }

      const message =
        typeof payload === 'string'
          ? payload
          : String(payload?.message ?? payload?.Message ?? '').trim();
      this.showNotification('warning', message || 'Invalid authenticator code.');
      if (
        String(message).toLowerCase().includes('expired') ||
        String(message).toLowerCase().includes('sign in again')
      ) {
        this.cancelTwoFactor();
      }
    } catch (errorRes: any) {
      this.handleLoginHttpError(errorRes);
    } finally {
      this.stopSubmitting();
      this.cdr.markForCheck();
    }
  }

  cancelTwoFactor(): void {
    this.requiresTwoFactor.set(false);
    this.twoFactorToken = '';
    this.twoFactorForm.reset();
  }

  private async finishSuccessfulLogin(response: any): Promise<void> {
    const payload = response?.data ?? response?.Data ?? {};
    const roles = normalizeCredentialRoles(payload?.role ?? payload?.Role);
    const token = payload?.token;
    const accessToken =
      typeof token === 'string'
        ? token
        : token?.accessToken ?? token?.access_token ?? '';

    await this.auth.setUserCredential({
      token: accessToken,
      expiration: String(token?.exp ?? token?.expiresIn ?? ''),
      role: roles,
      userFullName: payload?.userFullName ?? this.username.value,
      userName: String(payload?.userName ?? this.username.value ?? '').trim(),
    });

    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    const targetUrl = returnUrl && returnUrl.startsWith('/') ? returnUrl : '/dashboard';
    const hasRole = (name: string) =>
      roles.some(role => role.toLowerCase() === name.toLowerCase());

    // Prefer the most specific admin/registry destinations when a user has multiple roles.
    if (hasRole('Commission')) {
      this.router.navigateByUrl(targetUrl.startsWith('/commission') ? targetUrl : '/commission');
      return;
    }

    if (hasRole('SAOfficer')) {
      this.router.navigateByUrl(
        targetUrl.startsWith('/supper-admin') ? targetUrl : '/supper-admin',
      );
      return;
    }

    if (hasRole('Registry')) {
      this.router.navigateByUrl(targetUrl.startsWith('/admin') ? targetUrl : '/admin');
      return;
    }

    if (hasRole('Accounting')) {
      writeAccountingOptionId('3');
      this.router.navigateByUrl(
        targetUrl.startsWith('/accounting') ? targetUrl : '/accounting/income-statement',
      );
      return;
    }

    if (roles.some(isTeacherLikeRole)) {
      this.router.navigateByUrl('/teacher');
      return;
    }

    if (roles.some(isStudentOrParentRole)) {
      this.router.navigateByUrl(
        targetUrl.startsWith('/student') ? targetUrl : '/student',
      );
      return;
    }

    this.auth.logout().subscribe(() => {
      this.showNotification('warning', 'Previllege has not yet been created for this category of users');
    });
  }

  private handleLoginHttpError(errorRes: unknown): void {
    const httpError = errorRes as HttpErrorResponse;
    if (httpError?.status === 422) {
      const form = this.loginForm;
      const errors = (httpError as any).error?.errors;
      if (errors) {
        Object.keys(errors).forEach(key => {
          form.get(key === 'email' ? 'username' : key)?.setErrors({
            remote: errors[key][0],
          });
        });
      }
      return;
    }

    this.showNotification(
      'error',
      this.getHttpErrorMessage(httpError, 'Login failed. Please try again.'),
    );
  }
}
