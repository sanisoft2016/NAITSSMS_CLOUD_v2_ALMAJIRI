import { ParamMap } from '@angular/router';
import { Observable } from 'rxjs';

import { BaseComponent } from '@shared';

import {
  TeacherAssignmentArmResultRecord,
  TeacherSelectOptionRecord,
} from './teacher-registry.models';

const ARM_NAME_LABELS = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'N', 'O', 'NoArm'];

export abstract class TeacherAssignmentPageBase extends BaseComponent {
  teacherOptions: TeacherSelectOptionRecord[] = [];
  selectedTeacherId = '';
  selectedTeacherName = '';
  isTeacherLocked = false;
  isAssigning = false;

  protected constructor() {
    super();
  }

  goBack(): void {
    this.router.navigate(['/admin/teachers']);
  }

  showTransferAssignmentInfo(): void {
    this.showAlert('Class arm transfer assignment is not available yet.', { type: 'warning' });
  }

  onTeacherChange(teacherId: string): void {
    this.selectedTeacherId = teacherId;
    this.selectedTeacherName = this.findTeacherName(teacherId);
  }

  classLabel(classLabel: unknown): string {
    return String(classLabel ?? '').trim() || '-';
  }

  armLabel(armName: unknown): string {
    if (typeof armName === 'number') {
      return ARM_NAME_LABELS[armName] ?? String(armName);
    }

    const normalized = String(armName ?? '').trim();
    if (/^\d+$/.test(normalized)) {
      return ARM_NAME_LABELS[Number(normalized)] ?? normalized;
    }

    return normalized;
  }

  protected initializeTeacherSelection(teacherIdKeys: string[], teacherNameKeys: string[]): void {
    const queryParams = this.route.snapshot.queryParamMap;
    this.selectedTeacherId = this.firstQueryParamValue(queryParams, teacherIdKeys);
    this.selectedTeacherName = this.firstQueryParamValue(queryParams, teacherNameKeys);
    this.isTeacherLocked = !!this.selectedTeacherId;
  }

  protected syncLockedTeacherName(): void {
    if (this.isTeacherLocked && !this.selectedTeacherName) {
      this.selectedTeacherName = this.findTeacherName(this.selectedTeacherId) || this.selectedTeacherId;
    }
  }

  protected executeAssignmentRequest(
    request$: Observable<TeacherAssignmentArmResultRecord>,
    onSuccess: (result: TeacherAssignmentArmResultRecord) => void,
  ): void {
    if (this.isAssigning) {
      return;
    }

    this.isAssigning = true;

    const subscription = this.withRequestState(request$, {
      useLoading: false,
      errorMessage: 'Failed! We were unable to complete the request.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      onSuccess(result ?? { arms: [], message: '' });
    });

    subscription.add(() => {
      this.isAssigning = false;
      this.cdr.markForCheck();
    });
  }

  private firstQueryParamValue(queryParams: ParamMap, keys: string[]): string {
    for (const key of keys) {
      const value = queryParams.get(key);
      if (value) {
        return value;
      }
    }

    return '';
  }

  private findTeacherName(teacherId: string): string {
    return this.teacherOptions.find(option => option.value === teacherId)?.text ?? '';
  }
}