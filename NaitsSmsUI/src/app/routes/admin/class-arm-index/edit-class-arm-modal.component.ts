import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { CommonModule } from '@angular/common';
import { GenericService } from '@shared/services';
import { ArmName, ClassArmDto } from '@shared/interfaces';

@Component({
  selector: 'app-edit-class-arm-modal',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatButtonModule,
    MatSelectModule,
  ],
  template: `
    <h2 mat-dialog-title>Edit Class Arm</h2>
    <mat-dialog-content>
      <form [formGroup]="editForm" class="edit-arm-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Arm Name</mat-label>
          <mat-select formControlName="armName" required>
            @for (opt of armOptions; track opt.value) {
              <mat-option [value]="opt.value">{{ opt.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="onCancel()">Cancel</button>
      <button
        mat-raised-button
        color="primary"
        (click)="onSubmit()"
        [disabled]="editForm.invalid || isSubmitting"
      >
        Save
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .edit-arm-form { display: flex; flex-direction: column; min-width: 320px; padding: 16px 0; }
    .full-width { width: 100%; }
  `],
})
export class EditClassArmModalComponent {
  private readonly dialogRef = inject(MatDialogRef<EditClassArmModalComponent>);
  private readonly data: ClassArmDto = inject(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(GenericService<ClassArmDto>);
  private readonly cdr = inject(ChangeDetectorRef);

  isSubmitting = false;

  readonly armOptions: Array<{ value: ArmName; label: string }> = [
    { value: ArmName.A,     label: 'A' },
    { value: ArmName.B,     label: 'B' },
    { value: ArmName.C,     label: 'C' },
    { value: ArmName.D,     label: 'D' },
    { value: ArmName.E,     label: 'E' },
    { value: ArmName.F,     label: 'F' },
    { value: ArmName.G,     label: 'G' },
    { value: ArmName.H,     label: 'H' },
    { value: ArmName.I,     label: 'I' },
    { value: ArmName.J,     label: 'J' },
    { value: ArmName.K,     label: 'K' },
    { value: ArmName.L,     label: 'L' },
    { value: ArmName.N,     label: 'N' },
    { value: ArmName.O,     label: 'O' },
    { value: ArmName.NoArm, label: 'NoArm' },
  ];

  readonly editForm = this.fb.nonNullable.group({
    armName: [this.data.armName, [Validators.required]],
  });

  onCancel(): void {
    this.dialogRef.close(null);
  }

  onSubmit(): void {
    if (this.editForm.invalid || this.isSubmitting) return;
    this.isSubmitting = true;

    const payload: ClassArmDto = {
      ...this.data,
      armName: this.editForm.getRawValue().armName,
    };

    this.api
      .update(`/ClassAndArm/classes/arms/${this.data.id}`, payload)
      .subscribe({
        next: () => {
          this.dialogRef.close({ success: true, message: 'Arm updated successfully.' });
        },
        error: (err: unknown) => {
          this.isSubmitting = false;
          this.cdr.markForCheck();
          const msg = (err as any)?.error?.message ?? 'An error occurred while updating the arm.';
          alert(msg);
        },
      });
  }
}
