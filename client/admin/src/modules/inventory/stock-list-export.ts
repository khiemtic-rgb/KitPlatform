import * as XLSX from 'xlsx';
import type { StockBatch, StockProductSummary } from '@/shared/api/inventory.types';
import { formatDisplayDate } from '@/shared/utils/date';

export type StockExportLabels = {
  productCode: string;
  productName: string;
  unit: string;
  totalStock: string;
  warehouseCount: string;
  batchCount: string;
  lotDetail: string;
  actualQty: string;
  note: string;
};

export function formatLotDetail(batch: StockBatch): string {
  const made = batch.manufactureDate ? formatDisplayDate(batch.manufactureDate) : '—';
  const expiry = batch.expiryDate ? formatDisplayDate(batch.expiryDate) : '—';
  const qty = batch.quantityAvailable.toLocaleString('vi-VN');
  return `${batch.batchNumber} · NSX ${made} · HSD ${expiry} · SL ${qty} · ${batch.warehouseName}`;
}

export function buildStockExportSheet(
  products: StockProductSummary[],
  batches: StockBatch[],
  labels: StockExportLabels,
): XLSX.WorkSheet {
  const byProduct = new Map<string, StockBatch[]>();
  for (const batch of batches) {
    const list = byProduct.get(batch.productId) ?? [];
    list.push(batch);
    byProduct.set(batch.productId, list);
  }

  const header = [
    labels.productCode,
    labels.productName,
    labels.unit,
    labels.totalStock,
    labels.warehouseCount,
    labels.batchCount,
    labels.lotDetail,
    labels.actualQty,
    labels.note,
  ];
  const rows: (string | number)[][] = [];
  for (const product of products) {
    const lots = (byProduct.get(product.productId) ?? [])
      .slice()
      .sort((a, b) =>
        (a.expiryDate ?? '9999-99-99').localeCompare(b.expiryDate ?? '9999-99-99')
        || a.batchNumber.localeCompare(b.batchNumber),
      );
    const lotLines = lots.length > 0 ? lots : [null];
    lotLines.forEach((lot, index) => {
      const first = index === 0;
      rows.push([
        product.productCode,
        product.productName,
        product.saleUnitName ?? '',
        first ? product.totalQuantity : '',
        first ? product.warehouseCount : '',
        first ? product.batchCount : '',
        lot ? formatLotDetail(lot) : '',
        '',
        '',
      ]);
    });
  }

  const sheet = XLSX.utils.aoa_to_sheet([header, ...rows]);
  sheet['!cols'] = [
    { wch: 14 },
    { wch: 36 },
    { wch: 10 },
    { wch: 12 },
    { wch: 10 },
    { wch: 10 },
    { wch: 72 },
    { wch: 18 },
    { wch: 24 },
  ];
  return sheet;
}

export function downloadStockWorkbook(sheet: XLSX.WorkSheet, filename: string) {
  const book = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(book, sheet, 'Ton kho');
  XLSX.writeFile(book, filename);
}
