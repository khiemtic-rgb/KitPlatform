import { useEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import { useTranslation } from 'react-i18next';
import { AutoComplete } from 'antd';
import { fetchLotIdentity, fetchStockBatches } from '@/shared/api/inventory.api';
import type { StockBatch } from '@/shared/api/inventory.types';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayQuantity } from '@/shared/utils/money';

export type GrnExistingBatchPick = {
  batchNumber: string;
  manufactureDate?: string;
  expiryDate?: string;
  exists: boolean;
  hasConflict: boolean;
};

type Props = {
  value?: string;
  onChange?: (value: string) => void;
  warehouseId?: string;
  productId?: string;
  /** Tra cứu lô theo tenant (không chỉ kho đang nhập). */
  onPickExisting?: (batch: GrnExistingBatchPick) => void;
  placeholder?: string;
  style?: CSSProperties;
  disabled?: boolean;
  status?: '' | 'warning' | 'error';
};

function toDateField(iso?: string): string | undefined {
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
  status,
}: Props) {
  const { t } = useTranslation('procurement', { keyPrefix: 'shared' });
  const [batches, setBatches] = useState<StockBatch[]>([]);
  const [loading, setLoading] = useState(false);
  const onPickRef = useRef(onPickExisting);
  onPickRef.current = onPickExisting;

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

  useEffect(() => {
    const lot = value?.trim() ?? '';
    if (!productId || lot.length === 0) {
      onPickRef.current?.({ batchNumber: lot, exists: false, hasConflict: false });
      return;
    }
    let cancelled = false;
    const timer = window.setTimeout(() => {
      void fetchLotIdentity(productId, lot)
        .then((identity) => {
          if (cancelled) return;
          onPickRef.current?.({
            batchNumber: identity.batchNumber || lot,
            manufactureDate: toDateField(identity.manufactureDate),
            expiryDate: toDateField(identity.expiryDate),
            exists: identity.exists,
            hasConflict: identity.hasConflict,
          });
        })
        .catch(() => {
          if (!cancelled) {
            onPickRef.current?.({ batchNumber: lot, exists: false, hasConflict: false });
          }
        });
    }, 280);
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [productId, value]);

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
      status={status}
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
      onSelect={(selected) => onChange?.(selected)}
      allowClear
    />
  );
}
