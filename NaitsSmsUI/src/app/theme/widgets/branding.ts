import { Component, input } from '@angular/core';

@Component({
  selector: 'app-branding',
  template: `
    <a class="branding" href="/">
      <img src="images/naits-asms.svg" class="branding-logo" alt="NAITS-ASMS" />
      @if (showName()) {
        <span class="branding-name">NAITS-ASMS</span>
      }
    </a>
  `,
  styles: `
    .branding {
      display: flex;
      align-items: center;
      margin: 0 0.5rem;
      text-decoration: none;
      white-space: nowrap;
      color: inherit;
      border-radius: 50rem;
    }

    .branding-logo {
      width: 2rem;
      height: 2rem;
      border-radius: 50rem;
    }

    .branding-name {
      margin: 0 0.5rem;
      font-size: 0.95rem;
      font-weight: 700;
      letter-spacing: 0.02em;
    }
  `,
})
export class Branding {
  readonly showName = input(true);
}
