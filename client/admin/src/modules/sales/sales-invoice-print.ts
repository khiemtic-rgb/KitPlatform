import type { TFunction } from 'i18next';
import type { SalesOrderDetail, SalesOrderItem } from '@/shared/api/sales.types';
import { buildOrderNetPaymentLines } from '@/modules/sales/sales-payment-summary';
import {
  computeOrderTotalRefunded,
  isPartiallyReturnedFromItems,
  isPartiallyReturnedOrder,
  remainingLineNet,
} from '@/modules/sales/sales-return-pricing';
import { loadReceiptStoreSettings, type ReceiptStoreSettings } from '@/modules/sales/receipt-settings';
import {
  buildThermalReceiptDocument,
  dashedLine,
  formatReceiptDateTime,
  formatThermalMoney,
  openThermalPrintWindow,
  rowBetween,
} from '@/modules/sales/thermal-receipt-print';
import { resolveOrderPaymentSummary, SALES_PAYMENT_METHOD_CREDIT } from '@/modules/sales/sales-order-payment-summary';
import { escapeHtml } from '@/shared/utils/escape-html';
import { salesT } from '@/shared/i18n';

function paymentMethodLabel(t: TFunction<'sales'>, method: number): string {
  return t(`enums.paymentMethod.${method}`, { defaultValue: String(method) });
}

function formatPoints(value: number): string {
  return value.toLocaleString();
}

function orderStatusLabel(
  t: TFunction<'sales'>,
  order: Pick<SalesOrderDetail, 'status' | 'totalRefunded' | 'items'>,
): string {
  const partial =
    isPartiallyReturnedOrder(order.status, order.totalRefunded) ||
    (order.items != null && isPartiallyReturnedFromItems(order.status, order.items));
  if (partial) return t('receipt.orderStatus.partialReturn');
  return t(`receipt.orderStatus.${order.status}`, { defaultValue: String(order.status) });
}

function voucherDiscountLabel(t: TFunction<'sales'>, order: SalesOrderDetail): string {
  const code = order.voucherCode?.trim();
  const name = order.voucherName?.trim();
  if (code && name) return t('receipt.voucherNamed', { code, name });
  if (code) return t('receipt.voucherCode', { code });
  if (name) return `${t('receipt.voucher')} ${name}`;
  return t('receipt.voucher');
}

function returnedQty(line: SalesOrderItem): number {
  return line.returnedQuantity ?? 0;
}

function netQty(line: SalesOrderItem): number {
  return Math.max(0, line.quantity - returnedQty(line));
}

