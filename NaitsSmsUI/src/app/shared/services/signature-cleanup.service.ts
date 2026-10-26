import { Injectable } from '@angular/core';

/** Rectangular region (in source-image pixels) to crop before cleaning. */
export interface SignatureCropRegion {
  sx: number;
  sy: number;
  sWidth: number;
  sHeight: number;
}

export interface SignatureCleanupOptions {
  /** Output canvas width before cleaning/tighten. Defaults to rounded sWidth. */
  targetWidth?: number;
  /** Output canvas height before cleaning/tighten. Defaults to rounded sHeight. */
  targetHeight?: number;
  /** Padding (px) kept around detected ink when tightening. Default 6. */
  padPx?: number;
  /** Target maximum JPEG size in KB. Default 13. */
  maxKB?: number;
  /** File name for the produced File. Default 'signature.jpg'. */
  fileName?: string;
}

export interface SignatureCleanupResult {
  blob: Blob;
  file: File;
  dataUrl: string;
  width: number;
  height: number;
}

/**
 * Reusable "Crop & Use" signature pipeline:
 * white fill → crop → adaptive background removal → despeckle → tighten to ink → compress.
 *
 * Any component can inject this and call {@link cropCleanAndCompress} with a source
 * image/canvas and the crop rectangle (in source pixels). The heavy DOM crop-frame UI
 * stays in the host component; this service owns the pure image processing.
 */
@Injectable({ providedIn: 'root' })
export class SignatureCleanupService {
  async cropCleanAndCompress(
    source: CanvasImageSource,
    region: SignatureCropRegion,
    options: SignatureCleanupOptions = {}
  ): Promise<SignatureCleanupResult> {
    const targetW = Math.max(1, Math.round(options.targetWidth ?? region.sWidth));
    const targetH = Math.max(1, Math.round(options.targetHeight ?? region.sHeight));
    const padPx = options.padPx ?? 6;
    const maxKB = options.maxKB ?? 13;
    const fileName = options.fileName ?? 'signature.jpg';

    const canvas = this.doCropAndTighten(
      source,
      region.sx,
      region.sy,
      region.sWidth,
      region.sHeight,
      targetW,
      targetH,
      padPx
    );

    const blob = await this.compressToUnderKb(canvas, maxKB);
    const dataUrl = await this.blobToDataUrl(blob);
    const file = new File([blob], fileName, { type: blob.type || 'image/jpeg' });

    return { blob, file, dataUrl, width: canvas.width, height: canvas.height };
  }

  /** White canvas → crop → clean dirt → despeckle → tighten to ink. */
  private doCropAndTighten(
    source: CanvasImageSource,
    cropX: number,
    cropY: number,
    cropW: number,
    cropH: number,
    targetW: number,
    targetH: number,
    padPx: number
  ): HTMLCanvasElement {
    const temp = document.createElement('canvas');
    temp.width = targetW;
    temp.height = targetH;
    const tctx = temp.getContext('2d', { willReadFrequently: true });
    if (!tctx) {
      throw new Error('Canvas not supported');
    }

    tctx.fillStyle = '#FFFFFF';
    tctx.fillRect(0, 0, targetW, targetH);
    tctx.drawImage(source, cropX, cropY, cropW, cropH, 0, 0, targetW, targetH);

    this.cleanSignatureAdaptive(temp);
    this.despeckle(temp);
    // Pad so stroke tips are not flush-clipped after tighten.
    const tightened = this.tightenToContentWithPadding(temp, Math.max(padPx, 6));

    const finalCanvas = document.createElement('canvas');
    finalCanvas.width = tightened.width;
    finalCanvas.height = tightened.height;
    const fctx = finalCanvas.getContext('2d');
    if (!fctx) {
      throw new Error('Canvas not supported');
    }
    fctx.fillStyle = '#FFFFFF';
    fctx.fillRect(0, 0, finalCanvas.width, finalCanvas.height);
    fctx.drawImage(tightened, 0, 0);
    return finalCanvas;
  }

