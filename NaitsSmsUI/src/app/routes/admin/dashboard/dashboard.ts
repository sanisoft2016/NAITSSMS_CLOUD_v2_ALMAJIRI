import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  NgZone,
  OnDestroy,
  OnInit,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import type { ApexOptions } from 'apexcharts';

import { BaseComponent, GenericService } from '@shared';
import { credentialHasRole, credentialHasTeacherAccess } from '@shared/models/UserCredential';

import {
  AttendanceArmSummary,
  AttendanceAtRiskLearner,
  DailyAttendanceRosterEntry,
  DailyAttendanceStatus,
  DailyAttendanceSummary,
  toDateOnly,
} from '../daily-attendance/daily-attendance.models';
import { AttendanceSelectOption, DailyAttendanceService } from '../daily-attendance/daily-attendance.service';
import { forkJoin, of } from 'rxjs';
import { catchError, finalize, map } from 'rxjs/operators';

type ApexChartInstance = {
  render: () => Promise<void>;
  updateOptions: (options: ApexOptions, redrawPaths?: boolean, animate?: boolean) => Promise<void>;
  destroy: () => void;
};

type DashboardClassBreakdown = {
  classLabel?: string;
  numberOfStudents?: number;
};

type DashboardAssessmentRow = {
  studentId?: string;
  ca1?: number;
  ca2?: number;
  ca3?: number;
  exams?: number;
  student?: {
    fullName?: string;
    firstName?: string;
    middleName?: string;
    surName?: string;
    user?: { fullName?: string; firstName?: string; middleName?: string; surName?: string };
  };
  classArm?: {
    classId?: string;
    armName?: string | number;
    classs?: {
      classId?: string;
      classLabel?: string;
      className?: string | number;
    };
  };
};

type SchoolSummaryResponse = {
  session?: string;
  term?: string;
  totalStudents?: number;
  totalTeachers?: number;
  dashboardStudentsNumberAnalysis?: DashboardClassBreakdown[];
  studentsAccessment?: DashboardAssessmentRow[];
};

type ClassEnrollmentRow = {
  label: string;
  count: number;
  percent: number;
  color: string;
};

type TopStudentRow = {
  rankLabel: string;
  name: string;
  armLabel: string;
  average: number;
};

type ClassTopPerformers = {
  classKey: string;
  classLabel: string;
  accent: string;
  students: TopStudentRow[];
};

type AttendanceClassChip = {
  classId: string;
  classLabel: string;
  accent: string;
};

type ArmStatusBucket = {
  status: DailyAttendanceStatus;
  label: string;
  cssClass: string;
  learners: DailyAttendanceRosterEntry[];
};

type ArmAttendanceBoard = {
  armId: string;
  armLabel: string;
  total: number;
  buckets: ArmStatusBucket[];
};

