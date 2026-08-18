import type { ReactNode } from 'react';
import {
  ApiOutlined,
  CalendarOutlined,
  ClusterOutlined,
  DashboardOutlined,
  FileTextOutlined,
  FundOutlined,
  SettingOutlined,
  TagsOutlined,
  UnorderedListOutlined,
  VideoCameraOutlined,
} from '@ant-design/icons';

export type ContentNavItem = {
  key: string;
  label: string;
  path: string;
  icon: ReactNode;
};

export const CONTENT_NAV_WORK: ContentNavItem[] = [
  { key: 'content-ops', label: 'Hôm nay', path: '/content/ops', icon: <DashboardOutlined /> },
  { key: 'content-pool', label: 'Idea Pool', path: '/content/pool', icon: <ClusterOutlined /> },
  { key: 'content-packages', label: 'Góc brand', path: '/content/packages', icon: <FileTextOutlined /> },
  { key: 'content-calendar', label: 'Lịch tuần', path: '/content/calendar', icon: <CalendarOutlined /> },
  { key: 'content-videos', label: 'Videos', path: '/content/videos', icon: <VideoCameraOutlined /> },
];

export const CONTENT_NAV_SETUP: ContentNavItem[] = [
  { key: 'content-topics', label: 'Bài viết (kỹ thuật)', path: '/content/topics', icon: <UnorderedListOutlined /> },
  { key: 'content-brands', label: 'Thương hiệu', path: '/content/brands', icon: <TagsOutlined /> },
  { key: 'content-budget', label: 'Chi phí AI', path: '/content/budget', icon: <FundOutlined /> },
  { key: 'content-ai', label: 'Model AI', path: '/content/ai', icon: <ApiOutlined /> },
  { key: 'content-settings', label: 'Tuỳ chọn', path: '/content/settings', icon: <SettingOutlined /> },
];

export const CONTENT_NAV_ITEMS: ContentNavItem[] = [...CONTENT_NAV_WORK, ...CONTENT_NAV_SETUP];

export function resolveContentNavKey(pathname: string): string | undefined {
  return CONTENT_NAV_ITEMS.find((item) => pathname.startsWith(item.path))?.key;
}

export function resolveContentNavLabel(pathname: string): string | undefined {
  return CONTENT_NAV_ITEMS.find((item) => pathname.startsWith(item.path))?.label;
}
