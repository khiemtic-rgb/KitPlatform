import type { CSSProperties, ReactNode } from 'react';

/** Thanh lọc / công cụ phía trên bảng — dùng chung mọi module list. */
export const listFilterBarStyle: CSSProperties = {
  marginBottom: 16,
  display: 'flex',
  flexWrap: 'wrap',
  alignItems: 'center',
  gap: 8,
  minHeight: 40,
};

export function ListFilterBar({ children, style }: { children: ReactNode; style?: CSSProperties }) {
  return <div style={{ ...listFilterBarStyle, ...style }}>{children}</div>;
}
