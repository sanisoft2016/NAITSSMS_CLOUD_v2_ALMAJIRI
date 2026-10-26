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

import {
  AccountingApiService,
  AccountingOptionalFeesCategory,
} from './accounting.service';
import {
  writeAccountingFlashSuccess,
  writeAccountingFlashWarn,
  writeAccountingOptionId,
} from '../../theme/sidemenu/accounting-menu.config';

@Component({
  selector: 'app-accounting-create-optional-fees-item',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule],
  templateUrl: './accounting-create-optional-fees-item.html',
  styleUrl: './accounting-create-optional-fees-item.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingCreateOptionalFeesItemPage implements OnInit {
  private readonly api = inject(AccountingApiService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly warn = signal('');
  readonly itemNameError = signal('');

  readonly categories = signal<AccountingOptionalFeesCategory[]>([]);
  readonly itemCategoryId = signal('');
  readonly itemName = signal('');
  readonly cost = signal<number | null>(null);

  ngOnInit(): void {
    writeAccountingOptionId('5');
    this.loadForm();
  }

  private loadForm(): void {
    this.loading.set(true);
    this.error.set('');
    // Legacy RegisterOptionalFees GET → GetOptionalFeeCategoryListForDropdown
    this.api.getOptionalFeesCategories().subscribe({
      next: rows => {
        const list = Array.isArray(rows) ? rows : [];
        if (list.length === 0) {
          // Legacy: ShowMessage + RedirectToAction("CreateOptionalFeesCategory")
          writeAccountingFlashWarn(
            'Sorry! one or more Optional Fee category needs to be created first.',
          );
          void this.router.navigateByUrl('/accounting/create-optional-fees-category');
          return;
        }
        this.categories.set(list);
        this.itemCategoryId.set(list[0]?.id ?? '');
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(
          err?.error?.message || err?.message || 'Failed to load optional fee categories.',
        );
      },
    });
  }

  save(): void {
    this.error.set('');
    this.warn.set('');
    this.itemNameError.set('');

    const categoryId = this.itemCategoryId().trim();
    const name = this.itemName().trim();
    const costVal = Number(this.cost() ?? 0);

    if (!categoryId) {
      this.error.set('OPtional Fee Category is required.');
      return;
    }
    if (!name) {
      this.itemNameError.set('The Item Name field is required.');
      return;
    }
    if (name.length > 30) {
      this.itemNameError.set('Item Name must be at most 30 characters.');
      return;
    }
    if (!Number.isFinite(costVal)) {
      this.error.set('Cost must be a valid number.');
      return;
    }

    this.saving.set(true);
    this.api
      .createOptionalFeesItem({
        itemCategoryId: categoryId,
        itemName: name,
        cost: costVal,
      })
      .subscribe({
        next: result => {
          this.saving.set(false);
          const status = Number(result?.status);
          if (status === 0) {
            writeAccountingFlashSuccess(
              result.message || 'The item has successfully been saved.',
            );
            // Legacy RedirectToAction("OptionalFeesIndex")
            void this.router.navigateByUrl('/accounting/view-optional-fees-items');
            return;
          }
          if (status === 1) {
            this.warn.set(
              result?.message ||
                "All terms' status are 'previous'. Run new assessment preparation before this.",
            );
            return;
          }
          if (status === 2) {
            this.itemNameError.set(
              result?.message || 'Failed! The Item name you have entered already exist.',
            );
            return;
          }
          this.error.set(result?.message || 'Failed to create optional fees item.');
        },
        error: err => {
          this.saving.set(false);
          this.error.set(
            err?.error?.message || err?.message || 'Failed to create optional fees item.',
          );
        },
      });
  }
}
