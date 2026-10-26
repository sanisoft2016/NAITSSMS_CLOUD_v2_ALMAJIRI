import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  DefaultMatCalendarRangeStrategy,
  MAT_DATE_RANGE_SELECTION_STRATEGY,
  MatDatepickerModule,
} from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { finalize } from 'rxjs';

import { BaseComponent } from '@shared';

import { DailyAttendanceSummary, parseDateOnly, toDateOnly } from '../daily-attendance.models';
import { DailyAttendanceService } from '../daily-attendance.service';

@Component({
  selector: 'app-attendance-summary',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDatepickerModule, MatFormFieldModule, MatInputModule],
  providers: [
    {
      provide: MAT_DATE_RANGE_SELECTION_STRATEGY,
      useClass: DefaultMatCalendarRangeStrategy,
    },
  ],
  templateUrl: './attendance-summary.html',
  styleUrls: ['../daily-attendance.shared.scss'],
})
export class AttendanceSummary extends BaseComponent implements OnInit {
  private readonly attendance = inject(DailyAttendanceService);

  readonly maxDate = this.startOfDay(new Date());
  rangeStart: Date | null = this.addDays(this.maxDate, -29);
  rangeEnd: Date | null = this.maxDate;

  summary: DailyAttendanceSummary | null = null;
  isSyncing = false;

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.loadSummary();
  }

  onRangeInputChange(): void {
    if (this.rangeStart && this.rangeEnd && this.rangeStart > this.rangeEnd) {
      const swap = this.rangeStart;
      this.rangeStart = this.rangeEnd;
      this.rangeEnd = swap;
    }
    this.cdr.markForCheck();
  }

  loadSummary(): void {
    if (!this.rangeStart || !this.rangeEnd) {
      this.showAlert('Choose both a start and an end date.', { type: 'warning' });
      return;
    }

    const from = this.rangeStart <= this.rangeEnd ? this.rangeStart : this.rangeEnd;
    const to = this.rangeStart <= this.rangeEnd ? this.rangeEnd : this.rangeStart;
    const fromDate = toDateOnly(from);
    const toDate = toDateOnly(to);

    this.withRequestState(this.attendance.getSummary(fromDate, toDate), {
      errorMessage: 'Unable to load the attendance summary.',
    }).subscribe({
      next: summary => {
        this.summary = summary;
        if (summary?.fromDate) {
          this.rangeStart = parseDateOnly(summary.fromDate) ?? this.rangeStart;
        }
        if (summary?.toDate) {
          this.rangeEnd = parseDateOnly(summary.toDate) ?? this.rangeEnd;
        }
        this.cdr.markForCheck();
      },
      error: error => {
        this.summary = null;
        this.showAlert(this.getHttpErrorMessage(error, 'Unable to load the attendance summary.'), {
          type: 'error',
        });
        this.cdr.markForCheck();
      },
    });
  }

  syncToReportCards(): void {
    if (this.isSyncing) {
      return;
    }

    this.isSyncing = true;
    this.withRequestState(this.attendance.syncTermTotals(), {
      useLoading: false,
      errorMessage: 'Failed to sync attendance to report cards.',
    })
      .pipe(
        finalize(() => {
          this.isSyncing = false;
          this.cdr.markForCheck();
        })
      )
      .subscribe({
        next: result => {
          this.showAlert(result.message, { type: result.success ? 'success' : 'warning' });
          if (result.success) {
            this.loadSummary();
          }
        },
        error: error => {
          this.showAlert(this.getHttpErrorMessage(error, 'Failed to sync attendance to report cards.'), {
            type: 'error',
          });
        },
      });
  }

  printSummary(): void {
    window.print();
  }

  goToFacilitatorRegister(): void {
    this.router.navigate(['/admin/daily-attendance/facilitators']);
  }

  rateClass(rate: number): string {
    return rate >= 75 ? 'rate-strong' : 'rate-weak';
  }

  private startOfDay(date: Date): Date {
    return new Date(date.getFullYear(), date.getMonth(), date.getDate());
  }

  private addDays(date: Date, days: number): Date {
    const copy = new Date(date);
    copy.setDate(copy.getDate() + days);
    return this.startOfDay(copy);
  }
}
