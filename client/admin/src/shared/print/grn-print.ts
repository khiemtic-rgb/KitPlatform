import type { GoodsReceiptDetail } from '@/shared/api/procurement.types';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney, formatDisplayQuantity } from '@/shared/utils/money';
import { printHtmlDocument } from '@/shared/print/a4-print';

export function printGoodsReceipt(grn: GoodsReceiptDetail, tenantName?: string): void {
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
    `GRN ${grn.grnNumber}`,
    `<h1>Phiếu nhập kho (GRN)</h1>
    <h2>${tenantName ?? ''}</h2>
    <div class="meta">
      <div class="meta-row">
        <span><strong>Số phiếu:</strong> ${grn.grnNumber}</span>
        <span><strong>Ngày nhập:</strong> ${formatDisplayDate(grn.receiptDate)}</span>
      </div>
      <div class="meta-row">
        <span><strong>NCC:</strong> ${grn.supplierName ?? '—'}</span>
        <span><strong>Kho:</strong> ${grn.warehouseName ?? '—'}</span>
      </div>
      <div class="meta-row">
        <span><strong>PO:</strong> ${grn.poNumber ?? '—'}</span>
        <span><strong>Ghi chú:</strong> ${grn.notes ?? '—'}</span>
      </div>
    </div>
    <table>
      <thead>
        <tr>
          <th class="center">STT</th>
          <th>Mã SP</th>
          <th>Tên SP</th>
          <th>Số lô</th>
          <th class="center">HSD</th>
          <th class="num">SL</th>
          <th class="num">Đơn giá</th>
          <th class="num">Thành tiền</th>
        </tr>
      </thead>
      <tbody>${rows}</tbody>
      <tfoot>
        <tr>
          <td colspan="7" class="num">Tổng cộng</td>
          <td class="num">${formatDisplayMoney(total)}</td>
        </tr>
      </tfoot>
    </table>
    <div class="signatures">
      <div><p>Người lập phiếu</p><em>(Ký, họ tên)</em></div>
      <div><p>Thủ kho</p><em>(Ký, họ tên)</em></div>
      <div><p>Kế toán</p><em>(Ký, họ tên)</em></div>
    </div>`,
  );
}
