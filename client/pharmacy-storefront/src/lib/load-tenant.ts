import type { PharmacyTenantConfig } from '../tenants/types';
import { resolveTenant } from './tenant';

/** Load the build-time storefront tenant (static Pages). */
export function loadTenant(): PharmacyTenantConfig {
  return resolveTenant();
}
