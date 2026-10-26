import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { environment } from '@env/environment';
import { BaseComponent, GenericService, SignatureCropper, SignatureCropResult } from '@shared';

type GenderValue = 0 | 1;
type HeadStatusValue = 0 | 1;
type TimeStatusValue = 0 | 1;

type SchoolHeadVm = {
  id: string;
  headsName: string;
  phoneNumber: string;
  gender: GenderValue;
  status: TimeStatusValue;
  headStatus: HeadStatusValue;
  signatureImageString: string;
};

type SchoolHeadPayload = {
  id?: string;
  headsName: string;
  phoneNumber: string;
  signatureImageString: string;
  status: TimeStatusValue;
  schoolHeadCategoryId: number;
  headStatus: HeadStatusValue;
  gender: GenderValue;
};

@Component({
  selector: 'app-principal-index',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, SignatureCropper],
  templateUrl: './principal-index.html',
  styleUrl: './principal-index.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrincipalIndex extends BaseComponent implements OnInit {
  @ViewChild('editorPanel') private editorPanel?: ElementRef<HTMLElement>;

  private readonly api = inject(GenericService<SchoolHeadPayload>);
  private readonly fb = inject(FormBuilder);
  private readonly apiOrigin = new URL(environment.serverUrl).origin;

  readonly heads = signal<SchoolHeadVm[]>([]);
  readonly editingHeadId = signal<string | null>(null);
  readonly showEditor = signal(false);
  readonly signaturePreviewUrl = signal('');
  private signaturePending = false;

  readonly isEditing = computed(() => this.editingHeadId() !== null);
  readonly hasHeads = computed(() => this.heads().length > 0);

  readonly headForm = this.fb.nonNullable.group({
    headsName: ['', [Validators.required, Validators.maxLength(40)]],
    phoneNumber: ['', [Validators.maxLength(15)]],
    gender: [0 as GenderValue, [Validators.required]],
    status: [0 as TimeStatusValue, [Validators.required]],
    headStatus: [1 as HeadStatusValue, [Validators.required]],
    signatureImageString: [''],
  });

  readonly genderOptions = [
    { value: 0 as GenderValue, label: 'Male' },
    { value: 1 as GenderValue, label: 'Female' },
  ];

  readonly statusOptions = [
    { value: 0 as TimeStatusValue, label: 'Current' },
    { value: 1 as TimeStatusValue, label: 'Previous' },
  ];

  readonly headStatusOptions = [
    { value: 0 as HeadStatusValue, label: 'HeadTeacher' },
    { value: 1 as HeadStatusValue, label: 'Principal' },
  ];

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.loadHeads();
  }

  refreshPage(): void {
    this.loadHeads();
  }

  beginCreate(): void {
    this.editingHeadId.set(null);
    this.showEditor.set(true);
    this.signaturePreviewUrl.set('');
    this.signaturePending = false;
    this.headForm.reset({
      headsName: '',
      phoneNumber: '',
      gender: 0,
      status: 0,
      headStatus: 1,
      signatureImageString: '',
    });
    this.errorMessage.set(null);
    this.cdr.markForCheck();
    this.scrollEditorIntoView();
  }

  beginEdit(head: SchoolHeadVm): void {
    this.editingHeadId.set(head.id);
    this.showEditor.set(true);
    // Keep the form value as the stored filename; resolve a full API URL for <img src>.
    this.signaturePreviewUrl.set(this.resolveSignaturePreviewUrl(head.signatureImageString));
    this.headForm.reset({
      headsName: head.headsName,
      phoneNumber: head.phoneNumber,
      gender: head.gender,
      status: head.status,
      headStatus: head.headStatus,
      signatureImageString: head.signatureImageString,
    });
    this.errorMessage.set(null);
    this.cdr.markForCheck();
    this.scrollEditorIntoView();
  }

  cancelEdit(): void {
    this.editingHeadId.set(null);
    this.showEditor.set(false);
    this.signaturePreviewUrl.set('');
    this.signaturePending = false;
    this.headForm.reset({
      headsName: '',
      phoneNumber: '',
      gender: 0,
      status: 0,
      headStatus: 1,
      signatureImageString: '',
    });
    this.errorMessage.set(null);
    this.cdr.markForCheck();
  }

  submitHead(): void {
    if (this.headForm.invalid) {
      this.validateAllFormFields(this.headForm);
      this.showAlert('Enter a valid school head record before saving.', { type: 'warning' });
      return;
    }

    if (this.signaturePending) {
      this.showAlert('Please crop the signature image before saving.', { type: 'warning' });
      return;
    }

    const editingId = this.editingHeadId();
    const values = this.headForm.getRawValue();
    const gender = this.coerceGender(values.gender);
    const status = this.coerceTimeStatus(values.status);
    const headStatus = this.coerceHeadStatus(values.headStatus);
    const payload: SchoolHeadPayload = {
      id: editingId ?? undefined,
      headsName: values.headsName.trim(),
      phoneNumber: values.phoneNumber.trim(),
      signatureImageString: values.signatureImageString,
      status,
      schoolHeadCategoryId: headStatus === 0 ? 1 : 2,
      headStatus,
      gender,
    };

    const request$ = editingId
      ? this.api.update(`/Registry/update-school-head/${editingId}`, payload)
      : this.api.create<unknown>('/Registry/create-school-head', payload);

    this.withRequestState(request$, {
      useLoading: true,
      clearError: true,
      errorMessage: editingId ? 'Failed to update school head details.' : 'Failed to create school head.',
      rethrow: true,
      notifyOnError: 'none',
      onError: error => console.error('School head save failed:', error),
    }).subscribe({
      next: response => {
        const message = this.readResponseMessage(response)
          ?? (editingId
            ? 'School head details updated successfully.'
            : 'The Principal information has successfully been posted.');

        this.showAlert(message, { type: 'success' });
        this.cancelEdit();
        this.loadHeads();
      },
      error: error => {
        this.showAlert(
          this.getHttpErrorMessage(
            error,
            editingId ? 'Failed to update school head details.' : 'Failed to create school head.'
          ),
          { type: 'error' }
        );
      },
    });
  }

  setSchoolHead(): void {
    this.withRequestState(this.api.create<unknown>('/Registry/create-school-head-assignment', {}), {
      useLoading: true,
      clearError: false,
      errorMessage: 'Failed to set the school head.',
      rethrow: true,
      notifyOnError: 'none',
      onError: error => console.error('Set school head failed:', error),
    }).subscribe({
      next: response => {
        const status = this.readNumericStatus(response);
        if (status === 0) {
          this.showAlert('School head(s) has successfully been set.', { type: 'success' });
          return;
        }

        const message = this.readResponseMessage(response)
          ?? 'Sorry you need to run Student assessment preparation before this.';
        this.showAlert(message, { type: 'warning', duration: 5000 });
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to set the school head.'), { type: 'error' });
      },
    });
  }

  viewSetSchoolHead(): void {
    this.withRequestState(this.api.getAnObj('/Registry/get-school-head-names'), {
      useLoading: true,
      clearError: false,
      errorMessage: 'Failed to load the set school head information.',
      rethrow: true,
      notifyOnError: 'none',
      onError: error => console.error('View set school head failed:', error),
    }).subscribe({
      next: response => {
        const message = this.readDisplayString(response) ?? 'No set school head information was returned.';
        this.showAlert(message, { type: 'info', duration: 7000 });
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to load the set school head information.'), { type: 'error' });
      },
    });
  }

  onSignatureImageSelected(): void {
    this.signaturePending = true;
    this.headForm.patchValue({ signatureImageString: '' });
  }

  onSignatureCropped(result: SignatureCropResult): void {
    this.signaturePending = false;
    this.headForm.patchValue({ signatureImageString: result.dataUrl });
    this.signaturePreviewUrl.set(result.dataUrl);
  }

  onSignatureCleared(): void {
    this.signaturePending = false;
    this.headForm.patchValue({ signatureImageString: '' });
    this.signaturePreviewUrl.set('');
    this.signaturePending = false;
  }

  getGenderLabel(gender: GenderValue): string {
    return gender === 1 ? 'Female' : 'Male';
  }

  getStatusLabel(status: TimeStatusValue): string {
    return status === 1 ? 'Previous' : 'Current';
  }

  getHeadStatusLabel(headStatus: HeadStatusValue): string {
    return headStatus === 0 ? 'HeadTeacher' : 'Principal';
  }

  private resolveSignaturePreviewUrl(value: string | null | undefined): string {
    const fileName = String(value ?? '').trim();
    if (!fileName) {
      return '';
    }
    if (fileName.startsWith('data:image') || /^https?:\/\//i.test(fileName)) {
      return fileName;
    }
    if (fileName.startsWith('/')) {
      return `${this.apiOrigin}${fileName}`;
    }

    // Prefer modern uploads path; also resolve legacy MVC Content/Signatures URLs.
    return `${this.apiOrigin}/Signatures/${encodeURIComponent(fileName)}`;
  }

  private loadHeads(): void {
    this.withRequestState(this.api.get('/Registry/get-school-heads'), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Failed to load school head records.',
      rethrow: true,
      notifyOnError: 'none',
      onError: error => console.error('Error loading school head records:', error),
    }).subscribe({
      next: response => {
        this.heads.set(this.normalizeHeads(response));
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to load school head records.'), { type: 'error' });
      },
    });
  }

  private normalizeHeads(response: unknown): SchoolHeadVm[] {
    const items = this.extractArray(response);

    return items
      .map(item => {
        const record = item as Record<string, unknown>;
        const category = this.asRecord(record['schoolHeadCategory'] ?? record['SchoolHeadCategory']);
        const headStatus = category && (category['headStatus'] !== undefined || category['HeadStatus'] !== undefined)
          ? (category['headStatus'] ?? category['HeadStatus'])
          : (record['headStatus'] ?? record['HeadStatus']);

        return {
          id: this.readString(record, 'id', 'Id'),
          headsName: this.readString(record, 'headsName', 'HeadsName'),
          phoneNumber: this.readString(record, 'phoneNumber', 'PhoneNumber'),
          gender: this.coerceGender(record['gender'] ?? record['Gender']),
          status: this.coerceTimeStatus(record['status'] ?? record['Status']),
          headStatus: this.coerceHeadStatus(headStatus),
          signatureImageString: this.readString(record, 'signatureImageString', 'SignatureImageString'),
        } satisfies SchoolHeadVm;
      })
      .filter(item => item.id || item.headsName)
      .sort((left, right) => left.headsName.localeCompare(right.headsName));
  }

  private readString(record: Record<string, unknown>, ...keys: string[]): string {
    for (const key of keys) {
      const value = record[key];
      if (value !== undefined && value !== null) {
        const text = String(value).trim();
        if (text) {
          return text;
        }
      }
    }

    return '';
  }

  private extractArray(value: unknown): unknown[] {
    if (Array.isArray(value)) {
      return value;
    }

    const record = this.asRecord(value);
    if (!record) {
      return [];
    }

    const directData = record.data;
    if (Array.isArray(directData)) {
      return directData;
    }

    const directItems = record.items;
    if (Array.isArray(directItems)) {
      return directItems;
    }

    const nestedArray = Object.values(record).find(Array.isArray);
    return Array.isArray(nestedArray) ? nestedArray : [];
  }

  private readResponseMessage(value: unknown): string | null {
    if (typeof value === 'string') {
      return value.trim() ? value : null;
    }

    const record = this.asRecord(value);
    if (!record) {
      return null;
    }

    const candidates = [record.message, record.title, record.data, record.result];
    for (const candidate of candidates) {
      if (typeof candidate === 'string' && candidate.trim()) {
        return candidate;
      }
    }

    return null;
  }

  private readDisplayString(value: unknown): string | null {
    if (typeof value === 'string') {
      return value.trim() ? value : null;
    }

    const record = this.asRecord(value);
    if (!record) {
      return null;
    }

    const candidates = [record.data, record.message, record.result, record.title];
    for (const candidate of candidates) {
      if (typeof candidate === 'string' && candidate.trim()) {
        return candidate;
      }
    }

    return null;
  }

  private readNumericStatus(value: unknown): number | null {
    if (typeof value === 'number' && Number.isFinite(value)) {
      return value;
    }

    if (typeof value === 'string') {
      const parsed = Number(value);
      return Number.isFinite(parsed) ? parsed : null;
    }

    const record = this.asRecord(value);
    if (!record) {
      return null;
    }

    const candidates = [record['status'], record['Status'], record['data'], record['result'], record['message']];
    for (const candidate of candidates) {
      const parsed = this.readNumericStatus(candidate);
      if (parsed !== null) {
        return parsed;
      }
    }

    return null;
  }

  private asRecord(value: unknown): Record<string, unknown> | null {
    return value && typeof value === 'object' ? value as Record<string, unknown> : null;
  }

  private coerceGender(value: unknown): GenderValue {
    return Number(value) === 1 ? 1 : 0;
  }

  private coerceHeadStatus(value: unknown): HeadStatusValue {
    return Number(value) === 0 ? 0 : 1;
  }

  private coerceTimeStatus(value: unknown): TimeStatusValue {
    return Number(value) === 1 ? 1 : 0;
  }

  private scrollEditorIntoView(): void {
    queueMicrotask(() => {
      this.editorPanel?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
      this.cdr.markForCheck();
    });
  }
}