import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpParams } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatIconModule } from '@angular/material/icon';
import { Observable, Subscription, throwError } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';
import { environment } from '@env/environment';

import { SeoService } from '@core';
import { BaseComponent, CurrentClass, GenericService } from '@shared';
import {
  ArmName,
  ClassCategoryStatus,
  Gender,
  Religion,
  StudentRegistrationContext,
  StudentRegistrationDefaultsDto,
  StudentStatus,
} from '@shared/interfaces';
import { StudentRegistrationContextService } from '@shared/services';
import {
  SENIOR_CATEGORY_OPTIONS,
  isSeniorClass,
  seniorCategoryValidator,
  syncClassCategory,
} from '../class-category';

@Component({
  selector: 'app-register-student',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDatepickerModule, MatIconModule],
  templateUrl: './register-student.html',
  styleUrls: ['./register-student.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterStudentComponent extends BaseComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(GenericService<any>);
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly studentRegistrationContext = inject(StudentRegistrationContextService);
  private readonly seo = inject(SeoService);

  context: StudentRegistrationContext | null = null;
  contextMessage = '';
  passportFile: File | null = null;
  passportPreviewUrl: string | null = null;
  /** Full selected image shown under the crop frame (legacy #preview-img). */
  passportSourceUrl: string | null = null;
  showCropUi = false;
  isPassportCropped = false;
  cropLeft = 0;
  cropTop = 0;
  cropDisplayWidth = 160;
  cropDisplayHeight = 194;
  readonly cropOutputWidth = 160;
  readonly cropOutputHeight = 194;
  private imgNaturalWidth = 0;
  private imgNaturalHeight = 0;
  private imgDisplayWidth = 0;
  private imgDisplayHeight = 0;
  private cropDragging = false;
  private dragStartX = 0;
  private dragStartY = 0;
  private frameStartLeft = 0;
  private frameStartTop = 0;
  private readonly onCropPointerMove = (event: MouseEvent | TouchEvent) => this.handleCropPointerMove(event);
  private readonly onCropPointerUp = () => this.handleCropPointerUp();
  private readonly studentId = this.activatedRoute.snapshot.queryParamMap.get('studentId')?.trim() ?? '';

  readonly genderOptions = [
    { value: Gender.Male, label: 'Male' },
    { value: Gender.Female, label: 'Female' },
  ];

  readonly statusOptions = [
    { value: StudentStatus.Fresher, label: 'Fresher' },
    { value: StudentStatus.Returning, label: 'Returning' },
    { value: StudentStatus.Previous, label: 'Previous' },
  ];

  readonly religionOptions = [
    { value: Religion.Islam, label: 'Islam' },
    { value: Religion.Christianity, label: 'Christianity' },
  ];

  readonly classCategoryOptions = [
    { value: ClassCategoryStatus.PlayClass, label: 'PlayClass' },
    { value: ClassCategoryStatus.PreNursery, label: 'PreNursery' },
    { value: ClassCategoryStatus.Nursery, label: 'Nursery' },
    { value: ClassCategoryStatus.Primary, label: 'Primary' },
    { value: ClassCategoryStatus.Jss, label: 'Jss' },
    { value: ClassCategoryStatus.Science, label: 'Science' },
    { value: ClassCategoryStatus.Art, label: 'Art' },
    { value: ClassCategoryStatus.Commercial, label: 'Commercial' },
    { value: ClassCategoryStatus.None, label: 'None' },
  ];

  readonly seniorCategoryOptions = SENIOR_CATEGORY_OPTIONS;
  readonly noCategory = ClassCategoryStatus.None;
  private classCategorySync?: Subscription;

  readonly form = this.fb.nonNullable.group({
    sNo: [0],
    idNo: ['', [Validators.required, Validators.maxLength(15)]],
    firstName: ['', [Validators.required, Validators.maxLength(40)]],
    middleName: ['', [Validators.maxLength(40)]],
    surName: ['', [Validators.required, Validators.maxLength(40)]],
    dateOfBirth: ['', [Validators.required]],
    placeOfBirth: ['', [Validators.maxLength(50)]],
    status: [StudentStatus.Fresher, [Validators.required]],
    gender: [Gender.Male, [Validators.required]],
    religion: [Religion.Christianity, [Validators.required]],
    classCategory: [ClassCategoryStatus.None, [Validators.required, seniorCategoryValidator]],
    bloodGroup: ['', [Validators.maxLength(10)]],
    dateOfAdmsn: ['', [Validators.required]],
    transferedFromWhere: ['', [Validators.maxLength(150)]],
    studentMobileNo: ['', [Validators.maxLength(50)]],
    classArmId: [''],
    currentClass: [CurrentClass.PreNursery, [Validators.required]],
    guardiansName: ['', [Validators.maxLength(50)]],
    relationship: ['', [Validators.maxLength(50)]],
    guardianAddress: ['', [Validators.maxLength(200)]],
    occupation: ['', [Validators.maxLength(50)]],
    mobileNumber: ['', [Validators.maxLength(50)]],
    homeNumber: ['', [Validators.maxLength(50)]],
    emailAddress: ['', [Validators.email, Validators.maxLength(100)]],
    country: ['', [Validators.maxLength(50)]],
  });

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.seo.apply({
      title: 'Student registration',
      description: 'NAITS-ASMS student registration form.',
      path: '/auth/register-student',
      robots: 'noindex,nofollow',
    });
    // Every path that sets the class (URL context, server defaults) re-derives the category.
    this.classCategorySync = this.form.controls.currentClass.valueChanges.subscribe(() => this.applyClassCategory());

    this.context = this.resolveContext();
    if (!this.context) {
      this.contextMessage =
        'This is the student details capturing form. Open it from Student List to load the class-arm defaults and enable saving.';
      return;
    }

    this.contextMessage = '';
    this.form.patchValue({
      classArmId: this.context.armId,
      currentClass: this.resolveCurrentClass(this.context.classs),
    });

    this.loadDefaults();
  }

  ngOnDestroy(): void {
    this.classCategorySync?.unsubscribe();
    this.clearPassportCropState();
  }

  get isSeniorClass(): boolean {
    return isSeniorClass(this.form.controls.currentClass.value);
  }

  get currentClassLabel(): string {
    const currentClass = this.form.controls.currentClass.value;
    const match = this.api.classNames.find(
      (option: { value: CurrentClass; label: string }) => option.value === currentClass,
    );
    return match?.label ?? String(currentClass);
  }

  get hasRegistrationContext(): boolean {
    return !!this.context;
  }

  get isEditMode(): boolean {
    return this.studentId.length > 0;
  }

  get pageTitle(): string {
    if (this.isEditMode) {
      return 'Edit Student';
    }

    return this.context
      ? `${this.context.classLabel} ${this.context.armName} Student Registration`
      : 'Student Registration';
  }

  get submitLabel(): string {
    return this.isEditMode ? 'Update' : 'Submit';
  }

  goBack(): void {
    if (!this.context) {
      this.router.navigate(['/admin/class-reg-index']);
      return;
    }

    this.router.navigate(['/admin/student-reg-index'], {
      state: this.context,
      queryParams: this.studentRegistrationContext.toQueryParams(this.context),
    });
  }

  onPassportSelected(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.[0] ?? null;

    this.clearPassportCropState();
    this.passportFile = null;
    this.isPassportCropped = false;

    if (!file) {
      this.cdr.markForCheck();
      return;
    }

    const ext = file.name.includes('.')
      ? file.name.substring(file.name.lastIndexOf('.') + 1).toLowerCase()
      : '';
    const allowedExt = ['gif', 'png', 'jpg', 'jpeg', 'heic', 'heif', 'webp'];
    const looksLikeImage =
      file.type.startsWith('image/') || (!ext && !!file.type) || allowedExt.includes(ext);
    if (!looksLikeImage) {
      this.showAlert('Pls select only images', { type: 'warning' });
      if (input) {
        input.value = '';
      }
      this.cdr.markForCheck();
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      this.passportSourceUrl = String(reader.result ?? '');
      this.showCropUi = false;
      this.cdr.markForCheck();
    };
    reader.onerror = () => {
      this.showAlert('Unable to load the selected image.', { type: 'error' });
      this.cdr.markForCheck();
    };
    reader.readAsDataURL(file);
  }

  onPassportImageLoaded(event: Event): void {
    const img = event.target as HTMLImageElement | null;
    if (!img || !this.passportSourceUrl) {
      return;
    }

    // Defer measure one frame so mobile layout/CSS sizing is settled.
    requestAnimationFrame(() => {
      this.imgNaturalWidth = img.naturalWidth;
      this.imgNaturalHeight = img.naturalHeight;
      this.imgDisplayWidth = img.clientWidth || img.width;
      this.imgDisplayHeight = img.clientHeight || img.height;
      this.fitCropFrameToImage();
      this.showCropUi = true;
      this.isPassportCropped = false;
      this.cdr.markForCheck();
    });
  }

  onCropPointerDown(event: MouseEvent | TouchEvent): void {
    event.preventDefault();
    event.stopPropagation();
    const pos = this.getEventPosition(event);
    this.cropDragging = true;
    this.dragStartX = pos.x;
    this.dragStartY = pos.y;
    this.frameStartLeft = this.cropLeft;
    this.frameStartTop = this.cropTop;
    document.addEventListener('mousemove', this.onCropPointerMove, { passive: false });
    document.addEventListener('touchmove', this.onCropPointerMove, { passive: false });
    document.addEventListener('mouseup', this.onCropPointerUp);
    document.addEventListener('touchend', this.onCropPointerUp);
    document.addEventListener('touchcancel', this.onCropPointerUp);
  }

  async cropAndUsePassport(): Promise<void> {
    if (!this.passportSourceUrl || !this.showCropUi) {
      return;
    }

    if (!this.imgDisplayWidth || !this.imgDisplayHeight) {
      this.showAlert('Please wait for the image to finish loading.', { type: 'warning' });
      return;
    }

    try {
      const blob = await this.cropPassportToBlobAsync();
      if (!blob) {
        this.showAlert(
          'Unable to reduce image below 14KB. Please use a smaller or lower resolution image.',
          { type: 'warning' },
        );
        return;
      }

      this.passportFile = new File([blob], 'passport.jpg', { type: 'image/jpeg' });
      if (this.passportPreviewUrl) {
        URL.revokeObjectURL(this.passportPreviewUrl);
      }
      this.passportPreviewUrl = URL.createObjectURL(blob);
      this.passportSourceUrl = null;
      this.showCropUi = false;
      this.isPassportCropped = true;
      this.cdr.markForCheck();
    } catch {
      this.showAlert('Failed to crop passport image.', { type: 'error' });
    }
  }

  async onSubmit(): Promise<void> {
    this.form.markAllAsTouched();
    if (!this.context) {
      const message = 'Select a class arm from Student List before submitting this registration.';
      this.errorMessage.set(message);
      this.showAlert(message, { type: 'error' });
      return;
    }

    if (this.form.invalid || !this.startSubmitting()) {
      return;
    }

    if (this.passportSourceUrl && this.showCropUi && !this.isPassportCropped) {
      const message = 'Please crop the image before submitting.';
      this.errorMessage.set(message);
      this.showAlert(message, { type: 'warning' });
      this.stopSubmitting();
      return;
    }

    const value = this.form.getRawValue();
    const formData = new FormData();

    formData.append('classId', this.context.classId);
    formData.append('classLabel', this.context.classLabel);
    formData.append('armId', this.context.armId);
    formData.append('armName', this.resolveArmNameForApi(this.context.armName));
    formData.append('currentClass', String(Number(value.currentClass)));
    formData.append('sNo', String(value.sNo));
    formData.append('idNo', value.idNo.trim());
    formData.append('firstName', value.firstName.trim());
    formData.append('middleName', value.middleName.trim());
    formData.append('surName', value.surName.trim());
    formData.append('dateOfBirth', this.formatDateForApi(value.dateOfBirth));
    formData.append('placeOfBirth', value.placeOfBirth.trim());
    formData.append('status', String(Number(value.status)));
    formData.append('gender', String(Number(value.gender)));
    formData.append('religion', String(Number(value.religion)));
    formData.append('classCategory', String(Number(value.classCategory)));
    formData.append('bloodGroup', value.bloodGroup.trim());
    formData.append('dateOfAdmsn', this.formatDateForApi(value.dateOfAdmsn));
    formData.append('transferedFromWhere', value.transferedFromWhere.trim());
    formData.append('studentMobileNo', value.studentMobileNo.trim());
    formData.append('classArmId', value.classArmId || this.context.armId);
    formData.append('guardiansName', value.guardiansName.trim());
    formData.append('relationship', value.relationship.trim());
    formData.append('guardianAddress', value.guardianAddress.trim());
    formData.append('occupation', value.occupation.trim());
    formData.append('mobileNumber', value.mobileNumber.trim());
    formData.append('homeNumber', value.homeNumber.trim());
    const emailAddress = value.emailAddress.trim();
    if (emailAddress) {
      formData.append('emailAddress', emailAddress);
    }
    formData.append('country', value.country.trim());

    if (this.passportFile) {
      formData.append(
        'passport',
        this.passportFile,
        this.passportFile.name || 'passport.jpg',
      );
    }

    const request$ = this.createOrUpdateStudentRequest(formData);

    this.withRequestState(request$, {
      useLoading: false,
      clearError: true,
      errorMessage: this.isEditMode
        ? 'Failed to update student. Please review the form and try again.'
        : 'Failed to register student. Please review the form and try again.',
      notifyOnError: 'none',
      rethrow: true,
      onError: error => console.error(this.isEditMode ? 'Student update failed:' : 'Student registration failed:', error),
    })
      .pipe(finalize(() => this.stopSubmitting()))
      .subscribe({
        next: (response: any) => {
          const nextCount = this.isEditMode
            ? (this.context?.totalNoOfStudents ?? 0)
            : (this.context?.totalNoOfStudents ?? 0) + 1;
          const nextContext = {
            ...this.context!,
            totalNoOfStudents: nextCount,
          };
          this.studentRegistrationContext.setContext(nextContext);

          const successMessage =
            response?.message ?? (this.isEditMode ? 'Student updated successfully.' : 'Student Registration Successful.');
          this.showAlert(successMessage, { type: 'success' });
          this.router.navigate(['/admin/student-reg-index'], {
            state: nextContext,
            queryParams: this.studentRegistrationContext.toQueryParams(nextContext),
          });
        },
        error: error => {
          const message =
            this.getHttpErrorMessage(error, '') ||
            (error as { error?: { message?: string } })?.error?.message ||
            'Failed to register student.';
          this.showAlert(message, { type: 'error' });
        },
      });
  }

  private async loadStudentForEdit(): Promise<void> {
    if (!this.studentId) {
      return;
    }

    try {
      const student = await new Promise<any>((resolve, reject) => {
        this.withRequestState(this.getStudentDetailsRequest(this.studentId), {
          useLoading: true,
          clearError: true,
          errorMessage: 'Failed to load student details.',
          notifyOnError: 'none',
          rethrow: true,
          onError: error => console.error('Failed to load student details:', error),
        }).subscribe({
          next: resolve,
          error: reject,
        });
      });

      this.form.patchValue({
        sNo: student?.sNo ?? this.form.controls.sNo.value,
        idNo: student?.idNo ?? '',
        firstName: student?.firstName ?? '',
        middleName: student?.middleName ?? '',
        surName: student?.surName ?? '',
        dateOfBirth: this.toDateValue(student?.dateOfBirth),
        placeOfBirth: student?.placeOfBirth ?? '',
        status: student?.status ?? this.form.controls.status.value,
        gender: student?.gender ?? this.form.controls.gender.value,
        religion: student?.religion ?? this.form.controls.religion.value,
        classCategory: student?.classCategory ?? this.form.controls.classCategory.value,
        bloodGroup: student?.bloodGroup ?? '',
        dateOfAdmsn: this.toDateValue(student?.dateOfAdmsn),
        transferedFromWhere: student?.transferedFromWhere ?? '',
        studentMobileNo: student?.studentMobileNo ?? '',
        classArmId: student?.classArmId ?? this.form.controls.classArmId.value,
        guardiansName: student?.guardiansName ?? '',
        relationship: student?.relationship ?? '',
        guardianAddress: student?.guardianAddress ?? '',
        occupation: student?.occupation ?? '',
        mobileNumber: student?.mobileNumber ?? '',
        homeNumber: student?.homeNumber ?? '',
        emailAddress: student?.emailAddress ?? '',
        country: student?.country ?? '',
      });
      // A saved Science/Art/Commercial choice is kept; junior classes snap back to their category.
      this.applyClassCategory();

      this.passportPreviewUrl = student?.passport
        ? `${new URL(environment.serverUrl).origin}/Passport/${student.passport}`
        : this.passportPreviewUrl;
      this.cdr.markForCheck();
    } catch (error) {
      const message = this.getHttpErrorMessage(error, 'Failed to load student details.');
      this.showAlert(message, { type: 'error' });
    }
  }

  private resolveContext(): StudentRegistrationContext | null {
    const stateContext = this.buildContext({
      classId: history.state?.classId,
      classs: history.state?.classs,
      className: history.state?.className,
      classLabel: history.state?.classLabel,
      armId: history.state?.armId,
      armName: history.state?.armName,
      aarmName: history.state?.aarmName,
      totalNoOfStudents: history.state?.totalNoOfStudents,
    });

    const routeContext = this.buildContext({
      classId: this.activatedRoute.snapshot.queryParamMap.get('classId'),
      classs: this.activatedRoute.snapshot.queryParamMap.get('classs'),
      className: this.activatedRoute.snapshot.queryParamMap.get('className'),
      classLabel: this.activatedRoute.snapshot.queryParamMap.get('classLabel'),
      armId: this.activatedRoute.snapshot.queryParamMap.get('armId'),
      armName: this.activatedRoute.snapshot.queryParamMap.get('armName'),
      aarmName: this.activatedRoute.snapshot.queryParamMap.get('aarmName'),
      totalNoOfStudents: this.activatedRoute.snapshot.queryParamMap.get('totalNoOfStudents'),
    });

    const context = stateContext ?? routeContext ?? this.studentRegistrationContext.getContext();
    if (context) {
      this.studentRegistrationContext.setContext(context);
    }

    return context;
  }

  private formatDateForApi(value: unknown): string {
    if (value instanceof Date && !Number.isNaN(value.getTime())) {
      const year = value.getFullYear();
      const month = `${value.getMonth() + 1}`.padStart(2, '0');
      const day = `${value.getDate()}`.padStart(2, '0');
      return `${year}-${month}-${day}`;
    }

    const raw = String(value ?? '').trim();
    if (!raw || raw.toLowerCase() === 'invalid date') {
      return '';
    }

    // Keep yyyy-MM-dd as-is; otherwise try Date parse.
    if (/^\d{4}-\d{2}-\d{2}/.test(raw)) {
      return raw.slice(0, 10);
    }

    const parsed = new Date(raw);
    if (!Number.isNaN(parsed.getTime())) {
      const year = parsed.getFullYear();
      const month = `${parsed.getMonth() + 1}`.padStart(2, '0');
      const day = `${parsed.getDate()}`.padStart(2, '0');
      return `${year}-${month}-${day}`;
    }

    return raw;
  }

  private resolveArmNameForApi(armName: string | number | null | undefined): string {
    if (typeof armName === 'number' && Number.isFinite(armName)) {
      return String(armName);
    }

    const raw = String(armName ?? '').trim();
    if (!raw) {
      return String(ArmName.A);
    }

    const asNumber = Number(raw);
    if (Number.isInteger(asNumber) && ArmName[asNumber] !== undefined) {
      return String(asNumber);
    }

    const matchedKey = Object.keys(ArmName).find(
      key => Number.isNaN(Number(key)) && key.toLowerCase() === raw.toLowerCase(),
    );
    if (matchedKey) {
      return String(ArmName[matchedKey as keyof typeof ArmName]);
    }

    return raw;
  }

  private async cropPassportToBlobAsync(): Promise<Blob | null> {
    const scaleX = this.imgNaturalWidth / this.imgDisplayWidth;
    const scaleY = this.imgNaturalHeight / this.imgDisplayHeight;
    const cropX = this.cropLeft * scaleX;
    const cropY = this.cropTop * scaleY;
    const cropW = this.cropDisplayWidth * scaleX;
    const cropH = this.cropDisplayHeight * scaleY;

    const imageObj = await this.loadImageAsync(this.passportSourceUrl!);
    let targetW = this.cropOutputWidth;
    let targetH = this.cropOutputHeight;
    const canvas = document.createElement('canvas');
    canvas.width = targetW;
    canvas.height = targetH;
    const ctx = canvas.getContext('2d');
    if (!ctx) {
      throw new Error('Unable to prepare image canvas for cropping.');
    }

    const draw = () => {
      canvas.width = targetW;
      canvas.height = targetH;
      ctx.clearRect(0, 0, targetW, targetH);
      ctx.drawImage(imageObj, cropX, cropY, cropW, cropH, 0, 0, targetW, targetH);
    };

    draw();

    const minQuality = 0.5;
    const maxQuality = 0.92;
    const qualityStep = 0.07;
    let currentQuality = maxQuality;

    while (true) {
      const blob = await new Promise<Blob | null>(resolve => {
        canvas.toBlob(result => resolve(result), 'image/jpeg', currentQuality);
      });

      if (!blob) {
        return null;
      }

      if (blob.size <= 14 * 1024) {
        return blob;
      }

      if (currentQuality > minQuality) {
        currentQuality -= qualityStep;
        continue;
      }

      if (targetW > 80 && targetH > 97) {
        targetW = Math.floor(targetW * 0.9);
        targetH = Math.floor(targetH * 0.9);
        draw();
        currentQuality = maxQuality;
        continue;
      }

      return null;
    }
  }

  /** Keep 160:194 aspect, but shrink the on-screen frame so it fits phone-sized previews. */
  private fitCropFrameToImage(): void {
    const aspect = this.cropOutputWidth / this.cropOutputHeight;
    let frameW = this.cropOutputWidth;
    let frameH = this.cropOutputHeight;

    if (this.imgDisplayWidth > 0 && this.imgDisplayHeight > 0) {
      if (frameW > this.imgDisplayWidth || frameH > this.imgDisplayHeight) {
        if (this.imgDisplayWidth / this.imgDisplayHeight > aspect) {
          frameH = this.imgDisplayHeight;
          frameW = frameH * aspect;
        } else {
          frameW = this.imgDisplayWidth;
          frameH = frameW / aspect;
        }
      }
    }

    this.cropDisplayWidth = Math.max(40, Math.floor(frameW));
    this.cropDisplayHeight = Math.max(49, Math.floor(frameH));
    this.cropLeft = Math.max(0, (this.imgDisplayWidth - this.cropDisplayWidth) / 2);
    this.cropTop = Math.max(0, (this.imgDisplayHeight - this.cropDisplayHeight) / 2);
  }

  private handleCropPointerMove(event: MouseEvent | TouchEvent): void {
    if (!this.cropDragging) {
      return;
    }
    event.preventDefault();
    const pos = this.getEventPosition(event);
    const dx = pos.x - this.dragStartX;
    const dy = pos.y - this.dragStartY;
    const maxLeft = Math.max(0, this.imgDisplayWidth - this.cropDisplayWidth);
    const maxTop = Math.max(0, this.imgDisplayHeight - this.cropDisplayHeight);
    this.cropLeft = Math.max(0, Math.min(this.frameStartLeft + dx, maxLeft));
    this.cropTop = Math.max(0, Math.min(this.frameStartTop + dy, maxTop));
    this.cdr.markForCheck();
  }

  private handleCropPointerUp(): void {
    this.cropDragging = false;
    document.removeEventListener('mousemove', this.onCropPointerMove);
    document.removeEventListener('touchmove', this.onCropPointerMove);
    document.removeEventListener('mouseup', this.onCropPointerUp);
    document.removeEventListener('touchend', this.onCropPointerUp);
    document.removeEventListener('touchcancel', this.onCropPointerUp);
  }

  private getEventPosition(event: MouseEvent | TouchEvent): { x: number; y: number } {
    if ('touches' in event) {
      const touch = event.touches[0] ?? event.changedTouches[0];
      return { x: touch?.pageX ?? 0, y: touch?.pageY ?? 0 };
    }
    return { x: event.pageX, y: event.pageY };
  }

  private clearPassportCropState(): void {
    this.handleCropPointerUp();
    if (this.passportPreviewUrl) {
      URL.revokeObjectURL(this.passportPreviewUrl);
      this.passportPreviewUrl = null;
    }
    this.passportSourceUrl = null;
    this.showCropUi = false;
    this.cropLeft = 0;
    this.cropTop = 0;
  }

  private loadImageAsync(src: string): Promise<HTMLImageElement> {
    return new Promise((resolve, reject) => {
      const image = new Image();
      image.onload = () => resolve(image);
      image.onerror = () => reject(new Error('Unable to load the selected image.'));
      image.src = src;
    });
  }

  private buildContext(
    raw: {
      classId?: string | null;
      classs?: string | null;
      className?: string | null;
      classLabel?: string | null;
      armId?: string | null;
      armName?: string | null;
      aarmName?: string | null;
      totalNoOfStudents?: number | string | null;
    } | null | undefined,
  ): StudentRegistrationContext | null {
    if (!raw) {
      return null;
    }

    const classId = String(raw.classId ?? '').trim();
    const armId = String(raw.armId ?? '').trim();
    if (!classId || !armId) {
      return null;
    }

    return {
      classId,
      classs: String(raw.classs ?? raw.className ?? '').trim(),
      classLabel: String(raw.classLabel ?? '').trim(),
      armId,
      armName: String(raw.armName ?? raw.aarmName ?? '').trim(),
      totalNoOfStudents: this.normalizeCount(raw.totalNoOfStudents),
    };
  }

  private loadDefaults(): void {
    if (!this.context) {
      return;
    }

    const params = new HttpParams()
      .set('classId', this.context.classId)
      .set('classLabel', this.context.classLabel)
      .set('totalNoOfStudents', String(this.context.totalNoOfStudents))
      .set('armId', this.context.armId)
      .set('armName', this.context.armName)
      .set('currentClass', this.context.classs);

    this.withRequestState(this.api.get('/student/create-context', { params }), {
      useLoading: true,
      clearError: true,
      errorMessage: 'Failed to load student registration defaults.',
      notifyOnError: 'none',
      rethrow: false,
      onError: error => console.error('Failed to load student registration defaults:', error),
    }).subscribe({
      next: (response: any) => {
        const payload = (response?.data ?? response) as StudentRegistrationDefaultsDto | undefined;
        if (!payload?.student) {
          this.errorMessage.set('Unable to load student registration defaults.');
          return;
        }

        this.form.patchValue({
          sNo: payload.student.sNo,
          idNo: payload.student.idNo,
          classArmId: payload.student.classArmId,
          currentClass: payload.student.currentClass,
          dateOfAdmsn: this.toDateInputValue(payload.student.dateOfAdmsn),
          dateOfBirth: this.toDateInputValue(payload.student.dateOfBirth),
        });

        this.context = {
          ...this.context!,
          totalNoOfStudents: payload.totalNoOfStudents,
        };
        this.studentRegistrationContext.setContext(this.context);

        if (this.isEditMode) {
          void this.loadStudentForEdit();
        }
      },
    });
  }

  private applyClassCategory(): void {
    syncClassCategory(this.form.controls.currentClass, this.form.controls.classCategory);
    this.cdr.markForCheck();
  }

  private resolveCurrentClass(rawValue: string): CurrentClass {
    const numericValue = Number(rawValue);
    if (Number.isInteger(numericValue) && CurrentClass[numericValue as CurrentClass] !== undefined) {
      return numericValue as CurrentClass;
    }

    const matchedValue = this.api.classNames.find(
      (option: { value: CurrentClass; label: string }) =>
        option.label.toLowerCase() === rawValue.trim().toLowerCase(),
    );

    return matchedValue?.value ?? CurrentClass.PreNursery;
  }

  private toDateInputValue(value: string): string {
    if (!value) {
      return '';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return value;
    }

    return date.toISOString().slice(0, 10);
  }

  private toDateValue(value: unknown): string {
    if (!value) {
      return '';
    }

    const date = new Date(value as string);
    return Number.isNaN(date.getTime()) ? String(value) : date.toISOString().slice(0, 10);
  }

  private normalizeCount(value: number | string | null | undefined): number {
    const count = Number(value ?? 0);
    return Number.isFinite(count) && count >= 0 ? count : 0;
  }

  private createOrUpdateStudentRequest(formData: FormData): Observable<any> {
    // No fallback here: the API has no other create/update student route, and retrying
    // elsewhere would replace the real error (e.g. a 500) with a misleading 404.
    if (this.isEditMode) {
      return this.api.updateWithFile(`/user-registration/update-student-details-byId/${this.studentId}`, formData);
    }

    return this.api.createWithFile('/user-registration/create-student-details', formData);
  }

  private getStudentDetailsRequest(studentId: string): Observable<any> {
    return this.api.getAnObj<any>(`/user-registration/get-student-details-byId/${studentId}`).pipe(
      catchError(error => {
        if (!this.shouldFallbackToLegacyStudentEndpoint(error)) {
          return throwError(() => error);
        }

        return this.api.getAnObj<any>(`/student/${studentId}`);
      }),
    );
  }

  private shouldFallbackToLegacyStudentEndpoint(error: unknown): boolean {
    const status = (error as { status?: number })?.status;
    return status === 404 || status === 500 || status === 502 || status === 503 || status === 504;
  }
}
