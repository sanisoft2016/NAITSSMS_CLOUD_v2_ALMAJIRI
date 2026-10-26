import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

import { TeacherAssignmentPageBase } from './teacher-assignment-page.base';
import { TeacherRegistryService } from './teacher-registry.service';
import {
  TeacherClassAssignmentRecord,
  TeacherSelectOptionRecord,
  TeacherUserRecord,
} from './teacher-registry.models';

@Component({
  selector: 'app-transfer-class-arm-assignment',
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule, MatCardModule, MatIconModule],
  templateUrl: './transfer-class-arm-assignment.html',
  styleUrl: './transfer-subject-assignment.scss',
})
export class TransferClassArmAssignment extends TeacherAssignmentPageBase implements OnInit {
  private readonly teacherRegistry = inject(TeacherRegistryService);

  currentTeacherId = '';
  proposedTeacherId = '';
  teacherSearchTerm = '';
  assignments: TeacherClassAssignmentRecord[] = [];
  selectedAssignmentIds = new Set<string>();
  teachersLoading = false;
  assignmentsLoading = false;
  isTransferring = false;

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.loadTeacherOptions();
  }

  get proposedTeacherOptions(): TeacherSelectOptionRecord[] {
    return this.teacherOptions.filter(option => option.value !== this.currentTeacherId);
  }

  get displayAssignments(): TeacherClassAssignmentRecord[] {
    return [...this.assignments].sort((left, right) =>
      this.compareText(this.assignmentClassArmLabel(left), this.assignmentClassArmLabel(right)),
    );
  }

  get allAssignmentsSelected(): boolean {
    const rows = this.displayAssignments;
    return rows.length > 0 && rows.every(assignment => this.selectedAssignmentIds.has(this.assignmentId(assignment)));
  }

  onCurrentTeacherChange(teacherId: string): void {
    this.currentTeacherId = teacherId;
    this.assignments = [];
    this.selectedAssignmentIds.clear();

    if (!teacherId) {
      this.assignmentsLoading = false;
      this.cdr.markForCheck();
      return;
    }

    this.loadAssignments(teacherId);
  }

  submitTeacherSearch(): void {
    const searchValue = this.teacherSearchTerm.trim();
    if (!searchValue) {
      this.loadTeacherOptions();
      return;
    }

    this.teachersLoading = true;
    this.cdr.markForCheck();

    const subscription = this.withRequestState(this.teacherRegistry.searchFormTeachers(searchValue), {
      useLoading: false,
      errorMessage: 'Failed to search teachers.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.teacherOptions = this.mapFormTeachersToOptions(result);
      this.cdr.markForCheck();
    });

    subscription.add(() => {
      this.teachersLoading = false;
      this.cdr.markForCheck();
    });
  }

  toggleSelectAll(checked: boolean): void {
    this.selectedAssignmentIds.clear();

    if (checked) {
      for (const assignment of this.displayAssignments) {
        this.selectedAssignmentIds.add(this.assignmentId(assignment));
      }
    }

    this.cdr.markForCheck();
  }

  toggleAssignment(assignmentId: string, checked: boolean): void {
    if (checked) {
      this.selectedAssignmentIds.add(assignmentId);
    } else {
      this.selectedAssignmentIds.delete(assignmentId);
    }

    this.cdr.markForCheck();
  }

  isAssignmentSelected(assignmentId: string): boolean {
    return this.selectedAssignmentIds.has(assignmentId);
  }

  assignmentFullName(assignment: TeacherClassAssignmentRecord): string {
    const fromEmployee = this.teacherFullName(assignment.employee?.user);
    if (fromEmployee) {
      return fromEmployee;
    }

    return this.currentTeacherLabel();
  }

  assignmentUserName(assignment: TeacherClassAssignmentRecord): string {
    const userName = assignment.employee?.user?.userName;
    if (userName?.trim()) {
      return userName.trim();
    }

    return '-';
  }

  assignmentClassArmLabel(assignment: TeacherClassAssignmentRecord): string {
    const classArmLabel = (assignment as TeacherClassAssignmentRecord & { classArmLabel?: string }).classArmLabel;
    if (classArmLabel?.trim()) {
      return classArmLabel.trim();
    }

    const classLabel = this.classLabel(assignment.arm?.classs?.classLabel);
    const arm = this.armLabel(assignment.arm?.armName);
    return arm ? `${classLabel} ${arm}`.trim() : classLabel;
  }

  assignmentId(assignment: TeacherClassAssignmentRecord): string {
    return String(assignment.id);
  }

  transferAssignments(): void {
    if (!this.currentTeacherId) {
      this.showAlert('Select the current teacher whose assignments you want to transfer.', { type: 'warning' });
      return;
    }

    if (!this.proposedTeacherId) {
      this.showAlert('Select the proposed teacher to receive the assignments.', { type: 'warning' });
      return;
    }

    if (this.currentTeacherId === this.proposedTeacherId) {
      this.showAlert('Current teacher and proposed teacher must be different.', { type: 'warning' });
      return;
    }

    const assignmentIds = [...this.selectedAssignmentIds];
    if (!assignmentIds.length) {
      this.showAlert('Select at least one class-arm assignment to transfer.', { type: 'warning' });
      return;
    }

    if (this.isTransferring) {
      return;
    }

    this.isTransferring = true;
    this.cdr.markForCheck();

    const subscription = this.withRequestState(
      this.teacherRegistry.transferArmAssignments({
        assignmentIds,
        proposedTeacherId: this.proposedTeacherId,
      }),
      {
        useLoading: false,
        errorMessage: 'Failed to transfer class-arm assignments.',
        notifyOnError: 'alert',
        rethrow: false,
      },
    ).subscribe(result => {
      if (result?.success) {
        this.showAlert('Class-arm assignments transferred successfully.', { type: 'success' });
        this.selectedAssignmentIds.clear();
        this.loadAssignments(this.currentTeacherId);
        return;
      }

      this.showAlert('Failed to transfer class-arm assignments.', { type: 'error' });
    });

    subscription.add(() => {
      this.isTransferring = false;
      this.cdr.markForCheck();
    });
  }

  goBackToAssignment(): void {
    this.router.navigate(['/admin/teachers/assign-class-arm-index']);
  }

  private loadAssignments(teacherId: string): void {
    this.assignmentsLoading = true;
    this.cdr.markForCheck();

    const subscription = this.withRequestState(this.teacherRegistry.getTeacherAssignedArms(teacherId), {
      useLoading: false,
      errorMessage: 'Failed to load teacher class-arm assignments.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.assignments = result ?? [];
      this.cdr.markForCheck();
    });

    subscription.add(() => {
      this.assignmentsLoading = false;
      this.cdr.markForCheck();
    });
  }

  private loadTeacherOptions(): void {
    this.teachersLoading = true;
    this.cdr.markForCheck();

    const subscription = this.withRequestState(this.teacherRegistry.getClassArmAssignmentOptions(), {
      useLoading: false,
      errorMessage: 'Failed to load teacher options.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.teacherOptions = result?.teachers ?? [];
      this.cdr.markForCheck();
    });

    subscription.add(() => {
      this.teachersLoading = false;
      this.cdr.markForCheck();
    });
  }

  private mapFormTeachersToOptions(
    assignments: TeacherClassAssignmentRecord[] | null | undefined,
  ): TeacherSelectOptionRecord[] {
    const seen = new Set<string>();

    return (assignments ?? [])
      .map(assignment => {
        const value = String(assignment.employee?.userId ?? assignment.employee?.user?.id ?? '').trim();
        const text =
          this.teacherFullName(assignment.employee?.user) ||
          String(assignment.employee?.user?.userName ?? '').trim();
        return { value, text };
      })
      .filter(option => option.value && option.text && !seen.has(option.value) && seen.add(option.value))
      .sort((left, right) => left.text.localeCompare(right.text));
  }

  private currentTeacherLabel(): string {
    return this.teacherOptions.find(option => option.value === this.currentTeacherId)?.text ?? '-';
  }

  private teacherFullName(user: TeacherUserRecord | null | undefined): string {
    if (!user) {
      return '';
    }

    const record = user as TeacherUserRecord & {
      FullName?: string;
      FirstName?: string;
      MiddleName?: string;
      SurName?: string;
      LastName?: string;
    };

    const directValue = String(record.fullName ?? record.FullName ?? '').trim();
    if (directValue) {
      return directValue;
    }

    return [
      record.firstName ?? record.FirstName,
      record.middleName ?? record.MiddleName,
      record.surName ?? record.SurName,
      record.lastName ?? record.LastName,
    ]
      .map(value => String(value ?? '').trim())
      .filter(Boolean)
      .join(' ');
  }

  private compareText(left: unknown, right: unknown): number {
    return String(left ?? '').trim().localeCompare(String(right ?? '').trim());
  }
}
