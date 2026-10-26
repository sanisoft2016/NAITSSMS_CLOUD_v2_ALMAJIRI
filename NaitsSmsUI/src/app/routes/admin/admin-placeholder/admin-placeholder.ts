import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-admin-placeholder',
  imports: [RouterLink, MatButtonModule],
  template: `
    <section class="admin-placeholder">
      <h2>{{ pageTitle }}</h2>
      <p>This legacy registry screen has not been migrated to Angular yet.</p>
      <a mat-raised-button color="primary" routerLink="/admin">Back to Registry Home</a>
    </section>
  `,
  styles: `
    .admin-placeholder {
      display: grid;
      gap: 1rem;
      max-width: 36rem;
      padding: 1rem 0;
    }

    h2 {
      margin: 0;
      font-size: 1.35rem;
    }

    p {
      margin: 0;
      color: #64748b;
      line-height: 1.5;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminPlaceholder {
  private readonly route = inject(ActivatedRoute);

  readonly pageTitle = this.route.snapshot.data['pageTitle'] ?? 'Registry Page';
}
