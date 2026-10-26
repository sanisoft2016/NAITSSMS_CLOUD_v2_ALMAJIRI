import { Injectable } from '@angular/core';

export type CompressImageOptions = {
  /** Maximum output size in kilobytes. */
  maxKB: number;
  /** Optional max width/height; image is scaled down proportionally if larger. */
  maxDimension?: number;
  /** Output file name (default: derived from source or image.jpg). */
  fileName?: string;
  /** Starting JPEG quality (default 0.92). */
  maxQuality?: number;
  /** Minimum JPEG quality (default 0.5). */
  minQuality?: number;
  /** Quality step (default 0.07). */
  qualityStep?: number;
};

/**
 * Shared JPEG compress/downscale helper matching the legacy passport/signature
 * quality loop used across student and signature uploads.
 */
@Injectable({ providedIn: 'root' })
export class ImageCompressionService {
  async compressImageFile(file: File, options: CompressImageOptions): Promise<File> {
    if (!file.type.startsWith('image/') && !this.hasImageExtension(file.name)) {
      throw new Error('Only image files are allowed.');
    }

    const image = await this.loadImageFromFile(file);
    const canvas = document.createElement('canvas');
    const maxDimension = options.maxDimension ?? 0;

    let width = image.naturalWidth || image.width;
    let height = image.naturalHeight || image.height;
    if (!width || !height) {
      throw new Error('Unable to read image dimensions.');
    }

    if (maxDimension > 0 && (width > maxDimension || height > maxDimension)) {
      const scale = maxDimension / Math.max(width, height);
      width = Math.max(1, Math.floor(width * scale));
      height = Math.max(1, Math.floor(height * scale));
    }

    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d');
    if (!ctx) {
      throw new Error('Unable to prepare image canvas for compression.');
    }

    // White background so transparent PNGs compress cleanly as JPEG.
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, width, height);
    ctx.drawImage(image, 0, 0, width, height);

    const blob = await this.compressCanvasToUnderKb(canvas, options);
    if (blob.size > options.maxKB * 1024) {
      throw new Error(
        `Unable to reduce image below ${options.maxKB}KB. Please use a smaller or lower resolution image.`,
      );
    }

    const fileName = this.buildJpegFileName(options.fileName || file.name);
    return new File([blob], fileName, { type: 'image/jpeg' });
  }

  private async compressCanvasToUnderKb(
    canvas: HTMLCanvasElement,
    options: CompressImageOptions,
  ): Promise<Blob> {
    const maxBytes = options.maxKB * 1024;
    const minQ = options.minQuality ?? 0.5;
    const maxQ = options.maxQuality ?? 0.92;
    const step = options.qualityStep ?? 0.07;
    const minSide = 40;

    const toBlob = (c: HTMLCanvasElement, q: number) =>
      new Promise<Blob>((resolve, reject) => {
        c.toBlob(b => (b ? resolve(b) : reject(new Error('JPEG compression failed.'))), 'image/jpeg', q);
      });

    const downscale = (c: HTMLCanvasElement, factor: number) => {
      const nw = Math.max(minSide, Math.floor(c.width * factor));
      const nh = Math.max(minSide, Math.floor(c.height * factor));
      const tmp = document.createElement('canvas');
      tmp.width = nw;
      tmp.height = nh;
      const tctx = tmp.getContext('2d');
      if (!tctx) {
        throw new Error('Unable to downscale image.');
      }
      tctx.drawImage(c, 0, 0, c.width, c.height, 0, 0, nw, nh);
      c.width = nw;
      c.height = nh;
      const ctx = c.getContext('2d');
      if (!ctx) {
        throw new Error('Unable to downscale image.');
      }
      ctx.drawImage(tmp, 0, 0);
    };

    let quality = maxQ;
    while (quality >= minQ - 0.0001) {
      const blob = await toBlob(canvas, quality);
      if (blob.size <= maxBytes) {
        return blob;
      }
      quality -= step;
    }

    for (let i = 0; i < 8; i++) {
      if (canvas.width <= minSide || canvas.height <= minSide) {
        break;
      }
      downscale(canvas, 0.9);
      quality = maxQ;
      while (quality >= minQ - 0.0001) {
        const blob = await toBlob(canvas, quality);
        if (blob.size <= maxBytes) {
          return blob;
        }
        quality -= step;
      }
    }

    return toBlob(canvas, minQ);
  }

  private loadImageFromFile(file: File): Promise<HTMLImageElement> {
    return new Promise((resolve, reject) => {
      const url = URL.createObjectURL(file);
      const image = new Image();
      image.onload = () => {
        URL.revokeObjectURL(url);
        resolve(image);
      };
      image.onerror = () => {
        URL.revokeObjectURL(url);
        reject(new Error('Unable to load the selected image.'));
      };
      image.src = url;
    });
  }

  private buildJpegFileName(sourceName: string): string {
    const trimmed = String(sourceName ?? '').trim();
    if (!trimmed) {
      return 'image.jpg';
    }
    return trimmed.replace(/\.[^.]+$/, '') + '.jpg';
  }

  private hasImageExtension(fileName: string): boolean {
    const ext = fileName.includes('.')
      ? fileName.substring(fileName.lastIndexOf('.') + 1).toLowerCase()
      : '';
    return ['jpg', 'jpeg', 'png', 'gif', 'webp', 'heic', 'heif'].includes(ext);
  }
}
