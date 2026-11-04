import type { SalesOrderDetail } from '@/shared/api/sales.types';

type OrderLevelDiscounts = {
  discountAmount: number;
  voucherDiscountAmount?: number;
  loyaltyDiscountAmount?: number;
};

/** CK đơn + voucher + điểm — cùng phân bổ theo dòng khi trả hàng (khớp backend). */
export function orderLevelDiscount(order: OrderLevelDiscounts): number {
  return order.discountAmount + (order.voucherDiscountAmount ?? 0) + (order.loyaltyDiscountAmount ?? 0);
}

export function computeLineRefundAmount(
  lineTotal: number,
  soldQuantity: number,
  returnQuantity: number,
  merchandiseNet: number,
  orderDiscountAmount: number,
): number {
  if (soldQuantity <= 0 || returnQuantity <= 0) return 0;
  const lineOrderDiscountShare =
    merchandiseNet > 0 ? (lineTotal / merchandiseNet) * orderDiscountAmount : 0;
  const refundableLineNet = lineTotal - lineOrderDiscountShare;
  return Math.round((refundableLineNet * returnQuantity) / soldQuantity);
}

export function previewReturnRefund(
  order: SalesOrderDetail,
  quantities: Record<string, number>,
): { totalRefund: number; lines: { itemId: string; quantity: number; refundAmount: number }[] } {
  const merchandiseNet = order.items.reduce((sum, line) => sum + line.lineTotal, 0);
  const orderDiscountAmount = orderLevelDiscount(order);

  const lines = order.items
    .map((line) => {
      const quantity = Number(quantities[line.id] ?? 0);
      if (quantity <= 0) return null;
      const refundAmount = computeLineRefundAmount(
        line.lineTotal,
        line.quantity,
        quantity,
        merchandiseNet,
        orderDiscountAmount,
      );
      return { itemId: line.id, quantity, refundAmount };
    })
    .filter((row): row is { itemId: string; quantity: number; refundAmount: number } => row !== null);

  const totalRefund = lines.reduce((sum, row) => sum + row.refundAmount, 0);
  return { totalRefund, lines };
}

export function computeOrderTotalRefunded(order: OrderLevelDiscounts & {
  items: Pick<SalesOrderDetail['items'][number], 'lineTotal' | 'quantity' | 'returnedQuantity'>[];
}): number {
  const merchandiseNet = order.items.reduce((sum, line) => sum + line.lineTotal, 0);
  return order.items.reduce((sum, line) => {
    const returnedQty = line.returnedQuantity ?? 0;
    if (returnedQty <= 0) return sum;
    return (
      sum +
      computeLineRefundAmount(
        line.lineTotal,
        line.quantity,
        returnedQty,
        merchandiseNet,
        orderLevelDiscount(order),
      )
    );
  }, 0);
}

export function lineRefundAmount(
  line: Pick<SalesOrderDetail['items'][number], 'lineTotal' | 'quantity' | 'returnedQuantity'>,
  order: OrderLevelDiscounts & { items: Pick<SalesOrderDetail['items'][number], 'lineTotal'>[] },
): number {
  const returnedQty = line.returnedQuantity ?? 0;
  if (returnedQty <= 0) return 0;
  const merchandiseNet = order.items.reduce((sum, item) => sum + item.lineTotal, 0);
  return computeLineRefundAmount(
    line.lineTotal,
    line.quantity,
    returnedQty,
    merchandiseNet,
    orderLevelDiscount(order),
  );
}

export function remainingLineNet(
  line: Pick<SalesOrderDetail['items'][number], 'lineTotal' | 'quantity' | 'returnedQuantity'>,
  order: OrderLevelDiscounts & { items: Pick<SalesOrderDetail['items'][number], 'lineTotal'>[] },
): number {
  return line.lineTotal - lineRefundAmount(line, order);
}

export {
  isPartiallyReturnedFromItems,
  isPartiallyReturnedOrder,
  orderDisplayStatus,
} from '@/modules/sales/sales-order-status';
