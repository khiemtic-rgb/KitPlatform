export const APP_BRAND = import.meta.env.VITE_APP_BRAND?.trim() || 'Novixa';
export const APP_PRODUCT = import.meta.env.VITE_APP_PRODUCT?.trim() || 'ERP Nhà thuốc';

/** FamilyOS Starter — admin shell when tenant vertical = family. */
export const FAMILY_OS_BRAND = import.meta.env.VITE_FAMILY_OS_BRAND?.trim() || 'FamilyOS';
export const FAMILY_OS_PRODUCT =
  import.meta.env.VITE_FAMILY_OS_PRODUCT?.trim() || 'One Family. One Plan. One Daily Flow.';

/** KIT Marketing Park — admin shell when tenant vertical = marketing (org KIT_MKT). */
export const MARKETING_PARK_BRAND =
  import.meta.env.VITE_MARKETING_PARK_BRAND?.trim() || 'Marketing Park';
export const MARKETING_PARK_PRODUCT =
  import.meta.env.VITE_MARKETING_PARK_PRODUCT?.trim() || 'AI Content & Marketing Workspace';

export const DEFAULT_TENANT_CODE = import.meta.env.VITE_DEFAULT_TENANT_CODE?.trim() || '';

export const TENANT_CODE_STORAGE_KEY = 'novixa_tenant_code';
export const PLATFORM_KEY_STORAGE_KEY = 'novixa_platform_key';

export function resolveShellBrand(vertical: string | null | undefined): {
  brand: string;
  product: string;
  isFamily: boolean;
  isMarketing: boolean;
} {
  const value = String(vertical ?? '').trim().toLowerCase();
  if (value === 'family') {
    return { brand: FAMILY_OS_BRAND, product: FAMILY_OS_PRODUCT, isFamily: true, isMarketing: false };
  }
  if (value === 'marketing') {
    return {
      brand: MARKETING_PARK_BRAND,
      product: MARKETING_PARK_PRODUCT,
      isFamily: false,
      isMarketing: true,
    };
  }
  return { brand: APP_BRAND, product: APP_PRODUCT, isFamily: false, isMarketing: false };
}

/** Khi set VITE_DEFAULT_TENANT_CODE: ẩn ô mã (white-label 1 tenant). Multi-tenant: để trống. */
export function isTenantCodeLocked(): boolean {
  return DEFAULT_TENANT_CODE.length > 0;
}

export function loadStoredTenantCode(): string {
  if (typeof window === 'undefined') return DEFAULT_TENANT_CODE;
  return window.localStorage.getItem(TENANT_CODE_STORAGE_KEY)?.trim() || DEFAULT_TENANT_CODE;
}

export function saveStoredTenantCode(code: string): void {
  const trimmed = code.trim();
  if (!trimmed) {
    window.localStorage.removeItem(TENANT_CODE_STORAGE_KEY);
    return;
  }
  window.localStorage.setItem(TENANT_CODE_STORAGE_KEY, trimmed.toUpperCase());
}

export function loadStoredPlatformKey(): string {
  if (typeof window === 'undefined') return '';
  return window.sessionStorage.getItem(PLATFORM_KEY_STORAGE_KEY)?.trim() || '';
}

export function saveStoredPlatformKey(key: string): void {
  const trimmed = key.trim();
  if (!trimmed) {
    window.sessionStorage.removeItem(PLATFORM_KEY_STORAGE_KEY);
    return;
  }
  window.sessionStorage.setItem(PLATFORM_KEY_STORAGE_KEY, trimmed);
}
