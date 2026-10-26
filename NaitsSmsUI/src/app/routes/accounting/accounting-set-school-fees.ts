import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import {
  AccountingApiService,
  AccountingSetSchoolFeesForm,
  applyFormatAsCurrency,
} from './accounting.service';
import {
  consumeAccountingFlashWarn,
  writeAccountingFlashSuccess,
  writeAccountingOptionId,
} from '../../theme/sidemenu/accounting-menu.config';

type FeeFieldKey = keyof Pick<
  AccountingSetSchoolFeesForm,
  | 'freshPlayClass'
  | 'freshPreNursery'
  | 'freshNursery'
  | 'freshPrimary'
  | 'freshJss'
  | 'freshScience'
  | 'freshArt'
  | 'freshCommercial'
  | 'retnPlayClass'
  | 'retnPreNursery'
  | 'retnNursery'
  | 'retnPrimary'
  | 'retnJss'
  | 'retnScience'
  | 'retnArt'
  | 'retnCommercial'
>;

const EMPTY_FORM: AccountingSetSchoolFeesForm = {
  id: '',
  schoolId: '',
  yearlyAndTermlyStatusId: '',
  freshPlayClass: '',
  freshPreNursery: '',
  freshNursery: '',
  freshPrimary: '',
  freshJss: '',
  freshScience: '',
  freshArt: '',
  freshCommercial: '',
  retnPlayClass: '',
  retnPreNursery: '',
  retnNursery: '',
  retnPrimary: '',
  retnJss: '',
  retnScience: '',
  retnArt: '',
  retnCommercial: '',
};

const FRESH_FIELDS: { key: FeeFieldKey; label: string }[] = [
  { key: 'freshPlayClass', label: 'Play Class ₦' },
  { key: 'freshPreNursery', label: 'Pre Nursery ₦' },
  { key: 'freshNursery', label: 'Nursery ₦' },
  { key: 'freshPrimary', label: 'Primary ₦' },
  { key: 'freshJss', label: 'Jss ₦' },
  { key: 'freshScience', label: 'Science ₦' },
  { key: 'freshArt', label: 'Art ₦' },
  { key: 'freshCommercial', label: 'Commercial ₦' },
];

const RETN_FIELDS: { key: FeeFieldKey; label: string }[] = [
  { key: 'retnPlayClass', label: 'Play Class ₦' },
  { key: 'retnPreNursery', label: 'Pre Nursery ₦' },
  { key: 'retnNursery', label: 'Nursery ₦' },
  { key: 'retnPrimary', label: 'Primary ₦' },
  { key: 'retnJss', label: 'Jss ₦' },
  { key: 'retnScience', label: 'Science ₦' },
  { key: 'retnArt', label: 'Art ₦' },
  { key: 'retnCommercial', label: 'Commercial ₦' },
];

@Component({
  selector: 'app-accounting-set-school-fees',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule],
  templateUrl: './accounting-set-school-fees.html',
  styleUrl: './accounting-set-school-fees.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingSetSchoolFeesPage implements OnInit {
  private readonly api = inject(AccountingApiService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly hasCurrentTerm = signal(false);
  readonly form = signal<AccountingSetSchoolFeesForm>({ ...EMPTY_FORM });

  readonly freshFields = FRESH_FIELDS;
  readonly retnFields = RETN_FIELDS;

  ngOnInit(): void {
    writeAccountingOptionId('0');
    const warn = consumeAccountingFlashWarn();
    if (warn) {
      this.error.set(warn);
    }
    this.loading.set(true);
    this.api.getSetSchoolFees().subscribe({
      next: load => {
        this.hasCurrentTerm.set(!!load.hasCurrentTerm);
        this.form.set(load.fees ? { ...EMPTY_FORM, ...load.fees } : { ...EMPTY_FORM });
        this.loading.set(false);
      },
      error: err => {
        this.error.set(err?.error?.message || err?.message || 'Failed to load school fees settings.');
        this.loading.set(false);
      },
    });
  }

  onFeeKeyup(key: FeeFieldKey, raw: string): void {
    const formatted = applyFormatAsCurrency(raw);
    this.form.update(f => ({ ...f, [key]: formatted }));
  }

  /**
   * Legacy submitForm(): strip commas from .numericField, POST, then re-format.
   * On status 4 → redirect to StudentFessIndex (/accounting).
   */
  submitForm(): void {
    if (this.saving()) {
      return;
    }

    const current = this.form();
    const stripped: AccountingSetSchoolFeesForm = { ...current };
    for (const field of [...FRESH_FIELDS, ...RETN_FIELDS]) {
      stripped[field.key] = String(current[field.key] ?? '').replace(/,/g, '');
    }

    this.saving.set(true);
    this.error.set('');
    this.success.set('');

    this.api.saveSetSchoolFees(stripped).subscribe({
      next: result => {
        this.saving.set(false);
        if (result.status === 4) {
          const message = result.message || 'Changes has successfully been effected.';
          writeAccountingFlashSuccess(message);
          void this.router.navigateByUrl('/accounting');
          return;
        }

        this.error.set(result.message || 'Failed to save school fees.');
        this.reformatAfterSubmit(stripped);
      },
      error: err => {
        this.saving.set(false);
        const body = err?.error;
        this.error.set(
          body?.message ||
            body?.Message ||
            (typeof body === 'string' ? body : null) ||
            err?.message ||
            'Failed to save school fees settings.',
        );
        this.reformatAfterSubmit(stripped);
      },
    });
  }

  private reformatAfterSubmit(stripped: AccountingSetSchoolFeesForm): void {
    const reformatted = { ...stripped };
    for (const field of [...FRESH_FIELDS, ...RETN_FIELDS]) {
      const entry = String(stripped[field.key] ?? '');
      if (!entry) {
        reformatted[field.key] = '';
        continue;
      }
      const entryToInt = parseInt(entry, 10);
      if (!Number.isFinite(entryToInt)) {
        reformatted[field.key] = entry;
        continue;
      }
      reformatted[field.key] = entryToInt.toFixed(2).replace(/(\d)(?=(\d\d\d)+(?!\d))/g, '$1,');
    }
    this.form.set(reformatted);
  }

  fieldValue(key: FeeFieldKey): string {
    return this.form()[key] ?? '';
  }
}
