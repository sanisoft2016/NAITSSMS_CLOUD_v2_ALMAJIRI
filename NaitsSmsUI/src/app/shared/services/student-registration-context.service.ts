import { Injectable } from '@angular/core';

import { StudentRegistrationContext } from '@shared/interfaces';

@Injectable({
  providedIn: 'root',
})
export class StudentRegistrationContextService {
  private readonly storageKey = 'student-registration-context';

  getContext(): StudentRegistrationContext | null {
    const storage = this.getStorage();
    if (!storage) {
      return null;
    }

    const rawValue = storage.getItem(this.storageKey);
    if (!rawValue) {
      return null;
    }

    try {
      const context = JSON.parse(rawValue) as Partial<StudentRegistrationContext> | null;
      if (!context?.classId || !context?.armId) {
        return null;
      }

      return {
        classId: String(context.classId ?? ''),
        classs: String(context.classs ?? ''),
        classLabel: String(context.classLabel ?? ''),
        armId: String(context.armId ?? ''),
        armName: String(context.armName ?? ''),
        totalNoOfStudents: this.normalizeCount(context.totalNoOfStudents),
      };
    } catch {
      return null;
    }
  }

  setContext(context: StudentRegistrationContext): void {
    const storage = this.getStorage();
    if (!storage) {
      return;
    }

    const normalizedContext: StudentRegistrationContext = {
      classId: context.classId,
      classs: context.classs,
      classLabel: context.classLabel,
      armId: context.armId,
      armName: context.armName,
      totalNoOfStudents: this.normalizeCount(context.totalNoOfStudents),
    };

    storage.setItem(this.storageKey, JSON.stringify(normalizedContext));
  }

  toQueryParams(context: StudentRegistrationContext): Record<string, string | number> {
    return {
      classId: context.classId,
      classs: context.classs,
      classLabel: context.classLabel,
      armId: context.armId,
      armName: context.armName,
      totalNoOfStudents: this.normalizeCount(context.totalNoOfStudents),
    };
  }

  patchContext(context: Partial<StudentRegistrationContext>): StudentRegistrationContext | null {
    const currentContext = this.getContext();
    if (!currentContext) {
      return null;
    }

    const nextContext: StudentRegistrationContext = {
      ...currentContext,
      ...context,
      totalNoOfStudents: this.normalizeCount(
        context.totalNoOfStudents ?? currentContext.totalNoOfStudents,
      ),
    };

    this.setContext(nextContext);
    return nextContext;
  }

  clearContext(): void {
    const storage = this.getStorage();
    if (!storage) {
      return;
    }

    storage.removeItem(this.storageKey);
  }

  private getStorage(): Storage | null {
    try {
      return globalThis.sessionStorage ?? null;
    } catch {
      return null;
    }
  }

  private normalizeCount(value: number | string | null | undefined): number {
    const count = Number(value ?? 0);
    return Number.isFinite(count) && count >= 0 ? count : 0;
  }
}