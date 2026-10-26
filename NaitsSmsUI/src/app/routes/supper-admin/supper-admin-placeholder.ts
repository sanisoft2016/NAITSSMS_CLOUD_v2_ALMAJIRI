import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs';

@Component({
  selector: 'app-supper-admin-placeholder',
  template: `
    <section class="sa-placeholder">
      <h1 class="sa-placeholder__title">{{ title() }}</h1>
      <p class="sa-placeholder__lead">
        This Supper Admin page is ready for feature migration from the legacy MVC area.
      </p>
    </section>
  `,
  styles: `
    .sa-placeholder {
      padding: 1.25rem 1.5rem 2rem;
    }
    .sa-placeholder__title {
      margin: 0 0 0.5rem;
      font-size: 1.5rem;
      font-weight: 600;
      color: #333;
    }
    .sa-placeholder__lead {
      margin: 0;
      color: #555;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SupperAdminPlaceholder {
  private readonly route = inject(ActivatedRoute);

  readonly title = toSignal(
    this.route.data.pipe(map(data => String(data['title'] ?? 'Supper Admin'))),
    { initialValue: 'Supper Admin' },
  );
}
