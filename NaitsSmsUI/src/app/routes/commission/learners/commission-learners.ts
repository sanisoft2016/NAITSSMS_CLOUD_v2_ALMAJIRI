import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { HttpParams } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { BaseComponent, GenericService } from '@shared';

import { CommissionLearnerSearchRow } from '../../supper-admin/commission-dashboard/commission-dashboard.models';

/** Find any programme learner — on roll or already moved on — by name, programme ID or guardian. */
@Component({
  selector: 'app-commission-learners',
  imports: [FormsModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './commission-learners.html',
  styleUrls: ['../commission-pages.scss', './commission-learners.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CommissionLearners extends BaseComponent implements OnInit {
  private readonly api = inject(GenericService<CommissionLearnerSearchRow[]>);

  query = '';
  readonly results = signal<CommissionLearnerSearchRow[] | null>(null);
  readonly searchedFor = signal('');

  constructor() {
    super();
  }

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap.get('q');
    if (q) {
      this.query = q;
      this.search();
    }
  }

  search(): void {
    const q = this.query.trim();
    if (q.length < 2) {
      this.showAlert('Type at least two characters: part of a name, a programme ID or a phone number.', {
        type: 'warning',
      });
      return;
    }

    void this.router.navigate([], { queryParams: { q }, replaceUrl: true });
    this.withRequestState(
      this.api.getAnObj<CommissionLearnerSearchRow[]>('/Commission/learners', { params: new HttpParams().set('q', q) }),
      {
        useLoading: true,
        clearError: true,
        errorMessage: 'The search could not be completed.',
        notifyOnError: 'alert',
        rethrow: false,
      },
    ).subscribe(rows => {
      this.results.set(rows ?? []);
      this.searchedFor.set(q);
      this.cdr.markForCheck();
    });
  }

  open(row: CommissionLearnerSearchRow): void {
    void this.router.navigate(['/commission/learners', row.studentId]);
  }
}
