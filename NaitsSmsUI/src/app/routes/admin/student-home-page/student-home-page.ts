import { HttpErrorResponse, HttpParams } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { environment } from '@env/environment';
import {
  printDomElement,
  studentDetailsPrintStyles,
  studentListPrintStyles,
} from '@shared/utils';
import { CurrentClass, GenericService, StudentRegistrationContextService } from '@shared/services';
import {
  AccessStatus,
  ArmName,
  ClassArmDto,
  ClassCategoryStatus,
  ClassDto,
  Gender,
  Religion,
  StudentRegistrationContext,
  StudentStatus,
} from '@shared/interfaces';

interface StudentUserRecord {
  firstName?: string;
  middleName?: string;
  surName?: string;
  userName?: string;
  fullName?: string;
  phoneNumber?: string;
  email?: string;
  accessStatus?: AccessStatus | number | string;
}

interface StudentClassRecord {
  id?: string;
  className?: CurrentClass;
  classLabel?: string;
}

interface StudentClassArmRecord {
  id?: string;
  armName?: ArmName | number | string;
  classs?: StudentClassRecord;
}

interface StudentRecord {
  id: string;
  userId?: string;
  sNo?: number;
  idNo?: string;
  firstName?: string;
  middleName?: string;
  surName?: string;
  fullName?: string;
  gender?: Gender;
  religion?: Religion;
  currentClass?: CurrentClass;
  classArmId?: string;
  classArm?: StudentClassArmRecord;
  status?: StudentStatus;
  classCategory?: ClassCategoryStatus | number | string;
  schoolInfoId?: string;
  passport?: string | null;
  dateOfBirth?: string;
  dateOfAdmsn?: string;
  placeOfBirth?: string;
  bloodGroup?: string;
  transferedFromWhere?: string;
  studentMobileNo?: string;
  guardiansName?: string;
  relationship?: string;
  guardianAddress?: string;
  occupation?: string;
  mobileNumber?: string;
  homeNumber?: string;
  emailAddress?: string;
  country?: string;
  startClass?: string;
  user?: StudentUserRecord;
}

interface PrintableStudent {
  sNo: number;
  studentName: string;
  admisnNo: string;
  classStatus: string;
  space: string;
}

