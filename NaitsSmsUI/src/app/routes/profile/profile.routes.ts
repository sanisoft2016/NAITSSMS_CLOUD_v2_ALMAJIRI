import { Routes } from '@angular/router';

import { ProfileLayout } from './layout/layout';
import { ProfileOverview } from './overview/overview';
import { ChangeProfileImage } from './change-profile-image/change-profile-image';

export const routes: Routes = [
  {
    path: '',
    component: ProfileLayout,
    children: [
      { path: '', redirectTo: 'overview', pathMatch: 'full' },
      { path: 'overview', component: ProfileOverview },
      { path: 'settings', redirectTo: 'change-profile-image', pathMatch: 'full' },
      { path: 'change-profile-image', component: ChangeProfileImage },
      { path: 'change-password', redirectTo: 'overview', pathMatch: 'full' },
    ],
  },
];
