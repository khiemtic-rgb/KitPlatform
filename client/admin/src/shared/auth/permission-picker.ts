import { systemT } from '@/shared/i18n';
import {
  comparePermissionModules,
  normalizePermissionModuleKey,
  permissionLabel,
  permissionModuleLabel,
} from '@/shared/auth/permission-labels';

export type PermissionUiItem = { code: string; label: string };

export type PermissionUiGroup = {
  moduleKey: string;
  moduleLabel: string;
  hint?: string;
  items: PermissionUiItem[];
  discountCodes?: string[];
};

const PERMISSION_UI_GROUP_DEFS: Array<{
  moduleKey: string;
  items: string[];
  discountCodes?: string[];
}> = [
  {
    moduleKey: 'catalog',
    items: ['catalog.read', 'catalog.write'],
  },
  {
    moduleKey: 'inventory',
    items: ['inventory.read', 'inventory.write'],
  },
  {
    moduleKey: 'procurement',
    items: ['procurement.read', 'procurement.write'],
  },
  {
    moduleKey: 'sales',
    items: ['sales.read', 'sales.write'],
    discountCodes: ['sales.discount', 'sales.discount.unlimited'],
  },
  {
    moduleKey: 'system',
    items: ['system.read', 'system.write', 'system.delete_permanent'],
  },
];

function buildPermissionUiGroup(
  moduleKey: string,
  items: string[],
  discountCodes?: string[],
): PermissionUiGroup {
  const t = systemT();
  return {
    moduleKey,
    moduleLabel: permissionModuleLabel(moduleKey),
    hint: t(`permissions.groupHints.${moduleKey}`, { defaultValue: '' }) || undefined,
    items: items.map((code) => ({ code, label: permissionLabel(code) })),
    discountCodes,
  };
}

const WRITE_IMPLIES_READ_PREFIXES = new Set(['catalog', 'inventory', 'procurement']);
const DISCOUNT_EXCLUSIVE = ['sales.discount', 'sales.discount.unlimited'] as const;

export type DiscountLevel = 'none' | 'sales.discount' | 'sales.discount.unlimited';

export function getDiscountLevel(codes: string[]): DiscountLevel {
  if (codes.includes('sales.discount.unlimited')) return 'sales.discount.unlimited';
  if (codes.includes('sales.discount')) return 'sales.discount';
  return 'none';
}

export function setDiscountLevel(codes: string[], level: DiscountLevel): string[] {
  const next = codes.filter((c) => !DISCOUNT_EXCLUSIVE.includes(c as (typeof DISCOUNT_EXCLUSIVE)[number]));
  if (level !== 'none') next.push(level);
  return [...new Set(next)];
}

/** Bật/tắt một quyền — áp dụng quy tắc loại trừ và phụ thuộc. */
export function applyPermissionToggle(codes: string[], code: string, checked: boolean): string[] {
  let next = checked ? [...codes, code] : codes.filter((c) => c !== code);
  next = [...new Set(next)];

  if (checked && DISCOUNT_EXCLUSIVE.includes(code as (typeof DISCOUNT_EXCLUSIVE)[number])) {
    next = next.filter(
      (c) => c === code || !DISCOUNT_EXCLUSIVE.includes(c as (typeof DISCOUNT_EXCLUSIVE)[number]),
    );
  }

  const [prefix, action] = code.split('.');
  if (WRITE_IMPLIES_READ_PREFIXES.has(prefix) && action === 'write' && checked) {
    next.push(`${prefix}.read`);
  }
  if (WRITE_IMPLIES_READ_PREFIXES.has(prefix) && action === 'read' && !checked) {
    next = next.filter((c) => c !== `${prefix}.write`);
  }

  return [...new Set(next)];
}

/** Ghi DB: quyền sửa danh mục/kho/mua hàng luôn kèm quyền xem. */
export function normalizePermissionCodesForSave(codes: string[]): string[] {
  const set = new Set(codes);
  for (const prefix of WRITE_IMPLIES_READ_PREFIXES) {
    if (set.has(`${prefix}.write`)) set.add(`${prefix}.read`);
  }
  return [...set];
}

export function discountLevelLabel(level: DiscountLevel): string {
  if (level === 'none') return systemT()('permissions.discountLevels.none');
  return permissionLabel(level);
}

export type PermissionLookupLike = {
  permissionCode: string;
  permissionName?: string;
  moduleName: string;
};

/** Gom quyền từ API theo module (fallback nếu có quyền mới chưa khai báo UI). */
export function groupPermissionsForUi(permissions: PermissionLookupLike[]): PermissionUiGroup[] {
  const known = new Set(
    PERMISSION_UI_GROUP_DEFS.flatMap((g) => [...g.items, ...(g.discountCodes ?? [])]),
  );
  const extras = new Map<string, PermissionUiItem[]>();

  for (const p of permissions) {
    if (known.has(p.permissionCode)) continue;
    const moduleKey = normalizePermissionModuleKey(p.moduleName);
    const list = extras.get(moduleKey) ?? [];
    list.push({
      code: p.permissionCode,
      label: permissionLabel(p.permissionCode, p.permissionName),
    });
    extras.set(moduleKey, list);
  }

  const groups: PermissionUiGroup[] = PERMISSION_UI_GROUP_DEFS.map((group) =>
    buildPermissionUiGroup(group.moduleKey, group.items, group.discountCodes),
  );

  for (const [moduleKey, items] of extras.entries()) {
    groups.push({
      moduleKey,
      moduleLabel: permissionModuleLabel(moduleKey),
      items,
    });
  }

  return groups.sort((a, b) => comparePermissionModules(a.moduleKey, b.moduleKey));
}
