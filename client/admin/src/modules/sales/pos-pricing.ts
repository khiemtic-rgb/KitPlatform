import type { TFunction } from 'i18next';
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
  const totalAmount = merchandiseNet - orderDiscountAmount;

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

/** Mirrors SalesPricing.ValidateDiscounts — returns error message or null. */
export function validateCartDiscountPolicy(
  cart: CartLine[],
  orderDiscount: OrderDiscountState,
  maxPercent: number,
  unlimited: boolean,
  t?: TFunction<'sales'>,
): string | null {
  if (unlimited) return null;

  for (const line of cart) {
    const gross = lineGross(line);
    const amount = computeDiscountAmount(gross, line.discountType, line.discountValue);
    if (amount > 0 && discountPercent(amount, gross) > maxPercent + 0.01) {
      return t
        ? t('pos.messages.lineDiscountOverMax', { max: maxPercent })
        : `Chiết khấu dòng vượt quá ${maxPercent}%`;
    }
  }

  const priced = priceCart(cart, orderDiscount);
  if (
    priced.orderDiscountAmount > 0 &&
    discountPercent(priced.orderDiscountAmount, priced.merchandiseNet) > maxPercent + 0.01
  ) {
    return t
      ? t('pos.messages.orderDiscountOverMax', { max: maxPercent })
      : `Chiết khấu đơn vượt quá ${maxPercent}%`;
  }

  return null;
}
