import type { ProductFeatureKey } from '@/shared/product/product-phases';

/** Maps admin product feature keys to KIT Platform module / feature codes. */
export interface PlatformGateRef {
  modules?: string[];
  features?: string[];
}

export const PRODUCT_FEATURE_PLATFORM_GATES: Partial<Record<ProductFeatureKey, PlatformGateRef>> = {
  'sales.customerReservations': { modules: ['customer_app', 'reservations'] },
  'sales.chat': { modules: ['customer_app'] },
  'catalog.nationalDrug': { features: ['national_drug_catalog'] },
  'customer.engagement': { modules: ['customer_app'] },
};

export const ADMIN_MODULE_PLATFORM_CODES: Partial<
  Record<
    | 'dashboard'
    | 'catalog'
    | 'inventory'
    | 'procurement'
    | 'sales'
    | 'rx'
    | 'connect'
    | 'clinic'
    | 'receivables'
    | 'customer'
    | 'reports'
    | 'system',
    string
  >
> = {
  sales: 'sales',
  rx: 'e_rx',
  connect: 'novixa_connect',
  clinic: 'clinic_emr_lite',
  procurement: 'procurement',
  inventory: 'inventory',
  receivables: 'sales',
  customer: 'sales',
  catalog: 'sales',
  reports: 'reports',
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
