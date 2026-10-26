import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpParams } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

import { BaseComponent, GenericService } from '@shared';

import {
  ClassOption,
  NewAssessmentCollectionMode,
  SelectOption,
  UncompletedFormMasterRow,
  isAssessmentSuccessMessage,
  normalizeClasses,
  normalizeSelectListItems,
  normalizeUncompletedFormMasters,
  readDefaultedTeachersMessage,
  readResponseMessage,
} from './new-assessment-handler.models';

@Component({
  selector: 'app-new-assessment-handler',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './new-assessment-handler.html',
  styleUrl: './new-assessment-handler.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewAssessmentHandler extends BaseComponent implements OnInit {
  private readonly api = inject(GenericService);

  readonly sessions = signal<SelectOption[]>([]);
  readonly terms = signal<SelectOption[]>([]);
  readonly classOptions = signal<ClassOption[]>([]);
  readonly armOptions = signal<SelectOption[]>([]);
  readonly statusMessage = signal('');
  readonly statusSuccess = signal(false);
  readonly showDefaultedModal = signal(false);
  readonly defaultedTeachers = signal<UncompletedFormMasterRow[]>([]);
  readonly defaultedMessage = signal('');

  /** Legacy default Collection Size option is Per Arm Students ("2"). */
  collectionType: NewAssessmentCollectionMode = '2';
  sessionId = '';
  termStatus = '0';
  classValue = '-1';
  armValue = '-1';
  assessmentCategory: '0' | '1' = '0';
  actionType: '0' | '1' = '0';
  psychoAffectDefaultValue = '';

  /** Mirrors legacy #psychoAffectDefaultValueDiv no-display class. */
  showPsychoAffectField = false;

  get showClassField(): boolean {
    const collection = String(this.collectionType);
    return collection === '1' || collection === '2';
  }

  get showArmField(): boolean {
    return String(this.collectionType) === '2';
  }

  constructor() {
    super();
  }

  ngOnInit(): void {
    // Legacy NewAssessmentHandler() sets ViewBag.session / term / classes / armName once on load.
    this.loadBootstrap();
  }

  compareOptionValues(left: string | number, right: string | number): boolean {
    return String(left) === String(right);
  }

  /**
   * Legacy #assessmentCategory change:
   * hide psycho on Delete; otherwise show only for Psychomotor/Affective.
   */
  onAssessmentCategoryChange(): void {
    this.clearPageResult();
    if (this.actionType === '1') {
      this.showPsychoAffectField = false;
      this.psychoAffectDefaultValue = '';
    } else {
      this.showPsychoAffectField = this.assessmentCategory === '1';
    }
    this.cdr.markForCheck();
  }

  /**
   * Legacy #actionType change:
   * Create + Psychomotor => show psycho; Delete => hide (and ignore any leftover default).
   */
  onActionTypeChange(): void {
    this.clearPageResult();
    if (this.actionType === '0' && this.assessmentCategory === '1') {
      this.showPsychoAffectField = true;
    } else if (this.actionType === '1') {
      this.showPsychoAffectField = false;
      this.psychoAffectDefaultValue = '';
    }
    this.cdr.markForCheck();
  }

  /**
   * Legacy #collectionSize change.
   * "0" = All Students → confirm + GeneralNewAssessmentProcessing.
   * "1"/"2" = show Class / Class+Arm only (no immediate process).
   */
  onCollectionSizeChange(): void {
    this.clearPageResult();

    if (String(this.collectionType) === '0') {
      this.classValue = '-1';
      this.armValue = '-1';
      this.runGeneralAssessment();
      this.cdr.markForCheck();
      return;
    }

    this.classValue = '-1';
    this.armValue = '-1';
    this.cdr.markForCheck();
  }

  /**
   * Legacy #ClassName change (NewAssessmentHandler.cshtml).
   * Runs only when Collection Size = Per Class Students ("1").
   * When Assessment Category = Psychomotor/Affective ("1"):
   *   Create → requires psycho default (≤ 6), GET PerClassNewAssessmentProcessing
   *   Delete → confirm, then same endpoint without psycho default
   * Service: PrepareAssessmentEnvironmentForaClassStudents → PrepareEnvironmentForStudentAssessmentReturnDto
   */
  onClassChange(): void {
    // Legacy does not reset Arm on Class change; keep Per-Arm UX usable by clearing Arm only.
    const collection = String(this.collectionType);
    if (collection === '2') {
      this.armValue = '-1';
    }
    this.clearPageResult();

    if (this.classValue === '-1') {
      this.cdr.markForCheck();
      return;
    }

    // Legacy: Class change only processes when Collection Size = Per Class Students ("1").
    // Per Arm ("2") waits for the Arm change event — do not call the API here.
    if (collection === '2') {
      this.statusMessage.set('Class selected. Choose an Arm to generate or delete assessment records.');
      this.statusSuccess.set(false);
      this.cdr.markForCheck();
      return;
    }

    if (collection !== '1') {
      this.cdr.markForCheck();
      return;
    }

    if (String(this.actionType) === '1') {
      const confirmed = window.confirm(
        "Are you sure you want to DELETE the current term assessment? Note: this action is irreversible. If 'Yes' click 'Ok' to continue."
      );
      if (!confirmed) {
        this.cdr.markForCheck();
        return;
      }
    }

    const concatParameters =
      String(this.assessmentCategory) === '1'
        ? this.buildPsychomotorClassConcatParameters()
        : this.buildVariousSubjectsClassConcatParameters();

    if (concatParameters == null) {
      this.cdr.markForCheck();
      return;
    }

    this.postConcatResult('/Registry/new-assessment-handler/per-class', concatParameters);
  }

  /**
   * Legacy #ArmName change (NewAssessmentHandler.cshtml).
   * Runs only when Collection Size = Per Arm Students ("2").
   * When Assessment Category = Psychomotor/Affective ("1"):
   *   Create → requires psycho default (≤ 6), GET PerArmNewAssessmentProcessing
   *   Delete → confirm, then same endpoint without psycho default
   * Service: PrepareAssessmentEnvironmentForAnArmStudents → PrepareEnvironmentForStudentAssessmentReturnDto
   * Empty/null message is treated as "Success" (legacy Arm JS).
   */
  onArmChange(): void {
    this.clearPageResult();

    if (this.armValue === '-1') {
      this.cdr.markForCheck();
      return;
    }

    // Legacy: Arm change only processes when Collection Size = Per Arm Students ("2").
    if (String(this.collectionType) !== '2') {
      this.cdr.markForCheck();
      return;
    }

    if (this.classValue === '-1') {
      this.showAlert('Select a Class before choosing an Arm.', { type: 'warning' });
      this.armValue = '-1';
      this.cdr.markForCheck();
      return;
    }

    if (String(this.actionType) === '1') {
      const confirmed = window.confirm(
        "Are you sure you want to DELETE the current term assessment? Note: this action is irreversible. If 'Yes' click 'Ok' to continue."
      );
      if (!confirmed) {
        this.cdr.markForCheck();
        return;
      }
    }

    const concatParameters =
      String(this.assessmentCategory) === '1'
        ? this.buildPsychomotorArmConcatParameters()
        : this.buildVariousSubjectsArmConcatParameters();

    if (concatParameters == null) {
      this.cdr.markForCheck();
      return;
    }

    this.postConcatResult('/Registry/new-assessment-handler/per-arm', concatParameters, true);
  }

  openDefaultedTeachers(): void {
    this.withRequestState(this.loadDefaultedTeachersReport(), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Failed to load defaulted teachers.',
      notifyOnError: 'none',
      rethrow: true,
    }).subscribe({
      next: response => {
        const teachers = normalizeUncompletedFormMasters(response);
        this.defaultedTeachers.set(teachers);
        this.defaultedMessage.set(readDefaultedTeachersMessage(response, teachers.length));
        this.showDefaultedModal.set(true);
        this.cdr.markForCheck();
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to load defaulted teachers.'), { type: 'error' });
      },
    });
  }

  closeDefaultedTeachers(): void {
    this.showDefaultedModal.set(false);
    this.cdr.markForCheck();
  }

  printDefaultedTeachers(): void {
    const printWindow = window.open('', '_blank', 'width=900,height=700');
    if (!printWindow) {
      this.showAlert('Unable to open print window. Please allow pop-ups and try again.', { type: 'warning' });
      return;
    }

    const pageContent = document.getElementById('defaultedTeachersPageContent');
    if (!pageContent) {
      this.showAlert('Unable to find printable content.', { type: 'warning' });
      return;
    }

    printWindow.document.write(pageContent.outerHTML);
    printWindow.document.close();
    printWindow.focus();
    printWindow.print();
    printWindow.close();
  }

  private loadBootstrap(): void {
    this.withRequestState(this.api.getAnObj('/Registry/new-assessment-handler'), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Failed to load assessment handler.',
      notifyOnError: 'none',
      rethrow: true,
    }).subscribe({
      next: response => {
        this.applyBootstrap(response);
        this.cdr.markForCheck();
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to load assessment handler.'), { type: 'error' });
      },
    });
  }

  /**
   * Apply legacy ViewBag.session / term / classes / armName shape.
   * Session/Term lists contain exactly the selected current item(s).
   */
  private applyBootstrap(response: unknown): void {
    const body = this.unwrapResponseBody(response) as Record<string, unknown> | null;
    const sessionItems = normalizeSelectListItems(
      body?.['session'] ?? body?.['Session'] ?? []
    ).filter(option => option.value !== '-1' && option.value !== '');
    const termItems = normalizeSelectListItems(body?.['term'] ?? body?.['Term'] ?? []);
    const classItemsRaw = body?.['classes'] ?? body?.['Classes'] ?? [];
    const armItems = normalizeSelectListItems(body?.['armName'] ?? body?.['ArmName'] ?? []).filter(
      option => option.value !== '-1'
    );

    // Legacy DropDownList only contains the current session item.
    this.sessions.set(sessionItems);
    this.sessionId = sessionItems[0]?.value ?? '';

    const hasValidTerm = termItems.some(option => option.value !== '-1' && option.value !== '');
    if (hasValidTerm) {
      const currentTerm = termItems.find(option => option.value !== '-1') ?? termItems[0];
      this.terms.set([currentTerm]);
      this.termStatus = currentTerm.value;
    } else {
      this.terms.set([{ text: 'None', value: '-1' }]);
      this.termStatus = '-1';
    }

    // Class dropdown values are "enum,label" from legacy StudentIndexClassAndArmDropDown.
    const classOptionsFromSelect = normalizeSelectListItems(classItemsRaw)
      .filter(option => option.value && option.value !== '-1')
      .map(option => {
        const [enumPart, ...labelParts] = option.value.split(',');
        const classNameEnum = Number(enumPart);
        return {
          classId: '',
          classNameEnum: Number.isFinite(classNameEnum) ? classNameEnum : 0,
          text: option.text || labelParts.join(','),
          value: option.value,
        } as ClassOption;
      });

    if (classOptionsFromSelect.length > 0) {
      this.classOptions.set(classOptionsFromSelect);
    } else {
      // Fallback if bootstrap returns ClassDto objects.
      this.classOptions.set(normalizeClasses(classItemsRaw));
    }

    this.armOptions.set(armItems);
    this.classValue = '-1';
    this.armValue = '-1';
  }

  private runGeneralAssessment(): void {
    let concatParameters = '';

    if (this.actionType === '1') {
      const confirmed = window.confirm(
        "Are you sure you want to DELETE the whole students Current term assessment? Note: this action is irreversible and may take a while to complete. To continue click 'Ok' otherwise click 'Cancel' to terminate."
      );
      if (!confirmed) {
        return;
      }
      concatParameters = `${this.assessmentCategory},${this.actionType}`;
    } else {
      const confirmed = window.confirm(
        "Are you sure you want to process the whole students Current term assessment? Note: this may take a while to complete. To continue click 'Ok' otherwise click 'Cancel' to terminate."
      );
      if (!confirmed) {
        return;
      }

      if (!this.showPsychoAffectField) {
        concatParameters = `${this.assessmentCategory},${this.actionType}`;
      } else {
        const psychoAffective = this.psychoAffectDefaultValue.trim();
        if (psychoAffective === '') {
          this.showAlert('Failed! You need to provide value', { type: 'warning' });
          return;
        }

        const intPsychoAffective = Number.parseInt(psychoAffective, 10);
        if (Number.isNaN(intPsychoAffective) || intPsychoAffective > 6) {
          this.showAlert('Wrong range! The highest value is 6', { type: 'warning' });
          return;
        }

        concatParameters =
          `${this.assessmentCategory},${this.actionType},${psychoAffective}`;
      }
    }

    this.postConcatResult('/Registry/new-assessment-handler/general', concatParameters);
  }

  private buildClassConcatParameters(): string | null {
    return this.assessmentCategory === '1'
      ? this.buildPsychomotorClassConcatParameters()
      : this.buildVariousSubjectsClassConcatParameters();
  }

  private buildArmConcatParameters(): string | null {
    return this.assessmentCategory === '1'
      ? this.buildPsychomotorArmConcatParameters()
      : this.buildVariousSubjectsArmConcatParameters();
  }

  /**
   * Various Subjects (category "0") class concat:
   * ClassEnum,ClassLabel,Session,assessmentCategory,actionType
   */
  private buildVariousSubjectsClassConcatParameters(): string {
    return `${this.classValue},${this.sessionId},${this.assessmentCategory},${this.actionType}`;
  }

  /**
   * Psychomotor/Affective (category "1") class concat — legacy #ClassName change.
   * Create: ClassEnum,ClassLabel,Session,1,actionType,psychoDefault
   * Delete: ClassEnum,ClassLabel,Session,1,actionType (never send leftover psycho default)
   */
  private buildPsychomotorClassConcatParameters(): string | null {
    // Legacy Delete hides #psychoAffectDefaultValueDiv and omits that segment entirely.
    if (this.actionType === '1' || !this.showPsychoAffectField) {
      return `${this.classValue},${this.sessionId},${this.assessmentCategory},${this.actionType}`;
    }

    const psychoAffective = this.readValidatedPsychoDefault();
    if (psychoAffective == null) {
      return null;
    }

    return `${this.classValue},${this.sessionId},${this.assessmentCategory},${this.actionType},${psychoAffective}`;
  }

  /**
   * Various Subjects (category "0") arm concat:
   * Arm,ClassEnum,ClassLabel,Session,assessmentCategory,actionType
   */
  private buildVariousSubjectsArmConcatParameters(): string {
    return `${this.armValue},${this.classValue},${this.sessionId},${this.assessmentCategory},${this.actionType}`;
  }

  /**
   * Psychomotor/Affective (category "1") arm concat — legacy #ArmName change.
   * Create: Arm,ClassEnum,ClassLabel,Session,1,actionType,psychoDefault
   * Delete: Arm,ClassEnum,ClassLabel,Session,1,actionType (never send leftover psycho default)
   */
  private buildPsychomotorArmConcatParameters(): string | null {
    // Legacy Delete hides #psychoAffectDefaultValueDiv and omits that segment entirely.
    if (this.actionType === '1' || !this.showPsychoAffectField) {
      return `${this.armValue},${this.classValue},${this.sessionId},${this.assessmentCategory},${this.actionType}`;
    }

    const psychoAffective = this.readValidatedPsychoDefault();
    if (psychoAffective == null) {
      return null;
    }

    return `${this.armValue},${this.classValue},${this.sessionId},${this.assessmentCategory},${this.actionType},${psychoAffective}`;
  }

  /** Legacy psychoAffectDefaultValue validation (required, integer ≤ 6). */
  private readValidatedPsychoDefault(): string | null {
    const psychoAffective = this.psychoAffectDefaultValue.trim();
    if (psychoAffective === '') {
      this.showAlert('Failed! You need to provide value', { type: 'warning' });
      return null;
    }

    const intPsychoAffective = Number.parseInt(psychoAffective, 10);
    if (Number.isNaN(intPsychoAffective) || intPsychoAffective > 6) {
      this.showAlert('Wrong range! The highest value is 6', { type: 'warning' });
      return null;
    }

    return psychoAffective;
  }

  private postConcatResult(url: string, concatParameters: string, defaultSuccessIfEmpty = false): void {
    this.withRequestState(
      this.api.getAnObj(url, {
        params: new HttpParams().set('concatParameters', concatParameters),
      }),
      {
        useLoading: true,
        clearError: true,
        errorMessage: 'Failed to process assessment.',
        notifyOnError: 'none',
        rethrow: true,
      }
    ).subscribe({
      next: response => {
        // API returns PrepareEnvironmentForStudentAssessmentReturnDto (Message).
        let message = readResponseMessage(response);
        // Legacy Arm JS: only when the response body itself is null/undefined.
        if (defaultSuccessIfEmpty && (response == null || response === undefined) && !message) {
          message = 'Success';
        }

        this.statusMessage.set(message);
        this.statusSuccess.set(isAssessmentSuccessMessage(message));
        this.handleTermChangeAlert(message);
        this.cdr.markForCheck();
      },
      error: error => {
        const message = this.getHttpErrorMessage(error, 'Failed to process assessment.');
        this.statusMessage.set(message);
        this.statusSuccess.set(false);
        this.cdr.markForCheck();
      },
    });
  }

  private loadDefaultedTeachersReport() {
    return this.api.get('/Registry/new-assessment-handler/defaulted-teachers').pipe(
      catchError(error => {
        if (error?.status === 404) {
          return this.api.create('/SessionAndTerm/prepare-intitial-assessment?requestStatus=1', {});
        }

        return throwError(() => error);
      })
    );
  }

  private handleTermChangeAlert(message: string): void {
    // Legacy: data.split('\''); compare quoted term to $("#Term option:selected").html()
    const parts = message.split("'");
    if (parts.length <= 1) {
      return;
    }

    const newTerm = parts[1]?.split(' ')[0]?.trim();
    const displayedTerm = this.terms().find(option => option.value === this.termStatus)?.text?.trim();
    if (!newTerm || !displayedTerm || displayedTerm === '-1' || displayedTerm !== newTerm) {
      window.alert(message);
      window.location.reload();
    }
  }

  private clearPageResult(): void {
    this.statusMessage.set('');
    this.statusSuccess.set(false);
  }

  private unwrapResponseBody(response: unknown): unknown {
    if (Array.isArray(response)) {
      return response;
    }

    const record = response && typeof response === 'object'
      ? response as Record<string, unknown>
      : null;

    if (!record) {
      return response;
    }

    if ('data' in record && record['data'] != null && typeof record['data'] === 'object') {
      return record['data'];
    }

    return response;
  }
}
