import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';

import { BaseComponent, GenericService } from '@shared';

type QuickLinkAccent = 'primary' | 'success' | 'info' | 'warning' | 'danger';
type PageAlertType = 'danger' | 'warning' | 'success' | 'info';

type RegistryQuickLink = {
  id: string;
  title: string;
  description: string;
  /** Material Icons ligature name (used when `faIcon` is not set). */
  icon?: string;
  /** Font Awesome class list, e.g. `fa fa-check-square-o` (legacy Index.cshtml). */
  faIcon?: string;
  accent: QuickLinkAccent;
  route: string;
  smsTile?: boolean;
};

type RegistryIndexBootstrapResponse = {
  canContinue?: boolean;
  message?: string | null;
  Message?: string | null;
  alertType?: PageAlertType | string | null;
  AlertType?: PageAlertType | string | null;
  redirect?: { path?: string } | null;
};

@Component({
  selector: 'app-registry-index',
  imports: [RouterLink, MatIconModule],
  templateUrl: './index.html',
  styleUrl: './index.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Index extends BaseComponent implements OnInit {
  private readonly api = inject(GenericService<RegistryIndexBootstrapResponse>);
  private readonly sanitizer = inject(DomSanitizer);

  /** Legacy _alert.cshtml banner shown above the Index quick links. */
  readonly pageMessageText = signal('');
  readonly pageMessageHtml = signal<SafeHtml | null>(null);
  readonly pageAlertType = signal<PageAlertType>('danger');
  readonly showPageAlert = signal(false);

  /** Legacy Registry/Index.cshtml navigation tiles in original order. */
  readonly quickLinks: RegistryQuickLink[] = [
    {
      id: 'school',
      title: 'School',
      description: 'School Registration',
      icon: 'account_balance',
      accent: 'info',
      route: '/admin/school-registration',
    },
    {
      id: 'class-arm',
      title: 'Class/Arm',
      description: 'Registration / View Information',
      faIcon: 'fa fa-check-square-o',
      accent: 'primary',
      route: '/admin/class-reg-index',
    },
    {
      id: 'student',
      title: 'Student',
      description: 'Registration / View Information',
      icon: 'school',
      accent: 'success',
      route: '/admin/student-home-page',
    },
    {
      id: 'teachers',
      title: 'Teachers / Employees',
      description: 'Registration / Class Assignment and More',
      icon: 'edit',
      accent: 'info',
      route: '/admin/teachers',
    },
    {
      id: 'subjects',
      title: 'Subjects',
      description: 'School Subjects',
      icon: 'menu_book',
      accent: 'warning',
      route: '/admin/subject-index',
    },
    {
      id: 'more',
      title: 'More',
      description: 'Other Registrations',
      icon: 'library_books',
      accent: 'info',
      route: '/admin/other-registrations',
    },
    {
      id: 'results',
      title: 'Results',
      description: 'Students Assessment Results',
      icon: 'edit_note',
      accent: 'danger',
      route: '/admin/student-results',
    },
    {
      id: 'sms',
      title: 'SMS',
      description: 'School SMS',
      icon: 'sms',
      accent: 'info',
      route: '/admin/sms-index',
      smsTile: true,
    },
  ];

  constructor() {
    super();
  }

  ngOnInit(): void {
    // Legacy RegistryController.Index algorithm before rendering the quick-link view.
    this.withRequestState(this.api.getAnObj<RegistryIndexBootstrapResponse>('/Registry/index'), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Failed to verify registry access.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(response => {
      this.applyIndexBootstrap(response);
    });
  }

  dismissPageAlert(): void {
    this.showPageAlert.set(false);
    this.pageMessageText.set('');
    this.pageMessageHtml.set(null);
    this.cdr.markForCheck();
  }

  private applyIndexBootstrap(response: RegistryIndexBootstrapResponse | null | undefined): void {
    const payload = response ?? {};
    const message = String(payload.message ?? payload.Message ?? '').trim();
    const alertType = this.mapPageAlertType(payload.alertType ?? payload.AlertType);

    // Legacy ShowMessage → _alert.cshtml at top of page (not a toast).
    // Skip empty / "1" success token from SecurityService.B().
    if (message && message !== '1') {
      this.pageAlertType.set(alertType);
      this.pageMessageText.set(message);
      this.pageMessageHtml.set(this.sanitizer.bypassSecurityTrustHtml(message));
      this.showPageAlert.set(true);
    } else {
      this.showPageAlert.set(false);
      this.pageMessageText.set('');
      this.pageMessageHtml.set(null);
    }

    if (payload.canContinue === false) {
      const redirectPath = String(payload.redirect?.path ?? '/auth/login').trim() || '/auth/login';
      void this.router.navigateByUrl(redirectPath);
      return;
    }

    this.cdr.markForCheck();
  }

  private mapPageAlertType(alertType: string | null | undefined): PageAlertType {
    switch (String(alertType ?? '').toLowerCase()) {
      case 'success':
        return 'success';
      case 'warning':
        return 'warning';
      case 'info':
        return 'info';
      case 'danger':
      case 'error':
        return 'danger';
      default:
        return 'danger';
    }
  }
}
