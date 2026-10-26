import { DatePipe, DecimalPipe } from '@angular/common';
import { Overlay, OverlayRef } from '@angular/cdk/overlay';
import { TemplatePortal } from '@angular/cdk/portal';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  NgZone,
  OnDestroy,
  OnInit,
  TemplateRef,
  ViewContainerRef,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  DefaultMatCalendarRangeStrategy,
  MAT_DATE_RANGE_SELECTION_STRATEGY,
  MatDatepickerModule,
} from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { HttpParams } from '@angular/common/http';
import type { ApexOptions } from 'apexcharts';

import { BaseComponent, GenericService } from '@shared';

import {
  CommissionClassArmAttendanceStatus,
  CommissionCentreDirectoryRow,
  CommissionCentresDirectory,
  CommissionCentreRow,
  CommissionDefaultedCentre,
  CommissionLeaver,
  CommissionOversight,
  LeaverOutcome,
} from './commission-dashboard.models';

type AttendanceView = 'overview' | 'areas' | 'defaults' | 'class-arms' | 'trends';

type ApexChartInstance = {
  render: () => Promise<void>;
  updateOptions: (options: ApexOptions, redrawPaths?: boolean, animate?: boolean) => Promise<void>;
  destroy: () => void;
};

@Component({
  selector: 'app-commission-dashboard',
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  providers: [
    {
      provide: MAT_DATE_RANGE_SELECTION_STRATEGY,
      useClass: DefaultMatCalendarRangeStrategy,
    },
  ],
  templateUrl: './commission-dashboard.html',
  styleUrl: './commission-dashboard.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CommissionDashboard
  extends BaseComponent
  implements OnInit, AfterViewInit, OnDestroy
{
  private readonly api = inject(GenericService<CommissionCentresDirectory>);
  private readonly ngZone = inject(NgZone);
  private readonly overlay = inject(Overlay);
  private readonly viewContainerRef = inject(ViewContainerRef);

  private readonly trendHost = viewChild<ElementRef<HTMLDivElement>>('trendHost');
  private readonly enrolmentHost = viewChild<ElementRef<HTMLDivElement>>('enrolmentHost');
  private readonly defaultedCentreModal = viewChild<TemplateRef<unknown>>('defaultedCentreModal');

  private defaultedCentreOverlay: OverlayRef | null = null;

  private trendChart: ApexChartInstance | null = null;
  private enrolmentChart: ApexChartInstance | null = null;
  private viewReady = false;

  readonly directory = signal<CommissionCentresDirectory | null>(null);
  readonly oversight = signal<CommissionOversight | null>(null);
  readonly loadFailed = signal(false);
  readonly attendanceLoading = signal(false);
  readonly attendanceFailed = signal(false);
  readonly atRiskDetailsOpen = signal(false);
  readonly leaversOpen = signal(false);
  readonly leavers = signal<CommissionLeaver[] | null>(null);
  readonly leaversLoading = signal(false);
  readonly leaversFilter = signal<LeaverOutcome | 'all'>('all');
  readonly filteredLeavers = computed(() => {
    const filter = this.leaversFilter();
    const all = this.leavers() ?? [];
    return filter === 'all' ? all : all.filter(leaver => leaver.outcome === filter);
  });
  readonly attendanceView = signal<AttendanceView>('overview');
  readonly selectedDefaultedCentre = signal<CommissionDefaultedCentre | null>(null);
  readonly selectedMissingDatesArm = signal<CommissionClassArmAttendanceStatus | null>(null);

  /** Defaulted class arms grouped by centre for the Class/Arm Status tab. */
  readonly defaultedClassArmsByCentre = computed(() => {
    const oversight = this.oversight();
    if (!oversight) {
      return [] as CommissionDefaultedCentre[];
    }

    if ((oversight.defaultedCentres ?? []).length) {
      return oversight.defaultedCentres ?? [];
    }

    const defaulted = (oversight.classArmStatuses ?? []).filter(
      status => (status.missingDates?.length ?? 0) > 0
    );

    const byCentre = new Map<string, CommissionDefaultedCentre>();
    for (const status of defaulted) {
      const key = status.centreId || status.centreName || 'unknown';
      let group = byCentre.get(key);
      if (!group) {
        group = {
          centreId: status.centreId,
          centreName: status.centreName,
          missingSubmissionCount: 0,
          affectedClassArms: 0,
          classArms: [],
        };
        byCentre.set(key, group);
      }
      group.classArms.push(status);
      group.missingSubmissionCount += status.missingDates.length;
      group.affectedClassArms += 1;
    }

    return [...byCentre.values()].sort((a, b) =>
      (a.centreName || '').localeCompare(b.centreName || '', undefined, { sensitivity: 'base' })
    );
  });

  readonly maxDate = this.startOfDay(new Date());
  rangeStart: Date | null = this.addDays(this.maxDate, -29);
  rangeEnd: Date | null = this.maxDate;

  centreInitials(name: string | null | undefined): string {
    const parts = (name || 'Centre')
      .trim()
      .split(/\s+/)
      .filter(Boolean);
    if (parts.length === 0) {
      return 'LC';
    }
    if (parts.length === 1) {
      return parts[0].slice(0, 2).toUpperCase();
    }
    return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  }

  constructor() {
    super();

    effect(() => {
      const data = this.oversight();
      const loading = this.attendanceLoading();
      const activeView = this.attendanceView();

      if (loading || activeView !== 'trends') {
        this.destroyCharts();
        return;
      }

      if (!this.viewReady || !data) {
        return;
      }

      this.ngZone.runOutsideAngular(() => {
        requestAnimationFrame(() => {
          requestAnimationFrame(() => void this.renderCharts(data));
        });
      });
    });
  }

  ngOnInit(): void {
    this.refreshAll();
  }

  ngAfterViewInit(): void {
    this.viewReady = true;
    const data = this.oversight();
    if (
      this.attendanceView() === 'trends' &&
      data &&
      !this.attendanceLoading()
    ) {
      this.ngZone.runOutsideAngular(() => void this.renderCharts(data));
    }
  }

  ngOnDestroy(): void {
    this.destroyCharts();
    this.defaultedCentreOverlay?.dispose();
    this.defaultedCentreOverlay = null;
  }

  private destroyCharts(): void {
    this.trendChart?.destroy();
    this.trendChart = null;
    this.enrolmentChart?.destroy();
    this.enrolmentChart = null;
  }

  /** The directory gives the header its geography; oversight carries every figure. */
  refreshAll(): void {
    this.loadDirectory();
    this.loadOversight();
  }

  setAttendanceView(view: AttendanceView): void {
    this.attendanceView.set(view);
    this.atRiskDetailsOpen.set(false);
    this.leaversOpen.set(false);
    if (view !== 'defaults') {
      this.closeDefaultedCentre();
    }
  }

  toggleAtRiskDetails(): void {
    this.atRiskDetailsOpen.update(open => !open);
    this.leaversOpen.set(false);
  }

  /** Leavers are loaded on first open; the filter chips then work on the loaded list. */
  toggleLeavers(): void {
    const open = !this.leaversOpen();
    this.leaversOpen.set(open);
    this.atRiskDetailsOpen.set(false);
    if (open && this.leavers() === null && !this.leaversLoading()) {
      this.loadLeavers();
    }
  }

  setLeaversFilter(filter: LeaverOutcome | 'all'): void {
    this.leaversFilter.set(filter);
  }

  leaversCount(filter: LeaverOutcome | 'all'): number {
    const all = this.leavers() ?? [];
    return filter === 'all' ? all.length : all.filter(leaver => leaver.outcome === filter).length;
  }

  private loadLeavers(): void {
    this.leaversLoading.set(true);
    this.withRequestState(this.api.getAnObj<CommissionLeaver[]>('/Commission/leavers'), {
      useLoading: false,
      clearError: true,
      errorMessage: 'Unable to load the leavers list.',
      notifyOnError: 'alert',
      rethrow: false,
      onError: () => {
        this.leaversLoading.set(false);
        this.cdr.markForCheck();
      },
    }).subscribe(response => {
      this.leavers.set(response ?? []);
      this.leaversLoading.set(false);
      this.cdr.markForCheck();
    });
  }

  openDefaultedCentre(centre: CommissionDefaultedCentre): void {
    this.selectedDefaultedCentre.set(centre);
    this.selectedMissingDatesArm.set(null);
    this.attachDefaultedCentreOverlay();
  }

  closeDefaultedCentre(): void {
    this.selectedMissingDatesArm.set(null);
    this.selectedDefaultedCentre.set(null);
    this.defaultedCentreOverlay?.detach();
  }

  openMissingDates(arm: CommissionClassArmAttendanceStatus): void {
    this.selectedMissingDatesArm.set(arm);
  }

  closeMissingDates(): void {
    this.selectedMissingDatesArm.set(null);
  }

  /**
   * The dashboard renders inside `mat-sidenav-content`, which is its own stacking context, so a
   * fixed-position modal would sit behind the side menu. Hosting it in a CDK overlay attaches it to
   * the document body instead.
   */
  private attachDefaultedCentreOverlay(): void {
    const template = this.defaultedCentreModal();
    if (!template) {
      return;
    }

    if (!this.defaultedCentreOverlay) {
      this.defaultedCentreOverlay = this.overlay.create({
        hasBackdrop: false,
        scrollStrategy: this.overlay.scrollStrategies.block(),
        positionStrategy: this.overlay.position().global(),
      });

      this.defaultedCentreOverlay.keydownEvents().subscribe(event => {
        if (event.key === 'Escape') {
          if (this.selectedMissingDatesArm()) {
            this.closeMissingDates();
          } else {
            this.closeDefaultedCentre();
          }
          this.cdr.markForCheck();
        }
      });
    }

    if (!this.defaultedCentreOverlay.hasAttached()) {
      this.defaultedCentreOverlay.attach(new TemplatePortal(template, this.viewContainerRef));
    }
  }

  submissionTone(status: CommissionClassArmAttendanceStatus): string {
    if (status.missingDates.length === 0) return 'submission-good';
    if (status.neverSubmitted || status.submittedDates === 0) return 'submission-bad';
    return 'submission-warn';
  }

  openCentre(row: CommissionCentreDirectoryRow | CommissionCentreRow): void {
    void this.router.navigate(['/commission/centres', row.centreId]);
  }

  openLearner(studentId: string): void {
    void this.router.navigate(['/commission/learners', studentId]);
  }

  /** "today" on a school day; otherwise the weekday and date the figures come from. */
  dayLabel(data: CommissionOversight): string {
    const report = String(data.reportDate ?? '').slice(0, 10);
    const asAt = String(data.asAtDate ?? '').slice(0, 10);
    if (!report || report === asAt) {
      return 'today';
    }
    const date = new Date(`${report}T00:00:00`);
    return `· ${date.toLocaleDateString('en-GB', { weekday: 'short', day: '2-digit', month: 'short' })}`;
  }

  share(part: number, total: number): number {
    return total > 0 ? Math.round((part * 1000) / total) / 10 : 0;
  }

  rateTone(rate: number): string {
    if (rate >= 85) return 'tone-good';
    if (rate >= 70) return 'tone-warn';
    return 'tone-bad';
  }

  private loadDirectory(): void {
    this.loadFailed.set(false);

    this.withRequestState(
      this.api.getAnObj<CommissionCentresDirectory>('/Commission/centres'),
      {
        useLoading: false,
        clearError: true,
        errorMessage: 'Unable to load learning centres.',
        notifyOnError: 'alert',
        rethrow: false,
        onError: () => {
          this.loadFailed.set(true);
          this.directory.set(null);
          this.cdr.markForCheck();
        },
      }
    ).subscribe(response => {
      this.directory.set(response);
      this.cdr.markForCheck();
    });
  }

  private loadOversight(): void {
    if (!this.rangeStart || !this.rangeEnd) {
      this.showAlert('Select a from and to date.', { type: 'warning' });
      return;
    }

    const from = this.rangeStart <= this.rangeEnd ? this.rangeStart : this.rangeEnd;
    const to = this.rangeStart <= this.rangeEnd ? this.rangeEnd : this.rangeStart;

    this.selectedDefaultedCentre.set(null);
    this.attendanceFailed.set(false);
    this.attendanceLoading.set(true);
    const params = new HttpParams().set('from', this.toDateOnly(from)).set('to', this.toDateOnly(to));

    this.withRequestState(
      this.api.getAnObj<CommissionOversight>('/Commission/oversight', { params }),
      {
        useLoading: false,
        clearError: true,
        errorMessage: 'Unable to load attendance oversight.',
        notifyOnError: 'alert',
        rethrow: false,
        onError: () => {
          this.attendanceFailed.set(true);
          this.attendanceLoading.set(false);
          this.oversight.set(null);
          this.cdr.markForCheck();
        },
      }
    ).subscribe(response => {
      this.oversight.set(response);
      this.attendanceLoading.set(false);
      this.cdr.markForCheck();
    });
  }

  private async renderCharts(data: CommissionOversight | null): Promise<void> {
    if (!data) {
      return;
    }

    await this.renderTrend(data);
    await this.renderEnrolment(data);
  }

  private async renderTrend(data: CommissionOversight): Promise<void> {
    const host = this.trendHost()?.nativeElement;
    if (!host || data.dailyTrend.length === 0) {
      return;
    }

    const ApexCharts = (await import('apexcharts')).default;

    const options: ApexOptions = {
      chart: {
        type: 'line',
        height: 320,
        fontFamily: 'inherit',
        toolbar: { show: false },
      },
      stroke: { curve: 'smooth', width: 3 },
      colors: ['#138a62', '#2a5c86'],
      series: [
        {
          name: 'Learner attendance %',
          data: data.dailyTrend.map(point => Number(point.learnerAttendanceRate ?? 0)),
        },
        {
          name: 'Facilitator attendance %',
          data: data.dailyTrend.map(point => Number(point.facilitatorAttendanceRate ?? 0)),
        },
      ],
      xaxis: {
        categories: data.dailyTrend.map(point => this.shortDate(point.date)),
        tickAmount: Math.min(12, data.dailyTrend.length),
      },
      yaxis: {
        min: 0,
        max: 100,
        labels: { formatter: value => `${Math.round(value)}%` },
      },
      legend: { position: 'top', horizontalAlign: 'right' },
      dataLabels: { enabled: false },
      grid: { borderColor: '#e7eaf0' },
    };

    const hostHasCanvas = !!host.querySelector('.apexcharts-canvas');
    if (this.trendChart && hostHasCanvas) {
      void this.trendChart.updateOptions(options, true, true);
      return;
    }

    this.trendChart?.destroy();
    host.innerHTML = '';
    this.trendChart = new ApexCharts(host, options) as unknown as ApexChartInstance;
    void this.trendChart.render();
  }

  private async renderEnrolment(data: CommissionOversight): Promise<void> {
    const host = this.enrolmentHost()?.nativeElement;
    if (!host || data.enrolmentByCentre.length === 0) {
      return;
    }

    const ApexCharts = (await import('apexcharts')).default;

    const options: ApexOptions = {
      chart: {
        type: 'bar',
        height: 320,
        fontFamily: 'inherit',
        stacked: false,
        toolbar: { show: false },
      },
      colors: ['#2a5c86', '#e39b25'],
      plotOptions: {
        bar: { horizontal: true, borderRadius: 4, columnWidth: '60%' },
      },
      series: [
        {
          name: 'Learners',
          data: data.enrolmentByCentre.map(point => Number(point.learners ?? 0)),
        },
        {
          name: 'Facilitators',
          data: data.enrolmentByCentre.map(point => Number(point.facilitators ?? 0)),
        },
      ],
      xaxis: {
        categories: data.enrolmentByCentre.map(point => point.centreName || 'Unnamed centre'),
      },
      legend: { position: 'top', horizontalAlign: 'right' },
      dataLabels: { enabled: true },
      grid: { borderColor: '#e7eaf0' },
    };

    const hostHasCanvas = !!host.querySelector('.apexcharts-canvas');
    if (this.enrolmentChart && hostHasCanvas) {
      void this.enrolmentChart.updateOptions(options, true, true);
      return;
    }

    this.enrolmentChart?.destroy();
    host.innerHTML = '';
    this.enrolmentChart = new ApexCharts(host, options) as unknown as ApexChartInstance;
    void this.enrolmentChart.render();
  }

  private shortDate(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return value;
    }

    return date.toLocaleDateString(undefined, { day: '2-digit', month: 'short' });
  }

  private toDateOnly(date: Date): string {
    const year = date.getFullYear();
    const month = `${date.getMonth() + 1}`.padStart(2, '0');
    const day = `${date.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private startOfDay(date: Date): Date {
    return new Date(date.getFullYear(), date.getMonth(), date.getDate());
  }

  private addDays(date: Date, days: number): Date {
    const copy = this.startOfDay(date);
    copy.setDate(copy.getDate() + days);
    return copy;
  }
}
