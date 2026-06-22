import type { CartLine } from '@/shared/api/sales.types';
import { SALES_DISCOUNT_TYPES, type SalesDiscountType } from '@/shared/api/sales.types';

export type OrderDiscountState = {
  discountType?: SalesDiscountType;
  discountValue?: number;
};

export function computeDiscountAmount(
  basis: number,
  discountType?: SalesDiscountType,
  discountValue?: number,
): number {
  if (basis <= 0 || !discountType || !discountValue || discountValue <= 0) return 0;
  if (discountType === SALES_DISCOUNT_TYPES.Percent) {
    return Math.round((basis * Math.min(discountValue, 100)) / 100);
  }
  return Math.min(discountValue, basis);
}

export function lineGross(line: CartLine): number {
  return line.quantity * line.unitPrice;
}

export function lineNet(line: CartLine): number {
  return lineGross(line) - computeDiscountAmount(lineGross(line), line.discountType, line.discountValue);
}

export function priceCart(cart: CartLine[], orderDiscount: OrderDiscountState) {
  const subtotalGross = cart.reduce((sum, line) => sum + lineGross(line), 0);
  const lineDiscountTotal = cart.reduce(
    (sum, line) => sum + computeDiscountAmount(lineGross(line), line.discountType, line.discountValue),
    0,
  );
  const merchandiseNet = subtotalGross - lineDiscountTotal;
  const orderDiscountAmount = computeDiscountAmount(
    merchandiseNet,
    orderDiscount.discountType,
    orderDiscount.discountValue,
  );
  const totalAmount = Math.round(merchandiseNet - orderDiscountAmount);

  return {
    subtotalGross,
    lineDiscountTotal,
    merchandiseNet,
    orderDiscountAmount,
    totalDiscountAmount: lineDiscountTotal + orderDiscountAmount,
    totalAmount,
  };
}

export function discountPercent(amount: number, basis: number): number {
  if (basis <= 0) return 0;
  return (amount / basis) * 100;
}
