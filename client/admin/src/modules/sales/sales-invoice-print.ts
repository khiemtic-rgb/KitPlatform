import dayjs from 'dayjs';
import type { SalesOrderDetail, SalesOrderItem } from '@/shared/api/sales.types';
import { SALES_PAYMENT_METHOD_LABELS } from '@/shared/api/sales.types';
import { formatDisplayDate } from '@/shared/utils/date';
import { buildOrderNetPaymentLines } from '@/modules/sales/sales-payment-summary';
import {
  computeOrderTotalRefunded,
  orderDisplayStatus,
  remainingLineNet,
} from '@/modules/sales/sales-return-pricing';

import { getReceiptStoreSettings } from '@/modules/sales/receipt-settings';
import { escapeHtml } from '@/shared/utils/escape-html';

function formatThermalMoney(v: number): string {
  return `${new Intl.NumberFormat('vi-VN').format(Math.round(v))}đ`;
}

function formatReceiptDateTime(iso?: string): string {
  if (!iso) return '—';
  const d = dayjs(iso);
  if (d.isValid()) return d.format('DD/MM/YYYY HH:mm');
  return formatDisplayDate(iso);
}

function dashedLine(char = '─'): string {
  return `<div class="rule">${char.repeat(32)}</div>`;
}

function rowBetween(left: string, right: string, className = ''): string {
  return `<div class="row ${className}"><span class="row-left">${left}</span><span class="row-right">${right}</span></div>`;
}

function returnedQty(line: SalesOrderItem): number {
  return line.returnedQuantity ?? 0;
}

function netQty(line: SalesOrderItem): number {
  return Math.max(0, line.quantity - returnedQty(line));
}

function buildLineItemHtml(
  line: SalesOrderItem,
  order: SalesOrderDetail,
  hasReturns: boolean,
): string {
  const qty = hasReturns ? netQty(line) : line.quantity;
  if (qty <= 0) return '';

  const lineTotal = hasReturns ? remainingLineNet(line, order) : line.lineTotal;
  const name = escapeHtml(line.productName);
  const unit = escapeHtml(line.unitName);
  const qtyLabel = `${qty.toLocaleString('vi-VN')} ${unit}`;

  const discount =
    (line.discountAmount ?? 0) > 0
      ? `<div class="row sub"><span class="row-left">  CK</span><span class="row-right">-${formatThermalMoney(line.discountAmount ?? 0)}</span></div>`
      : '';

  return `
    <div class="item">
      <div class="item-name">${name}</div>
      <div class="row">
        <span class="row-left">${qtyLabel} x ${formatThermalMoney(line.unitPrice)}</span>
        <span class="row-right">${formatThermalMoney(lineTotal)}</span>
      </div>
      ${discount}
    </div>`;
}

function buildPaymentSection(order: SalesOrderDetail, hasReturns: boolean): string {
  const { lines: netLines } = buildOrderNetPaymentLines(order);
  const payments = order.payments ?? [];

  if (hasReturns && netLines.length > 0) {
    return netLines
      .map((line) => {
        const label = SALES_PAYMENT_METHOD_LABELS[line.paymentMethod] ?? String(line.paymentMethod);
        return rowBetween(label, formatThermalMoney(line.net), 'sub');
      })
      .join('');
  }

  if (payments.length === 0) {
    return rowBetween('Thanh toán', formatThermalMoney(order.totalAmount));
  }

  return payments
    .map((p) => {
      const label = SALES_PAYMENT_METHOD_LABELS[p.paymentMethod] ?? String(p.paymentMethod);
      return rowBetween(label, formatThermalMoney(p.amount), 'sub');
    })
    .join('');
}

