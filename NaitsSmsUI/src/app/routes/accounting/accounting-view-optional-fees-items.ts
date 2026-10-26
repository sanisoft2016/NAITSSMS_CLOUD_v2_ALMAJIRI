import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import {
  AccountingApiService,
  AccountingOptionalFeesCategory,
  AccountingOptionalFeesItem,
} from './accounting.service';
import {
  consumeAccountingFlashSuccess,
  writeAccountingOptionId,
} from '../../theme/sidemenu/accounting-menu.config';

@Component({
  selector: 'app-accounting-view-optional-fees-items',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule],
  templateUrl: './accounting-view-optional-fees-items.html',
  styleUrl: './accounting-view-optional-fees-items.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingViewOptionalFeesItemsPage implements OnInit {
  private readonly api = inject(AccountingApiService);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(true);
  readonly filtering = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly flashSuccess = signal('');

  readonly categories = signal<AccountingOptionalFeesCategory[]>([]);
  readonly selectedCategoryId = signal('');
  readonly items = signal<AccountingOptionalFeesItem[]>([]);
  /** When true, empty list uses the filtered-category message (legacy CategoryItemList). */
  readonly filteredByCategory = signal(false);

  readonly showEditModal = signal(false);
  readonly editId = signal('');
  readonly editCategoryId = signal('');
  readonly editTermId = signal('');
  readonly editItemName = signal('');
  readonly editCost = signal<number | null>(null);

  ngOnInit(): void {
    writeAccountingOptionId('5');
    this.flashSuccess.set(consumeAccountingFlashSuccess());
    this.bootstrap();
  }

  private bootstrap(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.getOptionalFeesCategories().subscribe({
      next: cats => {
        const list = Array.isArray(cats) ? cats : [];
        this.categories.set(list);
        if (list.length === 0) {
          this.categories.set([]);
        }

        const queryCategory = String(this.route.snapshot.queryParamMap.get('categoryId') ?? '').trim();
        // Legacy: dropdown shows categories, but initial table is ALL items unless whatToLoad is set.
        this.selectedCategoryId.set(queryCategory || list[0]?.id || '');

        if (queryCategory) {
          // Legacy document.ready → LoadCategoryFeeItems(whatToLoad)
          this.loadItems(queryCategory, true);
        } else {
          this.loadItems(undefined, false);
        }
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err?.error?.message || err?.message || 'Failed to load categories.');
      },
    });
  }

  onCategoryChange(categoryId: string): void {
    this.selectedCategoryId.set(categoryId);
    // Legacy #ItemCategoryId change → CategoryItemList
    this.loadItems(categoryId, true);
  }

  private loadItems(categoryId: string | undefined, filtered: boolean): void {
    if (this.loading()) {
      // keep loading spinner from bootstrap
    } else {
      this.filtering.set(true);
    }
    this.error.set('');
    this.filteredByCategory.set(filtered);

    this.api.getOptionalFeesItems(categoryId).subscribe({
      next: rows => {
        this.items.set(Array.isArray(rows) ? rows : []);
        this.loading.set(false);
        this.filtering.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.filtering.set(false);
        this.items.set([]);
        this.error.set(err?.error?.message || err?.message || 'Failed to load optional fees items.');
      },
    });
  }

  openEdit(item: AccountingOptionalFeesItem): void {
    this.editId.set(item.id);
    this.editCategoryId.set(item.itemCategoryId);
    this.editTermId.set(item.yearlyAndTermlyStatusId);
    this.editItemName.set(item.itemName);
    this.editCost.set(item.cost);
    this.showEditModal.set(true);
  }

  closeEditModal(): void {
    this.showEditModal.set(false);
  }

  saveChanges(): void {
    const name = this.editItemName().trim();
    const cost = Number(this.editCost() ?? 0);

    if (!name) {
      window.alert('Value for item name needs to be provided.');
      return;
    }
    if (cost === 0 || !Number.isFinite(cost)) {
      window.alert('Value for item cost needs to be provided.');
      return;
    }

    this.saving.set(true);
    this.api
      .updateOptionalFeesItem({
        id: this.editId(),
        itemCategoryId: this.editCategoryId(),
        yearlyAndTermlyStatusId: this.editTermId(),
        itemName: name,
        cost,
      })
      .subscribe({
        next: result => {
          this.saving.set(false);
          if (Number(result?.status) === 0) {
            window.alert(result.message || 'The changes has been succcessfully saved.');
            this.closeEditModal();
            this.reloadCurrentList();
            return;
          }
          window.alert(result?.message || 'Failed! Unable to complete the process.');
        },
        error: err => {
          this.saving.set(false);
          window.alert(err?.error?.message || err?.message || 'Failed! Unable to complete the process.');
        },
      });
  }

  deleteItem(item: AccountingOptionalFeesItem): void {
    if (
      !window.confirm(
        "Are you sure you want to delete this Optional-Fee-Item? If 'Yes' click 'Ok' to continue.",
      )
    ) {
      return;
    }

    this.api.deleteOptionalFeesItem(item.id).subscribe({
      next: result => {
        if (Number(result?.status) === 0) {
          window.alert(result.message || 'Record deleted Successfully.');
          this.reloadCurrentList();
          return;
        }
        window.alert(
          result?.message ||
            'Failed! The category has optional-fee item(s) to it and therefore, we can not delete it.',
        );
      },
      error: err => {
        window.alert(
          err?.error?.message ||
            err?.message ||
            'Failed! The category has optional-fee item(s) to it and therefore, we can not delete it.',
        );
      },
    });
  }

  private reloadCurrentList(): void {
    if (this.filteredByCategory()) {
      this.loadItems(this.selectedCategoryId(), true);
    } else {
      this.loadItems(undefined, false);
    }
  }

  formatCreatedDate(value: string): string {
    if (!value) {
      return '';
    }
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) {
      return value;
    }
    return d.toLocaleString();
  }
}
