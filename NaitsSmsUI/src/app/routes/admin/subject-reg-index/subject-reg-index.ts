import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { EMPTY, catchError, distinctUntilChanged, finalize, map, switchMap } from 'rxjs';

import { BaseComponent } from '@shared';

import {
  SubjectListResponse,
  SubjectRecord,
  SubjectSavePayload,
  SubjectStatusName,
  TimeStatusCode,
  subjectStatusToQueryName,
  timeStatusLabel,
} from '../subjects/subject-registry.models';
import { SubjectRegistryService } from '../subjects/subject-registry.service';

@Component({
  selector: 'app-subject-reg-index',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './subject-reg-index.html',
  styleUrl: './subject-reg-index.scss',
})
export class SubjectRegIndex extends BaseComponent implements OnInit {
  private readonly subjectRegistry = inject(SubjectRegistryService);

  readonly TimeStatusCode = TimeStatusCode;

  readonly subjectStatus = signal<SubjectStatusName>('PlayClass');
  readonly subjectLevelStatus = signal('');
  readonly pageTitle = signal('Subject Registration');
  readonly totalNoOfSubjects = signal(0);
  readonly subjects = signal<SubjectRecord[]>([]);

  editingSubject: SubjectRecord | null = null;
  isEditModalOpen = false;
  editForm: SubjectSavePayload = {
    subjectName: '',
    timeStatus: TimeStatusCode.Current,
  };

  private loadGeneration = 0;

  constructor() {
    super();

    // Cancel in-flight loads when the subject status changes (avoids out-of-order empty responses).
    this.route.queryParamMap
      .pipe(
        map(query => ({
          subjectStatus: subjectStatusToQueryName(query.get('subjectStatus')),
          subjectLevelStatus: query.get('subjectLevelStatus')?.trim() || '',
        })),
        distinctUntilChanged((left, right) => left.subjectStatus === right.subjectStatus),
        switchMap(({ subjectStatus, subjectLevelStatus }) => this.startSubjectListLoad(subjectStatus, subjectLevelStatus)),
        takeUntilDestroyed(),
      )
      .subscribe(({ response, generation }) => {
        if (generation !== this.loadGeneration) {
          return;
        }
        this.applySubjectResponse(response);
      });
  }

  ngOnInit(): void {
    // Query-param driven load is wired in the constructor for takeUntilDestroyed().
  }

  createSubject(): void {
    this.router.navigate(['/admin/subject-reg'], {
      queryParams: {
        subjectStatus: this.subjectStatus(),
        subjectLevelStatus: this.subjectLevelStatus(),
      },
    });
  }

  backToIndex(): void {
    this.router.navigate(['/admin/subjects']);
  }

  openEditModal(subject: SubjectRecord): void {
    this.editingSubject = subject;
    this.editForm = {
      subjectName: subject.subjectName?.trim() ?? '',
      timeStatus: Number(subject.timeStatus) === TimeStatusCode.Previous
        ? TimeStatusCode.Previous
        : TimeStatusCode.Current,
      subjectCategoryId: subject.subjectCategoryId ?? undefined,
    };
    this.isEditModalOpen = true;
    this.cdr.markForCheck();
  }

  closeEditModal(): void {
    this.isEditModalOpen = false;
    this.editingSubject = null;
    this.editForm = {
      subjectName: '',
      timeStatus: TimeStatusCode.Current,
    };
    this.cdr.markForCheck();
  }

  updateSubject(): void {
    if (!this.editingSubject) {
      return;
    }

    const subjectName = this.editForm.subjectName.trim();
    if (!subjectName) {
      this.showAlert('Enter a subject name before updating.', { type: 'warning' });
      return;
    }

    if (
      this.editForm.timeStatus === TimeStatusCode.Previous &&
      !window.confirm('Changing a subject to Previous will remove the current term assignment artefacts for that subject. Continue?')
    ) {
      return;
    }

    if (!this.startSubmitting()) {
      return;
    }

    const payload: SubjectSavePayload = {
      ...this.editForm,
      subjectName,
    };

    this.withRequestState(this.subjectRegistry.updateSubject(this.editingSubject.id, this.subjectStatus(), payload), {
      useLoading: false,
      errorMessage: 'Unable to update subject.',
    })
      .pipe(finalize(() => this.stopSubmitting()))
      .subscribe({
        next: response => {
          this.showAlert(response.message || 'Subject updated successfully.', { type: 'success' });
          this.closeEditModal();
          this.reloadSubjects();
        },
        error: error => {
          this.showAlert(this.getHttpErrorMessage(error, 'Unable to update subject.'), { type: 'error' });
          this.cdr.markForCheck();
        },
      });
  }

  deleteSubject(subject: SubjectRecord): void {
    const subjectName = subject.subjectName?.trim() || 'this subject';
    if (!window.confirm(`Delete ${subjectName}?`)) {
      return;
    }

    this.withRequestState(this.subjectRegistry.deleteSubject(subject.id, this.subjectStatus()), {
      errorMessage: 'Unable to delete subject.',
    }).subscribe({
      next: response => {
        this.showAlert(response.message || 'Subject deleted successfully.', { type: 'success' });
        this.reloadSubjects();
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Unable to delete subject.'), { type: 'error' });
        this.cdr.markForCheck();
      },
    });
  }

  getTimeStatusLabel(subject: SubjectRecord): string {
    return timeStatusLabel(subject.timeStatus);
  }

  trackBySubject(index: number, subject: SubjectRecord): string {
    return subject.id || `${index}`;
  }

  private reloadSubjects(): void {
    this.startSubjectListLoad(this.subjectStatus(), this.subjectLevelStatus()).subscribe(({ response, generation }) => {
      if (generation !== this.loadGeneration) {
        return;
      }
      this.applySubjectResponse(response);
    });
  }

  private startSubjectListLoad(subjectStatus: SubjectStatusName, subjectLevelStatus: string) {
    const levelStatus = subjectLevelStatus.trim() || subjectStatus;
    this.subjectStatus.set(subjectStatus);
    this.subjectLevelStatus.set(levelStatus);
    // Keep previous rows visible while loading so the page does not feel empty/slow.

    const generation = ++this.loadGeneration;
    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.cdr.markForCheck();

    return this.subjectRegistry.getSubjectList(subjectStatus, levelStatus).pipe(
      map(response => ({ response, generation })),
      catchError(error => {
        if (generation === this.loadGeneration) {
          this.showAlert(this.getHttpErrorMessage(error, 'Unable to load subject list.'), { type: 'error' });
          this.errorMessage.set('Unable to load subject list.');
          this.cdr.markForCheck();
        }
        return EMPTY;
      }),
      finalize(() => {
        // Only the latest generation may clear loading; cancelled requests must not flip it off.
        if (generation === this.loadGeneration) {
          this.isLoading.set(false);
          this.cdr.markForCheck();
        }
      }),
    );
  }

  private applySubjectResponse(response: SubjectListResponse): void {
    this.subjects.set(response.subjects ?? []);
    this.totalNoOfSubjects.set(response.totalNoOfSubjects ?? response.subjects?.length ?? 0);
    this.pageTitle.set(response.title?.trim() || 'Subject Registration');
    const levelStatus = response.subjectLevelStatus?.trim();
    if (levelStatus) {
      this.subjectLevelStatus.set(levelStatus);
    }
    this.cdr.markForCheck();
  }
}
