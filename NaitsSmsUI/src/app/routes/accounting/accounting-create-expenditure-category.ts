import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { AccountingApiService } from './accounting.service';
import {
  writeAccountingFlashSuccess,
  writeAccountingOptionId,
  consumeAccountingFlashWarn,
} from '../../theme/sidemenu/accounting-menu.config';

@Component({
  selector: 'app-accounting-create-expenditure-category',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule],
  templateUrl: './accounting-create-expenditure-category.html',
  styleUrl: './accounting-create-expenditure-category.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingCreateExpenditureCategoryPage implements OnInit {
  private readonly api = inject(AccountingApiService);
  private readonly router = inject(Router);

  readonly categoryName = signal('');
  readonly saving = signal(false);
  readonly error = signal('');
  readonly warn = signal('');
  readonly fieldError = signal('');

  ngOnInit(): void {
    writeAccountingOptionId('2');
    const flashWarn = consumeAccountingFlashWarn();
    if (flashWarn) {
      this.warn.set(flashWarn);
    }
  }

  save(): void {
    const name = this.categoryName().trim();
    this.error.set('');
    this.warn.set('');
    this.fieldError.set('');

    if (!name) {
      this.fieldError.set('The Category Name field is required.');
      return;
    }
    if (name.length > 50) {
      this.fieldError.set('Category Name must be at most 50 characters.');
      return;
    }

    this.saving.set(true);
    this.api.createExpenditureCategory(name).subscribe({
      next: result => {
        this.saving.set(false);
        if (result?.succeeded) {
          writeAccountingFlashSuccess(
            result.message || 'Category has successfully been created.',
          );
          // Legacy RedirectToAction("ExpenditureCategoryIndex")
          void this.router.navigateByUrl('/accounting/view-expenditure-category');
          return;
        }
        this.warn.set(result?.message || 'Failed! This category already exist.');
      },
      error: err => {
        this.saving.set(false);
        this.error.set(
          err?.error?.message || err?.message || 'Failed to create expenditure category.',
        );
      },
    });
  }
}
