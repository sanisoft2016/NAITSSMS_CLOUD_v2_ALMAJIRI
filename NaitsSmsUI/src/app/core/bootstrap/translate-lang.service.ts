import { Injectable, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { SettingsService } from './settings.service';

@Injectable({
  providedIn: 'root',
})
export class TranslateLangService {
  private readonly translate = inject(TranslateService);
  private readonly settings = inject(SettingsService);

  load() {
    const defaultLang = this.settings.getTranslateLang();
    this.translate.setFallbackLang(defaultLang);

    return new Promise<void>(resolve => {
      let settled = false;
      const done = () => {
        if (settled) {
          return;
        }
        settled = true;
        resolve();
      };

      this.translate.use(defaultLang).subscribe({
        next: () => done(),
        error: () => done(),
        complete: () => done(),
      });

      // A missing or hanging i18n file must not freeze /auth/login on LOADING.
      setTimeout(done, 4000);
    });
  }
}
