import { lookupPosProduct } from '@/shared/api/sales.api';
import type { CartLine, SalesDiscountType, SalesOrderDetail } from '@/shared/api/sales.types';
import type { OrderDiscountState } from '@/modules/sales/pos-pricing';

export { buildDraftUpdatePayload } from '@/modules/sales/pos-sale-payload';

const POS_DRAFT_EDIT_KEY = 'kitplatform.staff.pos.editingDraftId';

export function persistPosDraftEdit(draftId: string) {
  sessionStorage.setItem(POS_DRAFT_EDIT_KEY, draftId);
}

export function readPosDraftEditId(): string | null {
  return sessionStorage.getItem(POS_DRAFT_EDIT_KEY);
}

export function clearPosDraftEdit() {
  sessionStorage.removeItem(POS_DRAFT_EDIT_KEY);
}

export function orderDiscountFromDetail(order: SalesOrderDetail): OrderDiscountState {
  if (!order.orderDiscountType) return {};
  return {
    discountType: order.orderDiscountType as SalesDiscountType,
    discountValue: order.orderDiscountValue ?? 0,
  };
}

export async function loadDraftCartLines(order: SalesOrderDetail): Promise<CartLine[]> {
  const lines: CartLine[] = [];
  for (const item of order.items) {
    if (!item.productId || !item.productUnitId) continue;
    let stockAvailable = 0;
    let batchHints: CartLine['batchHints'];
    try {
      const lookup = await lookupPosProduct(item.productCode, order.warehouseId ?? '');
      stockAvailable = lookup.stockAvailable;
      batchHints = lookup.batchHints;
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
      batchHints,
      batchLabel: item.batchNumber,
      discountType: item.discountType as SalesDiscountType | undefined,
      discountValue: item.discountValue,
    });
  }
  return lines;
}
