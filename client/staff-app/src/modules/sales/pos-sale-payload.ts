import type { CartLine, PosCheckoutPaymentLine } from '@/shared/api/sales.types';
import type { OrderDiscountState } from '@/modules/sales/pos-pricing';
import { defaultBatchLabel } from '@/modules/sales/pos-batch';

function resolveLineBatchNumber(line: CartLine): string | undefined {
  const manual = line.batchLabel?.trim();
  if (manual) return manual;
  return defaultBatchLabel(line.batchHints);
}

export function buildSaleLineItems(cart: CartLine[]) {
  return cart.map((line) => {
    const batchNumber = resolveLineBatchNumber(line);
    const catalog = line.catalogUnitPrice ?? line.unitPrice;
    const priceOverride = Math.abs(line.unitPrice - catalog) > 0.009;
    return {
      productId: line.productId,
      productUnitId: line.productUnitId,
      quantity: line.quantity,
      ...(batchNumber ? { batchNumber } : {}),
      ...(priceOverride
        ? { unitPrice: line.unitPrice }
        : line.discountType
          ? { discountType: line.discountType, discountValue: line.discountValue ?? 0 }
          : {}),
      ...(line.prescriptionLineId ? { prescriptionLineId: line.prescriptionLineId } : {}),
    };
  });
}

export function buildCreateSalePayload(
  warehouseId: string,
  customerId: string | undefined,
  cart: CartLine[],
  orderDiscount: OrderDiscountState,
  saveAsDraft: boolean,
  payments?: PosCheckoutPaymentLine[],
  loyaltyDiscountAmount?: number,
  customerVoucherId?: string,
  prescriptionId?: string,
  orderReminder?: { label?: string; daysSupply?: number },
) {
  return {
    warehouseId,
    customerId,
    saveAsDraft,
    orderDiscountType: orderDiscount.discountType,
    orderDiscountValue: orderDiscount.discountValue,
    payments: payments?.map((p) => ({ paymentMethod: p.paymentMethod, amount: p.amount })),
    ...(loyaltyDiscountAmount != null && loyaltyDiscountAmount > 0 ? { loyaltyDiscountAmount } : {}),
    ...(customerVoucherId ? { customerVoucherId } : {}),
    ...(prescriptionId ? { prescriptionId } : {}),
    ...(orderReminder?.daysSupply != null && orderReminder.daysSupply >= 1
      ? {
          orderReminderLabel: orderReminder.label ?? null,
          orderReminderDaysSupply: orderReminder.daysSupply,
        }
      : {}),
    items: buildSaleLineItems(cart),
  };
}

export function buildDraftCompletePayload(
  customerId: string | undefined,
  cart: CartLine[],
  orderDiscount: OrderDiscountState,
  loyaltyDiscountAmount?: number,
  customerVoucherId?: string,
  prescriptionId?: string,
  orderReminder?: { label?: string; daysSupply?: number },
) {
  return {
    customerId: customerId ?? null,
    orderDiscountType: orderDiscount.discountType ?? null,
    orderDiscountValue: orderDiscount.discountType ? (orderDiscount.discountValue ?? 0) : null,
    ...(loyaltyDiscountAmount != null && loyaltyDiscountAmount > 0 ? { loyaltyDiscountAmount } : {}),
    ...(customerVoucherId ? { customerVoucherId } : {}),
    ...(prescriptionId ? { prescriptionId } : {}),
    ...(orderReminder?.daysSupply != null && orderReminder.daysSupply >= 1
      ? {
          orderReminderLabel: orderReminder.label ?? null,
          orderReminderDaysSupply: orderReminder.daysSupply,
        }
      : {}),
    items: buildSaleLineItems(cart),
  };
}

export function buildDraftUpdatePayload(
  customerId: string | undefined,
  cart: CartLine[],
  orderDiscount: OrderDiscountState,
) {
  return {
    customerId: customerId ?? null,
    orderDiscountType: orderDiscount.discountType ?? null,
    orderDiscountValue: orderDiscount.discountType ? (orderDiscount.discountValue ?? 0) : null,
    items: buildSaleLineItems(cart),
  };
}
