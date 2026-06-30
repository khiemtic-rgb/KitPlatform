import type { ReactNode } from 'react';
import {
  BarChartOutlined,
  DashboardOutlined,
  MedicineBoxOutlined,
  ShoppingOutlined,
  TeamOutlined,
  InboxOutlined,
  ShopOutlined,
  SettingOutlined,
} from '@ant-design/icons';
import { commonT } from '@/shared/i18n';
export type ModuleKey = 'dashboard' | 'catalog' | 'inventory' | 'procurement' | 'sales' | 'customer' | 'reports' | 'system';

/**
 * Module trên header — cùng thứ tự sidebar, theo luồng triển khai / vận hành:
 * Danh mục → Mua hàng → Kho → Bán hàng → Khách hàng → Báo cáo
 */
export const HEADER_MODULE_KEYS: ModuleKey[] = [
  'dashboard',
  'catalog',
  'procurement',
  'inventory',
  'sales',
  'customer',
  'reports',
];

export interface ModuleMenuItem {
  key: ModuleKey;
  label: string;
  path: string;
  icon: ReactNode;
  enabled: boolean;
}

/** Bật module khi API backend sẵn sàng — thứ tự = luồng go-live rồi vận hành hàng ngày */
export const moduleRegistry: ModuleMenuItem[] = [
  { key: 'dashboard', label: 'dashboard', path: '/', icon: <DashboardOutlined />, enabled: true },
  { key: 'catalog', label: 'catalog', path: '/catalog/products', icon: <MedicineBoxOutlined />, enabled: true },
  { key: 'procurement', label: 'procurement', path: '/procurement/suppliers', icon: <ShoppingOutlined />, enabled: true },
  { key: 'inventory', label: 'inventory', path: '/inventory/opening-balance', icon: <InboxOutlined />, enabled: true },
  { key: 'sales', label: 'sales', path: '/sales/pos', icon: <ShopOutlined />, enabled: true },
  { key: 'customer', label: 'customer', path: '/customer', icon: <TeamOutlined />, enabled: true },
  { key: 'reports', label: 'reports', path: '/reports', icon: <BarChartOutlined />, enabled: true },
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