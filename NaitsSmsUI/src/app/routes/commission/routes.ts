import { Routes } from '@angular/router';

import { CommissionDashboard } from '../supper-admin/commission-dashboard/commission-dashboard';
import { CommissionCentre } from './centre/commission-centre';
import { CommissionLearner } from './learner/commission-learner';
import { CommissionLearners } from './learners/commission-learners';
import { CommissionOutcomes } from './outcomes/commission-outcomes';

/** Commission officer area: programme oversight only, scoped server-side to programme centres. */
export const routes: Routes = [
  { path: '', component: CommissionDashboard, data: { title: 'Programme Overview' } },
  { path: 'outcomes', component: CommissionOutcomes, data: { title: 'Learning Outcomes' } },
  { path: 'learners', component: CommissionLearners, data: { title: 'Learner Records' } },
  { path: 'learners/:studentId', component: CommissionLearner, data: { title: 'Learner Record' } },
  { path: 'centres/:centreId', component: CommissionCentre, data: { title: 'Learning Centre' } },
];
