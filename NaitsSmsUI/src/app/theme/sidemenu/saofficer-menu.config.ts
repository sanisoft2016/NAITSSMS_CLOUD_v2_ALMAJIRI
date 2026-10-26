import { LegacySideMenuItem } from './teacher-menu.config';

/**
 * Legacy SupperAdmin Shared/_Layout.cshtml side-nav items.
 * Shown in the shared admin layout when role is SAOfficer.
 */
export const SAOFFICER_SIDE_MENU_ITEMS: LegacySideMenuItem[] = [
  {
    route: '/supper-admin',
    label: 'My Dashboard',
    icon: 'dashboard',
    exact: true,
    nowrap: true,
  },
  {
    route: '/supper-admin/commission-dashboard',
    label: 'Commission Oversight',
    icon: 'insights',
    nowrap: true,
  },
  {
    route: '/auth/register-school-admin',
    label: 'Create School',
    icon: 'add_business',
    nowrap: true,
  },
  {
    route: '/supper-admin/school-list',
    label: 'View School List',
    icon: 'list',
    nowrap: true,
  },
  {
    route: '/auth/login',
    label: 'Login As School Registra',
    icon: 'sign-in',
    iconSet: 'fa',
    nowrap: true,
  },
  {
    route: '/supper-admin/register-agents',
    label: 'Register Agents',
    icon: 'person_add',
    nowrap: true,
  },
  {
    route: '/supper-admin/agent-list',
    label: 'View Agent List',
    icon: 'groups',
    nowrap: true,
  },
];
