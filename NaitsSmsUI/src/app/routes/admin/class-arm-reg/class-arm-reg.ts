import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { finalize } from 'rxjs/operators';
import { BaseComponent, GenericService } from '@shared';
import { ArmName, ClassArmDto } from '@shared/interfaces';

@Component({
  selector: 'app-class-arm-reg',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    MatIconModule,
  ],
  templateUrl: './class-arm-reg.html',
  styleUrl: './class-arm-reg.scss',
})
export class ClassArmReg extends BaseComponent {
  constructor() { super(); }

  private readonly fb = inject(FormBuilder);
  private readonly api = inject(GenericService<ClassArmDto>);

  private readonly classId    = this.route.snapshot.queryParamMap.get('classId') ?? '';
  private readonly classLabel = this.route.snapshot.queryParamMap.get('classLabel') ?? '';
  private readonly className  = this.route.snapshot.queryParamMap.get('className') ?? '';

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

  readonly armForm = this.fb.nonNullable.group({
    armName: [ArmName.A, [Validators.required]],
    classId: [this.classId, [Validators.required]],
  });

  get headerLabel(): string {
    return this.classLabel || this.className || 'Class';
  }

  goBack(): void {
    this.router.navigate(['/admin/class-arm-index'], {
      queryParams: {
        classId:    this.classId,
        classLabel: this.classLabel,
        className:  this.className,
      },
    });
  }

  onSubmit(): void {
    this.armForm.markAllAsTouched();
    if (this.armForm.invalid || !this.startSubmitting()) return;

    const payload: Partial<ClassArmDto> = {
      classId: this.classId,
      armName: this.armForm.getRawValue().armName,
    };

    this.withRequestState(this.api.create('/ClassAndArm/classes/arms', payload as ClassArmDto), {
      useLoading: true,
      notifyOnError: 'none',
      onError: (error: unknown) => {
        this.errorMessage.set(this.getHttpErrorMessage(error, 'An error occurred while creating the arm.'));
      },
      rethrow: false,
    })
      .pipe(finalize(() => this.stopSubmitting()))
      .subscribe({
        next: () => {
          this.showAlert('Arm created successfully.', { type: 'success' });
          this.goBack();
        },
      });
  }
}
