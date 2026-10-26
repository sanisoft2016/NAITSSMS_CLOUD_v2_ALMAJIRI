import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { BaseComponent, GenericService } from '@shared';

import { CommissionCentreDetail } from '../../supper-admin/commission-dashboard/commission-dashboard.models';
import { barTone, rateTone, scoreTone, sparkline } from '../commission-format';

/** One learning centre as the Commission sees it: roll, staff cover, registers and results. */
@Component({
  selector: 'app-commission-centre',
  imports: [DatePipe, FormsModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './commission-centre.html',
  styleUrls: ['../commission-pages.scss', './commission-centre.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CommissionCentre extends BaseComponent implements OnInit {
  private readonly api = inject(GenericService<CommissionCentreDetail>);

  readonly detail = signal<CommissionCentreDetail | null>(null);
  readonly failed = signal(false);
  readonly rollFilter = signal('');

  readonly rateTone = rateTone;
  readonly scoreTone = scoreTone;
  readonly barTone = barTone;

  readonly chartWidth = 640;
  readonly chartHeight = 140;

  readonly learnerLine = computed(() =>
    sparkline((this.detail()?.dailyTrend ?? []).map(p => p.learnerAttendanceRate), this.chartWidth, this.chartHeight, 50),
  );

  readonly facilitatorLine = computed(() =>
    sparkline((this.detail()?.dailyTrend ?? []).map(p => p.facilitatorAttendanceRate), this.chartWidth, this.chartHeight, 50),
  );

  readonly filteredRoll = computed(() => {
    const needle = this.rollFilter().trim().toLowerCase();
    const roll = this.detail()?.roll ?? [];
    if (!needle) {
      return roll;
    }
    return roll.filter(
      learner =>
        learner.fullName.toLowerCase().includes(needle) ||
        learner.identificationNo.toLowerCase().includes(needle) ||
        learner.level.toLowerCase().includes(needle),
    );
  });

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => this.load(params.get('centreId') ?? ''));
  }

  back(): void {
    void this.router.navigate(['/commission']);
  }

  openLearner(studentId: string): void {
    void this.router.navigate(['/commission/learners', studentId]);
  }

  print(): void {
    window.print();
  }

  private load(centreId: string): void {
    this.failed.set(false);
    this.withRequestState(this.api.getAnObj<CommissionCentreDetail>(`/Commission/centres/${encodeURIComponent(centreId)}`), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Unable to load this learning centre.',
      notifyOnError: 'alert',
      rethrow: false,
      onError: () => {
        this.failed.set(true);
        this.detail.set(null);
        this.cdr.markForCheck();
      },
    }).subscribe(response => {
      this.detail.set(response);
      this.cdr.markForCheck();
    });
  }
}
