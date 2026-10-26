import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Observable } from 'rxjs';

import { BaseComponent, GenericService } from '@shared';

type ActionState = 'ready' | 'limited' | 'pending' | 'destructive';
type ActionAccent = 'success' | 'info' | 'warning' | 'danger';

type ActionId =
  | 'normal-promotion'
  | 'next-term'
  | 'session-term'
  | 'pass-mark'
  | 'attendance'
  | 'automatic-promotion'
  | 'subject-selection'
  | 'cas'
  | 'subscription-category'
  | 'head'
  | 'subscription-renewal'
  | 'system-migration'
  | 'deadline'
  | 'last-term'
  | 'assessment-status';

type QuickAction = {
  id: ActionId;
  title: string;
  description: string;
  icon: string;
  cta: string;
  state: ActionState;
  accent: ActionAccent;
  note?: string;
};

type QuickActionGroup = {
  title: string;
  intro: string;
  items: QuickAction[];
};

type CaOption = {
  text: string;
  value: string;
};

type ElectiveAccessOption = CaOption;

@Component({
  selector: 'app-other-registrations',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './other-registrations.html',
  styleUrl: './other-registrations.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OtherRegistrations extends BaseComponent {
  private readonly api = inject(GenericService<any>);

  showResumptionModal = false;
  resumptionDateModel: Date | null = null;
  resumptionSaving = false;
  showCaModal = false;
  caOptions: CaOption[] = [];
  caSelectedCategory = '';
  caPerValue = 0;
  caSaving = false;
  showElectiveModal = false;
  electiveOptions: ElectiveAccessOption[] = [];
  electiveSelectedStatus = '';
  electiveSaving = false;

  constructor() {
    super();
  }

  readonly actionGroups: QuickActionGroup[] = [
    {
      title: 'Academic Controls',
      intro: 'Core term setup and promotion tools that map to the modern Angular and API flow.',
      items: [
        {
          id: 'normal-promotion',
          title: 'Normal Promotion',
          description: 'Run students\' promotion for the configured transition path.',
          icon: 'school',
          cta: 'Run Promotion',
          state: 'ready',
          accent: 'success',
        },
        {
          id: 'next-term',
          title: 'Next Term',
          description: 'Set the next term resumption date.',
          icon: 'event_available',
          cta: 'Set Date',
          state: 'ready',
          accent: 'info',
        },
        {
          id: 'session-term',
          title: 'Session / Term',
          description: 'Manage academic sessions and launch the academic-term preparation flow.',
          icon: 'list_alt',
          cta: 'View Sessions',
          state: 'ready',
          accent: 'danger',
        },
        {
          id: 'pass-mark',
          title: 'Pass Mark',
          description: 'Read and update the current assessment pass mark.',
          icon: 'rule',
          cta: 'Set Pass Mark',
          state: 'ready',
          accent: 'warning',
        },
        {
          id: 'attendance',
          title: 'Attendance',
          description: 'Read and update the total number of attendance days for the current term.',
          icon: 'checklist',
          cta: 'Set Attendance',
          state: 'ready',
          accent: 'info',
        },
        {
          id: 'automatic-promotion',
          title: 'Automatic Promotion',
          description: 'Run the broader school-wide automatic promotion process.',
          icon: 'bolt',
          cta: 'Run Automatic Promotion',
          state: 'ready',
          accent: 'success',
        },
      ],
    },
    {
      title: 'Assessment & School Tools',
      intro: 'Legacy quick links reproduced for the modern shell, with live actions where APIs already exist.',
      items: [
        {
          id: 'subject-selection',
          title: 'Subject Selection',
          description: 'Enable / disable elective-subjects selection for students.',
          icon: 'library_add_check',
          cta: 'Enable / Disable',
          state: 'ready',
          accent: 'warning',
        },
        {
          id: 'cas',
          title: 'C A\'s',
          description: 'Set number of CAs and maximum value per CA.',
          icon: 'tune',
          cta: 'Open CA Settings',
          state: 'ready',
          accent: 'danger',
        },
        {
          id: 'subscription-category',
          title: 'Subscription Category',
          description: 'Update subscription category.',
          icon: 'category',
          cta: 'View Status',
          state: 'ready',
          accent: 'info',
        },
        {
          id: 'head',
          title: 'Head',
          description: 'Review and manage school head records using the modern admin page.',
          icon: 'badge',
          cta: 'View Heads',
          state: 'ready',
          accent: 'success',
        },
        {
          id: 'subscription-renewal',
          title: 'Subscription Renewal',
          description: 'Subscription renewal.',
          icon: 'autorenew',
          cta: 'View Status',
          state: 'ready',
          accent: 'info',
        },
        {
          id: 'system-migration',
          title: 'System Migration',
          description: 'New System Migration',
          icon: 'compare_arrows',
          cta: 'Run Migration',
          state: 'ready',
          accent: 'info',
        },
        {
          id: 'deadline',
          title: 'Deadline',
          description: 'Inspect and update the assessment deadline.',
          icon: 'timer',
          cta: 'Set Deadline',
          state: 'ready',
          accent: 'info',
        },
        {
          id: 'last-term',
          title: 'Last Term',
          description: 'Revert back to the last term.',
          icon: 'history',
          cta: 'View Status',
          state: 'destructive',
          accent: 'danger',
          note: 'This deletes current-term result records before rolling back to the last term.',
        },
        {
          id: 'assessment-status',
          title: 'Assessment Status',
          description: 'Toggle current-term assessment completion.',
          icon: 'toggle_on',
          cta: 'Review Status',
          state: 'ready',
          accent: 'warning',
        },
      ],
    },
  ];

  runAction(actionId: ActionId): void {
    switch (actionId) {
      case 'normal-promotion':
        this.runNormalPromotion();
        return;
      case 'next-term':
        this.manageResumptionDate();
        return;
      case 'session-term':
        this.reviewAcademicSessions();
        return;
      case 'pass-mark':
        this.managePassMark();
        return;
      case 'attendance':
        this.manageAttendance();
        return;
      case 'automatic-promotion':
        this.runAutomaticPromotion();
        return;
      case 'subject-selection':
        this.manageElectiveAccess();
        return;
      case 'cas':
        this.reviewCaSettings();
        return;
      case 'head':
        this.reviewSchoolHeads();
        return;
      case 'deadline':
        this.manageAssessmentDeadline();
        return;
      case 'assessment-status':
        this.reviewAssessmentStatus();
        return;
      case 'subscription-category':
        this.checkSubscriptionCategory();
        return;
      case 'subscription-renewal':
        this.renewSubscription();
        return;
      case 'system-migration':
        this.runSystemMigration();
        return;
      case 'last-term':
        this.revertToLastTerm();
        return;
      default:
        this.showPendingAction('Other Registrations', 'This action is not available yet.');
    }
  }

  actionStateLabel(state: ActionState): string {
    switch (state) {
      case 'ready':
        return 'Ready';
      case 'limited':
        return 'Partial';
      case 'destructive':
        return 'Restricted';
      default:
        return 'Pending';
    }
  }

  private runNormalPromotion(): void {
    if (!window.confirm('Run normal promotion now?')) {
      return;
    }

    this.executeRequest(
      this.api.get('/Registry/promotion/validate'),
      undefined,
      'Failed to validate normal promotion.',
      response => {
        const status = (response as any)?.status ?? (response as any)?.message;
        if (status !== 'Success') {
          this.showAlert(`Failed! ${status || 'Promotion validation failed.'}`, { type: 'warning' });
          return;
        }

        if (!window.confirm('Validation passed. Proceed?')) {
          return;
        }

        this.executeRequest(
          this.api.create('/Registry/promotion/confirm', {}),
          undefined,
          'Failed to run normal promotion.',
          result => {
            const resultStatus = (result as any)?.status ?? (result as any)?.message;
            this.showAlert(resultStatus === 'Succeeded' ? 'Promotion completed.' : String(resultStatus || 'Promotion failed.'), {
              type: resultStatus === 'Succeeded' ? 'success' : 'warning',
            });
          }
        );
      }
    );
  }

  private runAutomaticPromotion(): void {
    if (!window.confirm('Run the automatic school-wide promotion now?')) {
      return;
    }

    this.executeRequest(
      this.api.get('/Registry/promotion/validate'),
      undefined,
      'Failed to validate automatic promotion.',
      response => {
        const status = (response as any)?.status ?? (response as any)?.message;
        if (status !== 'Success') {
          this.showAlert(`Failed! ${status || 'Promotion validation failed.'}`, { type: 'warning' });
          return;
        }

        if (!window.confirm('Validation passed. Proceed?')) {
          return;
        }

        this.executeRequest(
          this.api.create('/Registry/promotion/automatic/confirm', {}),
          undefined,
          'Failed to run automatic promotion.',
          result => {
            const resultStatus = (result as any)?.status ?? (result as any)?.message;
            this.showAlert(resultStatus === 'Succeeded' ? 'Automatic promotion completed.' : String(resultStatus || 'Automatic promotion failed.'), {
              type: resultStatus === 'Succeeded' ? 'success' : 'warning',
            });
          }
        );
      }
    );
  }

  private manageResumptionDate(): void {
    this.executeRequest(
      this.api.get('/Registry/school/resumption-date'),
      undefined,
      'Failed to load the current resumption date.',
      response => {
        const normalizedDate = this.toDateOnlyString((response as any)?.resumptionDate);
        this.resumptionDateModel = normalizedDate ? this.parseDateOnly(normalizedDate) : null;
        this.showResumptionModal = true;
      }
    );
  }

  closeResumptionModal(): void {
    if (this.resumptionSaving) {
      return;
    }

    this.showResumptionModal = false;
  }

  saveResumptionDate(): void {
    if (this.resumptionSaving) {
      return;
    }

    const normalizedDate = this.resumptionDateModel ? this.toDateOnlyString(this.resumptionDateModel) : '';
    const parsed = this.parseDateOnly(normalizedDate);
    if (!parsed) {
      this.showAlert('Enter a valid date in YYYY-MM-DD format.', { type: 'warning' });
      return;
    }

    this.resumptionSaving = true;
    this.withRequestState(
      this.api.create('/Registry/school/resumption-date', this.toUtcNoonDate(normalizedDate)),
      {
        useLoading: true,
        clearError: true,
        errorMessage: 'Failed to update the resumption date.',
        notifyOnError: 'none',
        rethrow: true,
        onError: error => console.error('Resumption date save failed:', error),
      }
    ).subscribe({
      next: response => {
        this.showAlert(String((response as any)?.message || 'Resumption date updated successfully.'), { type: 'success' });
        this.showResumptionModal = false;
        this.resumptionSaving = false;
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to update the resumption date.'), { type: 'error' });
        this.resumptionSaving = false;
      },
    });
  }

  private reviewAcademicSessions(): void {
    this.router.navigate(['/admin/session-term-initialisation']);
  }

  private managePassMark(): void {
    this.executeRequest(
      this.api.get('/Registry/assessment/pass-mark'),
      undefined,
      'Failed to load the current pass mark.',
      response => {
        const currentValue = (response as any)?.passMark;
        const enteredValue = window.prompt('Enter pass mark (0-100).', currentValue != null ? String(currentValue) : '');

        if (enteredValue === null) {
          return;
        }

        const parsed = Number(enteredValue);
        if (!Number.isInteger(parsed) || parsed < 0 || parsed > 100) {
          this.showAlert('Pass mark must be a whole number between 0 and 100.', { type: 'warning' });
          return;
        }

        this.executeRequest(
          this.api.create('/Registry/assessment/pass-mark', parsed),
          'Pass mark updated successfully.',
          'Failed to update the pass mark.'
        );
      }
    );
  }

  private manageAttendance(): void {
    this.executeRequest(
      this.api.get('/Registry/attendance/total'),
      undefined,
      'Failed to load total attendance.',
      response => {
        const currentValue = (response as any)?.totalAttendance;
        const enteredValue = window.prompt('Enter total attendance days for the current term (1-255).', currentValue != null ? String(currentValue) : '');

        if (enteredValue === null) {
          return;
        }

        const parsed = Number(enteredValue);
        if (!Number.isInteger(parsed) || parsed < 1 || parsed > 255) {
          this.showAlert('Attendance must be a whole number between 1 and 255.', { type: 'warning' });
          return;
        }

        this.executeRequest(
          this.api.create('/Registry/attendance/total', parsed),
          'Total attendance updated successfully.',
          'Failed to update total attendance.'
        );
      }
    );
  }

  private manageElectiveAccess(): void {
    this.executeRequest(
      this.api.get('/Registry/electives/access-status'),
      undefined,
      'Failed to load elective access status.',
      response => {
        const options = this.asArray(response)
          .map(item => {
            const option = item as Record<string, unknown>;
            return {
              text: String(option?.text ?? ''),
              value: String(option?.value ?? ''),
            } as ElectiveAccessOption;
          })
          .filter(option => option.value !== '');

        if (options.length === 0) {
          this.showAlert('The elective access status endpoint returned no options.', { type: 'warning' });
          return;
        }

        this.electiveOptions = options;
        this.electiveSelectedStatus = options[0].value;
        this.showElectiveModal = true;
      }
    );
  }

  closeElectiveModal(): void {
    if (this.electiveSaving) {
      return;
    }

    this.showElectiveModal = false;
  }

  saveElectiveAccessStatus(): void {
    if (this.electiveSaving) {
      return;
    }

    const parsedSelectionId = Number(this.electiveSelectedStatus);
    if (!Number.isInteger(parsedSelectionId) || parsedSelectionId < 0) {
      this.showAlert('Please select a valid elective access status.', { type: 'warning' });
      return;
    }

    this.electiveSaving = true;
    this.withRequestState(
      this.api.create('/Registry/electives/access-status', parsedSelectionId),
      {
        useLoading: true,
        clearError: true,
        errorMessage: 'Failed to update elective access status.',
        notifyOnError: 'none',
        rethrow: true,
        onError: error => console.error('Elective access status save failed:', error),
      }
    ).subscribe({
      next: () => {
        const selectedLabel = this.electiveOptions.find(option => option.value === this.electiveSelectedStatus)?.text
          ?? 'updated';
        this.showAlert(`Elective subject selection has been set to ${selectedLabel}.`, { type: 'success' });
        this.showElectiveModal = false;
        this.electiveSaving = false;
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to update elective access status.'), { type: 'error' });
        this.electiveSaving = false;
      },
    });
  }

  private reviewCaSettings(): void {
    this.executeRequest(
      this.api.get('/Registry/assessment/ca-settings'),
      undefined,
      'Failed to load CA settings.',
      response => {
        const payload = (response as any) ?? {};
        const settings = this.asArray(payload?.numberOfCasCategoyDropdown)
          .map(item => {
            const option = item as Record<string, unknown>;
            return {
              text: String(option?.text ?? ''),
              value: String(option?.value ?? ''),
            } as CaOption;
          })
          .filter(option => option.value !== '');

        if (settings.length === 0) {
          this.showAlert('Sorry! Other registrations first need to be done before this place.', { type: 'warning' });
          return;
        }

        this.caOptions = settings;
        this.caSelectedCategory = settings[0].value;
        this.caPerValue = Number(payload?.perCasValue ?? 0);
        this.showCaModal = true;
      }
    );
  }

  closeCaModal(): void {
    if (this.caSaving) {
      return;
    }

    this.showCaModal = false;
  }

  saveCaSettings(): void {
    if (this.caSaving) {
      return;
    }

    const parsedCategory = Number(this.caSelectedCategory);
    if (!Number.isInteger(parsedCategory) || parsedCategory < 0) {
      this.showAlert('Please select a valid CA category.', { type: 'warning' });
      return;
    }

    if (!Number.isInteger(this.caPerValue) || this.caPerValue < 1 || this.caPerValue > 255) {
      this.showAlert('Per CA value must be a whole number between 1 and 255.', { type: 'warning' });
      return;
    }

    this.caSaving = true;
    this.withRequestState(
      this.api.create('/Registry/assessment/ca-settings', {
        casCategory: parsedCategory,
        perCasValue: this.caPerValue,
      }),
      {
        useLoading: true,
        clearError: true,
        errorMessage: 'Failed to save CA settings.',
        notifyOnError: 'none',
        rethrow: true,
        onError: error => console.error('CA settings save failed:', error),
      }
    ).subscribe({
      next: response => {
        const message = String((response as any)?.message || 'Your changes has successfully been saved.');
        this.showAlert(message, { type: 'success' });
        this.showCaModal = false;
        this.caSaving = false;
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to save CA settings.'), { type: 'error' });
        this.caSaving = false;
      },
    });
  }

  private reviewSchoolHeads(): void {
    this.router.navigate(['/admin/principal-index']);
  }

  private manageAssessmentDeadline(): void {
    this.executeRequest(
      this.api.get('/Registry/assessment/deadline'),
      undefined,
      'Failed to load the assessment deadline.',
      response => {
        const currentValue = this.toDateOnlyString((response as any)?.deadline);
        const enteredValue = window.prompt('Enter assessment deadline (YYYY-MM-DD).', currentValue);

        if (enteredValue === null) {
          return;
        }

        const normalizedDeadline = this.toDateOnlyString(enteredValue);
        const parsed = this.parseDateOnly(normalizedDeadline);
        if (!parsed) {
          this.showAlert('Enter a valid date in YYYY-MM-DD format.', { type: 'warning' });
          return;
        }

        this.executeRequest(
          this.api.create('/Registry/assessment/deadline/set', this.toUtcNoonDate(normalizedDeadline)),
          'Assessment deadline updated successfully.',
          'Failed to update the assessment deadline.'
        );
      }
    );
  }

  private reviewAssessmentStatus(): void {
    this.executeRequest(
      this.api.get('/Registry/assessment/completion-status/current-term'),
      undefined,
      'Failed to retrieve assessment status.',
      response => {
        const status = Number((response as any)?.status ?? 0);
        const statusLabel = status === 1 ? 'Completed' : 'Uncompleted';
        const nextStatus = status === 1 ? 0 : 1;
        const nextStatusLabel = nextStatus === 1 ? 'Completed' : 'Uncompleted';

        if (!window.confirm(`Current term assessment is ${statusLabel}. Mark it as ${nextStatusLabel}?`)) {
          return;
        }

        this.executeRequest(
          this.api.create('/Registry/assessment/completion-status/current-term/set', nextStatus),
          `Current term assessment has been marked as ${nextStatusLabel}.`,
          'Failed to update assessment completion status.'
        );
      }
    );
  }

  private revertToLastTerm(): void {
    if (!window.confirm('Are you sure you want to revert to the LAST academic term? This cannot be undone.')) {
      return;
    }

    if (!window.confirm('Final confirmation: this will delete all current-term result records and roll back to the last term. Proceed?')) {
      return;
    }

    this.executeRequest(
      this.api.get('/Registry/term/revert-to-last'),
      undefined,
      'Failed to revert to the last term.',
      response => {
        const status = (response as any)?.status;
        const message = (response as any)?.message;

        if (status === 'Success') {
          this.showAlert('System has reverted to the last term successfully.', { type: 'success' });
        } else if (status === 'FistTerm') {
          this.showAlert('Revert failed. First term cannot be reverted.', { type: 'warning' });
        } else {
          this.showAlert(message || 'Revert failed. You need to first clear all the students assessment scores you have entered for this term.', { type: 'error' });
        }
      }
    );
  }

  private checkSubscriptionCategory(): void {
    this.executeRequest(
      this.api.get('/Registry/subscription/category'),
      undefined,
      'Failed to check subscription category.',
      response => this.showAlert(String((response as any)?.message || 'Subscription category check completed.'), { type: 'info', duration: 7000 })
    );
  }

  private renewSubscription(): void {
    if (!window.confirm('Renew software subscription now?')) {
      return;
    }

    this.executeRequest(
      this.api.get('/Registry/subscription/renewal'),
      undefined,
      'Failed to renew subscription.',
      response => {
        const message = String((response as any)?.message || 'Subscription renewal completed.');
        this.showAlert(message, { type: message.toLowerCase().startsWith('failed') ? 'error' : 'info', duration: 7000 });
      }
    );
  }

  private runSystemMigration(): void {
    // Legacy OtherRegistration.NewSystemMigration: GET NewSystemMigration, alert(response).
    this.executeRequest(
      this.api.get('/Registry/system-migration'),
      undefined,
      'Failed to run system migration.',
      response => {
        const message = String((response as any)?.message || response || 'Failed! Unable to verify your credentials.');
        const failed = message.toLowerCase().startsWith('failed');
        this.showAlert(message, { type: failed ? 'error' : 'success', duration: 7000 });
      }
    );
  }

  private showPendingAction(title: string, details: string): void {
    this.showAlert(`${title}: ${details}`, { type: 'info', duration: 5000 });
  }

  private executeRequest<T>(
    request$: Observable<T>,
    successMessage?: string,
    fallbackErrorMessage = 'Unable to complete the request.',
    onSuccess?: (response: T) => void
  ): void {
    this.withRequestState(request$, {
      useLoading: true,
      clearError: true,
      errorMessage: fallbackErrorMessage,
      notifyOnError: 'none',
      rethrow: true,
      onError: error => console.error('Other registrations action failed:', error),
    }).subscribe({
      next: response => {
        onSuccess?.(response);
        if (successMessage) {
          this.showAlert(successMessage, { type: 'success' });
        }
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, fallbackErrorMessage), { type: 'error' });
      },
    });
  }

  private asArray(value: unknown): unknown[] {
    if (Array.isArray(value)) {
      return value;
    }

    if (value && typeof value === 'object') {
      const maybeValues = Object.values(value as Record<string, unknown>).find(Array.isArray);
      if (Array.isArray(maybeValues)) {
        return maybeValues;
      }
    }

    return [];
  }

  private toDateOnlyString(value: unknown): string {
    if (!value) {
      return '';
    }

    if (typeof value === 'string') {
      const isoMatch = /^(\d{4}-\d{2}-\d{2})/.exec(value.trim());
      if (isoMatch) {
        return isoMatch[1];
      }
    }

    const date = value instanceof Date ? value : new Date(String(value));
    if (Number.isNaN(date.getTime())) {
      return '';
    }

    const year = date.getFullYear();
    const month = `${date.getMonth() + 1}`.padStart(2, '0');
    const day = `${date.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private parseDateOnly(value: string): Date | null {
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value.trim());
    if (!match) {
      return null;
    }

    const date = new Date(+match[1], +match[2] - 1, +match[3]);
    return Number.isNaN(date.getTime()) ? null : date;
  }

  private toUtcNoonDate(dateOnly: string): Date {
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dateOnly.trim());
    if (!match) {
      return new Date(Number.NaN);
    }

    return new Date(Date.UTC(+match[1], +match[2] - 1, +match[3], 12, 0, 0));
  }
}