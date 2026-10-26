import { Component, ViewEncapsulation, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { AuthService } from '@core/authentication';
import { TranslateModule } from '@ngx-translate/core';

import { StudentPortalContextService } from '../../routes/student/student-portal-context.service';

@Component({
  selector: 'app-user-panel',
  template: `
    <div class="matero-user-panel" routerLink="/profile/overview">
      <img
        class="matero-user-panel-avatar"
        [src]="user()?.avatar || 'images/avatar-default.jpg'"
        alt="avatar"
        width="64" />
      <div class="matero-user-panel-info">
        <h4>{{ displayName() }}</h4>
        <h5>{{ displayEmail() }}</h5>
      </div>
    </div>
  `,
  styleUrl: './user-panel.scss',
  encapsulation: ViewEncapsulation.None,
  imports: [RouterLink, MatButtonModule, MatIconModule, MatTooltipModule, TranslateModule],
})
export class UserPanel {
  private readonly auth = inject(AuthService);
  private readonly portalContext = inject(StudentPortalContextService);

  user = toSignal(this.auth.user());

  /** Prefer the sibling selected in the student portal dropdown when present. */
  readonly displayName = computed(() => {
    const selected = this.portalContext.selectedStudent();
    if (selected?.displayName?.trim()) {
      return selected.displayName.trim();
    }

    const authUser = this.user();
    return authUser?.name || authUser?.['userName'] || 'Account';
  });

  readonly displayEmail = computed(() => {
    const selected = this.portalContext.selectedStudent();
    if (selected?.email?.trim()) {
      return selected.email.trim();
    }

    return this.user()?.email || '—';
  });
}
