import { useEffect } from 'react';
import { InputNumber } from 'antd';
import type { FormInstance } from 'antd';
import { fetchLastPurchasePriceHint } from '@/shared/api/procurement.api';
import { moneyInputNumberPropsAllowZeroSuffix, moneyInputNumberStyle } from '@/shared/utils/money';

interface PoUnitPriceFieldProps {
  supplierId?: string;
  productId?: string;
  value?: number;
  onChange?: (v: number | null) => void;
  form: FormInstance;
  fieldName: number;
  disabled?: boolean;
  valueFieldName?: 'unitPrice' | 'unitCost';
}

export function PoUnitPriceField({
  supplierId,
  productId,
  value,
  onChange,
  form,
  fieldName,
  disabled,
  valueFieldName = 'unitPrice',
}: PoUnitPriceFieldProps) {
  useEffect(() => {
    if (disabled || !supplierId || !productId) return;

    let cancelled = false;
    fetchLastPurchasePriceHint(supplierId, productId)
      .then((h) => {
        if (cancelled || h.unitPrice == null) return;
        const current = form.getFieldValue(['items', fieldName, valueFieldName]);
        if (current === 0 || current === undefined || current === null) {
          form.setFieldValue(['items', fieldName, valueFieldName], h.unitPrice);
        }
      })
      .catch(() => {});

    return () => {
      cancelled = true;
    };
  }, [supplierId, productId, fieldName, form, disabled, valueFieldName]);

  return (
    <InputNumber
      {...moneyInputNumberPropsAllowZeroSuffix}
      placeholder="0 đ"
      style={moneyInputNumberStyle}
      value={value}
      onChange={onChange}
      disabled={disabled}
    />
  );
}
