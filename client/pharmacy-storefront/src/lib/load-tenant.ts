import type { PharmacyTenantConfig } from '../tenants/types';
import { mergeKitKnowledge } from './kit-knowledge';
import { resolveTenant, StorefrontNotFoundError } from './tenant';

type AstroLike = {
  request: Request;
  url: URL;
  redirect: (path: string, status?: number) => Response;
  rewrite?: (path: string) => Response | Promise<Response>;
};

/** Load tenant or soft-404 for unpublished / unknown hosts. */
export async function loadTenant(astro: AstroLike): Promise<PharmacyTenantConfig | Response> {
  try {
    const tenant = await resolveTenant(astro.request, astro.url);
    return await mergeKitKnowledge(tenant);
  } catch (err) {
    const isNotFound =
      err instanceof StorefrontNotFoundError ||
      (err instanceof Error && err.name === 'StorefrontNotFoundError');
    if (isNotFound) {
      if (typeof astro.rewrite === 'function') {
        return astro.rewrite('/404');
      }
      return astro.redirect('/404');
    }
    throw err;
  }
}
