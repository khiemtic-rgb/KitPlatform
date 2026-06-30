import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import {
  getSaleStatusFilterOptions,
  orderDisplayStatus as resolveOrderDisplayStatus,
  type SaleStatusFilterValue,
} from '@/modules/sales/sales-order-status';

export function useSaleStatusLabels() {
  const { t } = useTranslation('sales');

  return useMemo(
    () => ({
      saleStatusLabel: (status: number) =>
        t(`receipt.orderStatus.${status}`, { defaultValue: String(status) }),
      partialReturnLabel: () => t('receipt.orderStatus.partialReturn'),
      orderDisplayStatus: (row: Parameters<typeof resolveOrderDisplayStatus>[0]) =>
        resolveOrderDisplayStatus(row, t),
      saleStatusFilterOptions: getSaleStatusFilterOptions(t) as {
        value: SaleStatusFilterValue;
        label: string;
      }[],
    }),
    [t],
  );
}
