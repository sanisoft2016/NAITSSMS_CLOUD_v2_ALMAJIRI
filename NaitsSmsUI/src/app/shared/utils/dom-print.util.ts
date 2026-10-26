export interface DomPrintOptions {
  contentElementId: string;
  title?: string;
  /** Optional heading rendered above the cloned content. */
  heading?: string;
  /** Extra CSS injected into the print document. */
  styles?: string;
  /** API/media origin for resolving relative image URLs. */
  mediaOrigin?: string;
  onWarning?: (message: string) => void;
}

const defaultWarn = (message: string) => window.alert(message);
const PRINT_IMAGE_TIMEOUT_MS = 4000;

/**
 * Prints a DOM element in a hidden iframe, waiting for images before opening the print dialog.
 */
export function printDomElement(options: DomPrintOptions): void {
  const warn = options.onWarning ?? defaultWarn;
  const source = document.getElementById(options.contentElementId);

  if (!source) {
    warn('Nothing is available to print.');
    return;
  }

  const clone = source.cloneNode(true) as HTMLElement;
  prepareCloneForPrint(clone, options.mediaOrigin);

  const heading = options.heading?.trim();
  const bodyHtml = heading
    ? `<h4 style="text-align:center;margin:0 0 1rem;">${escapeHtml(heading)}</h4>${clone.outerHTML}`
    : clone.outerHTML;

  openPrintDocument({
    title: options.title ?? options.heading ?? 'Print',
    styles: options.styles ?? '',
    bodyHtml,
    warn,
  });
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function prepareCloneForPrint(root: HTMLElement, mediaOrigin?: string): void {
  const baseOrigin = mediaOrigin?.trim() || window.location.origin;

  for (const image of Array.from(root.querySelectorAll('img'))) {
    const src = image.getAttribute('src')?.trim() ?? '';
    if (!src) {
      image.remove();
      continue;
    }

    if (/^https?:\/\//i.test(src) || src.startsWith('data:')) {
      continue;
    }

    try {
      image.setAttribute('src', new URL(src, baseOrigin).href);
    } catch {
      image.remove();
    }
  }
}

function openPrintDocument(args: {
  title: string;
  styles: string;
  bodyHtml: string;
  warn: (message: string) => void;
}): void {
  const iframe = document.createElement('iframe');
  iframe.setAttribute(
    'style',
    'position:fixed;right:0;bottom:0;width:0;height:0;border:0;visibility:hidden',
  );
  document.body.appendChild(iframe);

  const printWindow = iframe.contentWindow;
  const printDocument = printWindow?.document;
  if (!printWindow || !printDocument) {
    iframe.remove();
    args.warn('Unable to open print preview. Please try again.');
    return;
  }

  printDocument.open();
  printDocument.write(`<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <title>${escapeHtml(args.title)}</title>
  <style>${basePrintStyles()}</style>
  <style>${args.styles}</style>
</head>
<body>${args.bodyHtml}</body>
</html>`);
  printDocument.close();

  let finished = false;
  const cleanup = () => {
    window.setTimeout(() => iframe.remove(), 500);
  };

  const triggerPrint = () => {
    if (finished) {
      return;
    }
    finished = true;
    printWindow.focus();
    printWindow.print();
    printWindow.onafterprint = cleanup;
    window.setTimeout(cleanup, 2000);
  };

  const waitForImages = () => {
    const images = Array.from(printDocument.images).filter(img => Boolean(img.getAttribute('src')?.trim()));
    if (images.length === 0) {
      triggerPrint();
      return;
    }

    let pending = images.length;
    const done = () => {
      pending -= 1;
      if (pending <= 0) {
        window.setTimeout(triggerPrint, 150);
      }
    };

    window.setTimeout(triggerPrint, PRINT_IMAGE_TIMEOUT_MS);

    for (const image of images) {
      if (image.complete) {
        done();
      } else {
        image.addEventListener('load', done, { once: true });
        image.addEventListener('error', done, { once: true });
      }
    }
  };

  window.setTimeout(() => {
    if (printDocument.readyState === 'complete') {
      waitForImages();
    } else {
      printWindow.addEventListener('load', waitForImages, { once: true });
    }
  }, 50);
}

function basePrintStyles(): string {
  return `
    @page {
      size: A4 portrait;
      margin: 12mm;
    }

    html, body {
      margin: 0;
      padding: 0;
      background: #fff;
    }

    body {
      font-family: Arial, Helvetica, sans-serif;
      color: #111;
      -webkit-print-color-adjust: exact;
      print-color-adjust: exact;
    }
  `;
}

export function studentDetailsPrintStyles(): string {
  return `
    .legacy-body-border { border: 1px solid #000; padding: 1em; width: 100%; box-sizing: border-box; }
    .legacy-table-layout { width: 100%; border-collapse: collapse; }
    .legacy-cell { width: 50%; vertical-align: top; }
    .thumb-image { display: block; width: 16em; height: 16em; padding: 5px; object-fit: cover; box-sizing: border-box; }
    .image-holder { width: 16em; height: 16em; border: 2px double #000; margin-left: auto; margin-right: auto; overflow: hidden; }
    .thumb-placeholder { width: 16em; height: 16em; display: flex; align-items: center; justify-content: center; border: 2px double #000; margin-left: auto; margin-right: auto; }
    .dl-horizontal dt { float: left; width: 160px; overflow: hidden; clear: left; text-align: right; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; }
    .dl-horizontal dd { margin-left: 180px; margin-bottom: 6px; min-height: 18px; }
    .dl-horizontal dd:before, .dl-horizontal dd:after { display: table; content: ' '; }
    .dl-horizontal dd:after { clear: both; }
  `;
}

export function studentListPrintStyles(): string {
  return `
    .text-left { text-align: left; }
    .text-right { text-align: right; }
    .text-center { text-align: center; }
    .table { border-collapse: collapse !important; width: 100%; }
    .table-bordered th,
    .table-bordered td { border: 1px solid #ddd !important; }
    .table td,
    .table th { background-color: #fff !important; padding: 8px; }
  `;
}
