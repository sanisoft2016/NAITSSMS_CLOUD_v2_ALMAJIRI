import { ChangeDetectionStrategy, Component, Directive, ElementRef, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DOCUMENT } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpParams } from '@angular/common/http';

import { BaseComponent, GenericService } from '@shared';

type SmsStudentRow = {
  id: string;
  idNo: string;
  fullName: string;
  guardianName: string;
  telNo: string;
  classArm: string;
  selected: boolean;
};

type SmsCounter = {
  encoding: string;
  length: number;
  messages: number;
  perMessage: number;
  remaining: number;
};

type SmsSendResponse = {
  executionStatus?: string;
  costPerOrdinaryNo?: number;
  costPerMtnDndNo?: number;
  telNos?: { telNo?: string; telNoStatus?: number }[];
};

type SystemInfo = {
  userId?: string;
  schoolName?: string;
  schoolAddress?: string;
  telephone?: string;
  emailAddress?: string;
  website?: string;
  motto?: string;
  smsApiToken?: string;
  smsApiKey?: string;
};

type SendResultRow = {
  classArm: string;
  admsnNo: string;
  fullName: string;
  guardianName: string;
  telNo: string;
  status: string;
  chargeHtml: string;
};

type SendResultSummary = {
  noOfSms: number;
  costPerOrdinaryNo: number;
  costPerMtnDndNo: number;
  overallTotalCharge: string;
  noOfMtnDndNo: number;
  mtnDndTotalCharge: string;
  noOfOrdinaryNo: number;
  ordinaryTotalCharge: string;
  noOfInvalidNo: number;
  rows: SendResultRow[];
};

type ModalMode = 'none' | 'systemInfo' | 'singleSms' | 'sendResult';

@Directive({
  selector: '[smsModalLayer]',
  standalone: true,
})
class SmsModalLayer implements OnInit, OnDestroy {
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly document = inject(DOCUMENT);

  ngOnInit(): void {
    this.document.body.appendChild(this.host.nativeElement);
  }

  ngOnDestroy(): void {
    this.host.nativeElement.remove();
  }
}

const SMS_API = {
  systemInfo: '/Registry/sms-system-info',
  postSystemInformation: '/Email/post-sms-system-information',
  updateCredentials: '/Registry/update-sms-credentials',
  balance: '/Registry/sms-balance',
  send: '/Registry/send-sms',
  sendExpress: '/Registry/send-sms-express',
  searchStudents: '/student/search-for-sms',
  allStudents: '/student/all-for-sms',
  updateGuardian: '/TeachersAndOthersService/update-students-guardian-for-sms',
} as const;

