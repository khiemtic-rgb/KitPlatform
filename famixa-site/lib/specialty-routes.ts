import type { AppLocale } from '@/lib/cms/getLanding';

/** P0 specialty routes — VI slug + EN slug */
export const SPECIALTY = {
  plans: { vi: '/vi/goi/', en: '/en/plans/' },
  stories: { vi: '/vi/cau-chuyen/', en: '/en/stories/' },
  about: { vi: '/vi/ve-famixa/', en: '/en/about/' },
  privacy: { vi: '/vi/chinh-sach-bao-mat/', en: '/en/privacy/' },
  terms: { vi: '/vi/dieu-khoan/', en: '/en/terms/' },
} as const;

export type SpecialtyKey = keyof typeof SPECIALTY;

export function specialtyPath(key: SpecialtyKey, locale: AppLocale): string {
  return SPECIALTY[key][locale];
}

export function specialtyLocaleHrefs(key: SpecialtyKey): { vi: string; en: string } {
  return { vi: SPECIALTY[key].vi, en: SPECIALTY[key].en };
}

/** Resolve landing hash links when rendering chrome off the home page. */
export function resolveNavHref(href: string, locale: AppLocale): string {
  if (href.startsWith('#')) return `/${locale}/${href}`;
  if (href.startsWith('/')) return href;
  return href;
}
