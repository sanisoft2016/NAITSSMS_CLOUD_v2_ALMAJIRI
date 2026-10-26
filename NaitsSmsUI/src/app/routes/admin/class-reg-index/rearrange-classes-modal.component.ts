import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, FormArray, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { CommonModule } from '@angular/common';
import { ClassRegistryService } from '@shared/services';
import { ClassDto, ClassReArrangementDto } from '@shared/interfaces';

@Component({
  selector: 'app-rearrange-classes-modal',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatTableModule,
  ],
  template: `
    <h2 mat-dialog-title class="text-center text-danger">Re-arrange Classes</h2>
    <mat-dialog-content>
      <p class="lead text-center text-danger">
        Re-arrange the classes the way you want.
      </p>
      <form [formGroup]="rearrangeForm">
        <table mat-table [dataSource]="classes" class="mat-elevation-z8 full-width">
          <!-- SNo Column -->
          <ng-container matColumnDef="sno">
            <th mat-header-cell *matHeaderCellDef>SNo</th>
            <td mat-cell *matCellDef="let element; let i = index">{{ element.sNo }}</td>
          </ng-container>

          <!-- Class Name Column -->
          <ng-container matColumnDef="className">
            <th mat-header-cell *matHeaderCellDef>Class Name</th>
            <td mat-cell *matCellDef="let element">{{ element.className }}</td>
          </ng-container>

          <!-- Class Label Column -->
          <ng-container matColumnDef="classLabel">
            <th mat-header-cell *matHeaderCellDef>Class Label</th>
            <td mat-cell *matCellDef="let element">{{ element.classLabel }}</td>
          </ng-container>

          <!-- New Value Column -->
          <ng-container matColumnDef="newValue">
            <th mat-header-cell *matHeaderCellDef>New Value</th>
            <td mat-cell *matCellDef="let element; let i = index">
              <mat-form-field appearance="outline" class="new-value-input">
                <input
                  matInput
                  type="number"
                  [formControlName]="i"
                  min="1"
                  required
                  placeholder="Enter new SNo"
                />
                <mat-error *ngIf="getControl(i)?.hasError('required')">
                  Value is required
                </mat-error>
              </mat-form-field>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns"></tr>
        </table>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="onCancel()">Close</button>
      <button
        mat-raised-button
        color="primary"
        (click)="onSubmit()"
        [disabled]="!rearrangeForm.valid"
      >
        Submit
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      mat-dialog-content {
        max-height: 70vh;
        overflow-y: auto;
        padding: 20px;
      }

      .full-width {
        width: 100%;
      }

      .new-value-input {
        max-width: 100px;
      }

      .new-value-input ::ng-deep .mat-mdc-form-field-infix {
        padding: 8px 0;
      }

      table {
        width: 100%;
      }

      th.mat-header-cell,
      td.mat-cell {
        padding: 8px 16px;
      }

      .text-center {
        text-align: center;
      }

      .text-danger {
        color: #dc3545;
      }

      .lead {
        font-size: 1.1rem;
        font-weight: 300;
      }
    `,
  ],
})
export class RearrangeClassesModalComponent implements OnInit {
  private readonly dialogRef = inject(MatDialogRef<RearrangeClassesModalComponent>);
  private readonly data = inject<{ schoolInfoId: string; classes: ClassDto[] }>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly classService = inject(ClassRegistryService);

  rearrangeForm!: FormGroup;
  classes: ClassDto[] = [];
  displayedColumns: string[] = ['sno', 'className', 'classLabel', 'newValue'];

  ngOnInit(): void {
    this.classes = this.data.classes;
    this.initForm();
  }

  private initForm(): void {
    const formControls: { [key: string]: any } = {};
    this.classes.forEach((classItem, index) => {
      formControls[index] = ['', [Validators.required, Validators.min(1)]];
    });
    this.rearrangeForm = this.fb.group(formControls);
  }

  getControl(index: number) {
    return this.rearrangeForm.get(index.toString());
  }

  onSubmit(): void {
    if (this.rearrangeForm.valid) {
      const classDetails: ClassReArrangementDto[] = [];
      let allValid = true;

      this.classes.forEach((classItem, index) => {
        const newSNo = this.rearrangeForm.get(index.toString())?.value;
        if (!newSNo || newSNo === '') {
          allValid = false;
          return;
        }
        classDetails.push({
          id: classItem.id,
          schoolInfoId: this.data.schoolInfoId,
          sNo: parseInt(newSNo, 10),
        });
      });

      if (!allValid) {
        alert('One or more required Value(s) were not entered.');
        return;
      }

      this.classService.submitClassReArrangement(classDetails).subscribe({
        next: result => {
          if (result) {
            this.dialogRef.close({
              success: true,
              message: 'Classes have been successfully rearranged.',
            });
          } else {
            alert('Failed to rearrange classes.');
          }
        },
        error: error => {
          console.error('Error rearranging classes:', error);
          alert('An error occurred while rearranging the classes.');
        },
      });
    } else {
      alert('Please fill in all required fields.');
    }
  }

  onCancel(): void {
    this.dialogRef.close();
  }
}
