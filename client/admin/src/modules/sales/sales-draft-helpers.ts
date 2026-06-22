import { lookupPosProduct } from '@/shared/api/sales.api';
import type { ProductListItem } from '@/shared/api/catalog.types';
import type { CartLine, SalesOrderDetail, SalesDiscountType } from '@/shared/api/sales.types';
import type { OrderDiscountState } from '@/modules/sales/pos-pricing';

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
    let stockAvailable = item.quantity;
    if (product?.primaryBarcode) {
      try {
        const lookup = await lookupPosProduct(product.primaryBarcode, order.warehouseId);
        stockAvailable = lookup.stockAvailable;
      } catch {
        stockAvailable = item.quantity;
      }
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

export function buildDraftUpdatePayload(
  customerId: string | undefined,
  cart: CartLine[],
  orderDiscount: OrderDiscountState,
  notes?: string,
) {
  return {
    customerId: customerId ?? null,
    priceType: 1,
    orderDiscountType: orderDiscount.discountType ?? null,
    orderDiscountValue: orderDiscount.discountType ? (orderDiscount.discountValue ?? 0) : null,
    notes: notes ?? null,
    items: cart.map((line) => ({
      productId: line.productId,
      productUnitId: line.productUnitId,
      quantity: line.quantity,
      ...(line.discountType
        ? { discountType: line.discountType, discountValue: line.discountValue ?? 0 }
        : {}),
    })),
  };
}
