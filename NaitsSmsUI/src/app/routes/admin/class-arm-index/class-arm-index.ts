import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { map } from 'rxjs/operators';
import { GenericService, StudentRegistrationContextService } from '@shared/services';
import { ArmName, ClassArmDto } from '@shared/interfaces';
import { EditClassArmModalComponent } from './edit-class-arm-modal.component';

@Component({
  selector: 'app-class-arm-index',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatTableModule,
    MatIconModule,
    MatTooltipModule,
    MatDialogModule,
  ],
  templateUrl: './class-arm-index.html',
  styleUrl: './class-arm-index.scss',
})
export class ClassArmIndex implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly api = inject(GenericService<ClassArmDto>);
  private readonly studentRegistrationContext = inject(StudentRegistrationContextService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly classId = this.route.snapshot.queryParamMap.get('classId') ?? '';
  readonly classLabel = this.route.snapshot.queryParamMap.get('classLabel') ?? '';
  readonly className = this.route.snapshot.queryParamMap.get('className') ?? '';

  arms: ClassArmDto[] = [];
  totalNoOfArms = 0;
  noArms = false;
  isLoading = false;
  loadError = '';
  displayedColumns: string[] = ['sNo', 'armName', 'actions'];

  ngOnInit(): void {
    this.loadArms();
  }

  loadArms(): void {
    this.isLoading = true;
    this.loadError = '';
    const classId = this.classId;
    this.api
      .get(`/ClassAndArm/classes-arms/${classId}`)
      .pipe(
        map((response: any) => {
          if (!response) {
            return { arms: [] as ClassArmDto[], totalNoOfArms: 0, noArms: true };
          }
          return {
            arms: (response?.data ?? response ?? []) as ClassArmDto[],
            totalNoOfArms: response?.totalNoOfArms ?? 0,
            noArms: false,
          };
        })
      )
      .subscribe({
        next: result => {
          this.arms = result.arms;
          this.totalNoOfArms = result.totalNoOfArms;
          this.noArms = result.noArms;
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: err => {
          console.error('Error loading arms:', err);
          this.loadError = 'Failed to load arms. Please try again.';
          this.isLoading = false;
          this.cdr.markForCheck();
        },
      });
  }

  getArmLabel(armName: ArmName): string {
    return ArmName[armName] ?? String(armName);
  }

  goBack(): void {
    this.router.navigate(['/admin/class-reg-index']);
  }

  addArm(): void {
    this.router.navigate(['/admin/class-arm-reg'], {
      queryParams: {
        classId: this.classId,
        classLabel: this.classLabel,
        className: this.className,
        totalNoOfArms: this.totalNoOfArms,
      },
    });
  }

  editArm(arm: ClassArmDto): void {
    const dialogRef = this.dialog.open(EditClassArmModalComponent, {
      width: '400px',
      data: arm,
      disableClose: false,
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result?.success) {
        alert(result.message ?? 'Arm updated successfully.');
        this.loadArms();
      }
    });
  }

  deleteArm(arm: ClassArmDto): void {
    if (!confirm(`Are you sure you want to DELETE "${ArmName[arm.armName]}"? Click OK to continue.`)) return;

    this.api.delete(`/ClassAndArm/classes/arms/${arm.id}`).subscribe({
      next: () => {
        alert('Arm deleted successfully.');
        this.loadArms();
      },
      error: (err: unknown) => {
        const msg = (err as any)?.error?.message
          ?? "Failed! The arm contains student(s) and therefore cannot be deleted.";
        alert(msg);
      },
    });
  }

  viewOrAddStudents(arm: ClassArmDto): void {
    const context = {
      classId: this.classId,
      classs: this.className,
      classLabel: this.classLabel,
      armId: arm.id,
      armName: ArmName[arm.armName] ?? String(arm.armName),
      totalNoOfStudents: 0,
    };

    this.studentRegistrationContext.setContext(context);

    this.router.navigate(['/admin/student-reg-index'], {
      state: context,
      queryParams: this.studentRegistrationContext.toQueryParams(context),
    });
  }
}
