import type { AdjustmentCountPreviewLine, AdjustmentDetail } from '@/shared/api/inventory.types';
import { inventoryT } from '@/shared/i18n';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayQuantity } from '@/shared/utils/money';
import { printHtmlDocument } from '@/shared/print/a4-print';

export function printInventoryCountSheet(
  detail: AdjustmentDetail,
  previewLines: AdjustmentCountPreviewLine[],
  tenantName?: string,
): void {
  const t = inventoryT();
  const rows = previewLines
    .map(
      (line, idx) => `<tr>
        <td class="center">${idx + 1}</td>
        <td>${line.productCode}</td>
        <td>${line.productName}</td>
        <td>${line.batchNumber ?? '—'}</td>
        <td class="num">${formatDisplayQuantity(line.systemQuantity)}</td>
        <td class="num">${formatDisplayQuantity(line.countedQuantity)}</td>
        <td class="num">${formatDisplayQuantity(line.differenceQuantity)}</td>
      </tr>`,
    )
    .join('');

  printHtmlDocument(
    t('print.documentTitle', { number: detail.adjustmentNumber }),
    `<h1>${t('print.heading')}</h1>
    <h2>${tenantName ?? ''}</h2>
    <div class="meta">
      <div class="meta-row">
        <span><strong>${t('print.sessionNumber')}</strong> ${detail.adjustmentNumber}</span>
        <span><strong>${t('print.date')}</strong> ${formatDisplayDate(detail.adjustmentDate)}</span>
      </div>
      <div class="meta-row">
        <span><strong>${t('print.warehouse')}</strong> ${detail.warehouseName}</span>
        <span><strong>${t('print.reason')}</strong> ${detail.reason ?? '—'}</span>
      </div>
    </div>
    <table>
      <thead>
        <tr>
          <th class="center">${t('print.columns.index')}</th>
          <th>${t('print.columns.productCode')}</th>
          <th>${t('print.columns.productName')}</th>
          <th>${t('print.columns.batchNumber')}</th>
          <th class="num">${t('print.columns.systemQty')}</th>
          <th class="num">${t('print.columns.countedQty')}</th>
          <th class="num">${t('print.columns.difference')}</th>
        </tr>
      </thead>
      <tbody>${rows}</tbody>
    </table>
    <div class="signatures">
      <div><p>${t('print.signatures.countLead')}</p><em>${t('print.signatures.signHint')}</em></div>
      <div><p>${t('print.signatures.storekeeper')}</p><em>${t('print.signatures.signHint')}</em></div>
      <div><p>${t('print.signatures.manager')}</p><em>${t('print.signatures.signHint')}</em></div>
    </div>
    <p class="note">${t('print.footnote')}</p>`,
  );
}
