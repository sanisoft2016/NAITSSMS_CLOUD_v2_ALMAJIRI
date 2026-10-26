import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { UserCredential } from '@shared/models/UserCredential';

import {
  AccountingApiService,
  AccountingDashboardSummary,
  AccountingSelectOption,
  formatNaira,
} from './accounting.service';
import { consumeAccountingFlashSuccess } from '../../theme/sidemenu/accounting-menu.config';

@Component({
  selector: 'app-accounting-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, MatProgressSpinnerModule],
  templateUrl: './accounting-dashboard.html',
  styleUrl: './accounting-dashboard.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingDashboard implements OnInit {
  private readonly api = inject(AccountingApiService);

  readonly loading = signal(true);
  readonly error = signal('');
  readonly flashSuccess = signal('');
  readonly summary = signal<AccountingDashboardSummary | null>(null);
  readonly sessions = signal<AccountingSelectOption[]>([]);
  readonly terms = signal<AccountingSelectOption[]>([]);
  readonly selectedSessionId = signal('');
  readonly selectedTermId = signal('');

  readonly isSuperAccountant = computed(() => {
    try {
      const raw = localStorage.getItem('credential');
      if (!raw || raw === 'null') {
        return false;
      }
      const credential = JSON.parse(raw) as UserCredential;
      const userName = String(credential.userName ?? '').trim();
      return userName.toLowerCase().includes('superaccountant');
    } catch {
      return false;
    }
  });

  readonly formatNaira = formatNaira;

  ngOnInit(): void {
    // Legacy ShowMessage survives redirect to StudentFessIndex.
    this.flashSuccess.set(consumeAccountingFlashSuccess());
    this.bootstrap();
  }

  dismissFlash(): void {
    this.flashSuccess.set('');
  }

  onSessionChange(sessionId: string): void {
    this.selectedSessionId.set(sessionId);
    this.loadTerms(sessionId, null);
  }

  onTermChange(termId: string): void {
    this.selectedTermId.set(termId);
    this.loadSummary(termId);
  }

  beginNewAcademicTerm(): void {
    window.alert(
      "This will lead to migrating to a new term which is not reversible. This action will be available once term-migration is wired in the cloud API.",
    );
  }

  private bootstrap(): void {
    this.loading.set(true);
    this.error.set('');

    this.api.getSessionsDropdown().subscribe({
      next: sessions => {
        const list = Array.isArray(sessions) ? sessions : [];
        this.sessions.set(list);
        const preferred = list.find(s => s.selected)?.value || list[0]?.value || '';
        this.selectedSessionId.set(preferred);

        this.api.getDashboardSummary().subscribe({
          next: summary => {
            this.summary.set(summary);
            const sessionId = summary.currentSessionId || preferred;
            if (sessionId) {
              this.selectedSessionId.set(sessionId);
              this.loadTerms(sessionId, summary.currentTermId ?? null, false);
            } else {
              this.loading.set(false);
            }
          },
          error: err => {
            this.error.set(err?.error?.message || err?.message || 'Failed to load accounting dashboard.');
            this.loading.set(false);
          },
        });
      },
      error: err => {
        this.error.set(err?.error?.message || err?.message || 'Failed to load sessions.');
        this.loading.set(false);
      },
    });
  }

  private loadTerms(sessionId: string, preferredTermId: string | null, reloadSummary = true): void {
    if (!sessionId) {
      this.terms.set([]);
      this.loading.set(false);
      return;
    }

    this.api.getTermsDropdown(sessionId).subscribe({
      next: terms => {
        const list = Array.isArray(terms) ? terms : [];
        this.terms.set(list);
        const termId =
          preferredTermId ||
          list.find(t => t.selected)?.value ||
          list[0]?.value ||
          '';
        this.selectedTermId.set(termId);
        if (reloadSummary && termId) {
          this.loadSummary(termId);
        } else {
          this.loading.set(false);
        }
      },
      error: err => {
        this.error.set(err?.error?.message || err?.message || 'Failed to load terms.');
        this.loading.set(false);
      },
    });
  }

  private loadSummary(termId: string): void {
    this.loading.set(true);
    this.error.set('');
    this.api.getDashboardSummary(termId).subscribe({
      next: summary => {
        this.summary.set(summary);
        this.loading.set(false);
      },
      error: err => {
        this.error.set(err?.error?.message || err?.message || 'Failed to load accounting dashboard.');
        this.loading.set(false);
      },
    });
  }
}
