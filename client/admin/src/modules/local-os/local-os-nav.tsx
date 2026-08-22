import type { ReactNode } from 'react';
import { CheckSquareOutlined, ReadOutlined } from '@ant-design/icons';

export type LocalOsNavItem = {
  key: string;
  label: string;
  path: string;
  icon: ReactNode;
};

export const LOCAL_OS_NAV: LocalOsNavItem[] = [
  { key: 'localOs', label: 'Thái Nguyên Life', path: '/local-os/listings', icon: <ReadOutlined /> },
  { key: 'local-os-duyet', label: 'Duyệt tin', path: '/local-os/duyet', icon: <CheckSquareOutlined /> },
];

export function resolveLocalOsNavKey(pathname: string): string | undefined {
  if (pathname.startsWith('/local-os/duyet')) return 'local-os-duyet';
  if (pathname.startsWith('/local-os')) return 'localOs';
  return undefined;
}
