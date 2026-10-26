/** Opens a styled print preview for fee payment receipts. */
export function openReceiptPrintWindow(
  html: string,
  title = 'Payment Receipt',
  extraStyles = '',
): void {
  if (!html) {
    return;
  }

  const win = window.open('', '_blank', 'width=900,height=700');
  if (!win) {
    return;
  }

  // Legacy LoadStudentsForTermlyFees: `.page { page-break-after: always }` with up to 3 cards per page.
  const styles = `
    <style>
      @page {
        size: A4 portrait;
        margin: 10mm;
      }
      * { box-sizing: border-box; }
      body {
        margin: 0;
        font-family: Arial, Helvetica, sans-serif;
        color: #222;
        background: #fff;
      }
      .acct-fresh-receipt-print-batch {
        width: 100%;
      }
      .acct-fresh-receipt-print-page {
        margin: 0;
        padding: 0;
        background: #fff;
      }
      .acct-fresh-receipt-print--card {
        border: 1px solid #333;
        padding: 0.35em 0.55em 0.25em;
        margin: 0 0.35em 4em;
      }
      .acct-fresh-receipt-print-page .acct-fresh-receipt-print--card:last-child {
        margin-bottom: 0;
      }
      .acct-fresh-receipt-print__header {
        width: 100%;
        border-collapse: collapse;
      }
      .acct-fresh-receipt-print__logo-cell img {
        width: 4.6em;
        height: 4.6em;
        object-fit: contain;
      }
      .acct-fresh-receipt-print__title {
        margin: 0.25rem 0 0.35rem;
        text-align: center;
        color: #e74c3c;
        font-size: 0.95rem;
        font-weight: 700;
      }
      .acct-fresh-receipt-print__pid {
        color: #2980b9;
      }
      .acct-fresh-receipt-print__grid {
        width: 100%;
        border-collapse: collapse;
      }
      .acct-fresh-receipt-print__grid th,
      .acct-fresh-receipt-print__grid td {
        padding: 0.18rem 0.35rem;
        border: 1px solid #ccc;
        font-size: 0.78rem;
        vertical-align: middle;
      }
      .acct-fresh-receipt-print__grid th {
        text-align: right;
        font-weight: 700;
        white-space: nowrap;
        width: 16%;
      }
      .acct-fresh-receipt-print__grid td {
        text-align: left;
      }
      .acct-fresh-receipt-print__sig {
        width: 7em;
        height: 1.6em;
        object-fit: contain;
      }
      .acct-fresh__cleared {
        color: #26b99a;
        font-weight: 700;
      }
      .acct-fresh__uncleared {
        color: #e74c3c;
        font-weight: 700;
      }
      img { max-width: 100%; }
      @media print {
        body { margin: 0; }
        .acct-fresh-receipt-print-page {
          page-break-after: always;
          break-after: page;
        }
        .acct-fresh-receipt-print-batch .acct-fresh-receipt-print-page:last-child {
          page-break-after: auto;
          break-after: auto;
        }
        .acct-fresh-receipt-print--card {
          page-break-inside: avoid;
          break-inside: avoid;
        }
      }
      ${extraStyles}
    </style>
  `;

  win.document.open();
  win.document.write(
    `<!doctype html><html><head><meta charset="utf-8"><title>${title}</title>${styles}</head><body>${html}</body></html>`,
  );
  win.document.close();

  const triggerPrint = (): void => {
    win.focus();
    win.print();
    win.close();
  };

  // Wait for images (logo/signature) so preview layout matches on-screen receipt.
  const images = Array.from(win.document.images);
  if (images.length === 0) {
    setTimeout(triggerPrint, 50);
    return;
  }

  let remaining = images.length;
  const onReady = (): void => {
    remaining -= 1;
    if (remaining <= 0) {
      setTimeout(triggerPrint, 50);
    }
  };

  for (const img of images) {
    if (img.complete) {
      onReady();
    } else {
      img.addEventListener('load', onReady, { once: true });
      img.addEventListener('error', onReady, { once: true });
    }
  }
}

/** Legacy ExecutiveReport PrintPage — table-bordered grid lines for detail print. */
export const EXECUTIVE_REPORT_DETAIL_PRINT_STYLES = `
  .text-left { text-align: left; }
  .text-right { text-align: right; }
  .text-center { text-align: center; }
  .text-danger { color: #a94442; }
  .acct-exec__detail-print {
    border: 1px solid #333;
    margin: 1em;
    padding: 0.5em;
  }
  .acct-exec__detail-table,
  .table {
    width: 100%;
    margin: 1.4em auto;
    border-collapse: collapse !important;
  }
  .acct-exec__detail-table th,
  .acct-exec__detail-table td,
  .table-bordered th,
  .table-bordered td {
    border: 1px solid #ddd !important;
    padding: 8px;
    vertical-align: middle;
  }
  .acct-exec__detail-table td,
  .acct-exec__detail-table th,
  .table td,
  .table th {
    background-color: #fff !important;
  }
`;
