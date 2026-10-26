import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

import { PageHeader } from '@shared';

@Component({
  selector: 'app-profile-layout',
  templateUrl: './layout.html',
  styleUrl: './layout.scss',
  imports: [RouterOutlet, PageHeader, TranslateModule],
})
export class ProfileLayout {}
