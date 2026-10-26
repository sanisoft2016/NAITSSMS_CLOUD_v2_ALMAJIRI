import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { distinctUntilChanged, finalize } from 'rxjs';

import { environment } from '@env/environment';
import { StudentResultSheetComponent } from '../admin/student-results/student-result-sheet.component';
import {
  buildReportSheets,
  ReportSheetViewModel,
  SchoolInfoVm,
} from '../admin/student-results/student-results-report.builder';
import { printStudentResultSheets } from '../admin/student-results/student-results-print.util';
import {
  SelectOption,
  resolveStatusMessage,
  sortSessionOptionsDescending,
  unwrapReportPayload,
} from '../admin/student-results/student-results.models';

import { StudentPortalContextService } from './student-portal-context.service';
import { isStudentAccountDisabled } from './student-portal.models';
import { StudentPortalService } from './student-portal.service';

type ResultCategoryMode = '0' | '1';

@Component({
  selector: 'app-student-portal-results',
  standalone: true,
  imports: [CommonModule, FormsModule, StudentResultSheetComponent],
  templateUrl: './student-results.html',
  styleUrl: './student-results.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentPortalResults implements OnInit {
  private readonly portalService = inject(StudentPortalService);
  private readonly portalContext = inject(StudentPortalContextService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly apiOrigin = new URL(environment.serverUrl).origin;
  private readonly mediaCacheBust = Date.now();
  private requestToken = 0;
  private profileToken = 0;
  private currentStudentFilterId = '';

  readonly sessions = signal<SelectOption[]>([]);
  readonly terms = signal<SelectOption[]>([]);
  readonly reportSheets = signal<ReportSheetViewModel[]>([]);
  readonly reportMessage = signal('');
  readonly showPrintButton = signal(false);
  readonly isLoading = signal(false);
  readonly accountChecked = signal(false);
  readonly accountDisabled = signal(false);
  readonly accountDisabledMessage = 'Sorry! Your account has been disabled.';
  readonly schoolInfo = signal<SchoolInfoVm | null>(null);
  readonly schoolLogoUrl = signal('');

  resultCategory: ResultCategoryMode = '1';
  sessionId = '';
  termStatus = '';

  ngOnInit(): void {
    this.portalContext.ensureLoaded();
    this.portalContext.selectedStudentId$
      .pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.reloadForSelectedStudent());
  }

  get showTermField(): boolean {
    return this.resultCategory === '1';
  }

  compareOptionValues(left: string, right: string): boolean {
    return String(left ?? '') === String(right ?? '');
  }

  onResultCategoryChange(): void {
    this.clearReport();
    this.cdr.markForCheck();
  }

  onSessionChange(): void {
    if (!this.sessionId || this.sessionId === '-1') {
      return;
    }

    if (!this.showTermField) {
      this.loadYearlyReport();
      return;
    }

    this.loadTermsForSession(this.sessionId);
  }

  onTermChange(): void {
    if (!this.sessionId || this.sessionId === '-1' || !this.termStatus || this.termStatus === '-1') {
      return;
    }

    this.loadTermlyReport();
  }

  printReport(): void {
    printStudentResultSheets({
      hasSheets: this.reportSheets().length > 0,
      mediaOrigin: this.apiOrigin,
      onWarning: message => window.alert(message),
    });
  }

  private reloadForSelectedStudent(): void {
    const token = ++this.profileToken;
    this.requestToken += 1;
    this.isLoading.set(false);
    this.accountChecked.set(false);
    this.accountDisabled.set(false);
    this.clearReport();
    this.sessionId = '';
    this.termStatus = '';
    this.terms.set([]);
    this.sessions.set([]);
    this.schoolInfo.set(null);
    this.schoolLogoUrl.set('');

    this.portalService.getProfile().subscribe({
      next: profile => {
        if (token !== this.profileToken) {
          return;
        }

        this.currentStudentFilterId = String(profile.idNo ?? profile.id ?? '').trim();
        if (isStudentAccountDisabled(profile.user?.accessStatus)) {
          this.accountDisabled.set(true);
          this.accountChecked.set(true);
          this.cdr.markForCheck();
          return;
        }

        this.accountChecked.set(true);
        this.loadSchoolHeader();
        this.loadSessions();
        this.cdr.markForCheck();
      },
      error: () => {
        if (token !== this.profileToken) {
          return;
        }

        this.accountChecked.set(true);
        this.reportMessage.set('Unable to confirm this student account. Results are hidden until the account can be verified.');
        this.cdr.markForCheck();
      },
    });
  }

  private loadSchoolHeader(): void {
    this.portalService.getDashboard().subscribe({
      next: school => {
        if (!school) {
          return;
        }

        this.schoolInfo.set({
          schoolName: school.schoolName,
          schoolAddress: school.schoolAddress,
          motto: school.motto,
          schoolLogoString: school.schoolLogoString,
        });

        const logo = school.schoolLogoString?.trim();
        this.schoolLogoUrl.set(
          logo ? `${this.apiOrigin}/school-logos/${encodeURIComponent(logo)}` : '',
        );
        this.cdr.markForCheck();
      },
    });
  }

  private loadSessions(): void {
    if (this.accountDisabled()) {
      return;
    }

    this.isLoading.set(true);
    this.portalService
      .getResultSessions()
      .pipe(
        finalize(() => {
          this.isLoading.set(false);
          this.cdr.markForCheck();
        }),
      )
      .subscribe({
        next: sessions => {
          const sorted = sortSessionOptionsDescending(sessions);
          this.sessions.set(sorted);
          if (sorted.length && !this.sessionId) {
            this.sessionId = sorted[0].value;
            if (this.sessionId !== '-1' && this.showTermField) {
              this.loadTermsForSession(this.sessionId);
            }
          }
          this.cdr.markForCheck();
        },
        error: () => {
          this.reportMessage.set('Unable to load academic sessions.');
          this.cdr.markForCheck();
        },
      });
  }

  private loadTermsForSession(sessionId: string): void {
    this.portalService.getResultTermsForSession(sessionId).subscribe({
      next: terms => {
        this.terms.set(terms);
        const firstValid = terms.find(option => option.value !== '-1') ?? terms[0];
        this.termStatus = firstValid?.value ?? '';
        this.clearReport();
        if (this.termStatus && this.termStatus !== '-1') {
          this.loadTermlyReport();
        }
        this.cdr.markForCheck();
      },
      error: () => {
        this.terms.set([]);
        this.reportMessage.set('Unable to load terms for the selected session.');
        this.cdr.markForCheck();
      },
    });
  }

  private loadTermlyReport(): void {
    if (this.accountDisabled()) {
      return;
    }

    const token = ++this.requestToken;
    this.isLoading.set(true);
    this.portalService
      .getTermlyResult(this.sessionId, this.termStatus)
      .pipe(
        finalize(() => {
          if (token === this.requestToken) {
            this.isLoading.set(false);
            this.cdr.markForCheck();
          }
        }),
      )
      .subscribe({
        next: response => {
          if (token !== this.requestToken) {
            return;
          }
          this.applyReport(response, true);
        },
        error: () => {
          if (token !== this.requestToken) {
            return;
          }
          this.clearReport();
          this.reportMessage.set('Unable to load result. Please try again.');
          this.cdr.markForCheck();
        },
      });
  }

  private loadYearlyReport(): void {
    if (this.accountDisabled()) {
      return;
    }

    const token = ++this.requestToken;
    this.isLoading.set(true);
    this.portalService
      .getYearlyResult(this.sessionId)
      .pipe(
        finalize(() => {
          if (token === this.requestToken) {
            this.isLoading.set(false);
            this.cdr.markForCheck();
          }
        }),
      )
      .subscribe({
        next: response => {
          if (token !== this.requestToken) {
            return;
          }
          this.applyReport(response, false);
        },
        error: () => {
          if (token !== this.requestToken) {
            return;
          }
          this.clearReport();
          this.reportMessage.set('Unable to load yearly result. Please try again.');
          this.cdr.markForCheck();
        },
      });
  }

  private applyReport(response: Record<string, unknown>, isTermly: boolean): void {
    const payload = unwrapReportPayload(response);

    if (isTermly && payload.statusCode !== null && payload.statusCode !== undefined) {
      this.reportMessage.set(
        'Failed! No result is available for the selected SESSION and TERM. Please contact your Form-master.',
      );
      this.reportSheets.set([]);
      this.showPrintButton.set(false);
      this.cdr.markForCheck();
      return;
    }

    const statusMessage = resolveStatusMessage(payload.statusCode);
    if (statusMessage && payload.studentsAccessment.length === 0) {
      this.reportMessage.set(statusMessage);
      this.reportSheets.set([]);
      this.showPrintButton.set(false);
      this.cdr.markForCheck();
      return;
    }

    if (payload.message && payload.message !== 'Success' && payload.studentsAccessment.length === 0) {
      this.reportMessage.set(payload.message);
      this.reportSheets.set([]);
      this.showPrintButton.set(false);
      this.cdr.markForCheck();
      return;
    }

    const sessionLabel = this.sessions().find(option => option.value === this.sessionId)?.text ?? '';
    const termLabelText = this.terms().find(option => option.value === this.termStatus)?.text ?? '';
    const filterStudentId = String(
      response['studentId'] ?? response['StudentId'] ?? this.currentStudentFilterId ?? '',
    ).trim();

    const sheets = buildReportSheets(payload, {
      sessionLabel,
      termLabel: this.showTermField ? termLabelText : 'Yearly',
      termStatus: this.termStatus,
      schoolInfo: this.schoolInfo() ?? payload.schoolInfo ?? null,
      logoUrl: this.schoolLogoUrl(),
      apiOrigin: this.apiOrigin,
      mediaCacheBust: this.mediaCacheBust,
      filterStudentId: filterStudentId || undefined,
    });

    this.reportMessage.set(
      sheets.length ? '' : payload.message || 'No result is available for the selected SESSION and TERM.',
    );
    this.reportSheets.set(sheets);
    this.showPrintButton.set(sheets.length > 0);
    this.cdr.markForCheck();
  }

  private clearReport(): void {
    this.reportSheets.set([]);
    this.reportMessage.set('');
    this.showPrintButton.set(false);
  }
}
