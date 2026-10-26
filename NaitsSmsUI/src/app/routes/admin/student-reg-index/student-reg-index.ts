import { ChangeDetectorRef, Component, OnInit, TemplateRef, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { environment } from '@env/environment';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { GenericService, StudentRegistrationContextService } from '@shared/services';
import { AccessStatus, ArmName, Gender, Religion, StudentDto, StudentRegistrationContext, StudentStatus } from '@shared/interfaces';

interface StudentDetailsRecord {
  id?: string;
  userId?: string;
  idNo?: string;
  firstName?: string;
  middleName?: string;
  surName?: string;
  fullName?: string;
  gender?: Gender;
  religion?: Religion;
  status?: StudentStatus;
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
  classCategory?: string;
  passport?: string;
  user?: {
    firstName?: string;
    middleName?: string;
    surName?: string;
    userName?: string;
    fullName?: string;
    phoneNumber?: string;
    email?: string;
    accessStatus?: AccessStatus;
  };
  classArm?: {
    armName?: ArmName;
    classs?: {
      classLabel?: string;
    };
  };
}

@Component({
  selector: 'app-student-reg-index',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatIconModule,
    MatTooltipModule,
  ],
  templateUrl: './student-reg-index.html',
  styleUrl: './student-reg-index.scss',
})
export class StudentRegIndex implements OnInit {
  private readonly route  = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api    = inject(GenericService<StudentDto>);
  private readonly dialog = inject(MatDialog);
  private readonly studentRegistrationContext = inject(StudentRegistrationContextService);
  private readonly cdr    = inject(ChangeDetectorRef);
  private readonly apiOrigin = new URL(environment.serverUrl).origin;
  private detailsDialogRef: ReturnType<MatDialog['open']> | null = null;

  @ViewChild('studentDetailsDialog')
  private studentDetailsDialog!: TemplateRef<unknown>;

  classId = '';
  classs = '';
  classLabel = '';
  armId = '';
  armName = '';

  students: StudentDto[]  = [];
  totalNoOfStudents        = 0;
  isLoading                = false;
  loadError                = '';
  noStudents               = false;
  selectedStudent: StudentDetailsRecord | null = null;
  isDetailsLoading = false;
  detailsLoadError = '';
  isActionBusy = false;
  showChangePasswordPanel = false;
  newPassword = '';

  readonly AccessStatus = AccessStatus;

  readonly StudentStatus = StudentStatus;

  ngOnInit(): void {
    if (!this.loadContext()) {
      // Requires class/arm from Student List or Class/Arm flow; do not open bare.
      void this.router.navigate(['/admin/student-home-page']);
      return;
    }

    this.loadStudents();
  }

  private loadContext(): boolean {
    const stateContext = this.buildContext({
      classId: history.state?.classId,
      classs: history.state?.classs,
      classLabel: history.state?.classLabel,
      armId: history.state?.armId,
      armName: history.state?.armName,
      totalNoOfStudents: history.state?.totalNoOfStudents,
    });

    const routeContext: StudentRegistrationContext = {
      classId: this.route.snapshot.queryParamMap.get('classId') ?? '',
      classs: this.route.snapshot.queryParamMap.get('classs') ?? '',
      classLabel: this.route.snapshot.queryParamMap.get('classLabel') ?? '',
      armId: this.route.snapshot.queryParamMap.get('armId') ?? '',
      armName: this.route.snapshot.queryParamMap.get('armName') ?? '',
      totalNoOfStudents: Number(this.route.snapshot.queryParamMap.get('totalNoOfStudents') ?? '0'),
    };

    const hasRouteContext = !!routeContext.classId && !!routeContext.armId;
    const context = stateContext
      ?? (hasRouteContext ? routeContext : null)
      ?? this.studentRegistrationContext.getContext();

    if (!context) {
      return false;
    }

    this.classId = context.classId;
    this.classs = context.classs;
    this.classLabel = context.classLabel;
    this.armId = context.armId;
    this.armName = context.armName;
    this.totalNoOfStudents = Number(context.totalNoOfStudents ?? 0);
    this.studentRegistrationContext.setContext(context);
    return true;
  }

