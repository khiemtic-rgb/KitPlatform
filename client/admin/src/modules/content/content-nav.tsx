import type { ReactNode } from 'react';
import {
  ApiOutlined,
  CalendarOutlined,
  DashboardOutlined,
  ReadOutlined,
  ControlOutlined,
  VideoCameraOutlined,
} from '@ant-design/icons';

export type ContentNavItem = {
  key: string;
  label: string;
  path: string;
  icon: ReactNode;
};

export const CONTENT_NAV_WORK: ContentNavItem[] = [
  { key: 'content-overview', label: 'Tổng quan', path: '/content/overview', icon: <DashboardOutlined /> },
  { key: 'content-work', label: 'Bài viết', path: '/content/work', icon: <ReadOutlined /> },
  { key: 'content-studio', label: 'Video Studio', path: '/content/studio', icon: <VideoCameraOutlined /> },
  { key: 'content-calendar', label: 'Lịch đăng & Agent', path: '/content/calendar', icon: <CalendarOutlined /> },
  { key: 'content-channels', label: 'Kênh đăng', path: '/content/channels', icon: <ApiOutlined /> },
  { key: 'content-settings', label: 'Cấu hình', path: '/content/brands', icon: <ControlOutlined /> },
];

/** Kept so older shells can spread it. Videos is no longer a sibling of Content. */
export const CONTENT_NAV_OUTSIDE: ContentNavItem[] = [];

export const CONTENT_NAV_SETUP: ContentNavItem[] = [];

const CONTENT_ROUTE_OWNER: { key: string; prefixes: string[] }[] = [
  { key: 'content-overview', prefixes: ['/content/overview', '/content/ops'] },
  {
    key: 'content-work',
    prefixes: ['/content/work', '/content/pool', '/content/packages', '/content/article-series', '/content/topics', '/content/brand-view', '/content/import'],
  },
  { key: 'content-studio', prefixes: ['/content/studio', '/content/videos', '/content/visual-calibration'] },
  { key: 'content-calendar', prefixes: ['/content/calendar', '/content/agent'] },
  { key: 'content-channels', prefixes: ['/content/channels', '/content/youtube', '/content/tiktok', '/content/facebook'] },
  { key: 'content-settings', prefixes: ['/content/settings', '/content/brands', '/content/budget', '/content/ai'] },
];

export const CONTENT_NAV_ITEMS: ContentNavItem[] = [
  ...CONTENT_NAV_WORK,
  ...CONTENT_NAV_SETUP,
  ...CONTENT_NAV_OUTSIDE,
];

export function resolveContentNavKey(pathname: string): string | undefined {
  const owner = CONTENT_ROUTE_OWNER.find((group) => group.prefixes.some((prefix) => pathname.startsWith(prefix)));
  return owner?.key ?? CONTENT_NAV_ITEMS.find((item) => pathname.startsWith(item.path))?.key;
}

export function resolveContentNavLabel(pathname: string): string | undefined {
  const key = resolveContentNavKey(pathname);
  return CONTENT_NAV_ITEMS.find((item) => item.key === key)?.label;
}
