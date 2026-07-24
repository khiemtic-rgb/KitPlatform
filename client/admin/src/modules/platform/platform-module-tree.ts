import type { PlatformModuleRegistryItem } from '@/shared/api/platform.types';
import type { DataNode } from 'antd/es/tree';

/**
 * Commercial pack tree (Cha = gói bán, Con = SKU module_code).
 * Không gồm IAM hệ thống (chi nhánh / user / role / audit) — luôn có.
 */
export const PLATFORM_MODULE_GROUPS: ReadonlyArray<{
  key: string;
  labelKey: string;
  children: readonly string[];
}> = [
  {
    key: 'group:novixa_pharmacy',
    labelKey: 'moduleGroups.novixaPharmacy',
    children: [
      'sales',
      'inventory',
      'procurement',
      'reports',
      'loyalty',
      'customer_app',
      'medication',
      'health_wallet',
      'reservations',
    ],
  },
  {
    key: 'group:novixa_clinic',
    labelKey: 'moduleGroups.novixaClinic',
    children: [
      'clinic',
      'clinic_appointments',
      'clinic_emr_lite',
      'clinic_telemed_remote',
      'clinic_telemed_video',
    ],
  },
  {
    key: 'group:novixa_connect',
    labelKey: 'moduleGroups.novixaConnect',
    children: [
      'novixa_connect',
      'e_rx',
      'prescriber_network',
      'prescriber_portal',
      'telehealth',
    ],
  },
  {
    key: 'group:survey_kap',
    labelKey: 'moduleGroups.surveyKap',
    children: ['assessment', 'pharmacy_survey', 'crm_leads'],
  },
  {
    key: 'group:family_os',
    labelKey: 'moduleGroups.familyOs',
    children: ['family_os'],
  },
  {
    key: 'group:lab_spa',
    labelKey: 'moduleGroups.labSpa',
    children: ['lab', 'spa'],
  },
];

/** Gói mẫu Core — prefill allowed_modules (+ gợi ý vertical). */
export const PLATFORM_PACK_TEMPLATES: ReadonlyArray<{
  id: string;
  labelKey: string;
  suggestedVertical: string;
  moduleCodes: readonly string[];
}> = [
  {
    id: 'novixa_pharmacy',
    labelKey: 'packTemplates.novixaPharmacy',
    suggestedVertical: 'pharmacy',
    moduleCodes: [
      'sales',
      'inventory',
      'procurement',
      'reports',
      'loyalty',
      'customer_app',
      'medication',
      'health_wallet',
      'reservations',
      'novixa_connect',
    ],
  },
  {
    id: 'novixa_pharmacy_core',
    labelKey: 'packTemplates.novixaPharmacyCore',
    suggestedVertical: 'pharmacy',
    moduleCodes: ['sales', 'inventory', 'procurement', 'reports'],
  },
  {
    id: 'novixa_clinic',
    labelKey: 'packTemplates.novixaClinic',
    suggestedVertical: 'clinic',
    moduleCodes: [
      'clinic_appointments',
      'clinic_emr_lite',
      'novixa_connect',
      'crm_leads',
    ],
  },
  {
    id: 'novixa_clinic_plus_telemed',
    labelKey: 'packTemplates.novixaClinicTelemed',
    suggestedVertical: 'clinic',
    moduleCodes: [
      'clinic_appointments',
      'clinic_emr_lite',
      'clinic_telemed_remote',
      'clinic_telemed_video',
      'novixa_connect',
      'telehealth',
    ],
  },
  {
    id: 'novixa_connect',
    labelKey: 'packTemplates.novixaConnect',
    suggestedVertical: 'hybrid',
    moduleCodes: ['novixa_connect'],
  },
  {
    id: 'survey_kap',
    labelKey: 'packTemplates.surveyKap',
    suggestedVertical: 'pharmacy',
    moduleCodes: ['assessment', 'pharmacy_survey', 'crm_leads', 'reports'],
  },
  {
    id: 'family_os',
    labelKey: 'packTemplates.familyOs',
    suggestedVertical: 'family',
    moduleCodes: ['family_os'],
  },
];

