import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map } from 'rxjs';

import { environment } from '@env/environment';

import {
  TeacherAssignmentArmResultRecord,
  TeacherAssignmentOptionsRecord,
  TeacherCbtUsersResponse,
  EntityIdentifier,
  TeacherClassAssignmentRecord,
  TeacherEmployeeRecord,
  TeacherFormMasterAssignmentRequest,
  TeacherSelectOptionRecord,
  TeacherSubjectAssignmentRequest,
  TeacherSubjectAssignmentRecord,
  TeacherTransferSubjectRequest,
  TeacherTransferArmRequest,
  TeacherUnassignedSubjectRecord,
} from './teacher-registry.models';

@Injectable({
  providedIn: 'root',
})
export class TeacherRegistryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.serverUrl;

  getRegisteredTeachers(): Observable<TeacherEmployeeRecord[]> {
    return this.http.get<TeacherEmployeeRecord[]>(`${this.baseUrl}/Registry/teachers/registered`);
  }

  searchTeachers(userName: string): Observable<TeacherEmployeeRecord[]> {
    const params = new HttpParams().set('userName', userName);
    return this.http.get<TeacherEmployeeRecord[]>(`${this.baseUrl}/Registry/teachers/registered/search`, { params });
  }

  enableUser(id: EntityIdentifier): Observable<TeacherEmployeeRecord[]> {
    return this.http.post<TeacherEmployeeRecord[]>(`${this.baseUrl}/Registry/teachers/${id}/enable`, {});
  }

  disableUser(id: EntityIdentifier): Observable<TeacherEmployeeRecord[]> {
    return this.http.post<TeacherEmployeeRecord[]>(`${this.baseUrl}/Registry/teachers/${id}/disable`, {});
  }

  getSubjectTeachers(): Observable<TeacherSubjectAssignmentRecord[]> {
    return this.http.get<TeacherSubjectAssignmentRecord[]>(`${this.baseUrl}/Registry/teachers/subject-teachers`);
  }

  searchSubjectTeachers(userName: string): Observable<TeacherSubjectAssignmentRecord[]> {
    const params = new HttpParams().set('userName', userName);
    return this.http.get<TeacherSubjectAssignmentRecord[]>(`${this.baseUrl}/Registry/teachers/subject-teachers/search`, { params });
  }

  unassignSubjectTeacher(id: EntityIdentifier): Observable<TeacherSubjectAssignmentRecord[]> {
    return this.http.delete<TeacherSubjectAssignmentRecord[]>(`${this.baseUrl}/Registry/teachers/subject-teachers/${id}`);
  }

  getFormTeachers(): Observable<TeacherClassAssignmentRecord[]> {
    return this.http.get<TeacherClassAssignmentRecord[]>(`${this.baseUrl}/Registry/teachers/form-teachers`);
  }

  searchFormTeachers(userName: string): Observable<TeacherClassAssignmentRecord[]> {
    const params = new HttpParams().set('userName', userName);
    return this.http.get<TeacherClassAssignmentRecord[]>(`${this.baseUrl}/Registry/teachers/form-teachers/search`, { params });
  }

  unassignFormTeacher(id: EntityIdentifier): Observable<TeacherClassAssignmentRecord[]> {
    return this.http.delete<TeacherClassAssignmentRecord[]>(`${this.baseUrl}/Registry/teachers/form-teachers/${id}`);
  }

  getSubjectAssignmentOptions(): Observable<TeacherAssignmentOptionsRecord> {
    return this.http
      .get<TeacherAssignmentOptionsRecord>(`${this.baseUrl}/Registry/teachers/subject-assignment/options`)
      .pipe(map(result => this.normalizeAssignmentOptions(result)));
  }

  getSubjectAssignmentSubjects(classId: string): Observable<TeacherSelectOptionRecord[]> {
    const params = new HttpParams().set('classId', classId);
    return this.http.get<TeacherSelectOptionRecord[]>(`${this.baseUrl}/Registry/teachers/subject-assignment/subjects`, { params });
  }

  getSubjectAssignmentArms(subjectId: string, classId: string): Observable<TeacherAssignmentArmResultRecord> {
    const params = new HttpParams().set('subjectId', subjectId).set('classId', classId);
    return this.http.get<TeacherAssignmentArmResultRecord>(`${this.baseUrl}/Registry/teachers/subject-assignment/arms`, { params });
  }

  assignSubjectTeacher(request: TeacherSubjectAssignmentRequest): Observable<TeacherAssignmentArmResultRecord> {
    return this.http.post<TeacherAssignmentArmResultRecord>(`${this.baseUrl}/Registry/teachers/subject-assignment`, request);
  }

  getSubjectUnassignedTeachers(): Observable<TeacherUnassignedSubjectRecord[]> {
    return this.http.get<TeacherUnassignedSubjectRecord[]>(`${this.baseUrl}/Registry/teachers/subject-assignment/unassigned`);
  }

  getTeacherAssignedSubjects(teacherId: string): Observable<TeacherSubjectAssignmentRecord[]> {
    const params = new HttpParams().set('teacherId', teacherId);

    return this.http
      .get<TeacherSubjectAssignmentRecord[]>(`${this.baseUrl}/Registry/teachers/subject-assignment/assigned`, { params })
      .pipe(catchError(() => this.getTeacherAssignedSubjectsFromSubjectTeachers(teacherId)));
  }

  transferSubjectAssignments(request: TeacherTransferSubjectRequest): Observable<{ success: boolean }> {
    const payload = [
      ...request.assignmentIds.map(id => ({ id })),
      { id: request.proposedTeacherId },
    ];

    return this.http.post<{ success: boolean }>(
      `${this.baseUrl}/Registry/teachers/subject-assignment/transfer`,
      payload,
    );
  }

  private getTeacherAssignedSubjectsFromSubjectTeachers(
    teacherId: string,
  ): Observable<TeacherSubjectAssignmentRecord[]> {
    return this.getSubjectTeachers().pipe(
      map(list => (list ?? []).filter(assignment => this.isAssignmentForTeacher(assignment, teacherId))),
    );
  }

  private isAssignmentForTeacher(
    assignment: TeacherSubjectAssignmentRecord | TeacherClassAssignmentRecord,
    teacherId: string,
  ): boolean {
    const normalizedTeacherId = teacherId.trim().toLowerCase();
    const employee = assignment.employee;
    const user = employee?.user;
    const candidates = [user?.id, employee?.userId, employee?.id];

    return candidates.some(value => String(value ?? '').trim().toLowerCase() === normalizedTeacherId);
  }

  private normalizeAssignmentOptions(result: TeacherAssignmentOptionsRecord | null | undefined): TeacherAssignmentOptionsRecord {
    const source = result as TeacherAssignmentOptionsRecord & {
      Teachers?: TeacherSelectOptionRecord[];
      Classes?: TeacherSelectOptionRecord[];
    } | null | undefined;

    return {
      teachers: this.normalizeSelectOptions(source?.teachers ?? source?.Teachers),
      classes: this.normalizeSelectOptions(source?.classes ?? source?.Classes),
    };
  }

  private normalizeSelectOptions(options: TeacherSelectOptionRecord[] | null | undefined): TeacherSelectOptionRecord[] {
    return (options ?? []).map(option => this.normalizeSelectOption(option));
  }

  private normalizeSelectOption(option: TeacherSelectOptionRecord & { Text?: string; Value?: string }): TeacherSelectOptionRecord {
    return {
      text: String(option?.text ?? option?.Text ?? '').trim(),
      value: String(option?.value ?? option?.Value ?? '').trim(),
      selected: option?.selected,
      disabled: option?.disabled,
    };
  }

  getClassArmAssignmentOptions(): Observable<TeacherAssignmentOptionsRecord> {
    return this.http
      .get<TeacherAssignmentOptionsRecord>(`${this.baseUrl}/Registry/teachers/class-arm-assignment/options`)
      .pipe(map(result => this.normalizeAssignmentOptions(result)));
  }

  getClassArmAssignmentArms(classId: string): Observable<TeacherAssignmentArmResultRecord> {
    const params = new HttpParams().set('classId', classId);
    return this.http.get<TeacherAssignmentArmResultRecord>(`${this.baseUrl}/Registry/teachers/class-arm-assignment/arms`, { params });
  }

  assignFormMaster(request: TeacherFormMasterAssignmentRequest): Observable<TeacherAssignmentArmResultRecord> {
    return this.http.post<TeacherAssignmentArmResultRecord>(`${this.baseUrl}/Registry/teachers/class-arm-assignment`, request);
  }

  getTeacherAssignedArms(teacherId: string): Observable<TeacherClassAssignmentRecord[]> {
    // Use existing form-teachers route (already deployed); filter when API ignores teacherId.
    const params = new HttpParams().set('teacherId', teacherId);

    return this.http
      .get<TeacherClassAssignmentRecord[]>(`${this.baseUrl}/Registry/teachers/form-teachers`, { params })
      .pipe(map(list => this.filterAssignmentsForTeacher(list, teacherId)));
  }

  transferArmAssignments(request: TeacherTransferArmRequest): Observable<{ success: boolean }> {
    const payload = [
      ...request.assignmentIds.map(id => ({ id })),
      { id: request.proposedTeacherId },
    ];
    const params = new HttpParams().set('type', 'class-arm');

    return this.http.post<{ success: boolean }>(
      `${this.baseUrl}/Registry/teachers/subject-assignment/transfer`,
      payload,
      { params },
    );
  }

  private filterAssignmentsForTeacher(
    list: TeacherClassAssignmentRecord[] | null | undefined,
    teacherId: string,
  ): TeacherClassAssignmentRecord[] {
    const rows = list ?? [];

    // When API supports ?teacherId=, the list is already filtered.
    if (rows.length === 0 || rows.every(assignment => this.isAssignmentForTeacher(assignment, teacherId))) {
      return rows;
    }

    return rows.filter(assignment => this.isAssignmentForTeacher(assignment, teacherId));
  }

  getCbtUsers(userName?: string): Observable<TeacherCbtUsersResponse> {
    const normalized = userName?.trim();
    const params = normalized ? new HttpParams().set('userName', normalized) : undefined;

    return this.http
      .get<TeacherCbtUsersResponse>(`${this.baseUrl}/Registry/teachers/cbt-users`, { params })
      .pipe(map(result => this.normalizeCbtUsersResponse(result)));
  }

  assignCbtUser(userId: string): Observable<TeacherCbtUsersResponse> {
    return this.http
      .post<TeacherCbtUsersResponse>(`${this.baseUrl}/Registry/teachers/cbt-users/${userId}/assign`, {})
      .pipe(map(result => this.normalizeCbtUsersResponse(result)));
  }

  removeCbtUser(userId: string): Observable<TeacherCbtUsersResponse> {
    return this.http
      .post<TeacherCbtUsersResponse>(`${this.baseUrl}/Registry/teachers/cbt-users/${userId}/remove`, {})
      .pipe(map(result => this.normalizeCbtUsersResponse(result)));
  }

  private normalizeCbtUsersResponse(
    result: TeacherCbtUsersResponse | null | undefined,
  ): TeacherCbtUsersResponse {
    const source = result as TeacherCbtUsersResponse & {
      Employees?: TeacherCbtUsersResponse['employees'];
      UserIds?: TeacherCbtUsersResponse['userIds'];
    } | null | undefined;

    return {
      employees: [...(source?.employees ?? source?.Employees ?? [])],
      userIds: [...(source?.userIds ?? source?.UserIds ?? [])],
    };
  }
}