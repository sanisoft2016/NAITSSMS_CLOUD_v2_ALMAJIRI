import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import {
  AccountingApiService,
  AccountingOptionalFeesCategory,
} from './accounting.service';
import {
  consumeAccountingFlashSuccess,
  writeAccountingOptionId,
} from '../../theme/sidemenu/accounting-menu.config';

@Component({
  selector: 'app-accounting-view-optional-fees-category',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule],
  templateUrl: './accounting-view-optional-fees-category.html',
  styleUrl: './accounting-view-optional-fees-category.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingViewOptionalFeesCategoryPage implements OnInit {
  private readonly api = inject(AccountingApiService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly flashSuccess = signal('');
  readonly categories = signal<AccountingOptionalFeesCategory[]>([]);

  readonly showEditModal = signal(false);
  readonly editId = signal('');
  readonly editSchoolId = signal('');
  readonly editCategoryName = signal('');

  ngOnInit(): void {
    writeAccountingOptionId('5');
    this.flashSuccess.set(consumeAccountingFlashSuccess());
    this.loadCategories();
  }

  loadCategories(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.getOptionalFeesCategories().subscribe({
      next: rows => {
        this.categories.set(Array.isArray(rows) ? rows : []);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.categories.set([]);
        this.error.set(err?.error?.message || err?.message || 'Failed to load categories.');
      },
    });
  }

  openEdit(item: AccountingOptionalFeesCategory): void {
    this.editId.set(item.id);
    this.editSchoolId.set(item.schoolId);
    this.editCategoryName.set(item.categoryName);
    this.showEditModal.set(true);
  }

  closeEditModal(): void {
    this.showEditModal.set(false);
    this.editId.set('');
    this.editSchoolId.set('');
    this.editCategoryName.set('');
  }

  saveCategoryChanges(): void {
    const name = this.editCategoryName().trim();
    if (name.length > 50) {
      window.alert('Category length can not be more than 50 letters long.');
      return;
    }

    this.saving.set(true);
    this.api
      .updateOptionalFeesCategory({
        id: this.editId(),
        schoolId: this.editSchoolId(),
        categoryName: name,
      })
      .subscribe({
        next: result => {
          this.saving.set(false);
          if (Number(result?.status) === 0) {
            window.alert(result.message || 'The changes has been succcessfully saved.');
            this.closeEditModal();
            this.loadCategories();
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

  deleteCategory(item: AccountingOptionalFeesCategory): void {
    if (
      !window.confirm(
        "Are you sure you want to delete this category? If 'Yes' click 'Ok' to continue.",
      )
    ) {
      return;
    }

    this.api.deleteOptionalFeesCategory(item.id).subscribe({
      next: result => {
        if (Number(result?.status) === 0) {
          window.alert(result.message || 'Record deleted Successfully.');
          this.loadCategories();
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
}
