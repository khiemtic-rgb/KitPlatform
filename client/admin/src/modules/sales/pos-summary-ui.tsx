import type { CSSProperties, ReactNode } from 'react';
import { InputNumber, Select, Space, Typography } from 'antd';
import {
  SALES_DISCOUNT_TYPES,
  type SalesDiscountType,
} from '@/shared/api/sales.types';
import {
  moneyInputNumberPropsAllowZeroSuffix,
  moneyInputNumberStyle,
} from '@/shared/utils/money';

export const DISCOUNT_TYPE_SELECT_OPTIONS = [
  { value: SALES_DISCOUNT_TYPES.Percent, label: '%' },
  { value: SALES_DISCOUNT_TYPES.Fixed, label: 'Giá trị' },
] as const;

const summaryRowStyle: CSSProperties = {
  display: 'flex',
  justifyContent: 'space-between',
  alignItems: 'center',
  gap: 12,
  width: '100%',
};

const amountBoxStyle: CSSProperties = {
  fontVariantNumeric: 'tabular-nums',
  minWidth: 128,
  textAlign: 'right',
  padding: '3px 10px',
  border: '1px solid #d9d9d9',
  borderRadius: 6,
  background: '#fff',
  display: 'inline-block',
};

type SummaryMoneyProps = {
  value: string;
  danger?: boolean;
  strong?: boolean;
};

export function PosSummaryMoney({ value, danger, strong }: SummaryMoneyProps) {
  return (
    <Typography.Text type={danger ? 'danger' : undefined} strong={strong} style={amountBoxStyle}>
      {value}
    </Typography.Text>
  );
}

type SummaryRowProps = SummaryMoneyProps & {
  label: ReactNode;
};

export function PosSummaryRow({ label, value, danger, strong }: SummaryRowProps) {
  return (
    <div style={summaryRowStyle}>
      {typeof label === 'string' ? <Typography.Text strong={strong}>{label}</Typography.Text> : label}
      <PosSummaryMoney value={value} danger={danger} strong={strong} />
    </div>
  );
}

export function PosSummaryPanel({ children }: { children: ReactNode }) {
  return (
    <div
      style={{
        border: '1px solid #e8e8e8',
        borderRadius: 8,
        padding: '8px 12px',
        background: '#fafafa',
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        minWidth: 300,
        maxWidth: 380,
      }}
    >
      {children}
    </div>
  );
}

export function PosSummaryDivider() {
  return <div style={{ borderTop: '1px solid #e8e8e8', margin: '2px 0' }} />;
}

type OrderDiscountRowProps = {
  maxPercent: number;
  discountType?: SalesDiscountType;
  discountValue?: number;
  disabled?: boolean;
  onTypeChange: (type?: SalesDiscountType) => void;
  onValueChange: (value: number) => void;
};

export function PosSummaryOrderDiscountRow({
  maxPercent,
  discountType,
  discountValue,
  disabled,
  onTypeChange,
  onValueChange,
}: OrderDiscountRowProps) {
  const label = (
    <Typography.Text style={{ lineHeight: 1.3 }}>
      CK đơn{' '}
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        (Tối đa {maxPercent}%)
      </Typography.Text>
    </Typography.Text>
  );

  return (
    <div style={summaryRowStyle}>
      {label}
      <Space.Compact>
        <Select
          allowClear
          size="small"
          placeholder="—"
          style={{ width: 72 }}
          disabled={disabled}
          value={discountType}
          onChange={onTypeChange}
          options={[...DISCOUNT_TYPE_SELECT_OPTIONS]}
        />
        <InputNumber
          size="small"
          disabled={disabled || !discountType}
          value={discountValue}
          {...(discountType === SALES_DISCOUNT_TYPES.Fixed
            ? {
                ...moneyInputNumberPropsAllowZeroSuffix,
                style: { ...moneyInputNumberStyle, width: 96 },
              }
            : {
                min: 0,
                max: 100,
                style: { ...moneyInputNumberStyle, width: 56 },
              })}
          onChange={(v) => onValueChange(Number(v ?? 0))}
        />
      </Space.Compact>
    </div>
  );
}
