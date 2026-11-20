export const UNKNOWN_TENANT_LABEL = 'Tenant chưa xác định';

/** Display label only. Session user first, then the tenant_code claim already on the access token. */
export function authenticatedTenantCode(
  user: { tenantCode?: string | null } | null | undefined,
  accessToken?: string | null,
): string {
  const fromUser = String(user?.tenantCode ?? '').trim().toUpperCase();
  if (fromUser) return fromUser;
  return tenantCodeFromAccessToken(accessToken);
}

export function authenticatedTenantLabel(
  user: { tenantCode?: string | null } | null | undefined,
  accessToken?: string | null,
): string {
  return authenticatedTenantCode(user, accessToken) || UNKNOWN_TENANT_LABEL;
}

function tenantCodeFromAccessToken(accessToken?: string | null): string {
  const segment = String(accessToken ?? '').split('.')[1] ?? '';
  if (!segment) return '';
  try {
    const json = JSON.parse(decodeBase64Url(segment)) as { tenant_code?: unknown; tenantCode?: unknown };
    return String(json.tenant_code ?? json.tenantCode ?? '').trim().toUpperCase();
  } catch {
    return '';
  }
}

function decodeBase64Url(segment: string): string {
  const pad = segment.replace(/-/g, '+').replace(/_/g, '/');
  const padded = pad + '='.repeat((4 - (pad.length % 4)) % 4);
  if (typeof atob !== 'function') return '';
  return atob(padded);
}
