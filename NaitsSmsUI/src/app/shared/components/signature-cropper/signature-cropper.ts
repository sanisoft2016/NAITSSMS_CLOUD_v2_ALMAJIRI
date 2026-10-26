import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  ViewChild,
  ViewEncapsulation,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';

import { SignatureCleanupService } from '../../services/signature-cleanup.service';

export interface SignatureCropResult {
  file: File;
  dataUrl: string;
  width: number;
  height: number;
}

/**
 * Reusable signature picker + interactive crop/clean workflow.
 *
 * Renders a "Choose image" button, a draggable/resizable crop frame that auto-wraps
 * the detected signature ink, and a "Crop & Use" button that runs the shared
 * {@link SignatureCleanupService} pipeline (white fill → crop → adaptive clean →
 * despeckle → tighten → compress). Works with phone-camera captures: the preview
 * fits the container, touch drag/resize is scale-aware, and camera capture is enabled.
 *
 * Emits {@link cropped} with the produced File + data URL. Host components decide how
 * to store the result (form control, upload, etc.).
 */
@Component({
  selector: 'app-signature-cropper',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './signature-cropper.html',
  styleUrl: './signature-cropper.scss',
  encapsulation: ViewEncapsulation.None,
})
export class SignatureCropper implements AfterViewInit, OnChanges, OnDestroy {
  private readonly signatureCleanup = inject(SignatureCleanupService);
  private readonly cdr = inject(ChangeDetectorRef);

  /** Existing signature URL to display in edit mode (until a new image is chosen). */
  @Input() initialPreviewUrl = '';
  /** Target maximum JPEG size in KB for the produced signature. */
  @Input() maxKB = 13;
  /** File name for the produced File. */
  @Input() fileName = 'signature.jpg';
  /** Disables the picker (e.g. while a request is in flight). */
  @Input() disabled = false;

  /** Fired when a raw image is loaded and awaiting crop. */
  @Output() imageSelected = new EventEmitter<void>();
  /** Fired when "Crop & Use" produces the final signature. */
  @Output() cropped = new EventEmitter<SignatureCropResult>();
  /** Fired when the selection is cleared. */
  @Output() cleared = new EventEmitter<void>();

  @ViewChild('imageHolder', { static: true }) imageHolderRef!: ElementRef<HTMLDivElement>;
  @ViewChild('fileInput', { static: true }) fileInputRef!: ElementRef<HTMLInputElement>;
  @ViewChild('cameraInput', { static: true }) cameraInputRef!: ElementRef<HTMLInputElement>;

  error: string | null = null;

  private previewImgEl: HTMLImageElement | null = null;
  private cropFrameEl: HTMLDivElement | null = null;
  private existingShown = false;
  private initializedExisting = false;

  private imgNaturalW = 0;
  private imgNaturalH = 0;
  private imgDisplayW = 0;
  private imgDisplayH = 0;
  private idleHolderWidth = 0;
  private idleHolderHeight = 0;
  private readonly idleHolderMinWidth = '16em';
  private readonly idleHolderMinHeight = '8em';

  private dragging = false;
  private resizing = false;
  private resizeDir: 'nw' | 'n' | 'ne' | 'e' | 'se' | 's' | 'sw' | 'w' | '' = '';
  private startX = 0;
  private startY = 0;
  // Ratio of rendered CSS px to layout px, so pointer deltas map correctly even
  // when the holder is visually scaled (browser zoom, device pixel quirks).
  private pointerScale = 1;

  private frameStartLeft = 0;
  private frameStartTop = 0;
  private frameStartW = 0;
  private frameStartH = 0;

  private isCropped = false;

  private cleanupFns: Array<() => void> = [];

  get canCrop(): boolean {
    return !this.disabled && !!this.previewImgEl && !!this.cropFrameEl && !this.isCropped;
  }

  get hasSignature(): boolean {
    return this.isCropped || !!this.previewImgEl || this.existingShown;
  }

  /** True when a raw image is loaded but has not been cropped yet. */
  get pendingCrop(): boolean {
    return !!this.previewImgEl && !this.isCropped;
  }

  get hintText(): string {
    if (this.isCropped) return 'Signature ready.';
    if (this.pendingCrop) return 'Adjust the frame, then click "Crop & Use".';
    if (this.existingShown) return 'Current signature. Choose a new image, or open the camera, to replace it.';
    return 'No signature image selected. Choose an image, or open the camera on a phone or tablet.';
  }

