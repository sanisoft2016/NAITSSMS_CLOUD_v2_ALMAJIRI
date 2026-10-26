import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';

import { BaseComponent } from '@shared';

import { CasAndExamsScoreModal } from '../cas-and-exams-score-modal/cas-and-exams-score-modal';
import {
  readArmId,
  readClassArmLabel,
  readSubjectId,
  readSubjectName,
  sortAssignments,
} from '../teacher-portal.helpers';
import { AssignmentListMode, ScoreModalContext, TeacherSubjectArmAssignment } from '../teacher-portal.models';
import { TeacherPortalService } from '../teacher-portal.service';

@Component({
  selector: 'app-assigned-subject-and-arm',
  imports: [CommonModule, CasAndExamsScoreModal],
  templateUrl: './assigned-subject-and-arm.html',
  styleUrl: './assigned-subject-and-arm.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AssignedSubjectAndArm extends BaseComponent implements OnInit {
  private readonly teacherPortal = inject(TeacherPortalService);

  constructor() {
    super();
  }

  readonly assignments = signal<TeacherSubjectArmAssignment[]>([]);
  readonly listMode = signal<AssignmentListMode>('all');
  readonly scoreModalContext = signal<ScoreModalContext | null>(null);
  readonly loadingScoresKey = signal('');

  readonly readSubjectName = readSubjectName;
  readonly readClassArmLabel = readClassArmLabel;

  ngOnInit(): void {
    this.loadAssignments('all');
  }

  loadAssignments(mode: AssignmentListMode): void {
    this.listMode.set(mode);

    const request$ =
      mode === 'uncompleted-ca-exams'
        ? this.teacherPortal.getUncompletedCaAndExams()
        : mode === 'uncompleted-ca'
          ? this.teacherPortal.getUncompletedCa()
          : this.teacherPortal.getAssignedSubjectsAndArms();

    this.withRequestState(request$, {
      errorMessage: 'Unable to load assigned subjects and classes.',
      rethrow: false,
    }).subscribe(rows => {
      this.assignments.set(sortAssignments(rows ?? []));
    });
  }

  openScores(item: TeacherSubjectArmAssignment): void {
    const armId = readArmId(item);
    const subjectId = readSubjectId(item);
    const subjectName = readSubjectName(item);
    const classArm = readClassArmLabel(item);

    if (!armId || !subjectId) {
      this.showAlert('Missing class arm or subject information for this assignment.', { type: 'warning' });
      return;
    }

    this.loadingScoresKey.set(`${armId}:${subjectId}`);
    this.scoreModalContext.set({
      armId,
      subjectId,
      subjectName,
      classArm,
    });
  }

  closeScoreModal(): void {
    this.scoreModalContext.set(null);
    this.loadingScoresKey.set('');
  }

  scoresRowKey(item: TeacherSubjectArmAssignment): string {
    return `${readArmId(item)}:${readSubjectId(item)}`;
  }

  isScoresLoading(item: TeacherSubjectArmAssignment): boolean {
    return this.loadingScoresKey() === this.scoresRowKey(item);
  }
}
