import type { GoodsReceiptDetail } from '@/shared/api/procurement.types';
import { procurementT } from '@/shared/i18n';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney, formatDisplayQuantity } from '@/shared/utils/money';
import { printHtmlDocument } from '@/shared/print/a4-print';

export function printGoodsReceipt(grn: GoodsReceiptDetail, tenantName?: string): void {
  const t = procurementT();
  const rows = grn.items
    .map(
      (line, idx) => `<tr>
        <td class="center">${idx + 1}</td>
        <td>${line.productCode}</td>
        <td>${line.productName}</td>
        <td>${line.batchNumber ?? '—'}</td>
        <td class="center">${line.expiryDate ? formatDisplayDate(line.expiryDate) : '—'}</td>
        <td class="num">${formatDisplayQuantity(line.quantity)}</td>
        <td class="num">${formatDisplayMoney(line.unitCost)}</td>
        <td class="num">${formatDisplayMoney(line.lineTotal)}</td>
      </tr>`,
    )
    .join('');

  const total = grn.items.reduce((sum, line) => sum + line.lineTotal, 0);

  printHtmlDocument(
    t('print.documentTitle', { number: grn.grnNumber }),
    `<h1>${t('print.heading')}</h1>
    <h2>${tenantName ?? ''}</h2>
    <div class="meta">
      <div class="meta-row">
        <span><strong>${t('print.grnNumber')}</strong> ${grn.grnNumber}</span>
        <span><strong>${t('print.receiptDate')}</strong> ${formatDisplayDate(grn.receiptDate)}</span>
      </div>
      <div class="meta-row">
        <span><strong>${t('print.supplier')}</strong> ${grn.supplierName ?? '—'}</span>
        <span><strong>${t('print.warehouse')}</strong> ${grn.warehouseName ?? '—'}</span>
      </div>
      <div class="meta-row">
        <span><strong>${t('print.poNumber')}</strong> ${grn.poNumber ?? '—'}</span>
        <span><strong>${t('print.notes')}</strong> ${grn.notes ?? '—'}</span>
      </div>
    </div>
    <table>
      <thead>
        <tr>
          <th class="center">${t('print.columns.index')}</th>
          <th>${t('print.columns.productCode')}</th>
          <th>${t('print.columns.productName')}</th>
          <th>${t('print.columns.batchNumber')}</th>
          <th class="center">${t('print.columns.expiry')}</th>
          <th class="num">${t('print.columns.quantity')}</th>
          <th class="num">${t('print.columns.unitCost')}</th>
          <th class="num">${t('print.columns.lineTotal')}</th>
        </tr>
      </thead>
      <tbody>${rows}</tbody>
      <tfoot>
        <tr>
          <td colspan="7" class="num">${t('print.total')}</td>
          <td class="num">${formatDisplayMoney(total)}</td>
        </tr>
      </tfoot>
    </table>
    <div class="signatures">
      <div><p>${t('print.signatures.preparedBy')}</p><em>${t('print.signatures.signHint')}</em></div>
      <div><p>${t('print.signatures.storekeeper')}</p><em>${t('print.signatures.signHint')}</em></div>
      <div><p>${t('print.signatures.accountant')}</p><em>${t('print.signatures.signHint')}</em></div>
    </div>`,
  );
}