function buildLineItemHtml(
  t: TFunction<'sales'>,
  line: SalesOrderItem,
  order: SalesOrderDetail,
  hasReturns: boolean,
): string {
  const qty = hasReturns ? netQty(line) : line.quantity;
  if (qty <= 0) return '';

  const lineTotal = hasReturns ? remainingLineNet(line, order) : line.lineTotal;
  const name = escapeHtml(line.productName);
  const unit = escapeHtml(line.unitName);
  const qtyLabel = `${qty.toLocaleString()} ${unit}`;

  const discount =
    (line.discountAmount ?? 0) > 0
      ? `<div class="row sub"><span class="row-left">  ${escapeHtml(t('receipt.invoice.lineDiscount'))}</span><span class="row-right">-${formatThermalMoney(line.discountAmount ?? 0)}</span></div>`
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

function buildPaymentSection(t: TFunction<'sales'>, order: SalesOrderDetail, hasReturns: boolean): string {
  const { lines: netLines } = buildOrderNetPaymentLines(order);
  const { amountPaid, outstanding, hasOutstanding } = resolveOrderPaymentSummary(order);

  if (hasReturns && netLines.length > 0) {
    return netLines
      .map((line) => {
        const label = paymentMethodLabel(t, line.paymentMethod);
        return rowBetween(label, formatThermalMoney(line.net), 'sub');
      })
      .join('');
  }

  const paymentRows = (order.payments ?? [])
    .filter((p) => p.paymentMethod !== SALES_PAYMENT_METHOD_CREDIT)
    .map((p) => {
      const label = paymentMethodLabel(t, p.paymentMethod);
      return rowBetween(label, formatThermalMoney(p.amount), 'sub');
    })
    .join('');

  if (hasOutstanding) {
    return (
      paymentRows +
      rowBetween(t('receipt.invoice.paid'), formatThermalMoney(amountPaid), 'sub') +
      rowBetween(t('receipt.invoice.outstanding'), formatThermalMoney(outstanding), 'sub')
    );
  }

  if (paymentRows) return paymentRows;

  return rowBetween(t('receipt.invoice.payment'), formatThermalMoney(order.totalAmount));
}

export function buildSalesInvoiceHtml(order: SalesOrderDetail, receiptStore: ReceiptStoreSettings): string {
  const t = salesT();
  const hasReturns = order.items.some((line) => returnedQty(line) > 0);
  const { hasOutstanding } = resolveOrderPaymentSummary(order);
  const totalRefunded =
    order.totalRefunded && order.totalRefunded > 0
      ? order.totalRefunded
      : computeOrderTotalRefunded(order);
  const netPayable = Math.max(0, order.totalAmount - totalRefunded);

  const lineDiscountTotal =
    order.lineDiscountTotal ??
    order.items.reduce((sum, line) => sum + (line.discountAmount ?? 0), 0);

  const itemBlocks = order.items
    .map((line) => buildLineItemHtml(t, line, order, hasReturns))
    .filter(Boolean)
    .join('');

  const statusLabel = orderStatusLabel(t, {
    status: order.status,
    totalRefunded: order.totalRefunded ?? totalRefunded,
    items: order.items,
  });

  const storeName = escapeHtml(receiptStore.name);
  const storeTagline = receiptStore.tagline ? escapeHtml(receiptStore.tagline) : '';
  const storePhone = receiptStore.phone ? escapeHtml(receiptStore.phone) : '';
  const storeAddress = receiptStore.address ? escapeHtml(receiptStore.address) : '';

  const headerContact = [
    storePhone ? `${t('receipt.invoice.phonePrefix')}: ${storePhone}` : '',
    storeAddress,
  ]
    .filter(Boolean)
    .join(' · ');

  const totalsBlock = hasReturns
    ? `
      ${rowBetween(t('receipt.invoice.subtotal'), formatThermalMoney(order.subtotal), 'sub')}
      ${lineDiscountTotal > 0 ? rowBetween(t('receipt.invoice.lineDiscountTotal'), `-${formatThermalMoney(lineDiscountTotal)}`, 'sub') : ''}
      ${order.discountAmount > 0 ? rowBetween(t('receipt.invoice.orderDiscount'), `-${formatThermalMoney(order.discountAmount)}`, 'sub') : ''}
      ${(order.voucherDiscountAmount ?? 0) > 0 ? rowBetween(voucherDiscountLabel(t, order), `-${formatThermalMoney(order.voucherDiscountAmount ?? 0)}`, 'sub') : ''}
      ${(order.loyaltyDiscountAmount ?? 0) > 0 ? rowBetween(t('receipt.invoice.pointsRedeemedTotal', { points: formatPoints(order.loyaltyPointsRedeemed ?? 0) }), `-${formatThermalMoney(order.loyaltyDiscountAmount ?? 0)}`, 'sub') : ''}
      ${rowBetween(t('receipt.invoice.customerPaid'), formatThermalMoney(order.totalAmount), 'sub')}
      ${totalRefunded > 0 ? rowBetween(t('receipt.invoice.refunded'), `-${formatThermalMoney(totalRefunded)}`, 'sub') : ''}
      ${rowBetween(t('receipt.invoice.remaining'), formatThermalMoney(netPayable), 'total')}
    `
    : `
      ${rowBetween(t('receipt.invoice.subtotal'), formatThermalMoney(order.subtotal), 'sub')}
      ${lineDiscountTotal > 0 ? rowBetween(t('receipt.invoice.lineDiscountTotal'), `-${formatThermalMoney(lineDiscountTotal)}`, 'sub') : ''}
      ${order.discountAmount > 0 ? rowBetween(t('receipt.invoice.orderDiscount'), `-${formatThermalMoney(order.discountAmount)}`, 'sub') : ''}
      ${(order.voucherDiscountAmount ?? 0) > 0 ? rowBetween(voucherDiscountLabel(t, order), `-${formatThermalMoney(order.voucherDiscountAmount ?? 0)}`, 'sub') : ''}
      ${(order.loyaltyDiscountAmount ?? 0) > 0 ? rowBetween(t('receipt.invoice.pointsRedeemedTotal', { points: formatPoints(order.loyaltyPointsRedeemed ?? 0) }), `-${formatThermalMoney(order.loyaltyDiscountAmount ?? 0)}`, 'sub') : ''}
      ${rowBetween(t('receipt.invoice.grandTotal'), formatThermalMoney(order.totalAmount), 'total')}
    `;

  const paymentBlock = buildPaymentSection(t, order, hasReturns);

  const bodyHtml = `
    <div class="center store-name">${storeName}</div>
    ${storeTagline ? `<div class="center store-sub">${storeTagline}</div>` : ''}
    ${headerContact ? `<div class="center store-contact">${headerContact}</div>` : ''}

    ${dashedLine()}
    <div class="center title">${escapeHtml(t('receipt.invoice.title'))}</div>

    <div class="meta">${t('receipt.invoice.orderNo')}: <strong>${escapeHtml(order.orderNumber)}</strong></div>
    <div class="meta">${t('receipt.invoice.date')}: ${formatReceiptDateTime(order.orderDate)}</div>
    ${order.shiftNumber ? `<div class="meta">${t('receipt.invoice.shift')}: ${escapeHtml(order.shiftNumber)}</div>` : ''}
    ${order.customerName ? `<div class="meta">${t('receipt.invoice.customer')}: ${escapeHtml(order.customerName)}</div>` : `<div class="meta">${t('receipt.invoice.customer')}: ${escapeHtml(t('receipt.invoice.walkIn'))}</div>`}
    ${(order.loyaltyPointsEarned ?? 0) > 0 ? `<div class="meta">${escapeHtml(t('receipt.invoice.pointsEarned', { points: formatPoints(order.loyaltyPointsEarned!) }))}</div>` : ''}
    ${hasOutstanding ? `<div class="meta">${escapeHtml(t('receipt.invoice.creditNoPoints'))}</div>` : ''}
    ${(order.loyaltyPointsRedeemed ?? 0) > 0 ? `<div class="meta">${escapeHtml(t('receipt.invoice.pointsRedeemed', { points: formatPoints(order.loyaltyPointsRedeemed!), amount: formatThermalMoney(order.loyaltyDiscountAmount ?? 0) }))}</div>` : ''}
    ${(order.voucherDiscountAmount ?? 0) > 0 ? `<div class="meta">${escapeHtml(t('receipt.invoice.voucherLine', { amount: formatThermalMoney(order.voucherDiscountAmount ?? 0) }))}${order.voucherCode ? ` (${escapeHtml(order.voucherCode)})` : ''}</div>` : ''}
    ${hasReturns ? `<div class="meta">${t('receipt.invoice.status')}: ${escapeHtml(statusLabel)}</div>` : ''}

    ${dashedLine()}
    <div class="items">${itemBlocks}</div>
    ${dashedLine()}

    ${totalsBlock}
    ${paymentBlock ? `${dashedLine()}${paymentBlock}` : ''}

    ${order.notes ? `<div class="note">${t('receipt.invoice.notes')}: ${escapeHtml(order.notes)}</div>` : ''}
    ${hasReturns ? `<div class="note">${escapeHtml(t('receipt.invoice.reprintAfterReturn'))}</div>` : ''}

    ${dashedLine()}
    <div class="footer">
      <div>${escapeHtml(t('receipt.invoice.thanks'))}</div>
      <div>${escapeHtml(t('receipt.invoice.seeAgain'))}</div>
      <div class="note" style="margin-top:6px">${escapeHtml(t('receipt.invoice.disclaimer'))}</div>
    </div>`;

  return buildThermalReceiptDocument(t('receipt.invoice.docTitle', { number: escapeHtml(order.orderNumber) }), bodyHtml);
}

export async function printSalesInvoice(order: SalesOrderDetail): Promise<boolean> {
  const receiptStore = await loadReceiptStoreSettings();
  const html = buildSalesInvoiceHtml(order, receiptStore);
  return openThermalPrintWindow(html);
}
