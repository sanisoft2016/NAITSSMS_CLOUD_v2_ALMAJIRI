import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
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
} from '../../admin/daily-attendance/daily-attendance.models';
import { DailyAttendanceService } from '../../admin/daily-attendance/daily-attendance.service';
import { AffectivePsychomotorRemarkModal } from '../affective-psychomotor-remark-modal/affective-psychomotor-remark-modal';
import {
  buildFormMasterConcatParameters,
  formatArmName,
  readArmId,
  readClassArmLabel,
  readClassLabelOnly,
  readFormMasterFullName,
  readStudentFullName,
  readStudentGender,
  readStudentId,
  readStudentIdNo,
} from '../teacher-portal.helpers';
import {
  AssignedArmsStudentsResponse,
  FormMasterPanelMode,
  FormMasterReportResponse,
  StudentDetails,
  TeacherClassArmAssignment,
} from '../teacher-portal.models';
import { TeacherPortalService } from '../teacher-portal.service';

type BusyAction =
  | 'view-students'
  | 'ca-report'
  | 'termly-report'
  | 'class-position'
  | 'third-term'
  | 'attendance'
  | null;

type RemarkModalContext = {
  studentId: string;
  admissionNo: string;
  studentName: string;
  studentClass: string;
  studentArm: string;
  armId: string;
};

