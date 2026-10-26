import { Injectable, inject, DOCUMENT } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PreloaderService {
  private readonly document = inject(DOCUMENT);

  private readonly selector = 'globalLoader';

  private getElement() {
    return this.document.getElementById(this.selector);
  }

  hide() {
    const el = this.getElement();
    if (!el || el.classList.contains('global-loader-hidden')) {
      return;
    }

    const finish = () => {
      el.className = 'global-loader-hidden';
    };

    el.addEventListener('transitionend', finish, { once: true });
    el.classList.add('global-loader-fade-out');
    // transitionend is skipped when the document is restored from bfcache or
    // when the browser ignores the opacity transition — never leave LOADING up.
    setTimeout(finish, 800);
  }
}
