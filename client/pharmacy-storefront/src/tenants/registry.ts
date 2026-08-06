import type { PharmacyTenantConfig } from './types';
import { xuanhoa } from './xuanhoa';

const tenants: PharmacyTenantConfig[] = [xuanhoa];

/** Hostname substring match → tenant (first match wins). */
const hostMatchers: { includes: string; tenant: PharmacyTenantConfig }[] = [
  { includes: 'xuanhoa', tenant: xuanhoa },
];

export function listTenants(): PharmacyTenantConfig[] {
  return [...tenants];
}

export function getTenantBySlug(slug: string): PharmacyTenantConfig | undefined {
  const normalized = slug.trim().toLowerCase();
  return tenants.find((t) => t.slug.toLowerCase() === normalized);
}

export function getTenantByHost(host: string): PharmacyTenantConfig | undefined {
  const h = host.trim().toLowerCase().split(':')[0] ?? '';
  const byHostList = tenants.find((t) =>
    t.hosts.some((candidate) => h === candidate.toLowerCase() || h.includes(candidate.toLowerCase())),
  );
  if (byHostList) return byHostList;

  for (const m of hostMatchers) {
    if (h.includes(m.includes)) return m.tenant;
  }
  return undefined;
}
