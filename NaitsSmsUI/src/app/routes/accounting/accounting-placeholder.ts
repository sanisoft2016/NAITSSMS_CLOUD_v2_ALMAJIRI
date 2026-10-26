import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs';

import { consumeAccountingFlashSuccess, consumeAccountingFlashWarn } from '../../theme/sidemenu/accounting-menu.config';

@Component({
  selector: 'app-accounting-placeholder',
  template: `
    <section class="accounting-placeholder">
      @if (flashSuccess()) {
        <div class="accounting-placeholder__flash accounting-placeholder__flash--success" role="status">
          {{ flashSuccess() }}
        </div>
      }
      @if (flashWarn()) {
        <div class="accounting-placeholder__flash accounting-placeholder__flash--warn" role="status">
          {{ flashWarn() }}
        </div>
      }
      <h1 class="accounting-placeholder__title">{{ title() }}</h1>
      <p class="accounting-placeholder__lead">
        This Accounting page is ready for feature migration from the legacy MVC Accounting area.
      </p>
    </section>
  `,
  styles: `
    .accounting-placeholder {
      padding: 1.25rem 1.5rem 2rem;
    }
    .accounting-placeholder__flash {
      margin: 0 0 1rem;
      padding: 0.85rem 1rem;
      border-radius: 4px;
    }
    .accounting-placeholder__flash--success {
      color: #3c763d;
      background: #dff0d8;
      border: 1px solid #d6e9c6;
    }
    .accounting-placeholder__flash--warn {
      color: #8a6d3b;
      background: #fcf8e3;
      border: 1px solid #faebcc;
    }
    .accounting-placeholder__title {
      margin: 0 0 0.5rem;
      font-size: 1.5rem;
      font-weight: 600;
      color: #333;
    }
    .accounting-placeholder__lead {
      margin: 0;
      color: #555;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingPlaceholder implements OnInit {
  private readonly route = inject(ActivatedRoute);

  readonly flashSuccess = signal('');
  readonly flashWarn = signal('');

  readonly title = toSignal(
    this.route.data.pipe(map(data => String(data['title'] ?? 'Accounting'))),
    { initialValue: 'Accounting' },
  );

  ngOnInit(): void {
    this.flashSuccess.set(consumeAccountingFlashSuccess());
    this.flashWarn.set(consumeAccountingFlashWarn());
  }
}
