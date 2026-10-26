import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { distinctUntilChanged, finalize } from 'rxjs';

import { environment } from '@env/environment';

import { StudentPortalContextService } from './student-portal-context.service';
import { StudentPortalService } from './student-portal.service';
import { StudentSchoolInfo } from './student-portal.models';

type StudentQuickLink = {
  id: string;
  title: string;
  description: string;
  icon: string;
  route: string;
};

@Component({
  selector: 'app-student-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule],
  templateUrl: './student-dashboard.html',
  styleUrl: './student-dashboard.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentDashboard implements OnInit {
  private readonly portalService = inject(StudentPortalService);
  private readonly portalContext = inject(StudentPortalContextService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly apiOrigin = new URL(environment.serverUrl).origin;

  school: StudentSchoolInfo | null = null;
  loading = true;
  errorMessage = '';

  /** Mirrors legacy StudentsAndParents side-nav destinations as dashboard tiles. */
  readonly quickLinks: StudentQuickLink[] = [
    {
      id: 'subjects',
      title: 'Subject List and More',
      description: 'View subjects assigned to the student.',
      icon: 'menu_book',
      route: '/student/subjects',
    },
    {
      id: 'results',
      title: 'View Results',
      description: 'Open termly assessment and result sheets.',
      icon: 'assessment',
      route: '/student/results',
    },
    {
      id: 'profile',
      title: 'Student Profile',
      description: 'Review student profile details.',
      icon: 'person',
      route: '/student/profile',
    },
  ];

  ngOnInit(): void {
    this.portalContext.ensureLoaded();
    this.portalContext.selectedStudentId$
      .pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadDashboard());
  }

  get logoUrl(): string {
    const fileName = this.school?.schoolLogoString?.trim();
    if (!fileName) {
      return '';
    }
    return `${this.apiOrigin}/school-logos/${encodeURIComponent(fileName)}`;
  }

  onLogoError(event: Event): void {
    const image = event.target as HTMLImageElement | null;
    if (!image) {
      return;
    }
    image.style.display = 'none';
  }

  private loadDashboard(): void {
    this.loading = true;
    this.errorMessage = '';
    this.cdr.markForCheck();

    this.portalService
      .getDashboard()
      .pipe(
        finalize(() => {
          this.loading = false;
          this.cdr.markForCheck();
        }),
      )
      .subscribe({
        next: school => {
          this.school = school;
          this.cdr.markForCheck();
        },
        error: () => {
          this.errorMessage = 'Unable to load school profile for this student/parent account.';
          this.cdr.markForCheck();
        },
      });
  }
}
