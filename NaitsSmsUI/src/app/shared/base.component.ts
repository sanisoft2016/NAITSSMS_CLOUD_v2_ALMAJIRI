import { ChangeDetectorRef, DestroyRef, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { HotToastService } from '@ngxpert/hot-toast';
import { catchError, EMPTY, finalize, Observable, throwError } from 'rxjs';

import { CurrentPageService } from './services/current-page.service';
import { FormControl, FormGroup } from '@angular/forms';
import { UserCredential } from './models/UserCredential';
declare var $: any;

type RedirectMessage = {
  status: string | null;
  message: string | null;
};

type AlertType = 'info' | 'success' | 'warning' | 'error';

type AlertOptions = {
  type?: AlertType;
  duration?: number;
  position?: 'top-left' | 'top-center' | 'top-right' | 'bottom-left' | 'bottom-center' | 'bottom-right';
  dismissible?: boolean;
};

export type RequestStateOptions = {
  /** Set to false if you don't want BaseComponent to toggle `isLoading`. */
  useLoading?: boolean;
  /** Set to false if you don't want BaseComponent to clear `errorMessage` before the request. */
  clearError?: boolean;
  /** Message to expose via `errorMessage` on failure. */
  errorMessage?: string;
  /** Optional callback for logging/telemetry. */
  onError?: (error: unknown) => void;

  /** Optional centralized UI notification on error. */
  notifyOnError?: 'none' | 'alert';

  /**
   * When false, the error is swallowed (Observable completes) after state + notification.
   * Useful when you want zero per-component error handlers.
   */
  rethrow?: boolean;
};

/**
 * Common base class for UI components.
 * - Tracks current page URL
 * - Provides shared request state helpers (loading/error)
 */
export abstract class BaseComponent {
  protected readonly destroyRef = inject(DestroyRef);
  protected readonly router = inject(Router);
  protected readonly route = inject(ActivatedRoute);
  protected readonly currentPage = inject(CurrentPageService);
  protected readonly toast = inject(HotToastService);
  protected readonly cdr = inject(ChangeDetectorRef);

  /** Current URL after redirects. Useful for per-page state keys. */
  readonly currentUrl = this.currentPage.currentUrl;

  /** Generic loading flag for async operations. */
  readonly isLoading = signal(false);

  /** Last user-friendly error message (if any). */
  readonly errorMessage = signal<string | null>(null);

  /** Shared submit state for form actions. */
  readonly isSubmitting = signal(false);

  protected constructor() {}

  protected startSubmitting(): boolean {
    if (this.isSubmitting()) {
      return false;
    }

    this.isSubmitting.set(true);
    return true;
  }

  protected stopSubmitting(): void {
    this.isSubmitting.set(false);
  }

  protected showAlert(message: string, options?: AlertOptions): void {
    const type = options?.type ?? 'info';
    const config = {
      duration: options?.duration ?? 4000,
      position: options?.position ?? 'top-right',
      dismissible: options?.dismissible ?? true,
    } as const;

    switch (type) {
      case 'success':
        this.toast.success(message, config);
        break;
      case 'warning':
        this.toast.warning(message, config);
        break;
      case 'error':
        this.toast.error(message, config);
        break;
      default:
        this.toast.show(message, config);
        break;
    }
  }

  protected getHttpErrorMessage(error: unknown, fallback = 'An error occurred.'): string {
    const httpError = error as HttpErrorResponse | undefined;
    const payload = httpError?.error;
    const validationErrors = payload?.errors;

    if (validationErrors && typeof validationErrors === 'object') {
      const messages = Object.values(validationErrors)
        .flatMap(value => (Array.isArray(value) ? value : [String(value)]))
        .filter(Boolean);

      if (messages.length > 0) {
        return messages.join('\n');
      }
    }

    return (
      payload?.message ??
      payload?.Message ??
      payload?.Data ??
      payload?.data ??
      payload?.title ??
      fallback
    );
  }

  /**
   * Wrap an Observable request and manage shared loading + error signals.
   * By default re-throws the original error after setting `errorMessage`.
   */
  protected withRequestState<T>(request$: Observable<T>, options?: RequestStateOptions): Observable<T> {
    const useLoading = options?.useLoading ?? true;
    const clearError = options?.clearError ?? true;
    const notifyOnError = options?.notifyOnError ?? 'none';
    const rethrow = options?.rethrow ?? true;

    if (clearError) this.errorMessage.set(null);
    if (useLoading) this.isLoading.set(true);

    return request$.pipe(
      finalize(() => {
        if (useLoading) this.isLoading.set(false);
        // Zoneless apps do not auto-refresh after HTTP; ensure the view can update.
        this.cdr.markForCheck();
      }),
      catchError(error => {
        const message = options?.errorMessage ?? 'An error occurred.';
        this.errorMessage.set(message);
        options?.onError?.(error);

        if (notifyOnError === 'alert') {
          this.showAlert(message);
        }

        this.cdr.markForCheck();
        return rethrow ? throwError(() => error) : EMPTY;
      })
    );
  }


  validateAllFormFields(formGroup: FormGroup) {
    Object.keys(formGroup.controls).forEach((field) => {
      const control = formGroup.get(field);
      if (control instanceof FormControl) {
        control.markAsTouched({ onlySelf: true });
      } else if (control instanceof FormGroup) {
        this.validateAllFormFields(control);
      }
    });
  }

  accessDeniedErrorHandler() {
    let redirectMsg = {
            status: "Access Denial",
            message:
            "Sorry! You later logged in with another user account.\n Please re-login with the appropriate user account to access that page.",
        };

         localStorage.setItem("redirect-msg", JSON.stringify(redirectMsg));
         this.router.navigate(["/pages/login"]);

    //    this.apiService
    //   .finishProcessBeforeContinuing(redirectMsg)
    //   .then(async (resolve: boolean) => {
    //     this.router.navigate(["/pages/login"]);
    //   });
  }

  accessTokenExpiryErrorHandler() {
    let redirectMsg = {
      status: "Access Expiry",
      message:
        "Sorry! Your acess-validity has expired.\n Please re-login with correct username and password to procceed.",
    };
    localStorage.setItem("redirect-msg", JSON.stringify(redirectMsg));

    const newCred: UserCredential | null = null;

    this.setUserCredential(newCred)
      .then(() => {
        this.router.navigate(["/pages/login"]);
      });
  }

  networkConnectionErrorHandler(): void {
    this.showNotification(
      "danger",
      "Sorry! Unable to connect to the remote server. Please check your internet connection."
    );
  }

  unKnownErrorHandler(): void {
    this.showNotification(
      "danger",
      "Sorry! Unable to complete the process. Please contact your system administrator."
    );
  }

  showNotification(color: string, message: string) {
    const notify = (globalThis as { $?: { notify?: (content: unknown, options: unknown) => void } }).$?.notify;

    if (typeof notify !== 'function') {
      this.showAlert(message);
      return;
    }

    notify(
      {
        icon: "notifications",
        message: message,
      },
      this.getNotificationObj(color)
    );
  }







    setUserCredential(credential: UserCredential | null): Promise<boolean> {
      return new Promise<boolean>((resolve) => {
            localStorage.setItem("credential", JSON.stringify(credential));
            resolve(true);
        });
    }

    getUserCredential(): UserCredential | null {
      try {
        const storedCredential = localStorage.getItem('credential');
        if (!storedCredential || storedCredential === 'null') {
          return null;
        }

        return JSON.parse(storedCredential) as UserCredential;
      } catch {
        return null;
      }
    }

    getRedirectMsg(): RedirectMessage | null {
      try {
        const storedRedirectMsg = localStorage.getItem('redirect-msg');
        if (!storedRedirectMsg || storedRedirectMsg === '{}' || storedRedirectMsg === 'null') {
          return null;
        }

        const redirectMsg = JSON.parse(storedRedirectMsg) as Partial<RedirectMessage> | null;
        if (!redirectMsg || (!redirectMsg.status && !redirectMsg.message)) {
          return null;
        }

        return {
          status: redirectMsg.status ?? null,
          message: redirectMsg.message ?? null,
        };
      } catch {
        return null;
      }
    }

    clearRedirectMsg(): void {
      localStorage.setItem('redirect-msg', JSON.stringify({ status: null, message: null }));
    }

    getNotificationObj(color: string): any {
        return {
            type: color,
            timer: 3000,
            placement: {
                from: "top",
                align: "right"
            },
            template: '<div data-notify="container" class="col-xs-11 col-sm-3 alert alert-{0} alert-with-icon" role="alert">' +
                '<button mat-raised-button type="button" aria-hidden="true" class="close" data-notify="dismiss">  <i class="material-icons">close</i></button>' +
                '<i class="material-icons" data-notify="icon">notifications</i> ' +
                '<span data-notify="title">{1}</span> ' +
                '<span data-notify="message">{2}</span>' +
                '<div class="progress" data-notify="progressbar">' +
                '<div class="progress-bar progress-bar-{0}" role="progressbar" aria-valuenow="0" aria-valuemin="0" aria-valuemax="100" style="width: 0%;"></div>' +
                '</div>' +
                '<a href="{3}" target="{4}" data-notify="url"></a>' +
                '</div>'
        }
    }


}
