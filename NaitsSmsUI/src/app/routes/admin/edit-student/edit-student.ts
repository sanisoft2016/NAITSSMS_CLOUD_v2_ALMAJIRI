import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { HttpParams } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatIconModule } from '@angular/material/icon';
import { environment } from '@env/environment';
import { Observable, Subscription, throwError } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';

import { BaseComponent, CurrentClass, GenericService } from '@shared';
import {
  ArmName,
  ClassArmDto,
  ClassCategoryStatus,
  ClassDto,
  Gender,
  Religion,
  StudentRegistrationContext,
  StudentStatus,
} from '@shared/interfaces';
import { StudentRegistrationContextService } from '@shared/services';
import {
  SENIOR_CATEGORY_OPTIONS,
  isSeniorClass,
  seniorCategoryValidator,
  syncClassCategory,
} from '../../sessions/class-category';

@Component({
  selector: 'app-edit-student',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDatepickerModule, MatIconModule],
  templateUrl: './edit-student.html',
  styleUrl: './edit-student.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditStudent extends BaseComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(GenericService<any>);
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly studentRegistrationContext = inject(StudentRegistrationContextService);
  private readonly apiOrigin = new URL(environment.serverUrl).origin;

  readonly classNames = this.api.classNames;

  classes: ClassDto[] = [];
  arms: ClassArmDto[] = [];
  context: StudentRegistrationContext | null = null;
  contextMessage = '';
  isClassesLoading = false;
  isArmsLoading = false;
  passportFile: File | null = null;
  passportPreviewUrl = '';
  /** Full selected image shown under the crop frame. */
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
  private existingPassportPreviewUrl = '';
  /** Last status persisted on the server, used to revert the dropdown when "Previous" is cancelled or fails. */
  private savedStatus: StudentStatus = StudentStatus.Fresher;

  private readonly studentId = this.pickFirst(
    this.activatedRoute.snapshot.paramMap.get('id'),
    this.activatedRoute.snapshot.queryParamMap.get('studentId'),
    this.activatedRoute.snapshot.queryParamMap.get('editStudentId'),
    this.activatedRoute.snapshot.queryParamMap.get('id'),
  );

  private readonly returnView = this.resolveReturnView();

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
    startClass: ['', [Validators.required]],
    currentClass: [CurrentClass.PreNursery, [Validators.required]],
    selectedArmId: ['', [Validators.required]],
    sNo: [0],
    idNo: ['', [Validators.required, Validators.maxLength(15)]],
    firstName: ['', [Validators.required, Validators.maxLength(40)]],
    middleName: ['', [Validators.maxLength(40)]],
    surName: ['', [Validators.required, Validators.maxLength(40)]],
    dateOfBirth: ['', [Validators.required]],
    placeOfBirth: ['', [Validators.maxLength(50)]],
    gender: [Gender.Male, [Validators.required]],
    religion: [Religion.Christianity, [Validators.required]],
    classCategory: [ClassCategoryStatus.None, [Validators.required, seniorCategoryValidator]],
    status: [StudentStatus.Fresher, [Validators.required]],
    bloodGroup: ['', [Validators.maxLength(10)]],
    dateOfAdmsn: ['', [Validators.required]],
    transferedFromWhere: ['', [Validators.maxLength(150)]],
    studentMobileNo: ['', [Validators.maxLength(50)]],
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
    // Changing Current Class re-derives the category (locked for junior classes, chosen for SS).
    this.classCategorySync = this.form.controls.currentClass.valueChanges.subscribe(() => this.applyClassCategory());

    if (!this.studentId) {
      this.contextMessage = 'Student id is required to edit records.';
      return;
    }

    this.context = this.resolveContext();
    this.loadClasses();
  }

  ngOnDestroy(): void {
    this.classCategorySync?.unsubscribe();
    this.handleCropPointerUp();
    this.revokeObjectUrlPreviewIfNeeded();
  }

  get isSeniorClass(): boolean {
    return isSeniorClass(this.form.controls.currentClass.value);
  }

  get pageTitle(): string {
    return 'Edit Students Details';
  }

  /** Current Class options shown by the school's class label; the value stays the CurrentClass enum. */
  get currentClassOptions(): Array<{ value: CurrentClass; label: string }> {
    const options = this.classes.map(item => ({ value: Number(item.className) as CurrentClass, label: item.classLabel }));
    const current = Number(this.form.controls.currentClass.value) as CurrentClass;
    if (!options.some(option => option.value === current)) {
      const fallback = this.classNames.find(option => Number(option.value) === current);
      options.push({ value: current, label: fallback?.label ?? String(current) });
    }
    return options;
  }

  get currentPassportPreviewUrl(): string {
    return this.passportPreviewUrl || this.getDefaultPassportPreviewUrl();
  }

  goBack(): void {
    const targetPath = ['/admin', this.returnView];
    if (!this.context) {
      this.router.navigate(targetPath);
      return;
    }

    this.router.navigate(targetPath, {
      state: this.context,
      queryParams: this.studentRegistrationContext.toQueryParams(this.context),
    });
  }

  /** Legacy: the student's arm is resolved from Current Class + arm name, so the arm list follows Current Class. */
  onCurrentClassChange(): void {
    const previousArm = this.arms.find(item => item.id === this.form.controls.selectedArmId.value);
    const currentClass = this.findClassByCurrentClass(this.form.controls.currentClass.value);

    this.form.patchValue({ selectedArmId: '' });
    this.arms = [];

    if (!currentClass) {
      this.cdr.markForCheck();
      return;
    }

    this.loadArms(currentClass.id, '', previousArm?.armName);
  }

  onStatusChange(): void {
    const selectedStatus = Number(this.form.controls.status.value) as StudentStatus;
    if (selectedStatus !== StudentStatus.Previous) {
      return;
    }

    const confirmed = window.confirm(
      "Note: This is irreversible and will DELETE this student current term assessment record. Are you really sure you want to mark this student 'PREVIOUS'? If 'Yes', click 'Ok' to continue.",
    );
    if (!confirmed || !this.studentId) {
      this.form.patchValue({ status: this.savedStatus });
      this.cdr.markForCheck();
      return;
    }

    const params = new HttpParams().set('previousStatus', String(StudentStatus.Previous));
    this.api.create<{ message?: string }>(`/student/${this.studentId}/status/change-to-previous`, {}, { params }).subscribe({
      next: response => {
        this.savedStatus = StudentStatus.Previous;
        this.showAlert(response?.message ?? 'The Status has successfully been changed.', { type: 'success' });
        this.cdr.markForCheck();
      },
      error: error => {
        this.form.patchValue({ status: this.savedStatus });
        this.showAlert(this.getHttpErrorMessage(error, 'Unable to complete the process.'), { type: 'error' });
        this.cdr.markForCheck();
      },
    });
  }

  onPassportSelected(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.[0] ?? null;

    this.clearPassportCropState(false);
    this.passportFile = null;
    this.isPassportCropped = false;

    if (!file) {
      this.passportPreviewUrl = this.existingPassportPreviewUrl;
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
      this.passportPreviewUrl = this.existingPassportPreviewUrl;
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
      this.passportPreviewUrl = this.existingPassportPreviewUrl;
      this.cdr.markForCheck();
    };
    reader.readAsDataURL(file);
  }

  onPassportImageLoaded(event: Event): void {
    const img = event.target as HTMLImageElement | null;
    if (!img || !this.passportSourceUrl) {
      return;
    }

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
      this.revokeObjectUrlPreviewIfNeeded();
      this.passportPreviewUrl = URL.createObjectURL(blob);
      this.passportSourceUrl = null;
      this.showCropUi = false;
      this.isPassportCropped = true;
      this.cdr.markForCheck();
    } catch {
      this.showAlert('Failed to crop passport image.', { type: 'error' });
    }
  }

  onPassportPreviewError(): void {
    const fallbackUrl = this.getDefaultPassportPreviewUrl();
    if (this.passportPreviewUrl !== fallbackUrl) {
      this.passportPreviewUrl = fallbackUrl;
      this.cdr.markForCheck();
    }
  }

  async onSubmit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid || !this.studentId || !this.startSubmitting()) {
      return;
    }

    if (this.passportSourceUrl && this.showCropUi && !this.isPassportCropped) {
      this.stopSubmitting();
      this.showAlert('Please crop the image before submitting.', { type: 'warning' });
      return;
    }

    const value = this.form.getRawValue();
    const currentClass = this.findClassByCurrentClass(value.currentClass);
    const selectedArm = this.arms.find(item => item.id === value.selectedArmId);
    const startClass = value.startClass.trim();

    if (!startClass) {
      this.stopSubmitting();
      this.showAlert('You need to select the class.', { type: 'warning' });
      return;
    }

    if (!currentClass) {
      this.stopSubmitting();
      this.showAlert('You need to register the selected Current Class before adding Student to it.', { type: 'warning' });
      return;
    }

    if (!selectedArm) {
      this.stopSubmitting();
      this.showAlert('Please select class and arm to continue.', { type: 'warning' });
      return;
    }

    const formData = new FormData();
    formData.append('classId', currentClass.id);
    // The API stores classLabel as the student's StartClass (legacy model.StartClass).
    formData.append('classLabel', startClass);
    formData.append('armId', selectedArm.id);
    formData.append('classArmId', selectedArm.id);
    formData.append('armName', this.resolveArmNameForApi(selectedArm.armName));
    formData.append('currentClass', String(Number(value.currentClass)));
    formData.append('sNo', String(value.sNo));
    formData.append('idNo', value.idNo.trim());
    formData.append('firstName', value.firstName.trim());
    formData.append('middleName', value.middleName.trim());
    formData.append('surName', value.surName.trim());
    formData.append('dateOfBirth', this.formatDateForApi(value.dateOfBirth));
    formData.append('placeOfBirth', value.placeOfBirth.trim());
    formData.append('gender', String(Number(value.gender)));
    formData.append('religion', String(Number(value.religion)));
    formData.append('classCategory', String(Number(value.classCategory)));
    formData.append('status', String(Number(value.status)));
    formData.append('bloodGroup', value.bloodGroup.trim());
    formData.append('dateOfAdmsn', this.formatDateForApi(value.dateOfAdmsn));
    formData.append('transferedFromWhere', value.transferedFromWhere.trim());
    formData.append('studentMobileNo', value.studentMobileNo.trim());
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

    this.withRequestState(this.api.updateWithFile(`/user-registration/update-student-details-byId/${this.studentId}`, formData), {
      useLoading: false,
      clearError: true,
      errorMessage: 'Failed to update student. Please review the form and try again.',
      notifyOnError: 'none',
      rethrow: true,
    }).pipe(finalize(() => this.stopSubmitting())).subscribe({
      next: (response: any) => {
        const message = response?.message ?? 'Student updated successfully.';
        this.showAlert(message, { type: 'success' });
        this.goBack();
      },
      error: error => {
        const message =
          this.getHttpErrorMessage(error, '') ||
          (error as { error?: { message?: string } })?.error?.message ||
          'Failed to update student.';
        this.showAlert(message, { type: 'error' });
      },
    });
  }

  armNameLabel(value: ArmName | number | string | null | undefined): string {
    if (value === null || value === undefined || value === '') {
      return '';
    }

    if (typeof value === 'number') {
      return ArmName[value] ?? String(value);
    }

    const numericValue = Number(value);
    if (Number.isFinite(numericValue) && ArmName[numericValue] !== undefined) {
      return ArmName[numericValue];
    }

    return String(value);
  }

  private loadClasses(): void {
    this.isClassesLoading = true;
    this.api.get<ClassDto[]>('/ClassAndArm/classes').subscribe({
      next: response => {
        this.classes = this.extractArray<ClassDto>(response);
        this.isClassesLoading = false;
        void this.loadStudentDetails();
      },
      error: error => {
        this.isClassesLoading = false;
        this.errorMessage.set('Failed to load classes.');
        console.error('Failed to load classes:', error);
        this.cdr.markForCheck();
      },
    });
  }

  private async loadStudentDetails(): Promise<void> {
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
        }).subscribe({
          next: resolve,
          error: reject,
        });
      });

      const classId = String(student?.classArm?.classs?.id ?? student?.classArm?.classId ?? this.context?.classId ?? '').trim();
      const armId = String(student?.classArmId ?? this.context?.armId ?? '').trim();
      // Legacy EditStudentInfo: Current Class is the class of the student's arm; Start Class is the saved StartClass label.
      const armClass = this.classes.find(item => item.id === classId);
      const savedStartClass = String(student?.startClass ?? '').trim();
      const startClass = this.classes.find(item => item.classLabel.trim() === savedStartClass)?.classLabel ?? '';

      this.form.patchValue({
        startClass,
        currentClass: armClass ? (Number(armClass.className) as CurrentClass) : this.resolveCurrentClass(student?.currentClass),
        selectedArmId: armId,
        sNo: student?.sNo ?? 0,
        idNo: student?.idNo ?? '',
        firstName: student?.firstName ?? student?.user?.firstName ?? '',
        middleName: student?.middleName ?? student?.user?.middleName ?? '',
        surName: student?.surName ?? student?.user?.surName ?? '',
        dateOfBirth: this.toDateValue(student?.dateOfBirth),
        placeOfBirth: student?.placeOfBirth ?? '',
        gender: student?.gender ?? Gender.Male,
        religion: student?.religion ?? Religion.Christianity,
        classCategory: student?.classCategory ?? ClassCategoryStatus.None,
        status: student?.status ?? StudentStatus.Fresher,
        bloodGroup: student?.bloodGroup ?? '',
        dateOfAdmsn: this.toDateValue(student?.dateOfAdmsn),
        transferedFromWhere: student?.transferedFromWhere ?? '',
        studentMobileNo: student?.studentMobileNo ?? student?.user?.phoneNumber ?? '',
        guardiansName: student?.guardiansName ?? '',
        relationship: student?.relationship ?? '',
        guardianAddress: student?.guardianAddress ?? '',
        occupation: student?.occupation ?? '',
        mobileNumber: student?.mobileNumber ?? '',
        homeNumber: student?.homeNumber ?? '',
        emailAddress: student?.emailAddress ?? student?.user?.email ?? '',
        country: student?.country ?? '',
      });
      // A saved Science/Art/Commercial choice is kept; junior classes snap back to their category.
      this.applyClassCategory();
      this.savedStatus = this.form.controls.status.value;

      this.revokeObjectUrlPreviewIfNeeded();
      this.passportPreviewUrl = this.buildPassportPreviewUrl(student?.passport);
      this.existingPassportPreviewUrl = this.passportPreviewUrl;
      this.passportFile = null;
      this.passportSourceUrl = null;
      this.showCropUi = false;
      this.isPassportCropped = false;

      if (classId) {
        this.loadArms(classId, armId);
      }

      this.cdr.markForCheck();
    } catch (error) {
      this.showAlert(this.getHttpErrorMessage(error, 'Failed to load student details.'), { type: 'error' });
    }
  }

  private applyClassCategory(): void {
    syncClassCategory(this.form.controls.currentClass, this.form.controls.classCategory);
    this.cdr.markForCheck();
  }

  private loadArms(classId: string, selectedArmId = '', preferredArmName?: ClassArmDto['armName']): void {
    this.isArmsLoading = true;
    this.api.get<ClassArmDto[]>(`/ClassAndArm/classes-arms/${classId}`).subscribe({
      next: response => {
        this.arms = this.extractArray<ClassArmDto>(response);
        this.isArmsLoading = false;
        if (selectedArmId && this.arms.some(item => item.id === selectedArmId)) {
          this.form.patchValue({ selectedArmId });
        } else if (preferredArmName !== undefined && preferredArmName !== null) {
          // Keep the same arm name (e.g. "A") when Current Class changes, as legacy did.
          const sameArm = this.arms.find(item => this.armNameLabel(item.armName) === this.armNameLabel(preferredArmName));
          if (sameArm) {
            this.form.patchValue({ selectedArmId: sameArm.id });
          }
        }
        this.cdr.markForCheck();
      },
      error: error => {
        this.isArmsLoading = false;
        console.error('Failed to load arms:', error);
        this.showAlert('Failed to load class arms.', { type: 'error' });
        this.cdr.markForCheck();
      },
    });
  }

  private getStudentDetailsRequest(studentId: string): Observable<any> {
    return this.api.getAnObj<any>(`/user-registration/get-student-details-byId/${studentId}`).pipe(
      catchError(error => this.api.getAnObj<any>(`/student/${studentId}`).pipe(
        catchError(() => throwError(() => error)),
      )),
    );
  }

  private resolveContext(): StudentRegistrationContext | null {
    const state = history.state as Partial<StudentRegistrationContext> | undefined;
    const storage = this.studentRegistrationContext.getContext();

    const classId = this.pickFirst(state?.classId, this.activatedRoute.snapshot.queryParamMap.get('classId'), storage?.classId);
    const armId = this.pickFirst(state?.armId, this.activatedRoute.snapshot.queryParamMap.get('armId'), storage?.armId);

    if (!classId || !armId) {
      return null;
    }

    const context: StudentRegistrationContext = {
      classId,
      classs: this.pickFirst(state?.classs, this.activatedRoute.snapshot.queryParamMap.get('classs'), storage?.classs),
      classLabel: this.pickFirst(state?.classLabel, this.activatedRoute.snapshot.queryParamMap.get('classLabel'), storage?.classLabel),
      armId,
      armName: this.pickFirst(state?.armName, this.activatedRoute.snapshot.queryParamMap.get('armName'), storage?.armName),
      totalNoOfStudents: this.pickNumber(state?.totalNoOfStudents, this.activatedRoute.snapshot.queryParamMap.get('totalNoOfStudents'), storage?.totalNoOfStudents),
    };

    this.studentRegistrationContext.setContext(context);
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

  private toDateValue(value: unknown): string {
    if (!value) {
      return '';
    }

    const date = new Date(value as string);
    return Number.isNaN(date.getTime()) ? String(value) : date.toISOString().slice(0, 10);
  }

  private buildPassportPreviewUrl(passport: string | null | undefined): string {
    const fileName = String(passport ?? '').trim();
    if (!fileName) {
      return '';
    }

    return `${this.apiOrigin}/Passport/${encodeURIComponent(fileName)}`;
  }

  private getDefaultPassportPreviewUrl(): string {
    return `${this.apiOrigin}/Passport/no-passport.jpg`;
  }

  private revokeObjectUrlPreviewIfNeeded(): void {
    if (this.passportPreviewUrl.startsWith('blob:')) {
      URL.revokeObjectURL(this.passportPreviewUrl);
    }
  }

  private clearPassportCropState(restoreExisting = true): void {
    this.handleCropPointerUp();
    this.revokeObjectUrlPreviewIfNeeded();
    this.passportSourceUrl = null;
    this.showCropUi = false;
    this.cropLeft = 0;
    this.cropTop = 0;
    this.passportPreviewUrl = restoreExisting ? this.existingPassportPreviewUrl : '';
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

  private findClassByCurrentClass(value: unknown): ClassDto | undefined {
    const numericValue = Number(value);
    return this.classes.find(item => Number(item.className) === numericValue);
  }

  private resolveCurrentClass(rawValue: unknown): CurrentClass {
    const numericValue = Number(rawValue);
    if (Number.isInteger(numericValue) && CurrentClass[numericValue as CurrentClass] !== undefined) {
      return numericValue as CurrentClass;
    }

    const normalizedValue = String(rawValue ?? '').trim().toLowerCase();
    const matched = this.classNames.find(option => option.label.toLowerCase() === normalizedValue);
    return matched?.value ?? CurrentClass.PreNursery;
  }

  private loadImageAsync(src: string): Promise<HTMLImageElement> {
    return new Promise((resolve, reject) => {
      const image = new Image();
      image.onload = () => resolve(image);
      image.onerror = () => reject(new Error('Unable to load the selected image.'));
      image.src = src;
    });
  }

  private extractArray<T>(response: unknown): T[] {
    if (Array.isArray(response)) {
      return response as T[];
    }

    const wrapped = response as { data?: unknown } | null;
    if (Array.isArray(wrapped?.data)) {
      return wrapped.data as T[];
    }

    return [];
  }

  private pickFirst(...values: Array<string | number | null | undefined>): string {
    for (const value of values) {
      const normalized = String(value ?? '').trim();
      if (normalized) {
        return normalized;
      }
    }

    return '';
  }

  private pickNumber(...values: Array<string | number | null | undefined>): number {
    for (const value of values) {
      const numberValue = Number(value ?? NaN);
      if (Number.isFinite(numberValue)) {
        return numberValue;
      }
    }

    return 0;
  }

  private resolveReturnView(): 'student-home-page' | 'student-reg-index' {
    const raw = this.pickFirst(
      typeof history.state?.returnView === 'string' ? history.state.returnView : undefined,
      this.activatedRoute.snapshot.queryParamMap.get('returnView'),
    );

    return raw === 'student-reg-index' ? 'student-reg-index' : 'student-home-page';
  }

}
