import type { ReactNode } from 'react';
import {
  AccountBookOutlined,
  ApartmentOutlined,
  AppstoreOutlined,
  FormOutlined,
  BarChartOutlined,
  DashboardOutlined,
  FileProtectOutlined,
  FundOutlined,
  MedicineBoxOutlined,
  ShoppingOutlined,
  TeamOutlined,
  InboxOutlined,
  ShopOutlined,
  SettingOutlined,
  HomeOutlined,
  RiseOutlined,
  ReadOutlined,
} from '@ant-design/icons';
import { commonT } from '@/shared/i18n';
import { ADMIN_MODULE_PLATFORM_CODES } from '@/shared/platform/platform-feature-map';

export type ModuleKey =
  | 'dashboard'
  | 'success'
  | 'catalog'
  | 'inventory'
  | 'procurement'
  | 'sales'
  | 'rx'
  | 'connect'
  | 'clinic'
  | 'familyOs'
  | 'receivables'
  | 'customer'
  | 'reports'
  | 'kap'
  | 'content'
  | 'learning'
  | 'system';

/** Tenant platform.vertical — lọc sidebar theo loại tổ chức. */
export type AdminVertical = 'pharmacy' | 'clinic' | 'family';

/**
 * Module tạm ẩn khỏi sidebar/header (giữ code + route).
 * - rx: Portal BS / e-Rx NT — kê đơn pháp lý qua Clinic + Connect.
 */
export const TEMP_HIDDEN_MODULE_KEYS: readonly ModuleKey[] = ['rx'];

/**
 * Module trên header POS dropdown — cùng thứ tự sidebar.
 */
export const HEADER_MODULE_KEYS: ModuleKey[] = (
  [
    'dashboard',
    'success',
    'sales',
    'clinic',
    'rx',
    'connect',
    'procurement',
    'inventory',
    'receivables',
    'customer',
    'catalog',
    'reports',
    'learning',
  ] as ModuleKey[]
).filter((k) => !TEMP_HIDDEN_MODULE_KEYS.includes(k));

export interface ModuleMenuItem {
  key: ModuleKey;
  label: string;
  path: string;
  icon: ReactNode;
  enabled: boolean;
  /** KIT Platform module code — ẩn sidebar khi tenant tắt module (migration 051). */
  platformModule?: string;
  /**
   * Vertical được phép thấy module.
   * Mặc định cả pharmacy + clinic. PK thuần không thấy POS/kho/mua hàng…
   */
  verticals?: readonly AdminVertical[];
}

const PHARMACY_ONLY: readonly AdminVertical[] = ['pharmacy'];
const CLINIC_ONLY: readonly AdminVertical[] = ['clinic'];
const FAMILY_ONLY: readonly AdminVertical[] = ['family'];
const ALL_VERTICALS: readonly AdminVertical[] = ['pharmacy', 'clinic', 'family'];

/** Sidebar cấp 1 — thứ tự theo luồng vận hành */
export const moduleRegistry: ModuleMenuItem[] = [
  {
    key: 'dashboard',
    label: 'dashboard',
    path: '/',
    icon: <DashboardOutlined />,
    enabled: true,
    verticals: ALL_VERTICALS,
  },
  {
    key: 'success',
    label: 'success',
    path: '/success/cockpit',
    icon: <FundOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.success,
    verticals: PHARMACY_ONLY,
  },
  {
    key: 'sales',
    label: 'sales',
    path: '/sales/pos',
    icon: <ShopOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.sales,
    verticals: PHARMACY_ONLY,
  },
  {
    key: 'rx',
    label: 'rx',
    path: '/rx/overview',
    icon: <FileProtectOutlined />,
    // Tạm tắt Portal BS / e-Rx NT — kê đơn pháp lý qua Clinic + Connect. Bật lại: true + bỏ khỏi TEMP_HIDDEN_MODULE_KEYS.
    enabled: false,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.rx,
    verticals: PHARMACY_ONLY,
  },
  {
    key: 'connect',
    label: 'connect',
    path: '/connect/overview',
    icon: <ApartmentOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.connect,
    verticals: ['pharmacy', 'clinic'],
  },
  {
    key: 'clinic',
    label: 'clinic',
    path: '/clinic/overview',
    icon: <MedicineBoxOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.clinic,
    verticals: CLINIC_ONLY,
  },
  {
    key: 'familyOs',
    label: 'familyOs',
    path: '/family-os/overview',
    icon: <HomeOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.familyOs,
    verticals: FAMILY_ONLY,
  },
  {
    key: 'procurement',
    label: 'procurement',
    path: '/procurement/suppliers',
    icon: <ShoppingOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.procurement,
    verticals: PHARMACY_ONLY,
  },
  {
    key: 'inventory',
    label: 'inventory',
    path: '/inventory/stock',
    icon: <InboxOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.inventory,
    verticals: PHARMACY_ONLY,
  },
  {
    key: 'receivables',
    label: 'receivables',
    path: '/receivables/customers',
    icon: <AccountBookOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.receivables,
    verticals: PHARMACY_ONLY,
  },
  {
    key: 'customer',
    label: 'customer',
    path: '/customer',
    icon: <TeamOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.customer,
    verticals: ['pharmacy', 'clinic'],
  },
  {
    key: 'catalog',
    label: 'catalog',
    path: '/catalog/products',
    icon: <AppstoreOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.catalog,
    verticals: PHARMACY_ONLY,
  },
  {
    key: 'reports',
    label: 'reports',
    path: '/reports',
    icon: <BarChartOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.reports,
    verticals: PHARMACY_ONLY,
  },
  {
    key: 'learning',
    label: 'learning',
    path: '/people',
    icon: <RiseOutlined />,
    enabled: true,
    // Không gắn platformModule — tránh ẩn tab khi tenant chưa có code `learning` trong enabled_modules.
    verticals: PHARMACY_ONLY,
  },
  {
    key: 'kap',
    label: 'kap',
    path: '/kap/leads',
    icon: <FormOutlined />,
    enabled: true,
    platformModule: ADMIN_MODULE_PLATFORM_CODES.kap,
    verticals: ['pharmacy', 'clinic'],
  },
  {
    key: 'content',
    label: 'content',
    path: '/content/budget',
    icon: <ReadOutlined />,
    enabled: true,
    // Platform ADMIN ops — không gắn platformModule tenant pharmacy.
    verticals: ALL_VERTICALS,
  },
  {
    key: 'system',
    label: 'system',
    path: '/system/branches',
    icon: <SettingOutlined />,
    enabled: true,
    verticals: ALL_VERTICALS,
  },
];

/** Vertical tenant (platform.settings) — mặc định pharmacy. */
export function resolveAdminVertical(raw: string | null | undefined): AdminVertical {
  const value = String(raw ?? 'pharmacy').trim().toLowerCase();
  if (value === 'clinic') return 'clinic';
  if (value === 'family') return 'family';
  return 'pharmacy';
}

export function isModuleVisibleForVertical(
  module: ModuleMenuItem,
  vertical: AdminVertical,
): boolean {
  const scopes = module.verticals ?? (['pharmacy', 'clinic'] as const);
  return scopes.includes(vertical);
}

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
