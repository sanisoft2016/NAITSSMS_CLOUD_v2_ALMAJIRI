import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { finalize } from 'rxjs';

import { BaseComponent } from '@shared';

import {
  ATTENDANCE_STATUS_OPTIONS,
  DailyAttendanceRosterEntry,
  DailyAttendanceStatus,
  parseDateOnly,
  toDateOnly,
} from '../daily-attendance.models';
import { DailyAttendanceService } from '../daily-attendance.service';

@Component({
  selector: 'app-facilitator-register',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDatepickerModule, MatFormFieldModule, MatInputModule],
  templateUrl: './facilitator-register.html',
  styleUrls: ['../daily-attendance.shared.scss'],
})
export class FacilitatorRegister extends BaseComponent implements OnInit {
  private readonly attendance = inject(DailyAttendanceService);

  readonly statusOptions = ATTENDANCE_STATUS_OPTIONS;
  readonly maxDate = this.startOfDay(new Date());

  selectedDateValue: Date | null = this.maxDate;
  entries: DailyAttendanceRosterEntry[] = [];
  registerAlreadyTaken = false;
  rosterLoaded = false;

  constructor() {
    super();
  }

  get selectedDate(): string {
    return toDateOnly(this.selectedDateValue);
  }

  ngOnInit(): void {
    this.loadRoster();
  }

  get presentCount(): number {
    return this.countOf(DailyAttendanceStatus.Present);
  }

  get absentCount(): number {
    return this.countOf(DailyAttendanceStatus.Absent);
  }

  get lateCount(): number {
    return this.countOf(DailyAttendanceStatus.Late);
  }

  get excusedCount(): number {
    return this.countOf(DailyAttendanceStatus.Excused);
  }

  onDateChange(date: Date | null): void {
    this.selectedDateValue = date ? this.startOfDay(date) : null;
    this.loadRoster();
  }

  setStatus(entry: DailyAttendanceRosterEntry, status: DailyAttendanceStatus): void {
    entry.status = status;
    if (status === DailyAttendanceStatus.Present) {
      entry.remark = '';
    }
  }

  markAllPresent(): void {
    this.entries.forEach(entry => {
      entry.status = DailyAttendanceStatus.Present;
      entry.remark = '';
    });
    this.showAlert('All facilitators marked present. Adjust the exceptions, then save.', { type: 'info' });
  }

  saveRegister(): void {
    if (this.entries.length === 0) {
      this.showAlert('There are no facilitators on this register.', { type: 'warning' });
      return;
    }

    if (!this.selectedDate) {
      this.showAlert('Choose a date for the register.', { type: 'warning' });
      return;
    }

    if (!this.startSubmitting()) {
      return;
    }

    const payload = {
      attendanceDate: this.selectedDate,
      marks: this.entries.map(entry => ({
        personId: entry.personId,
        status: entry.status,
        remark: entry.remark,
      })),
    };

    this.withRequestState(this.attendance.saveFacilitatorRegister(payload), {
      useLoading: false,
      errorMessage: 'Failed to save the facilitator register.',
    })
      .pipe(finalize(() => this.stopSubmitting()))
      .subscribe({
        next: result => {
          this.showAlert(result.message, { type: result.success ? 'success' : 'warning' });
          if (result.success) {
            this.loadRoster();
          }
          this.cdr.markForCheck();
        },
        error: error => {
          this.showAlert(this.getHttpErrorMessage(error, 'Failed to save the facilitator register.'), {
            type: 'error',
          });
          this.cdr.markForCheck();
        },
      });
  }

  printRegister(): void {
    window.print();
  }

  goToSummary(): void {
    this.router.navigate(['/admin/daily-attendance/summary']);
  }

  private loadRoster(): void {
    if (!this.selectedDate) {
      return;
    }

    this.withRequestState(this.attendance.getFacilitatorRoster(this.selectedDate), {
      errorMessage: 'Unable to load the facilitator register for the selected date.',
    }).subscribe({
      next: roster => {
        this.entries = roster.entries ?? [];
        this.registerAlreadyTaken = roster.registerAlreadyTaken;
        this.rosterLoaded = true;
        if (roster.attendanceDate) {
          this.selectedDateValue = parseDateOnly(roster.attendanceDate) ?? this.selectedDateValue;
        }
        this.cdr.markForCheck();
      },
      error: error => {
        this.entries = [];
        this.registerAlreadyTaken = false;
        this.rosterLoaded = false;
        this.showAlert(
          this.getHttpErrorMessage(error, 'Unable to load the facilitator register for the selected date.'),
          { type: 'error' }
        );
        this.cdr.markForCheck();
      },
    });
  }

  private countOf(status: DailyAttendanceStatus): number {
    return this.entries.filter(entry => entry.status === status).length;
  }

  private startOfDay(date: Date): Date {
    return new Date(date.getFullYear(), date.getMonth(), date.getDate());
  }
}
