import { LegacySideMenuItem } from './teacher-menu.config';

/**
 * Side navigation for Commission officers. Programme oversight only: no school creation,
 * licensing or agent screens, which belong to the platform operator (SAOfficer).
 */
export const COMMISSION_SIDE_MENU_ITEMS: LegacySideMenuItem[] = [
  {
    route: '/commission',
    label: 'Programme Overview',
    icon: 'assessment',
    exact: true,
    nowrap: true,
  },
  {
    route: '/commission/outcomes',
    label: 'Learning Outcomes',
    icon: 'school',
    nowrap: true,
  },
  {
    route: '/commission/learners',
    label: 'Learner Records',
    icon: 'badge',
    nowrap: true,
  },
];
