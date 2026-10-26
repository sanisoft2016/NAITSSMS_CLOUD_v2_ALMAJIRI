import { HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { environment } from '@env/environment';

/**
 * Prevent browsers from serving stale GET responses after a full-page refresh.
 * Nginx has previously added long-lived Cache-Control on /api; request headers +
 * a unique query stamp force a network round-trip for dynamic API data.
 */
export function noCacheInterceptor(req: HttpRequest<unknown>, next: HttpHandlerFn) {
  if (req.method !== 'GET' || !isApiRequest(req.url)) {
    return next(req);
  }

  return next(
    req.clone({
      setHeaders: {
        'Cache-Control': 'no-cache, no-store, must-revalidate',
        Pragma: 'no-cache',
        Expires: '0',
      },
      setParams: {
        _: String(Date.now()),
      },
    }),
  );
}

function isApiRequest(url: string): boolean {
  const serverUrl = environment.serverUrl?.replace(/\/$/, '') ?? '';
  if (serverUrl && url.toLowerCase().startsWith(serverUrl.toLowerCase())) {
    return true;
  }

  try {
    const path = new URL(url, environment.baseURL).pathname.toLowerCase();
    return path.startsWith('/api/') || path === '/api' || path.startsWith('/home/');
  } catch {
    return url.includes('/api/') || url.includes('/home/');
  }
}
