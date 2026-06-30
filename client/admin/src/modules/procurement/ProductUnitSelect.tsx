import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Select } from 'antd';
import { fetchProduct } from '@/shared/api/catalog.api';
import type { ProductUnit } from '@/shared/api/catalog.types';
import { formatUnitLabel, pickDefaultProductUnitId } from '@/modules/procurement/product-unit.helpers';

const unitsCache = new Map<string, ProductUnit[]>();

interface ProductUnitSelectProps {
  productId?: string;
  value?: string;
  onChange?: (unitId: string) => void;
  width?: number;
  disabled?: boolean;
}

export function ProductUnitSelect({
  productId,
  value,
  onChange,
  width = 130,
  disabled = false,
}: ProductUnitSelectProps) {
  const { t } = useTranslation('procurement', { keyPrefix: 'shared.columns' });
  const [units, setUnits] = useState<ProductUnit[]>([]);
  const [loading, setLoading] = useState(false);
  const lastProductId = useRef<string | undefined>(undefined);
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;

  useEffect(() => {
    if (!productId) {
      setUnits([]);
      lastProductId.current = undefined;
      return;
    }

    const applyDefault = (list: ProductUnit[]) => {
      if (lastProductId.current === productId) return;
      const defaultId = pickDefaultProductUnitId(list);
      if (defaultId) onChangeRef.current?.(defaultId);
      lastProductId.current = productId;
    };

    const cached = unitsCache.get(productId);
    if (cached) {
      setUnits(cached);
      applyDefault(cached);
      return;
    }

    let cancelled = false;
    setLoading(true);
    fetchProduct(productId)
      .then((product) => {
        if (cancelled) return;
        unitsCache.set(productId, product.units);
        setUnits(product.units);
        applyDefault(product.units);
      })
      .catch(() => {
        if (!cancelled) setUnits([]);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [productId]);

  return (
    <Select
      value={value}
      onChange={onChange}
      loading={loading}
      disabled={disabled || !productId}
      placeholder={t('unitSelect')}
      style={{ width }}
      options={units.map((u) => ({
        value: u.id,
        label: formatUnitLabel(u),
      }))}
    />
  );
}
