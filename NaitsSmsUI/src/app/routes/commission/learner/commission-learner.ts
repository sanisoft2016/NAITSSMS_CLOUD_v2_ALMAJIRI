import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { BaseComponent, GenericService } from '@shared';

import { CommissionLearnerRecord } from '../../supper-admin/commission-dashboard/commission-dashboard.models';
import { rateTone, scoreTone } from '../commission-format';

/**
 * One learner's full programme record: identity, guardian, origin, every term's results and
 * attendance, and where they went on leaving. Prints as an academic record for reintegration.
 */
@Component({
  selector: 'app-commission-learner',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './commission-learner.html',
  styleUrls: ['../commission-pages.scss', './commission-learner.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CommissionLearner extends BaseComponent implements OnInit {
  private readonly api = inject(GenericService<CommissionLearnerRecord>);

  readonly record = signal<CommissionLearnerRecord | null>(null);
  readonly failed = signal(false);
  readonly printedOn = new Date();

  readonly rateTone = rateTone;
  readonly scoreTone = scoreTone;

  /** Most recent term first on screen; the printed record reads oldest first like a transcript. */
  readonly termsNewestFirst = computed(() => [...(this.record()?.terms ?? [])].reverse());

  readonly completedTerms = computed(() => (this.record()?.terms ?? []).filter(term => term.complete));

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => this.load(params.get('studentId') ?? ''));
  }

  back(): void {
    if (window.history.length > 1) {
      window.history.back();
      return;
    }
    void this.router.navigate(['/commission/learners']);
  }

  openCentre(centreId: string): void {
    void this.router.navigate(['/commission/centres', centreId]);
  }

  print(): void {
    window.print();
  }

  statusTone(status: string): string {
    if (status === 'Present') return 'flag--good';
    if (status === 'Absent') return 'flag--bad';
    return 'flag--warn';
  }

  private load(studentId: string): void {
    this.failed.set(false);
    this.withRequestState(
      this.api.getAnObj<CommissionLearnerRecord>(`/Commission/learners/${encodeURIComponent(studentId)}`),
      {
        useLoading: true,
        clearError: true,
        errorMessage: 'Unable to load this learner record.',
        notifyOnError: 'alert',
        rethrow: false,
        onError: () => {
          this.failed.set(true);
          this.cdr.markForCheck();
        },
      },
    ).subscribe(response => {
      this.record.set(response);
      this.cdr.markForCheck();
    });
  }
}