@Component({
  selector: 'app-sms-index',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, SmsModalLayer],
  templateUrl: './sms-index.html',
  styleUrl: './sms-index.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SmsIndex extends BaseComponent {
  private readonly api = inject(GenericService<unknown>);
  private readonly fb = inject(FormBuilder);

  searchParameter = '';

  readonly smsStatus = signal('');
  readonly balanceAmount = signal<string | null>(null);
  readonly pricePerSms = signal<string | null>(null);
  readonly students = signal<SmsStudentRow[]>([]);
  readonly systemInfo = signal<SystemInfo | null>(null);
  readonly modalMode = signal<ModalMode>('none');
  readonly singleSmsStudent = signal<SmsStudentRow | null>(null);
  readonly sendResult = signal<SendResultSummary | null>(null);
  readonly isSavingProfile = signal(false);
  readonly isSending = signal(false);
  readonly isSendingExpress = signal(false);
  readonly isSendingSingle = signal(false);
  readonly smsCounterTick = signal(0);
  readonly singleSmsCounterTick = signal(0);

  readonly smsForm = this.fb.nonNullable.group({
    senderName: ['', [Validators.required, Validators.maxLength(11)]],
    message: ['', [Validators.required, Validators.maxLength(480)]],
  });

  readonly singleSmsForm = this.fb.nonNullable.group({
    senderName: ['', [Validators.required, Validators.maxLength(11)]],
    recipientNo: ['', [Validators.required]],
    message: ['', [Validators.required, Validators.maxLength(480)]],
  });

  readonly allSelected = computed(() => {
    const rows = this.students();
    return rows.length > 0 && rows.every(row => row.selected);
  });

  readonly smsCounter = computed(() => {
    this.smsCounterTick();
    return this.computeSmsCounter(this.smsForm.controls.message.value);
  });

  readonly singleSmsCounter = computed(() => {
    this.singleSmsCounterTick();
    return this.computeSmsCounter(this.singleSmsForm.controls.message.value);
  });

  constructor() {
    super();
  }

  getSystemSmsInfo(): void {
    this.withRequestState(this.api.getAnObj<SystemInfo>(SMS_API.systemInfo), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Failed to load SMS system info.',
      notifyOnError: 'alert',
      rethrow: false,
    }).subscribe({
      next: response => {
        if (!response) {
          this.showAlert('No SMS system info was returned.', { type: 'warning' });
          return;
        }

        const info = this.toSystemInfo(response);
        this.systemInfo.set(info);
        if (info.schoolName && !this.smsForm.controls.senderName.value) {
          this.smsForm.patchValue({ senderName: info.schoolName.slice(0, 11) });
        }
        this.modalMode.set('systemInfo');
        this.cdr.markForCheck();
      },
    });
  }

  postSystemInformation(): void {
    // Legacy SmsIndex.PostSystemInformation → GET PostSystemInformation, alert(response).
    this.withRequestState(this.api.getAnObj<{ message?: string }>(SMS_API.postSystemInformation), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Failed to submit system information.',
    }).subscribe({
      next: response => {
        const message = this.readMessageResponse(response, 'Request completed.');
        const failed =
          message.toLowerCase().startsWith('failed') ||
          message.toLowerCase().startsWith('un-able') ||
          message.toLowerCase().includes('internet connection is required');
        this.showAlert(message, { type: failed ? 'error' : 'success', duration: 7000 });
      },
      error: error => this.showAlert(this.getApiErrorMessage(error, 'Failed to submit system information.'), { type: 'error' }),
    });
  }

  updateSmsCredentials(): void {
    this.withRequestState(this.api.getAnObj<{ message?: string }>(SMS_API.updateCredentials), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Failed to update SMS credentials.',
    }).subscribe({
      next: response => this.showAlert(this.readMessageResponse(response, 'Request completed.'), { type: 'info' }),
      error: error => this.showAlert(this.getApiErrorMessage(error, 'Failed to update SMS credentials.'), { type: 'error' }),
    });
  }

  getSmsBalance(): void {
    this.withRequestState(this.api.getAnObj<{ balance?: string }>(SMS_API.balance), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Failed to load SMS balance.',
    }).subscribe({
      next: response => this.applyBalanceResponse(this.readBalanceMessage(response)),
      error: error => this.showAlert(this.getApiErrorMessage(error, 'Failed to load SMS balance.'), { type: 'error' }),
    });
  }

  searchStudent(): void {
    const admissionNo = this.searchParameter.trim();
    if (!admissionNo) {
      this.showAlert('Search Parameter must be provided to continue.', { type: 'warning' });
      return;
    }

    if (!admissionNo.includes('/')) {
      this.showAlert("Wrong student's admission number.", { type: 'warning' });
      this.searchParameter = '';
      return;
    }

    const params = new HttpParams().set('admissionNo', admissionNo);
    this.withRequestState(this.api.getAnObj(SMS_API.searchStudents, { params }), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Failed to search student.',
    }).subscribe({
      next: response => {
        const rows = this.asArray(response).map(item => this.toSmsStudent(item));
        this.students.set(rows);
        if (rows.length === 1) {
          this.openSingleSmsModal(rows[0]);
        }
      },
      error: error => this.showAlert(this.getApiErrorMessage(error, 'Failed to search student.'), { type: 'error' }),
    });
  }

  loadAllStudents(): void {
    this.withRequestState(this.api.getAnObj(SMS_API.allStudents), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Failed to load students for SMS.',
    }).subscribe({
      next: response => {
        const rows = this.asArray(response)
          .map(item => this.toSmsStudent(item))
          .sort((left, right) => {
            const classCompare = left.classArm.localeCompare(right.classArm);
            return classCompare !== 0 ? classCompare : left.fullName.localeCompare(right.fullName);
          });
        this.students.set(rows);
      },
      error: error => this.showAlert(this.getApiErrorMessage(error, 'Failed to load students for SMS.'), { type: 'error' }),
    });
  }

  toggleStudent(studentId: string, checked: boolean): void {
    this.students.update(rows =>
      rows.map(row => (row.id === studentId ? { ...row, selected: checked } : row))
    );
  }

  toggleAll(checked: boolean): void {
    this.students.update(rows => rows.map(row => ({ ...row, selected: checked })));
  }

  updateStudentProfile(): void {
    const selectedRows = this.students().filter(
      row => row.selected && (row.guardianName.trim() !== '' || row.telNo.trim() !== '')
    );

    if (selectedRows.length === 0) {
      this.showAlert('Failed! one more option needs to be selected.', { type: 'warning' });
      return;
    }

    const payload = selectedRows.map(row => ({
      studentId: row.id,
      guardianName: row.guardianName.trim(),
      telNo: row.telNo.trim(),
    }));

    this.isSavingProfile.set(true);
    this.withRequestState(this.api.create(SMS_API.updateGuardian, payload), {
      useLoading: false,
      clearError: true,
      errorMessage: 'Failed to save guardian information.',
    }).subscribe({
      next: () => {
        this.isSavingProfile.set(false);
        this.showAlert('Changes has successfully been saved.', { type: 'success' });
      },
      error: error => {
        this.isSavingProfile.set(false);
        this.showAlert(this.getApiErrorMessage(error, 'Failed to save guardian information.'), { type: 'error' });
      },
    });
  }

  sendSms(): void {
    if (!this.validateBulkSmsForm()) {
      return;
    }

    const telNos = this.getSelectedTelNos();
    if (telNos.length === 0) {
      this.showAlert('Failed! one more option needs to be selected.', { type: 'warning' });
      return;
    }

    if (
      !window.confirm(
        "This is a normal route and it's not faster but does give the details of the Response Information. Are you sure you want to continue? If 'Yes', click 'Ok' to continue."
      )
    ) {
      return;
    }

    const values = this.smsForm.getRawValue();
    this.isSending.set(true);
    this.withRequestState(
      this.api.create<SmsSendResponse>(SMS_API.send, {
        senderName: values.senderName.trim(),
        message: values.message.trim(),
        recipientNo: telNos,
        noOfSms: this.smsCounter().messages || 1,
      }),
      {
        useLoading: false,
        clearError: true,
        errorMessage: 'Failed to send SMS.',
      }
    ).subscribe({
      next: response => this.handleBulkSendResponse(response, telNos),
      error: error => {
        this.isSending.set(false);
        this.showAlert(this.getApiErrorMessage(error, 'Failed to send SMS.'), { type: 'error' });
      },
    });
  }

  sendSmsExpress(): void {
    if (!this.validateBulkSmsForm()) {
      return;
    }

    const telNos = this.getSelectedTelNos();
    if (telNos.length === 0) {
      this.showAlert('Failed! one more option needs to be selected.', { type: 'warning' });
      return;
    }

    if (
      !window.confirm(
        "This is an express route and it is faster but does not give the details of the Response Information. Are you sure you want to continue? If 'Yes', click 'Ok' to continue."
      )
    ) {
      return;
    }

    const values = this.smsForm.getRawValue();
    this.isSendingExpress.set(true);
    this.withRequestState(
      this.api.create<{ message?: string }>(SMS_API.sendExpress, {
        senderName: values.senderName.trim(),
        message: values.message.trim(),
        recipientNo: telNos,
        noOfSms: this.smsCounter().messages || 1,
      }),
      {
        useLoading: false,
        clearError: true,
        errorMessage: 'Failed to send express SMS.',
      }
    ).subscribe({
      next: response => {
        this.isSendingExpress.set(false);
        this.showAlert(String(response?.message ?? 'Process completed.'), { type: 'info' });
        this.getSmsBalance();
      },
      error: error => {
        this.isSendingExpress.set(false);
        this.showAlert(this.getApiErrorMessage(error, 'Failed to send express SMS.'), { type: 'error' });
      },
    });
  }

  sendSmsToSinglePerson(): void {
    if (this.singleSmsForm.invalid) {
      this.singleSmsForm.markAllAsTouched();
      return;
    }

    const values = this.singleSmsForm.getRawValue();
    const senderName = values.senderName.trim();
    const message = values.message.trim();
    const recipientNo = values.recipientNo.trim();

    if (!senderName) {
      this.showAlert('Sender name needs to be provided.', { type: 'warning' });
      return;
    }

    if (senderName.length > 11) {
      this.showAlert('Sender name should not be more than 11 letters.', { type: 'warning' });
      return;
    }

    if (!message) {
      this.showAlert('Message needs to be provided.', { type: 'warning' });
      return;
    }

    if (!recipientNo) {
      this.showAlert('RecipientNo needs to be provided.', { type: 'warning' });
      return;
    }

    this.isSendingSingle.set(true);
    this.withRequestState(
      this.api.create<SmsSendResponse>(SMS_API.send, {
        senderName,
        message,
        recipientNo: [recipientNo],
        noOfSms: this.singleSmsCounter().messages || 1,
      }),
      {
        useLoading: false,
        clearError: true,
        errorMessage: 'Failed to send SMS.',
      }
    ).subscribe({
      next: response => {
        this.isSendingSingle.set(false);
        const telNos = response?.telNos ?? [];
        if (telNos.length > 0) {
          const noOfSms = this.singleSmsCounter().messages || 1;
          const costPerMtnDndNo = Number(response.costPerMtnDndNo ?? 0);
          const costPerOrdinaryNo = Number(response.costPerOrdinaryNo ?? 0);
          const totalSmsCostPerMtnDndNo = costPerMtnDndNo * noOfSms;
          const totalSmsCostPerOrdinaryNo = costPerOrdinaryNo * noOfSms;
          let smsCharge = 0;
          let sentStatus = 'Sent';

          if (telNos[0]?.telNoStatus === 0) {
            smsCharge = totalSmsCostPerOrdinaryNo;
          } else if (telNos[0]?.telNoStatus === 1) {
            smsCharge = totalSmsCostPerMtnDndNo;
            sentStatus = "Can't receive SMS";
          } else {
            smsCharge = 0;
            sentStatus = 'Failed';
          }

          this.showAlert(
            `Message ${sentStatus}, No. of SMS = ${noOfSms}, Total Cost =${smsCharge}`,
            { type: sentStatus === 'Sent' ? 'success' : 'warning' }
          );
          this.closeModal();
          this.getSmsBalance();
          return;
        }

        this.showAlert(String(response?.executionStatus ?? 'SMS request completed.'), { type: 'info' });
      },
      error: error => {
        this.isSendingSingle.set(false);
        this.showAlert(this.getApiErrorMessage(error, 'Failed to send SMS.'), { type: 'error' });
      },
    });
  }

  printPage(): void {
    const content = document.getElementById('pageContentMain');
    if (!content) {
      return;
    }

    this.openPrintWindow(content.outerHTML);
  }

  printResultPage(): void {
    const content = document.getElementById('pageContent2nd');
    if (!content) {
      return;
    }

    this.openPrintWindow(content.outerHTML);
  }

  closeModal(): void {
    if (this.modalMode() === 'sendResult') {
      if (
        !window.confirm(
          "Are you sure you want to close Result? If 'Yes',  click 'Ok' to continue."
        )
      ) {
        return;
      }
    }

    this.modalMode.set('none');
    this.systemInfo.set(null);
    this.singleSmsStudent.set(null);
    this.sendResult.set(null);
  }

  touchSmsCounter(): void {
    this.smsCounterTick.update(value => value + 1);
  }

  touchSingleSmsCounter(): void {
    this.singleSmsCounterTick.update(value => value + 1);
  }

  private openSingleSmsModal(student: SmsStudentRow): void {
    this.singleSmsStudent.set(student);
    this.singleSmsForm.reset({
      senderName: this.smsForm.controls.senderName.value,
      recipientNo: student.telNo,
      message: '',
    });
    this.modalMode.set('singleSms');
  }

  private validateBulkSmsForm(): boolean {
    const senderName = this.smsForm.controls.senderName.value.trim();
    const message = this.smsForm.controls.message.value.trim();

    if (!senderName) {
      this.showAlert('Sender name needs to be provided.', { type: 'warning' });
      return false;
    }

    if (senderName.length > 11) {
      this.showAlert('Sender name should not be more than 11 letters.', { type: 'warning' });
      return false;
    }

    if (!message) {
      this.showAlert('Message needs to be provided.', { type: 'warning' });
      return false;
    }

    return true;
  }

  private getSelectedTelNos(): string[] {
    return this.students()
      .filter(row => row.selected && row.telNo.trim() !== '')
      .map(row => row.telNo.trim());
  }

  private handleBulkSendResponse(response: SmsSendResponse, requestedTelNos: string[]): void {
    this.isSending.set(false);

    const returnedTelNos = response?.telNos ?? [];
    if (returnedTelNos.length === 0) {
      this.showAlert(String(response?.executionStatus ?? 'SMS request completed.'), { type: 'info' });
      return;
    }

    const noOfSms = this.smsCounter().messages || 1;
    const costPerMtnDndNo = Number(response.costPerMtnDndNo ?? 0);
    const costPerOrdinaryNo = Number(response.costPerOrdinaryNo ?? 0);
    const totalSmsCostPerMtnDndNo = costPerMtnDndNo * noOfSms;
    const totalSmsCostPerOrdinaryNo = costPerOrdinaryNo * noOfSms;

    const noOfInvalidNo = returnedTelNos.filter(item => item.telNoStatus === 2).length;
    const noOfMtnDndNo = returnedTelNos.filter(item => item.telNoStatus === 1).length;
    const noOfOrdinaryNo = returnedTelNos.filter(item => item.telNoStatus === 0).length;
    const mtnDndNoTotalCharge = noOfMtnDndNo * costPerMtnDndNo * noOfSms;
    const ordinaryNoTotalCharge = noOfOrdinaryNo * costPerOrdinaryNo * noOfSms;
    const overallTotalCharge = mtnDndNoTotalCharge + ordinaryNoTotalCharge;

    const telStatusByNumber = new Map(
      returnedTelNos
        .filter(item => item.telNo)
        .map(item => [item.telNo!.trim(), item.telNoStatus ?? 2])
    );

    const rows: SendResultRow[] = [];
    for (const student of this.students()) {
      if (!student.selected) {
        continue;
      }

      const telNo = student.telNo.trim();
      if (!telNo || !requestedTelNos.includes(telNo)) {
        continue;
      }

      const telNoStatus = telStatusByNumber.get(telNo);
      if (telNoStatus === undefined) {
        continue;
      }

      let status = 'Sent';
      let chargeHtml = String(totalSmsCostPerOrdinaryNo);

      if (telNoStatus === 1) {
        status = "Can't receive SMS";
        chargeHtml = `<span class="charge-highlight">${totalSmsCostPerMtnDndNo}</span>`;
      } else if (telNoStatus === 2) {
        status = 'Invalid no.';
        chargeHtml = '<span class="charge-zero">0</span>';
      }

      rows.push({
        classArm: student.classArm,
        admsnNo: student.idNo,
        fullName: student.fullName,
        guardianName: student.guardianName,
        telNo,
        status,
        chargeHtml,
      });
    }

    if (rows.length === 0) {
      this.showAlert(String(response.executionStatus ?? 'SMS request completed.'), { type: 'info' });
      return;
    }

    this.sendResult.set({
      noOfSms,
      costPerOrdinaryNo,
      costPerMtnDndNo: totalSmsCostPerMtnDndNo,
      overallTotalCharge: overallTotalCharge.toFixed(2),
      noOfMtnDndNo,
      mtnDndTotalCharge: mtnDndNoTotalCharge.toFixed(2),
      noOfOrdinaryNo,
      ordinaryTotalCharge: ordinaryNoTotalCharge.toFixed(2),
      noOfInvalidNo,
      rows,
    });
    this.modalMode.set('sendResult');
    this.getSmsBalance();
  }

  private readBalanceMessage(response: unknown): string {
    if (typeof response === 'string') {
      return response;
    }

    const record = response as { balance?: string; message?: string } | null;
    return String(record?.balance ?? record?.message ?? '');
  }

  private readMessageResponse(response: unknown, fallback: string): string {
    if (typeof response === 'string') {
      return response;
    }

    const record = response as { message?: string } | null;
    return String(record?.message ?? fallback);
  }

  private applyBalanceResponse(data: string): void {
    if (!data) {
      this.showAlert('Unable to complete the process.', { type: 'error' });
      return;
    }

    if (!data.includes(':')) {
      if (data === '000') {
        this.showAlert('Wrong credentials. Kindly contact the admin personnel.', { type: 'error' });
      } else {
        this.showAlert(data, { type: 'warning' });
      }
      this.balanceAmount.set(null);
      this.pricePerSms.set(null);
      return;
    }

    const [balance, price] = data.split(':');
    this.balanceAmount.set(parseFloat(balance).toFixed(2));
    this.pricePerSms.set(parseFloat(price).toFixed(2));
  }

  private computeSmsCounter(message: string): SmsCounter {
    const length = message.length;
    if (length === 0) {
      return {
        encoding: 'GSM_7BIT',
        length: 0,
        messages: 0,
        perMessage: 160,
        remaining: 160,
      };
    }

    const singleLimit = 160;
    const multiLimit = 153;
    const messages = length <= singleLimit ? 1 : Math.ceil(length / multiLimit);
    const perMessage = messages === 1 ? singleLimit : multiLimit;
    const remaining = perMessage * messages - length;

    return {
      encoding: 'GSM_7BIT',
      length,
      messages,
      perMessage,
      remaining,
    };
  }

  private openPrintWindow(content: string): void {
    const printCss =
      '<style>.table {border-collapse: collapse !important;}.table-bordered th,.table-bordered td {border: 1px solid #ddd !important;}.table td,.table th {background-color: #fff !important;}</style>';
    const winPrint = window.open('', '_blank', 'width=900,height=700');
    if (!winPrint) {
      return;
    }

    winPrint.document.write(printCss + content);
    winPrint.document.close();
    winPrint.focus();
    winPrint.print();
    winPrint.close();
  }

  private toSmsStudent(value: unknown): SmsStudentRow {
    const record = value as Record<string, unknown>;
    const classArmRecord = record['classArm'] as Record<string, unknown> | undefined;
    const classRecord = classArmRecord?.['classs'] as Record<string, unknown> | undefined;
    const classLabel = String(classRecord?.['classLabel'] ?? record['classLabel'] ?? '');
    const armName = String(classArmRecord?.['armName'] ?? record['arm'] ?? '');
    const classArm = [classLabel, armName].filter(Boolean).join(' ').trim();

    return {
      id: String(record['id'] ?? record['userId'] ?? ''),
      idNo: String(record['idNo'] ?? record['admsnNo'] ?? ''),
      fullName: String(record['fullName'] ?? '').trim()
        || [record['firstName'], record['middleName'], record['surName']].filter(Boolean).join(' ').trim(),
      guardianName: String(record['guardiansName'] ?? record['guardianName'] ?? ''),
      telNo: String(record['mobileNumber'] ?? record['telNo'] ?? record['homeNumber'] ?? ''),
      classArm,
      selected: false,
    };
  }

  private toSystemInfo(value: unknown): SystemInfo {
    const record = value as Record<string, unknown>;
    const officer = record['schoolRegistrationOfficer'] as Record<string, unknown> | undefined;
    const user = officer?.['user'] as Record<string, unknown> | undefined;

    return {
      userId: String(user?.['id'] ?? officer?.['userId'] ?? record['schoolRegistrationOfficerId'] ?? ''),
      schoolName: String(record['schoolName'] ?? ''),
      schoolAddress: String(record['schoolAddress'] ?? ''),
      telephone: String(record['telephone'] ?? ''),
      emailAddress: String(record['emailAddress'] ?? ''),
      website: String(record['website'] ?? ''),
      motto: String(record['motto'] ?? ''),
      smsApiToken: String(record['smsApiToken'] ?? ''),
      smsApiKey: String(record['smsApiKey'] ?? ''),
    };
  }

  private asArray(value: unknown): unknown[] {
    return Array.isArray(value) ? value : [];
  }

  private getApiErrorMessage(error: unknown, fallback: string): string {
    const httpError = error as { status?: number; error?: { message?: string } };
    if (httpError?.status === 0) {
      return 'Unable to connect to the API server. Check that the API is running and reachable from this site.';
    }

    const apiMessage = httpError?.error?.message;
    if (typeof apiMessage === 'string' && apiMessage.trim()) {
      return apiMessage;
    }

    return this.getHttpErrorMessage(error, fallback);
  }
}