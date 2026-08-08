import type { PharmacyTenantConfig } from '../tenants/types';
import { getTenantBySlug } from '../tenants/registry';
import { xuanhoa } from '../tenants/xuanhoa';

const DEFAULT_SLUG =
  (typeof import.meta.env.PUBLIC_STOREFRONT_TENANT === 'string' &&
    import.meta.env.PUBLIC_STOREFRONT_TENANT.trim()) ||
  'xuanhoa';

/**
 * Static Pages build: content is baked at build time.
 * Pilot dual-run uses the Xuân Hòa registry tenant (PUBLIC_STOREFRONT_TENANT).
 * Host-based multi-tenant SSR requires Cloudflare Workers (not Pages + Astro 7).
 */
export function resolveTenant(_request?: Request): PharmacyTenantConfig {
  return getTenantBySlug(DEFAULT_SLUG) ?? xuanhoa;
}
