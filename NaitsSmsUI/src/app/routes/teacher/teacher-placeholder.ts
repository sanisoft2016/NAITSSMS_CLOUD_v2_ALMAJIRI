import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TeacherPageBase } from './teacher-page.base';

@Component({
  selector: 'app-teacher-placeholder',
  imports: [RouterLink],
  templateUrl: './teacher-placeholder.html',
  styleUrl: './teacher-dashboard.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeacherPlaceholder extends TeacherPageBase implements OnInit {
  pageTitle = 'Teacher';

  ngOnInit(): void {
    this.pageTitle = (this.route.snapshot.data['title'] as string) || 'Teacher';
  }
}
