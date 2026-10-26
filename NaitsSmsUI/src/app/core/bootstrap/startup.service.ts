import { Injectable, inject } from '@angular/core';
import { AuthService, User } from '@core/authentication';
import { NgxPermissionsService, NgxRolesService } from 'ngx-permissions';
import { catchError, firstValueFrom, of, take, tap, timeout } from 'rxjs';
import { Menu, MenuService } from './menu.service';
import {
  credentialHasRole,
  credentialHasTeacherAccess,
  normalizeCredentialRoles,
} from '@shared/models/UserCredential';

const REGISTRY_MENU: Menu[] = [
  {
    route: 'admin',
    name: 'Admin',
    type: 'sub',
    icon: 'school',
    permissions: { only: ['ADMIN', 'Registry'] },
    children: [
      { route: 'class-reg-index', name: 'Classes', type: 'link' },
    ],
  },
];

const TEACHER_MENU: Menu[] = [
  {
    route: 'teacher',
    name: 'My Dashboard',
    type: 'link',
    icon: 'dashboard',
    permissions: { only: ['Teacher', 'CbtTeacher'] },
  },
  {
    route: 'teacher/academic-terms',
    name: 'Academic Terms/Question',
    type: 'link',
    icon: 'calendar_month',
    permissions: { only: ['Teacher', 'CbtTeacher'] },
  },
  {
    route: 'teacher/search-question',
    name: 'Search Question',
    type: 'link',
    icon: 'search',
    permissions: { only: ['Teacher', 'CbtTeacher'] },
  },
  {
    route: 'teacher/set-question',
    name: 'Set Question',
    type: 'link',
    icon: 'quiz',
    permissions: { only: ['Teacher', 'CbtTeacher'] },
  },
];

const SAOFFICER_MENU: Menu[] = [
  {
    route: 'supper-admin',
    name: 'My Dashboard',
    type: 'link',
    icon: 'dashboard',
    permissions: { only: ['SAOfficer'] },
  },
  {
    route: 'auth/register-school-admin',
    name: 'Create School',
    type: 'link',
    icon: 'add_business',
    permissions: { only: ['SAOfficer'] },
  },
  {
    route: 'supper-admin/school-list',
    name: 'View School List',
    type: 'link',
    icon: 'list',
    permissions: { only: ['SAOfficer'] },
  },
  {
    route: 'supper-admin/register-agents',
    name: 'Register Agents',
    type: 'link',
    icon: 'person_add',
    permissions: { only: ['SAOfficer'] },
  },
  {
    route: 'supper-admin/agent-list',
    name: 'View Agent List',
    type: 'link',
    icon: 'groups',
    permissions: { only: ['SAOfficer'] },
  },
];

const COMMISSION_MENU: Menu[] = [
  {
    route: 'commission',
    name: 'Programme Overview',
    type: 'link',
    icon: 'assessment',
    permissions: { only: ['Commission'] },
  },
  {
    route: 'commission/outcomes',
    name: 'Learning Outcomes',
    type: 'link',
    icon: 'school',
    permissions: { only: ['Commission'] },
  },
  {
    route: 'commission/learners',
    name: 'Learner Records',
    type: 'link',
    icon: 'badge',
    permissions: { only: ['Commission'] },
  },
];

const ACCOUNTING_MENU: Menu[] = [
  {
    route: 'accounting',
    name: 'Accounting',
    type: 'link',
    icon: 'payments',
    permissions: { only: ['Accounting'] },
  },
];

@Injectable({
  providedIn: 'root',
})
export class StartupService {
  private readonly authService = inject(AuthService);
  private readonly menuService = inject(MenuService);
  private readonly permissonsService = inject(NgxPermissionsService);
  private readonly rolesService = inject(NgxRolesService);

  /**
   * Load the application only after getting essential information such as
   * permissions and roles. Menu is served statically — no backend call needed.
   *
   * Must always settle: a hanging /user call used to leave /auth/login stuck on
   * the LOADING splash after a hard refresh.
   */
  load() {
    return firstValueFrom(
      this.authService.change().pipe(
        take(1),
        tap(user => this.setPermissions(user)),
        timeout({ first: 4000 }),
        catchError(() => of({} as User)),
      ),
    ).then(user => {
      this.setPermissions(user);
      this.setMenu(this.resolveMenuForRole());
    });
  }

  private setMenu(menu: Menu[]) {
    this.menuService.addNamespace(menu, 'menu');
    this.menuService.set(menu);
  }

  private resolveMenuForRole(): Menu[] {
    try {
      const storedCredential = localStorage.getItem('credential');
      if (storedCredential && storedCredential !== 'null') {
        const credential = JSON.parse(storedCredential) as { role?: string | string[] };
        if (credentialHasRole(credential, 'Commission')) {
          return COMMISSION_MENU;
        }
        if (credentialHasRole(credential, 'SAOfficer')) {
          return SAOFFICER_MENU;
        }
        if (credentialHasRole(credential, 'Accounting')) {
          return ACCOUNTING_MENU;
        }
        if (
          credentialHasTeacherAccess(credential) &&
          !credentialHasRole(credential, 'Registry')
        ) {
          return TEACHER_MENU;
        }
      }
    } catch {
      // fall through to registry menu
    }

    return REGISTRY_MENU;
  }

  private setPermissions(user: User) {
    const permissions = ['canAdd', 'canDelete', 'canEdit', 'canRead'];
    this.permissonsService.loadPermissions(permissions);
    this.rolesService.flushRoles();

    const rolesMap: Record<string, string[]> = {
      ADMIN: permissions,
      Registry: permissions,
      Teacher: permissions,
      CbtTeacher: permissions,
      SAOfficer: permissions,
      Commission: permissions,
      Accounting: permissions,
    };

    try {
      const storedCredential = localStorage.getItem('credential');
      if (storedCredential && storedCredential !== 'null') {
        const credential = JSON.parse(storedCredential) as { role?: string | string[] };
        for (const role of normalizeCredentialRoles(credential.role)) {
          rolesMap[role] = permissions;
        }
      }
    } catch {
      // keep default roles
    }

    this.rolesService.addRoles(rolesMap);
  }
}
