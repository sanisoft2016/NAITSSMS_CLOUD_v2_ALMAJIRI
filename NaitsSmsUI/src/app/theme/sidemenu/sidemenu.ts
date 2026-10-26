import { NgClass } from '@angular/common';
import {
  Component,
  DestroyRef,
  OnDestroy,
  ViewEncapsulation,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatRippleModule } from '@angular/material/core';
import { MatIconModule } from '@angular/material/icon';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { NgxPermissionsModule } from 'ngx-permissions';
import { Subscription, filter } from 'rxjs';

import { AuthService } from '@core/authentication';
import { MenuService } from '@core';
import { GenericService } from '@shared';
import { UserCredential, isStudentOrParentRole, isTeacherLikeRole, normalizeCredentialRoles } from '@shared/models/UserCredential';
import {
  ACCOUNTING_OPTION_CHOICES,
  ACCOUNTING_SIDE_MENUS,
  AccountingOptionId,
  accountingMenuForOption,
  readAccountingOptionId,
  writeAccountingOptionId,
} from './accounting-menu.config';
import { COMMISSION_SIDE_MENU_ITEMS } from './commission-menu.config';
import { SAOFFICER_SIDE_MENU_ITEMS } from './saofficer-menu.config';
import { STUDENT_PARENT_SIDE_MENU_ITEMS } from './student-parent-menu.config';
import {
  SubordinateAccountantDetails,
  SubordinateAccountantDialog,
} from './subordinate-accountant-dialog';
import { TEACHER_SIDE_MENU_ITEMS } from './teacher-menu.config';

type SideMenuRole = 'teacher' | 'commission' | 'saofficer' | 'accounting' | 'student' | 'registry';

type SubordinateAccountantStatus = {
  schoolId?: string;
  SchoolId?: string;
  subordinateAccountantStatus?: boolean;
  SubordinateAccountantStatus?: boolean;
};

/** Default Matero sidenav width from `_reboot.scss`. */
const DEFAULT_SIDENAV_WIDTH_REM = 15;
/** Floor = default + one-third. */
const ACCOUNTING_SIDENAV_MIN_REM = DEFAULT_SIDENAV_WIDTH_REM * (4 / 3);
const ACCOUNTING_SIDENAV_MAX_REM = 45;
/** Icon + gap + sidebar padding (both sides) + a little slack. */
const ACCOUNTING_SIDENAV_CHROME_PX = 96;
/**
 * Accounting width scale:
 * - was +1/4 (×5/4)
 * - then reduce by 2/7 of that width → ×(5/7)
 * Net: ×(5/4)×(5/7) = ×(25/28)
 */
const ACCOUNTING_SIDENAV_WIDTH_BOOST = (5 / 4) * (5 / 7);

