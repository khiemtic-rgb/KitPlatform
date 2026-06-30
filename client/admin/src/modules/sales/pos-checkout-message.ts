import type { SalesOrderDetail } from '@/shared/api/sales.types';
import { formatDisplayMoney } from '@/shared/utils/money';
import { resolveOrderPaymentSummary } from '@/modules/sales/sales-order-payment-summary';
import { salesT } from '@/shared/i18n';

export function formatPosCheckoutSuccessMessage(order: SalesOrderDetail): string {
  const t = salesT();
  const base = t('pos.checkoutSuccess.base', {
    orderNumber: order.orderNumber,
    amount: formatDisplayMoney(order.totalAmount),
  });
  const parts: string[] = [];
  const { amountPaid, outstanding, hasOutstanding } = resolveOrderPaymentSummary(order);

  if (hasOutstanding) {
    parts.push(
      t('pos.checkoutSuccess.paidOutstanding', {
        paid: formatDisplayMoney(amountPaid),
        outstanding: formatDisplayMoney(outstanding),
      }),
    );
  }

  const redeemed = order.loyaltyPointsRedeemed ?? 0;
  const earned = order.loyaltyPointsEarned ?? 0;
  if (redeemed > 0) {
    parts.push(t('pos.checkoutSuccess.pointsRedeemed', { points: redeemed.toLocaleString() }));
  }
  if (earned > 0) {
    parts.push(t('pos.checkoutSuccess.pointsEarned', { points: earned.toLocaleString() }));
  }
  if (parts.length === 0) return base;
  return `${base} · ${parts.join(' · ')}`;
}
