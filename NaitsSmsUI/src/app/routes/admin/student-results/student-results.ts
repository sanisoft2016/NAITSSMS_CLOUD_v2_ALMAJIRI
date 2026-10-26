import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpParams } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { finalize, Observable } from 'rxjs';

import { BaseComponent, GenericService } from '@shared';
import { environment } from '@env/environment';

import { StudentResultSheetComponent } from './student-result-sheet.component';
import {
  buildReportSheets,
  ReportSheetViewModel,
  SchoolInfoVm,
} from './student-results-report.builder';
import { printStudentResultSheets } from './student-results-print.util';
import {
  AssessmentRecord,
  ClassOption,
  CollectionScopeMode,
  ReportPayload,
  ResultCategoryMode,
  ResultTypeMode,
  SelectOption,
  ThirdTermResultMode,
  assessmentTotal,
  casColumns,
  normalizeArms,
  normalizeClasses,
  normalizeSelectOptions,
  normalizeSessions,
  normalizeSchoolInfo,
  normalizeTerms,
  sortSessionOptionsDescending,
  parseClassEnum,
  resolveStatusMessage,
  unwrapReportPayload,
} from './student-results.models';

type SchoolRegistrationResponse = {
  schoolInfo?: SchoolInfoVm;
  logoUrl?: string | null;
};