  ngAfterViewInit(): void {
    this.maybeShowExistingPreview();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['initialPreviewUrl'] && !this.previewImgEl && !this.isCropped) {
      // Show/refresh the existing signature (edit mode). Re-runnable when the URL changes.
      this.initializedExisting = false;
      const url = (this.initialPreviewUrl || '').trim();
      if (url) {
        this.maybeShowExistingPreview();
      } else if (this.imageHolderRef) {
        this.existingShown = false;
        this.clearHolder();
        this.cdr.markForCheck();
      }
    }
  }

  ngOnDestroy(): void {
    this.cleanupListeners();
  }

  onFileSelected(evt: Event): void {
    if (!this.previewImgEl && !this.cropFrameEl && (this.idleHolderWidth === 0 || this.idleHolderHeight === 0)) {
      this.captureIdleHolderSize();
    }

    this.error = null;
    this.isCropped = false;
    this.existingShown = false;

    const input = evt.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const extOk = /(\.gif|\.png|\.jpg|\.jpeg|\.webp|\.bmp)$/i.test(file.name) || file.type.startsWith('image/');
    if (!extOk) {
      this.error = 'Please select only images.';
      this.clearHolder();
      this.cdr.markForCheck();
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      this.mountPreviewImage(String(reader.result || ''));
      this.imageSelected.emit();
      this.cdr.markForCheck();
    };
    reader.readAsDataURL(file);
    this.resetFileInputs();
  }

  clear(): void {
    this.error = null;
    this.isCropped = false;
    this.existingShown = false;

    this.resetFileInputs();

    this.clearHolder();
    this.cleanupListeners();
    this.cleared.emit();
    this.cdr.markForCheck();
  }

  async cropAndUse(): Promise<void> {
    if (!this.previewImgEl || !this.cropFrameEl) return;

    try {
      const holder = this.imageHolderRef.nativeElement;
      const frame = this.cropFrameEl;

      const borderW = parseInt(getComputedStyle(frame).borderLeftWidth, 10) || 0;
      const innerPad = 0;
      const cropLeftAbs = frame.offsetLeft;
      const cropTopAbs = frame.offsetTop;
      const cropFrameW = frame.offsetWidth;
      const cropFrameH = frame.offsetHeight;

      const innerLeft = cropLeftAbs + borderW + innerPad;
      const innerTop = cropTopAbs + borderW + innerPad;
      const innerWidth = Math.max(1, cropFrameW - 2 * (borderW + innerPad));
      const innerHeight = Math.max(1, cropFrameH - 2 * (borderW + innerPad));

      const scaleX = this.imgNaturalW / this.imgDisplayW;
      const scaleY = this.imgNaturalH / this.imgDisplayH;

      const result = await this.signatureCleanup.cropCleanAndCompress(
        this.previewImgEl,
        {
          sx: innerLeft * scaleX,
          sy: innerTop * scaleY,
          sWidth: innerWidth * scaleX,
          sHeight: innerHeight * scaleY,
        },
        {
          targetWidth: innerWidth,
          targetHeight: innerHeight,
          padPx: innerPad,
          maxKB: this.maxKB,
          fileName: this.fileName,
        }
      );

      // Replace holder content with final preview
      this.clearHolder();
      const url = URL.createObjectURL(result.blob);
      const img = new Image();
      img.alt = 'signature preview';
      holder.style.display = 'inline-block';
      holder.style.boxSizing = 'content-box';
      holder.style.width = `${result.width}px`;
      holder.style.height = `${result.height}px`;
      holder.style.minWidth = '0';
      holder.style.minHeight = '0';
      holder.style.padding = '1em';

      img.style.width = `${result.width}px`;
      img.style.height = `${result.height}px`;
      img.style.display = 'block';
      img.onload = () => URL.revokeObjectURL(url);
      img.src = url;

      holder.appendChild(img);

      this.isCropped = true;
      this.cropFrameEl?.remove();
      this.cropFrameEl = null;
      this.cleanupListeners();

      this.cropped.emit({
        file: result.file,
        dataUrl: result.dataUrl,
        width: result.width,
        height: result.height,
      });
      this.cdr.markForCheck();
    } catch (err) {
      this.error =
        err instanceof Error && err.message
          ? err.message
          : 'Unable to crop/compress image. Please try another image.';
      this.cdr.markForCheck();
    }
  }

  private resetFileInputs(): void {
    const gallery = this.fileInputRef?.nativeElement;
    if (gallery) {
      gallery.value = '';
    }
    const camera = this.cameraInputRef?.nativeElement;
    if (camera) {
      camera.value = '';
    }
  }

  private maybeShowExistingPreview(): void {
    if (this.initializedExisting) return;
    const url = (this.initialPreviewUrl || '').trim();
    if (!url) return;

    const holder = this.imageHolderRef?.nativeElement;
    if (!holder) return;

    this.initializedExisting = true;
    this.existingShown = true;
    this.clearHolder();
    this.existingShown = true;

    holder.style.display = 'inline-block';
    holder.style.boxSizing = 'content-box';
    holder.style.minWidth = '0';
    holder.style.minHeight = '0';
    holder.style.padding = '1em';

    const img = new Image();
    img.alt = 'signature preview';
    img.style.maxWidth = '100%';
    img.style.display = 'block';
    img.src = url;
    holder.appendChild(img);
    this.cdr.markForCheck();
  }

  // ---------- UI mounting / crop frame ----------

  private mountPreviewImage(dataUrl: string): void {
    this.clearHolder();
    this.cleanupListeners();

    const holder = this.imageHolderRef.nativeElement;
    holder.style.position = 'relative';
    holder.style.overflow = 'hidden';

    const img = new Image();
    img.id = 'preview-img';
    img.className = 'thumb-image';
    img.style.position = 'absolute';
    img.style.left = '0px';
    img.style.top = '0px';
    img.style.visibility = 'hidden';
    img.style.userSelect = 'none';

    img.onload = () => {
      this.previewImgEl = img;
      this.imgNaturalW = img.naturalWidth || 1;
      this.imgNaturalH = img.naturalHeight || 1;

      // Fit the preview to the available container so phone-camera photos (which are
      // huge, e.g. 3000x4000) are fully visible instead of being a tiny scroll window.
      const fit = this.computeDisplayFit(this.imgNaturalW, this.imgNaturalH);
      this.imgDisplayW = fit.width;
      this.imgDisplayH = fit.height;

      holder.style.width = `${this.imgDisplayW}px`;
      holder.style.height = `${this.imgDisplayH}px`;

      img.style.width = `${this.imgDisplayW}px`;
      img.style.height = `${this.imgDisplayH}px`;
      img.style.visibility = 'visible';

      this.initCropFrame();
      this.cdr.markForCheck();
    };

    img.src = dataUrl;
    holder.appendChild(img);
  }

  /**
   * Computes a display size that fits the image inside the available preview area
   * (never upscaling past native size). Keeps portrait phone shots usable by also
   * capping to a fraction of the viewport height.
   */
  private computeDisplayFit(naturalW: number, naturalH: number): { width: number; height: number } {
    const holder = this.imageHolderRef.nativeElement;
    const preview = holder.parentElement;

    const previewWidth = preview?.clientWidth ?? holder.clientWidth ?? 0;
    const availW = Math.max(120, (previewWidth || window.innerWidth) - 20);
    const availH = Math.max(120, Math.round(window.innerHeight * 0.7));

    const fit = Math.min(1, availW / naturalW, availH / naturalH);
    return {
      width: Math.max(1, Math.round(naturalW * fit)),
      height: Math.max(1, Math.round(naturalH * fit)),
    };
  }

  private initCropFrame(): void {
    if (!this.previewImgEl) return;

    const holder = this.imageHolderRef.nativeElement;

    const minW = 60;
    const minH = 60;

    const detected = this.detectSignatureFrame();
    let frameW: number;
    let frameH: number;
    let left: number;
    let top: number;

    if (detected) {
      frameW = Math.max(minW, Math.min(this.imgDisplayW, detected.w));
      frameH = Math.max(minH, Math.min(this.imgDisplayH, detected.h));
      left = Math.max(0, Math.min(detected.x, this.imgDisplayW - frameW));
      top = Math.max(0, Math.min(detected.y, this.imgDisplayH - frameH));
    } else {
      const cover = 0.8;
      frameW = Math.max(minW, Math.min(this.imgDisplayW, Math.round(this.imgDisplayW * cover)));
      frameH = Math.max(minH, Math.min(this.imgDisplayH, Math.round(this.imgDisplayH * cover)));
      left = Math.round((this.imgDisplayW - frameW) / 2);
      top = Math.round((this.imgDisplayH - frameH) / 2);
    }

    this.cropFrameEl?.remove();

    const frame = document.createElement('div');
    frame.id = 'crop-frame';
    frame.style.position = 'absolute';
    frame.style.display = 'block';
    frame.style.boxSizing = 'border-box';
    frame.style.border = '2px dashed #00f';
    frame.style.background = 'rgba(0, 0, 255, 0.08)';
    frame.style.cursor = 'move';
    frame.style.zIndex = '10';
    frame.style.touchAction = 'none';
    frame.style.userSelect = 'none';
    frame.style.width = `${frameW}px`;
    frame.style.height = `${frameH}px`;
    frame.style.left = `${left}px`;
    frame.style.top = `${top}px`;

    const dirs = ['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w'] as const;
    for (const d of dirs) {
      const h = document.createElement('div');
      h.className = `resize-handle ${d}`;
      this.applyResizeHandleStyles(h, d);
      frame.appendChild(h);
    }

    holder.appendChild(frame);
    this.cropFrameEl = frame;

    this.wireCropEvents(minW, minH);
    this.cdr.markForCheck();
  }

  /**
   * Detects the signature ink bounding box (in display pixels) so the initial crop
   * frame wraps only the signature, not the whole photo. Returns null if no ink is found.
   */
  private detectSignatureFrame(): { x: number; y: number; w: number; h: number } | null {
    const img = this.previewImgEl;
    if (!img) return null;

    const dispW = this.imgDisplayW;
    const dispH = this.imgDisplayH;
    if (dispW < 2 || dispH < 2) return null;

    const maxDim = 700;
    const scale = Math.min(1, maxDim / Math.max(dispW, dispH));
    const cw = Math.max(1, Math.round(dispW * scale));
    const ch = Math.max(1, Math.round(dispH * scale));

    const canvas = document.createElement('canvas');
    canvas.width = cw;
    canvas.height = ch;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) return null;
    ctx.drawImage(img, 0, 0, cw, ch);

    let data: Uint8ClampedArray;
    try {
      data = ctx.getImageData(0, 0, cw, ch).data;
    } catch {
      return null;
    }

    const total = cw * ch;
    const gray = new Float64Array(total);
    for (let p = 0; p < total; p++) {
      const i = p * 4;
      gray[p] = 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
    }

    // Global paper brightness (bright majority) → ignores dark vignette / corners.
    const samples: number[] = [];
    for (let p = 0; p < total; p += 10) {
      samples.push(gray[p]);
    }
    samples.sort((a, b) => a - b);
    const paper = samples.length ? samples[Math.floor(samples.length * 0.75)] : 255;

    const iw = cw + 1;
    const integral = new Float64Array(iw * (ch + 1));
    for (let y = 0; y < ch; y++) {
      let rowSum = 0;
      for (let x = 0; x < cw; x++) {
        rowSum += gray[y * cw + x];
        integral[(y + 1) * iw + (x + 1)] = integral[y * iw + (x + 1)] + rowSum;
      }
    }

    // Small window: only slightly larger than a stroke, so smooth vignette/shadow
    // gradients are NOT flagged (a large window would straddle the gradient and
    // mark the whole shadow ring as ink).
    const radius = Math.max(4, Math.round(Math.min(cw, ch) / 70));
    const localMargin = 24;
    const absDark = paper - 55;

    const colCount = new Int32Array(cw);
    const rowCount = new Int32Array(ch);
    let inkTotal = 0;
    for (let y = 0; y < ch; y++) {
      const y0 = Math.max(0, y - radius);
      const y1 = Math.min(ch - 1, y + radius);
      for (let x = 0; x < cw; x++) {
        const g = gray[y * cw + x];
        if (g >= absDark) {
          continue;
        }
        const x0 = Math.max(0, x - radius);
        const x1 = Math.min(cw - 1, x + radius);
        const area = (x1 - x0 + 1) * (y1 - y0 + 1);
        const sum =
          integral[(y1 + 1) * iw + (x1 + 1)] -
          integral[y0 * iw + (x1 + 1)] -
          integral[(y1 + 1) * iw + x0] +
          integral[y0 * iw + x0];
        const localMean = sum / area;
        if (localMean - g > localMargin) {
          colCount[x]++;
          rowCount[y]++;
          inkTotal++;
        }
      }
    }

    if (inkTotal < 20) return null;

    const colThreshold = Math.max(2, Math.round(ch * 0.01));
    const rowThreshold = Math.max(2, Math.round(cw * 0.01));

    let minX = cw;
    let maxX = -1;
    for (let x = 0; x < cw; x++) {
      if (colCount[x] >= colThreshold) {
        if (x < minX) minX = x;
        if (x > maxX) maxX = x;
      }
    }
    let minY = ch;
    let maxY = -1;
    for (let y = 0; y < ch; y++) {
      if (rowCount[y] >= rowThreshold) {
        if (y < minY) minY = y;
        if (y > maxY) maxY = y;
      }
    }

    if (maxX < minX || maxY < minY) return null;

    const inv = 1 / scale;
    let x = minX * inv;
    let y = minY * inv;
    let w = (maxX - minX + 1) * inv;
    let h = (maxY - minY + 1) * inv;

    const padX = Math.round(w * 0.08) + 8;
    const padY = Math.round(h * 0.12) + 8;
    x = Math.max(0, x - padX);
    y = Math.max(0, y - padY);
    w = Math.min(dispW - x, w + padX * 2);
    h = Math.min(dispH - y, h + padY * 2);

    return { x: Math.round(x), y: Math.round(y), w: Math.round(w), h: Math.round(h) };
  }

  private wireCropEvents(minW: number, minH: number): void {
    if (!this.cropFrameEl) return;

    const frame = this.cropFrameEl;

    const getPos = (e: PointerEvent) => ({ x: e.clientX, y: e.clientY });

    const onPointerDown = (e: PointerEvent) => {
      if (!this.cropFrameEl) return;

      const target = e.target as HTMLElement | null;
      const isHandle = !!target?.classList.contains('resize-handle');

      e.preventDefault();
      frame.setPointerCapture?.(e.pointerId);

      const pos = getPos(e);
      this.startX = pos.x;
      this.startY = pos.y;

      const holderEl = this.imageHolderRef.nativeElement;
      const renderedW = holderEl.getBoundingClientRect().width;
      const layoutW = holderEl.offsetWidth || renderedW || 1;
      this.pointerScale = renderedW > 0 ? renderedW / layoutW : 1;

      this.frameStartLeft = frame.offsetLeft;
      this.frameStartTop = frame.offsetTop;
      this.frameStartW = frame.offsetWidth;
      this.frameStartH = frame.offsetHeight;

      if (isHandle) {
        this.resizing = true;
        const cls = Array.from(target!.classList).find(c => ['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w'].includes(c)) as any;
        this.resizeDir = cls || '';
      } else {
        this.dragging = true;
        this.resizeDir = '';
      }

      document.body.classList.add('no-scroll');
    };

    const onPointerMove = (e: PointerEvent) => {
      if (!this.cropFrameEl) return;
      if (!this.dragging && !this.resizing) return;

      e.preventDefault();

      const pos = getPos(e);
      const scale = this.pointerScale || 1;
      const dx = (pos.x - this.startX) / scale;
      const dy = (pos.y - this.startY) / scale;

      let left = this.frameStartLeft;
      let top = this.frameStartTop;
      let width = this.frameStartW;
      let height = this.frameStartH;

      const clamp = (l: number, t: number, w: number, h: number) => {
        w = Math.max(minW, w);
        h = Math.max(minH, h);

        l = Math.max(0, Math.min(l, this.imgDisplayW - w));
        t = Math.max(0, Math.min(t, this.imgDisplayH - h));
        return { l, t, w, h };
      };

      if (this.dragging) {
        const c = clamp(this.frameStartLeft + dx, this.frameStartTop + dy, width, height);
        frame.style.left = `${Math.round(c.l)}px`;
        frame.style.top = `${Math.round(c.t)}px`;
        return;
      }

      switch (this.resizeDir) {
        case 'nw':
          left = this.frameStartLeft + dx;
          top = this.frameStartTop + dy;
          width = this.frameStartW - dx;
          height = this.frameStartH - dy;
          break;
        case 'n':
          top = this.frameStartTop + dy;
          height = this.frameStartH - dy;
          break;
        case 'ne':
          top = this.frameStartTop + dy;
          width = this.frameStartW + dx;
          height = this.frameStartH - dy;
          break;
        case 'e':
          width = this.frameStartW + dx;
          break;
        case 'se':
          width = this.frameStartW + dx;
          height = this.frameStartH + dy;
          break;
        case 's':
          height = this.frameStartH + dy;
          break;
        case 'sw':
          left = this.frameStartLeft + dx;
          width = this.frameStartW - dx;
          height = this.frameStartH + dy;
          break;
        case 'w':
          left = this.frameStartLeft + dx;
          width = this.frameStartW - dx;
          break;
      }

      const c = clamp(left, top, width, height);
      frame.style.left = `${Math.round(c.l)}px`;
      frame.style.top = `${Math.round(c.t)}px`;
      frame.style.width = `${Math.round(c.w)}px`;
      frame.style.height = `${Math.round(c.h)}px`;
    };

    const end = () => {
      document.body.classList.remove('no-scroll');
      this.dragging = false;
      this.resizing = false;
      this.resizeDir = '';
    };

    frame.addEventListener('pointerdown', onPointerDown);
    window.addEventListener('pointermove', onPointerMove, { passive: false });
    window.addEventListener('pointerup', end);
    window.addEventListener('pointercancel', end);

    this.cleanupFns.push(() => frame.removeEventListener('pointerdown', onPointerDown));
    this.cleanupFns.push(() => window.removeEventListener('pointermove', onPointerMove as any));
    this.cleanupFns.push(() => window.removeEventListener('pointerup', end));
    this.cleanupFns.push(() => window.removeEventListener('pointercancel', end));
  }

  private cleanupListeners(): void {
    for (const fn of this.cleanupFns.splice(0)) fn();
  }

  private clearHolder(): void {
    const holder = this.imageHolderRef.nativeElement;
    holder.innerHTML = '';
    this.previewImgEl = null;
    this.cropFrameEl = null;
    this.restoreIdleHolderSize();
  }

  private captureIdleHolderSize(): void {
    const holder = this.imageHolderRef.nativeElement;
    const rect = holder.getBoundingClientRect();

    if (rect.width > 0 && rect.height > 0) {
      this.idleHolderWidth = Math.round(rect.width);
      this.idleHolderHeight = Math.round(rect.height);
    }
  }

  private restoreIdleHolderSize(): void {
    const holder = this.imageHolderRef.nativeElement;

    holder.style.display = 'block';
    holder.style.boxSizing = 'content-box';
    holder.style.padding = '0';
    holder.style.minWidth = this.idleHolderMinWidth;
    holder.style.minHeight = this.idleHolderMinHeight;

    if (this.idleHolderWidth > 0) {
      holder.style.width = `${this.idleHolderWidth}px`;
    }

    if (this.idleHolderHeight > 0) {
      holder.style.height = `${this.idleHolderHeight}px`;
    }
  }

  private applyResizeHandleStyles(
    handle: HTMLDivElement,
    direction: 'nw' | 'n' | 'ne' | 'e' | 'se' | 's' | 'sw' | 'w'
  ): void {
    const isCoarsePointer = window.matchMedia('(pointer: coarse)').matches;
    const size = isCoarsePointer ? 28 : 12;
    const offset = isCoarsePointer ? -14 : -6;

    handle.style.position = 'absolute';
    handle.style.width = `${size}px`;
    handle.style.height = `${size}px`;
    handle.style.background = '#fff';
    handle.style.border = isCoarsePointer ? '2px solid #00f' : '1px solid #00f';
    handle.style.boxSizing = 'border-box';
    handle.style.zIndex = '11';

    switch (direction) {
      case 'nw':
        handle.style.top = `${offset}px`;
        handle.style.left = `${offset}px`;
        handle.style.cursor = 'nwse-resize';
        break;
      case 'n':
        handle.style.top = `${offset}px`;
        handle.style.left = '50%';
        handle.style.transform = 'translateX(-50%)';
        handle.style.cursor = 'ns-resize';
        break;
      case 'ne':
        handle.style.top = `${offset}px`;
        handle.style.right = `${offset}px`;
        handle.style.cursor = 'nesw-resize';
        break;
      case 'e':
        handle.style.top = '50%';
        handle.style.right = `${offset}px`;
        handle.style.transform = 'translateY(-50%)';
        handle.style.cursor = 'ew-resize';
        break;
      case 'se':
        handle.style.bottom = `${offset}px`;
        handle.style.right = `${offset}px`;
        handle.style.cursor = 'nwse-resize';
        break;
      case 's':
        handle.style.bottom = `${offset}px`;
        handle.style.left = '50%';
        handle.style.transform = 'translateX(-50%)';
        handle.style.cursor = 'ns-resize';
        break;
      case 'sw':
        handle.style.bottom = `${offset}px`;
        handle.style.left = `${offset}px`;
        handle.style.cursor = 'nesw-resize';
        break;
      case 'w':
        handle.style.top = '50%';
        handle.style.left = `${offset}px`;
        handle.style.transform = 'translateY(-50%)';
        handle.style.cursor = 'ew-resize';
        break;
    }
  }
}
