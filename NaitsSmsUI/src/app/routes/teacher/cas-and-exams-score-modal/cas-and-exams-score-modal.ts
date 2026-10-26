import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnInit,
  Output,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';

import { BaseComponent } from '@shared';

import {
  CasCategoryName,
  ScoreRowState,
  computeAssessmentTotal,
  computeExamsMax,
  mapAssessmentToScoreRow,
  normalizeCasCategory,
  readCasSetting,
  readPerCasValue,
  readStudentsAccessment,
  sortScoreRows,
} from '../teacher-portal.helpers';
import { StudentsAccessment, StudentsAccessmentCasSetting } from '../teacher-portal.models';
import { TeacherPortalService } from '../teacher-portal.service';

type OrderByValue = '0' | '1' | '2';

@Component({
  selector: 'app-cas-and-exams-score-modal',
  imports: [CommonModule, FormsModule],
  templateUrl: './cas-and-exams-score-modal.html',
  styleUrl: './cas-and-exams-score-modal.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CasAndExamsScoreModal extends BaseComponent implements OnInit {
  private readonly teacherPortal = inject(TeacherPortalService);

  constructor() {
    super();
  }

  @Input({ required: true }) armId = '';
  @Input({ required: true }) subjectId = '';
  @Input({ required: true }) subjectName = '';
  @Input({ required: true }) classArm = '';

  @Output() readonly closed = new EventEmitter<void>();

  readonly rows = signal<ScoreRowState[]>([]);
  readonly casCategory = signal<CasCategoryName>('ThreeCAs');
  readonly perCasValue = signal(0);
  readonly examsMaxValue = signal(0);
  readonly orderBy = signal<OrderByValue>('0');
  readonly saving = signal(false);
  readonly loadingUnentered = signal<'ca' | 'ca-exams' | null>(null);

  readonly orderedRows = computed(() => sortScoreRows(this.rows(), this.orderBy()));
  readonly computeAssessmentTotal = computeAssessmentTotal;

  ngOnInit(): void {
    this.loadScores();
  }

  loadScores(): void {
    this.withRequestState(this.teacherPortal.getStudentsCaSetting(this.armId, this.subjectId), {
      errorMessage: 'Unable to load CA and exam scores.',
      rethrow: false,
    }).subscribe(payload => this.applyPayload(payload));
  }

  loadUnenteredCa(): void {
    this.loadingUnentered.set('ca');
    this.withRequestState(this.teacherPortal.getUnenteredCaScores(this.armId, this.subjectId), {
      errorMessage: 'Unable to load un-entered CA scores.',
      rethrow: false,
    })
      .pipe(finalize(() => this.loadingUnentered.set(null)))
      .subscribe(payload => this.applyPayload(payload));
  }

  loadUnenteredCaAndExams(): void {
    this.loadingUnentered.set('ca-exams');
    this.withRequestState(this.teacherPortal.getUnenteredCaAndExamScores(this.armId, this.subjectId), {
      errorMessage: 'Unable to load un-entered CA and exam scores.',
      rethrow: false,
    })
      .pipe(finalize(() => this.loadingUnentered.set(null)))
      .subscribe(payload => this.applyPayload(payload));
  }

  onOrderByChange(value: string): void {
    this.orderBy.set(value === '1' || value === '2' ? value : '0');
  }

  /**
   * Legacy CasAndExamsScore.cshtml `.eachStudent` focusout:
   * validate CA/exam against max; if out of range alert, reset to 0, and stop.
   * Totals recompute via the template.
   */
  onRowFocusOut(row: ScoreRowState): void {
    const caMax = this.perCasValue();
    const examsMax = this.examsMaxValue();
    const category = this.casCategory();

    const asNumber = (value: number | null | undefined): number => {
      if (value == null || Number.isNaN(Number(value))) {
        return 0;
      }
      return Number(value);
    };

    const exams = asNumber(row.exams);
    if (exams > examsMax) {
      alert(`The exam's score is out of range! The maximum value is = ${examsMax}`);
      this.updateRowValue(row, 'exams', 0);
      return;
    }

    const ca1 = asNumber(row.ca1);
    if (ca1 > caMax) {
      alert(`The CA's score is out of range! The maximum value is = ${caMax}`);
      this.updateRowValue(row, 'ca1', 0);
      return;
    }

    if (category === 'TwoCAs' || category === 'ThreeCAs') {
      const ca2 = asNumber(row.ca2);
      if (ca2 > caMax) {
        alert(`The CA's score is out of range! The maximum value is = ${caMax}`);
        this.updateRowValue(row, 'ca2', 0);
        return;
      }
    }

    if (category === 'ThreeCAs') {
      const ca3 = asNumber(row.ca3);
      if (ca3 > caMax) {
        alert(`The CA's score is out of range! The maximum value is = ${caMax}`);
        this.updateRowValue(row, 'ca3', 0);
      }
    }
  }

  submitScores(): void {
    const rows = this.orderedRows();
    const caMax = this.perCasValue();
    const examsMax = this.examsMaxValue();
    const category = this.casCategory();

    for (const row of rows) {
      const caFields: Array<{ field: 'ca1' | 'ca2' | 'ca3'; value: number | null }> =
        category === 'ThreeCAs'
          ? [
              { field: 'ca1', value: row.ca1 },
              { field: 'ca2', value: row.ca2 },
              { field: 'ca3', value: row.ca3 },
            ]
          : category === 'TwoCAs'
            ? [
                { field: 'ca1', value: row.ca1 },
                { field: 'ca2', value: row.ca2 },
              ]
            : [{ field: 'ca1', value: row.ca1 }];

      for (const entry of caFields) {
        if (entry.value == null || Number.isNaN(Number(entry.value))) {
          // Legacy SubmitValues: "One or more required Value were not entered."
          alert('One or more required Value were not entered.');
          this.focusScoreInput(row.assessmentId, entry.field);
          return;
        }

        if (Number(entry.value) > caMax) {
          // Legacy SubmitValues: "Value out of range."
          alert('Value out of range.');
          this.focusScoreInput(row.assessmentId, entry.field);
          return;
        }
      }

      if (row.exams == null || Number.isNaN(Number(row.exams))) {
        alert('One or more required Value were not entered.');
        this.focusScoreInput(row.assessmentId, 'exams');
        return;
      }

      if (Number(row.exams) > examsMax) {
        alert('Value out of range.');
        this.focusScoreInput(row.assessmentId, 'exams');
        return;
      }
    }

    const payload: StudentsAccessment[] = rows.map(row => ({
      Id: row.assessmentId,
      YearlyAndTermlyStatusId: row.yearlyAndTermlyStatusId,
      ClassArmId: row.classArmId,
      StudentId: row.studentId,
      SubjectId: row.subjectId,
      Ca1: Number(row.ca1 ?? 0),
      Ca2: category === 'OneCA' ? 0 : Number(row.ca2 ?? 0),
      Ca3: category === 'ThreeCAs' ? Number(row.ca3 ?? 0) : 0,
      Exams: Number(row.exams ?? 0),
    }));

    this.saving.set(true);
    this.withRequestState(this.teacherPortal.saveCaAndExamScores(payload), {
      errorMessage: 'Unable to save CA and exam scores.',
      rethrow: false,
    })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe(() => {
        alert('Students Scores has Successfully been Submited.');
        this.closeModal();
      });
  }

  printScores(): void {
    const content = document.getElementById('teacherScorePrintArea');
    if (!content) {
      alert('Nothing to print.');
      return;
    }

    // Clone live DOM so current input values are included; drop the Save row for print.
    const clone = content.cloneNode(true) as HTMLElement;
    clone.querySelectorAll('input').forEach(node => {
      const input = node as HTMLInputElement;
      input.setAttribute('value', input.value);
    });
    clone.querySelectorAll('button').forEach(button => {
      const row = button.closest('tr');
      (row ?? button).remove();
    });

    // Do not use noopener/noreferrer — Chromium returns null and print cannot run.
    const printWindow = window.open('', '_blank', 'width=980,height=720');
    if (!printWindow) {
      alert('Unable to open print preview. Please allow pop-ups for this site.');
      return;
    }

    const styles = `
      body { font-family: Arial, Helvetica, sans-serif; margin: 16px; color: #333; }
      table { width: 100%; border-collapse: collapse; }
      th, td { border: 1px solid #ddd; padding: 8px; background: #fff; vertical-align: middle; }
      .highlight { color: red; }
      .score-input { height: 25px; max-width: 70px; width: 100%; border: 1px solid #ccc; padding: 2px 4px; }
      .nowrap-cell, .nowrap-row { white-space: nowrap; }
      .text-center { text-align: center; }
      .text-right { text-align: right; }
    `;

    printWindow.document.open();
    printWindow.document.write(
      `<!DOCTYPE html><html><head><meta charset="utf-8" /><title>CA and Exams Scores</title>` +
        `<style>${styles}</style></head><body>${clone.outerHTML}</body></html>`,
    );
    printWindow.document.close();
    printWindow.focus();

    const closePrintWindow = () => {
      try {
        printWindow.close();
      } catch {
        /* ignore */
      }
    };

    printWindow.addEventListener('afterprint', closePrintWindow);
    // Give the new document a tick to render before invoking the print dialog.
    setTimeout(() => {
      printWindow.print();
      // Fallback when afterprint is not fired (some browsers).
      setTimeout(closePrintWindow, 1000);
    }, 200);
  }

  closeModal(): void {
    this.closed.emit();
  }

  scoreInputId(
    assessmentId: string,
    field: keyof Pick<ScoreRowState, 'ca1' | 'ca2' | 'ca3' | 'exams'>,
  ): string {
    return `score-${assessmentId}-${field}`;
  }

  updateRowValue(
    row: ScoreRowState,
    field: keyof Pick<ScoreRowState, 'ca1' | 'ca2' | 'ca3' | 'exams'>,
    rawValue: string | number | null,
  ): void {
    const parsed = rawValue == null || rawValue === '' ? null : Number(rawValue);
    this.rows.update(rows =>
      rows.map(item => (item.assessmentId === row.assessmentId ? { ...item, [field]: parsed } : item)),
    );
  }

  private focusScoreInput(
    assessmentId: string,
    field: keyof Pick<ScoreRowState, 'ca1' | 'ca2' | 'ca3' | 'exams'>,
  ): void {
    const element = document.getElementById(this.scoreInputId(assessmentId, field));
    if (element instanceof HTMLInputElement) {
      element.focus();
      element.select();
    }
  }

  private applyPayload(payload: StudentsAccessmentCasSetting | null | undefined): void {
    const casSetting = readCasSetting(payload);
    const students = readStudentsAccessment(payload);

    this.casCategory.set(normalizeCasCategory(casSetting.casCategory ?? casSetting.CasCategory));
    this.perCasValue.set(readPerCasValue(casSetting));
    this.examsMaxValue.set(computeExamsMax(casSetting));
    this.rows.set(students.map(row => mapAssessmentToScoreRow(row, this.subjectId, this.armId)));
  }
}
