import { lookupPosProduct } from '@/shared/api/sales.api';
import type { ProductListItem } from '@/shared/api/catalog.types';
import type { CartLine, SalesOrderDetail, SalesDiscountType } from '@/shared/api/sales.types';
import type { OrderDiscountState } from '@/modules/sales/pos-pricing';

export { buildDraftUpdatePayload } from '@/modules/sales/pos-sale-payload';

const POS_DRAFT_EDIT_KEY = 'pharmacore.pos.editingDraftId';

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

export async function loadDraftCartLines(
  order: SalesOrderDetail,
  products: ProductListItem[],
): Promise<CartLine[]> {
  const lines: CartLine[] = [];
  for (const item of order.items) {
    const product = products.find((p) => p.id === item.productId);
    let stockAvailable = 0;
    const lookupKey = product?.primaryBarcode ?? item.productCode;
    try {
      const lookup = await lookupPosProduct(lookupKey, order.warehouseId);
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
      stockAvailable,
      discountType: item.discountType as SalesDiscountType | undefined,
      discountValue: item.discountValue,
    });
  }
  return lines;
}
