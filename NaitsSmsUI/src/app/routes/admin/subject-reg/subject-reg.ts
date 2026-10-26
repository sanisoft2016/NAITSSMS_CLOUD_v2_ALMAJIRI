import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize, map } from 'rxjs';

import { BaseComponent } from '@shared';

import {
  SubjectSavePayload,
  SubjectStatusName,
  TimeStatusCode,
  subjectStatusToQueryName,
} from '../subjects/subject-registry.models';
import { SubjectRegistryService } from '../subjects/subject-registry.service';

@Component({
  selector: 'app-subject-reg',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './subject-reg.html',
  styleUrl: './subject-reg.scss',
})
export class SubjectReg extends BaseComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly subjectRegistry = inject(SubjectRegistryService);

  readonly TimeStatusCode = TimeStatusCode;

  subjectStatus: SubjectStatusName = 'PlayClass';
  subjectLevelStatus = '';
  readonly subjectForm = this.fb.nonNullable.group({
    subjectName: ['', [Validators.required, Validators.maxLength(35)]],
    timeStatus: [TimeStatusCode.Current, [Validators.required]],
  });

  constructor() {
    super();

    this.route.queryParamMap
      .pipe(
        map(query => ({
          subjectStatus: subjectStatusToQueryName(query.get('subjectStatus')),
          subjectLevelStatus: query.get('subjectLevelStatus')?.trim() || '',
        })),
        takeUntilDestroyed(),
      )
      .subscribe(({ subjectStatus, subjectLevelStatus }) => {
        this.subjectStatus = subjectStatus;
        this.subjectLevelStatus = subjectLevelStatus || subjectStatus;
        this.cdr.markForCheck();
      });
  }

  ngOnInit(): void {
    // Query params are subscribed in the constructor for takeUntilDestroyed().
  }

  backToList(): void {
    this.router.navigate(['/admin/subject-reg-index'], {
      queryParams: {
        subjectStatus: this.subjectStatus,
        subjectLevelStatus: this.subjectLevelStatus,
      },
    });
  }

  submit(): void {
    this.subjectForm.markAllAsTouched();

    if (this.subjectForm.invalid) {
      this.showAlert('Enter a subject name before saving.', { type: 'warning' });
      return;
    }

    if (!this.startSubmitting()) {
      return;
    }

    const formValue = this.subjectForm.getRawValue();
    const subjectName = formValue.subjectName.trim();
    const payload: SubjectSavePayload = {
      ...formValue,
      subjectName,
    };

    this.withRequestState(this.subjectRegistry.createSubject(this.subjectStatus, payload), {
      useLoading: false,
      errorMessage: 'Unable to create subject.',
    })
      .pipe(finalize(() => this.stopSubmitting()))
      .subscribe({
        next: response => {
          this.showAlert(response.message || 'Subject created successfully.', { type: 'success' });
          this.backToList();
        },
        error: error => {
          this.showAlert(this.getHttpErrorMessage(error, 'Unable to create subject.'), { type: 'error' });
          this.cdr.markForCheck();
        },
      });
  }
}
