import { HttpEvent, HttpHandlerFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { HotToastService } from '@ngxpert/hot-toast';
import { mergeMap, of, throwError } from 'rxjs';

export function apiInterceptor(req: HttpRequest<unknown>, next: HttpHandlerFn) {
  const toast = inject(HotToastService);

  if (!req.url.includes('/api/')) {
    return next(req);
  }

  return next(req).pipe(
    mergeMap((event: HttpEvent<any>) => {
      if (event instanceof HttpResponse) {
        const body: any = event.body;
        // Legacy ng-matero demo envelope convention:
        //   failure: { code: <non-zero number>, msg: 'failure' }
        //   success: { code: 0, msg: 'success', data: {} }
        // IMPORTANT: only apply this when `code` is a NUMBER. Real API DTOs use a
        // string `code` (e.g. "ok", "invalid") alongside a `message`/`success` field;
        // treating those as the demo envelope wrongly reports success responses as
        // failures (throwing []), so they must pass straight through to the caller.
        // Skip primitives (e.g. Ok(true) → JSON true). `'code' in true` throws TypeError.
        if (
          body &&
          typeof body === 'object' &&
          !Array.isArray(body) &&
          typeof body.code === 'number' &&
          body.code !== 0
        ) {
          if (body.msg) {
            toast.error(body.msg);
          }
          return throwError(() => []);
        }
      }
      // Pass down event if everything is OK
      return of(event);
    })
  );
}
