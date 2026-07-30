import type { ReactNode } from 'react';
import { getLandingContent, type AppLocale } from '@/lib/cms/getLanding';
import { specialtyLocaleHrefs, type SpecialtyKey } from '@/lib/specialty-routes';
import { Navbar } from '@/components/sections/Navbar';
import { Footer } from '@/components/sections/Footer';
import { LocaleHtmlLang } from '@/components/LocaleHtmlLang';
import { Container } from '@/components/ui/Container';
import { ChapterBadge } from '@/components/ui/ChapterChrome';

type Props = {
  locale: AppLocale;
  specialty: SpecialtyKey;
  eyebrow: string;
  title: string;
  lead?: string;
  children: ReactNode;
};

export async function SpecialtyShell({
  locale,
  specialty,
  eyebrow,
  title,
  lead,
  children,
}: Props) {
  const c = await getLandingContent(locale);
  const localeHrefs = specialtyLocaleHrefs(specialty);

  return (
    <>
      <LocaleHtmlLang locale={locale} />
      <Navbar
        content={c.nav}
        brand={c.brand}
        appUrl={c.appUrl}
        locale={locale}
        localeHrefs={localeHrefs}
        resolveHashLinks
      />
      <main className="bg-[#FBF8F1] pb-16 pt-10 md:pb-20 md:pt-12">
        <Container className="max-w-[920px]">
          <ChapterBadge>{eyebrow}</ChapterBadge>
          <h1 className="m-0 mt-3.5 font-extrabold tracking-[-0.03em] text-[#103B2B] text-[clamp(1.6rem,3.2vw,2.35rem)] leading-[1.2]">
            {title}
          </h1>
          {lead ? (
            <p className="mt-3 mb-0 max-w-[40rem] text-[1.05rem] leading-[1.6] text-[#5E6A63]">{lead}</p>
          ) : null}
          <div className="mt-8 md:mt-10">{children}</div>
        </Container>
      </main>
      <Footer content={c.footer} brand={c.brand} appUrl={c.appUrl} locale={locale} />
    </>
  );
}
