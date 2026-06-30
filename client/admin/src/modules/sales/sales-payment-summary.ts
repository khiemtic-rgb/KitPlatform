import type { SalesOrderDetail, SalesPaymentLine } from '@/shared/api/sales.types';
import { salesT } from '@/shared/i18n';
import { formatDisplayMoney } from '@/shared/utils/money';
import { computeOrderTotalRefunded } from '@/modules/sales/sales-return-pricing';

export type NetPaymentLine = {
  paymentMethod: number;
  collected: number;
  refunded: number;
  net: number;
};

function aggregatePaymentsByMethod(payments: SalesPaymentLine[] | undefined): Map<number, number> {
  const map = new Map<number, number>();
  for (const p of payments ?? []) {
    map.set(p.paymentMethod, (map.get(p.paymentMethod) ?? 0) + p.amount);
  }
  return map;
}

/** Phân bổ hoàn tiền theo tỷ lệ thu ban đầu khi API chưa trả refundPayments (đơn cũ). */
export function inferRefundPaymentsByMethod(
  payments: SalesPaymentLine[] | undefined,
  totalRefund: number,
): SalesPaymentLine[] {
  if (totalRefund <= 0.009) return [];
  const collectedByMethod = aggregatePaymentsByMethod(payments);
  const totalCollected = [...collectedByMethod.values()].reduce((sum, amount) => sum + amount, 0);
  if (totalCollected <= 0) return [];

  const methods = [...collectedByMethod.keys()].sort((a, b) => a - b);
  let remaining = totalRefund;
  const result: SalesPaymentLine[] = [];

  for (let i = 0; i < methods.length; i++) {
    const method = methods[i];
    const collected = collectedByMethod.get(method)!;
    const share =
      i === methods.length - 1 ? remaining : Math.round((totalRefund * collected) / totalCollected);
    remaining -= share;
    if (share > 0.009) {
      result.push({ paymentMethod: method, amount: share });
    }
  }

  return result;
}

export function resolveRefundPaymentsForOrder(
  order: Pick<
    SalesOrderDetail,
    'payments' | 'refundPayments' | 'totalRefunded' | 'items' | 'discountAmount'
  >,
): { payments: SalesPaymentLine[]; inferred: boolean } {
  if ((order.refundPayments?.length ?? 0) > 0) {
    return { payments: order.refundPayments!, inferred: false };
  }

  const totalRefunded =
    order.totalRefunded && order.totalRefunded > 0
      ? order.totalRefunded
      : computeOrderTotalRefunded(order);

  if (totalRefunded <= 0.009) {
    return { payments: [], inferred: false };
  }

  return {
    payments: inferRefundPaymentsByMethod(order.payments, totalRefunded),
    inferred: true,
  };
}

export function buildNetPaymentLines(
  payments: SalesPaymentLine[] | undefined,
  refundPayments: SalesPaymentLine[] | undefined,
): NetPaymentLine[] {
  const collectedMap = aggregatePaymentsByMethod(payments);
  const refundMap = aggregatePaymentsByMethod(refundPayments);

  const methods = new Set([...collectedMap.keys(), ...refundMap.keys()]);
  return [...methods]
    .sort((a, b) => a - b)
    .map((paymentMethod) => {
      const collected = collectedMap.get(paymentMethod) ?? 0;
      const refunded = refundMap.get(paymentMethod) ?? 0;
      return { paymentMethod, collected, refunded, net: collected - refunded };
    });
}

export function buildOrderNetPaymentLines(order: SalesOrderDetail): {
  lines: NetPaymentLine[];
  refundInferred: boolean;
} {
  const { payments: refundPayments, inferred } = resolveRefundPaymentsForOrder(order);
  return {
    lines: buildNetPaymentLines(order.payments, refundPayments),
    refundInferred: inferred,
  };
}

export function formatNetPaymentLine(line: NetPaymentLine, afterReturn = false): string {
  const t = salesT();
  const label = t(`enums.paymentMethod.${line.paymentMethod}`, {
    defaultValue: String(line.paymentMethod),
  });
  const unchanged = t('paymentSummary.unchanged');
  if (!afterReturn && line.refunded <= 0.009) {
    return t('paymentSummary.lineSimple', {
      method: label,
      amount: formatDisplayMoney(line.collected),
    });
  }
  if (line.refunded <= 0.009) {
    return t('paymentSummary.lineUnchanged', {
      method: label,
      amount: formatDisplayMoney(line.net),
      unchanged,
    });
  }
  return t('paymentSummary.lineDetailed', {
    method: label,
    collected: formatDisplayMoney(line.collected),
    refunded: formatDisplayMoney(line.refunded),
    net: formatDisplayMoney(line.net),
  });
}

export function formatNetPaymentLinesHtml(lines: NetPaymentLine[], afterReturn = false): string {
  return lines.map((line) => `<div>${formatNetPaymentLine(line, afterReturn)}</div>`).join('');
}

export function formatNetPaymentTotal(lines: NetPaymentLine[]): string {
  const net = lines.reduce((sum, line) => sum + line.net, 0);
  return formatDisplayMoney(net);
}
