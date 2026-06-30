import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import type { TenantBatchModeValue } from '@/shared/api/sales.api';

const BATCH_MODE_VALUES: TenantBatchModeValue[] = [
  'off',
  'suggest',
  'label_optional',
  'label_required',
];

export function useBatchModeLabels() {
  const { t } = useTranslation('sales', { keyPrefix: 'batchMode' });

  const batchModeOptions = useMemo(
    () =>
      BATCH_MODE_VALUES.map((value) => ({
        value,
        label: t(`options.${value}`),
      })),
    [t],
  );

  const batchModeHint = (mode: TenantBatchModeValue) => t(`hints.${mode}`);

  return { batchModeOptions, batchModeHint };
}
