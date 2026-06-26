import type { ReactNode } from 'react';
import {
  DashboardOutlined,
  MedicineBoxOutlined,
  ShoppingOutlined,
  TeamOutlined,
  InboxOutlined,
  ShopOutlined,
  SettingOutlined,
} from '@ant-design/icons';

export type ModuleKey = 'dashboard' | 'catalog' | 'inventory' | 'procurement' | 'sales' | 'customer' | 'system';

export interface ModuleMenuItem {
  key: ModuleKey;
  label: string;
  path: string;
  icon: ReactNode;
  enabled: boolean;
}

/** Bật module khi API backend sẵn sàng */
export const moduleRegistry: ModuleMenuItem[] = [
  { key: 'dashboard', label: 'Tổng quan', path: '/', icon: <DashboardOutlined />, enabled: true },
  { key: 'catalog', label: 'Danh mục', path: '/catalog/products', icon: <MedicineBoxOutlined />, enabled: true },
  { key: 'inventory', label: 'Kho hàng', path: '/inventory', icon: <InboxOutlined />, enabled: true },
  { key: 'procurement', label: 'Mua hàng', path: '/procurement', icon: <ShoppingOutlined />, enabled: true },
  { key: 'sales', label: 'Bán hàng', path: '/sales', icon: <ShopOutlined />, enabled: true },
  { key: 'customer', label: 'Khách hàng', path: '/customer', icon: <TeamOutlined />, enabled: true },
  { key: 'system', label: 'Hệ thống', path: '/system', icon: <SettingOutlined />, enabled: true },
];

export function buildMenuItems() {
  return moduleRegistry.map((module) => ({
    key: module.key,
    icon: module.icon,
    label: module.enabled ? module.label : `${module.label} (sắp có)`,
    disabled: !module.enabled,
  }));
}
