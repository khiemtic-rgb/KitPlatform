import { loadStoredTenantCode, saveStoredTenantCode, TENANT_CODE_STORAGE_KEY } from '@/shared/config/app-brand';

/** '1' = dùng dịch vụ nhà thuốc; '0' = tạm ngắt trên thiết bị (soft unlink). */
export const PHARMACY_LINK_ENABLED_KEY = 'novixa_pharmacy_link_enabled';
/** '1' = liên kết do NV quầy cấp (không cho tạm ngắt trên app). */
export const PHARMACY_LINK_LOCKED_KEY = 'novixa_pharmacy_link_locked';
export const PHARMACY_LINK_CHANGED_EVENT = 'novixa-pharmacy-link-changed';

export type PharmacyLinkState = {
  /** Có thể dùng đơn quầy / chat / điểm / đặt trước. */
  linked: boolean;
  tenantCode: string;
  /** Đã từng có mã tenant nhưng user tạm ngắt trên thiết bị. */
  paused: boolean;
  /** Liên kết khóa sau OTP quầy — không cho «Tạm ngắt». */
  locked: boolean;
};

function readEnabledFlag(): boolean | null {
  if (typeof window === 'undefined') return null;
  const raw = window.localStorage.getItem(PHARMACY_LINK_ENABLED_KEY);
  if (raw === '0') return false;
  if (raw === '1') return true;
  return null;
}

export function isPharmacyLinkLocked(): boolean {
  if (typeof window === 'undefined') return false;
  return window.localStorage.getItem(PHARMACY_LINK_LOCKED_KEY) === '1';
}

function notifyPharmacyLinkChanged() {
  if (typeof window === 'undefined') return;
  window.dispatchEvent(new Event(PHARMACY_LINK_CHANGED_EVENT));
}

/**
 * Soft-gate: ưu tiên quan hệ server (`pharmacyRelation`).
 * `member` (OTP quầy / NV xác nhận): luôn linked, không cho tạm ngắt trên app.
 * `prospect`/`revoked` → chưa linked.
 * Legacy local lock vẫn giữ cho phiên cũ sau OTP quầy.
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

  // Member trên server = đã liên kết nhà thuốc; không soft-unlink trên thiết bị.
  if (relation === 'member' && tenantCode) {
    return { linked: true, tenantCode, paused: false, locked: true };
  }

  if (isPharmacyLinkLocked() && Boolean(tenantCode)) {
    return { linked: true, tenantCode, paused: false, locked: true };
  }

  if (relation) {
    return { linked: false, tenantCode, paused: false, locked: false };
  }

  // Legacy fallback: chỉ linked khi local flag bật rõ.
  const paused = Boolean(tenantCode) && enabled === false;
  const linked = Boolean(tenantCode) && enabled === true;
  return { linked, tenantCode, paused, locked: false };
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

/**
 * Sau OTP do nhân viên quầy cấp: bật liên kết và khóa — khách không tự tắt trên app.
 */
export function lockPharmacyLinkFromCounter(tenantCode: string): void {
  enablePharmacyLink(tenantCode);
  if (typeof window !== 'undefined') {
    window.localStorage.setItem(PHARMACY_LINK_LOCKED_KEY, '1');
  }
  notifyPharmacyLinkChanged();
}

export function clearPharmacyLinkLock(): void {
  if (typeof window !== 'undefined') {
    window.localStorage.removeItem(PHARMACY_LINK_LOCKED_KEY);
  }
  notifyPharmacyLinkChanged();
}

/** Lưu tenant nhưng không bật commerce (prospect). */
export function bindPharmacyTenantWithoutLink(tenantCode: string): void {
  const code = tenantCode.trim().toUpperCase();
  if (code) saveStoredTenantCode(code);
  if (typeof window !== 'undefined') {
    window.localStorage.setItem(PHARMACY_LINK_ENABLED_KEY, '0');
    window.localStorage.removeItem(PHARMACY_LINK_LOCKED_KEY);
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
  const relation = (profile.pharmacyRelation ?? '').trim().toLowerCase();
  if (relation === 'member') {
    // Member = liên kết cứng trên app: bật + khóa, xoá trạng thái «tạm ngắt» cũ.
    lockPharmacyLinkFromCounter(code);
    return;
  }
  if (relation === 'prospect' || relation === 'revoked') {
    bindPharmacyTenantWithoutLink(code);
    return;
  }
  // Profile thiếu field (API cũ): không xoá lock / không ép tạm ngắt.
  if (code) saveStoredTenantCode(code);
}

/** Tạm ngắt dịch vụ nhà thuốc trên thiết bị. Trả false nếu bị khóa / là member. */
export function pausePharmacyLink(): boolean {
  if (isPharmacyLinkLocked()) return false;
  if (typeof window !== 'undefined') {
    window.localStorage.setItem(PHARMACY_LINK_ENABLED_KEY, '0');
  }
  notifyPharmacyLinkChanged();
  return true;
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
