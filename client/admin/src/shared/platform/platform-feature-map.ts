import type { ProductFeatureKey } from '@/shared/product/product-phases';

/** Maps admin product feature keys to KIT Platform module / feature codes. */
export interface PlatformGateRef {
  modules?: string[];
  features?: string[];
}

export const PRODUCT_FEATURE_PLATFORM_GATES: Partial<Record<ProductFeatureKey, PlatformGateRef>> = {
  'sales.customerReservations': { modules: ['customer_app', 'reservations'] },
  'sales.appOrders': { modules: ['customer_app'] },
  'sales.chat': { modules: ['customer_app'] },
  'catalog.nationalDrug': { features: ['national_drug_catalog'] },
  'customer.engagement': { modules: ['customer_app'] },
};

export const ADMIN_MODULE_PLATFORM_CODES: Partial<
  Record<
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
    | 'careOs'
    | 'receivables'
    | 'customer'
    | 'reports'
    | 'kap'
    | 'content'
    | 'learning'
    | 'system',
    string
  >
> = {
  success: 'reports',
  sales: 'sales',
  rx: 'e_rx',
  connect: 'novixa_connect',
  clinic: 'clinic_emr_lite',
  familyOs: 'family_os',
  /** Care OS instrumentation (T3-ready). Opt-in; not Community Health product UI. */
  careOs: 'care_os',
  procurement: 'procurement',
  inventory: 'inventory',
  receivables: 'sales',
  customer: 'sales',
  catalog: 'sales',
  reports: 'reports',
  /** Survey / KAP — SKU cha `assessment` (gói Survey·KAP). */
  kap: 'assessment',
  /** Marketing Park — product package `marketing_park`, org `KIT_MKT`. */
  content: 'kit_content',
  learning: 'learning',
};

export function isPlatformGateOpen(
  gate: PlatformGateRef | undefined,
  isModuleEnabled: (code: string) => boolean,
  isFeatureEnabled: (code: string) => boolean,
): boolean {
  if (!gate) return true;
  if (gate.modules?.some((code) => !isModuleEnabled(code))) return false;
  if (gate.features?.some((code) => !isFeatureEnabled(code))) return false;
  return true;
}
