import { getLandingContent, type AppLocale } from '@/lib/cms/getLanding';
import { LocaleHtmlLang } from '@/components/LocaleHtmlLang';
import { JsonLd } from '@/components/seo/JsonLd';
import { Navbar } from '@/components/sections/Navbar';
import { Hero } from '@/components/sections/Hero';
import { Chapters123 } from '@/components/sections/Chapters123';
import { Chapters45 } from '@/components/sections/Chapters45';
import { GrowthLoop } from '@/components/sections/GrowthLoop';
import { Testimonials } from '@/components/sections/Testimonials';
import { Pricing } from '@/components/sections/Pricing';
import { FAQ } from '@/components/sections/FAQ';
import { FinalCTA } from '@/components/sections/FinalCTA';
import { Footer } from '@/components/sections/Footer';
import type { Metadata } from 'next';
import { absoluteUrl } from '@/lib/site';

const ogLocale: Record<AppLocale, string> = {
  vi: 'vi_VN',
  en: 'en_US',
};

export async function buildLandingMetadata(locale: AppLocale): Promise<Metadata> {
  const c = await getLandingContent(locale);
  const path = `/${locale}/`;
  return {
    title: c.seo.title,
    description: c.seo.description,
    keywords: c.seo.keywords,
    openGraph: {
      title: c.seo.title,
      description: c.seo.description,
      url: absoluteUrl(path),
      images: [absoluteUrl(c.seo.ogImage)],
      locale: ogLocale[locale],
      alternateLocale: locale === 'vi' ? ['en_US'] : ['vi_VN'],
      type: 'website',
    },
    alternates: {
      canonical: absoluteUrl(path),
      languages: {
        vi: absoluteUrl('/vi/'),
        en: absoluteUrl('/en/'),
        'x-default': absoluteUrl('/vi/'),
      },
    },
  };
}

export async function LandingPage({ locale }: { locale: AppLocale }) {
  const c = await getLandingContent(locale);

  return (
    <>
      <LocaleHtmlLang locale={locale} />
      <JsonLd content={c} locale={locale} />
      <Navbar content={c.nav} brand={c.brand} appUrl={c.appUrl} locale={locale} />
      <main>
        <Hero content={c.hero} appUrl={c.appUrl} locale={locale} />
        <Chapters123
          chapter1={c.chapter1}
          chapter2={c.chapter2}
          chapter3={c.chapter3}
          locale={locale}
        />
        <Chapters45
          chapter4={c.chapter4}
          chapter5={c.chapter5}
          appUrl={c.appUrl}
          locale={locale}
        />
        <GrowthLoop content={c.chapter6} locale={locale} />
        <Testimonials content={c.chapter7} locale={locale} />
        <Pricing content={c.chapter8} appUrl={c.appUrl} locale={locale} />
        <FAQ content={c.faq} locale={locale} />
        <FinalCTA content={c.finalCta} brand={c.brand} appUrl={c.appUrl} />
      </main>
      <Footer content={c.footer} brand={c.brand} appUrl={c.appUrl} locale={locale} />
    </>
  );
}
