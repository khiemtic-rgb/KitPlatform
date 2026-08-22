import { systemT } from '@/shared/i18n';

/** Thứ tự nhóm quyền theo menu admin */
export const PERMISSION_MODULE_ORDER = [
  'catalog',
  'inventory',
  'procurement',
  'sales',
  'clinic',
  'connect',
  'success',
  'survey',
  'learning',
  'customer',
  'family_os',
  'content',
  'local_os',
  'system',
] as const;

const MODULE_KEY_ALIASES: Record<string, (typeof PERMISSION_MODULE_ORDER)[number] | string> = {
  catalog: 'catalog',
  Catalog: 'catalog',
  'Danh mục': 'catalog',
  inventory: 'inventory',
  Inventory: 'inventory',
  'Kho hàng': 'inventory',
  procurement: 'procurement',
  Procurement: 'procurement',
  'Mua hàng': 'procurement',
  sales: 'sales',
  Sales: 'sales',
  'Bán hàng': 'sales',
  clinic: 'clinic',
  Clinic: 'clinic',
  connect: 'connect',
  Connect: 'connect',
  success: 'success',
  Success: 'success',
  'Cockpit chủ NT': 'success',
  survey: 'survey',
  Survey: 'survey',
  KAP: 'survey',
  'KAP — Survey': 'survey',
  learning: 'learning',
  Learning: 'learning',
  'Phát triển Nhân sự': 'learning',
  'People Development': 'learning',
  customer: 'customer',
  Customer: 'customer',
  'Khách hàng': 'customer',
  family_os: 'family_os',
  FamilyOs: 'family_os',
  FamilyOS: 'family_os',
  family: 'family_os',
  'Family OS': 'family_os',
  content: 'content',
  Content: 'content',
  kit_content: 'content',
  local_os: 'local_os',
  local: 'local_os',
  LocalOs: 'local_os',
  'Thái Nguyên Life': 'local_os',
  system: 'system',
  System: 'system',
  'Hệ thống': 'system',
};

export function normalizePermissionModuleKey(moduleName: string): string {
  return MODULE_KEY_ALIASES[moduleName] ?? moduleName.toLowerCase();
}

export function permissionModuleLabel(moduleName: string): string {
  const key = normalizePermissionModuleKey(moduleName);
  return systemT()(`permissions.modules.${key}`, { defaultValue: moduleName });
}

export function permissionLabel(permissionCode: string, permissionName?: string): string {
  return systemT()(`permissions.labels.${permissionCode}`, {
    defaultValue: permissionName ?? permissionCode,
  });
}

export function comparePermissionModules(a: string, b: string): number {
  const aKey = normalizePermissionModuleKey(a);
  const bKey = normalizePermissionModuleKey(b);
  const ai = PERMISSION_MODULE_ORDER.indexOf(aKey as (typeof PERMISSION_MODULE_ORDER)[number]);
  const bi = PERMISSION_MODULE_ORDER.indexOf(bKey as (typeof PERMISSION_MODULE_ORDER)[number]);
  const aRank = ai === -1 ? PERMISSION_MODULE_ORDER.length : ai;
  const bRank = bi === -1 ? PERMISSION_MODULE_ORDER.length : bi;
  if (aRank !== bRank) return aRank - bRank;
  return permissionModuleLabel(a).localeCompare(permissionModuleLabel(b));
}
