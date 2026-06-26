import { useEffect, useState } from 'react';
import { InputNumber, Typography } from 'antd';
import type { FormInstance } from 'antd';
import { fetchLastPurchasePriceHint } from '@/shared/api/procurement.api';
import type { LastPurchasePriceHint } from '@/shared/api/procurement.types';
import { moneyInputNumberPropsAllowZero, moneyInputNumberStyle, formatDisplayMoney } from '@/shared/utils/money';

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
  const [hint, setHint] = useState<LastPurchasePriceHint | null>(null);

  useEffect(() => {
    if (disabled || !supplierId || !productId) {
      setHint(null);
      return;
    }

    let cancelled = false;
    fetchLastPurchasePriceHint(supplierId, productId)
      .then((h) => {
        if (cancelled) return;
        if (h.unitPrice == null) {
          setHint(null);
          return;
        }
        setHint(h);
        const current = form.getFieldValue(['items', fieldName, valueFieldName]);
        if (current === 0 || current === undefined || current === null) {
          form.setFieldValue(['items', fieldName, valueFieldName], h.unitPrice);
        }
      })
      .catch(() => {
        if (!cancelled) setHint(null);
      });

    return () => {
      cancelled = true;
    };
  }, [supplierId, productId, fieldName, form, disabled]);

  const hintLabel =
    hint?.unitPrice != null
      ? `Giá nhập gần nhất: ${formatDisplayMoney(hint.unitPrice)} (${hint.source === 'grn' ? 'GRN' : 'PO'} ${hint.documentNumber ?? ''})`
      : null;

  return (
    <div>
      <InputNumber
        {...moneyInputNumberPropsAllowZero}
        placeholder="Giá nhập"
        style={moneyInputNumberStyle}
        value={value}
        onChange={onChange}
        disabled={disabled}
      />
      {hintLabel && (
        <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block', maxWidth: 160, lineHeight: 1.3 }}>
          {hintLabel}
        </Typography.Text>
      )}
    </div>
  );
}
