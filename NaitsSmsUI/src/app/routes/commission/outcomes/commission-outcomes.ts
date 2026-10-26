import { DecimalPipe } from '@angular/common';
import { HttpParams } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { BaseComponent, GenericService } from '@shared';

import {
  CommissionOutcomes as OutcomesDto,
  CommissionTermOption,
} from '../../supper-admin/commission-dashboard/commission-dashboard.models';
import { barTone, delta, rateTone, scoreTone } from '../commission-format';

/** Programme-wide learning outcomes for one term, from scores the facilitators entered. */
@Component({
  selector: 'app-commission-outcomes',
  imports: [DecimalPipe, FormsModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './commission-outcomes.html',
  styleUrls: ['../commission-pages.scss', './commission-outcomes.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CommissionOutcomes extends BaseComponent implements OnInit {
  private readonly api = inject(GenericService<OutcomesDto>);

  readonly data = signal<OutcomesDto | null>(null);
  readonly failed = signal(false);
  selectedKey = '';

  readonly rateTone = rateTone;
  readonly scoreTone = scoreTone;
  readonly barTone = barTone;

  readonly averageDelta = computed(() => delta(this.data()?.averageScore, this.data()?.previousAverageScore));
  readonly passDelta = computed(() => delta(this.data()?.passRate, this.data()?.previousPassRate));

  readonly maxGradeCount = computed(() => Math.max(1, ...(this.data()?.grades ?? []).map(g => g.count)));
  readonly totalGraded = computed(() => (this.data()?.grades ?? []).reduce((sum, g) => sum + g.count, 0));

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.load();
  }

  termKey(term: CommissionTermOption | null | undefined): string {
    return term ? `${term.session}|${term.term}` : '';
  }

  onTermChange(key: string): void {
    this.selectedKey = key;
    const [session, term] = key.split('|');
    this.load(session, term);
  }

  openCentre(centreId: string): void {
    void this.router.navigate(['/commission/centres', centreId]);
  }

  gradeShare(count: number): number {
    const total = this.totalGraded();
    return total > 0 ? Math.round((count * 1000) / total) / 10 : 0;
  }

  private load(session?: string, term?: string): void {
    this.failed.set(false);
    let params = new HttpParams();
    if (session && term) {
      params = params.set('session', session).set('term', term);
    }

    this.withRequestState(this.api.getAnObj<OutcomesDto>('/Commission/outcomes', { params }), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Unable to load learning outcomes.',
      notifyOnError: 'alert',
      rethrow: false,
      onError: () => {
        this.failed.set(true);
        this.cdr.markForCheck();
      },
    }).subscribe(response => {
      this.data.set(response);
      this.selectedKey = this.termKey(response.term);
      this.cdr.markForCheck();
    });
  }
}
