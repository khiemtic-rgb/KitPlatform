import type { PharmacyTenantConfig } from '../tenants/types';
import { resolveTenant, StorefrontNotFoundError } from './tenant';

type AstroLike = {
  request: Request;
  redirect: (path: string, status?: number) => Response;
};

/** Load tenant or redirect to /404 for unpublished / unknown hosts. */
export async function loadTenant(astro: AstroLike): Promise<PharmacyTenantConfig | Response> {
  try {
    return await resolveTenant(astro.request);
  } catch (err) {
    if (err instanceof StorefrontNotFoundError) {
      return astro.redirect('/404', 404);
    }
    throw err;
  }
}
