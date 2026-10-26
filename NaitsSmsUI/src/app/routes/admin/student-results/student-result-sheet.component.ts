import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input, ViewEncapsulation } from '@angular/core';

import { ReportSheetViewModel } from './student-results-report.builder';

@Component({
  selector: 'app-student-result-sheet',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './student-result-sheet.html',
  styleUrl: './student-result-sheet.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
})
export class StudentResultSheetComponent {
  @Input({ required: true }) sheet!: ReportSheetViewModel;

  showCasColumn(key: 'ca1' | 'ca2' | 'ca3'): boolean {
    return this.sheet.casLayout.columns.some(column => column.key === key);
  }

  casLabel(key: 'ca1' | 'ca2' | 'ca3'): string {
    return this.sheet.casLayout.columns.find(column => column.key === key)?.label ?? key.toUpperCase();
  }

  casPercent(key: 'ca1' | 'ca2' | 'ca3'): number {
    return this.sheet.casLayout.columns.find(column => column.key === key)?.percent ?? 0;
  }

  /** Legacy OneCA uses width 8%; Two/Three use 5%. */
  get casMetricWidth(): string {
    return this.sheet.casLayout.columns.length === 1 ? '8%' : '5%';
  }

  onPassportError(event: Event): void {
    const image = event.target as HTMLImageElement | null;
    if (!image) {
      return;
    }

    if (image.src.includes('no-passport')) {
      image.src = '';
      return;
    }

    // Strip query/hash so we replace only the filename, then bust cache again.
    const clean = image.src.split('?')[0].split('#')[0];
    const slash = clean.lastIndexOf('/');
    const fallback =
      slash >= 0 ? `${clean.slice(0, slash + 1)}no-passport.jpg?v=${Date.now()}` : '';
    image.src = fallback;
  }

  /** Hide broken form-master / school-head signatures (missing or truncated DB filenames). */
  onSignatureError(event: Event): void {
    const image = event.target as HTMLImageElement | null;
    if (!image) {
      return;
    }
    image.removeAttribute('src');
    image.alt = '';
    image.style.display = 'none';
  }
}
