import dayjs from 'dayjs';
import type { ReceiptStoreSettings, SalesOrderDetail } from '@/shared/api/sales.types';
import { formatMoney } from '@/shared/utils/money';

const PAYMENT_LABEL: Record<number, string> = {
  1: 'Tiền mặt',
  2: 'Thẻ',
  3: 'Chuyển khoản',
  4: 'Ví điện tử',
  5: 'Ghi nợ',
};

export function buildReceiptHtml(order: SalesOrderDetail, store: ReceiptStoreSettings): string {
  const lines = order.items
    .map(
      (item) => `
      <div class="line">
        <div><strong>${escapeHtml(item.productName)}</strong></div>
        <div class="muted">${escapeHtml(item.productCode)} · ${escapeHtml(item.unitName)}${
          item.batchNumber ? ` · Lô ${escapeHtml(item.batchNumber)}` : ''
        }</div>
        <div class="row"><span>${item.quantity} x ${formatMoney(item.unitPrice)}</span><span>${formatMoney(item.lineTotal)}</span></div>
      </div>`,
    )
    .join('');

  const payments = order.payments
    .map((p) => `<div class="row"><span>${PAYMENT_LABEL[p.paymentMethod] ?? 'TT'}</span><span>${formatMoney(p.amount)}</span></div>`)
    .join('');

  return `<!DOCTYPE html>
<html lang="vi"><head><meta charset="utf-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1"/>
<style>
  @page { size: 80mm auto; margin: 2mm 3mm; }
  body { font-family: 'Courier New', monospace; font-size: 12px; width: 72mm; margin: 0 auto; color: #000; }
  .center { text-align: center; }
  .store { font-size: 15px; font-weight: 700; }
  .muted { font-size: 11px; color: #333; }
  .rule { text-align: center; margin: 6px 0; overflow: hidden; }
  .row { display: flex; justify-content: space-between; gap: 8px; margin: 2px 0; }
  .line { margin: 6px 0; padding-bottom: 4px; border-bottom: 1px dashed #ccc; }
  .total { font-size: 14px; font-weight: 700; margin-top: 8px; }
</style></head><body>
  <div class="center store">${escapeHtml(store.name)}</div>
  ${store.address ? `<div class="center muted">${escapeHtml(store.address)}</div>` : ''}
  ${store.phone ? `<div class="center muted">ĐT: ${escapeHtml(store.phone)}</div>` : ''}
  <div class="rule">────────────────</div>
  <div class="center"><strong>HÓA ĐƠN BÁN HÀNG</strong></div>
  <div>Số: ${escapeHtml(order.orderNumber)}</div>
  <div>Ngày: ${dayjs(order.orderDate).format('DD/MM/YYYY')}</div>
  <div>Giờ bán: ${dayjs(order.orderDate).format('HH:mm')}</div>
  ${order.customerName ? `<div>KH: ${escapeHtml(order.customerName)}</div>` : ''}
  <div class="rule">────────────────</div>
  ${lines}
  <div class="rule">────────────────</div>
  <div class="row total"><span>TỔNG</span><span>${formatMoney(order.totalAmount)}</span></div>
  ${payments}
  <div class="rule">────────────────</div>
  <div class="center muted">Cảm ơn quý khách!</div>
</body></html>`;
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

export function printReceiptDocument(html: string): void {
  const iframe = document.createElement('iframe');
  iframe.style.position = 'fixed';
  iframe.style.right = '0';
  iframe.style.bottom = '0';
  iframe.style.width = '0';
  iframe.style.height = '0';
  iframe.style.border = 'none';
  document.body.appendChild(iframe);
  const doc = iframe.contentDocument ?? iframe.contentWindow?.document;
  if (!doc) {
    document.body.removeChild(iframe);
    window.print();
    return;
  }
  doc.open();
  doc.write(html);
  doc.close();

  let printed = false;
  const doPrint = () => {
    if (printed) return;
    printed = true;
    iframe.contentWindow?.focus();
    iframe.contentWindow?.print();
    setTimeout(() => {
      if (iframe.parentNode) document.body.removeChild(iframe);
    }, 1000);
  };

  iframe.onload = () => doPrint();
  // Some mobile WebViews never fire onload after doc.write — fallback shortly after.
  window.setTimeout(() => doPrint(), 400);
}
