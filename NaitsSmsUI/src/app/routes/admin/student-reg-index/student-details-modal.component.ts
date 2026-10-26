import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';

import { GenericService } from '@shared/services';
import { Gender, Religion, StudentStatus } from '@shared/interfaces';

interface StudentDetailsDialogData {
  studentId: string;
  classLabel: string;
  armName: string;
}

interface StudentDetailsRecord {
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
  classCategory?: number;
  currentClass?: number;
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
  user?: {
    userName?: string;
    fullName?: string;
  };
  classArm?: {
    armName?: number;
    classs?: {
      classLabel?: string;
    };
  };
}

@Component({
  selector: 'app-student-details-modal',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatDividerModule,
  ],
  template: `
    <h2 mat-dialog-title>View Details</h2>

    <mat-dialog-content>
      @if (isLoading) {
        <p class="loading-text">Loading student details...</p>
      } @else if (loadError) {
        <p class="error-text">{{ loadError }}</p>
      } @else if (student) {
        <div class="grid">
          <div class="field"><span class="label">Full Name</span><span>{{ fullName }}</span></div>
          <div class="field"><span class="label">Admission No.</span><span>{{ student.idNo || '-' }}</span></div>
          <div class="field"><span class="label">User Name</span><span>{{ student.user?.userName || '-' }}</span></div>
          <div class="field"><span class="label">Gender</span><span>{{ genderLabel }}</span></div>
          <div class="field"><span class="label">Religion</span><span>{{ religionLabel }}</span></div>
          <div class="field"><span class="label">Status</span><span>{{ statusLabel }}</span></div>
          <div class="field"><span class="label">Date Of Birth</span><span>{{ formatDate(student.dateOfBirth) }}</span></div>
          <div class="field"><span class="label">Date Of Admission</span><span>{{ formatDate(student.dateOfAdmsn) }}</span></div>
          <div class="field"><span class="label">Class</span><span>{{ classDisplay }}</span></div>
          <div class="field"><span class="label">Place Of Birth</span><span>{{ student.placeOfBirth || '-' }}</span></div>
          <div class="field"><span class="label">Blood Group</span><span>{{ student.bloodGroup || '-' }}</span></div>
          <div class="field"><span class="label">Student Mobile</span><span>{{ student.studentMobileNo || '-' }}</span></div>
          <div class="field"><span class="label">Guardian's Name</span><span>{{ student.guardiansName || '-' }}</span></div>
          <div class="field"><span class="label">Relationship</span><span>{{ student.relationship || '-' }}</span></div>
          <div class="field"><span class="label">Guardian Address</span><span>{{ student.guardianAddress || '-' }}</span></div>
          <div class="field"><span class="label">Occupation</span><span>{{ student.occupation || '-' }}</span></div>
          <div class="field"><span class="label">Mobile Number</span><span>{{ student.mobileNumber || '-' }}</span></div>
          <div class="field"><span class="label">Home Number</span><span>{{ student.homeNumber || '-' }}</span></div>
          <div class="field"><span class="label">Email Address</span><span>{{ student.emailAddress || '-' }}</span></div>
          <div class="field"><span class="label">Country</span><span>{{ student.country || '-' }}</span></div>
          <div class="field wide"><span class="label">Transfered From</span><span>{{ student.transferedFromWhere || '-' }}</span></div>
        </div>
      } @else {
        <p class="error-text">Student details not available.</p>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-stroked-button type="button" (click)="close()">Close</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .loading-text,
    .error-text {
      margin: 12px 0;
    }

    .error-text {
      color: #d32f2f;
    }

    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 12px 16px;
      padding: 6px 0;
    }

    .field {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .field.wide {
      grid-column: 1 / -1;
    }

    .label {
      color: rgba(0, 0, 0, 0.62);
      font-size: 12px;
      text-transform: uppercase;
      letter-spacing: 0.02em;
    }
  `],
})
export class StudentDetailsModalComponent implements OnInit {
  private readonly dialogRef = inject(MatDialogRef<StudentDetailsModalComponent>);
  private readonly data = inject<StudentDetailsDialogData>(MAT_DIALOG_DATA);
  private readonly api = inject(GenericService<any>);

  student: StudentDetailsRecord | null = null;
  isLoading = false;
  loadError = '';

  ngOnInit(): void {
    this.loadStudentDetails();
  }

  get fullName(): string {
    const fromUser = this.student?.user?.fullName?.trim();
    if (fromUser) {
      return fromUser;
    }

    const fromStudent = this.student?.fullName?.trim();
    if (fromStudent) {
      return fromStudent;
    }

    const assembled = [
      this.student?.firstName,
      this.student?.middleName,
      this.student?.surName,
    ]
      .map(value => value?.trim())
      .filter(Boolean)
      .join(' ');

    return assembled || '-';
  }

  get classDisplay(): string {
    const classLabel = this.student?.classArm?.classs?.classLabel || this.data.classLabel || '-';
    const armName = this.resolveArmName(this.student?.classArm?.armName) || this.data.armName || '';
    return `${classLabel} ${armName}`.trim();
  }

  get genderLabel(): string {
    if (this.student?.gender === Gender.Female) {
      return 'Female';
    }

    if (this.student?.gender === Gender.Male) {
      return 'Male';
    }

    return '-';
  }

  get religionLabel(): string {
    if (this.student?.religion === Religion.Islam) {
      return 'Islam';
    }

    if (this.student?.religion === Religion.Christianity) {
      return 'Christianity';
    }

    return '-';
  }

  get statusLabel(): string {
    if (typeof this.student?.status === 'number') {
      return StudentStatus[this.student.status] ?? String(this.student.status);
    }

    return '-';
  }

  close(): void {
    this.dialogRef.close();
  }

  formatDate(value?: string): string {
    if (!value) {
      return '-';
    }

    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString();
  }

  private loadStudentDetails(): void {
    this.isLoading = true;
    this.loadError = '';

    this.api.getAnObj<StudentDetailsRecord>(`/student/${this.data.studentId}`).subscribe({
      next: response => {
        this.student = response;
        this.isLoading = false;
      },
      error: (error: unknown) => {
        console.error('Error loading student details:', error);
        this.loadError = 'Failed to load student details.';
        this.isLoading = false;
      },
    });
  }

  private resolveArmName(armValue?: number): string {
    if (typeof armValue !== 'number') {
      return '';
    }

    const values = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'N', 'O', 'NoArm'];
    return values[armValue] ?? String(armValue);
  }
}
