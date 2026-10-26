import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpParams } from '@angular/common/http';

import { BaseComponent, GenericService } from '@shared';
import { environment } from '@env/environment';

import { BroadSheetReportSheetComponent } from './broadsheet-report-sheet.component';
import {
  BroadSheetCollectionMode,
  BroadSheetReport,
  ClassOption,
  SelectOption,
  hasBroadSheetContent,
  normalizeBroadSheetReport,
  normalizeClasses,
  normalizeSelectListItems,
  normalizeSessions,
  normalizeTerms,
  parseClassEnum,
} from './broadsheet-report.models';

@Component({
  selector: 'app-broadsheet-report',
  standalone: true,
  imports: [CommonModule, FormsModule, BroadSheetReportSheetComponent],
  templateUrl: './broadsheet-report.html',
  styleUrl: './broadsheet-report.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BroadsheetReport extends BaseComponent implements OnInit {
  private readonly api = inject(GenericService);
  private readonly apiOrigin = new URL(environment.serverUrl).origin;
  private readonly mediaCacheBust = Date.now();
  private requestToken = 0;

  readonly sessions = signal<SelectOption[]>([]);
  readonly terms = signal<SelectOption[]>([]);
  readonly classOptions = signal<ClassOption[]>([]);
  readonly armOptions = signal<SelectOption[]>([]);
  readonly report = signal<BroadSheetReport | null>(null);
  readonly reportMessage = signal('');
  readonly showPrintButton = signal(false);

  collectionType: BroadSheetCollectionMode = '2';
  sessionId = '';
  termStatus = '0';
  classValue = '';
  armValue = '';

  readonly logoUrl = computed(() => this.resolveLogoUrl(this.report()?.schoolInfo?.schoolLogoString));

  readonly hasReport = computed(() => hasBroadSheetContent(this.report()));

  get showClassField(): boolean {
    return this.collectionType === '1' || this.collectionType === '2';
  }

  get showArmField(): boolean {
    return this.collectionType === '2';
  }

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.loadSessions();
    this.loadClasses();
    this.loadAllArms();
  }

  compareOptionValues(left: string | number, right: string | number): boolean {
    return String(left) === String(right);
  }

  onCollectionTypeChange(): void {
    this.clearReport();
    this.armValue = '';

    if (this.collectionType === '1' && this.classValue) {
      this.loadClassReport();
    }

    this.cdr.markForCheck();
  }

  onSessionChange(): void {
    this.clearReport();
    if (!this.sessionId || this.sessionId === '-1') {
      this.terms.set([]);
      this.termStatus = '';
      this.cdr.markForCheck();
      return;
    }

    this.loadTerms();
  }

  onTermChange(): void {
    this.clearReport();
    this.cdr.markForCheck();
  }

  onClassChange(): void {
    this.clearReport();

    if (!this.classValue) {
      this.cdr.markForCheck();
      return;
    }

    if (this.collectionType === '1') {
      this.loadClassReport();
      return;
    }

    if (this.armValue && this.armValue !== '-1') {
      this.loadArmReport();
    }

    this.cdr.markForCheck();
  }

  onArmChange(): void {
    if (this.collectionType !== '2' || !this.armValue || this.armValue === '-1' || !this.classValue) {
      return;
    }

    this.clearReport();
    this.loadArmReport();
  }

  printReport(): void {
    const content = document.getElementById('pageContent');
    if (!content?.innerHTML.trim()) {
      this.showAlert('No report content to print.', { type: 'warning' });
      return;
    }

    const printWindow = window.open('', '_blank', 'width=1024,height=768');
    if (!printWindow) {
      this.showAlert('Unable to open print window. Please allow pop-ups and try again.', { type: 'warning' });
      return;
    }

    printWindow.document.open();
    printWindow.document.write(`<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <title>Broad Sheet Report</title>
  <style>${this.printStyles()}</style>
</head>
<body><div id="pageContent">${content.innerHTML}</div></body>
</html>`);
    printWindow.document.close();

    const triggerPrint = () => {
      printWindow.focus();
      printWindow.print();
      printWindow.onafterprint = () => printWindow.close();
    };

    const waitForImages = () => {
      const images = Array.from(printWindow.document.images);
      if (images.length === 0) {
        triggerPrint();
        return;
      }

      let pending = images.length;
      const done = () => {
        pending -= 1;
        if (pending <= 0) {
          triggerPrint();
        }
      };

      for (const image of images) {
        if (image.complete) {
          done();
        } else {
          image.addEventListener('load', done, { once: true });
          image.addEventListener('error', done, { once: true });
        }
      }
    };

    if (printWindow.document.readyState === 'complete') {
      waitForImages();
    } else {
      printWindow.addEventListener('load', waitForImages, { once: true });
    }
  }

  private loadSessions(): void {
    this.withRequestState(this.api.get('/Registry/student-results/sessions'), {
      useLoading: false,
      clearError: true,
      errorMessage: 'Failed to load sessions.',
      notifyOnError: 'none',
      rethrow: true,
    }).subscribe({
      next: response => {
        const options = normalizeSessions(this.unwrapResponseBody(response));
        this.sessions.set(options);
        const latest = options.find(option => option.value !== '-1') ?? options[0];
        this.sessionId = latest?.value ?? '';
        if (this.sessionId) {
          this.loadTerms(false);
        }
        this.cdr.markForCheck();
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to load sessions.'), { type: 'error' });
      },
    });
  }

  private loadTerms(clearReport = true): void {
    if (clearReport) {
      this.clearReport();
    }

    this.withRequestState(
      this.api.get(`/Registry/student-results/sessions/${encodeURIComponent(this.sessionId)}/terms`),
      {
        useLoading: false,
        clearError: true,
        errorMessage: 'Failed to load terms.',
        notifyOnError: 'none',
        rethrow: true,
      }
    ).subscribe({
      next: response => {
        const options = normalizeTerms(this.unwrapResponseBody(response));
        this.terms.set(options);
        const latest = options.find(option => option.value !== '-1') ?? options[0];
        this.termStatus = latest?.value ?? '0';
        this.cdr.markForCheck();
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to load terms.'), { type: 'error' });
      },
    });
  }

  private loadAllArms(): void {
    this.withRequestState(this.api.get('/Registry/broadsheet-report/arms'), {
      useLoading: false,
      clearError: false,
      notifyOnError: 'none',
      rethrow: true,
    }).subscribe({
      next: response => {
        this.armOptions.set(normalizeSelectListItems(this.unwrapResponseBody(response)));
        this.cdr.markForCheck();
      },
      error: () => {
        this.armOptions.set([]);
        this.cdr.markForCheck();
      },
    });
  }

  private loadClasses(): void {
    this.withRequestState(this.api.getAnObj('/Registry/student-results/classes'), {
      useLoading: false,
      clearError: false,
      notifyOnError: 'none',
      rethrow: true,
    }).subscribe({
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

  private loadClassReport(): void {
    const classEnum = parseClassEnum(this.classValue);
    if (!classEnum || !this.sessionId || this.termStatus === '') {
      return;
    }

    const params = new HttpParams()
      .set('sessionId', this.sessionId)
      .set('termStatus', this.termStatus)
      .set('className', String(classEnum));

    this.fetchReport(`/Registry/broadsheet-report/class?${params.toString()}`);
  }

  private loadArmReport(): void {
    const classEnum = parseClassEnum(this.classValue);
    if (!classEnum || !this.sessionId || this.termStatus === '' || !this.armValue) {
      return;
    }

    const params = new HttpParams()
      .set('sessionId', this.sessionId)
      .set('termStatus', this.termStatus)
      .set('className', String(classEnum))
      .set('armName', this.armValue);

    this.fetchReport(`/Registry/broadsheet-report/arm?${params.toString()}`);
  }

  private fetchReport(url: string): void {
    const token = ++this.requestToken;

    this.withRequestState(this.api.getAnObj(url), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Failed to load broadsheet report.',
      notifyOnError: 'none',
      rethrow: true,
    }).subscribe({
      next: response => {
        if (token !== this.requestToken) {
          return;
        }

        const normalized = normalizeBroadSheetReport(this.unwrapResponseBody(response));
        this.report.set(normalized);
        this.reportMessage.set(normalized.message);
        this.showPrintButton.set(hasBroadSheetContent(normalized));
        this.cdr.markForCheck();
      },
      error: error => {
        if (token !== this.requestToken) {
          return;
        }

        this.report.set(null);
        this.showPrintButton.set(false);
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to load broadsheet report.'), { type: 'error' });
        this.cdr.markForCheck();
      },
    });
  }

  private clearReport(): void {
    this.requestToken += 1;
    this.report.set(null);
    this.reportMessage.set('');
    this.showPrintButton.set(false);
  }

  private unwrapResponseBody(response: unknown): unknown {
    if (Array.isArray(response)) {
      return response;
    }

    const record = response && typeof response === 'object'
      ? response as Record<string, unknown>
      : null;

    if (!record) {
      return response;
    }

    if ('data' in record) {
      return record['data'];
    }

    return response;
  }

  private resolveLogoUrl(fileName?: string | null): string {
    const logo = fileName?.trim();
    if (!logo) {
      return '';
    }
    let resolved: string;
    if (/^https?:\/\//i.test(logo) || logo.startsWith('data:')) {
      resolved = logo;
    } else if (logo.startsWith('/')) {
      resolved = `${this.apiOrigin}${logo}`;
    } else {
      resolved = `${this.apiOrigin}/school-logos/${encodeURIComponent(logo)}`;
    }
    if (resolved.startsWith('data:')) {
      return resolved;
    }
    const sep = resolved.includes('?') ? '&' : '?';
    return `${resolved}${sep}v=${this.mediaCacheBust}`;
  }

  private printStyles(): string {
    return `
      @page { margin: 10mm; }
      html, body { margin: 0; padding: 0; }
      body { font-family: Arial, Helvetica, sans-serif; color: #111; background: #fff; }
      #pageContent, .page-content { width: 100%; margin: 0; padding: 0; background: #fff; }
      .broadsheet-report { padding: 0; background: #fff; }
      .broadsheet-report__header, .broadsheet-report__table { width: 100%; border-collapse: collapse; }
      .broadsheet-report__header-row { height: 10em; }
      .broadsheet-report__spacer { width: 5% !important; }
      .broadsheet-report__logo-cell { width: 16% !important; border: 0 !important; text-align: right; }
      .broadsheet-report__logo { width: 9em; height: 9em; object-fit: contain; }
      .broadsheet-report__title-cell { width: 64% !important; border: 0 !important; text-align: center; padding-top: 1em; font-size: 12px !important; }
      .broadsheet-report__school-name { display: inline-block; width: 100%; font-weight: 800; font-size: large; letter-spacing: 4px; text-align: center; text-transform: uppercase; }
      .broadsheet-report__motto { text-transform: uppercase; letter-spacing: 2px; color: #00f; font-weight: 700; }
      .broadsheet-report__report-title { font-weight: 700; }
      .broadsheet-report__upper { text-transform: uppercase; }
      .broadsheet-report__header { page-break-after: avoid; break-after: avoid-page; }
      .broadsheet-report__arm { padding-top: 1em; overflow: visible; page-break-inside: auto; break-inside: auto; }
      .broadsheet-report__arm:first-of-type { page-break-before: avoid; break-before: avoid-page; padding-top: 0.5em; }
      .broadsheet-report__arm + .broadsheet-report__arm { page-break-before: always; break-before: page; }
      .broadsheet-report__arm-title { text-align: left; }
      .broadsheet-report__table { table-layout: auto; width: max-content; min-width: 100%; margin-bottom: 2em; page-break-inside: auto; break-inside: auto; }
      .mytable-head th, .mytable-body td, .mytable-body th { border: 1px solid #ddd; padding: 2px 4px; vertical-align: middle; background: #fff; }
      .mytable-body { page-break-inside: avoid; break-inside: avoid-page; }
      .broadsheet-report__header th, .broadsheet-report__header td { background: #fff; }
      .broadsheet-report__serial-col, .broadsheet-report__serial-cell { width: 1%; min-width: 2.5em; white-space: nowrap; text-align: center; }
      .broadsheet-report__admission-col, .broadsheet-report__admission-cell { width: 1%; min-width: 11em; white-space: nowrap; text-align: left; }
      .broadsheet-report__name-col, .broadsheet-report__name-cell { width: auto; min-width: 14em; white-space: nowrap; text-align: left; overflow: visible; text-overflow: clip; }
      .broadsheet-report__score-cell { text-align: center; }
      .broadsheet-report__fail { color: red; }
      .broadsheet-report .rotate { vertical-align: bottom; height: 140px; width: 1%; min-width: 2.4em; max-width: 3.2em; padding: 0; overflow: hidden; text-align: center; }
      .broadsheet-report .rotate > div { writing-mode: vertical-rl; transform: rotate(180deg); display: flex; align-items: flex-end; justify-content: center; height: 100%; width: 100%; }
      .broadsheet-report .rotate > div > span { font-size: 12px; letter-spacing: 2px; white-space: normal; word-break: break-word; display: block; }
      .broadsheet-report__summary th { text-align: left; font-weight: 700; }
    `;
  }
}
