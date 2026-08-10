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
    items: ['catalog.read', 'catalog.write', 'catalog.merge'],
  },
  {
    moduleKey: 'inventory',
    items: ['inventory.read', 'inventory.write', 'inventory.approve'],
  },
  {
    moduleKey: 'procurement',
    items: [
      'procurement.read',
      'procurement.write',
      'procurement.suppliers',
      'procurement.po',
      'procurement.approve',
      'procurement.receive',
      'procurement.pay',
    ],
  },
  {
    moduleKey: 'sales',
    items: [
      'sales.read',
      'sales.write',
      'sales.pos',
      'sales.customers',
      'sales.customers.merge',
      'sales.settings',
      'sales.cancel',
      'sales.price.override',
      'sales.price.manage',
    ],
    discountCodes: ['sales.discount', 'sales.discount.unlimited'],
  },
  {
    moduleKey: 'clinic',
    items: ['clinic.read', 'clinic.write'],
  },
  {
    moduleKey: 'connect',
    items: ['connect.read', 'connect.write'],
  },
  {
    moduleKey: 'success',
    items: ['success.read'],
  },
  {
    moduleKey: 'survey',
    items: ['survey.read', 'survey.write'],
  },
  {
    moduleKey: 'content',
    items: ['content.read', 'content.write', 'content.publish', 'content.budget.manage'],
  },
  {
    moduleKey: 'learning',
    items: ['learning.read', 'learning.write'],
  },
  {
    moduleKey: 'system',
    items: ['system.read', 'system.write', 'system.delete_permanent'],
  },
];

/** Module quyền FamilyOS được hiện / lưu. */
const FAMILY_PERMISSION_MODULES = new Set(['system', 'family_os', 'family']);

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

const WRITE_IMPLIES_READ_PREFIXES = new Set([
  'catalog',
  'inventory',
  'procurement',
  'sales',
  'clinic',
  'connect',
  'survey',
  'learning',
]);
const DISCOUNT_EXCLUSIVE = ['sales.discount', 'sales.discount.unlimited'] as const;

/**
 * Selecting legacy package write unlocks specialized codes (saved together).
 * SoD gates (approve / receive / pay / sales.settings) are NOT auto-included.
 */
const WRITE_IMPLIES_PACKAGE: Record<string, string[]> = {
  'sales.write': ['sales.read', 'sales.pos', 'sales.customers'],
  'procurement.write': ['procurement.read', 'procurement.suppliers', 'procurement.po'],
};

const PACKAGE_WRITE_BY_INCLUDED_PERMISSION = Object.entries(WRITE_IMPLIES_PACKAGE).reduce<
  Record<string, string[]>
>((result, [writeCode, includedCodes]) => {
  for (const includedCode of includedCodes) {
    result[includedCode] = [...(result[includedCode] ?? []), writeCode];
  }
  return result;
}, {});

const SPECIALIZED_IMPLIES_READ: Record<string, string> = {
  'catalog.merge': 'catalog.read',
  'sales.pos': 'sales.read',
  'sales.customers': 'sales.read',
  'sales.customers.merge': 'sales.read',
  'sales.settings': 'sales.read',
  'sales.cancel': 'sales.read',
  'sales.price.override': 'sales.read',
  'sales.price.manage': 'sales.read',
  'procurement.suppliers': 'procurement.read',
  'procurement.po': 'procurement.read',
  'procurement.approve': 'procurement.read',
  'procurement.receive': 'procurement.read',
  'procurement.pay': 'procurement.read',
  'inventory.approve': 'inventory.read',
};

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

  // Package write would re-add this child on save — drop package when admin unticks a child.
  if (!checked) {
    for (const writeCode of PACKAGE_WRITE_BY_INCLUDED_PERMISSION[code] ?? []) {
      next = next.filter((c) => c !== writeCode);
    }
  }

  if (checked && DISCOUNT_EXCLUSIVE.includes(code as (typeof DISCOUNT_EXCLUSIVE)[number])) {
    next = next.filter(
      (c) => c === code || !DISCOUNT_EXCLUSIVE.includes(c as (typeof DISCOUNT_EXCLUSIVE)[number]),
    );
  }

  if (checked && WRITE_IMPLIES_PACKAGE[code]) {
    next.push(...WRITE_IMPLIES_PACKAGE[code]);
  }

  if (checked && SPECIALIZED_IMPLIES_READ[code]) {
    next.push(SPECIALIZED_IMPLIES_READ[code]);
  }

  const [prefix, action] = code.split('.');
  if (WRITE_IMPLIES_READ_PREFIXES.has(prefix) && action === 'write' && checked) {
    next.push(`${prefix}.read`);
  }
  if (WRITE_IMPLIES_READ_PREFIXES.has(prefix) && action === 'read' && !checked) {
    next = next.filter((c) => !c.startsWith(`${prefix}.`));
  }

  return [...new Set(next)];
}

