export const APP_BRAND = import.meta.env.VITE_APP_BRAND?.trim() || 'Novixa';
export const DEFAULT_TENANT_CODE =
  import.meta.env.VITE_DEFAULT_TENANT_CODE?.trim() || (import.meta.env.DEV ? 'DEMO_PHARMACY' : '');

export const TENANT_CODE_STORAGE_KEY = 'novixa_customer_tenant_code';

export function loadStoredTenantCode(): string {
  if (typeof window === 'undefined') return DEFAULT_TENANT_CODE;
  return window.localStorage.getItem(TENANT_CODE_STORAGE_KEY)?.trim() || DEFAULT_TENANT_CODE;
}

export function saveStoredTenantCode(code: string): void {
  const trimmed = code.trim().toUpperCase();
  if (!trimmed) {
    window.localStorage.removeItem(TENANT_CODE_STORAGE_KEY);
    return;
  }
  window.localStorage.setItem(TENANT_CODE_STORAGE_KEY, trimmed);
}
