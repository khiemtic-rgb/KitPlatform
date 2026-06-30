import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';

const WAREHOUSE_TYPE_IDS = [1, 2, 3, 4, 5] as const;
const TRANSFER_STATUS_IDS = [1, 2, 3, 4] as const;
const ADJUSTMENT_STATUS_IDS = [1, 2, 3, 4] as const;
const STATUS_IDS = [1, 2] as const;

export function useInventoryEnums() {
  const { t } = useTranslation('inventory');

  const warehouseTypeLabel = (type: number) =>
    t(`enums.warehouseType.${type}`, { defaultValue: String(type) });

  const transferStatusLabel = (status: number) =>
    t(`enums.transferStatus.${status}`, { defaultValue: String(status) });

  const adjustmentStatusLabel = (status: number) =>
    t(`enums.adjustmentStatus.${status}`, { defaultValue: String(status) });

  const statusLabel = (status: number) =>
    t(`enums.status.${status}`, { defaultValue: String(status) });

  const warehouseTypeOptions = useMemo(
    () => WAREHOUSE_TYPE_IDS.map((value) => ({ value, label: warehouseTypeLabel(value) })),
    [t],
  );

  const transferStatusOptions = useMemo(
    () => TRANSFER_STATUS_IDS.map((value) => ({ value, label: transferStatusLabel(value) })),
    [t],
  );

  const adjustmentStatusOptions = useMemo(
    () => ADJUSTMENT_STATUS_IDS.map((value) => ({ value, label: adjustmentStatusLabel(value) })),
    [t],
  );

  const statusOptions = useMemo(
    () => STATUS_IDS.map((value) => ({ value, label: statusLabel(value) })),
    [t],
  );

  return {
    warehouseTypeLabel,
    warehouseTypeOptions,
    transferStatusLabel,
    transferStatusOptions,
    adjustmentStatusLabel,
    adjustmentStatusOptions,
    statusLabel,
    statusOptions,
  };
}