@Component({
  selector: 'app-student-home-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './student-home-page.html',
  styleUrl: './student-home-page.scss',
})
export class StudentHomePage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(GenericService<StudentRecord>);
  private readonly studentRegistrationContext = inject(StudentRegistrationContextService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly apiOrigin = new URL(environment.serverUrl).origin;

  classId = '';
  classs = '';
  classLabel = '';
  armId = '';
  armName = '';
  totalNoOfStudents = 0;

  currentClass = '';
  currentArm = '';
  searchParameter = '';

  classes: ClassDto[] = [];
  arms: ClassArmDto[] = [];
  students: StudentRecord[] = [];
  filteredStudents: StudentRecord[] = [];
  selectedStudent: StudentRecord | null = null;
  printData: PrintableStudent[] = [];
  printTitle = 'Student List';
  studentPassportPreviewUrl = '';

  isLoading = false;
  isLoadingWithoutPassport = false;
  loadError = '';
  emptyStateMessage = '';
  showDetailsModal = false;
  isDetailsLoading = false;
  detailsLoadError = '';
  isActionBusy = false;
  showChangePasswordPanel = false;
  newPassword = '';
  isPrintPreview = false;

  readonly StudentStatus = StudentStatus;
  readonly AccessStatus = AccessStatus;

  ngOnInit(): void {
    this.loadContext();
    this.loadClasses();
    this.loadStudentRecentStudents();
  }

  private loadContext(): void {
    const state = (history.state ?? {}) as Partial<StudentRegistrationContext> & {
      clasName?: string;
      className?: string;
    };
    const stored = this.studentRegistrationContext.getContext();

    this.classId = this.pickFirst(state.classId, this.route.snapshot.queryParamMap.get('classId'), stored?.classId);
    this.classs = this.pickFirst(
      state.classs,
      state.className,
      state.clasName,
      this.route.snapshot.queryParamMap.get('classs'),
      this.route.snapshot.queryParamMap.get('className'),
      this.route.snapshot.queryParamMap.get('clasName'),
      stored?.classs,
    );
    this.classLabel = this.pickFirst(
      state.classLabel,
      this.route.snapshot.queryParamMap.get('classLabel'),
      stored?.classLabel,
    );
    this.armId = this.pickFirst(state.armId, this.route.snapshot.queryParamMap.get('armId'), stored?.armId);
    this.armName = this.pickFirst(state.armName, this.route.snapshot.queryParamMap.get('armName'), stored?.armName);
    this.totalNoOfStudents = this.pickNumber(
      state.totalNoOfStudents,
      this.route.snapshot.queryParamMap.get('totalNoOfStudents'),
      stored?.totalNoOfStudents,
    );

    this.currentClass = this.classId;
    this.currentArm = this.armId;
  }

  getEnumStringValue(arm: ArmName | number | string): string {
    if (typeof arm === 'string') {
      const trimmed = arm.trim();
      const numeric = Number(trimmed);
      if (Number.isFinite(numeric) && ArmName[numeric] !== undefined) {
        return ArmName[numeric];
      }
      return trimmed || '-';
    }

    return ArmName[arm] ?? String(arm);
  }
  loadClasses(): void {
    this.api.get<ClassDto[]>('/ClassAndArm/classes').subscribe({
      next: response => {
        this.classes = this.extractArray<ClassDto>(response).map(item => this.normalizeClass(item));
        if (this.currentClass) {
          this.syncSelectedClassMeta();
          this.loadArms(this.currentClass, this.pickFirst(this.currentArm, this.armId));
        }
        this.cdr.markForCheck();
      },
      error: (err: unknown) => {
        console.error('Error loading classes:', err);
        this.loadError = 'Failed to load classes. Please try again.';
        this.cdr.markForCheck();
      },
    });
  }

  onClassChange(classId: string = this.currentClass): void {
    const preferredArmId = this.pickFirst(this.currentArm, this.armId);
    this.currentClass = classId ?? '';
    this.syncSelectedClassMeta();
    this.arms = [];
    this.students = [];
    this.filteredStudents = [];
    this.totalNoOfStudents = 0;
    this.emptyStateMessage = '';
    this.loadError = '';

    if (!this.currentClass) {
      this.currentArm = '';
      this.armId = '';
      this.armName = '';
      this.cdr.markForCheck();
      return;
    }

    this.loadArms(this.currentClass, preferredArmId);
  }

  onArmChange(armId: string = this.currentArm): void {
    this.currentArm = armId ?? '';
    this.armId = this.currentArm;
    this.syncSelectedArmMeta();

    if (!this.currentArm) {
      this.students = [];
      this.filteredStudents = [];
      this.totalNoOfStudents = 0;
      this.emptyStateMessage = '';
      this.cdr.markForCheck();
      return;
    }

    this.loadStudentsByArm(this.currentArm);
  }

  searchStudent(): void {
    const parameter = this.searchParameter.trim();
    if (!parameter) {
      alert('Search Parameter must be provided to continue.');
      return;
    }

    this.isLoading = true;
    this.loadError = '';
    this.emptyStateMessage = '';

    const params = new HttpParams().set('searchParameter', parameter);
    this.api.get<StudentRecord[]>('/student/by-parameters', { params }).subscribe({
      next: response => {
        const results = this.sortStudents(this.extractArray<StudentRecord>(response));
        this.filteredStudents = results;
        this.emptyStateMessage = results.length === 0
          ? 'No students found for the supplied search parameter.'
          : '';
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: (err: unknown) => {
        console.error('Error searching students:', err);
        this.applyLocalSearch(parameter);
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  getStudentsWithoutPassport(): void {
    if (this.isLoadingWithoutPassport) {
      return;
    }

    const className = this.selectedClassLabel;
    if (!className) {
      alert('Please select a class first.');
      return;
    }

    this.isLoadingWithoutPassport = true;
    this.isLoading = true;
    this.loadError = '';
    this.emptyStateMessage = '';
    this.selectedStudent = null;

    const params = new HttpParams().set('className', className);
    this.api.get<StudentRecord[]>('/student/without-passport', { params }).subscribe({
      next: response => {
        const results = this.sortStudents(this.extractArray<StudentRecord>(response));
        this.filteredStudents = results;
        this.emptyStateMessage = results.length === 0
          ? `No students without passport found in ${className}.`
          : '';
        this.isLoadingWithoutPassport = false;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: (err: unknown) => {
        console.error('Error loading students without passport:', err);
        this.loadError = 'Failed to load students without passport. Please try again.';
        this.emptyStateMessage = '';
        this.isLoadingWithoutPassport = false;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  printPreview(): void {
    if (this.filteredStudents.length === 0) {
      alert('There are no students available for print preview.');
      return;
    }

    let serialNumber = 0;
    this.printData = this.filteredStudents
      .filter(student => student.status !== StudentStatus.Previous)
      .map(student => {
        serialNumber += 1;
        return {
          sNo: serialNumber,
          studentName: this.getStudentFullName(student),
          admisnNo: student.idNo ?? '',
          classStatus: this.getStudentClassCategory(student),
          space: '',
        };
      });

    this.printTitle = this.classLabel && this.armName
      ? `${this.classLabel} ${this.armName}`
      : 'Student List';

    this.isPrintPreview = true;
    this.showDetailsModal = true;
    this.selectedStudent = null;
    this.showChangePasswordPanel = false;
    this.detailsLoadError = '';
    this.isDetailsLoading = false;
    this.cdr.markForCheck();
  }

  printPage(): void {
    printDomElement({
      contentElementId: 'printResult',
      title: this.printTitle || 'Student List',
      heading: this.printTitle || 'Student List',
      styles: studentListPrintStyles(),
    });
  }

  viewDetails(studentId: string | null | undefined): void {
    const resolvedStudentId = this.pickFirst(studentId);

    this.showDetailsModal = true;
    this.isPrintPreview = false;
    this.selectedStudent = null;
    this.studentPassportPreviewUrl = '';
    this.isDetailsLoading = true;
    this.detailsLoadError = '';
    this.showChangePasswordPanel = false;
    this.newPassword = '';

    if (!resolvedStudentId) {
      this.isDetailsLoading = false;
      this.detailsLoadError = 'Student details are not available for this record.';
      this.cdr.markForCheck();
      return;
    }

    this.api.getAnObj<StudentRecord>(`/user-registration/get-student-details-byId/${encodeURIComponent(resolvedStudentId)}`).subscribe({
      next: response => {
        const details = this.extractItem<StudentRecord>(response);

        if (!details || !this.pickFirst(details.id, details.userId)) {
          this.detailsLoadError = 'Student details are not available for this record.';
          this.isDetailsLoading = false;
          this.cdr.markForCheck();
          return;
        }

        this.selectedStudent = details;
        this.studentPassportPreviewUrl = this.buildStudentPassportPreviewUrl(details.passport);
        this.isDetailsLoading = false;
        this.cdr.markForCheck();
      },
      error: (err: unknown) => {
        console.error('Error loading student details:', err);
        this.detailsLoadError = 'Failed to load student details. Please try again.';
        this.isDetailsLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  closeDetailsModal(): void {
    this.showDetailsModal = false;
    this.isPrintPreview = false;
    this.selectedStudent = null;
    this.isDetailsLoading = false;
    this.detailsLoadError = '';
    this.showChangePasswordPanel = false;
    this.newPassword = '';
    this.printData = [];
    this.studentPassportPreviewUrl = '';
  }

  editRecord(): void {
    if (!this.selectedStudent?.id) {
      return;
    }

    const studentId = this.selectedStudent.id;
    const context = this.buildCurrentContext();
    if (context) {
      this.studentRegistrationContext.setContext(context);
    }

    this.closeDetailsModal();
    // Legacy-style URL (/EditStudentInfo/{id}): only the student id is visible; the rest travels in history state.
    this.router.navigate(['/admin/edit-student', studentId], {
      state: { ...(context ?? {}), returnView: 'student-home-page' },
    });
  }

  deleteRecord(): void {
    if (!this.selectedStudent?.id) {
      return;
    }

    const confirmed = confirm(
      `Are you sure you want to DELETE "${this.fullName}"? If 'Yes', click 'Ok' to continue.`
    );

    if (!confirmed) {
      return;
    }

    this.isActionBusy = true;
    this.cdr.markForCheck();

    // Legacy: POST Registration/DeleteStudent?id=...&userId=...
    const params = new HttpParams().set('userId', this.selectedStudent.userId ?? this.selectedStudent.id);
    this.api.delete(`/student/${this.selectedStudent.id}`, { params }).subscribe({
      next: () => {
        this.isActionBusy = false;
        alert('Record successfully Deleted.');
        this.closeDetailsModal();
        if (this.armId) {
          this.loadStudentsByArm(this.armId);
        }
      },
      error: (err: unknown) => {
        console.error('Error deleting student:', err);
        this.isActionBusy = false;
        const message = (err as { error?: { message?: string } })?.error?.message;
        alert(message || 'Failed! The student has one or more important record(s) and therefore we cannot delete it.');
        this.cdr.markForCheck();
      },
    });
  }

  loadPasswordPage(): void {
    this.showChangePasswordPanel = !this.showChangePasswordPanel;
    if (!this.showChangePasswordPanel) {
      this.newPassword = '';
    }
  }

  submitChangePassword(): void {
    const password = this.newPassword.trim();
    if (!password) {
      alert('Please enter a new password.');
      return;
    }

    const userId = this.selectedStudent?.userId;
    if (!userId) {
      alert('Unable to resolve user ID.');
      return;
    }

    this.isActionBusy = true;
    this.cdr.markForCheck();

    this.api.create('/user/post-change-password', { userId, newPassword: password }).subscribe({
      next: () => {
        this.isActionBusy = false;
        this.showChangePasswordPanel = false;
        this.newPassword = '';
        alert('Password changed successfully.');
        this.cdr.markForCheck();
      },
      error: (err: unknown) => {
        console.error('Error changing password:', err);
        this.isActionBusy = false;
        alert('Failed to change password. Please try again.');
        this.cdr.markForCheck();
      },
    });
  }

  enableStudentAccount(): void {
    if (this.isAccountDisabled()) {
      this.toggleAccountStatus('enable');
    }
  }

  disableStudentAccount(): void {
    if (!this.isAccountDisabled()) {
      this.toggleAccountStatus('disable');
    }
  }

  printStudentDetails(): void {
    if (!this.selectedStudent) {
      alert('Student details are not available for printing.');
      return;
    }

    printDomElement({
      contentElementId: 'studentDetails',
      title: 'Student Details',
      heading: 'Student Details',
      styles: studentDetailsPrintStyles(),
      mediaOrigin: this.apiOrigin,
    });
  }

  goBack(): void {
    if (!this.classId) {
      this.router.navigate(['/admin/class-reg-index']);
      return;
    }

    this.router.navigate(['/admin/class-arm-index'], {
      queryParams: {
        classId: this.classId,
        classLabel: this.classLabel,
        className: this.classs,
      },
    });
  }

  addStudent(): void {
    this.syncSelectedClassMeta();
    this.syncSelectedArmMeta();

    const context =
      this.buildCurrentContext() ??
      this.buildContextFromSelections() ??
      this.studentRegistrationContext.getContext();

    if (context?.classId && context?.armId) {
      this.studentRegistrationContext.setContext(context);
    }

    this.router.navigate(['/auth/register-student2nd'], {
      state: context ?? undefined,
      queryParams: {
        ...(context ? this.studentRegistrationContext.toQueryParams(context) : {}),
        returnView: 'student-home-page',
      },
    });
  }

  getStudentFullName(student: StudentRecord): string {
    return this.resolveStudentFullName(student);
  }

  getStudentUserName(student: StudentRecord): string {
    return student.user?.userName?.trim() || '-';
  }

  getStudentClassCategory(student: StudentRecord): string {
    // Prefer the student's CurrentClass; nested classs.className can be absent/defaulted to 0.
    const currentClass = this.resolveCurrentClassValue(
      student.currentClass ?? student.classArm?.classs?.className,
    );

    if (
      currentClass === CurrentClass.Ss1 ||
      currentClass === CurrentClass.Ss2 ||
      currentClass === CurrentClass.Ss3
    ) {
      return this.formatClassCategory(student.classCategory);
    }

    return this.getClassCategoryStatus(currentClass);
  }

  getClassCategoryStatus(className?: CurrentClass | number | string | null): string {
    const classNum = this.resolveCurrentClassValue(className);
    if (classNum === null) {
      return '-';
    }

    if (classNum === CurrentClass.PreNursery) {
      return 'PreNursery';
    }
    if (classNum === CurrentClass.Nursery1 || classNum === CurrentClass.Nursery2) {
      return 'Nursery';
    }
    if (classNum >= CurrentClass.Primary1 && classNum <= CurrentClass.Primary6) {
      return 'Primary';
    }
    if (classNum >= CurrentClass.Jss1 && classNum <= CurrentClass.Jss3) {
      return 'Jss';
    }
    if (
      classNum === CurrentClass.Ss1 ||
      classNum === CurrentClass.Ss2 ||
      classNum === CurrentClass.Ss3
    ) {
      return '-';
    }
    return 'PlayClass';
  }

  private resolveCurrentClassValue(
    className?: CurrentClass | number | string | null,
  ): CurrentClass | null {
    if (className === undefined || className === null || className === '') {
      return null;
    }

    if (typeof className === 'number' && Number.isFinite(className)) {
      return className as CurrentClass;
    }

    const asNumber = Number(className);
    if (Number.isFinite(asNumber) && CurrentClass[asNumber] !== undefined) {
      return asNumber as CurrentClass;
    }

    if (typeof className === 'string') {
      const byName = (CurrentClass as unknown as Record<string, number>)[className];
      if (typeof byName === 'number') {
        return byName as CurrentClass;
      }
    }

    return null;
  }

  getStatusLabel(status?: StudentStatus | number | string): string {
    if (status === undefined || status === null || status === '') {
      return '-';
    }

    if (typeof status === 'number') {
      return StudentStatus[status] ?? String(status);
    }

    return String(status);
  }

  isAccountDisabled(): boolean {
    const accessStatus = this.selectedStudent?.user?.accessStatus;
    if (typeof accessStatus === 'number') {
      return accessStatus === AccessStatus.Disable;
    }

    const normalized = String(accessStatus ?? '').trim().toLowerCase();
    return normalized === 'disable' || normalized === 'disabled' || normalized === String(AccessStatus.Disable);
  }

  get fullName(): string {
    return this.selectedStudent ? this.resolveStudentFullName(this.selectedStudent) : '-';
  }

  get firstName(): string {
    return this.selectedStudent?.user?.firstName?.trim()
      || this.selectedStudent?.firstName?.trim()
      || '-';
  }

  get middleName(): string {
    return this.selectedStudent?.user?.middleName?.trim()
      || this.selectedStudent?.middleName?.trim()
      || '-';
  }

  get surName(): string {
    return this.selectedStudent?.user?.surName?.trim()
      || this.selectedStudent?.surName?.trim()
      || '-';
  }

  get detailsClassDisplay(): string {
    const detailsClassLabel = this.selectedStudent?.classArm?.classs?.classLabel || this.classLabel || '-';
    const detailsArmName = this.resolveArmName(this.selectedStudent?.classArm?.armName) || this.armName || '';
    // Collapse padded/non-breaking whitespace so the value sits one space after the label, like the other fields.
    return `${detailsClassLabel} ${detailsArmName}`.replace(/\s+/g, ' ').trim();
  }

  get religionLabel(): string {
    if (this.selectedStudent?.religion === Religion.Islam) {
      return 'Islam';
    }
    if (this.selectedStudent?.religion === Religion.Christianity) {
      return 'Christianity';
    }
    return '-';
  }

  get studentPassportUrl(): string {
    const passport = this.selectedStudent?.passport?.trim();
    return passport ? this.studentPassportPreviewUrl : '';
  }

  onPassportPreviewError(): void {
    this.studentPassportPreviewUrl = '';
    this.cdr.markForCheck();
  }

  formatDate(value?: string): string {
    if (!value) {
      return '-';
    }

    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString();
  }

  private loadArms(classId: string, preferredArmId = ''): void {
    this.api.get<ClassArmDto[]>(`/ClassAndArm/classes-arms/${classId}`).subscribe({
      next: response => {
        this.arms = this.sortArms(
          this.extractArray<ClassArmDto>(response).map(item => this.normalizeClassArm(item)),
        );
        this.applyArmSelectionAfterLoad(preferredArmId);
        this.cdr.markForCheck();
      },
      error: (err: unknown) => {
        console.error('Error loading arms:', err);
        this.arms = [];
        this.currentArm = '';
        this.armId = '';
        this.armName = '';
        this.loadError = 'Failed to load class arms. Please try again.';
        this.cdr.markForCheck();
      },
    });
  }

  private applyArmSelectionAfterLoad(preferredArmId: string): void {
    const normalizedPreferred = String(preferredArmId ?? '').trim();
    const matchingArm = normalizedPreferred
      ? this.arms.find(item => this.sameId(item.id, normalizedPreferred))
      : undefined;

    if (matchingArm) {
      this.currentArm = matchingArm.id;
      this.syncSelectedArmMeta();
      this.loadStudentsByArm(this.currentArm);
      return;
    }

    if (this.arms.length > 0) {
      const lowestArm = this.findLowestValueArm(this.arms);
      this.currentArm = lowestArm.id;
      this.syncSelectedArmMeta();
      this.loadStudentsByArm(this.currentArm);
      return;
    }

    this.currentArm = '';
    this.armId = '';
    this.armName = '';
  }

  private sortArms(arms: ClassArmDto[]): ClassArmDto[] {
    return [...arms].sort((left, right) => this.compareArmsByValue(left, right));
  }

  private findLowestValueArm(arms: ClassArmDto[]): ClassArmDto {
    return this.sortArms(arms)[0];
  }

  private compareArmsByValue(left: ClassArmDto, right: ClassArmDto): number {
    const armDiff = this.armNameSortValue(left.armName) - this.armNameSortValue(right.armName);
    if (armDiff !== 0) {
      return armDiff;
    }

    const sNoDiff = (left.sNo ?? 0) - (right.sNo ?? 0);
    if (sNoDiff !== 0) {
      return sNoDiff;
    }

    return String(left.id ?? '').localeCompare(String(right.id ?? ''));
  }

  private armNameSortValue(armName?: ArmName | number | string): number {
    if (typeof armName === 'number' && Number.isFinite(armName)) {
      return armName;
    }

    const numeric = Number(armName);
    if (Number.isFinite(numeric) && String(armName ?? '').trim() !== '') {
      return numeric;
    }

    const normalized = String(armName ?? '').trim();
    const enumValue = ArmName[normalized as keyof typeof ArmName];
    return typeof enumValue === 'number' ? enumValue : Number.MAX_SAFE_INTEGER;
  }

  private loadStudentsByArm(armId: string): void {
    this.isLoading = true;
    this.loadError = '';
    this.emptyStateMessage = '';

    this.api.get<StudentRecord[]>(`/student/get-students-by-armId/${armId}`).subscribe({
      next: response => {
        const list = this.sortStudents(this.extractArray<StudentRecord>(response));
        this.students = list;
        this.filteredStudents = [...list];
        this.totalNoOfStudents = list.length;
        this.emptyStateMessage = list.length === 0 && !!this.currentArm
          ? 'Fresh Class! Start Adding Student'
          : '';
        this.persistContext();
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: (err: unknown) => {
        console.error('Error loading students:', err);
        this.students = [];
        this.filteredStudents = [];
        this.totalNoOfStudents = 0;
        this.loadError = 'Failed to load students. Please try again.';
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }


  private loadStudentRecentStudents(): void {
    this.isLoading = true;
    this.loadError = '';
    this.emptyStateMessage = '';

    this.api.get<StudentRecord[]>(`/student/recent`).subscribe({
      next: response => {
        const list = this.sortStudents(this.extractArray<StudentRecord>(response));
        this.students = list;
        this.filteredStudents = [...list];
        this.totalNoOfStudents = list.length;
        this.emptyStateMessage = list.length === 0 && !!this.currentArm
          ? 'Fresh Class! Start Adding Student'
          : '';
        this.persistContext();
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: (err: unknown) => {
        console.error('Error loading students:', err);
        this.students = [];
        this.filteredStudents = [];
        this.totalNoOfStudents = 0;
        this.loadError = 'Failed to load students. Please try again.';
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  private buildCurrentContext(): StudentRegistrationContext | null {
    if (!this.classId || !this.armId) {
      return null;
    }

    return {
      classId: this.classId,
      classs: this.classs,
      classLabel: this.classLabel,
      armId: this.armId,
      armName: this.armName,
      totalNoOfStudents: this.totalNoOfStudents,
    };
  }

  private buildContextFromSelections(): StudentRegistrationContext | null {
    const classId = this.pickFirst(this.currentClass, this.classId);
    const armId = this.pickFirst(this.currentArm, this.armId);

    if (!classId || !armId) {
      return null;
    }

    const selectedClass = this.findClassById(classId);
    const selectedArm = this.findClassArmById(armId);

    return {
      classId,
      classs: this.pickFirst(
        this.classs,
        selectedClass ? this.resolveClassName(selectedClass.className) : '',
      ),
      classLabel: this.pickFirst(this.classLabel, selectedClass?.classLabel),
      armId,
      armName: this.pickFirst(
        this.armName,
        selectedArm ? this.resolveArmName(selectedArm.armName) : '',
      ),
      totalNoOfStudents: this.totalNoOfStudents,
    };
  }

  private persistContext(): void {
    const context = this.buildCurrentContext();
    if (context) {
      this.studentRegistrationContext.setContext(context);
    }
  }

  private applyLocalSearch(parameter: string): void {
    const normalized = parameter.trim().toLowerCase();
    this.filteredStudents = this.sortStudents(
      this.students.filter(student => {
        const fullName = this.getStudentFullName(student).toLowerCase();
        const userName = this.getStudentUserName(student).toLowerCase();
        const idNo = String(student.idNo ?? '').toLowerCase();
        return fullName.includes(normalized) || userName.includes(normalized) || idNo.includes(normalized);
      }),
    );

    this.emptyStateMessage = this.filteredStudents.length === 0
      ? 'No students found for the supplied search parameter.'
      : '';
  }

  private applyLocalWithoutPassportFallback(): void {
    this.filteredStudents = this.sortStudents(
      this.students.filter(student => !String(student.passport ?? '').trim()),
    );

    this.emptyStateMessage = this.filteredStudents.length === 0
      ? 'No students without passport found.'
      : '';
  }

  private toggleAccountStatus(action: 'enable' | 'disable'): void {
    if (!this.selectedStudent?.userId) {
      alert('Unable to resolve user ID.');
      return;
    }

    const confirmed = confirm(
      `Are you sure you want to ${action.toUpperCase()} the account of "${this.fullName}"?`
    );
    if (!confirmed) {
      return;
    }

    this.isActionBusy = true;
    this.cdr.markForCheck();

    this.api.create(`/student/${this.selectedStudent.userId}/${action}`, {}).subscribe({
      next: () => {
        this.isActionBusy = false;
        if (this.selectedStudent?.user) {
          this.selectedStudent.user.accessStatus = action === 'enable'
            ? AccessStatus.Active
            : AccessStatus.Disable;
        }
        alert(`Student account ${action}d successfully.`);
        this.cdr.markForCheck();
      },
      error: (err: unknown) => {
        console.error(`Error ${action}ing student account:`, err);
        this.isActionBusy = false;

        const status = err instanceof HttpErrorResponse ? err.status : undefined;
        if (status === 404 || status === 405) {
          alert(`Student account ${action} endpoint is not available on the current API.`);
        } else {
          alert(`Failed to ${action} the student account. Please try again.`);
        }

        this.cdr.markForCheck();
      },
    });
  }

  private syncSelectedClassMeta(): void {
    this.classId = this.currentClass;

    const selectedClass = this.findClassById(this.currentClass);
    if (!selectedClass) {
      return;
    }

    this.classLabel = selectedClass.classLabel ?? this.classLabel;
    this.classs = this.resolveClassName(selectedClass.className);
  }

  private syncSelectedArmMeta(): void {
    this.armId = this.currentArm;

    const selectedArm = this.findClassArmById(this.currentArm);
    if (!selectedArm) {
      return;
    }

    this.armName = this.resolveArmName(selectedArm.armName);
  }

  private resolveStudentFullName(student: StudentRecord): string {
    const userFullName = student.user?.fullName?.trim();
    if (userFullName) {
      return userFullName;
    }

    const fullName = student.fullName?.trim();
    if (fullName) {
      return fullName;
    }

    return [student.firstName, student.middleName, student.surName]
      .map(value => value?.trim())
      .filter((value): value is string => !!value)
      .join(' ') || '-';
  }

  private resolveClassName(className?: CurrentClass): string {
    if (className === undefined || className === null) {
      return this.classs;
    }

    return CurrentClass[className] ?? String(className);
  }

  private resolveArmName(armName?: ArmName | number | string): string {
    if (armName === undefined || armName === null) {
      return '';
    }

    if (typeof armName === 'number') {
      return ArmName[armName] ?? String(armName);
    }

    return String(armName);
  }

  private formatClassCategory(value?: ClassCategoryStatus | number | string): string {
    if (value === undefined || value === null || value === '') {
      return '-';
    }

    if (typeof value === 'number') {
      return ClassCategoryStatus[value] ?? String(value);
    }

    const numericValue = Number(value);
    if (Number.isFinite(numericValue) && ClassCategoryStatus[numericValue] !== undefined) {
      return ClassCategoryStatus[numericValue];
    }

    return String(value);
  }

  private sortStudents(students: StudentRecord[]): StudentRecord[] {
    return [...students].sort((left, right) =>
      this.resolveStudentFullName(left).localeCompare(this.resolveStudentFullName(right)),
    );
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

  private extractItem<T>(response: unknown): T | null {
    if (!response || Array.isArray(response)) {
      return null;
    }

    const wrapped = response as { data?: unknown } | null;
    if (wrapped?.data && typeof wrapped.data === 'object' && !Array.isArray(wrapped.data)) {
      return wrapped.data as T;
    }

    return response as T;
  }

  private buildStudentPassportPreviewUrl(passport: string | null | undefined): string {
    const fileName = String(passport ?? '').trim();
    if (!fileName) {
      return '';
    }

    return `${this.apiOrigin}/Passport/${encodeURIComponent(fileName)}`;
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

  private get selectedClassLabel(): string {
    const selectedClass = this.findClassById(this.currentClass);
    return selectedClass?.classLabel ?? this.classLabel;
  }

  private normalizeClass(item: ClassDto | Record<string, unknown>): ClassDto {
    const raw = item as Record<string, unknown>;
    return {
      id: String(raw['id'] ?? raw['Id'] ?? '').trim(),
      className: (raw['className'] ?? raw['ClassName']) as ClassDto['className'],
      classLabel: String(raw['classLabel'] ?? raw['ClassLabel'] ?? '').trim(),
      sNo: Number(raw['sNo'] ?? raw['SNo'] ?? 0),
      schoolInfoId: String(raw['schoolInfoId'] ?? raw['SchoolInfoId'] ?? '').trim(),
    };
  }

  private normalizeClassArm(item: ClassArmDto | Record<string, unknown>): ClassArmDto {
    const raw = item as Record<string, unknown>;
    return {
      id: String(raw['id'] ?? raw['Id'] ?? '').trim(),
      sNo: Number(raw['sNo'] ?? raw['SNo'] ?? 0),
      classId: String(raw['classId'] ?? raw['ClassId'] ?? '').trim(),
      armName: (raw['armName'] ?? raw['ArmName']) as ClassArmDto['armName'],
      formMaster: String(raw['formMaster'] ?? raw['FormMaster'] ?? '').trim(),
    };
  }

  private findClassById(classId: string): ClassDto | undefined {
    const normalizedId = String(classId ?? '').trim();
    if (!normalizedId) {
      return undefined;
    }

    return this.classes.find(item => this.sameId(item.id, normalizedId));
  }

  private findClassArmById(armId: string): ClassArmDto | undefined {
    const normalizedId = String(armId ?? '').trim();
    if (!normalizedId) {
      return undefined;
    }

    return this.arms.find(item => this.sameId(item.id, normalizedId));
  }

  private sameId(left?: string | null, right?: string | null): boolean {
    return String(left ?? '').trim().toLowerCase() === String(right ?? '').trim().toLowerCase();
  }
}
