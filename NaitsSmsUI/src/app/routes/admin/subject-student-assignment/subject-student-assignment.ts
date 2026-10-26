import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';

import { BaseComponent, CurrentClass } from '@shared';

import {
  SelectOptionRecord,
  SubjectSelectionRecord,
  SubjectStudentRecord,
  SubjectStudentsResponse,
  classCategoryLabel,
} from '../subjects/subject-registry.models';
import { SubjectRegistryService } from '../subjects/subject-registry.service';

@Component({
  selector: 'app-subject-student-assignment',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './subject-student-assignment.html',
  styleUrl: './subject-student-assignment.scss',
})
export class SubjectStudentAssignment extends BaseComponent implements OnInit {
  private readonly allStudentsCategoryValue = 'all';
  private readonly subjectRegistry = inject(SubjectRegistryService);

  readonly categoryOptions: Array<{ value: string; label: string }> = [
    { value: this.allStudentsCategoryValue, label: 'All Students In The Selected Arm' },
    { value: '2', label: 'Art Students' },
    { value: '3', label: 'Science Students' },
    { value: '4', label: 'Commercial Students' },
  ];

  subjectOptions: SelectOptionRecord[] = [];
  classOptions: SelectOptionRecord[] = [];
  armOptions: SelectOptionRecord[] = [];

  selectedSubjectId = '';
  selectedClassId = '';
  selectedArmId = '';
  selectedCategory = this.allStudentsCategoryValue;

