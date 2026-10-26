import { HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { GenericService } from '@shared';

import {
  ArmName,
  ClassArmDto,
  ClassDto,
} from '@shared/interfaces/class.interface';
import {
  DailyAttendanceRoster,
  DailyAttendanceSaveResult,
  DailyAttendanceSubmit,
  DailyAttendanceSummary,
  SyncTermAttendanceResult,
} from './daily-attendance.models';

export interface AttendanceSelectOption {
  value: string;
  label: string;
}

@Injectable({ providedIn: 'root' })
export class DailyAttendanceService {
  private readonly api = inject(GenericService<unknown>);

  getClasses(): Observable<AttendanceSelectOption[]> {
    return this.api
      .getAnObj<{ data?: ClassDto[] }>('/Registry/student-results/classes')
      .pipe(
        map(response =>
          (response?.data ?? []).map(item => ({
            value: item.id,
            label: item.classLabel?.trim() || `Class ${item.className}`,
          }))
        )
      );
  }

  getArms(classId: string): Observable<AttendanceSelectOption[]> {
    return this.api
      .getAnObj<{ data?: ClassArmDto[] }>(`/Registry/student-results/classes/${classId}/arms`)
      .pipe(
        map(response =>
          (response?.data ?? []).map(item => ({
            value: item.id,
            label: `Arm ${ArmName[item.armName] ?? ''}`.trim(),
          }))
        )
      );
  }

  getLearnerRoster(armId: string, date: string): Observable<DailyAttendanceRoster> {
    const params = new HttpParams().set('armId', armId).set('date', date);
    return this.api.getAnObj<DailyAttendanceRoster>('/Registry/daily-attendance/learners', { params });
  }

  saveLearnerRegister(payload: DailyAttendanceSubmit): Observable<DailyAttendanceSaveResult> {
    return this.api.create<DailyAttendanceSaveResult>('/Registry/daily-attendance/learners', payload);
  }

  getFacilitatorRoster(date: string): Observable<DailyAttendanceRoster> {
    const params = new HttpParams().set('date', date);
    return this.api.getAnObj<DailyAttendanceRoster>('/Registry/daily-attendance/facilitators', { params });
  }

  saveFacilitatorRegister(payload: DailyAttendanceSubmit): Observable<DailyAttendanceSaveResult> {
    return this.api.create<DailyAttendanceSaveResult>('/Registry/daily-attendance/facilitators', payload);
  }

  getSummary(from: string, to: string): Observable<DailyAttendanceSummary> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.api.getAnObj<DailyAttendanceSummary>('/Registry/daily-attendance/summary', { params });
  }

  syncTermTotals(): Observable<SyncTermAttendanceResult> {
    return this.api.create<SyncTermAttendanceResult>('/Registry/daily-attendance/sync-term-totals', {});
  }
}
