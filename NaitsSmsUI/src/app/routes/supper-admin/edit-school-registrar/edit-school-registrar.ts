import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs/operators';

import { BaseComponent, GenericService } from '@shared';

type EditSchoolRegistrarModel = {
  id?: string;
  firstName?: string;
  middleName?: string;
  surName?: string;
  userName?: string;
  email?: string;
  gender?: number;
  phoneNumber?: string;
  schoolName?: string;
  createdDate?: string;
  dateOfBirth?: string | null;
  addresses?: string;
  signatureImageString?: string;
  timeStatus?: number;
  accessStatus?: number;
  userStatus?: number;
  smsApiKey?: string;
  smsApiToken?: string;
  password?: string | null;
  confirmPassword?: string | null;
};

type UpdateSchoolRegistrarResult = {
  status?: number;
  success?: boolean;
  message?: string;
  redirectPath?: string;
};

@Component({
  selector: 'app-edit-school-registrar',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './edit-school-registrar.html',
  styleUrl: './edit-school-registrar.scss',
})
export class EditSchoolRegistrar extends BaseComponent implements OnInit {
  private readonly api = inject(GenericService<any>);
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  userId = '';
  loaded = false;
  private createdDate = '';
  private signatureImageStringOld = '';
  private timeStatus = 0;
  private accessStatus = 0;
  private userStatus = 0;
  private dateOfBirth: string | null = null;
  private addresses = '';

  readonly genders = [
    { value: 0, label: 'Male' },
    { value: 1, label: 'Female' },
  ];

  readonly form = this.fb.nonNullable.group({
    id: ['', Validators.required],
    smsApiToken: [''],
    smsApiKey: [''],
    firstName: ['', Validators.required],
    middleName: [''],
    surName: ['', Validators.required],
    userName: ['', Validators.required],
    email: [''],
    gender: [0, Validators.required],
    phoneNumber: [''],
    schoolName: ['', Validators.required],
    password: [''],
    confirmPassword: [''],
  });

  constructor() {
    super();
  }

  ngOnInit(): void {
    this.userId = String(this.activatedRoute.snapshot.queryParamMap.get('userId') ?? '').trim();
    if (!this.userId) {
      this.showAlert('User id is required to edit School Registrar.', { type: 'warning' });
      this.router.navigate(['/supper-admin/school-list']);
      return;
    }

    this.loadRegistrar();
  }

  goBack(): void {
    this.router.navigate(['/supper-admin/school-list']);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.showAlert('Please complete the required fields to continue.', { type: 'warning' });
      return;
    }

    const raw = this.form.getRawValue();
    const password = (raw.password ?? '').trim();
    const confirmPassword = (raw.confirmPassword ?? '').trim();

    // Legacy SadminController.EditSchoolRegistrar POST: password optional, must match when set.
    if (password && password !== confirmPassword) {
      this.showAlert('Password mis-matched.', { type: 'warning' });
      return;
    }

    if (!this.startSubmitting()) return;

    const payload: EditSchoolRegistrarModel = {
      id: raw.id.trim(),
      firstName: raw.firstName.trim(),
      middleName: (raw.middleName ?? '').trim(),
      surName: raw.surName.trim(),
      userName: raw.userName.trim(),
      email: (raw.email ?? '').trim(),
      gender: Number(raw.gender),
      phoneNumber: (raw.phoneNumber ?? '').trim(),
      schoolName: raw.schoolName.trim(),
      smsApiToken: (raw.smsApiToken ?? '').trim(),
      smsApiKey: (raw.smsApiKey ?? '').trim(),
      password: password || null,
      confirmPassword: confirmPassword || null,
      createdDate: this.createdDate,
      signatureImageString: this.signatureImageStringOld,
      timeStatus: this.timeStatus,
      accessStatus: this.accessStatus,
      userStatus: this.userStatus,
      dateOfBirth: this.dateOfBirth,
      addresses: this.addresses,
    };

    this.withRequestState(
      this.api.create<UpdateSchoolRegistrarResult>('/SuperAdmin/update-school-registrar-info', payload),
      {
        errorMessage: 'Failed! Unable to complete the process.',
        notifyOnError: 'none',
        rethrow: false,
        useLoading: false,
        onError: () => {
          this.showAlert('Failed! Unable to complete the process.', { type: 'warning' });
        },
      },
    )
      .pipe(finalize(() => this.stopSubmitting()))
      .subscribe(result => {
        const message = result?.message || 'Failed! Unable to complete the process.';
        if (result?.success) {
          this.showAlert(message, { type: 'success' });
          this.router.navigateByUrl(result.redirectPath || '/supper-admin/school-list');
          return;
        }

        this.showAlert(message, { type: 'warning' });
      });
  }

  private loadRegistrar(): void {
    this.withRequestState(
      this.api.getAnObj<EditSchoolRegistrarModel>(
        `/SuperAdmin/get-school-registrar-byId/${encodeURIComponent(this.userId)}`,
      ),
      {
        errorMessage: 'Failed to load school registrar details.',
        notifyOnError: 'alert',
        rethrow: false,
      },
    ).subscribe(model => {
      if (!model?.id) {
        this.showAlert('School registrar was not found.', { type: 'warning' });
        this.router.navigate(['/supper-admin/school-list']);
        return;
      }

      this.createdDate = model.createdDate ?? '';
      this.signatureImageStringOld = model.signatureImageString ?? '';
      this.timeStatus = Number(model.timeStatus ?? 0);
      this.accessStatus = Number(model.accessStatus ?? 0);
      this.userStatus = Number(model.userStatus ?? 0);
      this.dateOfBirth = model.dateOfBirth ?? null;
      this.addresses = model.addresses ?? '';

      this.form.patchValue({
        id: model.id ?? '',
        smsApiToken: model.smsApiToken ?? '',
        smsApiKey: model.smsApiKey ?? '',
        firstName: model.firstName ?? '',
        middleName: model.middleName ?? '',
        surName: model.surName ?? '',
        userName: model.userName ?? '',
        email: model.email ?? '',
        gender: Number(model.gender ?? 0),
        phoneNumber: model.phoneNumber ?? '',
        schoolName: model.schoolName ?? '',
        password: '',
        confirmPassword: '',
      });

      this.loaded = true;
    });
  }
}
