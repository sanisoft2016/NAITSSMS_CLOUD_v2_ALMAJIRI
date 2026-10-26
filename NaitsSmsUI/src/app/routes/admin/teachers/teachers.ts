import { HttpParams } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';

import { AccessStatus, BaseComponent, GenericService } from '@shared';

import {
  AccessStatusValue,
  EntityIdentifier,
  TeacherClassAssignmentRecord,
  TeacherEmployeeRecord,
  TeacherSubjectAssignmentRecord,
  TeacherUnassignRequest,
  TeacherUserRecord,
  TeacherViewMode,
} from './teacher-registry.models';

@Component({
  selector: 'app-teachers',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatDividerModule,
    MatIconModule,
  ],
  templateUrl: './teachers.html',
  styleUrl: './teachers.scss',
})
export class Teachers extends BaseComponent {
  private readonly api = inject(GenericService<any>);

  activeView: TeacherViewMode | null = null;
  registeredTeachers: TeacherEmployeeRecord[] = [];
  subjectTeachers: TeacherSubjectAssignmentRecord[] = [];
  formTeachers: TeacherClassAssignmentRecord[] = [];

  registeredEmptyMessage = 'No registered teacher foung.';
  subjectEmptyMessage = 'No Subject Teacher yet';
  formEmptyMessage = 'No Form -Master yet';
  subjectSuccessMessage = '';
  formSuccessMessage = '';
  registeredSearchTerm = '';
  subjectSearchTerm = '';
  formSearchTerm = '';

  constructor() {
    super();
  }

  get displayRegisteredTeachers(): TeacherEmployeeRecord[] {
    return [...(this.registeredTeachers ?? [])]
      .filter(employee => !this.isAccountant(employee))
      .sort((left, right) => this.compareText(this.teacherFullName(left.user), this.teacherFullName(right.user)));
  }

  get displaySubjectTeachers(): TeacherSubjectAssignmentRecord[] {
    return [...(this.subjectTeachers ?? [])].sort((left, right) => {
      const nameSort = this.compareText(this.teacherFullName(left.employee?.user), this.teacherFullName(right.employee?.user));
      if (nameSort !== 0) {
        return nameSort;
      }

      const classSort = this.compareText(left.arm?.classs?.className, right.arm?.classs?.className);
      if (classSort !== 0) {
        return classSort;
      }

      return this.compareText(left.arm?.armName, right.arm?.armName);
    });
  }

  get displayFormTeachers(): TeacherClassAssignmentRecord[] {
    return [...(this.formTeachers ?? [])].sort((left, right) => {
      const nameSort = this.compareText(this.teacherFullName(left.employee?.user), this.teacherFullName(right.employee?.user));
      if (nameSort !== 0) {
        return nameSort;
      }

      const classSort = this.compareText(left.arm?.classs?.className, right.arm?.classs?.className);
      if (classSort !== 0) {
        return classSort;
      }

      return this.compareText(left.arm?.armName, right.arm?.armName);
    });
  }

  openEmployeesRegistration(): void {
    this.router.navigate(['/auth/register-teacher']);
  }

  editTeacher(employee: TeacherEmployeeRecord): void {
    this.router.navigate(['/auth/register-teacher'], {
      queryParams: { teacherId: employee.id },
    });
  }

  openCbtUserHandler(): void {
    this.router.navigate(['/admin/teachers/cbt-user-handler']);
  }

  openSubjectTeacherAssignment(): void {
    this.router.navigate(['/admin/teachers/assign-subject-index']);
  }

  openFormMasterAssignment(): void {
    this.router.navigate(['/admin/teachers/assign-class-arm-index']);
  }

  listOfRegisteredTeachers(): void {
    this.activeView = 'registered';
    this.subjectSuccessMessage = '';
    this.formSuccessMessage = '';

    this.withRequestState(this.api.get<TeacherEmployeeRecord[]>('/Registry/teachers/registered'), {
      errorMessage: 'Failed to load registered employees.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.registeredTeachers = this.normalizeRegisteredTeachers(result);
      this.registeredEmptyMessage = 'No registered teacher foung.';
    });
  }

  listOfSubjectTeacher(): void {
    this.activeView = 'subject';
    this.subjectSuccessMessage = '';
    this.formSuccessMessage = '';

    this.withRequestState(this.api.get<TeacherSubjectAssignmentRecord[]>('/Registry/teachers/subject-teachers'), {
      errorMessage: 'Failed to load subject teachers.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.subjectTeachers = result ?? [];
      this.subjectEmptyMessage = 'No Subject Teacher yet';
    });
  }

  listOfFormMasters(): void {
    this.activeView = 'form';
    this.subjectSuccessMessage = '';
    this.formSuccessMessage = '';

    this.withRequestState(this.api.get<TeacherClassAssignmentRecord[]>('/Registry/teachers/form-teachers'), {
      errorMessage: 'Failed to load form masters.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.formTeachers = result ?? [];
      this.formEmptyMessage = 'No Form -Master yet';
    });
  }