@Component({
  selector: 'app-assigned-arm-as-form-master',
  imports: [
    CommonModule,
    FormsModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    AffectivePsychomotorRemarkModal,
  ],
  templateUrl: './assigned-arm-as-form-master.html',
  styleUrl: './assigned-arm-as-form-master.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AssignedArmAsFormMaster extends BaseComponent implements OnInit {
  private readonly teacherPortal = inject(TeacherPortalService);
  private readonly attendanceApi = inject(DailyAttendanceService);

  constructor() {
    super();
  }

  readonly assignments = signal<TeacherClassArmAssignment[]>([]);
  readonly busyAction = signal<BusyAction>(null);
  readonly busyArmId = signal('');
  readonly panelMode = signal<FormMasterPanelMode>('none');
  readonly panelMessage = signal('');
  readonly panelClassLabel = signal('');
  readonly armStudents = signal<StudentDetails[]>([]);
  readonly armTermlyAssessmentStatus = signal<string | number | null>(null);
  /** Bound select value ('0' | '1') — kept separate so CD during save does not reset the dropdown. */
  readonly termlyStatusSelect = signal('0');
  readonly panelArmId = signal('');
  readonly statusUpdating = signal(false);
  readonly remarkContext = signal<RemarkModalContext | null>(null);

  readonly statusOptions = ATTENDANCE_STATUS_OPTIONS;
  readonly maxDate = this.startOfDay(new Date());
  readonly attendanceDateValue = signal<Date | null>(this.maxDate);
  readonly attendanceDate = computed(() => toDateOnly(this.attendanceDateValue()));
  readonly attendanceArmId = signal('');
  readonly attendanceArmLabel = signal('');
  readonly attendanceEntries = signal<DailyAttendanceRosterEntry[]>([]);
  readonly attendanceRegisterAlreadyTaken = signal(false);
  readonly attendanceRosterLoaded = signal(false);
  readonly attendanceError = signal('');
  readonly isBusyLoadingAttendance = signal(false);

  readonly attendancePresentCount = computed(() => this.countAttendance(DailyAttendanceStatus.Present));
  readonly attendanceAbsentCount = computed(() => this.countAttendance(DailyAttendanceStatus.Absent));
  readonly attendanceLateCount = computed(() => this.countAttendance(DailyAttendanceStatus.Late));
  readonly attendanceExcusedCount = computed(() => this.countAttendance(DailyAttendanceStatus.Excused));

  readonly readFormMasterFullName = readFormMasterFullName;
  readonly readClassArmLabel = readClassArmLabel;
  readonly readStudentFullName = readStudentFullName;
  readonly readStudentIdNo = readStudentIdNo;
  readonly formatArmName = formatArmName;

  ngOnInit(): void {
    this.loadAssignments();
  }

  isArmTermCompleted(status: string | number | null | undefined = this.armTermlyAssessmentStatus()): boolean {
    return status === 1 || status === '1' || status === 'Completed';
  }

  loadAssignments(): void {
    this.withRequestState(this.teacherPortal.getAssignedArmsAsFormMaster(), {
      errorMessage: 'Unable to load form-master class assignments.',
      rethrow: false,
    }).subscribe(rows => {
      this.assignments.set(rows ?? []);
    });
  }

  isBusy(item: TeacherClassArmAssignment, action: Exclude<BusyAction, null>): boolean {
    return this.busyAction() === action && this.busyArmId() === readArmId(item);
  }

  viewStudents(item: TeacherClassArmAssignment): void {
    const armId = readArmId(item);
    const classLabel = `${readClassLabelOnly(item)}${formatArmName(item.arm?.armName ?? item.Arm?.ArmName)}`;

    if (!armId) {
      alert('Missing class arm information for this assignment.');
      return;
    }

    this.clearAttendancePanel();
    this.busyAction.set('view-students');
    this.busyArmId.set(armId);
    this.panelArmId.set(armId);
    this.withRequestState(this.teacherPortal.getAssignedArmsStudents(armId, classLabel), {
      errorMessage: 'Unable to load arm students.',
      rethrow: false,
    })
      .pipe(
        finalize(() => {
          this.busyAction.set(null);
          this.busyArmId.set('');
        }),
      )
      .subscribe(payload => this.applyStudentsPanel(payload, classLabel, armId));
  }

  /** Legacy AssignedArmsStudents #ArmTermlyAssessmentStatus change → MarkTermCompleted */
  onTermlyAssessmentStatusChange(selectedStatusRaw: string): void {
    const selectedStatus = String(selectedStatusRaw ?? '').trim();
    const armId = this.panelArmId();
    const previous = this.termlyStatusSelect();

    if (!armId || (selectedStatus !== '0' && selectedStatus !== '1')) {
      this.termlyStatusSelect.set(previous);
      return;
    }

    if (selectedStatus === previous || this.statusUpdating()) {
      return;
    }

    // Optimistic UI so change-detection during the request cannot snap the select back.
    this.termlyStatusSelect.set(selectedStatus);
    this.statusUpdating.set(true);

    let settled = false;
    const conCatParameters = `${armId},${selectedStatus}`;
    this.withRequestState(this.teacherPortal.markTermCompleted(conCatParameters), {
      errorMessage: 'Failed! Unable to complete the process.',
      rethrow: false,
    })
      .pipe(
        finalize(() => {
          this.statusUpdating.set(false);
          if (!settled) {
            this.termlyStatusSelect.set(previous);
          }
        }),
      )
      .subscribe(ok => {
        settled = true;
        if (ok === true) {
          this.armTermlyAssessmentStatus.set(selectedStatus === '1' ? 1 : 0);
          alert('The status has successfully been updated.');
          return;
        }

        this.termlyStatusSelect.set(previous);
        alert('Failed! Unable to complete the process.');
      });
  }

  openRemark(student: StudentDetails): void {
    const studentId = readStudentId(student);
    const admissionNo = readStudentIdNo(student);
    const studentName = readStudentFullName(student);
    const arm = student.classArm ?? student.ClassArm;
    const studentClass = String(
      student.currentClass ?? student.CurrentClass ?? arm?.classs?.classLabel ?? arm?.Classs?.ClassLabel ?? '',
    ).trim();
    const studentArm = formatArmName(arm?.armName ?? arm?.ArmName);
    const armId = String(student.classArmId ?? student.ClassArmId ?? arm?.id ?? '').trim();

    if (!studentId || !armId) {
      alert('Missing student or class arm information for remark.');
      return;
    }

    this.remarkContext.set({
      studentId,
      admissionNo,
      studentName,
      studentClass,
      studentArm,
      armId,
    });
  }

  closeRemarkModal(): void {
    this.remarkContext.set(null);
  }

  onRemarkSubmitted(): void {
    // Keep students panel open after successful submit.
  }

  caReportSheet(item: TeacherClassArmAssignment): void {
    this.runReportAction(item, 'ca-report', params => this.teacherPortal.getPerArmCaResult(params));
  }

  processReportSheet(item: TeacherClassArmAssignment): void {
    this.runReportAction(item, 'termly-report', params => this.teacherPortal.getPerArmResult(params));
  }

  thirdTermResultAlone(item: TeacherClassArmAssignment): void {
    this.runReportAction(item, 'third-term', params => this.teacherPortal.getThirdTermResultAlone(params));
  }

  armStudentClassPosition(item: TeacherClassArmAssignment): void {
    const armId = readArmId(item);
    const classAndArmName = readClassArmLabel(item);
    if (!armId) {
      alert('Missing class arm information for this assignment.');
      return;
    }

    this.clearAttendancePanel();
    this.panelMode.set('class-position');
    this.panelClassLabel.set(classAndArmName);
    this.panelMessage.set(
      `Student's Class Position for "${classAndArmName}" requires GetArmStudentAssessmentByArmId API wiring.`,
    );
    this.armStudents.set([]);
  }

  printPanel(): void {
    const content = document.getElementById('pageContent');
    if (!content) {
      return;
    }

    const printWindow = window.open('', '_blank', 'noopener,noreferrer,width=980,height=720');
    if (!printWindow) {
      return;
    }

    printWindow.document.write(`<html><head><title>Form Master</title></head><body>${content.outerHTML}</body></html>`);
    printWindow.document.close();
    printWindow.focus();
    printWindow.print();
    printWindow.close();
  }

  studentAttendance(item: TeacherClassArmAssignment): void {
    const armId = readArmId(item);
    const classLabel = readClassArmLabel(item);

    if (!armId) {
      alert('Missing class arm information for this assignment.');
      return;
    }

    this.armStudents.set([]);
    this.panelMessage.set('');
    this.panelMode.set('attendance');
    this.panelClassLabel.set(classLabel);
    this.attendanceArmId.set(armId);
    this.attendanceArmLabel.set(classLabel);
    this.attendanceError.set('');
    this.busyAction.set('attendance');
    this.busyArmId.set(armId);
    this.isBusyLoadingAttendance.set(true);

    this.fetchAttendanceRoster(armId, () => {
      this.busyAction.set(null);
      this.busyArmId.set('');
      queueMicrotask(() =>
        document.getElementById('attendanceContent')?.scrollIntoView({ behavior: 'smooth', block: 'start' }),
      );
    });
  }

  onAttendanceDateChange(date: Date | null): void {
    this.attendanceDateValue.set(date ? this.startOfDay(date) : null);
    const armId = this.attendanceArmId();
    if (!armId || !this.attendanceDate()) {
      return;
    }

    this.fetchAttendanceRoster(armId);
  }

  setAttendanceStatus(entry: DailyAttendanceRosterEntry, status: DailyAttendanceStatus): void {
    entry.status = status;
    if (status === DailyAttendanceStatus.Present) {
      entry.remark = '';
    }
    this.attendanceEntries.set([...this.attendanceEntries()]);
  }

  markAllPresent(): void {
    const next = this.attendanceEntries().map(entry => ({
      ...entry,
      status: DailyAttendanceStatus.Present,
      remark: '',
    }));
    this.attendanceEntries.set(next);
    this.showAlert('All learners marked present. Adjust the exceptions, then save.', { type: 'info' });
  }

  saveAttendanceRegister(): void {
    const armId = this.attendanceArmId();
    const entries = this.attendanceEntries();

    if (!armId) {
      this.showAlert('No assigned class arm is selected for this register.', { type: 'warning' });
      return;
    }

    if (entries.length === 0) {
      this.showAlert('There are no learners on this register.', { type: 'warning' });
      return;
    }

    if (!this.startSubmitting()) {
      return;
    }

    const payload = {
      attendanceDate: this.attendanceDate(),
      classArmId: armId,
      marks: entries.map(entry => ({
        personId: entry.personId,
        status: entry.status,
        remark: entry.remark,
      })),
    };

    this.withRequestState(this.attendanceApi.saveLearnerRegister(payload), {
      useLoading: false,
      errorMessage: 'Failed to save the register.',
      rethrow: false,
      onError: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to save the register.'), { type: 'error' });
      },
    })
      .pipe(finalize(() => this.stopSubmitting()))
      .subscribe(result => {
        if (!result) {
          return;
        }

        this.showAlert(result.message, { type: result.success ? 'success' : 'warning' });
        if (result.success) {
          this.fetchAttendanceRoster(armId);
        }
        this.cdr.markForCheck();
      });
  }

  printAttendanceRegister(): void {
    window.print();
  }

  private fetchAttendanceRoster(armId: string, afterLoad?: () => void): void {
    const date = this.attendanceDate();
    if (!date) {
      this.showAlert('Choose a date for the register.', { type: 'warning' });
      afterLoad?.();
      return;
    }

    this.attendanceError.set('');
    this.isBusyLoadingAttendance.set(true);

    this.withRequestState(this.attendanceApi.getLearnerRoster(armId, date), {
      useLoading: false,
      errorMessage: 'Unable to load the register for this assigned arm.',
      rethrow: false,
      onError: error => {
        this.attendanceEntries.set([]);
        this.attendanceRegisterAlreadyTaken.set(false);
        this.attendanceRosterLoaded.set(true);
        this.attendanceError.set(
          this.getHttpErrorMessage(error, 'Unable to load the register for this assigned arm.'),
        );
      },
    })
      .pipe(
        finalize(() => {
          this.isBusyLoadingAttendance.set(false);
          this.cdr.markForCheck();
          afterLoad?.();
        }),
      )
      .subscribe(roster => {
        if (!roster) {
          return;
        }

        this.attendanceEntries.set([...(roster.entries ?? [])]);
        if (roster.classArmLabel) {
          this.attendanceArmLabel.set(roster.classArmLabel);
        }
        if (roster.attendanceDate) {
          this.attendanceDateValue.set(parseDateOnly(roster.attendanceDate) ?? this.attendanceDateValue());
        }
        this.attendanceRegisterAlreadyTaken.set(!!roster.registerAlreadyTaken);
        this.attendanceRosterLoaded.set(true);
        this.attendanceError.set('');
        this.cdr.markForCheck();
      });
  }

  private startOfDay(date: Date): Date {
    return new Date(date.getFullYear(), date.getMonth(), date.getDate());
  }

  private clearAttendancePanel(): void {
    this.attendanceArmId.set('');
    this.attendanceArmLabel.set('');
    this.attendanceEntries.set([]);
    this.attendanceRegisterAlreadyTaken.set(false);
    this.attendanceRosterLoaded.set(false);
    this.attendanceError.set('');
    this.isBusyLoadingAttendance.set(false);
  }

  private countAttendance(status: DailyAttendanceStatus): number {
    return this.attendanceEntries().filter(entry => entry.status === status).length;
  }

  private runReportAction(
    item: TeacherClassArmAssignment,
    action: Exclude<BusyAction, null>,
    requestFactory: (concatParameters: string) => ReturnType<TeacherPortalService['getPerArmResult']>,
  ): void {
    const armId = readArmId(item);
    const concatParameters = buildFormMasterConcatParameters(item);
    if (!armId || !concatParameters) {
      alert('Missing class/arm information for this assignment.');
      return;
    }

    this.clearAttendancePanel();
    this.busyAction.set(action);
    this.busyArmId.set(armId);
    this.withRequestState(requestFactory(concatParameters), {
      errorMessage: 'Unable to process report.',
      rethrow: false,
    })
      .pipe(
        finalize(() => {
          this.busyAction.set(null);
          this.busyArmId.set('');
        }),
      )
      .subscribe(payload => this.applyReportPanel(action, payload));
  }

  private applyStudentsPanel(
    payload: AssignedArmsStudentsResponse | null | undefined,
    fallbackLabel: string,
    armId: string,
  ): void {
    const students = [...(payload?.studentDetails ?? payload?.StudentDetails ?? [])].sort((left, right) => {
      const genderCompare = readStudentGender(left).localeCompare(readStudentGender(right));
      if (genderCompare !== 0) {
        return genderCompare;
      }

      return readStudentFullName(left).localeCompare(readStudentFullName(right), undefined, { sensitivity: 'base' });
    });

    const resolvedArmId =
      armId ||
      String(students[0]?.classArmId ?? students[0]?.ClassArmId ?? '').trim();

    this.panelMode.set('students');
    this.panelClassLabel.set(String(payload?.classLabel ?? fallbackLabel).trim());
    this.panelArmId.set(resolvedArmId);
    const status = payload?.armTermlyAssessmentStatus ?? null;
    this.armTermlyAssessmentStatus.set(status);
    this.termlyStatusSelect.set(this.isArmTermCompleted(status) ? '1' : '0');
    this.armStudents.set(students);
    this.panelMessage.set(students.length ? '' : 'No Student in this Class.');
  }

  private applyReportPanel(action: Exclude<BusyAction, null>, payload: FormMasterReportResponse | null | undefined): void {
    const mode: FormMasterPanelMode =
      action === 'ca-report'
        ? 'ca-report'
        : action === 'termly-report'
          ? 'termly-report'
          : action === 'third-term'
            ? 'third-term'
            : 'none';

    this.panelMode.set(mode);
    this.panelClassLabel.set(String(payload?.classLabel ?? '').trim());
    this.armStudents.set([]);

    const message = String(payload?.message ?? '').trim();
    if (message) {
      this.panelMessage.set(message);
      return;
    }

    const rows = payload?.studentsAccessment ?? payload?.StudentsAccessment;
    const count = Array.isArray(rows) ? rows.length : rows ? 1 : 0;
    this.panelMessage.set(
      count > 0
        ? `Report loaded for ${this.panelClassLabel() || 'selected class'} (${count} assessment row(s)).`
        : `No report rows were returned for ${this.panelClassLabel() || 'the selected class'}.`,
    );
  }
}
