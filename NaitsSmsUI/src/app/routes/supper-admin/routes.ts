import { Routes } from '@angular/router';

import { SupperAdminDashboard } from './supper-admin-dashboard';
import { SupperAdminPlaceholder } from './supper-admin-placeholder';
import { SchoolList } from './school-list/school-list';
import { CreateAdminAccountant } from './create-admin-accountant/create-admin-accountant';
import { CreateSubordinateAccountant } from './create-subordinate-accountant/create-subordinate-accountant';
import { EditSchoolRegistrar } from './edit-school-registrar/edit-school-registrar';
import { LicensingIndex } from './licensing-index/licensing-index';
import { CommissionDashboard } from './commission-dashboard/commission-dashboard';

export const routes: Routes = [
  { path: '', component: SupperAdminDashboard },
  {
    path: 'commission-dashboard',
    component: CommissionDashboard,
    data: { title: 'Commission Oversight' },
  },
  {
    path: 'school-list',
    component: SchoolList,
    data: { title: 'View School List' },
  },
  {
    path: 'create-admin-accountant',
    component: CreateAdminAccountant,
    data: { title: 'Create Admin Accountant' },
  },
  {
    path: 'create-subordinate-accountant',
    component: CreateSubordinateAccountant,
    data: { title: 'Create Subordinate Accountant' },
  },
  {
    path: 'edit-school-registrar',
    component: EditSchoolRegistrar,
    data: { title: 'Edit School Registrar' },
  },
  {
    path: 'school-sms-index/:id',
    component: SupperAdminPlaceholder,
    data: { title: 'SMS and More' },
  },
  {
    path: 'licensing-index/:id',
    component: LicensingIndex,
    data: { title: 'Licensing' },
  },
  {
    path: 'register-agents',
    component: SupperAdminPlaceholder,
    data: { title: 'Register Agents' },
  },
  {
    path: 'agent-list',
    component: SupperAdminPlaceholder,
    data: { title: 'View Agent List' },
  },
  {
    path: 'profile',
    component: SupperAdminPlaceholder,
    data: { title: 'View Profile' },
  },
];
