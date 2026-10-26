import { LegacySideMenuItem } from './teacher-menu.config';

/**
 * Legacy StudentsAndParents Shared/_Layout.cshtml side-nav items.
 * Shown in the shared admin layout when role is StudentOrParent.
 */
export const STUDENT_PARENT_SIDE_MENU_ITEMS: LegacySideMenuItem[] = [
  {
    route: '/student',
    label: 'My Dashboard',
    icon: 'dashboard',
    exact: true,
    nowrap: true,
  },
  {
    route: '/student/subjects',
    label: 'Subject List and More',
    icon: 'menu_book',
    nowrap: true,
  },
  {
    route: '/student/results',
    label: 'View Results',
    icon: 'assessment',
    nowrap: true,
  },
  {
    route: '/student/profile',
    label: 'Student Profile',
    icon: 'person',
    nowrap: true,
  },
];
