export interface LegacySideMenuItem {
  route: string;
  label: string;
  icon: string;
  iconSet?: 'material' | 'fa';
  exact?: boolean;
  nowrap?: boolean;
}

/** Legacy Teacher area side nav (Areas/Teacher/Views/Shared/_Layout.cshtml). */
export const TEACHER_SIDE_MENU_ITEMS: LegacySideMenuItem[] = [
  {
    route: '/teacher',
    label: 'My Dashboard',
    icon: 'dashboard',
    exact: true,
    nowrap: true,
  },
  {
    route: '/teacher/academic-terms',
    label: 'Academic Terms/Question',
    icon: 'calendar-check-o',
    iconSet: 'fa',
    nowrap: true,
  },
  {
    route: '/teacher/search-question',
    label: 'Search Question',
    icon: 'search',
    nowrap: true,
  },
  {
    route: '/teacher/set-question',
    label: 'Set Question',
    icon: 'pencil-square-o',
    iconSet: 'fa',
    nowrap: true,
  },
];
