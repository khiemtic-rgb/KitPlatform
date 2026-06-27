export const APP_BRAND = import.meta.env.VITE_APP_BRAND?.trim() || 'Novixa';
export const APP_PRODUCT = import.meta.env.VITE_APP_PRODUCT?.trim() || 'ERP Nhà thuốc';
export const DEFAULT_TENANT_CODE = import.meta.env.VITE_DEFAULT_TENANT_CODE?.trim() || '';

export const TENANT_CODE_STORAGE_KEY = 'novixa_tenant_code';
export const PLATFORM_KEY_STORAGE_KEY = 'novixa_platform_key';

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
