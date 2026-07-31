import type { ReactNode } from 'react';
import { getLandingContent } from '@/lib/cms/getLanding';
import { Navbar } from '@/components/sections/Navbar';
import { Footer } from '@/components/sections/Footer';
import { LocaleHtmlLang } from '@/components/LocaleHtmlLang';
import { Container } from '@/components/ui/Container';
import { ChapterBadge } from '@/components/ui/ChapterChrome';

type Props = {
  eyebrow?: string;
  title: string;
  lead?: string;
  children: ReactNode;
};

export async function BlogShell({ eyebrow = 'GÓC CHA MẸ', title, lead, children }: Props) {
  const content = await getLandingContent('vi');

  return (
    <>
      <LocaleHtmlLang locale="vi" />
      <Navbar content={content.nav} brand={content.brand} appUrl={content.appUrl} locale="vi" resolveHashLinks />
      <main className="bg-[#FBF8F1] pb-16 pt-10 md:pb-20 md:pt-12">
        <Container className="max-w-[980px]">
          <ChapterBadge>{eyebrow}</ChapterBadge>
          <h1 className="m-0 mt-3.5 font-extrabold tracking-[-0.03em] text-[#103B2B] text-[clamp(1.8rem,3.5vw,2.6rem)] leading-[1.16]">
            {title}
          </h1>
          {lead ? (
            <p className="mt-3 mb-0 max-w-[42rem] text-[1.05rem] leading-[1.65] text-[#5E6A63]">{lead}</p>
          ) : null}
          <div className="mt-8 md:mt-10">{children}</div>
        </Container>
      </main>
      <Footer content={content.footer} brand={content.brand} appUrl={content.appUrl} locale="vi" />
    </>
  );
}
