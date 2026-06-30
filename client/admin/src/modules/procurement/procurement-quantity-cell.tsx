import type { ReactNode } from 'react';
import { procurementT } from '@/shared/i18n';
import { formatDisplayQuantity, tableQuantityCellClassName } from '@/shared/utils/money';

export function ProcurementQuantityCell({ value }: { value?: number | null }) {
  return <span className={tableQuantityCellClassName}>{formatDisplayQuantity(value)}</span>;
}

export function procurementQuantityColumn<T extends string>(
  title: string,
  dataIndex: T,
  width: number,
) {
  return {
    title,
    dataIndex,
    width,
    align: 'right' as const,
    render: (v: number) => <ProcurementQuantityCell value={v} />,
  };
}

export function procurementRemainingQtyColumn(width = 85) {
  return {
    title: procurementT()('shared.columns.remaining'),
    width,
    align: 'right' as const,
    render: (_: unknown, row: { orderedQty: number; receivedQty: number }): ReactNode => (
      <ProcurementQuantityCell value={row.orderedQty - row.receivedQty} />
    ),
  };
}
