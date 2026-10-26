import { Injectable, computed, inject, signal } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';

import { credentialHasStudentOrParentAccess } from '@shared/models/UserCredential';

import { StudentPortalSibling } from './student-portal.models';
import { StudentPortalService } from './student-portal.service';

export const STUDENT_PORTAL_SELECTED_ID_STORAGE_KEY = 'naits.studentPortal.selectedStudentId';

/**
 * Holds the selected sibling student for the Student / Parent portal.
 * Persist in sessionStorage so a refresh keeps the choice; cleared on logout.
 */
@Injectable({
  providedIn: 'root',
})
export class StudentPortalContextService {
  private readonly portalService = inject(StudentPortalService);

  private readonly siblingsSignal = signal<StudentPortalSibling[]>([]);
  private readonly selectedIdSignal = signal<string>('');
  private readonly loadingSignal = signal(false);
  private readonly errorSignal = signal('');
  private loadGeneration = 0;

  readonly siblings = this.siblingsSignal.asReadonly();
  readonly selectedStudentId = this.selectedIdSignal.asReadonly();
  readonly loading = this.loadingSignal.asReadonly();
  readonly error = this.errorSignal.asReadonly();

  readonly selectedStudent = computed(() => {
    const id = this.selectedIdSignal();
    return this.siblingsSignal().find(s => s.id === id) ?? null;
  });

  readonly hasMultipleSiblings = computed(() => this.siblingsSignal().length > 1);

  /** Emits whenever the effective selected student id changes (for page reloads). */
  readonly selectedStudentId$ = toObservable(this.selectedIdSignal);

  ensureLoaded(): void {
    if (!this.isStudentPortalUser()) {
      this.clear();
      return;
    }

    if (this.siblingsSignal().length > 0 || this.loadingSignal()) {
      return;
    }

    this.reload();
  }

  reload(): void {
    if (!this.isStudentPortalUser()) {
      this.clear();
      return;
    }

    const generation = ++this.loadGeneration;
    this.loadingSignal.set(true);
    this.errorSignal.set('');

    this.portalService.getSiblings().subscribe({
      next: siblings => {
        if (generation !== this.loadGeneration) {
          return;
        }

        this.siblingsSignal.set(siblings);
        this.loadingSignal.set(false);

        if (siblings.length === 0) {
          this.selectedIdSignal.set('');
          writeSelectedStudentId('');
          return;
        }

        const stored = readSelectedStudentId();
        const preferred =
          siblings.find(s => s.id === stored)?.id ??
          siblings.find(s => s.isCurrentLogin)?.id ??
          siblings[0].id;

        writeSelectedStudentId(preferred);
        this.selectedIdSignal.set(preferred);
      },
      error: () => {
        if (generation !== this.loadGeneration) {
          return;
        }
        this.loadingSignal.set(false);
        this.errorSignal.set('Unable to load related students.');
      },
    });
  }

  selectStudent(studentId: string): void {
    const normalized = String(studentId ?? '').trim();
    if (!normalized || normalized === this.selectedIdSignal()) {
      return;
    }

    if (!this.siblingsSignal().some(s => s.id === normalized)) {
      return;
    }

    writeSelectedStudentId(normalized);
    this.selectedIdSignal.set(normalized);
  }

  clear(): void {
    this.loadGeneration += 1;
    this.siblingsSignal.set([]);
    this.selectedIdSignal.set('');
    this.loadingSignal.set(false);
    this.errorSignal.set('');
    writeSelectedStudentId('');
  }

  private isStudentPortalUser(): boolean {
    try {
      const raw = localStorage.getItem('credential');
      if (!raw) {
        return false;
      }
      const credential = JSON.parse(raw) as { role?: string | string[] };
      return credentialHasStudentOrParentAccess(credential);
    } catch {
      return false;
    }
  }
}

export function readSelectedStudentId(): string {
  try {
    return sessionStorage.getItem(STUDENT_PORTAL_SELECTED_ID_STORAGE_KEY)?.trim() ?? '';
  } catch {
    return '';
  }
}

export function writeSelectedStudentId(id: string): void {
  try {
    if (!id) {
      sessionStorage.removeItem(STUDENT_PORTAL_SELECTED_ID_STORAGE_KEY);
    } else {
      sessionStorage.setItem(STUDENT_PORTAL_SELECTED_ID_STORAGE_KEY, id);
    }
  } catch {
    // ignore quota / private mode
  }
}
