import { Component, ViewEncapsulation, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService, TokenService } from '@core/authentication';
import {
  UserCredential,
  normalizeCredentialRoles,
  resolvePortalHomeUrl,
} from '@shared/models/UserCredential';
import { filter, map, startWith } from 'rxjs';

@Component({
  selector: 'app-auth-layout',
  templateUrl: './auth-layout.html',
  styleUrl: './auth-layout.scss',
  encapsulation: ViewEncapsulation.None,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  host: {
    '[class.auth-bleed]': 'isBleedRoute()',
  },
})
export class AuthLayout {
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly tokenService = inject(TokenService);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map(() => this.router.url),
      startWith(this.router.url)
    ),
    { initialValue: this.router.url }
  );

  readonly isLoggedIn = toSignal(
    this.tokenService.change().pipe(
      map(() => this.auth.check()),
      startWith(this.auth.check())
    ),
    { initialValue: this.auth.check() }
  );

  readonly dashboardUrl = computed(() => {
    if (!this.isLoggedIn()) {
      return '/dashboard';
    }
    return resolvePortalHomeUrl(this.readStoredRoles());
  });

  /** Login fills under the nav so the hero image meets the menu. */
  readonly isBleedRoute = computed(() => {
    const path = (this.url() ?? '').split('?')[0];
    return path === '/auth/login' || path === '/auth' || path.startsWith('/auth/login/');
  });

  private readStoredRoles(): string[] {
    try {
      const raw = localStorage.getItem('credential');
      if (!raw) {
        return [];
      }
      const credential = JSON.parse(raw) as UserCredential;
      return normalizeCredentialRoles(credential.role);
    } catch {
      return [];
    }
  }
}
