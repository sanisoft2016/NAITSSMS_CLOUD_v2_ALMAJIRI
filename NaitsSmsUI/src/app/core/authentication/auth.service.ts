import { Injectable, inject } from '@angular/core';
import {
  BehaviorSubject,
  catchError,
  firstValueFrom,
  iif,
  map,
  merge,
  of,
  share,
  switchMap,
  tap,
  timeout,
} from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { filterObject, isEmptyObject } from './helpers';
import { User } from './interface';
import { LoginService } from './login.service';
import { TokenService } from './token.service';
import { UserCredential } from '@shared/models/UserCredential';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly loginService = inject(LoginService);
  private readonly tokenService = inject(TokenService);

  private user$ = new BehaviorSubject<User>({});
  private change$ = merge(
    this.tokenService.change(),
    this.tokenService.refresh().pipe(switchMap(() => this.refresh()))
  ).pipe(
    switchMap(() => this.assignUser()),
    share()
  );

  init() {
    return new Promise<void>(resolve => this.change$.subscribe(() => resolve()));
  }

  change() {
    return this.change$;
  }

  check() {
    return this.tokenService.valid();
  }

  /** Observable version kept for internal/backwards usage. */
  login$(username: string, password: string, rememberMe = false) {
    // Return the actual HTTP response so the component can check response.status
    return this.loginService.login(username, password, rememberMe).pipe(
      tap(response => this.applyLoginToken(response))
    );
  }

  login(username: string, password: string, rememberMe = false) {
    return firstValueFrom(this.login$(username, password, rememberMe));
  }

  loginTwoFactor(twoFactorToken: string, code: string) {
    return firstValueFrom(
      this.loginService.loginTwoFactor(twoFactorToken, code).pipe(
        tap(response => this.applyLoginToken(response))
      )
    );
  }

  private applyLoginToken(response: any): void {
    const status = String(response?.status ?? response?.Status ?? '').toLowerCase();
    const data = response?.data ?? response?.Data;
    if (status !== 'success' || !data?.token) {
      return;
    }

    const tokenData =
      typeof data.token === 'string'
        ? { access_token: data.token, expires_in: 3600 }
        : {
            access_token: data.token.accessToken ?? data.token.access_token,
            expires_in: data.token.expiresIn ?? data.token.expires_in ?? 3600,
            exp: data.token.exp,
          };
    this.tokenService.set(tokenData);
  }


    setUserCredential(credential: UserCredential) {
        return new Promise((resolve) => {
            localStorage.setItem("credential", JSON.stringify(credential));
            resolve(true);
        });
    }

  refresh() {
    const refreshToken = this.tokenService.getRefreshToken();
    if (!refreshToken) {
      return of(this.check());
    }

    return this.loginService
      .refresh(filterObject({ refresh_token: refreshToken }))
      .pipe(
        catchError(() => of(undefined)),
        tap(token => this.tokenService.set(token)),
        map(() => this.check())
      );
  }

  logout() {
    return this.loginService.logout().pipe(
      catchError(() => of(null)),
      tap(() => {
        this.tokenService.clear();
        localStorage.removeItem('credential');
        try {
          sessionStorage.removeItem('naits.studentPortal.selectedStudentId');
        } catch {
          // ignore
        }
      }),
      map(() => !this.check())
    );
  }

  user() {
    return this.user$.pipe(share());
  }

  menu() {
    return iif(() => this.check(), this.loginService.menu(), of([]));
  }

  /** Force-refresh the cached user profile from the API. */
  refreshUser() {
    if (!this.check()) {
      this.user$.next({});
      return of({} as User);
    }

    return this.loginService.user().pipe(
      timeout({ first: 4000 }),
      catchError((error: HttpErrorResponse | Error) => {
        console.warn('Failed to fetch user info:', (error as HttpErrorResponse).status ?? error.message);
        return of(this.withCredentialFallbacks({} as User));
      }),
      map(user => this.withCredentialFallbacks(user)),
      tap(user => this.user$.next(user)),
    );
  }

  private assignUser() {
    if (!this.check()) {
      return of({}).pipe(tap(user => this.user$.next(user)));
    }

    const cached = this.user$.getValue();
    if (!isEmptyObject(cached) && cached.userName) {
      return of(cached);
    }

    return this.refreshUser();
  }

  private withCredentialFallbacks(user: User): User {
    const credential = this.readCredential();
    const tokenUserName = this.readUserNameFromToken();
    const userName = String(user.userName ?? credential?.userName ?? tokenUserName ?? '').trim();

    return {
      ...user,
      userName: userName || user.userName,
      name: user.name || credential?.userFullName || userName || user.name,
    };
  }

  private readCredential(): UserCredential | null {
    try {
      const raw = localStorage.getItem('credential');
      return raw ? (JSON.parse(raw) as UserCredential) : null;
    } catch {
      return null;
    }
  }

  private readUserNameFromToken(): string {
    const bearer = this.tokenService.getBearerToken();
    const token = bearer.replace(/^Bearer\s+/i, '').trim();
    if (!token || token.split('.').length < 2) {
      return '';
    }

    try {
      const payloadJson = atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'));
      const payload = JSON.parse(payloadJson) as Record<string, unknown>;
      return String(
        payload['unique_name']
          ?? payload['name']
          ?? payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name']
          ?? '',
      ).trim();
    } catch {
      return '';
    }
  }
}
