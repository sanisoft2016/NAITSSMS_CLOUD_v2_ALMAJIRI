import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { ArmName } from '@shared/interfaces';
import { Observable, of, shareReplay, throwError } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';

import { environment } from '@env/environment';

import {
  SelectOptionRecord,
  SubjectArmsResponse,
  SubjectAssignmentContextRecord,
  SubjectAssignmentSubmitResponse,
  SubjectCategoryRecord,
  SubjectListResponse,
  SubjectRecord,
  SubjectSavePayload,
  SubjectSelectionRecord,
  SubjectStatusName,
  SubjectStudentsResponse,
  normalizeSelectOptions,
  subjectStatusToCode,
} from './subject-registry.models';

@Injectable({
  providedIn: 'root',
})
export class SubjectRegistryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.serverUrl;

  private readonly listCache = new Map<string, { at: number; value: SubjectListResponse }>();
  private readonly inFlightLists = new Map<string, Observable<SubjectListResponse>>();
  private readonly listCacheTtlMs = 30_000;

  getSubjectLabels(): Observable<SubjectCategoryRecord[]> {
    return this.http.get<SubjectCategoryRecord[]>(`${this.baseUrl}/Subject/labels`);
  }

  getSubjectList(subjectStatus: SubjectStatusName, subjectLevelStatus?: string): Observable<SubjectListResponse> {
    const statusCode = String(subjectStatusToCode(subjectStatus));
    const cached = this.listCache.get(statusCode);
    if (cached && Date.now() - cached.at < this.listCacheTtlMs) {
      return of({
        ...cached.value,
        subjectLevelStatus: subjectLevelStatus?.trim() || cached.value.subjectLevelStatus,
      });
    }

    const inFlight = this.inFlightLists.get(statusCode);
    if (inFlight) {
      return inFlight.pipe(
        map(response => ({
          ...response,
          subjectLevelStatus: subjectLevelStatus?.trim() || response.subjectLevelStatus,
        })),
      );
    }

    const params = new HttpParams().set('subjectStatus', statusCode);
    const request$ = this.http.get<Record<string, unknown>>(`${this.baseUrl}/Subject`, { params }).pipe(
      map(response => this.normalizeSubjectListResponse(response, subjectStatus, subjectLevelStatus)),
      tap(response => {
        this.listCache.set(statusCode, { at: Date.now(), value: response });
        this.inFlightLists.delete(statusCode);
      }),
      catchError(error => {
        this.inFlightLists.delete(statusCode);
        return throwError(() => error);
      }),
      shareReplay({ bufferSize: 1, refCount: true }),
    );

    this.inFlightLists.set(statusCode, request$);
    return request$;
  }

  createSubject(subjectStatus: SubjectStatusName, payload: SubjectSavePayload): Observable<{ message: string }> {
    const params = new HttpParams().set('subjectStatus', String(subjectStatusToCode(subjectStatus)));
    return this.http.post<{ message: string }>(`${this.baseUrl}/Subject`, payload, { params }).pipe(
      tap(() => this.invalidateSubjectListCache(subjectStatus)),
    );
  }

  updateSubject(id: string, subjectStatus: SubjectStatusName, payload: SubjectSavePayload): Observable<{ message: string }> {
    const params = new HttpParams().set('subjectStatus', String(subjectStatusToCode(subjectStatus)));
    return this.http.put<{ message: string }>(`${this.baseUrl}/Subject/${encodeURIComponent(id)}`, payload, { params }).pipe(
      tap(() => this.invalidateSubjectListCache(subjectStatus)),
    );
  }

  deleteSubject(id: string, subjectStatus?: SubjectStatusName): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.baseUrl}/Subject/${encodeURIComponent(id)}`).pipe(
      tap(() => {
        if (subjectStatus) {
          this.invalidateSubjectListCache(subjectStatus);
        } else {
          this.listCache.clear();
          this.inFlightLists.clear();
        }
      }),
    );
  }

  private invalidateSubjectListCache(subjectStatus: SubjectStatusName): void {
    const statusCode = String(subjectStatusToCode(subjectStatus));
    this.listCache.delete(statusCode);
    this.inFlightLists.delete(statusCode);
  }

  private normalizeSubjectListResponse(
    response: Record<string, unknown> | null | undefined,
    fallbackStatus: SubjectStatusName,
    fallbackLevelStatus?: string,
  ): SubjectListResponse {
    const raw = response ?? {};
    const subjectsRaw = raw['subjects'] ?? raw['Subjects'] ?? [];
    const subjects = Array.isArray(subjectsRaw)
      ? subjectsRaw.map(item => this.normalizeSubjectRecordFast(item))
      : [];

    return {
      subjectLevelStatus: String(
        raw['subjectLevelStatus'] ?? raw['SubjectLevelStatus'] ?? fallbackLevelStatus ?? fallbackStatus,
      ),
      subjectStatus: subjectStatusToCode(
        (raw['subjectStatus'] ?? raw['SubjectStatus'] ?? fallbackStatus) as string | number | null | undefined,
      ),
      totalNoOfSubjects: Number(raw['totalNoOfSubjects'] ?? raw['TotalNoOfSubjects'] ?? subjects.length) || subjects.length,
      title: String(raw['title'] ?? raw['Title'] ?? 'Subject Registration'),
      shouldCreate: Boolean(raw['shouldCreate'] ?? raw['ShouldCreate'] ?? subjects.length === 0),
      subjects,
    };
  }

  private normalizeSubjectRecordFast(item: unknown): SubjectRecord {
    const raw = (item ?? {}) as Record<string, unknown>;
    // Fast path for camelCase API payloads (production + current API).
    if (typeof raw['id'] === 'string' || typeof raw['subjectName'] === 'string') {
      return {
        id: String(raw['id'] ?? '').trim(),
        subjectName: (raw['subjectName'] ?? null) as string | null,
        timeStatus: (raw['timeStatus'] ?? null) as SubjectRecord['timeStatus'],
        subjectCategoryId: (raw['subjectCategoryId'] ?? null) as string | null,
        subjectCategory: null,
      };
    }

    return {
      id: String(raw['Id'] ?? '').trim(),
      subjectName: (raw['SubjectName'] ?? null) as string | null,
      timeStatus: (raw['TimeStatus'] ?? null) as SubjectRecord['timeStatus'],
      subjectCategoryId: (raw['SubjectCategoryId'] ?? null) as string | null,
      subjectCategory: null,
    };
  }

  getAllSubjectDropdown(): Observable<SelectOptionRecord[]> {
    return this.http.get<SelectOptionRecord[]>(`${this.baseUrl}/Subject/all/dropdown`);
  }

  getSubjectLevels(subjectName: string): Observable<SubjectRecord[]> {
    const params = new HttpParams().set('subjectName', subjectName);
    return this.http.get<SubjectRecord[]>(`${this.baseUrl}/Subject/all/levels`, { params });
  }

  searchSubjectLevels(searchParameter: string): Observable<SubjectRecord[]> {
    const params = new HttpParams().set('searchParameter', searchParameter);
    return this.http.get<SubjectRecord[]>(`${this.baseUrl}/Subject/all/levels/search`, { params });
  }

  getStudentAssignmentContext(): Observable<SubjectAssignmentContextRecord> {
    return this.http.get<SubjectAssignmentContextRecord>(`${this.baseUrl}/Subject/student-assignment/context`).pipe(
      map(context => ({
        electiveSubjectsDropdown: normalizeSelectOptions(context.electiveSubjectsDropdown),
        classLabelDropdown: normalizeSelectOptions(context.classLabelDropdown),
      })),
    );
  }

  getStudentAssignmentArms(selectedClassId: string, subjectId?: string): Observable<SubjectArmsResponse> {
    const classId = selectedClassId?.trim();
    if (!classId) {
      return of({ arms: [], subjectId, message: '' });
    }

    return this.http
      .get<{ data?: Array<Record<string, unknown>> }>(
        `${this.baseUrl}/ClassAndArm/classes-arms/${encodeURIComponent(classId)}`,
        { observe: 'response' },
      )
      .pipe(
        map(response => this.mapClassArmResponse(response, subjectId)),
        catchError(error => throwError(() => error)),
      );
  }

  private mapClassArmResponse(
    response: HttpResponse<{ data?: Array<Record<string, unknown>> }>,
    subjectId?: string,
  ): SubjectArmsResponse {
    if (response.status === 204 || !response.body) {
      return {
        arms: [],
        subjectId,
        message: 'The selected Class does not contain Arm',
      };
    }

    const arms = normalizeSelectOptions(
      (response.body.data ?? []).map(row => ({
        value: row['id'] ?? row['Id'],
        text: this.formatArmLabel(row['armName'] ?? row['ArmName']),
      })),
    );

    return {
      arms,
      subjectId,
      message: arms.length === 0 ? 'The selected Class does not contain Arm' : '',
    };
  }

  private formatArmLabel(armName: unknown): string {
    if (typeof armName === 'string' && armName.trim()) {
      return armName.trim();
    }

    const numericValue = Number(armName);
    if (Number.isInteger(numericValue) && numericValue in ArmName) {
      return ArmName[numericValue as ArmName];
    }

    return String(armName ?? '').trim();
  }

  getStudentAssignmentStudents(selectedArmId: string, subjectId: string): Observable<SubjectStudentsResponse> {
    const params = new HttpParams().set('selectedArmId', selectedArmId).set('subjectIdd', subjectId);
    return this.http.get<SubjectStudentsResponse>(`${this.baseUrl}/Subject/student-assignment/students`, { params });
  }

  getStudentAssignmentStudentsByCategory(catStatus: string, armId: string, subjectId: string): Observable<SubjectStudentsResponse> {
    const params = new HttpParams().set('catStatus', catStatus).set('armId', armId).set('subjectId', subjectId);
    return this.http.get<SubjectStudentsResponse>(`${this.baseUrl}/Subject/student-assignment/students/by-category`, { params });
  }

  assignSubjectToStudents(payload: SubjectSelectionRecord[]): Observable<SubjectAssignmentSubmitResponse> {
    return this.http.post<SubjectAssignmentSubmitResponse>(`${this.baseUrl}/Subject/student-assignment`, payload);
  }

  unassignSubjectFromStudents(payload: SubjectSelectionRecord[]): Observable<SubjectAssignmentSubmitResponse> {
    return this.http.post<SubjectAssignmentSubmitResponse>(`${this.baseUrl}/Subject/student-assignment/unassign`, payload);
  }
}
