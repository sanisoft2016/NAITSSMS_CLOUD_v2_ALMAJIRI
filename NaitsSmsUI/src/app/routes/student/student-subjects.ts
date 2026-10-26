import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { distinctUntilChanged, finalize } from 'rxjs';

import { AccessStatus } from '@shared';

import {
  SelectOption,
  StudentSubjectRecord,
  SubjectsAndClassNameRecord,
  resolveSelectionMode,
  subjectStatusLabel,
} from './student-portal.models';
import { StudentPortalContextService } from './student-portal-context.service';
import { StudentPortalService } from './student-portal.service';

@Component({
  selector: 'app-student-subjects',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './student-subjects.html',
  styleUrl: './student-subjects.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentSubjects implements OnInit {
  private readonly portalService = inject(StudentPortalService);
  private readonly portalContext = inject(StudentPortalContextService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  categories: SelectOption[] = [];
  selectedCategory = '';
  searchParameter = '';
  loading = false;
  listLoading = false;
  actionBusy = false;
  message = '';
  subjectStatusFilter = '';
  /** Legacy SearchLevelsOfferingThisSuject uses a read-only partial (no Selections column). */
  isSearchMode = false;
  payload: SubjectsAndClassNameRecord | null = null;
  selectedIds = new Set<string>();

  ngOnInit(): void {
    this.portalContext.ensureLoaded();
    this.portalContext.selectedStudentId$
      .pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.reloadForSelectedStudent());
  }

  get sortedSubjects(): StudentSubjectRecord[] {
    const rows = this.payload?.subjectDtos ?? [];
    return [...rows].sort((a, b) => a.subjectName.localeCompare(b.subjectName));
  }

  get canMutate(): boolean {
    return Number(this.payload?.accessStatu) === AccessStatus.Active;
  }

  get showUnassign(): boolean {
    return !this.isSearchMode && this.canMutate && this.subjectStatusFilter === 'My-subject-list';
  }

  get showAssign(): boolean {
    return (
      !this.isSearchMode &&
      this.canMutate &&
      this.subjectStatusFilter !== 'My-subject-list' &&
      this.subjectStatusFilter !== ''
    );
  }

  onCategoryChange(): void {
    if (!this.selectedCategory) {
      this.payload = null;
      this.subjectStatusFilter = '';
      this.isSearchMode = false;
      this.selectedIds.clear();
      this.cdr.markForCheck();
      return;
    }

    this.isSearchMode = false;
    this.subjectStatusFilter = this.selectedCategory.trim();
    this.selectedIds.clear();
    this.listLoading = true;
    this.message = '';

    this.portalService
      .getSubjectsForCategory(this.subjectStatusFilter)
      .pipe(
        finalize(() => {
          this.listLoading = false;
          this.cdr.markForCheck();
        }),
      )
      .subscribe({
        next: payload => {
          this.payload = payload;
          this.cdr.markForCheck();
        },
        error: () => {
          this.payload = null;
          this.message = 'Unable to load subjects for the selected category.';
          this.cdr.markForCheck();
        },
      });
  }

  searchSubjects(): void {
    const parameter = this.searchParameter.trim();
    if (!parameter) {
      window.alert('Search Parameter must be provided to continue.');
      return;
    }

    this.isSearchMode = true;
    this.subjectStatusFilter = '';
    this.selectedCategory = '';
    this.selectedIds.clear();
    this.listLoading = true;
    this.message = '';

    this.portalService
      .searchSubjects(parameter)
      .pipe(
        finalize(() => {
          this.listLoading = false;
          this.cdr.markForCheck();
        }),
      )
      .subscribe({
        next: payload => {
          this.payload = payload;
          if (!payload.subjectDtos.length) {
            this.message = payload.emptyMessage || 'No Subject matched with the text you have just typed.';
          }
          this.cdr.markForCheck();
        },
        error: () => {
          this.payload = null;
          this.message = 'Unable to search subjects.';
          this.cdr.markForCheck();
        },
      });
  }

  selectionMode(subject: StudentSubjectRecord): 'compulsory' | 'checkbox' {
    return resolveSelectionMode(
      this.subjectStatusFilter || 'My-subject-list',
      subject,
      this.payload?.studentStatus,
      this.payload?.className,
      this.payload?.electiveStatus ?? [],
    );
  }

  categoryLabel(subject: StudentSubjectRecord): string {
    return subjectStatusLabel(subject);
  }

  timeStatusLabel(value: number | string | null | undefined): string {
    if (value === null || value === undefined || value === '') {
      return '';
    }
    if (typeof value === 'string' && Number.isNaN(Number(value))) {
      return value;
    }
    return Number(value) === 1 ? 'Previous' : 'Current';
  }

  toggleSelection(subjectId: string, checked: boolean): void {
    if (checked) {
      this.selectedIds.add(subjectId);
    } else {
      this.selectedIds.delete(subjectId);
    }
  }

  isSelected(subjectId: string): boolean {
    return this.selectedIds.has(subjectId);
  }

  submitSelections(): void {
    const subjectIds = [...this.selectedIds].map(id => ({ id }));
    if (!subjectIds.length) {
      window.alert('Failed! One or more subject(s) has to be selected to continue.');
      return;
    }

    this.actionBusy = true;
    this.portalService
      .assignSubjects(subjectIds)
      .pipe(
        finalize(() => {
          this.actionBusy = false;
          this.cdr.markForCheck();
        }),
      )
      .subscribe({
        next: status => {
          if (status === 'succeed') {
            window.alert('Your selections has Successfully been Submited.');
            this.onCategoryChange();
          } else if (status === 'Failed') {
            window.alert('Failed! You cannot select this elective your self. Please Contact administration.');
          } else {
            window.alert(status);
          }
        },
        error: () => window.alert('failed.'),
      });
  }

  removeSelections(): void {
    const subjectIds = [...this.selectedIds].map(id => ({ id }));
    if (!subjectIds.length) {
      window.alert('Failed! One or more subject(s) has to be selected to continue.');
      return;
    }

    this.actionBusy = true;
    this.portalService
      .unassignSubjects(subjectIds)
      .pipe(
        finalize(() => {
          this.actionBusy = false;
          this.cdr.markForCheck();
        }),
      )
      .subscribe({
        next: status => {
          if (status === 'succeed') {
            window.alert('Subject(s) has Successfully been Removed.');
            this.onCategoryChange();
          } else {
            window.alert(status || 'Failed');
          }
        },
        error: () => window.alert('failed.'),
      });
  }

  private reloadForSelectedStudent(): void {
    this.payload = null;
    this.selectedIds.clear();
    this.message = '';
    this.isSearchMode = false;
    this.searchParameter = '';
    this.selectedCategory = '';
    this.subjectStatusFilter = '';
    this.loading = true;
    this.cdr.markForCheck();

    this.portalService
      .getSubjectCategories()
      .pipe(
        finalize(() => {
          this.loading = false;
          this.cdr.markForCheck();
        }),
      )
      .subscribe({
        next: categories => {
          this.categories = categories;
          this.cdr.markForCheck();
        },
        error: () => {
          this.message = 'Unable to load subject categories.';
          this.cdr.markForCheck();
        },
      });
  }
}
