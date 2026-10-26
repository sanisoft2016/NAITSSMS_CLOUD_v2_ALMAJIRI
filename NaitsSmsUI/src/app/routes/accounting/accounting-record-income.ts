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
import { MatDatepickerModule } from '@angular/material/datepicker';

import {
  AccountingApiService,
  AccountingSelectOption,
  applyFormatAsCurrency,
} from './accounting.service';
import {
  consumeAccountingFlashWarn,
  writeAccountingFlashSuccess,
  writeAccountingFlashWarn,
  writeAccountingIncomeConfirmNeeded,
  writeAccountingOptionId,
} from '../../theme/sidemenu/accounting-menu.config';

@Component({
  selector: 'app-accounting-record-income',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule, MatDatepickerModule],
  templateUrl: './accounting-record-income.html',
  styleUrl: './accounting-record-income.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingRecordIncomePage implements OnInit {
  private readonly api = inject(AccountingApiService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly warn = signal('');
  readonly fieldError = signal('');

  readonly dateDisplay = signal('');
  readonly weekPosition = signal('');
  readonly categories = signal<AccountingSelectOption[]>([]);

  readonly capturedDate = signal<Date | null>(null);
  readonly itemCategoryId = signal('');
  readonly details = signal('');
  readonly costDisplay = signal('');

  readonly applyFormatAsCurrency = applyFormatAsCurrency;

  ngOnInit(): void {
    writeAccountingOptionId('4');
    const flashWarn = consumeAccountingFlashWarn();
    if (flashWarn) {
      this.warn.set(flashWarn);
    }
    this.loadForm();
  }

  onCostInput(raw: string): void {
    this.costDisplay.set(applyFormatAsCurrency(raw));
    this.fieldError.set('');
  }

  onCapturedDateChange(value: Date | null): void {
    this.capturedDate.set(value);
    this.fieldError.set('');
  }

  save(): void {
    this.error.set('');
    this.warn.set('');
    this.fieldError.set('');

    const categoryId = this.itemCategoryId().trim();
    const dateObj = this.capturedDate();
    const details = this.details().trim();
    const costRaw = this.costDisplay().replace(/,/g, '');
    const cost = Number(costRaw);

    if (!dateObj) {
      this.fieldError.set("The Item' Date field is required.");
      return;
    }
    if (!categoryId) {
      this.fieldError.set('Income Category is required.');
      return;
    }
    if (details.length > 150) {
      this.fieldError.set('Details must be at most 150 characters.');
      return;
    }
    if (!Number.isFinite(cost)) {
      this.fieldError.set('The Cost field is required.');
      return;
    }

    const dateVal = toIsoDateOnly(dateObj);

    this.saving.set(true);
    this.api
      .createIncome({
        itemCategoryId: categoryId,
        capturedDate: dateVal,
        details,
        cost,
      })
      .subscribe({
        next: result => {
          this.saving.set(false);
          const status = Number(result?.status);
          if (status === 0) {
            writeAccountingFlashSuccess(
              result.message || 'The item has successfully been saved.',
            );
            // Legacy RedirectToAction("IncomeCapturingIndex")
            void this.router.navigateByUrl('/accounting/view-income');
            return;
          }
          if (status === 1) {
            this.warn.set(
              result?.message ||
                'Sorry! You have already entered and closed Income entering within the selected period.',
            );
            return;
          }
          if (status === 2) {
            writeAccountingFlashWarn(
              result?.message ||
                'Failed! You need to first confirm the list of recent Income before you can add new one.',
            );
            writeAccountingIncomeConfirmNeeded();
            // Legacy RedirectToAction("IncomeCapturingIndex") + TempData direction
            void this.router.navigateByUrl('/accounting/view-income');
            return;
          }
          this.error.set(result?.message || 'Failed to save income item.');
        },
        error: err => {
          this.saving.set(false);
          this.error.set(
            err?.error?.message || err?.message || 'Failed to save income item.',
          );
        },
      });
  }

  private loadForm(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.getRegisterIncomeForm().subscribe({
      next: form => {
        if (!form?.hasCategories) {
          writeAccountingFlashWarn(
            form?.message ||
              'Sorry! one or more income category needs to be created first.',
          );
          void this.router.navigateByUrl('/accounting/create-income-category');
          return;
        }

        this.dateDisplay.set(form.dateDisplay || '');
        this.weekPosition.set(form.weekPosition || '');
        this.categories.set(form.categories ?? []);
        this.itemCategoryId.set(form.categories?.[0]?.value ?? '');
        this.capturedDate.set(parseLocalDate(form.capturedDate));
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(
          err?.error?.message || err?.message || 'Failed to load income form.',
        );
      },
    });
  }
}

function parseLocalDate(value: string | Date | null | undefined): Date {
  if (value instanceof Date && !Number.isNaN(value.getTime())) {
    return new Date(value.getFullYear(), value.getMonth(), value.getDate());
  }
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}/.test(value)) {
    const [y, m, d] = value.slice(0, 10).split('-').map(Number);
    return new Date(y, (m ?? 1) - 1, d ?? 1);
  }
  const date = value ? new Date(value) : new Date();
  if (Number.isNaN(date.getTime())) {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), now.getDate());
  }
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

function toIsoDateOnly(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}
