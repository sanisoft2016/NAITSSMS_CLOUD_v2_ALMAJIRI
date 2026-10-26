import { Routes } from '@angular/router';
import { authGuard } from '@core';
import { AdminLayout } from '@theme/admin-layout/admin-layout';
import { AuthLayout } from '@theme/auth-layout/auth-layout';
import { Dashboard } from './routes/admin/dashboard/dashboard';
import { Error403 } from './routes/sessions/error-403';
import { Error404 } from './routes/sessions/error-404';
import { Error500 } from './routes/sessions/error-500';
import { Login } from './routes/sessions/login/login';
import { RegisterStudentComponent } from './routes/sessions/register-student/register-student';
import { RegisterStudent2nd } from './routes/sessions/register-student2nd/register-student2nd';
import { RegisterTeacher } from './routes/sessions/register-teacher/register-teacher';
import { RegisterSchoolAdmin } from './routes/sessions/register-school-admin/register-school-admin';
import { AboutUs } from './routes/sessions/about-us/about-us';
import { ContactUs } from './routes/sessions/contact-us/contact-us';

export const routes: Routes = [
  {
    path: '',
    component: AdminLayout,
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: Dashboard },
      { path: '403', component: Error403 },
      { path: '404', component: Error404 },
      { path: '500', component: Error500 },
      {
        path: 'admin/other-registrations',
        loadComponent: () => import('./routes/admin/other-registrations/other-registrations').then(m => m.OtherRegistrations),
      },
      {
        path: 'admin/session-term-initialisation',
        loadComponent: () => import('./routes/admin/session-term-initialisation/session-term-initialisation').then(m => m.SessionTermInitialisation),
      },
      {
        path: 'admin/principal-index',
        loadComponent: () => import('./routes/admin/principal-index/principal-index').then(m => m.PrincipalIndex),
      },
      {
        path: 'admin/broadsheet-report',
        loadComponent: () => import('./routes/admin/broadsheet-report/broadsheet-report').then(m => m.BroadsheetReport),
      },
      {
        path: 'admin/new-assessment-handler',
        loadComponent: () => import('./routes/admin/new-assessment-handler/new-assessment-handler').then(m => m.NewAssessmentHandler),
      },

      {
        path: 'profile',
        loadChildren: () => import('./routes/profile/profile.routes').then(m => m.routes),
      },
      {
        path: 'permissions',
        loadChildren: () => import('./routes/permissions/permissions.routes').then(m => m.routes),
      },
      {
        path: 'admin',
        loadChildren: () => import('./routes/admin/routes').then(m => m.routes),
      },
      {
        path: 'teacher',
        loadChildren: () => import('./routes/teacher/routes').then(m => m.routes),
      },
      {
        path: 'commission',
        loadChildren: () => import('./routes/commission/routes').then(m => m.routes),
      },
      {
        path: 'supper-admin',
        loadChildren: () => import('./routes/supper-admin/routes').then(m => m.routes),
      },
      {
        path: 'accounting',
        loadChildren: () => import('./routes/accounting/routes').then(m => m.routes),
      },
      {
        path: 'student',
        loadChildren: () => import('./routes/student/routes').then(m => m.routes),
      },
    ],
  },
  {
    path: 'auth',
    component: AuthLayout,
    children: [
      { path: 'login', component: Login },
      { path: 'about', component: AboutUs },
      { path: 'contact', component: ContactUs },
      { path: 'register-teacher', component: RegisterTeacher },
      { path: 'register-student', component: RegisterStudentComponent },
      { path: 'register-student2nd', component: RegisterStudent2nd },
      { path: 'register-school-admin', component: RegisterSchoolAdmin },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
