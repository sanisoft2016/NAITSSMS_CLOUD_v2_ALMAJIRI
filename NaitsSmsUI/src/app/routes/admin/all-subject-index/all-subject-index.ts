import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';

import { BaseComponent } from '@shared';

import {
  SelectOptionRecord,
  SubjectRecord,
  SubjectSavePayload,
  SubjectStatusName,
  TimeStatusCode,
  subjectCategoryDisplayLabel,
  subjectStatusToQueryName,
  timeStatusLabel,
} from '../subjects/subject-registry.models';
import { SubjectRegistryService } from '../subjects/subject-registry.service';

@Component({
  selector: 'app-all-subject-index',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './all-subject-index.html',
  styleUrl: './all-subject-index.scss',
})
export class AllSubjectIndex extends BaseComponent implements OnInit {
  private readonly subjectRegistry = inject(SubjectRegistryService);

  readonly TimeStatusCode = TimeStatusCode;

  subjectOptions: SelectOptionRecord[] = [];
  selectedSubjectName = '';
  searchParameter = '';
  subjects: SubjectRecord[] = [];
  infoMessage = 'Select a subject or search by subject name to load all levels offering that subject.';

  editingSubject: SubjectRecord | null = null;
  editingSubjectStatus: SubjectStatusName = 'PlayClass';
  isEditModalOpen = false;
  editForm: SubjectSavePayload = {
    subjectName: '',
    timeStatus: TimeStatusCode.Current,
  };