export function buildSalesInvoiceHtml(order: SalesOrderDetail): string {
  const hasReturns = order.items.some((line) => returnedQty(line) > 0);
  const totalRefunded =
    order.totalRefunded && order.totalRefunded > 0
      ? order.totalRefunded
      : computeOrderTotalRefunded(order);
  const netPayable = Math.max(0, order.totalAmount - totalRefunded);

  const lineDiscountTotal =
    order.lineDiscountTotal ??
    order.items.reduce((sum, line) => sum + (line.discountAmount ?? 0), 0);

  const itemBlocks = order.items
    .map((line) => buildLineItemHtml(line, order, hasReturns))
    .filter(Boolean)
    .join('');

  const statusLabel = orderDisplayStatus({
    status: order.status,
    totalRefunded: order.totalRefunded ?? totalRefunded,
    items: order.items,
  }).label;

  const receiptStore = getReceiptStoreSettings();
  const storeName = escapeHtml(receiptStore.name);
  const storeTagline = receiptStore.tagline ? escapeHtml(receiptStore.tagline) : '';
  const storePhone = receiptStore.phone ? escapeHtml(receiptStore.phone) : '';
  const storeAddress = receiptStore.address ? escapeHtml(receiptStore.address) : '';

  const headerContact = [
    storePhone ? `ĐT: ${storePhone}` : '',
    storeAddress,
  ]
    .filter(Boolean)
    .join(' · ');

  const totalsBlock = hasReturns
    ? `
      ${rowBetween('Tổng tiền hàng', formatThermalMoney(order.subtotal), 'sub')}
      ${lineDiscountTotal > 0 ? rowBetween('Chiết khấu SP', `-${formatThermalMoney(lineDiscountTotal)}`, 'sub') : ''}
      ${order.discountAmount > 0 ? rowBetween('Chiết khấu đơn', `-${formatThermalMoney(order.discountAmount)}`, 'sub') : ''}
      ${rowBetween('Khách đã trả', formatThermalMoney(order.totalAmount), 'sub')}
      ${totalRefunded > 0 ? rowBetween('Đã hoàn trả', `-${formatThermalMoney(totalRefunded)}`, 'sub') : ''}
      ${rowBetween('Còn lại', formatThermalMoney(netPayable), 'total')}
    `
    : `
      ${rowBetween('Tạm tính', formatThermalMoney(order.subtotal), 'sub')}
      ${lineDiscountTotal > 0 ? rowBetween('Chiết khấu SP', `-${formatThermalMoney(lineDiscountTotal)}`, 'sub') : ''}
      ${order.discountAmount > 0 ? rowBetween('Chiết khấu đơn', `-${formatThermalMoney(order.discountAmount)}`, 'sub') : ''}
      ${rowBetween('TỔNG CỘNG', formatThermalMoney(order.totalAmount), 'total')}
    `;

  const paymentBlock = buildPaymentSection(order, hasReturns);

  return `<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>HD ${escapeHtml(order.orderNumber)}</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    @page {
      size: 80mm auto;
      margin: 2mm 3mm;
    }
    html, body {
      width: 80mm;
      max-width: 80mm;
      margin: 0 auto;
      padding: 0;
      background: #fff;
      color: #000;
      -webkit-print-color-adjust: exact;
      print-color-adjust: exact;
    }
    body {
      font-family: 'Courier New', Courier, 'Liberation Mono', monospace;
      font-size: 12px;
      line-height: 1.35;
      padding: 4mm 3mm 6mm;
    }
    .receipt { width: 100%; }
    .center { text-align: center; }
    .store-name {
      font-size: 15px;
      font-weight: 700;
      letter-spacing: 0.02em;
      margin-bottom: 2px;
    }
    .store-sub {
      font-size: 11px;
      margin-bottom: 2px;
    }
    .store-contact {
      font-size: 11px;
      margin-bottom: 6px;
      word-break: break-word;
    }
    .title {
      font-size: 13px;
      font-weight: 700;
      margin: 4px 0 6px;
      letter-spacing: 0.04em;
    }
    .meta {
      font-size: 11px;
      margin-bottom: 2px;
      word-break: break-word;
    }
    .rule {
      text-align: center;
      overflow: hidden;
      white-space: nowrap;
      margin: 6px 0;
      font-size: 10px;
      letter-spacing: -0.05em;
      color: #000;
    }
    .item { margin-bottom: 6px; }
    .item-name {
      font-weight: 600;
      word-break: break-word;
      margin-bottom: 1px;
    }
    .row {
      display: flex;
      justify-content: space-between;
      gap: 4px;
      align-items: flex-start;
    }
    .row-left {
      flex: 1 1 auto;
      min-width: 0;
      word-break: break-word;
    }
    .row-right {
      flex: 0 0 auto;
      text-align: right;
      white-space: nowrap;
    }
    .row.sub { font-size: 11px; }
    .row.total {
      font-size: 14px;
      font-weight: 700;
      margin-top: 2px;
    }
    .row.total .row-left,
    .row.total .row-right {
      font-weight: 700;
    }
    .footer {
      margin-top: 8px;
      font-size: 11px;
      text-align: center;
      line-height: 1.45;
    }
    .note {
      font-size: 10px;
      margin-top: 4px;
      text-align: center;
      font-style: italic;
    }
    @media print {
      body { padding: 0; }
      .no-print { display: none; }
    }
    @media (max-width: 220px) {
      body { font-size: 11px; }
      .store-name { font-size: 13px; }
    }
  </style>
</head>
<body>
  <div class="receipt">
    <div class="center store-name">${storeName}</div>
    ${storeTagline ? `<div class="center store-sub">${storeTagline}</div>` : ''}
    ${headerContact ? `<div class="center store-contact">${headerContact}</div>` : ''}

    ${dashedLine()}
    <div class="center title">HÓA ĐƠN BÁN HÀNG</div>

    <div class="meta">Số: <strong>${escapeHtml(order.orderNumber)}</strong></div>
    <div class="meta">Ngày: ${formatReceiptDateTime(order.orderDate)}</div>
    ${order.shiftNumber ? `<div class="meta">Ca: ${escapeHtml(order.shiftNumber)}</div>` : ''}
    <div class="meta">Khách: ${escapeHtml(order.customerName ?? 'Khách lẻ')}</div>
    ${hasReturns ? `<div class="meta">TT: ${escapeHtml(statusLabel)}</div>` : ''}

    ${dashedLine()}
    <div class="items">${itemBlocks}</div>
    ${dashedLine()}

    ${totalsBlock}
    ${paymentBlock ? `${dashedLine()}${paymentBlock}` : ''}

    ${order.notes ? `<div class="note">Ghi chú: ${escapeHtml(order.notes)}</div>` : ''}
  ${hasReturns ? '<div class="note">Phiếu in lại sau trả hàng — số lượng là phần còn lại.</div>' : ''}

    ${dashedLine()}
    <div class="footer">
      <div>Cảm ơn quý khách!</div>
      <div>Hẹn gặp lại</div>
      <div class="note" style="margin-top:6px">Phiếu bán lẻ — không phải hóa đơn GTGT</div>
    </div>
  </div>
  <script>window.onload = () => { window.print(); }</script>
</body>
</html>`;
}

export function printSalesInvoice(order: SalesOrderDetail): boolean {
  const html = buildSalesInvoiceHtml(order);
  const win = window.open('', '_blank', 'width=360,height=720');
  if (!win) return false;
  win.document.write(html);
  win.document.close();
  return true;
}
