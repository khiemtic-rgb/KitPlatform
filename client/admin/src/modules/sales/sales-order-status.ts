/** Trạng thái đơn bán — nguồn thống nhất cho list, filter, in. */
export const SALE_STATUS_LABELS: Record<number, string> = {
  1: 'Nháp',
  2: 'Hoàn tất',
  3: 'Đã hủy',
  4: 'Hoàn tiền',
};

export const SALE_STATUS_COLORS: Record<number, string> = {
  1: 'default',
  2: 'success',
  3: 'error',
  4: 'warning',
};

export const PARTIAL_RETURN_STATUS = 'partial_return' as const;

export const PARTIAL_RETURN_LABEL = 'Trả một phần';

export const SALE_STATUS_FILTER_OPTIONS: { value: number | typeof PARTIAL_RETURN_STATUS; label: string }[] =
  [
    { value: 1, label: SALE_STATUS_LABELS[1] },
    { value: 2, label: SALE_STATUS_LABELS[2] },
    { value: PARTIAL_RETURN_STATUS, label: PARTIAL_RETURN_LABEL },
    { value: 3, label: SALE_STATUS_LABELS[3] },
    { value: 4, label: SALE_STATUS_LABELS[4] },
  ];

export function isPartiallyReturnedOrder(status: number, totalRefunded?: number): boolean {
  return status === 2 && (totalRefunded ?? 0) > 0.0001;
}

export function isPartiallyReturnedFromItems(
  status: number,
  items: { returnedQuantity?: number }[],
): boolean {
  return status === 2 && items.some((line) => (line.returnedQuantity ?? 0) > 0.0001);
}

export function orderDisplayStatus(row: {
  status: number;
  totalRefunded?: number;
  items?: { returnedQuantity?: number }[];
}): { label: string; color: string } {
  const partial =
    isPartiallyReturnedOrder(row.status, row.totalRefunded) ||
    (row.items != null && isPartiallyReturnedFromItems(row.status, row.items));
  if (partial) {
    return { label: PARTIAL_RETURN_LABEL, color: 'orange' };
  }
  return {
    label: SALE_STATUS_LABELS[row.status] ?? String(row.status),
    color: SALE_STATUS_COLORS[row.status] ?? 'default',
  };
}

export function matchesSaleStatusFilter(
  row: { status: number; totalRefunded?: number; items?: { returnedQuantity?: number }[] },
  filter: number | typeof PARTIAL_RETURN_STATUS | undefined,
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
