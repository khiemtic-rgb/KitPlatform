import { lookupPosProduct } from '@/shared/api/sales.api';
import type { CustomerDraftOrderPosLoad } from '@/shared/api/customer-draft-orders.api';
import type { CartLine, SalesDiscountType } from '@/shared/api/sales.types';
import type { OrderDiscountState } from '@/modules/sales/pos-pricing';

/**
 * Nạp đơn nháp app vào POS — luôn lấy productUnitId/tồn từ lookup đang bán.
 * Giá/CK giữ theo đơn nháp nếu có.
 */
export async function loadCustomerDraftCartLines(
  payload: CustomerDraftOrderPosLoad,
): Promise<CartLine[]> {
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
        unitPrice: item.unitPrice > 0 ? item.unitPrice : lookup.unitPrice,
        catalogUnitPrice: lookup.unitPrice,
        stockAvailable: lookup.stockAvailable,
        batchHints: lookup.batchHints,
        dispensingClass: lookup.dispensingClass,
        discountType: item.discountType as SalesDiscountType | undefined,
        discountValue: item.discountValue,
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
      `Không nạp được đơn nháp: ${problems.join('; ')}. Thêm lại sản phẩm đang bán trên POS.`,
    );
  }

  return lines;
}

export function orderDiscountFromCustomerDraft(payload: CustomerDraftOrderPosLoad): OrderDiscountState {
  if (!payload.orderDiscountType) return {};
  return {
    discountType: payload.orderDiscountType as SalesDiscountType,
    discountValue: payload.orderDiscountValue ?? 0,
  };
}

function isActionableCustomerDraftStatus(status: number): boolean {
  return status === 2 || status === 3;
}

export { isActionableCustomerDraftStatus };