@Component({
  selector: 'app-sidemenu',
  templateUrl: './sidemenu.html',
  styleUrl: './sidemenu.scss',
  encapsulation: ViewEncapsulation.None,
  host: {
    '[class.teacher-sidemenu-host]': 'menuRole() === "teacher"',
    '[class.saofficer-sidemenu-host]': 'menuRole() === "saofficer"',
    '[class.accounting-sidemenu-host]': 'menuRole() === "accounting"',
    '[class.student-sidemenu-host]': 'menuRole() === "student"',
  },
  imports: [
    NgClass,
    FormsModule,
    RouterLink,
    RouterLinkActive,
    NgxPermissionsModule,
    MatIconModule,
    MatRippleModule,
    MatDialogModule,
    TranslateModule,
  ],
})
export class Sidemenu implements OnDestroy {
  readonly menu = inject(MenuService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly api = inject(GenericService<any>);
  private readonly dialog = inject(MatDialog);

  readonly teacherMenuItems = TEACHER_SIDE_MENU_ITEMS;
  readonly saOfficerMenuItems = SAOFFICER_SIDE_MENU_ITEMS;
  readonly commissionMenuItems = COMMISSION_SIDE_MENU_ITEMS;
  readonly studentParentMenuItems = STUDENT_PARENT_SIDE_MENU_ITEMS;
  readonly accountingOptionChoices = ACCOUNTING_OPTION_CHOICES;
  readonly menuRole = signal<SideMenuRole>('registry');
  readonly accountingOptionId = signal<AccountingOptionId>(readAccountingOptionId());
  readonly accountingMenuItems = computed(() => accountingMenuForOption(this.accountingOptionId()));

  /** Legacy: only when username contains "SuperAccountant". */
  readonly showSubordinateAccountantLink = signal(false);
  readonly subordinateAccountantExists = signal(false);
  readonly subordinateAccountantSchoolId = signal('');
  readonly subordinateAccountantBusy = signal(false);

  private navigationSubscription = Subscription.EMPTY;
  private authSubscription = Subscription.EMPTY;
  private measureCanvas: HTMLCanvasElement | null = null;

  constructor() {
    this.refreshRole();

    this.navigationSubscription = this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => this.refreshRole());

    this.authSubscription = this.auth.change().subscribe(() => this.refreshRole());

    effect(() => {
      if (this.menuRole() !== 'accounting') {
        this.resetSidenavWidth();
        this.showSubordinateAccountantLink.set(false);
        return;
      }
      // Recompute when option / menu set changes.
      void this.accountingMenuItems();
      this.applyAccountingSidenavWidth();
    });

    effect(() => {
      if (this.menuRole() !== 'accounting') {
        this.showSubordinateAccountantLink.set(false);
        return;
      }
      this.refreshSubordinateAccountantLink();
    });

    this.destroyRef.onDestroy(() => this.resetSidenavWidth());
  }

  ngOnDestroy(): void {
    this.navigationSubscription.unsubscribe();
    this.authSubscription.unsubscribe();
    this.resetSidenavWidth();
  }

  /**
   * Legacy Accounting `_Layout.cshtml` #optionType change:
   * - optionId "3" → navigate to Income Statement (AccountingBase/Index)
   * - always refresh side-nav content for the selected option
   */
  onAccountingOptionChange(rawValue: string): void {
    const optionId = this.normalizeOptionId(rawValue);
    writeAccountingOptionId(optionId);
    this.accountingOptionId.set(optionId);

    if (optionId === '3') {
      void this.router.navigateByUrl('/accounting/income-statement');
    }
  }

  private applyAccountingSidenavWidth(): void {
    const labels = [
      ...ACCOUNTING_OPTION_CHOICES.map(choice => choice.label),
      ...Object.values(ACCOUNTING_SIDE_MENUS).flatMap(items => items.map(item => item.label)),
    ];

    const textWidthPx = labels.reduce(
      (max, label) => Math.max(max, this.measureTextWidth(label)),
      0,
    );

    const neededRem = (textWidthPx + ACCOUNTING_SIDENAV_CHROME_PX) / 16;
    const baseRem = Math.max(ACCOUNTING_SIDENAV_MIN_REM, neededRem);
    const widthRem = Math.min(ACCOUNTING_SIDENAV_MAX_REM, baseRem * ACCOUNTING_SIDENAV_WIDTH_BOOST);

    this.setSidenavWidthCss(`${widthRem.toFixed(2)}rem`);
  }

  private resetSidenavWidth(): void {
    this.setSidenavWidthCss(`${DEFAULT_SIDENAV_WIDTH_REM}rem`);
  }

  private setSidenavWidthCss(width: string): void {
    document.documentElement.style.setProperty('--sidenav-width', width);
    // styles.scss also binds mat-sidenav to --app-sidenav-width
    document.documentElement.style.setProperty('--app-sidenav-width', width);
  }

  private measureTextWidth(text: string): number {
    if (typeof document === 'undefined') {
      return text.length * 9;
    }

    this.measureCanvas ??= document.createElement('canvas');
    const context = this.measureCanvas.getContext('2d');
    if (!context) {
      return text.length * 9;
    }

    context.font = '600 15.5px Roboto, "Helvetica Neue", Helvetica, Arial, sans-serif';
    return context.measureText(text).width;
  }