  /**
   * Adaptive (local mean) thresholding. A pixel is ink when it is meaningfully
   * darker than the paper in its own neighborhood. This keeps faint pen strokes
   * intact while treating smooth lighting gradients / corner shadows as paper,
   * so they are whitened instead of surviving as background dirt.
   */
  private cleanSignatureAdaptive(srcCanvas: HTMLCanvasElement): HTMLCanvasElement {
    const w = srcCanvas.width;
    const h = srcCanvas.height;
    const ctx = srcCanvas.getContext('2d', { willReadFrequently: true });
    if (!ctx || w < 2 || h < 2) {
      return srcCanvas;
    }

    const img = ctx.getImageData(0, 0, w, h);
    const d = img.data;
    const total = w * h;

    const gray = new Float64Array(total);
    for (let p = 0; p < total; p++) {
      const i = p * 4;
      gray[p] = 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
    }

    // Integral image for fast local-mean lookups.
    const iw = w + 1;
    const integral = new Float64Array(iw * (h + 1));
    for (let y = 0; y < h; y++) {
      let rowSum = 0;
      for (let x = 0; x < w; x++) {
        rowSum += gray[y * w + x];
        integral[(y + 1) * iw + (x + 1)] = integral[y * iw + (x + 1)] + rowSum;
      }
    }

    // Window large enough to span stroke width so strokes don't flatten their own local mean.
    const radius = Math.max(12, Math.round(Math.min(w, h) / 8));
    const absMargin = 12;
    const relMargin = 0.82;

    for (let y = 0; y < h; y++) {
      const y0 = Math.max(0, y - radius);
      const y1 = Math.min(h - 1, y + radius);
      for (let x = 0; x < w; x++) {
        const x0 = Math.max(0, x - radius);
        const x1 = Math.min(w - 1, x + radius);
        const area = (x1 - x0 + 1) * (y1 - y0 + 1);
        const sum =
          integral[(y1 + 1) * iw + (x1 + 1)] -
          integral[y0 * iw + (x1 + 1)] -
          integral[(y1 + 1) * iw + x0] +
          integral[y0 * iw + x0];
        const localMean = sum / area;
        const g = gray[y * w + x];

        const i = (y * w + x) * 4;
        const isInk = g < localMean - absMargin && g < localMean * relMargin;
        if (!isInk) {
          d[i] = 255;
          d[i + 1] = 255;
          d[i + 2] = 255;
          d[i + 3] = 255;
        } else {
          // Boost contrast so faint strokes stay clearly visible.
          const ink = Math.max(0, Math.round(g - 45));
          d[i] = ink;
          d[i + 1] = ink;
          d[i + 2] = ink;
          d[i + 3] = 255;
        }
      }
    }

    ctx.putImageData(img, 0, 0);
    return srcCanvas;
  }

  /**
   * Removes tiny isolated ink specks (scanner noise / leftover corner dither)
   * without touching real strokes, which form large connected components.
   */
  private despeckle(srcCanvas: HTMLCanvasElement): HTMLCanvasElement {
    const w = srcCanvas.width;
    const h = srcCanvas.height;
    const ctx = srcCanvas.getContext('2d', { willReadFrequently: true });
    if (!ctx || w < 2 || h < 2) {
      return srcCanvas;
    }

    const img = ctx.getImageData(0, 0, w, h);
    const d = img.data;
    const total = w * h;
    const labels = new Int32Array(total);
    const isInk = (idx: number) => d[idx * 4] < 245;
    const minArea = Math.max(6, Math.round(total * 0.00008));

    const stack: number[] = [];
    let nextLabel = 1;
    for (let start = 0; start < total; start++) {
      if (labels[start] || !isInk(start)) {
        continue;
      }

      const label = nextLabel++;
      const members: number[] = [];
      stack.length = 0;
      stack.push(start);
      labels[start] = label;

      while (stack.length) {
        const p = stack.pop()!;
        members.push(p);
        const x = p % w;
        const y = (p / w) | 0;
        for (let dy = -1; dy <= 1; dy++) {
          for (let dx = -1; dx <= 1; dx++) {
            if (dx === 0 && dy === 0) {
              continue;
            }
            const nx = x + dx;
            const ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= w || ny >= h) {
              continue;
            }
            const n = ny * w + nx;
            if (labels[n] || !isInk(n)) {
              continue;
            }
            labels[n] = label;
            stack.push(n);
          }
        }
      }

      if (members.length < minArea) {
        for (const p of members) {
          const i = p * 4;
          d[i] = 255;
          d[i + 1] = 255;
          d[i + 2] = 255;
          d[i + 3] = 255;
        }
      }
    }

