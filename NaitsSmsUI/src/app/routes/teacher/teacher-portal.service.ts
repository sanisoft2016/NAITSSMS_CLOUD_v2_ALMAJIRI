import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '@env/environment';

import {
  AssignedArmsStudentsResponse,
  FormMasterReportResponse,
  StudentsAccessment,
  StudentsAccessmentCasSetting,
  TeacherClassArmAssignment,
  TeacherSubjectArmAssignment,
} from './teacher-portal.models';

@Injectable({
  providedIn: 'root',
})
export class TeacherPortalService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.serverUrl}/TeachersAndOthersService`;

  getAssignedSubjectsAndArms(): Observable<TeacherSubjectArmAssignment[]> {
    return this.http.get<TeacherSubjectArmAssignment[]>(`${this.baseUrl}/teacher/assignments`);
  }

  getAssignedArmsAsFormMaster(): Observable<TeacherClassArmAssignment[]> {
    return this.http.get<TeacherClassArmAssignment[]>(`${this.baseUrl}/teacher/assigned-arms`);
  }

  getAssignedArmsStudents(armId: string, classLabel: string): Observable<AssignedArmsStudentsResponse> {
    const params = new HttpParams().set('armId', armId).set('classLabel', classLabel);
    return this.http.get<AssignedArmsStudentsResponse>(`${this.baseUrl}/form-master/assigned-arms-students`, {
      params,
    });
  }

  /** Legacy TeacherController.MarkTermCompleted — conCatParameters = armId,status (0|1) */
  markTermCompleted(conCatParameters: string): Observable<boolean> {
    const params = new HttpParams().set('conCatParameters', conCatParameters);
    return this.http.get<boolean>(`${this.baseUrl}/form-master/mark-term-completed`, { params });
  }

  getPerArmCaResult(concatParameters: string): Observable<FormMasterReportResponse> {
    const params = new HttpParams().set('concatParameters', concatParameters);
    return this.http.get<FormMasterReportResponse>(`${this.baseUrl}/form-master/per-arm-ca-result`, { params });
  }

  getPerArmResult(concatParameters: string): Observable<FormMasterReportResponse> {
    const params = new HttpParams().set('concatParameters', concatParameters);
    return this.http.get<FormMasterReportResponse>(`${this.baseUrl}/form-master/per-arm-result`, { params });
  }

  getThirdTermResultAlone(concatParameters: string): Observable<FormMasterReportResponse> {
    const params = new HttpParams().set('concatParameters', concatParameters);
    return this.http.get<FormMasterReportResponse>(`${this.baseUrl}/form-master/third-term-result`, { params });
  }

  getAffectivePsychomotorRemark(concatParameter: string): Observable<any> {
    const params = new HttpParams().set('concatParameter', concatParameter);
    return this.http.get(`${this.baseUrl}/form-master/affective-psychomotor-remark`, { params });
  }

  submitAffectivePsychomotorScores(payload: {
    studentId: string;
    armId: string;
    attendance: number;
    teacherComment: string;
    schoolHeadComment: string;
    scores: Array<{ ItemId: number; Score: string }>;
  }): Observable<{ message?: string }> {
    return this.http.post<{ message?: string }>(`${this.baseUrl}/form-master/affective-psychomotor-scores`, payload);
  }

  getUncompletedCaAndExams(): Observable<TeacherSubjectArmAssignment[]> {
    return this.http.get<TeacherSubjectArmAssignment[]>(`${this.baseUrl}/teacher/uncompleted-ca-exams`);
  }

  getUncompletedCa(): Observable<TeacherSubjectArmAssignment[]> {
    return this.http.get<TeacherSubjectArmAssignment[]>(`${this.baseUrl}/teacher/uncompleted-ca`);
  }

  getStudentsCaSetting(armId: string, subjectId: string): Observable<StudentsAccessmentCasSetting> {
    const params = new HttpParams().set('subjectId', subjectId);
    return this.http.get<StudentsAccessmentCasSetting>(
      `${this.baseUrl}/arm/${encodeURIComponent(armId)}/students/ca-setting`,
      { params },
    );
  }

  getUnenteredCaScores(armId: string, subjectId: string): Observable<StudentsAccessmentCasSetting> {
    const params = new HttpParams().set('subjectId', subjectId);
    return this.http.get<StudentsAccessmentCasSetting>(
      `${this.baseUrl}/arm/${encodeURIComponent(armId)}/students/ca-unentered`,
      { params },
    );
  }

  getUnenteredCaAndExamScores(armId: string, subjectId: string): Observable<StudentsAccessmentCasSetting> {
    const params = new HttpParams().set('subjectId', subjectId);
    return this.http.get<StudentsAccessmentCasSetting>(
      `${this.baseUrl}/arm/${encodeURIComponent(armId)}/students/ca-exams-unentered`,
      { params },
    );
  }

  saveCaAndExamScores(scores: StudentsAccessment[]): Observable<{ message?: string }> {
    return this.http.post<{ message?: string }>(`${this.baseUrl}/assessment/ca-exams-scores`, scores);
  }
}
