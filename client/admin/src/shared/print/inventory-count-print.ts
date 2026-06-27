import type { AdjustmentCountPreviewLine, AdjustmentDetail } from '@/shared/api/inventory.types';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayQuantity } from '@/shared/utils/money';
import { printHtmlDocument } from '@/shared/print/a4-print';

export function printInventoryCountSheet(
  detail: AdjustmentDetail,
  previewLines: AdjustmentCountPreviewLine[],
  tenantName?: string,
): void {
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
    `Kiểm kê ${detail.adjustmentNumber}`,
    `<h1>Biên bản kiểm kê kho</h1>
    <h2>${tenantName ?? ''}</h2>
    <div class="meta">
      <div class="meta-row">
        <span><strong>Số phiên:</strong> ${detail.adjustmentNumber}</span>
        <span><strong>Ngày:</strong> ${formatDisplayDate(detail.adjustmentDate)}</span>
      </div>
      <div class="meta-row">
        <span><strong>Kho:</strong> ${detail.warehouseName}</span>
        <span><strong>Lý do:</strong> ${detail.reason ?? '—'}</span>
      </div>
    </div>
    <table>
      <thead>
        <tr>
          <th class="center">STT</th>
          <th>Mã SP</th>
          <th>Tên SP</th>
          <th>Số lô</th>
          <th class="num">SL sổ</th>
          <th class="num">SL đếm</th>
          <th class="num">Chênh lệch</th>
        </tr>
      </thead>
      <tbody>${rows}</tbody>
    </table>
    <div class="signatures">
      <div><p>Trưởng nhóm kiểm kê</p><em>(Ký, họ tên)</em></div>
      <div><p>Thủ kho</p><em>(Ký, họ tên)</em></div>
      <div><p>Quản lý</p><em>(Ký, họ tên)</em></div>
    </div>
    <p class="note">In từ hệ thống PharmaCore — chỉ mang tính tham chiếu trước khi duyệt chốt.</p>`,
  );
}
