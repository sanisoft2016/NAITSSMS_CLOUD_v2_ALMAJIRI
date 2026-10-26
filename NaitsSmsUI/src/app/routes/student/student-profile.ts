import { CommonModule, DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { distinctUntilChanged, finalize } from 'rxjs';

import { environment } from '@env/environment';

import { StudentPortalContextService } from './student-portal-context.service';
import { StudentPortalService } from './student-portal.service';
import { StudentProfileRecord } from './student-portal.models';

@Component({
  selector: 'app-student-profile',
  standalone: true,
  imports: [CommonModule, DatePipe],
  templateUrl: './student-profile.html',
  styleUrl: './student-profile.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentProfile implements OnInit {
  private readonly portalService = inject(StudentPortalService);
  private readonly portalContext = inject(StudentPortalContextService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly apiOrigin = new URL(environment.serverUrl).origin;

  profile: StudentProfileRecord | null = null;
  loading = true;
  errorMessage = '';

  ngOnInit(): void {
    this.portalContext.ensureLoaded();
    this.portalContext.selectedStudentId$
      .pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadProfile());
  }

  get hasAdmissionNo(): boolean {
    return Boolean(this.profile?.idNo?.trim());
  }

  get passportUrl(): string {
    const passport = this.profile?.passport?.trim();
    if (!passport) {
      return `${this.apiOrigin}/Passport/no-passport.jpg`;
    }
    return `${this.apiOrigin}/Passport/${encodeURIComponent(passport)}`;
  }

  get firstName(): string {
    return this.profile?.user?.firstName || this.profile?.firstName || '';
  }

  get middleName(): string {
    return this.profile?.user?.middleName || this.profile?.middleName || '';
  }

  get surName(): string {
    return this.profile?.user?.surName || this.profile?.surName || '';
  }

  get classLabel(): string {
    return this.profile?.classArm?.classs?.classLabel || '';
  }

  get armLabel(): string {
    const arm = this.profile?.classArm?.armName;
    if (arm === null || arm === undefined || arm === '') {
      return '';
    }
    return String(arm);
  }

  onPassportError(event: Event): void {
    const image = event.target as HTMLImageElement | null;
    if (!image) {
      return;
    }
    image.src = `${this.apiOrigin}/Passport/no-passport.jpg`;
  }

  private loadProfile(): void {
    this.loading = true;
    this.errorMessage = '';
    this.cdr.markForCheck();

    this.portalService
      .getProfile()
      .pipe(
        finalize(() => {
          this.loading = false;
          this.cdr.markForCheck();
        }),
      )
      .subscribe({
        next: profile => {
          this.profile = profile;
          this.cdr.markForCheck();
        },
        error: () => {
          this.errorMessage = 'Unable to load student profile.';
          this.cdr.markForCheck();
        },
      });
  }
}
