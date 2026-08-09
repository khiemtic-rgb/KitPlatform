import { lookupPosProduct } from '@/shared/api/sales.api';
import type { CustomerDraftOrderPosLoad } from '@/shared/api/customer-draft-orders.api';
import type { CartLine } from '@/shared/api/sales.types';
import type { OrderDiscountState } from '@/modules/sales/pos-pricing';
import type { SalesDiscountType } from '@/shared/api/sales.types';

export async function loadCustomerDraftCartLines(
  payload: CustomerDraftOrderPosLoad,
): Promise<CartLine[]> {
  const lines: CartLine[] = [];
  for (const item of payload.lines) {
    let stockAvailable = 0;
    try {
      const lookup = await lookupPosProduct(item.productCode, payload.warehouseId);
      stockAvailable = lookup.stockAvailable;
    } catch {
      stockAvailable = 0;
    }
    lines.push({
      key: item.productUnitId,
      productId: item.productId,
      productCode: item.productCode,
      productName: item.productName,
      productUnitId: item.productUnitId,
      unitName: item.unitName,
      quantity: item.quantity,
      unitPrice: item.unitPrice,
      catalogUnitPrice: item.unitPrice,
      stockAvailable,
      discountType: item.discountType as SalesDiscountType | undefined,
      discountValue: item.discountValue,
    });
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
