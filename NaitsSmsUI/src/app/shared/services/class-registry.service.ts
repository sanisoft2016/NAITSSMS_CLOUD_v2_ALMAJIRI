import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { ClassDto, ClassReArrangementDto } from '@shared/interfaces';
import { catchError, map, of } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class ClassRegistryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.serverUrl;

  /**
   * Get all classes for a school
   */
  getClasses(schoolInfoId: number): Observable<{ classes: ClassDto[]; totalNoOfClasses: number }> {
    // Modern API: GET /api/Registry/classes
    // The backend infers school context from auth/user; `schoolInfoId` is ignored here.
    return this.http.get<ClassDto[]>(`${this.baseUrl}/ClassAndArm/classes`).pipe(
      map(classes => ({ classes: classes ?? [], totalNoOfClasses: (classes ?? []).length }))
    );
  }

  /**
   * Get classes for rearrangement
   */
  getClassesForReArrangement(schoolInfoId: string): Observable<ClassDto[]> {
    // Modern API doesn't need schoolInfoId for fetching; reuse classes endpoint.
    return this.http.get<ClassDto[]>(`${this.baseUrl}/Registry/classes`);
  }

  /**
   * Get class by ID for editing
   */
  getClassForEdit(id: number, className: string, classLabel: string, sno: number): Observable<ClassDto> {
    const concat = `${id},${className},${classLabel},${sno}`;
    const params = new HttpParams().set('concat', concat);
    return this.http.get<ClassDto>(`${this.baseUrl}/Registry/EditClass`, { params });
  }

  /**
   * Create a new class
   */
  createClass(classDto: ClassDto): Observable<boolean> {
    return this.http.post<boolean>(`${this.baseUrl}/Registry/ClassReg`, classDto);
  }

  /**
   * Update an existing class
   */
  updateClass(classDto: ClassDto): Observable<boolean> {
    const payload = {
      id: classDto.id,
      className: classDto.className,
      classLabel: classDto.classLabel,
      sNo: classDto.sNo,
      schoolInfoId: classDto.schoolInfoId,
    };

    return this.http
      .put(`${this.baseUrl}/ClassAndArm/classes/${encodeURIComponent(classDto.id)}`, payload)
      .pipe(map(() => true));
  }

  /**
   * Delete a class by ID
   */
  deleteClass(id: string): Observable<boolean> {
    return this.http
      .delete(`${this.baseUrl}/ClassAndArm/classes/${encodeURIComponent(id)}`)
      .pipe(map(() => true));
  }

  /**
   * Submit class rearrangement
   */
  submitClassReArrangement(classDetails: ClassReArrangementDto[]): Observable<boolean> {
    // Modern API: POST /api/Registry/classes/rearrange
    // Backend expects a List<ClassDto> but only uses Id + SNo (+ SchoolInfoId as needed).
    const payload = classDetails.map(d => ({ id: d.id, schoolInfoId: d.schoolInfoId, sNo: d.sNo }));
    return this.http.post(`${this.baseUrl}/Registry/classes/rearrange`, payload).pipe(
      map(() => true),
      catchError(() => of(false))
    );
  }

  /**
   * Get class names dropdown
   */
  getClassNames(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/Registry/GetClassNames`);
  }
}