  students: SubjectStudentRecord[] = [];
  assignedStudentIds = new Set<string>();
  selectedStudentIds = new Set<string>();
  teacherAssignmentMessage = '';

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.loadContext();
  }

  backToIndex(): void {
    this.router.navigate(['/admin/subjects']);
  }

  onClassChange(classId: string): void {
    this.selectedClassId = classId;
    this.selectedArmId = '';
    this.armOptions = [];
    this.clearStudentBoard();

    if (!classId) {
      this.cdr.markForCheck();
      return;
    }

    this.withRequestState(this.subjectRegistry.getStudentAssignmentArms(classId, this.selectedSubjectId), {
      errorMessage: 'Unable to load arms for the selected class.',
    }).subscribe({
      next: response => {
        this.armOptions = response.arms ?? [];
        this.teacherAssignmentMessage = response.message ?? '';
        this.cdr.markForCheck();
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Unable to load arms for the selected class.'), { type: 'error' });
        this.cdr.markForCheck();
      },
    });
  }

  onSubjectChange(subjectId: string): void {
    this.selectedSubjectId = subjectId;
    this.clearStudentBoard();
    if (this.selectedClassId) {
      this.onClassChange(this.selectedClassId);
      return;
    }

    this.cdr.markForCheck();
  }

  onArmChange(armId: string): void {
    this.selectedArmId = armId;
    this.loadStudentsForCurrentFilter();
  }

  onCategoryChange(category: string): void {
    this.selectedCategory = category;
    this.loadStudentsForCurrentFilter();
  }

  isStudentChecked(studentId: string): boolean {
    return this.selectedStudentIds.has(studentId);
  }

  toggleStudentSelection(studentId: string, checked: boolean): void {
    if (checked) {
      this.selectedStudentIds.add(studentId);
      return;
    }

    this.selectedStudentIds.delete(studentId);
  }

  toggleSelectAll(checked: boolean): void {
    if (checked) {
      this.selectedStudentIds = new Set(this.students.map(student => student.id));
      return;
    }

    this.selectedStudentIds.clear();
  }

  isAllSelected(): boolean {
    return this.students.length > 0 && this.selectedStudentIds.size === this.students.length;
  }

  submitAssignments(): void {
    if (!this.selectedSubjectId || !this.selectedArmId) {
      this.showAlert('Select a subject and arm before submitting.', { type: 'warning' });
      return;
    }

    const selectedIds = this.students
      .map(student => student.id)
      .filter(studentId => this.selectedStudentIds.has(studentId));

    const payloadBase: SubjectSelectionRecord[] = [
      { Id: this.selectedArmId },
      { Id: this.selectedSubjectId },
    ];

    if (selectedIds.length > 0) {
      const payload = [
        ...payloadBase,
        ...selectedIds.map(studentId => ({ Id: studentId })),
        { Id: this.selectedCategory },
      ];

      if (!this.startSubmitting()) {
        return;
      }

      this.withRequestState(this.subjectRegistry.assignSubjectToStudents(payload), {
        useLoading: false,
        errorMessage: 'Unable to assign subject to the selected students.',
      })
        .pipe(finalize(() => this.stopSubmitting()))
        .subscribe({
          next: response => {
            if (response.data?.trim()) {
              this.showAlert(response.data.trim(), { type: 'success', duration: 6000 });
            } else {
              this.showAlert(response.message || 'Subject assigned successfully.', { type: 'success' });
            }
            this.loadStudentsForCurrentFilter();
          },
          error: error => {
            this.showAlert(this.getHttpErrorMessage(error, 'Unable to assign subject to the selected students.'), { type: 'error' });
          },
        });
      return;
    }

    const idsToRemove = this.students
      .map(student => student.id)
      .filter(studentId => this.assignedStudentIds.has(studentId));

    if (idsToRemove.length === 0) {
      this.showAlert('Tick students to assign, or load already assigned students before removing.', { type: 'warning' });
      return;
    }

    if (!this.startSubmitting()) {
      return;
    }

    const removalPayload = [
      ...payloadBase,
      ...idsToRemove.map(studentId => ({ Id: studentId })),
    ];

    this.withRequestState(this.subjectRegistry.unassignSubjectFromStudents(removalPayload), {
      useLoading: false,
      errorMessage: 'Unable to remove subject from the selected students.',
    })
      .pipe(finalize(() => this.stopSubmitting()))
      .subscribe({
        next: response => {
          this.showAlert(response.message || 'Subject removed successfully.', { type: 'success' });
          this.loadStudentsForCurrentFilter();
        },
        error: error => {
          this.showAlert(this.getHttpErrorMessage(error, 'Unable to remove subject from the selected students.'), { type: 'error' });
        },
      });
  }
  getCategoryLabel(
    value: SubjectStudentRecord['classCategory'],
    currentClass?: SubjectStudentRecord['currentClass'],
  ): string {
    const categoryLabel = this.resolveCategoryLabel(value);
    if (categoryLabel) {
      return categoryLabel;
    }

    return this.resolveCategoryFromCurrentClass(currentClass) ?? '-';
  }

  private loadContext(): void {
    this.withRequestState(this.subjectRegistry.getStudentAssignmentContext(), {
      errorMessage: 'Unable to load subject assignment context.',
    }).subscribe({
      next: context => {
        this.subjectOptions = context.electiveSubjectsDropdown ?? [];
        this.classOptions = context.classLabelDropdown ?? [];
        this.cdr.markForCheck();
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Unable to load subject assignment context.'), { type: 'error' });
        this.cdr.markForCheck();
      },
    });
  }

  private loadStudentsForCurrentFilter(): void {
    if (!this.selectedSubjectId || !this.selectedArmId) {
      this.clearStudentBoard();
      return;
    }

    const request$ = this.selectedCategory === this.allStudentsCategoryValue
      ? this.subjectRegistry.getStudentAssignmentStudents(this.selectedArmId, this.selectedSubjectId)
      : this.subjectRegistry.getStudentAssignmentStudentsByCategory(this.selectedCategory, this.selectedArmId, this.selectedSubjectId);

    this.withRequestState(request$, {
      errorMessage: 'Unable to load students for the selected arm.',
    }).subscribe({
      next: response => this.applyStudentResult(response),
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Unable to load students for the selected arm.'), { type: 'error' });
        this.cdr.markForCheck();
      },
    });
  }

  private applyStudentResult(response: SubjectStudentsResponse): void {
    this.students = response.studentsList ?? [];
    this.assignedStudentIds = new Set(response.assignedStudentIds ?? []);
    this.selectedStudentIds = new Set(response.assignedStudentIds ?? []);
    this.cdr.markForCheck();
  }

  private resolveCategoryLabel(value: SubjectStudentRecord['classCategory']): string | null {
    const mappedLabel = classCategoryLabel(value);
    if (mappedLabel !== '-') {
      return mappedLabel;
    }

    if (typeof value !== 'string') {
      return null;
    }

    switch (value.trim().toLowerCase()) {
      case 'playclass':
      case 'play class':
        return 'PlayClass';
      case 'prenursery':
      case 'pre nursery':
        return 'PreNursery';
      case 'nursery':
        return 'Nursery';
      case 'primary':
        return 'Primary';
      case 'jss':
        return 'Jss';
      case 'science':
        return 'Science';
      case 'art':
        return 'Art';
      case 'commercial':
        return 'Commercial';
      case 'none':
        return 'None';
      default:
        return value.trim() || null;
    }
  }

  private resolveCategoryFromCurrentClass(currentClass?: SubjectStudentRecord['currentClass']): string | null {
    if (currentClass == null || currentClass === '') {
      return null;
    }

    const numericValue = Number(currentClass);
    if (Number.isInteger(numericValue)) {
      if (numericValue === CurrentClass.PlayClass) {
        return 'PlayClass';
      }

      if (numericValue === CurrentClass.PreNursery) {
        return 'PreNursery';
      }

      if (numericValue >= CurrentClass.Nursery1 && numericValue <= CurrentClass.Nursery2) {
        return 'Nursery';
      }

      if (numericValue >= CurrentClass.Primary1 && numericValue <= CurrentClass.Primary6) {
        return 'Primary';
      }

      if (numericValue >= CurrentClass.Jss1 && numericValue <= CurrentClass.Jss3) {
        return 'Jss';
      }

      return null;
    }

    const normalizedValue = String(currentClass).trim().toLowerCase();
    if (!normalizedValue) {
      return null;
    }

    if (normalizedValue.startsWith('play')) {
      return 'PlayClass';
    }

    if (normalizedValue.startsWith('pre')) {
      return 'PreNursery';
    }

    if (normalizedValue.startsWith('nursery')) {
      return 'Nursery';
    }

    if (normalizedValue.startsWith('primary')) {
      return 'Primary';
    }

    if (normalizedValue.startsWith('jss')) {
      return 'Jss';
    }

    return null;
  }

  private clearStudentBoard(): void {
    this.students = [];
    this.assignedStudentIds.clear();
    this.selectedStudentIds.clear();
    this.teacherAssignmentMessage = '';
    this.cdr.markForCheck();
  }
}