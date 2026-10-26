import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnInit,
  Output,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';

import { BaseComponent } from '@shared';

import { TeacherPortalService } from '../teacher-portal.service';

type RemarkItemRow = {
  itemId: number;
  itemsName: string;
  score: number | null;
};

@Component({
  selector: 'app-affective-psychomotor-remark-modal',
  imports: [CommonModule, FormsModule],
  templateUrl: './affective-psychomotor-remark-modal.html',
  styleUrl: './affective-psychomotor-remark-modal.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AffectivePsychomotorRemarkModal extends BaseComponent implements OnInit {
  private readonly teacherPortal = inject(TeacherPortalService);

  constructor() {
    super();
  }

  @Input({ required: true }) studentId = '';
  @Input({ required: true }) admissionNo = '';
  @Input({ required: true }) studentName = '';
  @Input({ required: true }) studentClass = '';
  @Input({ required: true }) studentArm = '';
  @Input({ required: true }) armId = '';

  @Output() readonly closed = new EventEmitter<void>();
  @Output() readonly submitted = new EventEmitter<void>();

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly mode = signal<'create' | 'reupdate' | 'empty'>('create');
  readonly message = signal('');
  readonly totalAttendance = signal(0);
  readonly attendance = signal<number | null>(0);
  readonly teacherComment = signal('');
  readonly schoolHeadComment = signal('');
  readonly items = signal<RemarkItemRow[]>([]);

  readonly affectiveItems = computed(() => this.items().slice(0, 6));
  readonly psychomotorItems = computed(() => this.items().slice(6));

  ngOnInit(): void {
    this.loadRemark();
  }

  loadRemark(): void {
    const concatParameter = [
      this.studentId,
      this.admissionNo,
      this.studentName,
      this.studentClass,
      this.studentArm,
      this.armId,
    ].join(',');

    this.loading.set(true);
    this.withRequestState(this.teacherPortal.getAffectivePsychomotorRemark(concatParameter), {
      errorMessage: 'Unable to load remark form.',
      rethrow: false,
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe(payload => {
        const mode = (payload?.mode ?? 'empty') as 'create' | 'reupdate' | 'empty';
        this.mode.set(mode);
        this.message.set(String(payload?.message ?? '').trim());
        this.totalAttendance.set(Number(payload?.totalAttendance ?? 0));
        this.attendance.set(Number(payload?.attendance ?? 0));
        this.teacherComment.set(String(payload?.teacherRemarks ?? ''));
        this.schoolHeadComment.set(String(payload?.schoolHeadRemarks ?? ''));

        const rawItems = Array.isArray(payload?.items) ? payload.items : [];
        this.items.set(
          rawItems.map((item: any) => ({
            itemId: Number(item.itemId ?? item.ItemId ?? item.id ?? item.Id ?? 0),
            itemsName: String(
              item.itemsName ??
                item.ItemsName ??
                item.affectiveAndPsychomotor?.itemsName ??
                item.AffectiveAndPsychomotor?.ItemsName ??
                '',
            ),
            score:
              item.score == null && item.Score == null
                ? null
                : Number(item.score ?? item.Score ?? 0),
          })),
        );
      });
  }

  updateItemScore(itemId: number, rawValue: string | number | null): void {
    const parsed = rawValue == null || rawValue === '' ? null : Number(rawValue);
    this.items.update(rows =>
      rows.map(row => (row.itemId === itemId ? { ...row, score: parsed } : row)),
    );
  }

  setAttendance(rawValue: string | number | null): void {
    this.attendance.set(rawValue === '' || rawValue == null ? null : Number(rawValue));
  }

  /**
   * Legacy affective/psychomotor score rule (SubmitEntries): score must be <= 5.
   * Applied on focus-out so invalid values are caught before submit.
   */
  onScoreFocusOut(itemId: number, event: FocusEvent): void {
    const input = event.target as HTMLInputElement | null;
    if (!input) {
      return;
    }

    const raw = String(input.value ?? '').trim();
    if (raw === '') {
      this.updateItemScore(itemId, null);
      return;
    }

    const value = Number(raw);
    if (Number.isNaN(value) || value > 5) {
      alert('Value out of range.');
      this.updateItemScore(itemId, null);
      input.value = '';
      queueMicrotask(() => input.focus());
    }
  }

  /** Legacy attendance checks from SubmitEntries, applied on focus-out. */
  onAttendanceFocusOut(event: FocusEvent): void {
    const input = event.target as HTMLInputElement | null;
    if (!input) {
      return;
    }

    const raw = String(input.value ?? '').trim();
    if (raw === '') {
      return;
    }

    const attendanceNo = Number(raw);
    const totalAttendance = this.totalAttendance();
    if (Number.isNaN(attendanceNo)) {
      return;
    }

    if (attendanceNo > totalAttendance) {
      alert(`value out of range. The maximum value for attendance is '${totalAttendance}'`);
      this.attendance.set(null);
      input.value = '';
      queueMicrotask(() => input.focus());
      return;
    }

    if (totalAttendance < 1) {
      alert('value out of range. You need set the total attendance in the admin environment.');
      this.attendance.set(null);
      input.value = '';
      queueMicrotask(() => input.focus());
    }
  }

  submitEntries(): void {
    // Legacy SubmitEntries validation
    const rows = this.items();
    for (const row of rows) {
      if (row.score != null && Number(row.score) > 5) {
        alert('Value out of range.');
        queueMicrotask(() => {
          const el = document.getElementById(`Id${row.itemId}`) as HTMLInputElement | null;
          el?.focus();
        });
        return;
      }
    }

    const attendanceNo = Number(this.attendance());
    const totalAttendance = this.totalAttendance();
    if (Number.isNaN(attendanceNo)) {
      alert('One or more required Value were not entered.');
      queueMicrotask(() => document.getElementById('attendanceNo')?.focus());
      return;
    }
    if (attendanceNo > totalAttendance) {
      alert(`value out of range. The maximum value for attendance is '${totalAttendance}'`);
      this.attendance.set(null);
      queueMicrotask(() => document.getElementById('attendanceNo')?.focus());
      return;
    }
    if (totalAttendance < 1) {
      alert('value out of range. You need set the total attendance in the admin environment.');
      this.attendance.set(null);
      queueMicrotask(() => document.getElementById('attendanceNo')?.focus());
      return;
    }

    const teacherComment = this.teacherComment();
    const schoolHeadComment = this.schoolHeadComment();
    if (teacherComment.length > 150 || schoolHeadComment.length > 150) {
      alert('Comment should not be more than 150 character length');
      return;
    }

    const scores = rows.map(row => ({
      ItemId: row.itemId,
      Score: String(row.score ?? 0),
    }));

    this.saving.set(true);
    this.withRequestState(
      this.teacherPortal.submitAffectivePsychomotorScores({
        studentId: this.studentId,
        armId: this.armId,
        attendance: attendanceNo,
        teacherComment,
        schoolHeadComment,
        scores,
      }),
      {
        errorMessage: 'Failed to submit remark.',
        rethrow: false,
      },
    )
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe(() => {
        alert('Your enteries has Successfully been Submited.');
        this.submitted.emit();
        this.closed.emit();
      });
  }

  closeModal(): void {
    this.closed.emit();
  }
}
