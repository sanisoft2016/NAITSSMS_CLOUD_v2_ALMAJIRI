import { Routes } from '@angular/router';

import { StudentDashboard } from './student-dashboard';
import { StudentPortalResults } from './student-results';
import { StudentProfile } from './student-profile';
import { StudentSubjects } from './student-subjects';

export const routes: Routes = [
  { path: '', component: StudentDashboard },
  { path: 'subjects', component: StudentSubjects },
  { path: 'results', component: StudentPortalResults },
  { path: 'profile', component: StudentProfile },
];
