import type { CSSProperties, ReactNode } from 'react';
import { listFilterBarStyle } from '@/shared/ui/ListFilterBar';

/** Số tiền trong bảng — căn cột thẳng hàng */
export const tabularMoneyStyle: CSSProperties = {
  fontVariantNumeric: 'tabular-nums',
};

/** @deprecated Dùng `listFilterBarStyle` / `ListFilterBar` từ `@/shared/ui/ListFilterBar` */
export const filterBarStyle: CSSProperties = listFilterBarStyle;

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