  private activeSearchMode: 'dropdown' | 'search' | null = null;
  private activeSearchValue = '';

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.loadDropdown();
  }

  backToIndex(): void {
    this.router.navigate(['/admin/subjects']);
  }

  onSelectedSubjectChange(subjectName: string): void {
    this.selectedSubjectName = subjectName;

    if (!subjectName) {
      this.activeSearchMode = null;
      this.activeSearchValue = '';
      this.subjects = [];
      this.infoMessage = 'Select a subject or search by subject name to load all levels offering that subject.';
      return;
    }

    this.activeSearchMode = 'dropdown';
    this.activeSearchValue = subjectName;
    this.withRequestState(this.subjectRegistry.getSubjectLevels(subjectName), {
      errorMessage: 'Unable to load levels offering this subject.',
    }).subscribe({
      next: subjects => this.applyResults(subjects),
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Unable to load levels offering this subject.'), { type: 'error' });
      },
    });
  }

  loadSelectedSubjectLevels(): void {
    if (!this.selectedSubjectName) {
      this.showAlert('Select a subject from the dropdown first.', { type: 'warning' });
      return;
    }

    this.activeSearchMode = 'dropdown';
    this.activeSearchValue = this.selectedSubjectName;
    this.withRequestState(this.subjectRegistry.getSubjectLevels(this.selectedSubjectName), {
      errorMessage: 'Unable to load levels offering this subject.',
    }).subscribe({
      next: subjects => this.applyResults(subjects),
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Unable to load levels offering this subject.'), { type: 'error' });
      },
    });
  }

  searchSubjectLevels(): void {
    const value = this.searchParameter.trim();
    if (!value) {
      this.showAlert('Enter a subject name to search.', { type: 'warning' });
      return;
    }

    this.activeSearchMode = 'search';
    this.activeSearchValue = value;
    this.withRequestState(this.subjectRegistry.searchSubjectLevels(value), {
      errorMessage: 'Unable to search subject levels.',
    }).subscribe({
      next: subjects => this.applyResults(subjects),
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Unable to search subject levels.'), { type: 'error' });
      },
    });
  }

  openEditModal(subject: SubjectRecord): void {
    this.editingSubject = subject;
    this.editingSubjectStatus = subjectStatusToQueryName(subject.subjectCategory?.subjectStatus ?? null);
    this.editForm = {
      subjectName: subject.subjectName?.trim() ?? '',
      timeStatus: Number(subject.timeStatus) === TimeStatusCode.Previous
        ? TimeStatusCode.Previous
        : TimeStatusCode.Current,
      subjectCategoryId: subject.subjectCategoryId ?? undefined,
    };
    this.isEditModalOpen = true;
  }

  closeEditModal(): void {
    this.isEditModalOpen = false;
    this.editingSubject = null;
    this.editForm = {
      subjectName: '',
      timeStatus: TimeStatusCode.Current,
    };
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

    if (!this.startSubmitting()) {
      return;
    }

    const payload: SubjectSavePayload = {
      ...this.editForm,
      subjectName,
    };

    this.withRequestState(this.subjectRegistry.updateSubject(this.editingSubject.id, this.editingSubjectStatus, payload), {
      useLoading: false,
      errorMessage: 'Unable to update subject.',
    })
      .pipe(finalize(() => this.stopSubmitting()))
      .subscribe({
        next: response => {
          this.showAlert(response.message || 'Subject updated successfully.', { type: 'success' });
          this.closeEditModal();
          this.reloadResults();
          this.cdr.markForCheck();
        },
        error: error => {
          this.showAlert(this.getHttpErrorMessage(error, 'Unable to update subject.'), { type: 'error' });
          this.cdr.markForCheck();
        },
      });
  }

  deleteSubject(subject: SubjectRecord): void {
    if (!window.confirm(`Delete ${subject.subjectName || 'this subject'}?`)) {
      return;
    }

    this.withRequestState(this.subjectRegistry.deleteSubject(subject.id), {
      errorMessage: 'Unable to delete subject.',
    }).subscribe({
      next: response => {
        this.showAlert(response.message || 'Subject deleted successfully.', { type: 'success' });
        this.reloadResults();
        this.cdr.markForCheck();
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Unable to delete subject.'), { type: 'error' });
        this.cdr.markForCheck();
      },
    });
  }

  getSubjectCategoryLabel(subject: SubjectRecord): string {
    return subjectCategoryDisplayLabel(subject);
  }

  getTimeStatusLabel(subject: SubjectRecord): string {
    return timeStatusLabel(subject.timeStatus);
  }

  private loadDropdown(): void {
    this.withRequestState(this.subjectRegistry.getAllSubjectDropdown(), {
      errorMessage: 'Unable to load subject dropdown.',
    }).subscribe({
      next: options => {
        this.subjectOptions = options ?? [];
        this.cdr.markForCheck();
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Unable to load subject dropdown.'), { type: 'error' });
        this.cdr.markForCheck();
      },
    });
  }

  private applyResults(subjects: SubjectRecord[]): void {
    this.subjects = subjects ?? [];
    this.infoMessage = this.subjects.length > 0
      ? `${this.subjects.length} record(s) loaded.`
      : 'No level currently offers the requested subject.';
    this.cdr.markForCheck();
  }

  private reloadResults(): void {
    if (this.activeSearchMode === 'dropdown' && this.activeSearchValue) {
      this.withRequestState(this.subjectRegistry.getSubjectLevels(this.activeSearchValue), {
        errorMessage: 'Unable to refresh levels offering this subject.',
      }).subscribe({
        next: subjects => this.applyResults(subjects),
        error: error => {
          this.showAlert(this.getHttpErrorMessage(error, 'Unable to refresh levels offering this subject.'), { type: 'error' });
        },
      });
      return;
    }

    if (this.activeSearchMode === 'search' && this.activeSearchValue) {
      this.withRequestState(this.subjectRegistry.searchSubjectLevels(this.activeSearchValue), {
        errorMessage: 'Unable to refresh subject search.',
      }).subscribe({
        next: subjects => this.applyResults(subjects),
        error: error => {
          this.showAlert(this.getHttpErrorMessage(error, 'Unable to refresh subject search.'), { type: 'error' });
        },
      });
      return;
    }

    this.subjects = [];
  }
}