@Component({
  selector: 'app-student-results',
  standalone: true,
  imports: [CommonModule, FormsModule, StudentResultSheetComponent],
  templateUrl: './student-results.html',
  styleUrl: './student-results.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentResults extends BaseComponent implements OnInit {
  private readonly genericService = inject(GenericService);
  private readonly apiOrigin = new URL(environment.serverUrl).origin;
  private requestToken = 0;

  readonly sessions = signal<SelectOption[]>([]);
  readonly terms = signal<SelectOption[]>([]);
  readonly classOptions = signal<ClassOption[]>([]);
  readonly armOptions = signal<SelectOption[]>([]);
  readonly reportSheets = signal<ReportSheetViewModel[]>([]);
  readonly reportPayload = signal<ReportPayload | null>(null);
  readonly reportMessage = signal('');
  readonly showPrintButton = signal(false);
  readonly schoolInfo = signal<SchoolInfoVm | null>(null);
  readonly schoolLogoUrl = signal('');
  /** Stable per page-load stamp so prior immutable 404 image caches are bypassed. */
  private readonly mediaCacheBust = Date.now();

  resultType: ResultTypeMode = '0';
  resultCategory: ResultCategoryMode = '1';
  collectionScope: CollectionScopeMode = '1';
  thirdTermResultType: ThirdTermResultMode = '0';
  studentId = '';
  sessionId = '';
  termStatus = '0';
  classValue = '';
  armValue = '';

  readonly casSetting = computed(() => this.reportPayload()?.casSetting ?? null);
  readonly visibleCasColumns = computed(() => casColumns(this.casSetting()));

  get isIndividual(): boolean {
    return this.resultType === '0';
  }

  get isCollection(): boolean {
    return this.resultType === '1';
  }

  get isYearly(): boolean {
    return this.isIndividual && this.resultCategory === '0';
  }

  get isTermly(): boolean {
    return this.isIndividual ? this.resultCategory === '1' : true;
  }

  get showTermField(): boolean {
    return this.isTermly;
  }

  get showClassField(): boolean {
    return this.isCollection && this.collectionScope !== '0';
  }

  get showArmField(): boolean {
    return this.isCollection && this.collectionScope === '2';
  }

  get showThirdTermType(): boolean {
    const selected = this.terms().find(option => option.value === this.termStatus);
    return (selected?.text ?? '').toLowerCase() === 'third';
  }

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.loadSchoolProfile();
    this.loadSessions();
    this.loadClasses();
  }

  onResultTypeChange(): void {
    this.clearReportContent();
    this.studentId = '';
    this.classValue = '';
    this.armValue = '';
    this.armOptions.set([]);
    this.collectionScope = '1';
    this.thirdTermResultType = '0';
    this.resultCategory = '1';
    this.reloadMenuCategory();
    this.cdr.markForCheck();
  }

  onResultCategoryChange(): void {
    this.clearReportContent();
    if (this.isYearly && this.sessionId && this.sessionId !== '-1') {
      this.tryLoadIndividualYearly();
    }
    this.cdr.markForCheck();
  }

  onSessionChange(sessionId?: string): void {
    if (sessionId !== undefined) {
      this.sessionId = sessionId;
    }

    this.clearReportContent();
    if (!this.sessionId || this.sessionId === '-1') {
      this.terms.set([]);
      this.termStatus = '';
      this.cdr.markForCheck();
      return;
    }

    if (this.isIndividual && this.isYearly) {
      this.tryLoadIndividualYearly();
      return;
    }

    this.reloadTerms();
  }

  onTermChange(termStatus?: string): void {
    if (termStatus !== undefined) {
      this.termStatus = termStatus;
    }

    this.clearReportContent();

    if (this.isIndividual) {
      this.tryLoadIndividualTermly();
      this.cdr.markForCheck();
      return;
    }

    if (this.collectionScope === '0') {
      this.processWholeSchoolResults();
    } else if (this.collectionScope === '1' && this.classValue) {
      this.processClassResults();
    } else if (this.collectionScope === '2' && this.classValue && this.armValue) {
      this.processArmResults();
    }

    this.cdr.markForCheck();
  }

  onCollectionScopeChange(): void {
    this.clearReportContent();
    this.classValue = '';
    this.armValue = '';
    this.armOptions.set([]);

    if (this.collectionScope === '0') {
      this.processWholeSchoolResults();
    }

    this.cdr.markForCheck();
  }

  onClassChange(): void {
    this.clearReportContent();
    this.armValue = '';
    this.armOptions.set([]);

    const selectedClass = this.classOptions().find(option => option.value === this.classValue);
    if (!selectedClass?.classId) {
      return;
    }

    if (this.collectionScope === '2') {
      this.genericService
        .getAnObj(`/Registry/student-results/classes/${encodeURIComponent(selectedClass.classId)}/arms`)
        .subscribe({
          next: response => {
            this.armOptions.set(normalizeArms(this.unwrapResponseBody(response)));
            this.cdr.markForCheck();
          },
          error: () => {
            this.armOptions.set([]);
            this.cdr.markForCheck();
          },
        });
      return;
    }

    if (this.collectionScope === '1') {
      this.processClassResults();
    }
  }

  onArmChange(): void {
    if (this.collectionScope !== '2' || !this.armValue || this.armValue === '-1') {
      return;
    }

    this.clearReportContent();
    this.processArmResults();
  }

  onThirdTermResultTypeChange(): void {
    if (this.collectionScope === '1') {
      this.processClassResults();
      return;
    }

    if (this.collectionScope === '2') {
      this.processArmResults();
    }
  }

  printReport(): void {
    printStudentResultSheets({
      hasSheets: this.reportSheets().length > 0,
      mediaOrigin: this.apiOrigin,
      onWarning: message => this.showAlert(message, { type: 'warning' }),
    });
  }

  subjectTotal(row: AssessmentRecord): number {
    return assessmentTotal(row);
  }

  private loadSessions(): void {
    this.withRequestState(this.getSessionsRequest(), {
      useLoading: true,
      clearError: true,
      rethrow: false,
      errorMessage: 'Failed to load sessions.',
      onError: error => console.error('Failed to load sessions:', error),
    }).subscribe({
      next: response => {
        this.applySessionOptions(response);
        this.cdr.markForCheck();
      },
      error: error => this.errorMessage.set(this.getHttpErrorMessage(error, 'Failed to load sessions.')),
    });
  }

  private loadClasses(): void {
    this.genericService.getAnObj('/Registry/student-results/classes').subscribe({
      next: response => {
        this.classOptions.set(normalizeClasses(this.unwrapResponseBody(response)));
        this.cdr.markForCheck();
      },
      error: () => {
        this.classOptions.set([]);
        this.cdr.markForCheck();
      },
    });
  }

  private reloadMenuCategory(): void {
    this.withRequestState(this.getSessionsRequest(), {
      useLoading: true,
      clearError: true,
      rethrow: false,
      errorMessage: 'Failed to load sessions.',
      onError: error => console.error('Failed to reload sessions:', error),
    }).subscribe({
      next: response => {
        this.applySessionOptions(response);
      },
      error: error => this.errorMessage.set(this.getHttpErrorMessage(error, 'Failed to load sessions.')),
    });

    this.loadClasses();
  }

  private sortSelectOptionsDescending(options: SelectOption[]): SelectOption[] {
    const placeholders = options.filter(option => option.value === '-1');
    const valid = options.filter(option => option.value !== '-1');
    const sorted = [...valid].sort(
      (left, right) => Number(right.value) - Number(left.value) || right.value.localeCompare(left.value),
    );

    return placeholders.length > 0 ? [...placeholders, ...sorted] : sorted;
  }

  private sortTermOptionsAscending(options: SelectOption[]): SelectOption[] {
    const placeholders = options.filter(option => option.value === '-1');
    const valid = options.filter(option => option.value !== '-1');
    const sorted = [...valid].sort(
      (left, right) => Number(left.value) - Number(right.value) || left.value.localeCompare(right.value),
    );

    return placeholders.length > 0 ? [...placeholders, ...sorted] : sorted;
  }

  private applySessionOptions(response: unknown): void {
    const normalized = normalizeSelectOptions(response);
    const sessionOptions = sortSessionOptionsDescending(
      normalized.length > 0 ? normalized : normalizeSessions(response),
    );
    this.sessions.set(sessionOptions);

    const validSessions = sessionOptions.filter(option => option.value !== '-1');
    const selectedSession = validSessions[0]?.value ?? sessionOptions[0]?.value ?? '';

    this.sessionId = selectedSession;

    if (this.sessionId && this.sessionId !== '-1') {
      this.reloadTerms();
    } else {
      this.terms.set([]);
      this.termStatus = '';
      this.cdr.markForCheck();
    }
  }

  compareOptionValues(left: string | null | undefined, right: string | null | undefined): boolean {
    return String(left ?? '') === String(right ?? '');
  }

  private reloadTerms(): void {
    if (!this.sessionId || this.sessionId === '-1') {
      this.terms.set([]);
      this.termStatus = '';
      this.cdr.markForCheck();
      return;
    }

    this.getTermsForSessionRequest(this.sessionId).subscribe({
      next: response => {
        const normalized = normalizeSelectOptions(response);
        const fallback = normalized.length > 0 ? normalized : normalizeTerms(response);
        // Terms must stay chronological (First → Second → Third), not session-style descending.
        const options = this.sortTermOptionsAscending(
          fallback.length > 0 ? fallback : this.defaultTermOptions(),
        );
        this.terms.set(options);
        const firstValid = this.terms().find(option => option.value !== '-1') ?? this.terms()[0];
        this.termStatus = firstValid?.value ?? '';
        if (this.isIndividual && this.isTermly && this.studentId.trim() && this.termStatus && this.termStatus !== '-1') {
          this.tryLoadIndividualTermly();
        }
        this.cdr.markForCheck();
      },
      error: error => {
        console.error('Failed to load terms:', error);
        this.terms.set(this.sortTermOptionsAscending(this.defaultTermOptions()));
        this.cdr.markForCheck();
      },
    });
  }

  tryLoadIndividualTermly(): void {
    if (!this.studentId.trim()) {
      this.showAlert('Sorry! Student Id is required.', { type: 'warning' });
      return;
    }

    if (!this.sessionId || this.sessionId === '-1' || !this.termStatus || this.termStatus === '-1') {
      return;
    }

    this.runReportRequest(
      this.getTermlyAssessmentRequest(this.studentId.trim(), this.sessionId, this.termStatus),
    );
  }

  private tryLoadIndividualYearly(): void {
    if (!this.studentId.trim()) {
      this.showAlert('Sorry! Student Id is required.', { type: 'warning' });
      return;
    }

    if (!this.sessionId || this.sessionId === '-1') {
      return;
    }

    this.runReportRequest(
      this.getYearlyAssessmentRequest(this.studentId.trim(), this.sessionId),
    );
  }

  private processWholeSchoolResults(): void {
    const confirmed = window.confirm(
      "Are you sure you want to process the whole students report sheet? Note: this may take a while to complete. To continue click 'Ok' otherwise click 'Cancel' to terminate.",
    );
    if (!confirmed) {
      return;
    }

    this.runReportRequest(this.processAllResultsRequest());
  }

  private processClassResults(): void {
    if (!this.canRunCollectionRequest() || !this.classValue) {
      return;
    }

    const classNameEnum = parseClassEnum(this.classValue);
    const request$ =
      this.showThirdTermType && this.thirdTermResultType === '1'
        ? this.processClassThirdTermRequest(this.sessionId, this.termStatus, classNameEnum)
        : this.processClassResultRequest(this.sessionId, this.termStatus, classNameEnum);

    this.runReportRequest(request$);
  }

  private processArmResults(): void {
    if (!this.canRunCollectionRequest() || !this.classValue || !this.armValue || this.armValue === '-1') {
      return;
    }

    const classNameEnum = parseClassEnum(this.classValue);
    const request$ =
      this.showThirdTermType && this.thirdTermResultType === '1'
        ? this.processArmThirdTermRequest(classNameEnum, this.armValue)
        : this.processArmResultDetailedRequest(this.sessionId, this.termStatus, classNameEnum, this.armValue);

    this.runReportRequest(request$);
  }

  private canRunCollectionRequest(): boolean {
    return Boolean(this.sessionId && this.sessionId !== '-1' && this.termStatus && this.termStatus !== '-1');
  }

  private runReportRequest(request$: Observable<unknown>): void {
    const token = ++this.requestToken;
    this.isLoading.set(true);
    this.errorMessage.set(null);

    request$
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
          this.applyReportResponse(response as Record<string, unknown>);
        },
        error: error => {
          if (token !== this.requestToken) {
            return;
          }
          this.clearReportContent();
          this.showAlert(this.getHttpErrorMessage(error, 'Unable to load student result.'), { type: 'error' });
        },
      });
  }

  private applyReportResponse(response: Record<string, unknown>): void {
    const payload = unwrapReportPayload(response);
    const statusMessage = resolveStatusMessage(payload.statusCode);

    if (statusMessage && payload.studentsAccessment.length === 0) {
      this.reportMessage.set(statusMessage);
      this.reportPayload.set(null);
      this.reportSheets.set([]);
      this.showPrintButton.set(false);
      this.cdr.markForCheck();
      return;
    }

    if (payload.message && payload.studentsAccessment.length === 0) {
      this.reportMessage.set(payload.message);
      this.reportPayload.set(null);
      this.reportSheets.set([]);
      this.showPrintButton.set(false);
      this.cdr.markForCheck();
      return;
    }

    this.reportPayload.set(payload);
    this.reportSheets.set(this.buildSheets(payload));
    this.reportMessage.set(statusMessage ?? payload.message ?? '');
    this.showPrintButton.set(this.reportSheets().length > 0);
    this.cdr.markForCheck();
  }

  onStudentIdChange(): void {
    this.clearReportContent();
    if (this.isIndividual && this.isYearly) {
      this.tryLoadIndividualYearly();
      return;
    }
    if (this.isIndividual && this.isTermly) {
      this.tryLoadIndividualTermly();
    }
  }

  clearReportContent(): void {
    this.reportPayload.set(null);
    this.reportSheets.set([]);
    this.reportMessage.set('');
    this.showPrintButton.set(false);
  }

  private buildSheets(payload: ReportPayload): ReportSheetViewModel[] {
    const sessionLabel = this.sessions().find(option => option.value === this.sessionId)?.text ?? '';
    const termLabel = this.terms().find(option => option.value === this.termStatus)?.text ?? '';
    const schoolFromPayload = payload.schoolInfo;
    const normalizedSchool =
      normalizeSchoolInfo(this.schoolInfo()) ??
      normalizeSchoolInfo(schoolFromPayload) ??
      null;

    if (normalizedSchool && !this.schoolInfo()) {
      this.schoolInfo.set(normalizedSchool);
      if (!this.schoolLogoUrl()) {
        this.schoolLogoUrl.set(this.resolveLogoUrl(null, normalizedSchool.schoolLogoString));
      }
    }

    return buildReportSheets(payload, {
      sessionLabel,
      termLabel,
      termStatus: this.termStatus,
      schoolInfo: normalizedSchool ?? this.schoolInfo(),
      logoUrl: this.schoolLogoUrl(),
      apiOrigin: this.apiOrigin,
      mediaCacheBust: this.mediaCacheBust,
      filterStudentId: this.isIndividual ? this.studentId.trim() : undefined,
    });
  }

  private loadSchoolProfile(): void {
    this.genericService.getAnObj<SchoolRegistrationResponse>('/Registry/school/registration').subscribe({
      next: response => {
        const payload = (this.unwrapResponseBody(response) as SchoolRegistrationResponse) ?? {};
        const school = normalizeSchoolInfo(payload.schoolInfo) ?? normalizeSchoolInfo(payload);
        if (school) {
          this.schoolInfo.set(school);
        }
        this.schoolLogoUrl.set(this.resolveLogoUrl(payload.logoUrl, school?.schoolLogoString));
        if (this.reportPayload()) {
          this.reportSheets.set(this.buildSheets(this.reportPayload()!));
        }
        this.cdr.markForCheck();
      },
      error: () => {
        this.schoolInfo.set(null);
        this.schoolLogoUrl.set(this.resolveLogoUrl(null, 'NaitsLogo.png'));
        this.cdr.markForCheck();
      },
    });
  }

  private resolveLogoUrl(logoUrl?: string | null, fileName?: string | null): string {
    let resolved: string;
    if (logoUrl) {
      resolved = logoUrl.startsWith('http') ? logoUrl : `${this.apiOrigin}${logoUrl}`;
    } else if (fileName) {
      resolved = `${this.apiOrigin}/school-logos/${encodeURIComponent(fileName)}`;
    } else {
      resolved = `${this.apiOrigin}/school-logos/NaitsLogo.png`;
    }
    // Bust stale browser caches of prior 404s (nginx once served immutable misses).
    const sep = resolved.includes('?') ? '&' : '?';
    return `${resolved}${sep}v=${this.mediaCacheBust}`;
  }

  private defaultTermOptions(): SelectOption[] {
    return [
      { text: 'Select', value: '-1' },
      { text: 'First', value: '0' },
      { text: 'Second', value: '1' },
      { text: 'Third', value: '2' },
    ];
  }

  private unwrapResponseBody(response: unknown): unknown {
    const payload = response as { data?: unknown; body?: unknown };
    return payload.data ?? payload.body ?? response;
  }

  private getSessionsRequest(): Observable<unknown> {
    return this.genericService.getAnObj('/Registry/student-results/sessions');
  }

  private getTermsForSessionRequest(sessionId: string): Observable<unknown> {
    const encodedSessionId = encodeURIComponent(sessionId);
    return this.genericService.getAnObj(`/Registry/student-results/sessions/${encodedSessionId}/terms`);
  }

  private getTermlyAssessmentRequest(
    studentId: string,
    sessionId: string,
    termStatus: string,
  ): Observable<unknown> {
    // Legacy SelectedStudentTermlyReportSheet used query-string studentId — admission numbers
    // contain '/' and break path segments even when encodeURIComponent is used.
    const params = new HttpParams()
      .set('studentId', studentId)
      .set('sessionId', sessionId)
      .set('termStatus', termStatus);
    return this.genericService.getAnObj('/Registry/student-results/termly', { params });
  }

  private getYearlyAssessmentRequest(studentId: string, sessionId: string): Observable<unknown> {
    const params = new HttpParams().set('studentId', studentId).set('selectedSessionId', sessionId);
    return this.genericService.getAnObj('/Registry/student-results/yearly', { params });
  }

  private processAllResultsRequest(): Observable<unknown> {
    return this.genericService.create('/Registry/student-results/process-all', {});
  }

  private processClassResultRequest(
    sessionId: string,
    termStatus: string,
    classNameEnum: string,
  ): Observable<unknown> {
    const params = new HttpParams()
      .set('sessionId', sessionId)
      .set('termStatus', termStatus)
      .set('className', classNameEnum);
    return this.genericService.getAnObj('/Registry/student-results/per-class/cumulative', { params });
  }

  private processClassThirdTermRequest(
    sessionId: string,
    termStatus: string,
    classNameEnum: string,
  ): Observable<unknown> {
    const params = new HttpParams()
      .set('sessionId', sessionId)
      .set('termStatus', termStatus)
      .set('className', classNameEnum);
    return this.genericService.getAnObj('/Registry/student-results/per-class/third-term', { params });
  }

  private processArmResultDetailedRequest(
    sessionId: string,
    termStatus: string,
    classNameEnum: string,
    armNameEnum: string,
  ): Observable<unknown> {
    const params = new HttpParams()
      .set('sessionId', sessionId)
      .set('termStatus', termStatus)
      .set('className', classNameEnum)
      .set('armName', armNameEnum);
    return this.genericService.getAnObj('/Registry/student-results/per-arm/cumulative', { params });
  }

  private processArmThirdTermRequest(classNameEnum: string, armNameEnum: string): Observable<unknown> {
    const params = new HttpParams().set('className', classNameEnum).set('armName', armNameEnum);
    return this.genericService.getAnObj('/Registry/student-results/per-arm/third-term', { params });
  }
}
