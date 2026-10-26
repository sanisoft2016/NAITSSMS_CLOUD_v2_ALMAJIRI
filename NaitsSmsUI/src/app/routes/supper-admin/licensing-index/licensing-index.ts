import { CommonModule } from '@angular/common';
import { HttpParams } from '@angular/common/http';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs/operators';

import { BaseComponent, GenericService } from '@shared';

type LicensingIndexPage = {
  id?: string;
  schoolName?: string;
  schoolAddress?: string;
  schoolTelephone?: string;
  smsBalance?: number;
  expiringDate?: string | null;
  featureImplementationType?: number | null;
  featureImplementationTypeWarning?: string | null;
  totalStudents?: string;
};

type LastSubscriptionInfo = {
  hasSubscription?: boolean;
  subscriptionId?: string;
  registrationKey?: string;
  expiryDate?: string;
};

type SubscriptionHistoryItem = {
  subscriptionId?: number;
  paymentDate?: string;
  expiryDate?: string;
  charges?: number;
  paymentDateLong?: string;
  expiryDateLong?: string;
};

type CreateSubscriptionKeyResult = {
  success?: boolean;
  htmlMessage?: string;
  message?: string;
};

type UpdateNumberOfStudentsResult = {
  success?: boolean;
  failedSentinel?: boolean;
  message?: string;
};

type FeatureOption = { value: number; label: string };

@Component({
  selector: 'app-licensing-index',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
  ],
  templateUrl: './licensing-index.html',
  styleUrl: './licensing-index.scss',
})
export class LicensingIndex extends BaseComponent implements OnInit {
  private readonly api = inject(GenericService<any>);
  private readonly activatedRoute = inject(ActivatedRoute);

  schoolId = '';
  page: LicensingIndexPage | null = null;
  history: SubscriptionHistoryItem[] = [];
  showHistory = false;

  modalOpen = false;
  modalMode: 'none' | 'last' | 'new' | 'students' = 'none';
  lastInfo: LastSubscriptionInfo | null = null;

  // New subscription form (legacy CreateSubscriptionKey)
  expiryDate: Date | null = null;
  subscriptionId = '';
  charges = '';
  createResultHtml = '';
  readonly minExpiryDate = new Date();

  // Update students form
  noOfStudents = '';
  studentsResultMessage = '';

  featureOptions: FeatureOption[] = [];
  selectedFeatureType: number | null = null;

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.schoolId = String(this.activatedRoute.snapshot.paramMap.get('id') ?? '').trim();
    if (!this.schoolId) {
      this.showAlert('School id is required for licensing.', { type: 'warning' });
      this.router.navigate(['/supper-admin/school-list']);
      return;
    }

