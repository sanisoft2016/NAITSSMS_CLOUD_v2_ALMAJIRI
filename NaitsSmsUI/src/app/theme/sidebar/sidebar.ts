import { Component, ViewEncapsulation, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatToolbarModule } from '@angular/material/toolbar';

import { Sidemenu } from '../sidemenu/sidemenu';
import { Branding } from '../widgets/branding';
import { StudentSiblingSwitcher } from './student-sibling-switcher';
import { UserPanel } from './user-panel';

@Component({
  selector: 'app-sidebar',
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.scss',
  encapsulation: ViewEncapsulation.None,
  imports: [
    MatSlideToggleModule,
    MatIconModule,
    MatButtonModule,
    MatToolbarModule,
    Branding,
    Sidemenu,
    UserPanel,
    StudentSiblingSwitcher,
  ],
})
export class Sidebar {
  readonly showToggle = input(true);
  readonly showUser = input(true);
  readonly showHeader = input(true);
  readonly toggleChecked = input(false);

  readonly toggleCollapsed = output<void>();
  readonly closeSidenav = output<void>();
}
