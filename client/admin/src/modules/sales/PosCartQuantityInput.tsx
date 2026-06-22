import { useEffect, useState } from 'react';
import { Input, Typography } from 'antd';
import {
  capQuantityToStock,
  outOfStockWarningText,
  stockCapWarningText,
} from '@/modules/sales/pos-stock-messages';

type Props = {
  value: number;
  stockAvailable: number;
  unitName: string;
  disabled?: boolean;
  externalWarning?: string;
  onChange: (quantity: number) => void;
  onClearWarning?: () => void;
};

export function PosCartQuantityInput({
  value,
  stockAvailable,
  unitName,
  disabled,
  externalWarning,
  onChange,
  onClearWarning,
}: Props) {
  const [draft, setDraft] = useState(String(value));
  const [focused, setFocused] = useState(false);
  const [inlineWarning, setInlineWarning] = useState<string | null>(null);

  const outOfStock = stockAvailable <= 0;
  const warning = inlineWarning ?? externalWarning ?? (outOfStock ? outOfStockWarningText(unitName) : null);

  useEffect(() => {
    if (!focused) {
      setDraft(String(value));
    }
  }, [focused, value]);

  const capToStock = (requested: number) => {
    const capped = capQuantityToStock(stockAvailable, requested);
    const text =
      stockAvailable <= 0
        ? outOfStockWarningText(unitName)
        : stockCapWarningText(stockAvailable, unitName);
    setInlineWarning(text);
    setDraft(String(capped));
    if (capped !== value) {
      onChange(capped);
    }
  };

  const handleDraftChange = (raw: string) => {
    if (outOfStock) return;

    onClearWarning?.();
    setInlineWarning(null);

    const digits = raw.replace(/\D/g, '');
    setDraft(digits);
    if (!digits) return;

    const next = parseInt(digits, 10);
    if (Number.isNaN(next) || next < 1) return;

    if (next > stockAvailable + 0.0001) {
      capToStock(next);
      return;
    }

    if (next !== value) {
      onChange(next);
    }
  };

  const handleBlur = () => {
    setFocused(false);
    if (outOfStock) {
      setDraft('0');
      if (value !== 0) onChange(0);
      return;
    }

    const digits = draft.replace(/\D/g, '');
    if (!digits) {
      setDraft(String(value));
      setInlineWarning(null);
      return;
    }

    const next = parseInt(digits, 10);
    if (Number.isNaN(next) || next < 1) {
      setDraft(String(value));
      return;
    }

    if (next > stockAvailable + 0.0001) {
      capToStock(next);
      return;
    }

    setInlineWarning(null);
    if (next !== value) {
      onChange(next);
    }
  };

  return (
    <div style={{ minWidth: 200 }}>
      <Input
        value={outOfStock ? '0' : draft}
        disabled={disabled || outOfStock}
        inputMode="numeric"
        aria-label="Số lượng bán"
        status={warning ? 'warning' : undefined}
        style={{ width: 76, textAlign: 'right' }}
        onFocus={() => {
          if (outOfStock) return;
          setFocused(true);
          setDraft(String(value));
          setInlineWarning(null);
          onClearWarning?.();
        }}
        onChange={(e) => handleDraftChange(e.target.value)}
        onBlur={handleBlur}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            e.currentTarget.blur();
          }
        }}
      />
      {warning ? (
        <Typography.Text
          type="warning"
          style={{ fontSize: 11, lineHeight: 1.3, display: 'block', marginTop: 4, maxWidth: 220 }}
        >
          {warning}
        </Typography.Text>
      ) : null}
    </div>
  );
}