    this.loadPage();
  }

  goBack(): void {
    this.router.navigate(['/supper-admin/school-list']);
  }

  onFeatureTypeChange(): void {
    if (this.selectedFeatureType == null || !this.schoolId) return;

    this.withRequestState(
      this.api.getAnObj<boolean>('/SuperAdmin/update-feature-implementation-type', {
        params: new HttpParams()
          .set('implemetationOption', String(this.selectedFeatureType))
          .set('schoolId', this.schoolId),
      }),
      {
        errorMessage: 'Failed! Unable to complete the process.',
        notifyOnError: 'alert',
        rethrow: false,
      },
    ).subscribe(ok => {
      if (ok === true) {
        this.showAlert('The status has successfully been updated.', { type: 'success' });
      } else {
        this.showAlert('Failed! Unable to complete the process.', { type: 'warning' });
      }
    });
  }

  loadSubscriptionHistory(): void {
    this.withRequestState(
      this.api.get<SubscriptionHistoryItem[]>(
        `/SuperAdmin/get-subscription-history/${encodeURIComponent(this.schoolId)}`,
      ),
      {
        errorMessage: 'Failed to load subscription history.',
        notifyOnError: 'alert',
        rethrow: false,
      },
    ).subscribe(rows => {
      this.history = rows ?? [];
      this.showHistory = true;
    });
  }

  openLastSubscriptionInfo(): void {
    this.withRequestState(
      this.api.getAnObj<LastSubscriptionInfo>(
        `/SuperAdmin/get-last-subscription-info/${encodeURIComponent(this.schoolId)}`,
      ),
      {
        errorMessage: 'Failed to load last subscription info.',
        notifyOnError: 'alert',
        rethrow: false,
      },
    ).subscribe(info => {
      this.lastInfo = info ?? { hasSubscription: false };
      this.modalMode = 'last';
      this.modalOpen = true;
    });
  }

  openNewSubscription(): void {
    this.expiryDate = new Date();
    this.subscriptionId = '';
    this.charges = '';
    this.createResultHtml = '';
    this.modalMode = 'new';
    this.modalOpen = true;
  }

  openUpdateStudents(): void {
    this.noOfStudents = '';
    this.studentsResultMessage = '';
    this.modalMode = 'students';
    this.modalOpen = true;
  }

  closeModal(): void {
    this.modalOpen = false;
    this.modalMode = 'none';
  }

  submitSubscriptionKey(): void {
    const chargesRaw = String(this.charges ?? '').replace(/,/g, '').trim();
    const subscriptionId = String(this.subscriptionId ?? '').trim();

    if (!chargesRaw || chargesRaw === '0') {
      this.showAlert('Payment amount must be entered to continue.', { type: 'warning' });
      return;
    }
    if (!subscriptionId || subscriptionId === '0') {
      this.showAlert('SubscriptionId must be entered to continue.', { type: 'warning' });
      return;
    }
    if (!/^\d+$/.test(subscriptionId) || (subscriptionId.length !== 7 && subscriptionId.length !== 9)) {
      this.showAlert('Failed! The subscription Id is out of range.', { type: 'warning' });
      return;
    }

    const confirmMsg =
      subscriptionId.length === 7
        ? "This is 4 Months Subscription Id. Are you sure you want to continue? if 'Yes', click 'Ok' to continue."
        : "This is 1 Year Subscription Id. Are you sure you want to continue? if 'Yes', click 'Ok' to continue.";
    if (!window.confirm(confirmMsg)) {
      return;
    }

    if (!this.startSubmitting()) return;

    const payload = {
      schoolSmsId: this.schoolId,
      subscriptionId: Number(subscriptionId),
      charges: Number(chargesRaw),
      expiryDate: (this.expiryDate ?? new Date()).toISOString(),
      id: 0,
    };

    this.withRequestState(
      this.api.create<CreateSubscriptionKeyResult>('/SuperAdmin/create-subscription-key', payload),
      {
        errorMessage: 'Failed! Unable to Complete the process.',
        notifyOnError: 'alert',
        rethrow: false,
        useLoading: false,
        onError: error => {
          const body =
            error && typeof error === 'object' && 'error' in error
              ? (error as { error?: { message?: string; Message?: string } }).error
              : undefined;
          const apiMessage = body?.message || body?.Message;
          if (apiMessage) {
            this.showAlert(String(apiMessage), { type: 'warning' });
          }
        },
      },
    )
      .pipe(finalize(() => this.stopSubmitting()))
      .subscribe({
        next: result => {
          if (!result?.success) {
            this.showAlert(result?.message || 'Failed! Unable to Complete the process.', { type: 'warning' });
            return;
          }
          this.createResultHtml = result.message || result.htmlMessage || '';
          this.showAlert(result.message || 'Subscription key created successfully.', { type: 'success' });
          this.loadPage();
        },
      });
  }

  submitNumberOfStudents(): void {
    const raw = String(this.noOfStudents ?? '').trim();
    const no = Number(raw);
    if (!raw || !Number.isFinite(no) || no <= 0) {
      this.showAlert('No. of Students must be entered to continue.', { type: 'warning' });
      return;
    }

    if (!this.startSubmitting()) return;

    this.withRequestState(
      this.api.create<UpdateNumberOfStudentsResult>('/SuperAdmin/update-number-of-students', {
        schoolId: this.schoolId,
        noOfStudents: no,
      }),
      {
        errorMessage: 'Failed! Unable to Complete the process.',
        notifyOnError: 'none',
        rethrow: false,
        useLoading: false,
      },
    )
      .pipe(finalize(() => this.stopSubmitting()))
      .subscribe(result => {
        if (!result?.success || result.failedSentinel) {
          this.showAlert(result?.message || 'Failed! Unable to Complete the process.', { type: 'warning' });
          return;
        }
        this.studentsResultMessage = result.message || 'Has successfully been updated';
        this.loadPage();
      });
  }

  private loadPage(): void {
    this.withRequestState(
      this.api.getAnObj<LicensingIndexPage>(
        `/SuperAdmin/get-licensing-index-page/${encodeURIComponent(this.schoolId)}`,
      ),
      {
        errorMessage: 'Failed to load licensing page.',
        notifyOnError: 'alert',
        rethrow: false,
      },
    ).subscribe(page => {
      if (!page?.id) {
        this.showAlert('School was not found for licensing.', { type: 'warning' });
        this.router.navigate(['/supper-admin/school-list']);
        return;
      }

      this.page = page;
      this.selectedFeatureType =
        page.featureImplementationType == null ? null : Number(page.featureImplementationType);
      this.featureOptions = this.buildFeatureOptions(this.selectedFeatureType);

      if (page.featureImplementationTypeWarning) {
        this.showAlert(page.featureImplementationTypeWarning, { type: 'warning' });
      }
    });
  }

  private buildFeatureOptions(selected: number | null): FeatureOption[] {
    const all: FeatureOption[] = [
      { value: 0, label: 'Result processing alone' },
      { value: 1, label: 'Accounting management alone' },
      { value: 2, label: 'Both result-processing and accounting-management' },
    ];
    if (selected == null) return all;
    const first = all.find(x => x.value === selected);
    if (!first) return all;
    return [first, ...all.filter(x => x.value !== selected)];
  }
}