const ENROLMENT_PALETTE = [
  '#0f766e',
  '#0369a1',
  '#b45309',
  '#be123c',
  '#15803d',
  '#0e7490',
  '#c2410c',
  '#1d4ed8',
  '#a16207',
  '#047857',
];

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DecimalPipe, RouterLink, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
})
export class Dashboard extends BaseComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly api = inject(GenericService<SchoolSummaryResponse>);
  private readonly attendanceApi = inject(DailyAttendanceService);
  private readonly nav = inject(Router);
  private readonly ngZone = inject(NgZone);

  private readonly pieHost = viewChild<ElementRef<HTMLElement>>('enrolmentPie');
  private readonly trendHost = viewChild<ElementRef<HTMLElement>>('attendanceTrend');

  private pieChart?: ApexChartInstance;
  private trendChart?: ApexChartInstance;
  private viewReady = false;

  readonly summary = signal<SchoolSummaryResponse | null>(null);
  readonly loadFailed = signal(false);
  readonly emptyReason = signal<string | null>(null);
  readonly selectedClassKey = signal<string | null>(null);

  readonly attendance = signal<DailyAttendanceSummary | null>(null);
  readonly attendanceLoading = signal(false);
  readonly attendanceFailed = signal(false);

  /** Top-performers-style class attendance board — closed until the user opens it. */
  readonly classAttendanceOpen = signal(false);
  readonly attendanceClassChips = signal<AttendanceClassChip[]>([]);
  readonly selectedAttendanceClassId = signal<string | null>(null);
  readonly armAttendanceBoards = signal<ArmAttendanceBoard[]>([]);
  readonly armAttendanceLoading = signal(false);
  readonly armAttendanceError = signal('');
  /** `${armId}:${status}` — student names stay hidden until a status button is clicked. */
  readonly selectedStatusKey = signal<string | null>(null);
  readonly attendanceRollDate = toDateOnly(new Date());

  readonly sessionLabel = computed(() => this.summary()?.session?.trim() || '—');
  readonly termLabel = computed(() => this.formatTerm(this.summary()?.term));
  readonly totalStudents = computed(() => this.summary()?.totalStudents ?? 0);
  readonly totalTeachers = computed(() => this.summary()?.totalTeachers ?? 0);

  readonly classEnrollment = computed<ClassEnrollmentRow[]>(() => {
    const rows = this.summary()?.dashboardStudentsNumberAnalysis ?? [];
    const total = Math.max(this.totalStudents(), 1);
    return rows.map((row, index) => {
      const count = Number(row.numberOfStudents ?? 0);
      return {
        label: (row.classLabel ?? 'Class').trim() || 'Class',
        count,
        percent: Math.min(100, Math.round((count / total) * 1000) / 10),
        color: ENROLMENT_PALETTE[index % ENROLMENT_PALETTE.length],
      };
    });
  });

  readonly topPerformers = computed<ClassTopPerformers[]>(() =>
    this.buildTopPerformers(this.summary()?.studentsAccessment ?? []),
  );

  readonly selectedPerformers = computed(() => {
    const groups = this.topPerformers();
    if (!groups.length) {
      return null;
    }
    const key = this.selectedClassKey();
    return groups.find(group => group.classKey === key) ?? groups[0];
  });

  readonly hasEnrollment = computed(() => this.classEnrollment().length > 0);
  readonly hasTopPerformers = computed(() => this.topPerformers().length > 0);
  readonly hasSummary = computed(() => this.summary() != null);
  readonly largestClass = computed(() => {
    const rows = this.classEnrollment();
    if (!rows.length) {
      return null;
    }
    return [...rows].sort((a, b) => b.count - a.count)[0];
  });

  readonly attendanceArms = computed<AttendanceArmSummary[]>(() => {
    const arms = this.attendance()?.arms ?? [];
    return [...arms].sort((a, b) => a.attendanceRate - b.attendanceRate);
  });

  readonly atRiskPreview = computed<AttendanceAtRiskLearner[]>(() =>
    (this.attendance()?.atRiskLearners ?? []).slice(0, 5),
  );

  readonly hasAttendanceTrend = computed(() => (this.attendance()?.dailyTrend?.length ?? 0) > 0);

  readonly attendanceRateTone = computed(() => {
    const rate = this.attendance()?.learnerAttendanceRate ?? 0;
    if (rate >= 85) return 'good';
    if (rate >= 70) return 'warn';
    return 'bad';
  });

  readonly selectedAttendanceClass = computed(() => {
    const id = this.selectedAttendanceClassId();
    return this.attendanceClassChips().find(chip => chip.classId === id) ?? null;
  });

  readonly selectedStatusLearners = computed(() => {
    const key = this.selectedStatusKey();
    if (!key) {
      return null;
    }

    const separator = key.lastIndexOf(':');
    if (separator < 0) {
      return null;
    }

    const armId = key.slice(0, separator);
    const status = Number(key.slice(separator + 1)) as DailyAttendanceStatus;
    const board = this.armAttendanceBoards().find(item => item.armId === armId);
    const bucket = board?.buckets.find(item => item.status === status);
    if (!board || !bucket) {
      return null;
    }

    return {
      armLabel: board.armLabel,
      statusLabel: bucket.label,
      statusClass: bucket.cssClass,
      learners: bucket.learners,
    };
  });

  constructor() {
    super();
    effect(() => {
      const rows = this.classEnrollment();
      const loading = this.isLoading();

      // Loading replaces the pie host in the template — drop the stale Apex instance.
      if (loading) {
        this.destroyEnrolmentPie();
        return;
      }

      if (!this.viewReady || !this.hasSummary()) {
        return;
      }

      // Wait for the enrolment panel to enter the DOM after loading completes.
      this.ngZone.runOutsideAngular(() => {
        requestAnimationFrame(() => {
          requestAnimationFrame(() => this.renderEnrolmentPie(rows));
        });
      });
    });

    effect(() => {
      const data = this.attendance();
      const loading = this.attendanceLoading();

      if (loading) {
        this.destroyTrendChart();
        return;
      }

      if (!this.viewReady || !data?.dailyTrend?.length) {
        return;
      }

      this.ngZone.runOutsideAngular(() => {
        requestAnimationFrame(() => {
          requestAnimationFrame(() => void this.renderAttendanceTrend(data));
        });
      });
    });
  }

  ngOnInit(): void {
    if (this.isTeacherUser()) {
      void this.nav.navigateByUrl('/teacher');
      return;
    }

    // Commission officers have no school of their own; their home is programme oversight.
    if (this.hasStoredRole('Commission')) {
      void this.nav.navigateByUrl('/commission');
      return;
    }

    this.loadSummary();
    this.loadAttendanceSummary();
  }

  ngAfterViewInit(): void {
    this.viewReady = true;
    const rows = this.classEnrollment();
    if (rows.length && !this.isLoading()) {
      this.ngZone.runOutsideAngular(() => this.renderEnrolmentPie(rows));
    }

    const attendance = this.attendance();
    if (attendance?.dailyTrend?.length && !this.attendanceLoading()) {
      this.ngZone.runOutsideAngular(() => void this.renderAttendanceTrend(attendance));
    }
  }

  ngOnDestroy(): void {
    this.destroyEnrolmentPie();
    this.destroyTrendChart();
  }

  refresh(): void {
    this.destroyEnrolmentPie();
    this.destroyTrendChart();
    this.resetClassAttendanceBoard();
    this.loadSummary();
    this.loadAttendanceSummary();
  }

  rateBarWidth(rate: number): string {
    return `${Math.max(0, Math.min(100, Number(rate) || 0))}%`;
  }

  rateTone(rate: number): string {
    if (rate >= 85) return 'good';
    if (rate >= 70) return 'warn';
    return 'bad';
  }

  selectClass(classKey: string): void {
    this.selectedClassKey.set(classKey);
  }

  openClassAttendanceBoard(): void {
    this.classAttendanceOpen.set(true);
    this.selectedStatusKey.set(null);
    this.loadAttendanceClasses();
  }

  closeClassAttendanceBoard(): void {
    this.resetClassAttendanceBoard();
  }

  selectAttendanceClass(classId: string): void {
    if (!classId || this.selectedAttendanceClassId() === classId) {
      return;
    }

    this.selectedAttendanceClassId.set(classId);
    this.selectedStatusKey.set(null);
    this.armAttendanceBoards.set([]);
    this.loadArmAttendanceBoards(classId);
  }

  toggleStatusLearners(armId: string, status: DailyAttendanceStatus): void {
    const key = `${armId}:${status}`;
    this.selectedStatusKey.set(this.selectedStatusKey() === key ? null : key);
  }

  isStatusSelected(armId: string, status: DailyAttendanceStatus): boolean {
    return this.selectedStatusKey() === `${armId}:${status}`;
  }

  private resetClassAttendanceBoard(): void {
    this.classAttendanceOpen.set(false);
    this.attendanceClassChips.set([]);
    this.selectedAttendanceClassId.set(null);
    this.armAttendanceBoards.set([]);
    this.armAttendanceLoading.set(false);
    this.armAttendanceError.set('');
    this.selectedStatusKey.set(null);
  }

  private loadAttendanceClasses(): void {
    this.armAttendanceError.set('');
    this.withRequestState(this.attendanceApi.getClasses(), {
      useLoading: false,
      clearError: false,
      errorMessage: 'Unable to load classes for attendance.',
      rethrow: false,
      onError: error => {
        this.armAttendanceError.set(
          this.getHttpErrorMessage(error, 'Unable to load classes for attendance.'),
        );
        this.cdr.markForCheck();
      },
    }).subscribe((classes: AttendanceSelectOption[] | undefined) => {
      const chips = (classes ?? []).map((item, index) => ({
        classId: item.value,
        classLabel: item.label,
        accent: ENROLMENT_PALETTE[index % ENROLMENT_PALETTE.length],
      }));
      this.attendanceClassChips.set(chips);
      this.cdr.markForCheck();

      if (chips.length && !this.selectedAttendanceClassId()) {
        // Do not auto-select a class — user must click a class chip first.
      }
    });
  }

  private loadArmAttendanceBoards(classId: string): void {
    this.armAttendanceLoading.set(true);
    this.armAttendanceError.set('');

    this.withRequestState(this.attendanceApi.getArms(classId), {
      useLoading: false,
      clearError: false,
      errorMessage: 'Unable to load arms for this class.',
      rethrow: false,
      onError: error => {
        this.armAttendanceLoading.set(false);
        this.armAttendanceBoards.set([]);
        this.armAttendanceError.set(this.getHttpErrorMessage(error, 'Unable to load arms for this class.'));
        this.cdr.markForCheck();
      },
    }).subscribe(arms => {
      const armList = arms ?? [];
      if (!armList.length) {
        this.armAttendanceLoading.set(false);
        this.armAttendanceBoards.set([]);
        this.armAttendanceError.set('No arms were found for this class.');
        this.cdr.markForCheck();
        return;
      }

      const date = this.attendanceRollDate;
      forkJoin(
        armList.map(arm =>
          this.attendanceApi.getLearnerRoster(arm.value, date).pipe(
            map(roster => this.buildArmBoard(arm.value, arm.label, roster.entries ?? [])),
            catchError(() => of(this.buildArmBoard(arm.value, arm.label, []))),
          ),
        ),
      )
        .pipe(
          finalize(() => {
            this.armAttendanceLoading.set(false);
            this.cdr.markForCheck();
          }),
        )
        .subscribe(boards => {
          this.armAttendanceBoards.set(boards);
          this.cdr.markForCheck();
        });
    });
  }

  private buildArmBoard(
    armId: string,
    armLabel: string,
    entries: DailyAttendanceRosterEntry[],
  ): ArmAttendanceBoard {
    const sorted = [...entries].sort((a, b) =>
      a.fullName.localeCompare(b.fullName, undefined, { sensitivity: 'base' }),
    );

    const byStatus = (status: DailyAttendanceStatus) =>
      sorted.filter(entry => entry.status === status);

    return {
      armId,
      armLabel: armLabel || sorted[0]?.classArmLabel || 'Arm',
      total: sorted.length,
      buckets: [
        {
          status: DailyAttendanceStatus.Present,
          label: 'Present',
          cssClass: 'present',
          learners: byStatus(DailyAttendanceStatus.Present),
        },
        {
          status: DailyAttendanceStatus.Absent,
          label: 'Absent',
          cssClass: 'absent',
          learners: byStatus(DailyAttendanceStatus.Absent),
        },
        {
          status: DailyAttendanceStatus.Late,
          label: 'Late',
          cssClass: 'late',
          learners: byStatus(DailyAttendanceStatus.Late),
        },
        {
          status: DailyAttendanceStatus.Excused,
          label: 'Excused',
          cssClass: 'excused',
          learners: byStatus(DailyAttendanceStatus.Excused),
        },
      ],
    };
  }

  private destroyEnrolmentPie(): void {
    try {
      this.pieChart?.destroy();
    } catch {
      // Chart may already be detached from the DOM after a refresh/remount.
    }
    this.pieChart = undefined;
  }

  private destroyTrendChart(): void {
    try {
      this.trendChart?.destroy();
    } catch {
      // Chart may already be detached from the DOM after a refresh/remount.
    }
    this.trendChart = undefined;
  }

  private loadAttendanceSummary(): void {
    const to = toDateOnly(new Date());
    const fromDate = new Date();
    fromDate.setDate(fromDate.getDate() - 29);
    const from = toDateOnly(fromDate);

    this.attendanceFailed.set(false);
    this.attendanceLoading.set(true);

    this.withRequestState(this.attendanceApi.getSummary(from, to), {
      useLoading: false,
      clearError: false,
      errorMessage: 'Unable to load learner attendance.',
      rethrow: false,
      onError: () => {
        this.attendanceFailed.set(true);
        this.attendance.set(null);
        this.attendanceLoading.set(false);
        this.cdr.markForCheck();
      },
    })
      .subscribe(summary => {
        this.attendanceLoading.set(false);
        this.attendance.set(summary ?? null);
        this.cdr.markForCheck();
      });
  }

  private loadSummary(): void {
    this.loadFailed.set(false);
    this.emptyReason.set(null);
    this.destroyEnrolmentPie();

    this.withRequestState(this.api.getAnObj<SchoolSummaryResponse | null>('/Registry/dashboard/summary'), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Unable to load the school dashboard.',
      notifyOnError: 'alert',
      rethrow: false,
      onError: () => {
        this.loadFailed.set(true);
        this.summary.set(null);
        this.cdr.markForCheck();
      },
    }).subscribe({
      next: response => {
        if (!response) {
          this.summary.set(null);
          this.selectedClassKey.set(null);
          this.emptyReason.set(
            'No current academic session or term was found. Initialise session and term, then try again.',
          );
          this.cdr.markForCheck();
          return;
        }

        this.summary.set(response);
        const groups = this.buildTopPerformers(response.studentsAccessment ?? []);
        this.selectedClassKey.set(groups[0]?.classKey ?? null);
        this.cdr.markForCheck();
      },
    });
  }

  private renderEnrolmentPie(rows: ClassEnrollmentRow[]): void {
    void this.renderEnrolmentPieAsync(rows);
  }

  private async renderEnrolmentPieAsync(rows: ClassEnrollmentRow[]): Promise<void> {
    const host = this.pieHost()?.nativeElement;
    if (!host) {
      return;
    }

    if (!rows.length) {
      this.destroyEnrolmentPie();
      host.innerHTML = '';
      return;
    }

    const ApexCharts = (await import('apexcharts')).default;

    // Keep outer radius the same; shrink the hole so the ring grows inward.
    // Previous hole was 68%; ~27% ≈ hole reduced by ~2.5× (fills inner empty space).
    const options: ApexOptions = {
      chart: {
        type: 'donut',
        height: 360,
        fontFamily: 'inherit',
        animations: {
          enabled: true,
        },
      },
      series: rows.map(row => row.count),
      labels: rows.map(row => row.label),
      colors: rows.map(row => row.color),
      stroke: {
        width: 3,
        colors: ['#f7faf9'],
      },
      plotOptions: {
        pie: {
          startAngle: -25,
          endAngle: 335,
          expandOnClick: true,
          donut: {
            size: '27%',
            background: 'transparent',
            labels: {
              show: true,
              name: {
                show: true,
                fontSize: '15px',
                fontWeight: 800,
                color: '#1a2b34',
                offsetY: 22,
              },
              value: {
                show: true,
                fontSize: '26px',
                fontWeight: 750,
                color: '#1a2b34',
                offsetY: -14,
                formatter: value => String(value),
              },
              total: {
                show: true,
                showAlways: true,
                label: 'Students',
                fontSize: '15px',
                fontWeight: 800,
                color: '#1a2b34',
                formatter: () => String(this.totalStudents()),
              },
            },
          },
        },
      },
      dataLabels: { enabled: false },
      legend: { show: false },
      tooltip: {
        theme: 'light',
        y: {
          formatter: value => `${value} students`,
        },
      },
      states: {
        hover: {
          filter: { type: 'lighten' },
        },
        active: {
          filter: { type: 'none' },
        },
      },
    };

    // After refresh the host is a new DOM node; always recreate when canvas is missing.
    const hostHasCanvas = !!host.querySelector('.apexcharts-canvas');
    if (this.pieChart && hostHasCanvas) {
      void this.pieChart.updateOptions(options, true, true);
      return;
    }

    this.destroyEnrolmentPie();
    host.innerHTML = '';
    this.pieChart = new ApexCharts(host, options);
    void this.pieChart.render();
  }

  private async renderAttendanceTrend(data: DailyAttendanceSummary): Promise<void> {
    const host = this.trendHost()?.nativeElement;
    if (!host || !data.dailyTrend?.length) {
      return;
    }

    const ApexCharts = (await import('apexcharts')).default;
    const options: ApexOptions = {
      chart: {
        type: 'area',
        height: 240,
        fontFamily: 'inherit',
        toolbar: { show: false },
        zoom: { enabled: false },
        animations: { enabled: true },
      },
      colors: ['#0f766e'],
      fill: {
        type: 'gradient',
        gradient: {
          shadeIntensity: 0.45,
          opacityFrom: 0.42,
          opacityTo: 0.05,
          stops: [0, 90, 100],
        },
      },
      stroke: { curve: 'smooth', width: 3 },
      dataLabels: { enabled: false },
      series: [
        {
          name: 'Learner attendance %',
          data: data.dailyTrend.map(point => Number(point.attendanceRate ?? 0)),
        },
      ],
      xaxis: {
        categories: data.dailyTrend.map(point => this.shortTrendDate(point.date)),
        tickAmount: Math.min(8, data.dailyTrend.length),
        labels: {
          style: { colors: '#5b6e78', fontSize: '11px' },
        },
        axisBorder: { show: false },
        axisTicks: { show: false },
      },
      yaxis: {
        min: 0,
        max: 100,
        labels: {
          formatter: value => `${Math.round(value)}%`,
          style: { colors: '#5b6e78', fontSize: '11px' },
        },
      },
      grid: {
        borderColor: 'rgba(26, 43, 52, 0.08)',
        strokeDashArray: 4,
      },
      tooltip: {
        y: { formatter: value => `${Math.round(Number(value))}%` },
      },
    };

    const hostHasCanvas = !!host.querySelector('.apexcharts-canvas');
    if (this.trendChart && hostHasCanvas) {
      void this.trendChart.updateOptions(options, true, true);
      return;
    }

    this.destroyTrendChart();
    host.innerHTML = '';
    this.trendChart = new ApexCharts(host, options) as unknown as ApexChartInstance;
    void this.trendChart.render();
  }

  private shortTrendDate(value: string): string {
    const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(value ?? '').trim());
    if (match) {
      const date = new Date(+match[1], +match[2] - 1, +match[3]);
      return date.toLocaleDateString(undefined, { day: '2-digit', month: 'short' });
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return value;
    }

    return date.toLocaleDateString(undefined, { day: '2-digit', month: 'short' });
  }

  private buildTopPerformers(rows: DashboardAssessmentRow[]): ClassTopPerformers[] {
    const byClass = new Map<string, DashboardAssessmentRow[]>();

    for (const row of rows) {
      const classKey =
        String(row.classArm?.classId ?? row.classArm?.classs?.classId ?? row.classArm?.classs?.classLabel ?? '').trim();
      if (!classKey) {
        continue;
      }
      const bucket = byClass.get(classKey) ?? [];
      bucket.push(row);
      byClass.set(classKey, bucket);
    }

    const result: ClassTopPerformers[] = [];
    let accentIndex = 0;

    for (const [classKey, classRows] of byClass) {
      const byStudent = new Map<string, DashboardAssessmentRow[]>();
      for (const row of classRows) {
        const studentId = String(row.studentId ?? '').trim();
        if (!studentId) {
          continue;
        }
        const bucket = byStudent.get(studentId) ?? [];
        bucket.push(row);
        byStudent.set(studentId, bucket);
      }

      const ranked = [...byStudent.values()]
        .map(studentRows => {
          const sample = studentRows[0];
          const average =
            studentRows.reduce((sum, item) => sum + this.rowTotal(item), 0) / Math.max(studentRows.length, 1);
          return {
            rankLabel: '',
            name: this.resolveStudentName(sample),
            armLabel: this.formatArmName(sample?.classArm?.armName),
            average,
          } satisfies TopStudentRow;
        })
        .sort((a, b) => b.average - a.average)
        .slice(0, 3)
        .map((student, index) => ({
          ...student,
          rankLabel: index === 0 ? '1st' : index === 1 ? '2nd' : '3rd',
        }));

      if (ranked.length === 0) {
        continue;
      }

      const classLabel =
        classRows[0]?.classArm?.classs?.classLabel?.trim() ||
        String(classRows[0]?.classArm?.classs?.className ?? 'Class');

      result.push({
        classKey,
        classLabel,
        accent: ENROLMENT_PALETTE[accentIndex % ENROLMENT_PALETTE.length],
        students: ranked,
      });
      accentIndex += 1;
    }

    return result.sort((a, b) => a.classLabel.localeCompare(b.classLabel));
  }

  private rowTotal(row: DashboardAssessmentRow): number {
    return Number(row.ca1 ?? 0) + Number(row.ca2 ?? 0) + Number(row.ca3 ?? 0) + Number(row.exams ?? 0);
  }

  private resolveStudentName(row: DashboardAssessmentRow | undefined): string {
    const fromUser = row?.student?.user?.fullName?.trim();
    if (fromUser) {
      return fromUser;
    }

    const userParts = [row?.student?.user?.firstName, row?.student?.user?.middleName, row?.student?.user?.surName]
      .map(part => String(part ?? '').trim())
      .filter(Boolean);
    if (userParts.length) {
      return userParts.join(' ');
    }

    const fromStudent = row?.student?.fullName?.trim();
    if (fromStudent) {
      return fromStudent;
    }

    const studentParts = [row?.student?.firstName, row?.student?.middleName, row?.student?.surName]
      .map(part => String(part ?? '').trim())
      .filter(Boolean);
    return studentParts.join(' ') || 'Student';
  }

  private formatArmName(arm: string | number | undefined | null): string {
    if (arm == null || arm === '') {
      return '';
    }
    if (typeof arm === 'string' && Number.isNaN(Number(arm))) {
      return arm;
    }
    const index = typeof arm === 'number' ? arm : Number(arm);
    if (!Number.isFinite(index) || index < 0) {
      return String(arm);
    }
    const letters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
    return letters[index] ?? String(arm);
  }

  private formatTerm(term: string | undefined | null): string {
    const value = String(term ?? '').trim();
    if (!value) {
      return '—';
    }
    if (/term/i.test(value)) {
      return value;
    }
    return `${value} Term`;
  }

  private hasStoredRole(role: string): boolean {
    try {
      const storedCredential = localStorage.getItem('credential');
      if (!storedCredential || storedCredential === 'null') {
        return false;
      }

      return credentialHasRole(JSON.parse(storedCredential) as { role?: string | string[] }, role);
    } catch {
      return false;
    }
  }

  private isTeacherUser(): boolean {
    try {
      const storedCredential = localStorage.getItem('credential');
      if (!storedCredential || storedCredential === 'null') {
        return false;
      }

      const credential = JSON.parse(storedCredential) as { role?: string | string[] };
      return credentialHasTeacherAccess(credential);
    } catch {
      return false;
    }
  }
}
