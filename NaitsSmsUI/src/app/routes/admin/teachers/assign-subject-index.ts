import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDividerModule } from '@angular/material/divider';

import { TeacherAssignmentPageBase } from './teacher-assignment-page.base';
import { TeacherRegistryService } from './teacher-registry.service';
import {
  TeacherAssignmentArmResultRecord,
  TeacherSelectOptionRecord,
  TeacherUnassignedSubjectRecord,
} from './teacher-registry.models';

@Component({
  selector: 'app-assign-subject-index',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatDividerModule,
  ],
  templateUrl: './assign-subject-index.html',
  styleUrl: './assign-subject-index.scss',
})
export class AssignSubjectIndex extends TeacherAssignmentPageBase implements OnInit {
  private readonly teacherRegistry = inject(TeacherRegistryService);

  classOptions: TeacherSelectOptionRecord[] = [];
  subjectOptions: TeacherSelectOptionRecord[] = [];

  selectedClassId = '';
  selectedSubjectId = '';

  unassignedButtonLabel = 'List of Subject Un-assigned Teachers';

  contentMode: 'blank' | 'arms' | 'unassigned' = 'blank';
  armResult: TeacherAssignmentArmResultRecord = { arms: [], message: '' };
  unassignedSubjects: TeacherUnassignedSubjectRecord[] = [];

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.initializeTeacherSelection(['teacherId', 'teachers'], ['teacherName', 'teachersName']);
    this.loadOptions();
  }

  get sortedUnassignedSubjects(): TeacherUnassignedSubjectRecord[] {
    return [...this.unassignedSubjects].sort((left, right) => {
      const classSort = this.compareValue(left.className, right.className);
      if (classSort !== 0) {
        return classSort;
      }

      return this.compareValue(left.armName, right.armName);
    });
  }

  onClassChange(classId: string): void {
    this.selectedClassId = classId;
    this.selectedSubjectId = '';
    this.subjectOptions = [];
    this.contentMode = 'blank';
    this.armResult = { arms: [], message: '' };
    this.unassignedSubjects = [];

    if (!classId) {
      return;
    }

    this.withRequestState(this.teacherRegistry.getSubjectAssignmentSubjects(classId), {
      errorMessage: 'Failed to load subject options.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.subjectOptions = result ?? [];
    });
  }

  onSubjectChange(subjectId: string): void {
    this.selectedSubjectId = subjectId;
    this.unassignedSubjects = [];

    if (!subjectId || subjectId === 'default') {
      this.contentMode = 'blank';
      this.armResult = { arms: [], message: '' };
      return;
    }

    this.loadArms();
  }

  openTransferAssignment(): void {
    this.router.navigate(['/admin/teachers/transfer-subject-assignment']);
  }

  assignArm(armId: string): void {
    if (!this.selectedTeacherId) {
      this.showAlert('Please select a teacher before continuing.', { type: 'warning' });
      return;
    }

    if (!this.selectedSubjectId || this.selectedSubjectId === 'default') {
      this.showAlert('Please select a subject before continuing.', { type: 'warning' });
      return;
    }

    this.executeAssignmentRequest(
      this.teacherRegistry.assignSubjectTeacher({
        teacherId: this.selectedTeacherId,
        subjectId: this.selectedSubjectId,
        armId,
      }),
      result => {
        this.armResult = result;
        this.contentMode = 'arms';
      },
    );
  }

  listSubjectUnassignedTeachers(): void {
    this.unassignedButtonLabel = 'Please Wait ...';

    const subscription = this.withRequestState(this.teacherRegistry.getSubjectUnassignedTeachers(), {
      useLoading: false,
      errorMessage: 'Failed to load subject un-assigned teachers.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.unassignedSubjects = result ?? [];
      this.contentMode = 'unassigned';
      this.armResult = { arms: [], message: '' };
    });

    subscription.add(() => {
      this.unassignedButtonLabel = 'List of Subject Un-assigned Teachers';
      this.cdr.markForCheck();
    });
  }

  private loadOptions(): void {
    this.withRequestState(this.teacherRegistry.getSubjectAssignmentOptions(), {
      errorMessage: 'Failed to load teacher assignment options.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.teacherOptions = result?.teachers ?? [];
      this.classOptions = result?.classes ?? [];
      this.syncLockedTeacherName();
    });
  }

  private loadArms(): void {
    this.withRequestState(this.teacherRegistry.getSubjectAssignmentArms(this.selectedSubjectId, this.selectedClassId), {
      errorMessage: 'Failed to load available arms.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.armResult = result ?? { arms: [], message: '' };
      this.contentMode = 'arms';
    });
  }

  private compareValue(left: unknown, right: unknown): number {
    const leftText = String(left ?? '').trim();
    const rightText = String(right ?? '').trim();

    const leftNumber = Number(leftText);
    const rightNumber = Number(rightText);
    if (!Number.isNaN(leftNumber) && !Number.isNaN(rightNumber)) {
      return leftNumber - rightNumber;
    }

    return leftText.localeCompare(rightText);
  }
}
