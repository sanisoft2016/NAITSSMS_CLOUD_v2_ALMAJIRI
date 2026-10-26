import { NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

type TeacherQuickLinkAccent = 'success' | 'primary';

type TeacherQuickLink = {
  id: string;
  title: string;
  description: string;
  icon: string;
  accent: TeacherQuickLinkAccent;
  route: string;
};

@Component({
  selector: 'app-teacher-dashboard',
  imports: [NgClass, RouterLink],
  templateUrl: './teacher-dashboard.html',
  styleUrl: './teacher-dashboard.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeacherDashboard {
  /** Legacy Teacher/Index.cshtml navigation tiles. */
  readonly quickLinks: TeacherQuickLink[] = [
    {
      id: 'subject-teacher',
      title: 'Subject Teacher',
      description: 'Assigned Subjects / Classes-Arm as a Subject-teacher',
      icon: 'list-alt',
      accent: 'success',
      route: '/teacher/assigned-subject-and-arm',
    },
    {
      id: 'class-teacher',
      title: 'Class Teacher',
      description: 'Assigned Classes as a Form-master',
      icon: 'list-ul',
      accent: 'primary',
      route: '/teacher/assigned-arm-as-form-master',
    },
  ];
}
