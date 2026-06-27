export function printHtmlDocument(title: string, bodyHtml: string): void {
  const win = window.open('', '_blank', 'noopener,noreferrer,width=900,height=700');
  if (!win) return;
  win.document.write(`<!DOCTYPE html>
<html><head><meta charset="utf-8"><title>${title}</title>
<style>
  @page { size: A4 portrait; margin: 12mm 14mm; }
  body { font-family: 'Segoe UI', Arial, sans-serif; font-size: 11px; color: #111; margin: 0; }
  h1 { font-size: 16px; margin: 0 0 4px; text-align: center; text-transform: uppercase; }
  h2 { font-size: 13px; margin: 0 0 12px; text-align: center; font-weight: normal; color: #444; }
  .meta { margin-bottom: 12px; }
  .meta-row { display: flex; gap: 24px; flex-wrap: wrap; margin-bottom: 4px; }
  .meta-row span { min-width: 220px; }
  table { width: 100%; border-collapse: collapse; margin-top: 8px; }
  th, td { border: 1px solid #333; padding: 5px 6px; vertical-align: top; }
  th { background: #f0f0f0; font-weight: 600; }
  td.num, th.num { text-align: right; font-variant-numeric: tabular-nums; }
  td.center, th.center { text-align: center; }
  tfoot td { font-weight: bold; }
  .signatures { margin-top: 28px; display: flex; justify-content: space-between; gap: 16px; }
  .signatures div { flex: 1; text-align: center; }
  .signatures p { margin: 48px 0 4px; font-weight: 600; }
  .note { margin-top: 12px; font-size: 10px; color: #555; }
</style></head><body>${bodyHtml}</body></html>`);
  win.document.close();
  win.focus();
  win.print();
}