  private refreshRole(): void {
    const roles = this.readStoredRoles();
    const lower = roles.map(role => role.toLowerCase());
    if (lower.includes('commission')) {
      this.menuRole.set('commission');
      return;
    }
    if (lower.includes('saofficer')) {
      this.menuRole.set('saofficer');
      return;
    }
    if (lower.includes('accounting')) {
      this.menuRole.set('accounting');
      this.accountingOptionId.set(readAccountingOptionId());
      return;
    }
    if (roles.some(isTeacherLikeRole) && !lower.includes('registry')) {
      this.menuRole.set('teacher');
      return;
    }
    if (roles.some(isStudentOrParentRole)) {
      this.menuRole.set('student');
      return;
    }
    this.menuRole.set('registry');
  }

  private normalizeOptionId(rawValue: string): AccountingOptionId {
    if (
      rawValue === '0' ||
      rawValue === '1' ||
      rawValue === '2' ||
      rawValue === '3' ||
      rawValue === '4' ||
      rawValue === '5'
    ) {
      return rawValue;
    }
    return '0';
  }

  private readStoredRoles(): string[] {
    try {
      const storedCredential = localStorage.getItem('credential');
      if (!storedCredential || storedCredential === 'null') {
        return [];
      }

      const credential = JSON.parse(storedCredential) as UserCredential;
      return normalizeCredentialRoles(credential.role);
    } catch {
      return [];
    }
  }

  private refreshSubordinateAccountantLink(): void {
    if (!this.isSuperAccountantUser()) {
      this.showSubordinateAccountantLink.set(false);
      return;
    }

    this.showSubordinateAccountantLink.set(true);
    this.subordinateAccountantBusy.set(true);
    this.api.getAnObj<SubordinateAccountantStatus>('/user-registration/subordinate-accountant-status').subscribe({
      next: status => {
        const schoolId = String(status?.schoolId ?? status?.SchoolId ?? '').trim();
        const exists = !!(status?.subordinateAccountantStatus ?? status?.SubordinateAccountantStatus);
        this.subordinateAccountantSchoolId.set(schoolId);
        this.subordinateAccountantExists.set(exists);
        this.subordinateAccountantBusy.set(false);
      },
      error: () => {
        // Still show Create link even if status API is unavailable; the create page will resolve schoolId.
        this.subordinateAccountantExists.set(false);
        this.subordinateAccountantBusy.set(false);
      },
    });
  }

  private isSuperAccountantUser(): boolean {
    try {
      const storedCredential = localStorage.getItem('credential');
      if (!storedCredential || storedCredential === 'null') {
        return false;
      }

      const credential = JSON.parse(storedCredential) as UserCredential;
      return String(credential.userName ?? '')
        .trim()
        .toLowerCase()
        .includes('superaccountant');
    } catch {
      return false;
    }
  }

  createSubordinateAccountantQueryParams(): Record<string, string> {
    const schoolId = this.subordinateAccountantSchoolId().trim();
    return schoolId ? { schoolId } : {};
  }

  onViewSubordinateAccountant(): void {
    const schoolId = this.subordinateAccountantSchoolId().trim();
    if (!schoolId || this.subordinateAccountantBusy()) {
      return;
    }

    this.subordinateAccountantBusy.set(true);
    this.api
      .getAnObj<SubordinateAccountantDetails>(
        `/user-registration/subordinate-accountant-details/${encodeURIComponent(schoolId)}`,
      )
      .subscribe({
        next: details => {
          this.subordinateAccountantBusy.set(false);
          if (!details) {
            return;
          }

          this.dialog
            .open(SubordinateAccountantDialog, {
              width: '520px',
              maxWidth: '95vw',
              data: { schoolId, details },
              autoFocus: 'first-tabbable',
            })
            .afterClosed()
            .subscribe(changed => {
              if (changed) {
                this.refreshSubordinateAccountantLink();
              }
            });
        },
        error: () => {
          this.subordinateAccountantBusy.set(false);
        },
      });
  }

  faIconClasses(icon: string): string[] {
    return ['fa', `fa-${icon}`, 'legacy-icon', 'legacy-icon--fa'];
  }
}