const GROUP_PREFIX = 'group:';

export function isModuleGroupKey(key: string): boolean {
  return key.startsWith(GROUP_PREFIX);
}

export function moduleMatchesVertical(
  item: PlatformModuleRegistryItem,
  vertical: string | undefined,
): boolean {
  if (!vertical?.trim()) return true;
  const v = vertical.trim().toLowerCase();
  if (v === 'hybrid') return true;

  const verts = (item.verticals ?? []).map((x) => String(x).toLowerCase());
  if (verts.length === 0) return true;
  return verts.includes(v);
}

export function filterModulesForVertical(
  registry: PlatformModuleRegistryItem[],
  vertical: string | undefined,
): PlatformModuleRegistryItem[] {
  return registry.filter((item) => moduleMatchesVertical(item, vertical));
}

export function pruneModulesToVertical(
  moduleCodes: string[],
  registry: PlatformModuleRegistryItem[],
  vertical: string | undefined,
): string[] {
  const allowed = new Set(
    filterModulesForVertical(registry, vertical).map((m) => m.moduleCode.toLowerCase()),
  );
  return moduleCodes.filter((code) => allowed.has(code.toLowerCase()));
}

/** Resolve template modules that exist in registry (and optional vertical filter). */
export function resolvePackTemplateModules(
  templateId: string,
  registry: PlatformModuleRegistryItem[],
  vertical?: string,
): { vertical: string; moduleCodes: string[] } | null {
  const template = PLATFORM_PACK_TEMPLATES.find((t) => t.id === templateId);
  if (!template) return null;

  const nextVertical = vertical?.trim() || template.suggestedVertical;
  const inRegistry = new Set(registry.map((m) => m.moduleCode.toLowerCase()));
  const codes = template.moduleCodes.filter((c) => inRegistry.has(c.toLowerCase()));
  const pruned = pruneModulesToVertical(codes, registry, nextVertical);

  return {
    vertical: nextVertical,
    moduleCodes: pruned.length > 0 ? pruned : codes,
  };
}

export function buildModuleTreeData(
  registry: PlatformModuleRegistryItem[],
  vertical: string | undefined,
  translateGroup: (labelKey: string) => string,
): DataNode[] {
  const byCode = new Map(
    filterModulesForVertical(registry, vertical).map((m) => [m.moduleCode.toLowerCase(), m]),
  );
  const placed = new Set<string>();
  const nodes: DataNode[] = [];

  for (const group of PLATFORM_MODULE_GROUPS) {
    const children: DataNode[] = [];
    for (const code of group.children) {
      const item = byCode.get(code.toLowerCase());
      if (!item) continue;
      placed.add(item.moduleCode.toLowerCase());
      children.push({
        key: item.moduleCode,
        title: `${item.moduleName} (${item.moduleCode})`,
        isLeaf: true,
      });
    }
    if (children.length === 0) continue;
    nodes.push({
      key: group.key,
      title: translateGroup(group.labelKey),
      children,
      checkable: true,
    });
  }

  const orphans: DataNode[] = [];
  for (const item of byCode.values()) {
    if (placed.has(item.moduleCode.toLowerCase())) continue;
    orphans.push({
      key: item.moduleCode,
      title: `${item.moduleName} (${item.moduleCode})`,
      isLeaf: true,
    });
  }
  if (orphans.length > 0) {
    nodes.push({
      key: 'group:other',
      title: translateGroup('moduleGroups.other'),
      children: orphans,
      checkable: true,
    });
  }

  return nodes;
}

export function leafKeysFromChecked(checked: unknown): string[] {
  const keys = Array.isArray(checked)
    ? checked
    : ((checked as { checked?: unknown[] } | null)?.checked ?? []);
  return keys.map(String).filter((k) => !isModuleGroupKey(k));
}