  submitRegisteredSearch(): void {
    const searchValue = this.registeredSearchTerm.trim();
    if (!searchValue) {
      alert('Search Parameter must be provided to continue.');
      return;
    }

    this.searchTeacher(searchValue);
  }

  submitSubjectSearch(): void {
    const searchValue = this.subjectSearchTerm.trim();
    if (!searchValue) {
      alert('Search Parameter must be provided to continue.');
      return;
    }

    this.searchSubjectTeacher(searchValue);
  }

  submitFormSearch(): void {
    const searchValue = this.formSearchTerm.trim();
    if (!searchValue) {
      alert('Search Parameter must be provided to continue.');
      return;
    }

    this.searchFormTeacher(searchValue);
  }

  searchTeacher(userName: string): void {
    this.activeView = 'registered';
    this.withRequestState(this.api.get<TeacherEmployeeRecord[]>('/Registry/teachers/registered/search', {
      params: new HttpParams().set('userName', userName),
    }), {
      errorMessage: 'Failed to search registered employees.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.registeredTeachers = this.normalizeRegisteredTeachers(result);
      this.registeredEmptyMessage = this.registeredTeachers.length > 0
        ? 'No registered teacher foung.'
        : 'Failed! No record match the entered Username';
    });
  }

  searchSubjectTeacher(userName: string): void {
    this.activeView = 'subject';
    this.subjectSuccessMessage = '';

    this.withRequestState(this.api.get<TeacherSubjectAssignmentRecord[]>('/Registry/teachers/subject-teachers/search', {
      params: new HttpParams().set('userName', userName),
    }), {
      errorMessage: 'Failed to search subject teachers.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.subjectTeachers = result ?? [];
      this.subjectEmptyMessage = this.subjectTeachers.length > 0
        ? 'No Subject Teacher yet'
        : 'No Username match the name you have just typed.';
    });
  }

  searchFormTeacher(userName: string): void {
    this.activeView = 'form';
    this.formSuccessMessage = '';

    this.withRequestState(this.api.get<TeacherClassAssignmentRecord[]>('/Registry/teachers/form-teachers/search', {
      params: new HttpParams().set('userName', userName),
    }), {
      errorMessage: 'Failed to search form masters.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.formTeachers = result ?? [];
      this.formEmptyMessage = this.formTeachers.length > 0
        ? 'No Form -Master yet'
        : 'No Username match the name you have just typed.';
    });
  }

  enableUser(id: number | string): void {
    this.activeView = 'registered';

    this.withRequestState(this.api.create<TeacherEmployeeRecord[]>(`/Registry/teachers/${id}/enable`, {}), {
      errorMessage: 'Failed! We were unable to complete the request.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.registeredTeachers = this.normalizeRegisteredTeachers(result);
      this.patchEmployeeAccessStatus(id, AccessStatus.Active);
      this.registeredEmptyMessage = 'No registered teacher foung.';
      this.showAlert('The user has successfully been Enabled.', { type: 'success' });
    });
  }

  disableUser(id: number | string): void {
    this.activeView = 'registered';

    this.withRequestState(this.api.create<TeacherEmployeeRecord[]>(`/Registry/teachers/${id}/disable`, {}), {
      errorMessage: 'Failed! We were unable to complete the request.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.registeredTeachers = this.normalizeRegisteredTeachers(result);
      this.patchEmployeeAccessStatus(id, AccessStatus.Disable);
      this.registeredEmptyMessage = 'No registered teacher foung.';
      this.showAlert('The user has successfully been disabled.', { type: 'success' });
    });
  }

  deleteTeacher(employee: TeacherEmployeeRecord): void {
    const fullName = this.getTeacherFullName(employee) || 'this User';
    if (!confirm(this.buildDeletePrompt(fullName))) {
      return;
    }

    this.activeView = 'registered';

    this.withRequestState(this.api.delete<{ message?: string }>(`/user-registration/delete-teacher-byId/${employee.id}`), {
      errorMessage: 'Failed! We were unable to complete the request.',
      notifyOnError: 'none',
      rethrow: true,
    }).subscribe({
      next: response => {
        this.showAlert(response?.message ?? 'Teacher deleted successfully.', { type: 'success' });
        this.registeredTeachers = this.registeredTeachers.filter(t => t.id !== employee.id);
        this.registeredEmptyMessage = this.registeredTeachers.length > 0
          ? 'No registered teacher foung.' : 'No record found.';
      },
      error: error => {
        const message = this.getHttpErrorMessage(error, 'Failed! We were unable to complete the request.');
        this.showAlert(message, { type: 'error' });
      },
    });
  }

