import { loadStoredTenantCode, saveStoredTenantCode, TENANT_CODE_STORAGE_KEY } from '@/shared/config/app-brand';

/** '1' = dùng dịch vụ nhà thuốc; '0' = tạm ngắt trên thiết bị (soft unlink). */
export const PHARMACY_LINK_ENABLED_KEY = 'novixa_pharmacy_link_enabled';
export const PHARMACY_LINK_CHANGED_EVENT = 'novixa-pharmacy-link-changed';

export type PharmacyLinkState = {
  /** Có thể dùng đơn quầy / chat / điểm / đặt trước. */
  linked: boolean;
  tenantCode: string;
  /** Đã từng có mã tenant nhưng user tạm ngắt trên thiết bị. */
  paused: boolean;
};

function readEnabledFlag(): boolean | null {
  if (typeof window === 'undefined') return null;
  const raw = window.localStorage.getItem(PHARMACY_LINK_ENABLED_KEY);
  if (raw === '0') return false;
  if (raw === '1') return true;
  return null;
}

function notifyPharmacyLinkChanged() {
  if (typeof window === 'undefined') return;
  window.dispatchEvent(new Event(PHARMACY_LINK_CHANGED_EVENT));
}

/**
 * Soft-gate: ưu tiên quan hệ server (`pharmacyRelation`).
 * `member` + chưa pause local → linked; `prospect`/`revoked` → chưa linked.
 */
export function resolvePharmacyLinkState(
  profileTenantCode?: string | null,
  pharmacyRelation?: string | null,
): PharmacyLinkState {
  const fromProfile = profileTenantCode?.trim().toUpperCase() ?? '';
  const fromStorage = loadStoredTenantCode().trim().toUpperCase();
  const tenantCode = fromProfile || fromStorage;
  const enabled = readEnabledFlag();
  const relation = (pharmacyRelation ?? '').trim().toLowerCase();

  if (relation) {
    const serverMember = relation === 'member';
    const paused = serverMember && enabled === false;
    const linked = serverMember && enabled !== false;
    return { linked, tenantCode, paused };
  }

  // Legacy fallback (profile chưa có field): tenant + local flag.
  const paused = Boolean(tenantCode) && enabled === false;
  const linked = Boolean(tenantCode) && enabled !== false;
  return { linked, tenantCode, paused };
}

/** Sau OTP member / QR thành công: gắn tenant và bật dịch vụ partner. */
export function enablePharmacyLink(tenantCode: string): void {
  const code = tenantCode.trim().toUpperCase();
  if (code) saveStoredTenantCode(code);
  if (typeof window !== 'undefined') {
    window.localStorage.setItem(PHARMACY_LINK_ENABLED_KEY, '1');
  }
  notifyPharmacyLinkChanged();
}

/** Lưu tenant nhưng không bật commerce (prospect). */
export function bindPharmacyTenantWithoutLink(tenantCode: string): void {
  const code = tenantCode.trim().toUpperCase();
  if (code) saveStoredTenantCode(code);
  if (typeof window !== 'undefined') {
    window.localStorage.setItem(PHARMACY_LINK_ENABLED_KEY, '0');
  }
  notifyPharmacyLinkChanged();
}

/** Đồng bộ soft-gate từ profile sau login /me. */
export function syncPharmacyLinkFromProfile(profile: {
  tenantCode?: string | null;
  pharmacyRelation?: string | null;
}): void {
  const code = (profile.tenantCode ?? '').trim().toUpperCase();
  if (!code) return;
  if ((profile.pharmacyRelation ?? '').toLowerCase() === 'member') {
    enablePharmacyLink(code);
  } else {
    bindPharmacyTenantWithoutLink(code);
  }
}

/** Tạm ngắt dịch vụ nhà thuốc trên thiết bị (core app vẫn dùng được). */
export function pausePharmacyLink(): void {
  if (typeof window !== 'undefined') {
    window.localStorage.setItem(PHARMACY_LINK_ENABLED_KEY, '0');
  }
  notifyPharmacyLinkChanged();
}

export function resumePharmacyLink(): void {
  if (typeof window !== 'undefined') {
    window.localStorage.setItem(PHARMACY_LINK_ENABLED_KEY, '1');
  }
  notifyPharmacyLinkChanged();
}

/** Đường cần partner link (commerce / O2O). */
export const PHARMACY_LINK_REQUIRED_PATHS = [
  '/orders',
  '/chat',
  '/loyalty',
  '/reservations',
  '/receivables',
] as const;

export function pathRequiresPharmacyLink(pathname: string): boolean {
  return PHARMACY_LINK_REQUIRED_PATHS.some(
    (p) => pathname === p || pathname.startsWith(`${p}/`),
  );
}

export { TENANT_CODE_STORAGE_KEY };
