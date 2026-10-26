import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, of } from 'rxjs';

import { Menu } from '@core';
import { Token, User } from './interface';

import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class LoginService {
  protected readonly http = inject(HttpClient);

  private get baseUrl(): string {
    return environment.serverUrl;
  }

  login(username: string, password: string, rememberMe = false) {
    return this.http.post<any>(`${this.baseUrl}/user/login`, { username, password, rememberMe });
  }

  loginTwoFactor(twoFactorToken: string, code: string) {
    return this.http.post<any>(`${this.baseUrl}/user/login-2fa`, { twoFactorToken, code });
  }

  refresh(params: Record<string, any>) {
    return this.http.post<Token>(`${this.baseUrl}/user/refresh`, params);
  }

  /** JWT logout is client-side only (clear token). Avoid POST to SPA host (405 on IIS). */
  logout() {
    return of({ status: 'success' });
  }

  user() {
    return this.http.get<any>(`${this.baseUrl}/user`).pipe(
      map(response => {
        const payload = response?.data ?? response?.Data ?? response;
        if (!payload || typeof payload !== 'object') {
          return {} as User;
        }

        const roles = payload.roles ?? payload.Roles ?? [];
        const name = String(payload.name ?? payload.Name ?? '').trim()
          || [payload.firstName ?? payload.FirstName, payload.surName ?? payload.SurName ?? payload.lastName]
            .filter(Boolean)
            .join(' ')
            .trim();
        const userName = String(payload.userName ?? payload.UserName ?? '').trim();
        const rawAvatar = String(payload.avatar ?? payload.Avatar ?? '').trim();

        return {
          id: payload.id ?? payload.Id ?? null,
          name: name || undefined,
          email: payload.email ?? payload.Email ?? undefined,
          userName: userName || undefined,
          avatar: this.resolveAvatarUrl(rawAvatar),
          roles: Array.isArray(roles) ? roles : [],
          twoFactorEnabled: !!(payload.twoFactorEnabled ?? payload.TwoFactorEnabled),
          canManageTwoFactor: !!(payload.canManageTwoFactor ?? payload.CanManageTwoFactor),
        } as User;
      }),
    );
  }

  private resolveAvatarUrl(avatar: string): string {
    if (!avatar) {
      return 'images/avatar-default.jpg';
    }

    if (
      avatar.startsWith('http://') ||
      avatar.startsWith('https://') ||
      avatar.startsWith('data:') ||
      avatar.startsWith('images/')
    ) {
      return avatar;
    }

    const origin = new URL(this.baseUrl).origin;
    return `${origin}${avatar.startsWith('/') ? avatar : `/${avatar}`}`;
  }

  menu() {
    return this.http.get<{ menu: Menu[] }>(`${this.baseUrl}/user/menu`).pipe(map(res => res.menu));
  }
}