  unassignSubjectTeacher(request: TeacherUnassignRequest): void {
    if (!confirm(this.buildUnassignPrompt(request.fullName))) {
      return;
    }

    this.activeView = 'subject';

    this.withRequestState(this.api.delete<TeacherSubjectAssignmentRecord[]>(`/Registry/teachers/subject-teachers/${request.assignmentId}`), {
      errorMessage: 'Failed! We were unable to complete the request.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.subjectTeachers = result ?? [];
      this.subjectEmptyMessage = 'No Subject Teacher yet';
      this.subjectSuccessMessage = 'You have successfully undone the Assignment';
    });
  }

  unassignFormTeacher(request: TeacherUnassignRequest): void {
    if (!confirm(this.buildUnassignPrompt(request.fullName))) {
      return;
    }

    this.activeView = 'form';

    this.withRequestState(this.api.delete<TeacherClassAssignmentRecord[]>(`/Registry/teachers/form-teachers/${request.assignmentId}`), {
      errorMessage: 'Failed! We were unable to complete the request.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.formTeachers = result ?? [];
      this.formEmptyMessage = 'No Form -Master yet';
      this.formSuccessMessage = 'You have successfully undone the Assignment';
    });
  }

  private buildUnassignPrompt(fullName: string): string {
    return `Are you sure you want to Un-assign this teacher "${fullName}"? \n If 'Yes',  click 'Ok' to continue.`;
  }

  private buildDeletePrompt(fullName: string): string {
    return `Are you sure you want to DELETE this User "${fullName}"? \n If 'Yes',  click 'Ok' to continue.`;
  }

  private getTeacherFullName(employee: TeacherEmployeeRecord): string {
    return this.teacherFullName(employee.user);
  }

  trackByEmployee(index: number, employee: TeacherEmployeeRecord): EntityIdentifier {
    return employee.id ?? index;
  }

  trackBySubjectAssignment(index: number, assignment: TeacherSubjectAssignmentRecord): EntityIdentifier {
    return assignment.id ?? index;
  }

  trackByFormAssignment(index: number, assignment: TeacherClassAssignmentRecord): EntityIdentifier {
    return assignment.id ?? index;
  }

  isNonTeacher(employee: TeacherEmployeeRecord): boolean {
    return this.normalizeText(employee.employeeStatus) !== 'Teacher';
  }

  isAccountDisabled(employee: TeacherEmployeeRecord): boolean {
    const accessStatus = this.readAccessStatus(employee);
    if (typeof accessStatus === 'number') {
      return accessStatus === AccessStatus.Disable;
    }

    const normalized = String(accessStatus ?? '').trim().toLowerCase();
    return normalized === 'disable'
      || normalized === 'disabled'
      || normalized === String(AccessStatus.Disable);
  }

  timeStatusLabel(timeStatus: unknown): string {
    if (typeof timeStatus === 'number') {
      return timeStatus === 0 ? 'Current' : 'Previous';
    }

    const normalized = String(timeStatus ?? '').trim();
    return normalized || '-';
  }

  teacherFullName(user: TeacherUserRecord | null | undefined): string {
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

  subjectClassLabel(assignment: TeacherSubjectAssignmentRecord): string {
    return String(assignment.arm?.classs?.classLabel ?? '').trim() || '-';
  }

  formClassLabel(assignment: TeacherClassAssignmentRecord): string {
    return String(assignment.arm?.classs?.classLabel ?? '').trim() || '-';
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

  private isAccountant(employee: TeacherEmployeeRecord): boolean {
    return this.normalizeText(employee.employeeStatus) === 'Accountant';
  }

  private readAccessStatus(employee: TeacherEmployeeRecord): AccessStatusValue {
    const user = employee.user as TeacherUserRecord & { AccessStatus?: AccessStatusValue } | null | undefined;
    return user?.accessStatus ?? user?.AccessStatus;
  }

  private normalizeRegisteredTeachers(result: TeacherEmployeeRecord[] | null | undefined): TeacherEmployeeRecord[] {
    return (result ?? []).map(employee => ({
      ...employee,
      user: employee.user
        ? {
            ...employee.user,
            accessStatus: this.readAccessStatus(employee),
          }
        : employee.user,
    }));
  }

  private patchEmployeeAccessStatus(id: number | string, accessStatus: AccessStatus): void {
    this.registeredTeachers = this.registeredTeachers.map(employee => {
      if (employee.id !== id || !employee.user) {
        return employee;
      }

      return {
        ...employee,
        user: {
          ...employee.user,
          accessStatus,
        },
      };
    });
  }

  private normalizeText(value: unknown): string {
    return String(value ?? '').trim();
  }

  private compareText(left: unknown, right: unknown): number {
    return this.normalizeText(left).localeCompare(this.normalizeText(right));
  }
}

const ARM_NAME_LABELS = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'N', 'O', 'NoArm'];