    ctx.putImageData(img, 0, 0);
    return srcCanvas;
  }

  private sampleBackground(
    ctx: CanvasRenderingContext2D,
    w: number,
    h: number
  ): { r: number; g: number; b: number } {
    const top = ctx.getImageData(0, 0, w, 1).data;
    const bottom = ctx.getImageData(0, h - 1, w, 1).data;
    const left = ctx.getImageData(0, 0, 1, h).data;
    const right = ctx.getImageData(w - 1, 0, 1, h).data;

    let sumR = 0;
    let sumG = 0;
    let sumB = 0;
    let count = 0;
    const accumulate = (arr: Uint8ClampedArray) => {
      for (let i = 0; i < arr.length; i += 4) {
        sumR += arr[i];
        sumG += arr[i + 1];
        sumB += arr[i + 2];
        count++;
      }
    };
    accumulate(top);
    accumulate(bottom);
    accumulate(left);
    accumulate(right);

    return count
      ? { r: sumR / count, g: sumG / count, b: sumB / count }
      : { r: 255, g: 255, b: 255 };
  }

  private computeInkBounds(
    ctx: CanvasRenderingContext2D,
    w: number,
    h: number,
    bg: { r: number; g: number; b: number }
  ): { x: number; y: number; w: number; h: number } {
    const data = ctx.getImageData(0, 0, w, h).data;
    let minX = w;
    let minY = h;
    let maxX = -1;
    let maxY = -1;
    const bgB = 0.299 * bg.r + 0.587 * bg.g + 0.114 * bg.b;

    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        const i = (y * w + x) * 4;
        const r = data[i];
        const g = data[i + 1];
        const b = data[i + 2];
        const diff = (Math.abs(r - bg.r) + Math.abs(g - bg.g) + Math.abs(b - bg.b)) / 3;
        const br = 0.299 * r + 0.587 * g + 0.114 * b;
        if (diff > 18 || Math.abs(br - bgB) > 20) {
          if (x < minX) minX = x;
          if (x > maxX) maxX = x;
          if (y < minY) minY = y;
          if (y > maxY) maxY = y;
        }
      }
    }

    if (maxX < minX || maxY < minY) {
      return { x: 0, y: 0, w, h };
    }
    return { x: minX, y: minY, w: maxX - minX + 1, h: maxY - minY + 1 };
  }

  private tightenToContentWithPadding(srcCanvas: HTMLCanvasElement, padPx: number): HTMLCanvasElement {
    const w = srcCanvas.width;
    const h = srcCanvas.height;
    const ctx = srcCanvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) {
      return srcCanvas;
    }

    const bg = this.sampleBackground(ctx, w, h);
    const bounds = this.computeInkBounds(ctx, w, h, bg);
    const nx = Math.max(0, bounds.x - padPx);
    const ny = Math.max(0, bounds.y - padPx);
    let nw = Math.min(w - nx, bounds.w + 2 * padPx);
    let nh = Math.min(h - ny, bounds.h + 2 * padPx);
    nw = Math.max(2, nw);
    nh = Math.max(2, nh);

    const out = document.createElement('canvas');
    out.width = nw;
    out.height = nh;
    out.getContext('2d')!.drawImage(srcCanvas, nx, ny, nw, nh, 0, 0, nw, nh);
    return out;
  }

  private compressToUnderKb(canvas: HTMLCanvasElement, maxKB: number): Promise<Blob> {
    const maxBytes = maxKB * 1024;
    const minQ = 0.5;
    const maxQ = 0.92;
    const step = 0.07;

    const toBlob = (c: HTMLCanvasElement, q: number) =>
      new Promise<Blob>((resolve, reject) => {
        c.toBlob(b => (b ? resolve(b) : reject(new Error('toBlob failed'))), 'image/jpeg', q);
      });

    const downscale = (c: HTMLCanvasElement, factor: number) => {
      const nw = Math.max(20, Math.floor(c.width * factor));
      const nh = Math.max(20, Math.floor(c.height * factor));
      const tmp = document.createElement('canvas');
      tmp.width = nw;
      tmp.height = nh;
      const tctx = tmp.getContext('2d')!;
      tctx.drawImage(c, 0, 0, c.width, c.height, 0, 0, nw, nh);
      c.width = nw;
      c.height = nh;
      c.getContext('2d')!.drawImage(tmp, 0, 0);
    };

    const run = async (): Promise<Blob> => {
      let q = maxQ;

      while (q >= minQ) {
        const b = await toBlob(canvas, q);
        if (b.size <= maxBytes) return b;
        q -= step;
      }

      for (let i = 0; i < 6; i++) {
        downscale(canvas, 0.9);

        q = maxQ;
        while (q >= minQ) {
          const b = await toBlob(canvas, q);
          if (b.size <= maxBytes) return b;
          q -= step;
        }
      }

      return toBlob(canvas, minQ);
    };

    return run().then(blob => {
      if (blob.size > maxBytes) {
        throw new Error(
          `Unable to reduce the signature below ${maxKB}KB. Please crop tighter or use a smaller image.`,
        );
      }
      return blob;
    });
  }

  private blobToDataUrl(blob: Blob): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result || ''));
      reader.onerror = () => reject(reader.error ?? new Error('Failed to read image payload.'));
      reader.readAsDataURL(blob);
    });
  }
}
