import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';

export type SeoConfig = {
  title: string;
  description: string;
  path?: string;
  keywords?: string;
  robots?: string;
  image?: string;
  type?: 'website' | 'article';
};

@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  private readonly siteName = 'NAITS-ASMS';
  private readonly defaultImage = '/images/naits-asms.svg';

  apply(config: SeoConfig): void {
    const pageTitle = config.title.includes(this.siteName)
      ? config.title
      : `${config.title} | ${this.siteName}`;
    const description = config.description.trim();
    const origin = typeof window !== 'undefined' ? window.location.origin : '';
    const canonical = config.path
      ? `${origin}${config.path.startsWith('/') ? config.path : `/${config.path}`}`
      : typeof window !== 'undefined'
        ? window.location.href.split('#')[0]
        : origin;
    const imageUrl = config.image?.startsWith('http')
      ? config.image
      : `${origin}${config.image ?? this.defaultImage}`;
    const robots = config.robots ?? 'index,follow';
    const ogType = config.type ?? 'website';

    this.title.setTitle(pageTitle);

    this.upsertName('description', description);
    this.upsertName('robots', robots);
    if (config.keywords) {
      this.upsertName('keywords', config.keywords);
    } else {
      this.meta.removeTag('name="keywords"');
    }

    this.upsertProperty('og:site_name', this.siteName);
    this.upsertProperty('og:title', pageTitle);
    this.upsertProperty('og:description', description);
    this.upsertProperty('og:type', ogType);
    this.upsertProperty('og:url', canonical);
    this.upsertProperty('og:image', imageUrl);

    this.upsertName('twitter:card', 'summary_large_image');
    this.upsertName('twitter:title', pageTitle);
    this.upsertName('twitter:description', description);
    this.upsertName('twitter:image', imageUrl);

    this.upsertLinkCanonical(canonical);
  }

  private upsertName(name: string, content: string): void {
    if (this.meta.getTag(`name="${name}"`)) {
      this.meta.updateTag({ name, content });
    } else {
      this.meta.addTag({ name, content });
    }
  }

  private upsertProperty(property: string, content: string): void {
    if (this.meta.getTag(`property="${property}"`)) {
      this.meta.updateTag({ property, content });
    } else {
      this.meta.addTag({ property, content });
    }
  }

  private upsertLinkCanonical(href: string): void {
    if (typeof document === 'undefined') {
      return;
    }
    let link = document.querySelector<HTMLLinkElement>("link[rel='canonical']");
    if (!link) {
      link = document.createElement('link');
      link.setAttribute('rel', 'canonical');
      document.head.appendChild(link);
    }
    link.setAttribute('href', href);
  }
}
