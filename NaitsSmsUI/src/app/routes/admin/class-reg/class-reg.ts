import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ClassDto } from '@shared/interfaces';
import { BaseComponent, CurrentClass, GenericService } from '@shared';
import { finalize } from 'rxjs/operators';



@Component({
  selector: 'app-class-reg',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
  ],
  templateUrl: './class-reg.html',
  styleUrl: './class-reg.scss',
})
export class ClassReg extends BaseComponent implements OnInit {
  constructor() { super(); }

  private readonly fb = inject(FormBuilder);
  private readonly genericService = inject(GenericService<ClassDto>);

  readonly classNames: Array<{ value: CurrentClass; label: string }> = [
    { value: CurrentClass.PlayClass, label: 'Play Class' },
    { value: CurrentClass.PreNursery, label: 'Pre Nursery' },
    { value: CurrentClass.Nursery1, label: 'Nursery 1' },
    { value: CurrentClass.Nursery2, label: 'Nursery 2' },
    { value: CurrentClass.Primary1, label: 'Primary 1' },
    { value: CurrentClass.Primary2, label: 'Primary 2' },
    { value: CurrentClass.Primary3, label: 'Primary 3' },
    { value: CurrentClass.Primary4, label: 'Primary 4' },
    { value: CurrentClass.Primary5, label: 'Primary 5' },
    { value: CurrentClass.Primary6, label: 'Primary 6' },
    { value: CurrentClass.Jss1, label: 'JSS 1' },
    { value: CurrentClass.Jss2, label: 'JSS 2' },
    { value: CurrentClass.Jss3, label: 'JSS 3' },
    { value: CurrentClass.Ss1, label: 'SS 1' },
    { value: CurrentClass.Ss2, label: 'SS 2' },
    { value: CurrentClass.Ss3, label: 'SS 3' },
  ];

  readonly classForm = this.fb.nonNullable.group({
    id: [''],
    className: [CurrentClass.PlayClass, [Validators.required]],
    classLabel: ['', [Validators.required]],
    sNo: [0],
    schoolInfoId: [''],
  });

  ngOnInit(): void {
    const totalNoOfClasses = Number(this.route.snapshot.queryParamMap.get('totalNoOfClasses') ?? '0');
    const schoolInfoId = this.route.snapshot.queryParamMap.get('schoolInfoId') ?? '1';

    const nextSNo = Number.isFinite(totalNoOfClasses) && totalNoOfClasses > 0 ? totalNoOfClasses + 1 : 0;

    this.classForm.patchValue({
      sNo: nextSNo,
      schoolInfoId: String(Number.isFinite(Number(schoolInfoId)) && Number(schoolInfoId) > 0 ? schoolInfoId : '1'),
    });

    this.syncClassLabelFromClassName(this.classForm.controls.className.value);

    this.classForm.controls.className.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(className => this.syncClassLabelFromClassName(className));
  }

  private syncClassLabelFromClassName(className: CurrentClass): void {
    const label = this.classNames.find(opt => opt.value === className)?.label ?? '';
    this.classForm.controls.classLabel.setValue(label);
  }

  goBack(): void {
    this.router.navigate(['/admin/class-reg-index']);
  }

  onSubmit(): void
  {
    this.classForm.markAllAsTouched();
    if (this.classForm.invalid || !this.startSubmitting()) return;

    const formValue = this.classForm.getRawValue();
    const payload: ClassDto = {
      ...(formValue as unknown as ClassDto),
      // Keep server/API compatibility: send enum name (e.g. "PlayClass")
      className: formValue.className as CurrentClass,
    };

    this.withRequestState(this.genericService.create<ClassDto>('/ClassAndArm/classes', payload), {
      useLoading: true,
      notifyOnError: 'none',
      onError: (error: unknown) => {
        this.errorMessage.set(this.getHttpErrorMessage(error, 'An error occurred while creating the class.'));
      },
      rethrow: false,
    })
      .pipe(finalize(() => this.stopSubmitting()))
      .subscribe({
        next: () => {
          this.showAlert('Class created successfully.', { type: 'success' });
          this.goBack();
        },
      });
  }
}
