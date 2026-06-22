import type { CSSProperties, ReactNode } from 'react';

/** Số tiền trong bảng — căn cột thẳng hàng */
export const tabularMoneyStyle: CSSProperties = {
  fontVariantNumeric: 'tabular-nums',
};

/** Thanh lọc / công cụ phía trên bảng */
export const filterBarStyle: CSSProperties = {
  marginBottom: 16,
};

/** Khoảng cách block trong drawer / modal */
export const sectionGapStyle: CSSProperties = {
  marginBottom: 16,
};

export const sectionGapTopStyle: CSSProperties = {
  marginTop: 16,
};

export function TabularMoney({ children }: { children: ReactNode }) {
  return <span style={tabularMoneyStyle}>{children}</span>;
}
