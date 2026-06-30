import type { SalesReturnDetail } from '@/shared/api/sales.types';
import { loadReceiptStoreSettings } from '@/modules/sales/receipt-settings';
import {
  buildThermalReceiptDocument,
  dashedLine,
  formatReceiptDateTime,
  formatThermalMoney,
  openThermalPrintWindow,
  rowBetween,
} from '@/modules/sales/thermal-receipt-print';
import { escapeHtml } from '@/shared/utils/escape-html';
import { salesT } from '@/shared/i18n';

function paymentMethodLabel(method: number): string {
  const t = salesT();
  return t(`enums.paymentMethod.${method}`, { defaultValue: String(method) });
}

function buildReturnItemHtml(line: SalesReturnDetail['items'][number]): string {
  const t = salesT();
  const name = escapeHtml(line.productName);
  const batch = escapeHtml(line.batchNumber || '—');
  const qtyLabel = `${line.quantity.toLocaleString()}`;

  return `
    <div class="item">
      <div class="item-name">${name}</div>
      <div class="item-sub">${t('receipt.return.batch')}: ${batch}</div>
      <div class="row">
        <span class="row-left">${t('receipt.return.qtyReturned')}: ${qtyLabel}</span>
        <span class="row-right">${formatThermalMoney(line.refundAmount)}</span>
      </div>
    </div>`;
}

function buildRefundPaymentSection(ret: SalesReturnDetail): string {
  const t = salesT();
  const payments = ret.payments ?? [];
  if (payments.length === 0) {
    return rowBetween(t('receipt.return.refund'), formatThermalMoney(ret.totalRefund), 'sub');
  }

  return payments
    .map((p) => {
      const label = paymentMethodLabel(p.paymentMethod);
      return rowBetween(label, formatThermalMoney(p.amount), 'sub');
    })
    .join('');
}

export async function buildSalesReturnHtml(ret: SalesReturnDetail): Promise<string> {
  const t = salesT();
  const store = await loadReceiptStoreSettings();
  const storeName = escapeHtml(store.name);
  const storeTagline = store.tagline ? escapeHtml(store.tagline) : '';
  const storePhone = store.phone ? escapeHtml(store.phone) : '';
  const storeAddress = store.address ? escapeHtml(store.address) : '';
  const headerContact = [
    storePhone ? `${t('receipt.invoice.phonePrefix')}: ${storePhone}` : '',
    storeAddress,
  ]
    .filter(Boolean)
    .join(' · ');

  const itemBlocks = ret.items.map((line) => buildReturnItemHtml(line)).join('');
  const paymentBlock = buildRefundPaymentSection(ret);

  const bodyHtml = `
    <div class="center store-name">${storeName}</div>
    ${storeTagline ? `<div class="center store-sub">${storeTagline}</div>` : ''}
    ${headerContact ? `<div class="center store-contact">${headerContact}</div>` : ''}

    ${dashedLine()}
    <div class="center title">${escapeHtml(t('receipt.return.title'))}</div>

    <div class="meta">${t('receipt.return.returnNo')}: <strong>${escapeHtml(ret.returnNumber)}</strong></div>
    <div class="meta">${t('receipt.return.orderNo')}: ${escapeHtml(ret.orderNumber)}</div>
    <div class="meta">${t('receipt.return.returnDate')}: ${formatReceiptDateTime(ret.returnDate)}</div>
    ${ret.reason ? `<div class="meta">${t('receipt.return.reason')}: ${escapeHtml(ret.reason)}</div>` : ''}

    ${dashedLine()}
    <div class="items">${itemBlocks}</div>
    ${dashedLine()}

    ${rowBetween(t('receipt.return.refundTotal'), formatThermalMoney(ret.totalRefund), 'total')}
    ${paymentBlock ? `${dashedLine()}${paymentBlock}` : ''}

    ${dashedLine()}
    <div class="footer">
      <div>${escapeHtml(t('receipt.return.footerDone'))}</div>
      <div class="note" style="margin-top:6px">${escapeHtml(t('receipt.return.footerNote'))}</div>
    </div>`;

  return buildThermalReceiptDocument(t('receipt.return.docTitle', { number: escapeHtml(ret.returnNumber) }), bodyHtml);
}

export async function printSalesReturn(ret: SalesReturnDetail): Promise<boolean> {
  const html = await buildSalesReturnHtml(ret);
  return openThermalPrintWindow(html);
}
