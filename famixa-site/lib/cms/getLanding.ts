import type { LandingDocument } from '@/content/schema';
import landingVi from '@/content/landing.json';
import landingEn from '@/content/landing.en.json';

const byLocale: Record<LandingDocument['locale'], LandingDocument> = {
  vi: landingVi as LandingDocument,
  en: landingEn as LandingDocument,
};

/**
 * CMS loader — today: local JSON per locale.
 * Tomorrow: `fetch(CMS_URL)` / Sanity / Contentful — same return type.
 */
export async function getLandingContent(
  locale: LandingDocument['locale'] = 'vi',
): Promise<LandingDocument> {
  return byLocale[locale] ?? byLocale.vi;
}

/** Sync helper for static pages / tools */
export function getLandingContentSync(
  locale: LandingDocument['locale'] = 'vi',
): LandingDocument {
  return byLocale[locale] ?? byLocale.vi;
}

export const SUPPORTED_LOCALES = ['vi', 'en'] as const;
export type AppLocale = (typeof SUPPORTED_LOCALES)[number];
