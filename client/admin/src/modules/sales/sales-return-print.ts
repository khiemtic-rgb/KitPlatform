import type { SalesReturnDetail } from '@/shared/api/sales.types';
import { SALES_PAYMENT_METHOD_LABELS } from '@/shared/api/sales.types';
import { loadReceiptStoreSettings } from '@/modules/sales/receipt-settings';
import { formatDisplayDate } from '@/shared/utils/date';
import { escapeHtml } from '@/shared/utils/escape-html';
import { formatDisplayMoney } from '@/shared/utils/money';

export async function buildSalesReturnHtml(ret: SalesReturnDetail): Promise<string> {
  const store = await loadReceiptStoreSettings();
  const rows = ret.items
    .map(
      (line) => `
      <tr>
        <td>${escapeHtml(line.productCode)}</td>
        <td>${escapeHtml(line.productName)}</td>
        <td>${escapeHtml(line.batchNumber)}</td>
        <td style="text-align:right">${line.quantity.toLocaleString('vi-VN')}</td>
        <td style="text-align:right">${formatDisplayMoney(line.refundAmount)}</td>
      </tr>`,
    )
    .join('');

  const paymentRows = (ret.payments ?? [])
    .map(
      (p) =>
        `<div>${escapeHtml(SALES_PAYMENT_METHOD_LABELS[p.paymentMethod] ?? p.paymentMethod)}: ${formatDisplayMoney(p.amount)}</div>`,
    )
    .join('');

  return `<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="utf-8" />
  <title>${escapeHtml(ret.returnNumber)}</title>
  <style>
    body { font-family: Arial, sans-serif; font-size: 13px; margin: 24px; color: #111; }
    h1 { font-size: 18px; margin: 0 0 4px; }
    .meta { margin-bottom: 16px; color: #444; }
    table { width: 100%; border-collapse: collapse; margin-top: 12px; }
    th, td { border: 1px solid #ccc; padding: 6px 8px; }
    th { background: #f5f5f5; text-align: left; }
    .total { margin-top: 16px; font-size: 15px; font-weight: bold; text-align: right; }
    .payments { margin-top: 8px; text-align: right; color: #333; }
  </style>
</head>
<body>
  <h1>PHIẾU TRẢ HÀNG</h1>
  <div class="meta">
    <div>${escapeHtml(store.name)}</div>
    <div>Số phiếu: <strong>${escapeHtml(ret.returnNumber)}</strong></div>
    <div>Đơn bán: <strong>${escapeHtml(ret.orderNumber)}</strong></div>
    <div>Ngày trả: ${formatDisplayDate(ret.returnDate)}</div>
    ${ret.reason ? `<div>Lý do trả: ${escapeHtml(ret.reason)}</div>` : ''}
  </div>
  <table>
    <thead>
      <tr>
        <th>Mã SP</th>
        <th>Tên SP</th>
        <th>Lô</th>
        <th style="text-align:right">SL trả</th>
        <th style="text-align:right">Tiền hoàn</th>
      </tr>
    </thead>
    <tbody>${rows}</tbody>
  </table>
  <div class="total">Tổng hoàn tiền: ${formatDisplayMoney(ret.totalRefund)}</div>
  ${paymentRows ? `<div class="payments"><strong>Hình thức hoàn tiền</strong>${paymentRows}</div>` : ''}
  <script>window.onload = () => { window.print(); }</script>
</body>
</html>`;
}

export async function printSalesReturn(ret: SalesReturnDetail): Promise<boolean> {
  const html = await buildSalesReturnHtml(ret);
  const win = window.open('', '_blank', 'width=800,height=600');
  if (!win) return false;
  win.document.write(html);
  win.document.close();
  return true;
}
