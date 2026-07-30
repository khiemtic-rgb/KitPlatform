import { absoluteUrl } from '@/lib/site';
import { specialtyLocaleHrefs, type SpecialtyKey } from '@/lib/specialty-routes';
import { getSpecialtyContent } from '@/content/specialty';
import type { AppLocale } from '@/lib/cms/getLanding';
import type { Metadata } from 'next';

export function buildSpecialtyMetadata(locale: AppLocale, key: SpecialtyKey): Metadata {
  const pageKey =
    key === 'plans'
      ? 'plans'
      : key === 'stories'
        ? 'stories'
        : key === 'about'
          ? 'about'
          : key === 'privacy'
            ? 'privacy'
            : 'terms';
  const seo = getSpecialtyContent(locale)[pageKey].seo;
  const alts = specialtyLocaleHrefs(key);
  const path = alts[locale];

  return {
    title: seo.title,
    description: seo.description,
    alternates: {
      canonical: absoluteUrl(path),
      languages: {
        vi: absoluteUrl(alts.vi),
        en: absoluteUrl(alts.en),
        'x-default': absoluteUrl(alts.vi),
      },
    },
    openGraph: {
      title: seo.title,
      description: seo.description,
      url: absoluteUrl(path),
      locale: locale === 'vi' ? 'vi_VN' : 'en_US',
      type: 'website',
    },
  };
}
