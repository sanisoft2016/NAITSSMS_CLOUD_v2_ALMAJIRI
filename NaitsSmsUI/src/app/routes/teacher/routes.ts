import { Routes } from '@angular/router';
import { AssignedArmAsFormMaster } from './assigned-arm-as-form-master/assigned-arm-as-form-master';
import { AssignedSubjectAndArm } from './assigned-subject-and-arm/assigned-subject-and-arm';
import { TeacherDashboard } from './teacher-dashboard';
import { TeacherPlaceholder } from './teacher-placeholder';

export const routes: Routes = [
  { path: '', component: TeacherDashboard },
  {
    path: 'assigned-subject-and-arm',
    component: AssignedSubjectAndArm,
    data: { title: 'Subject Teacher' },
  },
  {
    path: 'assigned-arm-as-form-master',
    component: AssignedArmAsFormMaster,
    data: { title: 'Class Teacher' },
  },
  {
    path: 'academic-terms',
    component: TeacherPlaceholder,
    data: { title: 'Academic Terms/Question' },
  },
  {
    path: 'search-question',
    component: TeacherPlaceholder,
    data: { title: 'Search Question' },
  },
  {
    path: 'set-question',
    component: TeacherPlaceholder,
    data: { title: 'Set Question' },
  },
];
