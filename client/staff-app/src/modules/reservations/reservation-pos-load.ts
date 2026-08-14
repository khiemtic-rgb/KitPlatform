import { lookupPosProduct } from '@/shared/api/sales.api';
import type { ReservationPosLoad } from '@/shared/api/reservations.api';
import type { CartLine } from '@/shared/api/sales.types';

/**
 * Nạp dòng giữ hàng vào giỏ POS bằng lookup mã SP đang bán.
 * Tránh product_unit_id / sản phẩm đã xóa trên đơn giữ cũ.
 */
export async function buildReservationCartLines(payload: ReservationPosLoad): Promise<CartLine[]> {
  const lines: CartLine[] = [];
  const problems: string[] = [];

  for (const item of payload.lines) {
    try {
      const lookup = await lookupPosProduct(item.productCode, payload.warehouseId);
      lines.push({
        key: lookup.productUnitId,
        productId: lookup.productId,
        productCode: lookup.productCode,
        productName: lookup.productName,
        productUnitId: lookup.productUnitId,
        unitName: lookup.unitName,
        quantity: item.quantity,
        unitPrice: lookup.unitPrice,
        catalogUnitPrice: lookup.unitPrice,
        stockAvailable: lookup.stockAvailable,
        batchHints: lookup.batchHints,
        dispensingClass: lookup.dispensingClass,
      });
    } catch {
      const label = item.productCode
        ? `${item.productCode} — ${item.productName}`
        : item.productName || 'Sản phẩm';
      problems.push(`${label} (đã ngưng bán hoặc không tìm thấy trên POS)`);
    }
  }

  if (problems.length > 0) {
    throw new Error(
      `Không nạp được giữ hàng: ${problems.join('; ')}. Thêm lại sản phẩm đang bán trên POS.`,
    );
  }

  return lines;
}
