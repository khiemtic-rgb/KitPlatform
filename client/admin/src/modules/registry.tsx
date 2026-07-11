import type { ReactNode } from 'react';
import {
  AccountBookOutlined,
  FormOutlined,
  BarChartOutlined,
  DashboardOutlined,
  FileProtectOutlined,
  MedicineBoxOutlined,
  ShoppingOutlined,
  TeamOutlined,
  InboxOutlined,
  ShopOutlined,
  SettingOutlined,
} from '@ant-design/icons';
import { commonT } from '@/shared/i18n';
import { ADMIN_MODULE_PLATFORM_CODES } from '@/shared/platform/platform-feature-map';

export type ModuleKey =
  | 'dashboard'
  | 'catalog'
  | 'inventory'
  | 'procurement'
  | 'sales'
  | 'rx'
  | 'receivables'
  | 'customer'
  | 'reports'
  | 'kap'
  | 'system';

/**
 * Module trên header POS dropdown — cùng thứ tự sidebar.
 */
export const HEADER_MODULE_KEYS: ModuleKey[] = [
  'dashboard',
  'sales',
  'rx',
  'procurement',
  'inventory',
  'receivables',
  'customer',
  'catalog',
  'reports',
];

export interface ModuleMenuItem {
  key: ModuleKey;
  label: string;
  path: string;
  icon: ReactNode;
  enabled: boolean;
  /** KIT Platform module code — ẩn sidebar khi tenant tắt module (migration 051). */
  platformModule?: string;
}

/** Sidebar cấp 1 — thứ tự theo luồng vận hành nhà thuốc */
export const moduleRegistry: ModuleMenuItem[] = [
  { key: 'dashboard', label: 'dashboard', path: '/', icon: <DashboardOutlined />, enabled: true },
  {
    key: 'sales',
    label: 'sales',
    path: '/sales/pos',
    icon: <ShopOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.sales,
  },
  {
    key: 'rx',
    label: 'rx',
    path: '/rx/overview',
    icon: <FileProtectOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.rx,
  },
  {
    key: 'procurement',
    label: 'procurement',
    path: '/procurement/suppliers',
    icon: <ShoppingOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.procurement,
  },
  {
    key: 'inventory',
    label: 'inventory',
    path: '/inventory/stock',
    icon: <InboxOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.inventory,
  },
  {
    key: 'receivables',
    label: 'receivables',
    path: '/receivables/customers',
    icon: <AccountBookOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.receivables,
  },
  {
    key: 'customer',
    label: 'customer',
    path: '/customer',
    icon: <TeamOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.customer,
  },
  {
    key: 'catalog',
    label: 'catalog',
    path: '/catalog/products',
    icon: <MedicineBoxOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.catalog,
  },
  {
    key: 'reports',
    label: 'reports',
    path: '/reports',
    icon: <BarChartOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.reports,
  },
  { key: 'kap', label: 'kap', path: '/kap/leads', icon: <FormOutlined />, enabled: true },
  { key: 'system', label: 'system', path: '/system/branches', icon: <SettingOutlined />, enabled: true },
];

export function buildMenuItems() {
  const t = commonT();
  return moduleRegistry.map((module) => ({
    key: module.key,
    icon: module.icon,
    label: module.enabled
      ? t(`modules.${module.key}`)
      : t('modules.comingSoon', { name: t(`modules.${module.key}`) }),
    disabled: !module.enabled,
  }));
}
