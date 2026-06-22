import type { SalesOrderDetail } from '@/shared/api/sales.types';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';
import {
  buildOrderNetPaymentLines,
  formatNetPaymentLinesHtml,
  formatNetPaymentTotal,
} from '@/modules/sales/sales-payment-summary';
import {
  computeOrderTotalRefunded,
  orderDisplayStatus,
  remainingLineNet,
} from '@/modules/sales/sales-return-pricing';

function returnedQty(line: SalesOrderDetail['items'][number]): number {
  return line.returnedQuantity ?? 0;
}

function netQty(line: SalesOrderDetail['items'][number]): number {
  return Math.max(0, line.quantity - returnedQty(line));
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
  const hasDiscounts = lineDiscountTotal > 0 || order.discountAmount > 0;

  const rows = order.items
    .map((line) => {
      const returned = returnedQty(line);
      const remaining = netQty(line);
      const lineTotal = hasReturns ? remainingLineNet(line, order) : line.lineTotal;
      const qtyCell = hasReturns
        ? `<td style="text-align:right">${line.quantity.toLocaleString('vi-VN')}</td>
        <td style="text-align:right">${returned > 0 ? returned.toLocaleString('vi-VN') : '—'}</td>
        <td style="text-align:right">${remaining.toLocaleString('vi-VN')}</td>`
        : `<td style="text-align:right">${line.quantity.toLocaleString('vi-VN')}</td>`;

      const discountNote =
        (line.discountAmount ?? 0) > 0
          ? `<br><small>CK: −${formatDisplayMoney(line.discountAmount ?? 0)}</small>`
          : '';

      return `
      <tr>
        <td>${line.productCode}</td>
        <td>${line.productName}${discountNote}</td>
        <td>${line.batchNumber ?? '—'}</td>
        <td>${line.expiryDate ? formatDisplayDate(line.expiryDate) : '—'}</td>
        ${qtyCell}
        <td style="text-align:right">${formatDisplayMoney(line.unitPrice)}</td>
        <td style="text-align:right">${formatDisplayMoney(lineTotal)}</td>
      </tr>`;
    })
    .join('');

  const qtyHeaders = hasReturns
    ? `<th style="text-align:right">SL bán</th>
        <th style="text-align:right">Đã trả</th>
        <th style="text-align:right">SL còn</th>`
    : `<th style="text-align:right">SL</th>`;

  const statusLabel = orderDisplayStatus({
    status: order.status,
    totalRefunded: order.totalRefunded ?? totalRefunded,
    items: order.items,
  }).label;

  const { lines: netPaymentLines, refundInferred } = buildOrderNetPaymentLines(order);
  const paymentSectionTitle = hasReturns ? 'Thanh toán sau trả hàng' : 'Thanh toán';
  const paymentRows = formatNetPaymentLinesHtml(netPaymentLines, hasReturns);
  const paymentNote =
    hasReturns && refundInferred
      ? '<div><small><em>Hoàn tiền phân bổ theo tỷ lệ thu ban đầu (in lại phiếu hoàn để xem chính xác từng hình thức).</em></small></div>'
      : hasReturns
        ? '<div><small><em>Số ròng = tiền thu ban đầu − tiền đã hoàn trên phiếu hoàn.</em></small></div>'
        : '';
  const paymentTotal =
    hasReturns && netPaymentLines.length > 0
      ? `<div><strong>Tổng đã thu ròng: ${formatNetPaymentTotal(netPaymentLines)}</strong></div>`
      : '';

  const totalBlock = `<div style="margin-top:12px;text-align:right;line-height:1.6">
      <div>Tổng tiền hàng: ${formatDisplayMoney(order.subtotal)}</div>
      ${lineDiscountTotal > 0 ? `<div>Chiết khấu sản phẩm: −${formatDisplayMoney(lineDiscountTotal)}</div>` : ''}
      ${order.discountAmount > 0 ? `<div>Chiết khấu đơn hàng: −${formatDisplayMoney(order.discountAmount)}</div>` : ''}
      <div><strong>Khách phải trả: ${formatDisplayMoney(order.totalAmount)}</strong></div>
      ${totalRefunded > 0 ? `<div>Đã hoàn trả (phiếu hoàn): −${formatDisplayMoney(totalRefunded)}</div>` : ''}
      ${hasReturns ? `<div class="total">Thành tiền hàng còn lại: ${formatDisplayMoney(netPayable)}</div>` : ''}
      ${hasReturns && (order.payments?.length ?? 0) > 0 ? '<div><em>Khách đã thanh toán đủ ban đầu — số trên là giá trị hàng giữ lại, không thu thêm.</em></div>' : ''}
      ${!hasReturns && !hasDiscounts ? '' : ''}
      ${paymentRows ? `<div style="margin-top:8px"><strong>${paymentSectionTitle}</strong>${paymentRows}${paymentTotal}${paymentNote}</div>` : ''}
    </div>`;

  return `<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="utf-8" />
  <title>Hóa đơn ${order.orderNumber}</title>
  <style>
    body { font-family: Arial, sans-serif; font-size: 13px; margin: 24px; color: #111; }
    h1 { font-size: 18px; margin: 0 0 4px; }
    .meta { margin-bottom: 16px; line-height: 1.5; }
    table { width: 100%; border-collapse: collapse; margin-top: 12px; }
    th, td { border: 1px solid #ccc; padding: 6px 8px; }
    th { background: #f5f5f5; text-align: left; }
    .total { margin-top: 8px; font-size: 15px; font-weight: bold; }
    @media print { body { margin: 12px; } }
  </style>
</head>
<body>
  <h1>HÓA ĐƠN BÁN HÀNG</h1>
  <div class="meta">
    <div><strong>Số đơn:</strong> ${order.orderNumber}</div>
    <div><strong>Ngày:</strong> ${formatDisplayDate(order.orderDate)}</div>
    <div><strong>Kho:</strong> ${order.warehouseName}</div>
    <div><strong>Khách:</strong> ${order.customerName ?? 'Khách lẻ'}</div>
    <div><strong>Trạng thái:</strong> ${statusLabel}</div>
    ${order.notes ? `<div><strong>Ghi chú:</strong> ${order.notes}</div>` : ''}
    ${hasReturns ? '<div><em>Thành tiền dòng phản ánh số lượng còn lại sau trả hàng</em></div>' : ''}
  </div>
  <table>
    <thead>
      <tr>
        <th>Mã SP</th>
        <th>Tên SP</th>
        <th>Lô</th>
        <th>HSD</th>
        ${qtyHeaders}
        <th style="text-align:right">Đơn giá</th>
        <th style="text-align:right">Thành tiền</th>
      </tr>
    </thead>
    <tbody>${rows}</tbody>
  </table>
  ${totalBlock}
  <script>window.onload = () => { window.print(); }</script>
</body>
</html>`;
}

export function printSalesInvoice(order: SalesOrderDetail): boolean {
  const html = buildSalesInvoiceHtml(order);
  const win = window.open('', '_blank', 'width=800,height=600');
  if (!win) return false;
  win.document.write(html);
  win.document.close();
  return true;
}
