import type { PharmacyTenantConfig } from '../tenants/types';
import { createCoreBrandSafeSeed } from '../tenants/core-brand-safe-seed';
import { createTemplateSeed } from '../tenants/template-seed';
import { xuanhoa } from '../tenants/xuanhoa';
import { deepMerge } from './deep-merge';

export type PublicStorefrontPayload = {
  slug: string;
  tenantCode: string;
  tenantName: string;
  content: Record<string, unknown>;
  /** Draft preview — still uses brand-safe base for non-xuanhoa. */
  isPreview?: boolean;
};

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === 'object' && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

/**
 * Map published (or preview) API profile → PharmacyTenantConfig.
 * Slug `xuanhoa` keeps the full Xuân Hòa template overlay (pilot dual-run).
 * Other tenants use a brand-safe Core base so pilot copy/media cannot leak.
 */
export function mapPublicContentToTenant(payload: PublicStorefrontPayload): PharmacyTenantConfig {
  const slugHint = (payload.slug || 'pharmacy').toLowerCase();
  const isXuanHoaPilot = slugHint === 'xuanhoa';
  const base = isXuanHoaPilot ? createTemplateSeed() : createCoreBrandSafeSeed();
  const merged = deepMerge(base, payload.content) as PharmacyTenantConfig;

  const slug = slugHint;
  const brand = asRecord(merged.brand);
  const name =
    (typeof brand.name === 'string' && brand.name.trim()) ||
    payload.tenantName ||
    'Nhà thuốc';

  merged.id = slug;
  merged.slug = slug;
  merged.tenantCode = payload.tenantCode || merged.tenantCode || '';
  merged.hosts = [`${slug}.novixa.vn`, `${slug}.localhost`, slug];
  merged.brand = {
    ...merged.brand,
    name,
    shortName:
      (typeof brand.shortName === 'string' && brand.shortName.trim()) ||
      name,
  };

  if (!Array.isArray(payload.content.nav)) {
    merged.nav = createTemplateSeed().nav;
  }

  // Sync whyUs → trustBand when CMS sends whyUs but trustBand items empty-ish
  if (Array.isArray(merged.whyUs) && merged.whyUs.length > 0) {
    const tbItems = merged.trustBand?.items ?? [];
    const looksEmpty = tbItems.length === 0;
    if (looksEmpty) {
      const icons = ['badge', 'pharmacist', 'customers', 'hours'] as const;
      merged.trustBand = {
        ...merged.trustBand,
        title: merged.trustBand?.title || 'Vì sao chọn chúng tôi',
        items: merged.whyUs.map((label, i) => ({
          icon: icons[i % icons.length],
          label,
          value: label,
        })),
      };
    }
  }

  const pages = asRecord(merged.pages);
  const servicesPage = asRecord(pages.services);
  if (Array.isArray(servicesPage.featured) && servicesPage.featured.length > 0) {
    merged.services = servicesPage.featured as PharmacyTenantConfig['services'];
    // Defensive: ensure featured cards have bullets for /dich-vu
    const featured = servicesPage.featured.map((raw, index) => {
      const item = asRecord(raw);
      const bullets = Array.isArray(item.bullets)
        ? (item.bullets as string[]).filter((b) => typeof b === 'string' && b.trim())
        : [];
      const description = typeof item.description === 'string' ? item.description : '';
      return {
        id: typeof item.id === 'string' && item.id ? item.id : `svc-${index + 1}`,
        title: typeof item.title === 'string' ? item.title : '',
        description,
        icon: typeof item.icon === 'string' && item.icon ? item.icon : 'rx',
        bullets: bullets.length > 0 ? bullets : description ? [description] : ['Đặt trên App Novixa'],
        tone: (item.tone as 'green' | 'blue' | 'yellow' | 'purple' | undefined) ??
          (['green', 'blue', 'yellow', 'purple'] as const)[index % 4],
      };
    });
    merged.pages = {
      ...merged.pages,
      services: {
        ...merged.pages.services,
        featured,
      },
    };
  }

  if (!Array.isArray(merged.articles)) {
    merged.articles = [];
  }

  // CMS overlay with empty articles must not wipe the Xuân Hòa static knowledge set.
  if (isXuanHoaPilot && merged.articles.length === 0) {
    merged.articles = xuanhoa.articles;
  }

  return merged;
}
