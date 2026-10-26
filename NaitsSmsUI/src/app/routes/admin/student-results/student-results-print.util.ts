export interface StudentResultPrintOptions {
  contentElementId?: string;
  hasSheets?: boolean;
  /** API/media origin, e.g. https://localhost:7049 for Passport and school logos. */
  mediaOrigin?: string;
  onWarning?: (message: string) => void;
}

const defaultWarn = (message: string) => window.alert(message);
const PRINT_IMAGE_TIMEOUT_MS = 4000;

/**
 * Opens a print preview for legacy student result sheets rendered under #pageContent.
 * Shared by admin and student/parent portal result pages.
 */
export function printStudentResultSheets(options: StudentResultPrintOptions = {}): void {
  const warn = options.onWarning ?? defaultWarn;
  const contentId = options.contentElementId ?? 'pageContent';

  if (options.hasSheets === false) {
    warn('No report content to print.');
    return;
  }

  const content = document.getElementById(contentId);
  if (!content) {
    warn('No report content to print.');
    return;
  }

  const sheetNodes = Array.from(content.querySelectorAll('.legacy-report-sheet'));
  if (sheetNodes.length === 0) {
    warn('No report content to print.');
    return;
  }

  const embeddedSheetCss = Array.from(content.querySelectorAll('style'))
    .map(styleEl => styleEl.textContent ?? '')
    .join('\n')
    .replace(/page-break-after\s*:\s*always\s*;?/gi, '')
    .replace(/break-after\s*:\s*page\s*;?/gi, '');

  const pagesHtml = sheetNodes
    .map(node => {
      const clone = node.cloneNode(true) as HTMLElement;
      clone.querySelectorAll('style').forEach(styleEl => styleEl.remove());
      prepareCloneForPrint(clone, options.mediaOrigin);
      return `<section class="print-a4-page">${clone.outerHTML}</section>`;
    })
    .join('');

  openStudentResultPrintDocument(pagesHtml, embeddedSheetCss, warn);
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

function openStudentResultPrintDocument(
  html: string,
  embeddedSheetCss: string,
  warn: (message: string) => void,
): void {
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
    warn('Unable to open print preview. Please try again.');
    return;
  }

  printDocument.open();
  printDocument.write(`<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <title>Student Result</title>
  <style>${embeddedSheetCss}</style>
  <style>${studentResultPrintStyles()}</style>
</head>
<body>${html}</body>
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

/** Print-window CSS. Screen component SCSS does not apply in the popup document. */
export function studentResultPrintStyles(): string {
  return `
    @page {
      size: A4 portrait;
      margin: 10mm;
    }

    html, body {
      margin: 0;
      padding: 0;
      width: 100%;
      background: #fff;
    }

    body {
      font-family: Arial, Helvetica, sans-serif;
      color: #111;
      font-size: var(--report-font-size, 14px);
      -webkit-print-color-adjust: exact;
      print-color-adjust: exact;
    }

    .print-a4-page {
      display: block;
      box-sizing: border-box;
      width: 100%;
      margin: 0;
      padding: 0;
      background: #fff;
      overflow: visible;
      page-break-after: always;
      break-after: page;
      page-break-inside: auto;
      break-inside: auto;
    }

    .print-a4-page:last-child {
      page-break-after: auto;
      break-after: auto;
    }

    .legacy-report-sheet {
      display: block;
      width: 100% !important;
      max-width: none !important;
      min-height: 0 !important;
      margin: 0 !important;
      padding: 0;
      box-sizing: border-box;
      background: #fff;
      box-shadow: none !important;
      font-size: var(--report-font-size, 14px);
      page-break-after: auto !important;
      break-after: auto !important;
    }

    .page {
      margin: 0;
      border: initial;
      border-radius: initial;
      width: 100%;
      min-height: initial;
      box-shadow: initial;
      background: #ffffff;
      page-break-after: auto;
      break-after: auto;
    }

    .text-left { text-align: left; }
    .text-right { text-align: right; }
    .text-center { text-align: center; }
    .text-primary { color: #0d6efd; }

    .table {
      border-collapse: collapse !important;
      width: 100%;
      table-layout: fixed;
    }
    .table-bordered th,
    .table-bordered td {
      border: 1px solid #ddd !important;
    }
    .table td,
    .table th {
      background-color: #fff !important;
    }
    .table > thead > tr > th,
    .table > tbody > tr > th,
    .table > tfoot > tr > th,
    .table > thead > tr > td,
    .table > tbody > tr > td,
    .table > tfoot > tr > td {
      vertical-align: middle;
    }
    .borderLess { border-width: 0 !important; }
    .tableFontSize { font-size: var(--report-font-size, 14px); }

    th.rotate {
      height: 100px;
      width: 1em;
      white-space: nowrap;
      padding: 0;
    }
    .wrap {
      white-space: nowrap;
      width: 6%;
    }
    th.rotate > div {
      transform: translate(2px, 51px) rotate(-90deg);
      width: 5px;
    }
    th.rotate > div > span {
      padding-left: 6px;
    }

    table {
      width: 100%;
      table-layout: fixed;
    }
    .img-responsive {
      display: inline-block;
      max-width: 100%;
      height: auto;
    }
    .legacy-report__logo {
      object-fit: contain;
    }
    .col-md-12 { width: 100%; }
  `;
}
