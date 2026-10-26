import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { Router, RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

import { AuthService, User } from '@core';
import { ChangePassword } from '../../routes/profile/change-password/change-password';
import { TwoFactorSetup } from '../../routes/profile/two-factor-setup/two-factor-setup';

@Component({
  selector: 'app-user',
  template: `
    <button matIconButton [matMenuTriggerFor]="menu">
      <img
        class="avatar"
        [src]="user().avatar || 'images/avatar-default.jpg'"
        width="24"
        alt="avatar" />
    </button>

    <mat-menu #menu="matMenu">
      <button routerLink="/profile/overview" mat-menu-item>
        <mat-icon>account_circle</mat-icon>
        <span>{{ 'profile' | translate }}</span>
      </button>
      <button routerLink="/profile/change-profile-image" mat-menu-item>
        <mat-icon>add_a_photo</mat-icon>
        <span>{{ 'change_profile_image' | translate }}</span>
      </button>
      <button mat-menu-item (click)="openChangePassword()">
        <mat-icon>lock</mat-icon>
        <span>{{ 'change_password' | translate }}</span>
      </button>
      @if (canManageTwoFactor()) {
        <button mat-menu-item (click)="openTwoFactorSetup()">
          <mat-icon>phonelink_lock</mat-icon>
          <span>{{ 'two_factor_optional' | translate }}</span>
        </button>
      }
      <button mat-menu-item (click)="logout()">
        <mat-icon>exit_to_app</mat-icon>
        <span>{{ 'logout' | translate }}</span>
      </button>
    </mat-menu>
  `,
  styles: `
    .avatar {
      width: 1.5rem;
      height: 1.5rem;
      border-radius: 50rem;
      object-fit: cover;
    }
  `,
  imports: [
    RouterLink,
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatMenuModule,
    TranslateModule,
  ],
})
export class UserButton {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private static readonly adminTwoFactorRoles = new Set([
    'accounting',
    'accountant',
    'registry',
    'remoteagents',
    'saofficer',
    'headadmin',
    'admin',
  ]);

  user = toSignal(this.auth.user(), { initialValue: {} as User });

  canManageTwoFactor(): boolean {
    const current = this.user();
    if (current?.canManageTwoFactor === true) {
      return true;
    }

    const roles = Array.isArray(current?.roles) ? current.roles : [];
    return roles.some(
      (role: unknown) =>
        UserButton.adminTwoFactorRoles.has(String(role ?? '').toLowerCase()),
    );
  }

  openChangePassword(): void {
    this.dialog.open(ChangePassword, {
      width: '420px',
      maxWidth: '95vw',
      autoFocus: 'first-tabbable',
      disableClose: false,
    });
  }

  openTwoFactorSetup(): void {
    this.dialog.open(TwoFactorSetup, {
      width: '480px',
      maxWidth: '95vw',
      autoFocus: 'first-tabbable',
      disableClose: false,
    });
  }

  logout() {
    this.auth.logout().subscribe(() => {
      this.router.navigateByUrl('/auth/login');
    });
  }
}
