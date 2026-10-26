import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

import { BaseComponent } from '@shared';

import { TeacherRegistryService } from './teacher-registry.service';
import { TeacherCbtUserRecord, TeacherCbtUsersResponse } from './teacher-registry.models';

@Component({
  selector: 'app-cbt-user-handler',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatCardModule],
  templateUrl: './cbt-user-handler.html',
  styleUrl: './cbt-user-handler.scss',
})
export class CbtUserHandler extends BaseComponent {
  private readonly teacherRegistry = inject(TeacherRegistryService);

  contentMode: 'blank' | 'list' = 'blank';
  filterMode: 'all' | 'assigned' = 'all';
  currentActionUserId = '';

  cbtUsers: TeacherCbtUsersResponse = {
    employees: [],
    userIds: [],
  };

  constructor() {
    super();
  }

  get visibleEmployees(): TeacherCbtUserRecord[] {
    const assignedUsers = new Set(this.cbtUsers.userIds ?? []);

    return [...(this.cbtUsers.employees ?? [])]
      .filter(employee => this.normalizeText(employee.employeeStatus) !== 'Accountant')
      .filter(employee => this.filterMode === 'all' || assignedUsers.has(employee.userId))
      .sort((left, right) => this.fullName(left).localeCompare(this.fullName(right)));
  }

  get emptyMessage(): string {
    return this.filterMode === 'assigned'
      ? 'No registered CBT user found.'
      : 'No registered teacher foung.';
  }

  listOfRegisteredTeachers(): void {
    this.filterMode = 'all';
    this.loadCbtUsers();
  }

  listOfCbtUsers(): void {
    this.filterMode = 'assigned';
    this.loadCbtUsers();
  }

  searchTeacher(searchTerm: string): void {
    const normalized = searchTerm.trim();
    if (!normalized) {
      alert('Search Parameter must be provided to continue.');
      return;
    }

    this.contentMode = 'list';
    this.withRequestState(this.teacherRegistry.getCbtUsers(normalized), {
      errorMessage: 'Failed to search registered employees.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.cbtUsers = this.normalizeCbtUsers(result);
    });
  }

  assignAsCbtTeacher(userId: string): void {
    this.currentActionUserId = userId;

    const subscription = this.withRequestState(this.teacherRegistry.assignCbtUser(userId), {
      useLoading: false,
      errorMessage: 'Failed! We were unable to complete the request.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.cbtUsers = this.normalizeCbtUsers(result);
      this.contentMode = 'list';
    });

    subscription.add(() => {
      this.currentActionUserId = '';
      this.cdr.markForCheck();
    });
  }

  unAssignAsCbtTeacher(userId: string): void {
    this.currentActionUserId = userId;

    const subscription = this.withRequestState(this.teacherRegistry.removeCbtUser(userId), {
      useLoading: false,
      errorMessage: 'Failed! We were unable to complete the request.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.cbtUsers = this.normalizeCbtUsers(result);
      this.contentMode = 'list';
    });

    subscription.add(() => {
      this.currentActionUserId = '';
      this.cdr.markForCheck();
    });
  }

  fullName(employee: TeacherCbtUserRecord): string {
    return this.normalizeText(employee.fullName) || this.normalizeText(employee.userName) || '-';
  }

  isNonTeacher(employee: TeacherCbtUserRecord): boolean {
    return this.normalizeText(employee.employeeStatus) !== 'Teacher';
  }

  isAssigned(employee: TeacherCbtUserRecord): boolean {
    return (this.cbtUsers.userIds ?? []).includes(employee.userId);
  }

  isBusyFor(userId: string): boolean {
    return this.currentActionUserId === userId;
  }

  timeStatusLabel(timeStatus: unknown): string {
    if (typeof timeStatus === 'number') {
      return timeStatus === 0 ? 'Current' : 'Previous';
    }

    return this.normalizeText(timeStatus) || '-';
  }

  private loadCbtUsers(): void {
    this.contentMode = 'list';
    this.withRequestState(this.teacherRegistry.getCbtUsers(), {
      errorMessage: 'Failed to load CBT user data.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe(result => {
      this.cbtUsers = this.normalizeCbtUsers(result);
    });
  }

  private normalizeCbtUsers(result: TeacherCbtUsersResponse | null | undefined): TeacherCbtUsersResponse {
    return {
      employees: [...(result?.employees ?? [])],
      userIds: [...(result?.userIds ?? [])],
    };
  }

  private normalizeText(value: unknown): string {
    return String(value ?? '').trim();
  }
}