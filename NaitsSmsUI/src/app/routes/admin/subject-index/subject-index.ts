import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { BaseComponent, GenericService } from '@shared';

import {
  LabelEditorRecord,
  SubjectCategoryRecord,
  SubjectStatusCode,
  SUBJECT_STATUS_QUERY_NAMES,
  resolveSubjectLabel,
} from '../subjects/subject-registry.models';
import { SubjectRegistryService } from '../subjects/subject-registry.service';

@Component({
  selector: 'app-subject-index',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './subject-index.html',
  styleUrl: './subject-index.scss',
})
export class SubjectIndex extends BaseComponent implements OnInit {
  private readonly genericService = inject(GenericService<LabelEditorRecord>);
  private readonly subjectRegistry = inject(SubjectRegistryService);

  readonly SubjectStatusCode = SubjectStatusCode;

  labels: SubjectCategoryRecord[] = [];
  isLabelEditorOpen = false;
  isSeniorEnvironmentOpen = false;
  labelForm: LabelEditorRecord = {
    generalSujectLabel: '',
    electiveSubjectLabel: '',
    playClassLabel: '',
    preNurseryLabel: '',
    nurseryLabel: '',
    primaryLabel: '',
    jssLabel: '',
    ssLabel: '',
  };

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.loadLabels();
  }

  get playClassLabel(): string {
    return this.getLabel(SubjectStatusCode.PlayClass);
  }

  get preNurseryLabel(): string {
    return this.getLabel(SubjectStatusCode.PreNursery);
  }

  get nurseryLabel(): string {
    return this.getLabel(SubjectStatusCode.Nursery);
  }

  get primaryLabel(): string {
    return this.getLabel(SubjectStatusCode.Primary);
  }

  get jssLabel(): string {
    return this.getLabel(SubjectStatusCode.Jss);
  }

  get generalLabel(): string {
    return this.getLabel(SubjectStatusCode.General);
  }

  get electiveLabel(): string {
    return this.getLabel(SubjectStatusCode.Elective);
  }

  openSubjectIndex(subjectStatus: SubjectStatusCode, subjectLevelStatus?: string): void {
    this.isSeniorEnvironmentOpen = false;
    this.router.navigate(['/admin/subject-reg-index'], {
      queryParams: {
        subjectStatus: SUBJECT_STATUS_QUERY_NAMES[subjectStatus],
        subjectLevelStatus: subjectLevelStatus?.trim() || this.getLabel(subjectStatus),
      },
    });
  }

  openSeniorEnvironment(): void {
    this.isSeniorEnvironmentOpen = true;
    this.cdr.markForCheck();
  }

  closeSeniorEnvironment(): void {
    this.isSeniorEnvironmentOpen = false;
    this.cdr.markForCheck();
  }

  openLabelEditor(): void {
    this.patchLabelForm();
    this.isLabelEditorOpen = true;
    this.cdr.markForCheck();
  }

  closeLabelEditor(): void {
    this.isLabelEditorOpen = false;
    this.cdr.markForCheck();
  }

  openAllSubjects(): void {
    this.router.navigate(['/admin/all-subject-index']);
  }

  openStudentAssignment(): void {
    this.router.navigate(['/admin/subject-student-assignment']);
  }

  saveLabels(): void {
    if (!this.startSubmitting()) {
      return;
    }

    this.withRequestState(this.genericService.update<{ message: string }>('/Subject/labels', this.labelForm), {
      useLoading: false,
      errorMessage: 'Unable to update school level labels.',
    }).subscribe({
      next: response => {
        this.stopSubmitting();
        this.showAlert(response.message || 'School level labels updated successfully.', { type: 'success' });
        this.closeLabelEditor();
        this.loadLabels();
      },
      error: error => {
        this.stopSubmitting();
        this.showAlert(this.getHttpErrorMessage(error, 'Unable to update school level labels.'), { type: 'error' });
      },
    });
  }

  private loadLabels(): void {
    this.withRequestState(this.subjectRegistry.getSubjectLabels(), {
      errorMessage: 'Unable to load subject labels.',
    }).subscribe({
      next: labels => {
        this.labels = labels ?? [];
        this.patchLabelForm();
        this.cdr.markForCheck();
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Unable to load subject labels.'), { type: 'error' });
        this.cdr.markForCheck();
      },
    });
  }

  getLabel(subjectStatus: SubjectStatusCode): string {
    return resolveSubjectLabel(this.labels, subjectStatus);
  }

  private patchLabelForm(): void {
    this.labelForm = {
      generalSujectLabel: this.generalLabel,
      electiveSubjectLabel: this.electiveLabel,
      playClassLabel: this.playClassLabel,
      preNurseryLabel: this.preNurseryLabel,
      nurseryLabel: this.nurseryLabel,
      primaryLabel: this.primaryLabel,
      jssLabel: this.jssLabel,
      ssLabel: this.getLabel(SubjectStatusCode.AllSeniors),
    };
  }

}
