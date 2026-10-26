import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '@env/environment';

import { readSelectedStudentId } from './student-portal-context.service';
import {
  SelectOption,
  StudentPortalSibling,
  StudentProfileRecord,
  StudentSchoolInfo,
  SubjectsAndClassNameRecord,
  normalizeSchoolInfo,
  normalizeSelectOptions,
  normalizeStudentPortalSiblings,
  normalizeStudentProfile,
  normalizeSubjectsAndClassName,
} from './student-portal.models';

@Injectable({
  providedIn: 'root',
})
export class StudentPortalService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.serverUrl}/StudentPortal`;

  getDashboard(): Observable<StudentSchoolInfo | null> {
    return this.http
      .get<unknown>(`${this.baseUrl}/dashboard`, { headers: this.selectedStudentHeaders() })
      .pipe(map(normalizeSchoolInfo));
  }

  getProfile(): Observable<StudentProfileRecord> {
    return this.http
      .get<unknown>(`${this.baseUrl}/profile`, { headers: this.selectedStudentHeaders() })
      .pipe(map(normalizeStudentProfile));
  }

  getSiblings(): Observable<StudentPortalSibling[]> {
    // Do not send X-Selected-Student-Id here — siblings are always relative to the login user.
    return this.http.get<unknown>(`${this.baseUrl}/siblings`).pipe(map(normalizeStudentPortalSiblings));
  }

  getSubjectCategories(): Observable<SelectOption[]> {
    return this.http
      .get<unknown>(`${this.baseUrl}/subjects/categories`, { headers: this.selectedStudentHeaders() })
      .pipe(map(normalizeSelectOptions));
  }

  getSubjectsForCategory(subjectStatus: string): Observable<SubjectsAndClassNameRecord> {
    const params = new HttpParams().set('subjectStatus', subjectStatus);
    return this.http
      .get<unknown>(`${this.baseUrl}/subjects`, { params, headers: this.selectedStudentHeaders() })
      .pipe(map(normalizeSubjectsAndClassName));
  }

  searchSubjects(searchParameter: string): Observable<SubjectsAndClassNameRecord> {
    const params = new HttpParams().set('searchParameter', searchParameter);
    return this.http
      .get<unknown>(`${this.baseUrl}/subjects/search`, { params, headers: this.selectedStudentHeaders() })
      .pipe(map(normalizeSubjectsAndClassName));
  }

  assignSubjects(subjectIds: Array<{ id: string }>): Observable<string> {
    return this.http
      .post<unknown>(`${this.baseUrl}/subjects/assign`, subjectIds, { headers: this.selectedStudentHeaders() })
      .pipe(map(normalizeActionPayload));
  }

  unassignSubjects(subjectIds: Array<{ id: string }>): Observable<string> {
    return this.http
      .post<unknown>(`${this.baseUrl}/subjects/unassign`, subjectIds, { headers: this.selectedStudentHeaders() })
      .pipe(map(normalizeActionPayload));
  }

  getResultSessions(): Observable<SelectOption[]> {
    return this.http
      .get<unknown>(`${this.baseUrl}/results/sessions`, { headers: this.selectedStudentHeaders() })
      .pipe(map(normalizeSelectOptions));
  }

  getResultTermsForSession(sessionId: string): Observable<SelectOption[]> {
    return this.http
      .get<unknown>(`${this.baseUrl}/results/sessions/${encodeURIComponent(sessionId)}/terms`, {
        headers: this.selectedStudentHeaders(),
      })
      .pipe(map(normalizeSelectOptions));
  }

  getTermlyResult(sessionId: string, termStatus: string): Observable<Record<string, unknown>> {
    const params = new HttpParams().set('sessionId', sessionId).set('termStatus', termStatus);
    return this.http.get<Record<string, unknown>>(`${this.baseUrl}/results/termly`, {
      params,
      headers: this.selectedStudentHeaders(),
    });
  }

  getYearlyResult(selectedSessionId: string): Observable<Record<string, unknown>> {
    const params = new HttpParams().set('selectedSessionId', selectedSessionId);
    return this.http.get<Record<string, unknown>>(`${this.baseUrl}/results/yearly`, {
      params,
      headers: this.selectedStudentHeaders(),
    });
  }

  private selectedStudentHeaders(): HttpHeaders | undefined {
    const selectedId = readSelectedStudentId();
    if (!selectedId) {
      return undefined;
    }
    return new HttpHeaders({ 'X-Selected-Student-Id': selectedId });
  }
}

function normalizeActionPayload(body: unknown): string {
  if (typeof body === 'string') {
    return normalizeActionResult(body);
  }

  if (body && typeof body === 'object') {
    const record = body as Record<string, unknown>;
    const message = record['message'] ?? record['Message'];
    if (typeof message === 'string') {
      return message;
    }
  }

  return normalizeActionResult(String(body ?? 'Failed'));
}

function normalizeActionResult(body: string): string {
  const trimmed = String(body ?? '').trim();
  if (!trimmed) {
    return 'Failed';
  }

  if (trimmed.startsWith('"') && trimmed.endsWith('"')) {
    try {
      return JSON.parse(trimmed) as string;
    } catch {
      return trimmed.replace(/^"|"$/g, '');
    }
  }

  try {
    const parsed = JSON.parse(trimmed) as unknown;
    if (typeof parsed === 'string') {
      return parsed;
    }
    if (parsed && typeof parsed === 'object') {
      const record = parsed as Record<string, unknown>;
      return String(record['message'] ?? record['Message'] ?? trimmed);
    }
  } catch {
    // plain text
  }

  return trimmed;
}
