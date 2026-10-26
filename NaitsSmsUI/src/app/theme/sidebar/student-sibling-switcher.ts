import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { StudentPortalContextService } from '../../routes/student/student-portal-context.service';

@Component({
  selector: 'app-student-sibling-switcher',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './student-sibling-switcher.html',
  styleUrl: './student-sibling-switcher.scss',
})
export class StudentSiblingSwitcher implements OnInit {
  readonly context = inject(StudentPortalContextService);

  ngOnInit(): void {
    this.context.ensureLoaded();
  }

  onSelectionChange(studentId: string): void {
    this.context.selectStudent(studentId);
  }

  optionLabel(sibling: {
    displayName: string;
    email?: string;
    idNo: string;
    classLabel: string;
    armLabel: string;
  }): string {
    const parts = [sibling.displayName];
    if (sibling.idNo) {
      parts.push(`(${sibling.idNo})`);
    }
    const classArm = [sibling.classLabel, sibling.armLabel].filter(Boolean).join(' ');
    if (classArm) {
      parts.push(`— ${classArm}`);
    }
    return parts.join(' ');
  }
}
