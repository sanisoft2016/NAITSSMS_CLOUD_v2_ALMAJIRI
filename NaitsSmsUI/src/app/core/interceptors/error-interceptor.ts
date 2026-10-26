import { HttpErrorResponse, HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { HotToastService } from '@ngxpert/hot-toast';
import { catchError, throwError } from 'rxjs';

export enum STATUS {
  UNAUTHORIZED = 401,
  FORBIDDEN = 403,
  NOT_FOUND = 404,
  INTERNAL_SERVER_ERROR = 500,
}

export function errorInterceptor(req: HttpRequest<unknown>, next: HttpHandlerFn) {
  const router = inject(Router);
  const toast = inject(HotToastService);
  const errorPages = [STATUS.FORBIDDEN, STATUS.NOT_FOUND, STATUS.INTERNAL_SERVER_ERROR];

  const isApiRequest = (() => {
    // In this app, API calls are typically proxied under `/api/*` in dev.
    // Avoid redirecting the entire SPA to the 4xx/5xx routes for API failures.
    try {
      const url = new URL(req.url, window.location.origin);
      return url.pathname.startsWith('/api/');
    } catch {
      return req.url.startsWith('/api/');
    }
  })();

  const getMessage = (error: HttpErrorResponse) => {
    if (error.status === 0) {
      return 'Network error: cannot reach the server. Check API is running and dev proxy/CORS settings.';
    }
    if (error.error?.detail) {
      console.error('[API error detail]', error.error.detail);
    }
    if (error.error?.message) {
      return error.error.detail
        ? `${error.error.message} (${error.error.detail})`
        : error.error.message;
    }
    if (error.error?.msg) {
      return error.error.msg;
    }
    return `${error.status} ${error.statusText}`;
  };

  const isOptionalAuthRequest = (() => {
    try {
      const url = new URL(req.url, window.location.origin);
      return url.pathname.endsWith('/user/refresh');
    } catch {
      return req.url.includes('/user/refresh');
    }
  })();

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === STATUS.UNAUTHORIZED) {
        console.warn(`[401] ${req.method} ${req.url}`, {
          status: error.status,
          statusText: error.statusText,
          message: getMessage(error),
        });

        const body = error.error;
        const hasApiMessage =
          body &&
          typeof body === 'object' &&
          !Array.isArray(body) &&
          typeof (body as { message?: unknown }).message === 'string';

        // Bare JWT challenges (expired/missing token) → login.
        // Controller 401 bodies with { message } stay on the page.
        if (!hasApiMessage) {
          router.navigateByUrl('/auth/login');
        }
      }

      if (error.status === 0) {
        console.warn(`[HTTP 0] ${req.method} ${req.url} — API unreachable. Check that the API is running and CORS allows this origin.`);
        return throwError(() => error);
      }

      if (error.status === STATUS.NOT_FOUND && isOptionalAuthRequest) {
        console.warn(`[HTTP 404] ${req.method} ${req.url} — refresh endpoint is not implemented on the API.`);
        return throwError(() => error);
      }

      // Important: Don't navigate the whole SPA to an error route because an API call failed.
      // It makes valid pages (like /admin/class-reg-index) appear as "404 Not Found".
      console.error(`[HTTP ${error.status}] ${req.method} ${req.url}`, error);
      toast.error(getMessage(error));

      return throwError(() => error);
    })
  );
}
