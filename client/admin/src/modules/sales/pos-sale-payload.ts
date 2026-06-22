import type { CartLine, PosCheckoutPaymentLine } from '@/shared/api/sales.types';
import type { OrderDiscountState } from '@/modules/sales/pos-pricing';

export function buildSaleLineItems(cart: CartLine[]) {
  return cart.map((line) => ({
    productId: line.productId,
    productUnitId: line.productUnitId,
    quantity: line.quantity,
    ...(line.discountType
      ? { discountType: line.discountType, discountValue: line.discountValue ?? 0 }
      : {}),
  }));
}

export function buildCreateSalePayload(
  warehouseId: string,
  customerId: string | undefined,
  cart: CartLine[],
  orderDiscount: OrderDiscountState,
  saveAsDraft: boolean,
  payments?: PosCheckoutPaymentLine[],
) {
  return {
    warehouseId,
    customerId,
    saveAsDraft,
    orderDiscountType: orderDiscount.discountType,
    orderDiscountValue: orderDiscount.discountValue,
    payments: payments?.map((p) => ({ paymentMethod: p.paymentMethod, amount: p.amount })),
    items: buildSaleLineItems(cart),
  };
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
    items: buildSaleLineItems(cart),
  };
}
