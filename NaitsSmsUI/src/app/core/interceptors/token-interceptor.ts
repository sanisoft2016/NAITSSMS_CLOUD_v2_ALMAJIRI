import { HttpErrorResponse, HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { TokenService } from '@core/authentication';
import { environment } from '@env/environment';
import { catchError, throwError } from 'rxjs';
import { BASE_URL, hasHttpScheme } from './base-url-interceptor';

export function tokenInterceptor(req: HttpRequest<unknown>, next: HttpHandlerFn) {
  const baseUrl = inject(BASE_URL, { optional: true });
  const tokenService = inject(TokenService);

  const includeBaseUrl = (url: string) => {
    if (!baseUrl) {
      return false;
    }
    return new RegExp(`^${baseUrl.replace(/\/$/, '')}`, 'i').test(url);
  };

  const includeServerUrl = (url: string) => {
    const serverUrl = environment.serverUrl?.replace(/\/$/, '');
    return serverUrl ? url.toLowerCase().startsWith(serverUrl.toLowerCase()) : false;
  };

  const shouldAppendToken = (url: string) =>
    !hasHttpScheme(url) || includeBaseUrl(url) || includeServerUrl(url);

  if (tokenService.valid() && shouldAppendToken(req.url)) {
    return next(
      req.clone({
        headers: req.headers.set('Authorization', tokenService.getBearerToken()),
        withCredentials: true,
      })
    ).pipe(
      catchError((error: HttpErrorResponse) => {
        // Only clear the stored JWT on a bare auth challenge (expired/invalid token).
        // Controller-returned 401 bodies with { message } must not force logout mid-page.
        if (error.status === 401) {
          const body = error.error;
          const hasApiMessage =
            body &&
            typeof body === 'object' &&
            !Array.isArray(body) &&
            typeof (body as { message?: unknown }).message === 'string';
          if (!hasApiMessage) {
            tokenService.clear();
          }
        }
        return throwError(() => error);
      })
    );
  }

  return next(req);
}
