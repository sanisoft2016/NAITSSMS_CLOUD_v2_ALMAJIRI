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
} from './teacher-registry.models';

@Component({
  selector: 'app-assign-class-arm-index',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatDividerModule,
  ],
  templateUrl: './assign-class-arm-index.html',
  styleUrl: './assign-class-arm-index.scss',
})
export class AssignClassArmIndex extends TeacherAssignmentPageBase implements OnInit {
  private readonly teacherRegistry = inject(TeacherRegistryService);

  classOptions: TeacherSelectOptionRecord[] = [];

  selectedClassId = '';

  contentMode: 'blank' | 'arms' = 'blank';
  armResult: TeacherAssignmentArmResultRecord = { arms: [], message: '' };

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.initializeTeacherSelection(['teacherId', 'TeachersName'], ['teacherName', 'teachersName']);
    this.loadOptions();
  }

  onClassChange(classId: string): void {
    this.selectedClassId = classId;
    this.contentMode = 'blank';
    this.armResult = { arms: [], message: '' };

    if (!classId) {
      return;
    }

    this.withRequestState(this.teacherRegistry.getClassArmAssignmentArms(classId), {
      errorMessage: 'Failed to load available arms.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.armResult = result ?? { arms: [], message: '' };
      this.contentMode = 'arms';
    });
  }

  assignArm(armId: string): void {
    if (!this.selectedTeacherId) {
      this.showAlert('Please select a teacher before continuing.', { type: 'warning' });
      return;
    }

    this.executeAssignmentRequest(
      this.teacherRegistry.assignFormMaster({
        teacherId: this.selectedTeacherId,
        armId,
      }),
      result => {
        this.armResult = result;
        this.contentMode = 'arms';
      },
    );
  }

  openTransferAssignment(): void {
    this.router.navigate(['/admin/teachers/transfer-class-arm-assignment']);
  }

  private loadOptions(): void {
    this.withRequestState(this.teacherRegistry.getClassArmAssignmentOptions(), {
      errorMessage: 'Failed to load class-arm assignment options.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.teacherOptions = result?.teachers ?? [];
      this.classOptions = result?.classes ?? [];
      this.syncLockedTeacherName();
    });
  }
}