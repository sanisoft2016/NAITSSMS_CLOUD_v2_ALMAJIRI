import { ChangeDetectionStrategy, Component, Input, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';

import { BroadSheetReport } from './broadsheet-report.models';

@Component({
  selector: 'app-broadsheet-report-sheet',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './broadsheet-report-sheet.html',
  styleUrl: './broadsheet-report-sheet.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
})
export class BroadSheetReportSheetComponent {
  @Input({ required: true }) report!: BroadSheetReport;
  @Input() logoUrl = '';
  onLogoError(event: Event): void {
    const image = event.target as HTMLImageElement | null;
    image?.removeAttribute('src');
  }
}
