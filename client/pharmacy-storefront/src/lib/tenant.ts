import type { PharmacyTenantConfig } from '../tenants/types';
import { getTenantByHost, getTenantBySlug } from '../tenants/registry';

const DEFAULT_SLUG =
  (typeof import.meta.env.PUBLIC_STOREFRONT_TENANT === 'string' &&
    import.meta.env.PUBLIC_STOREFRONT_TENANT.trim()) ||
  'xuanhoa';

/**
 * Resolve white-label tenant from request Host (dev / future SSR),
 * falling back to PUBLIC_STOREFRONT_TENANT (default: xuanhoa).
 *
 * Static prerender cannot read Host; production multi-host routing
 * needs SSR (adapter) or per-tenant builds via PUBLIC_STOREFRONT_TENANT.
 */
export function resolveTenant(request: Request): PharmacyTenantConfig {
  if (import.meta.env.DEV) {
    const host = request.headers.get('host') ?? '';
    const byHost = host ? getTenantByHost(host) : undefined;
    if (byHost) return byHost;
  }

  const byEnv = getTenantBySlug(DEFAULT_SLUG);
  if (byEnv) return byEnv;

  const fallback = getTenantBySlug('xuanhoa');
  if (!fallback) {
    throw new Error('No pharmacy storefront tenants registered');
  }
  return fallback;
}