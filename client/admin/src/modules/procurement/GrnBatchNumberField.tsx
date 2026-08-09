import { useEffect, useMemo, useState, type CSSProperties } from 'react';
import { useTranslation } from 'react-i18next';
import { AutoComplete } from 'antd';
import { fetchStockBatches } from '@/shared/api/inventory.api';
import type { StockBatch } from '@/shared/api/inventory.types';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayQuantity } from '@/shared/utils/money';

export type GrnExistingBatchPick = {
  batchNumber: string;
  expiryDate?: string;
};

type Props = {
  value?: string;
  onChange?: (value: string) => void;
  warehouseId?: string;
  productId?: string;
  /** Khi chọn lô đã có trong kho — caller điền HSD (và các field liên quan). */
  onPickExisting?: (batch: GrnExistingBatchPick) => void;
  placeholder?: string;
  style?: CSSProperties;
  disabled?: boolean;
};

function toExpiryFieldValue(iso?: string): string | undefined {
  if (!iso) return undefined;
  return iso.length >= 10 ? iso.slice(0, 10) : iso;
}

/** AutoComplete số lô: gợi ý lô đã có (SP + kho nhận), vẫn cho nhập lô mới. */
export function GrnBatchNumberField({
  value,
  onChange,
  warehouseId,
  productId,
  onPickExisting,
  placeholder,
  style,
  disabled,
}: Props) {
  const { t } = useTranslation('procurement', { keyPrefix: 'shared' });
  const [batches, setBatches] = useState<StockBatch[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!warehouseId || !productId) {
      setBatches([]);
      return;
    }
    let cancelled = false;
    setLoading(true);
    void fetchStockBatches({ warehouseId, productId, page: 1, pageSize: 50 })
      .then((page) => {
        if (!cancelled) setBatches(page.items);
      })
      .catch(() => {
        if (!cancelled) setBatches([]);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [warehouseId, productId]);

  const options = useMemo(
    () =>
      batches.map((b) => {
        const expiry = b.expiryDate ? formatDisplayDate(b.expiryDate) : t('emDash');
        const qty = formatDisplayQuantity(b.quantityAvailable);
        return {
          value: b.batchNumber,
          label: `${b.batchNumber} · HSD ${expiry} · ${t('columns.stockQtyShort', { qty })}`,
          batch: b,
        };
      }),
    [batches, t],
  );

  return (
    <AutoComplete
      value={value}
      options={options}
      disabled={disabled}
      placeholder={placeholder ?? t('columns.batchShort')}
      style={style ?? { width: '100%' }}
      notFoundContent={
        !warehouseId || !productId
          ? t('columns.batchPickNeedProductWarehouse')
          : loading
            ? t('columns.batchPickLoading')
            : t('columns.batchPickTypeNew')
      }
      filterOption={(input, option) =>
        String(option?.value ?? '')
          .toLowerCase()
          .includes(input.trim().toLowerCase())
      }
      onSearch={(text) => onChange?.(text)}
      onChange={(text) => onChange?.(text)}
      onSelect={(selected) => {
        const match = batches.find((b) => b.batchNumber === selected);
        onChange?.(selected);
        if (match) {
          onPickExisting?.({
            batchNumber: match.batchNumber,
            expiryDate: toExpiryFieldValue(match.expiryDate),
          });
        }
      }}
      allowClear
    />
  );
}
