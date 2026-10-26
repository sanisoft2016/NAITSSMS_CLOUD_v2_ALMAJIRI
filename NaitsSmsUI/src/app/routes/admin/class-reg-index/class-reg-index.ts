import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router } from '@angular/router';
import { ClassRegistryService, CurrentClass, GenericService } from '@shared/services';
import { ClassDto } from '@shared/interfaces';
import { map } from 'rxjs';
import { EditClassModalComponent } from './edit-class-modal.component';
import { RearrangeClassesModalComponent } from './rearrange-classes-modal.component';

@Component({
  selector: 'app-class-reg-index',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatIconModule,
    MatTooltipModule,
  ],
  templateUrl: './class-reg-index.html',
  styleUrl: './class-reg-index.scss',
})
export class ClassRegIndex implements OnInit {
  private readonly genericService = inject(GenericService<ClassDto>);
  private readonly classService = inject(ClassRegistryService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);

  classes: ClassDto[] = [];
  totalNoOfClasses: number = 0;
  schoolInfoId: string = '';

  ngOnInit(): void {
    this.loadClasses();
  }

   classObj: ClassDto = {} as ClassDto;

  getClassNameInString(className: number): string {
    var ght = this.genericService.classNames.find(option => option.value === className);
    return ght ? ght.label : String(className);
  }

  private classNameSortValue(className: CurrentClass | string | number | null | undefined): number {
    if (typeof className === 'number' && Number.isFinite(className)) {
      return className;
    }

    const numeric = Number(className);
    if (Number.isFinite(numeric) && String(className ?? '').trim() !== '') {
      return numeric;
    }

    const normalized = String(className ?? '').trim();
    const byLabel = this.genericService.classNames.find(
      option => option.label === normalized || String(option.value) === normalized,
    );
    if (byLabel) {
      return byLabel.value;
    }

    const enumValue = CurrentClass[normalized as keyof typeof CurrentClass];
    return typeof enumValue === 'number' ? enumValue : Number.MAX_SAFE_INTEGER;
  }

  loadClasses(): void {
    this.genericService
      .get('/ClassAndArm/classes')
      .pipe(
        map((response: any) => ({
          classes: (response?.data ?? response ?? []) as ClassDto[],
          totalNoOfClasses: response?.totalNoOfClasses ?? (response as ClassDto[])?.length ?? 0,
        }))
      )
      .subscribe({
        next: response => {
          this.classes = [...response.classes].sort(
            (left, right) => this.classNameSortValue(left.className) - this.classNameSortValue(right.className),
          );
          this.totalNoOfClasses = response.totalNoOfClasses;
          if (this.classes.length > 0) {
            this.schoolInfoId = String(this.classes[0].schoolInfoId);
          }
          this.cdr.markForCheck();
        },
        error: error => {
          console.error('Error loading classes:', error);
          this.cdr.markForCheck();
        },
      });
  }

  addNewClass(): void {
    // Navigate to the ClassReg page with totalNoOfClasses parameter
    this.router.navigate(['/admin/class-reg'], {
      queryParams: { totalNoOfClasses: this.totalNoOfClasses, schoolInfoId: this.schoolInfoId },
    });
  }

  editClass(classItem: ClassDto): void {
    const dialogRef = this.dialog.open(EditClassModalComponent, {
      width: '500px',
      data: classItem,
      disableClose: false,
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result?.success) {
        alert(result.message || 'The changes have been successfully saved.');
        this.loadClasses();
      }
    });
  }

  deleteClass(classItem: ClassDto): void {
    const confirmDelete = confirm(
      `Are you sure you want to DELETE "${classItem.classLabel}"? If 'Yes', click 'Ok' to continue.`
    );

    if (confirmDelete) {
      this.classService.deleteClass(classItem.id).subscribe({
        next: result => {
          if (result) {
            alert('Record successfully Deleted.');
            this.loadClasses();
          } else {
            alert(
              "Failed! The class has arm(s) containing student(s) and therefore we can't delete it."
            );
          }
        },
        error: (error: unknown) => {
          console.error('Error deleting class:', error);
          const httpError = error as { error?: { message?: string }; status?: number };
          const message = httpError.error?.message
            ?? (httpError.status === 409
              ? "Failed! The class has arm(s) containing student(s) and therefore we can't delete it."
              : 'An error occurred while deleting the class.');
          alert(message);
        },
      });
    }
  }


  viewArmsAndMore(classItem: ClassDto): void {
    // Navigate to ClassArmIndex
    this.router.navigate(['/admin/class-arm-index'], {
      queryParams: {
        classId: classItem.id,
        classLabel: classItem.classLabel,
        className: classItem.className,
      },
    });
  }

  reArrangeClasses(): void {
    this.classService.getClassesForReArrangement(this.schoolInfoId).subscribe({
      next: classes => {
        const dialogRef = this.dialog.open(RearrangeClassesModalComponent, {
          width: '700px',
          maxHeight: '80vh',
          data: {
            schoolInfoId: this.schoolInfoId,
            classes: classes,
          },
          disableClose: false,
        });

        dialogRef.afterClosed().subscribe(result => {
          if (result?.success) {
            alert(result.message || 'Classes have been successfully rearranged.');
            this.loadClasses();
          }
        });
      },
      error: error => {
        console.error('Error loading classes for rearrangement:', error);
        alert('An error occurred while loading classes for rearrangement.');
      },
    });
  }
}