/** Ghi DB: quyền gói write luôn kèm các quyền chuyên biệt Phase 1 (trừ SoD). */
export function normalizePermissionCodesForSave(codes: string[]): string[] {
  const set = new Set(codes);
  for (const prefix of WRITE_IMPLIES_READ_PREFIXES) {
    if (set.has(`${prefix}.write`)) set.add(`${prefix}.read`);
  }
  for (const [writeCode, extras] of Object.entries(WRITE_IMPLIES_PACKAGE)) {
    if (set.has(writeCode)) {
      for (const extra of extras) set.add(extra);
    }
  }
  for (const [specialized, readCode] of Object.entries(SPECIALIZED_IMPLIES_READ)) {
    if (set.has(specialized)) set.add(readCode);
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

export type GroupPermissionsOptions = {
  /** When family — hide pharmacy/clinic permission groups. */
  vertical?: 'pharmacy' | 'clinic' | 'family';
};

/** FamilyOS: chỉ giữ system / family_os (allowlist — không dựa regex tiếng Việt). */
function isFamilyFacingPermissionCode(code: string): boolean {
  const prefix = (code.split('.')[0] ?? '').trim().toLowerCase();
  return FAMILY_PERMISSION_MODULES.has(prefix);
}

function isFamilyFacingModule(moduleKey: string): boolean {
  const key = normalizePermissionModuleKey(moduleKey);
  return FAMILY_PERMISSION_MODULES.has(key);
}

/** Khi lưu quyền trên FamilyOS — bỏ mã nhà thuốc còn sót từ template cũ. */
export function filterPermissionCodesForVertical(
  codes: string[],
  vertical?: GroupPermissionsOptions['vertical'],
): string[] {
  if (vertical !== 'family') return codes;
  return codes.filter((code) => isFamilyFacingPermissionCode(code));
}

/** Gom quyền từ API theo module (fallback nếu có quyền mới chưa khai báo UI). */
export function groupPermissionsForUi(
  permissions: PermissionLookupLike[],
  options?: GroupPermissionsOptions,
): PermissionUiGroup[] {
  const isFamily = options?.vertical === 'family';
  const known = new Set(
    PERMISSION_UI_GROUP_DEFS.flatMap((g) => [...g.items, ...(g.discountCodes ?? [])]),
  );
  const extras = new Map<string, PermissionUiItem[]>();

  for (const p of permissions) {
    if (known.has(p.permissionCode)) continue;
    if (isFamily && !isFamilyFacingPermissionCode(p.permissionCode)) continue;
    const moduleKey = normalizePermissionModuleKey(p.moduleName);
    if (isFamily && !isFamilyFacingModule(moduleKey)) continue;
    const list = extras.get(moduleKey) ?? [];
    list.push({
      code: p.permissionCode,
      label: permissionLabel(p.permissionCode, p.permissionName),
    });
    extras.set(moduleKey, list);
  }

  const defs = isFamily
    ? PERMISSION_UI_GROUP_DEFS.filter((g) => FAMILY_PERMISSION_MODULES.has(g.moduleKey))
    : PERMISSION_UI_GROUP_DEFS;

  const groups: PermissionUiGroup[] = defs.map((group) =>
    buildPermissionUiGroup(group.moduleKey, group.items, group.discountCodes),
  );

  for (const [moduleKey, items] of extras.entries()) {
    const existing = groups.find((g) => g.moduleKey === moduleKey);
    if (existing) {
      const seen = new Set(existing.items.map((i) => i.code));
      for (const item of items) {
        if (!seen.has(item.code)) existing.items.push(item);
      }
    } else {
      groups.push({
        moduleKey,
        moduleLabel: permissionModuleLabel(moduleKey),
        items,
      });
    }
  }

  return groups.sort((a, b) => comparePermissionModules(a.moduleKey, b.moduleKey));
}