  private buildContext(raw: Partial<StudentRegistrationContext> | null | undefined): StudentRegistrationContext | null {
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
      classs: String(raw.classs ?? '').trim(),
      classLabel: String(raw.classLabel ?? '').trim(),
      armId,
      armName: String(raw.armName ?? '').trim(),
      totalNoOfStudents: Number(raw.totalNoOfStudents ?? 0),
    };
  }

  loadStudents(): void {
    if (!this.armId) {
      this.loadError = 'Student registration context is missing. Please select the class arm again.';
      return;
    }

    this.isLoading  = true;
    this.loadError  = '';
    this.noStudents = false;

    this.api.get(`/student/get-students-by-armId/${this.armId}`).subscribe({
      next: (response: any) => {
        const list = (Array.isArray(response) ? response : response?.data ?? []) as StudentDto[];
        this.students         = list.sort((a, b) =>
          (a.fullName ?? '').localeCompare(b.fullName ?? ''),
        );
        this.totalNoOfStudents = this.students.length;
        this.studentRegistrationContext.patchContext({
          totalNoOfStudents: this.totalNoOfStudents,
        });
        this.noStudents        = this.students.length === 0;
        this.isLoading         = false;
        this.cdr.markForCheck();
      },
      error: (err: unknown) => {
        console.error('Error loading students:', err);
        this.loadError = 'Failed to load students. Please try again.';
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  getGenderLabel(gender: Gender): string {
    return gender === Gender.Female ? 'Female' : 'Male';
  }

  getStatusClass(status: StudentStatus): string {
    if (status === StudentStatus.Fresher)   return 'status-fresher';
    if (status === StudentStatus.Previous)  return 'status-previous';
    return '';
  }

  getStatusLabel(status: StudentStatus): string {
    return StudentStatus[status] ?? String(status);
  }

  getClassDisplay(student: StudentDto): string {
    const label   = student.classArm?.classs?.classLabel ?? this.classLabel ?? '';
    const armName = student.classArm
      ? ArmName[student.classArm.armName] ?? ''
      : this.armName;
    return `${label}  ${armName}`.trim();
  }

  get fullName(): string {
    const fromUser = this.selectedStudent?.user?.fullName?.trim();
    if (fromUser) {
      return fromUser;
    }

    const fromStudent = this.selectedStudent?.fullName?.trim();
    if (fromStudent) {
      return fromStudent;
    }

    const assembled = [
      this.selectedStudent?.firstName,
      this.selectedStudent?.middleName,
      this.selectedStudent?.surName,
    ]
      .map(value => value?.trim())
      .filter(Boolean)
      .join(' ');

    return assembled || '-';
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
    const classLabel = this.selectedStudent?.classArm?.classs?.classLabel || this.classLabel || '-';
    const armName = this.selectedStudent?.classArm?.armName;
    const resolvedArm = typeof armName === 'number'
      ? (ArmName[armName] ?? '')
      : (this.armName || '');

    // Collapse padded/non-breaking whitespace so the value sits one space after the label, like the other fields.
    return `${classLabel} ${resolvedArm}`.replace(/\s+/g, ' ').trim();
  }

  get genderLabel(): string {
    if (this.selectedStudent?.gender === Gender.Female) {
      return 'Female';
    }

    if (this.selectedStudent?.gender === Gender.Male) {
      return 'Male';
    }

    return '-';
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

  get statusLabel(): string {
    if (typeof this.selectedStudent?.status === 'number') {
      return StudentStatus[this.selectedStudent.status] ?? String(this.selectedStudent.status);
    }

    return '-';
  }

  get studentPassportUrl(): string {
    const passport = this.selectedStudent?.passport?.trim();
    if (!passport) {
      return '';
    }

    return `${this.apiOrigin}/Passport/${encodeURIComponent(passport)}`;
  }

  get isAccountDisabled(): boolean {
    return this.selectedStudent?.user?.accessStatus === AccessStatus.Disable;
  }

  editRecord(): void {
    this.editStudent();
  }

  deleteRecord(): void {
    this.deleteStudent();
  }

  loadPasswordPage(): void {
    this.toggleChangePasswordPanel();
  }

  enableStudentAccount(): void {
    if (this.isAccountDisabled) {
      this.toggleAccountStatus();
    }
  }

  disableStudentAccount(): void {
    if (!this.isAccountDisabled) {
      this.toggleAccountStatus();
    }
  }

  editStudent(): void {
    if (!this.selectedStudent) {
      return;
    }

    const editStudentId = this.selectedStudent.id;
    const editStudentUserId = this.selectedStudent.userId;
    const context = {
      classId: this.classId,
      classs: this.classs,
      classLabel: this.classLabel,
      armId: this.armId,
      armName: this.armName,
      totalNoOfStudents: this.totalNoOfStudents,
    };

    this.studentRegistrationContext.setContext(context);
    this.closeDetailsDialog();

    // Legacy-style URL (/EditStudentInfo/{id}): only the student id is visible; the rest travels in history state.
    this.router.navigate(['/admin/edit-student', editStudentId], {
      state: {
        ...context,
        returnView: 'student-reg-index',
        editStudentId,
        editStudentUserId,
      },
    });
  }

  deleteStudent(): void {
    if (!this.selectedStudent) {
      return;
    }

    const fullName = this.fullName;
    const confirmed = confirm(
      `Are you sure you want to DELETE "${fullName}"? If 'Yes', click 'Ok' to continue.`
    );

    if (!confirmed) {
      return;
    }

    this.isActionBusy = true;
    this.cdr.markForCheck();

    this.api.delete(`/student/${this.selectedStudent.id}`).subscribe({
      next: () => {
        this.isActionBusy = false;
        alert('Record successfully Deleted.');
        this.closeDetailsDialog();
        this.loadStudents();
      },
      error: (err: unknown) => {
        console.error('Error deleting student:', err);
        this.isActionBusy = false;
        alert('Failed! The student has one or more important record(s) and therefore we cannot delete it.');
        this.cdr.markForCheck();
      },
    });
  }

  toggleChangePasswordPanel(): void {
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

    this.api.create<{ status: string; data?: unknown }>('/user/post-change-password', {
      userId,
      newPassword: password,
    }).subscribe({
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

  toggleAccountStatus(): void {
    if (!this.selectedStudent) {
      return;
    }

    const action = this.isAccountDisabled ? 'enable' : 'disable';
    const fullName = this.fullName;
    const confirmed = confirm(
      `Are you sure you want to ${action.toUpperCase()} the account of "${fullName}"?`
    );

    if (!confirmed) {
      return;
    }

    const userId = this.selectedStudent.userId;
    if (!userId) {
      alert('Unable to resolve user ID.');
      return;
    }

    this.isActionBusy = true;
    this.cdr.markForCheck();

    this.api.create<{ message: string }>(`/student/${userId}/${action}`, {}).subscribe({
      next: () => {
        this.isActionBusy = false;
        const newStatus = this.isAccountDisabled ? AccessStatus.Active : AccessStatus.Disable;
        if (this.selectedStudent?.user) {
          this.selectedStudent.user.accessStatus = newStatus;
        }
        alert(`Student account ${action}d successfully.`);
        this.cdr.markForCheck();
      },
      error: (err: unknown) => {
        console.error(`Error ${action}ing account:`, err);
        this.isActionBusy = false;
        alert(`Failed to ${action} the student account. Please try again.`);
        this.cdr.markForCheck();
      },
    });
  }

  printStudentDetails(): void {
    const detailsElement = document.getElementById('studentDetails');
    if (!detailsElement) {
      alert('Student details are not available for printing.');
      return;
    }

    const winPrint = window.open('', '_blank', 'width=1024,height=768');
    if (!winPrint) {
      alert('Unable to open print window. Please allow pop-ups and try again.');
      return;
    }

    const printStyles = `
      body { font-family: Arial, Helvetica, sans-serif; background: #fff; margin: 20px; }
      .legacy-body-border { border: 1px solid #000; padding: 1em; width: 100%; box-sizing: border-box; }
      .legacy-table-layout { width: 100%; border-collapse: collapse; }
      .legacy-cell { width: 50%; vertical-align: top; }
      .thumb-image { display: block; width: 100%; height: 100%; padding: 5px; object-fit: cover; box-sizing: border-box; }
      .image-holder { display: flow-root; width: 16em; height: 16em; border-style: double; margin: 0 auto 8px; box-sizing: border-box; }
      .dl-horizontal dt { float: left; width: 160px; overflow: hidden; clear: left; text-align: right; text-overflow: ellipsis; white-space: nowrap; }
      .dl-horizontal dd { margin-left: 180px; margin-bottom: 6px; min-height: 18px; }
      .dl-horizontal dd:before, .dl-horizontal dd:after { display: table; content: " "; }
      .dl-horizontal dd:after { clear: both; }
    `;

    winPrint.document.write(`
      <html>
        <head>
          <title>Student Details</title>
          <style>${printStyles}</style>
        </head>
        <body>
          <h4 style="text-align:center;">Student Details</h4>
          ${detailsElement.outerHTML}
        </body>
      </html>
    `);
    winPrint.document.close();
    winPrint.focus();
    winPrint.print();
    winPrint.close();
  }

  formatDate(value?: string): string {
    if (!value) {
      return '-';
    }

    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString();
  }

  goBack(): void {
    this.router.navigate(['/admin/class-arm-index'], {
      queryParams: {
        classId:    this.classId,
        classLabel: this.classLabel,
        className:  this.classs,
        returnView: 'student-reg-index',
      },
    });
  }

  addStudent(): void {
    const context = {
      classId: this.classId,
      classs: this.classs,
      classLabel: this.classLabel,
      armId: this.armId,
      armName: this.armName,
      totalNoOfStudents: this.totalNoOfStudents,
    };

    this.studentRegistrationContext.setContext(context);

    this.router.navigate(['/auth/register-student'], {
      state: context,
      queryParams: {
        ...this.studentRegistrationContext.toQueryParams(context),
        returnView: 'student-reg-index',
      },
    });
  }

  viewDetails(student: StudentDto): void {
    this.selectedStudent = null;
    this.isDetailsLoading = true;
    this.detailsLoadError = '';

    this.detailsDialogRef = this.dialog.open(this.studentDetailsDialog, {
      width: '900px',
      maxWidth: '95vw',
    });

    this.api.getAnObj<StudentDetailsRecord>(`/user-registration/get-student-details-byId/${student.id}`).subscribe({
      next: details => {
        this.selectedStudent = details;
        this.isDetailsLoading = false;
        this.cdr.markForCheck();
      },
      error: (err: unknown) => {
        console.error('Error loading student details:', err);
        this.detailsLoadError = 'Failed to load student details.';
        this.isDetailsLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  closeDetailsDialog(): void {
    if (this.detailsDialogRef) {
      this.detailsDialogRef.close();
      this.detailsDialogRef = null;
    }
    this.selectedStudent = null;
    this.isDetailsLoading = false;
    this.detailsLoadError = '';
  }

  ngOnDestroy(): void {
    if (this.detailsDialogRef) {
      this.detailsDialogRef.close();
      this.detailsDialogRef = null;
    }
  }
}
