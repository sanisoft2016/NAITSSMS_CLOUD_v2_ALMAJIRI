import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { AuthService, User } from '@core';
import { environment } from '@env/environment';
import { BaseComponent, GenericService } from '@shared';

type SchoolInfoVm = {
  ssNo?: number;
  schoolName?: string;
  schoolAddress?: string;
  telephone?: string;
  poAddress?: string;
  emailAddress?: string;
  website?: string;
  schoolLogoString?: string;
  motto?: string;
};

type SchoolRegistrationResponse = {
  proprietorCode?: string;
  schoolInfo?: SchoolInfoVm | Record<string, unknown>;
  logoUrl?: string | null;
};

@Component({
  selector: 'app-profile-overview',
  standalone: true,
  templateUrl: './overview.html',
  styleUrl: './overview.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterLink, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
})
export class ProfileOverview extends BaseComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly api = inject(GenericService<unknown>);
  private readonly apiOrigin = new URL(environment.serverUrl).origin;

  readonly user = toSignal(this.auth.user(), { initialValue: {} as User });
  readonly school = signal<SchoolInfoVm | null>(null);
  readonly logoUrl = signal('');
  readonly registrationId = signal('');
  readonly loadFailed = signal(false);

  readonly displayName = computed(() => {
    const current = this.user();
    return String(current.name ?? current.userName ?? 'Account').trim() || 'Account';
  });

  readonly userName = computed(() => String(this.user().userName ?? '').trim());

  readonly roleLabels = computed(() => {
    const roles = this.user().roles;
    return Array.isArray(roles) ? roles.map(role => String(role)) : [];
  });

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.auth.refreshUser().subscribe({
      next: () => this.cdr.markForCheck(),
    });
    this.loadSchoolProfile();
  }

  refresh(): void {
    this.auth.refreshUser().subscribe({
      next: () => this.cdr.markForCheck(),
    });
    this.loadSchoolProfile();
  }

  private loadSchoolProfile(): void {
    this.loadFailed.set(false);
    this.withRequestState(this.api.getAnObj<SchoolRegistrationResponse>('/Registry/school/registration'), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Failed to load school profile.',
      notifyOnError: 'none',
      rethrow: true,
    }).subscribe({
      next: response => {
        const payload = (response ?? {}) as SchoolRegistrationResponse;
        const school = this.normalizeSchoolInfo(payload.schoolInfo);
        this.school.set(school);
        this.registrationId.set(`${payload.proprietorCode ?? ''}${school.ssNo ?? ''}`);
        this.logoUrl.set(this.resolveLogoUrl(payload.logoUrl, school.schoolLogoString));
        this.loadFailed.set(false);
        this.cdr.markForCheck();
      },
      error: () => {
        this.loadFailed.set(true);
        this.school.set(null);
        this.cdr.markForCheck();
      },
    });
  }

  private normalizeSchoolInfo(value: SchoolInfoVm | Record<string, unknown> | null | undefined): SchoolInfoVm {
    const record = (value ?? {}) as Record<string, unknown>;
    return {
      ssNo: Number(record['ssNo'] ?? record['SsNo'] ?? 0) || undefined,
      schoolName: String(record['schoolName'] ?? record['SchoolName'] ?? '').trim(),
      schoolAddress: String(record['schoolAddress'] ?? record['SchoolAddress'] ?? '').trim(),
      telephone: String(record['telephone'] ?? record['Telephone'] ?? '').trim(),
      poAddress: String(record['poAddress'] ?? record['PoAddress'] ?? '').trim(),
      emailAddress: String(record['emailAddress'] ?? record['EmailAddress'] ?? '').trim(),
      website: String(record['website'] ?? record['Website'] ?? '').trim(),
      schoolLogoString: String(record['schoolLogoString'] ?? record['SchoolLogoString'] ?? '').trim(),
      motto: String(record['motto'] ?? record['Motto'] ?? '').trim(),
    };
  }

  private resolveLogoUrl(logoUrl?: string | null, fileName?: string): string {
    if (logoUrl) {
      return logoUrl.startsWith('http') ? logoUrl : `${this.apiOrigin}${logoUrl}`;
    }

    if (fileName) {
      return `${this.apiOrigin}/school-logos/${fileName}`;
    }

    return '';
  }
}
