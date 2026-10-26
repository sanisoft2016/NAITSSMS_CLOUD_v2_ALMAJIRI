import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { BaseComponent, GenericService, ImageCompressionService } from '@shared';
import { environment } from '@env/environment';

type SchoolInfoVm = {
  ssNo?: number;
  schoolName?: string;
  schoolAddress?: string;
  telephone?: string;
  poAddress?: string;
  emailAddress?: string;
  website?: string;
  schoolLogoString?: string;
  motto?: string;
  schoolRegistrationOfficerId?: string;
};

type SchoolRegistrationResponse = {
  proprietorCode?: string;
  schoolInfo?: SchoolInfoVm;
  logoUrl?: string | null;
};

@Component({
  selector: 'app-school-registration',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './school-registration.html',
  styleUrl: './school-registration.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SchoolRegistration extends BaseComponent implements OnInit {
  private readonly api = inject(GenericService<FormData>);
  private readonly fb = inject(FormBuilder);
  private readonly imageCompression = inject(ImageCompressionService);

  private readonly apiOrigin = new URL(environment.serverUrl).origin;
  private readonly logoMaxKb = 16;

  readonly proprietorCode = signal('');
  readonly registrationId = signal('');
  readonly logoPreviewUrl = signal('');
  readonly existingLogoFile = signal('');
  readonly isCompressingLogo = signal(false);

  readonly schoolForm = this.fb.nonNullable.group({
    schoolName: ['', [Validators.required, Validators.maxLength(70)]],
    emailAddress: ['', [Validators.maxLength(50)]],
    schoolAddress: ['', [Validators.maxLength(150)]],
    telephone: ['', [Validators.maxLength(50)]],
    poAddress: ['', [Validators.maxLength(100)]],
    website: ['', [Validators.maxLength(70)]],
    motto: ['', [Validators.maxLength(70)]],
  });

  private pendingLogoFile: File | null = null;

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.loadSchoolProfile();
  }

  async onLogoSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    const looksLikeImage =
      file.type.startsWith('image/') ||
      ['image/jpeg', 'image/png', 'image/jpg'].includes(file.type) ||
      /\.(jpe?g|png)$/i.test(file.name);
    if (!looksLikeImage) {
      this.showAlert("Only 'JPEG' and 'png' image are allowed.", { type: 'error' });
      input.value = '';
      return;
    }

    this.isCompressingLogo.set(true);
    this.cdr.markForCheck();

    try {
      const compressed = await this.imageCompression.compressImageFile(file, {
        maxKB: this.logoMaxKb,
        maxDimension: 512,
        fileName: 'school-logo.jpg',
      });

      this.pendingLogoFile = compressed;
      const reader = new FileReader();
      reader.onload = () => {
        this.logoPreviewUrl.set(String(reader.result ?? ''));
        this.cdr.markForCheck();
      };
      reader.readAsDataURL(compressed);
    } catch (error) {
      this.pendingLogoFile = null;
      input.value = '';
      const message =
        error instanceof Error
          ? error.message
          : `Unable to reduce logo below ${this.logoMaxKb}KB. Please use a smaller image.`;
      this.showAlert(message, { type: 'warning' });
    } finally {
      this.isCompressingLogo.set(false);
      this.cdr.markForCheck();
    }
  }

  saveSchoolProfile(): void {
    if (this.schoolForm.invalid) {
      this.schoolForm.markAllAsTouched();
      return;
    }

    if (this.isCompressingLogo()) {
      this.showAlert('Please wait for logo compression to finish.', { type: 'warning' });
      return;
    }

    const values = this.schoolForm.getRawValue();
    const formData = new FormData();
    formData.append('SchoolName', values.schoolName);
    formData.append('EmailAddress', values.emailAddress);
    formData.append('SchoolAddress', values.schoolAddress);
    formData.append('Telephone', values.telephone);
    formData.append('PoAddress', values.poAddress);
    formData.append('Website', values.website);
    formData.append('Motto', values.motto);

    if (this.existingLogoFile()) {
      formData.append('SchoolLogoStringOld', this.existingLogoFile());
    }

    if (this.pendingLogoFile) {
      formData.append('SchoolLogo', this.pendingLogoFile, this.pendingLogoFile.name);
    }

    this.withRequestState(
      this.api.updateWithFile('/Registry/school/registration', formData),
      {
        useLoading: true,
        clearError: true,
        errorMessage: 'Failed to update school registration.',
      }
    ).subscribe({
      next: response => {
        this.showAlert(String((response as any)?.message || 'School registration updated successfully.'), { type: 'success' });
        this.pendingLogoFile = null;
        this.router.navigate(['/admin']);
      },
      error: error => {
        this.showAlert(this.getHttpErrorMessage(error, 'Failed to update school registration.'), { type: 'error' });
      },
    });
  }

  private loadSchoolProfile(): void {
    this.withRequestState(this.api.getAnObj<SchoolRegistrationResponse>('/Registry/school/registration'), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Failed to load school registration profile.',
    }).subscribe({
      next: response => {
        const payload = (response as SchoolRegistrationResponse) ?? {};
        const school = this.normalizeSchoolInfo(payload.schoolInfo);
        this.proprietorCode.set(String(payload.proprietorCode ?? (response as any)?.ProprietorCode ?? ''));
        this.registrationId.set(`${this.proprietorCode()}${school.ssNo ?? ''}`);
        this.existingLogoFile.set(String(school.schoolLogoString ?? ''));
        this.logoPreviewUrl.set(this.resolveLogoUrl(
          payload.logoUrl ?? (response as any)?.logoUrl ?? (response as any)?.LogoUrl,
          school.schoolLogoString,
        ));
        this.schoolForm.reset({
          schoolName: String(school.schoolName ?? ''),
          emailAddress: String(school.emailAddress ?? ''),
          schoolAddress: String(school.schoolAddress ?? ''),
          telephone: String(school.telephone ?? ''),
          poAddress: String(school.poAddress ?? ''),
          website: String(school.website ?? ''),
          motto: String(school.motto ?? ''),
        });
      },
      error: error => {
        this.errorMessage.set(this.getHttpErrorMessage(error, 'Failed to load school registration profile.'));
      },
    });
  }

  private normalizeSchoolInfo(value: SchoolInfoVm | Record<string, unknown> | null | undefined): SchoolInfoVm {
    const record = (value ?? {}) as Record<string, unknown>;
    return {
      ssNo: Number(record['ssNo'] ?? record['SsNo'] ?? 0) || undefined,
      schoolName: String(record['schoolName'] ?? record['SchoolName'] ?? '').trim(),
      schoolAddress: String(record['schoolAddress'] ?? record['SchoolAddress'] ?? '').trim(),
      telephone: String(record['telephone'] ?? record['Telephone'] ?? '').trim(),
      poAddress: String(record['poAddress'] ?? record['PoAddress'] ?? '').trim(),
      emailAddress: String(record['emailAddress'] ?? record['EmailAddress'] ?? '').trim(),
      website: String(record['website'] ?? record['Website'] ?? '').trim(),
      schoolLogoString: String(record['schoolLogoString'] ?? record['SchoolLogoString'] ?? '').trim(),
      motto: String(record['motto'] ?? record['Motto'] ?? '').trim(),
      schoolRegistrationOfficerId: String(
        record['schoolRegistrationOfficerId'] ?? record['SchoolRegistrationOfficerId'] ?? '',
      ).trim(),
    };
  }

  private resolveLogoUrl(logoUrl?: string | null, fileName?: string): string {
    if (logoUrl) {
      return logoUrl.startsWith('http') ? logoUrl : `${this.apiOrigin}${logoUrl}`;
    }

    if (fileName) {
      return `${this.apiOrigin}/school-logos/${fileName}`;
    }

    return '';
  }
}
