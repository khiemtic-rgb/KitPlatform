import type { TFunction } from 'i18next';
import { salesT } from '@/shared/i18n';

/** Trạng thái đơn bán — nguồn thống nhất cho list, filter, in. */
export const SALE_STATUS_COLORS: Record<number, string> = {
  1: 'default',
  2: 'success',
  3: 'error',
  4: 'warning',
};

export const PARTIAL_RETURN_STATUS = 'partial_return' as const;

export type SaleStatusFilterValue = number | typeof PARTIAL_RETURN_STATUS;

export function getSaleStatusLabel(status: number, t: TFunction<'sales'> = salesT()): string {
  return t(`receipt.orderStatus.${status}`, { defaultValue: String(status) });
}

export function getPartialReturnLabel(t: TFunction<'sales'> = salesT()): string {
  return t('receipt.orderStatus.partialReturn');
}

export function getSaleStatusFilterOptions(
  t: TFunction<'sales'> = salesT(),
): { value: SaleStatusFilterValue; label: string }[] {
  return [
    { value: 1, label: getSaleStatusLabel(1, t) },
    { value: 2, label: getSaleStatusLabel(2, t) },
    { value: PARTIAL_RETURN_STATUS, label: getPartialReturnLabel(t) },
    { value: 3, label: getSaleStatusLabel(3, t) },
    { value: 4, label: getSaleStatusLabel(4, t) },
  ];
}

export function isPartiallyReturnedOrder(status: number, totalRefunded?: number): boolean {
  return status === 2 && (totalRefunded ?? 0) > 0.0001;
}

export function isPartiallyReturnedFromItems(
  status: number,
  items: { returnedQuantity?: number }[],
): boolean {
  return status === 2 && items.some((line) => (line.returnedQuantity ?? 0) > 0.0001);
}

export function orderDisplayStatus(
  row: {
    status: number;
    totalRefunded?: number;
    items?: { returnedQuantity?: number }[];
  },
  t: TFunction<'sales'> = salesT(),
): { label: string; color: string } {
  const partial =
    isPartiallyReturnedOrder(row.status, row.totalRefunded) ||
    (row.items != null && isPartiallyReturnedFromItems(row.status, row.items));
  if (partial) {
    return { label: getPartialReturnLabel(t), color: 'orange' };
  }
  return {
    label: getSaleStatusLabel(row.status, t),
    color: SALE_STATUS_COLORS[row.status] ?? 'default',
  };
}

export function matchesSaleStatusFilter(
  row: { status: number; totalRefunded?: number; items?: { returnedQuantity?: number }[] },
  filter: SaleStatusFilterValue | undefined,
): boolean {
  if (filter == null) return true;
  if (filter === PARTIAL_RETURN_STATUS) {
    return (
      isPartiallyReturnedOrder(row.status, row.totalRefunded) ||
      (row.items != null && isPartiallyReturnedFromItems(row.status, row.items))
    );
  }
  if (filter === 2) {
    const partial =
      isPartiallyReturnedOrder(row.status, row.totalRefunded) ||
      (row.items != null && isPartiallyReturnedFromItems(row.status, row.items));
    return row.status === 2 && !partial;
  }
  return row.status === filter;
}
