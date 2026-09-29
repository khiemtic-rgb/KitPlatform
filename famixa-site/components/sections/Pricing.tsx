'use client';

import { Headphones, RefreshCw, Shield } from 'lucide-react';
import { Container } from '@/components/ui/Container';
import { ChapterBadge, PillCta } from '@/components/ui/ChapterChrome';
import { ui } from '@/lib/ui-strings';
import type { AppLocale } from '@/lib/cms/getLanding';
import type { LandingContent } from '@/content/landing';

type Props = {
  content: LandingContent['chapter8'];
  appUrl: string;
  locale: AppLocale;
};

const perkIcon = {
  shield: Shield,
  refresh: RefreshCw,
  support: Headphones,
} as const;

/** Chương 8 — mời bắt đầu / demo, không hiện bảng giá (gói còn ở /vi/goi/). */
export function Pricing({ content, appUrl, locale }: Props) {
  const perks = content.perks ?? [];
  const ctaHref = content.ctaHref || appUrl;

  return (
    <section className="bg-[#FBF8F1] pb-6 pt-0 md:pb-7" aria-label={ui(locale).pricingAria}>
      <Container>
        <article
          id={content.id}
          className="scroll-mt-24 rounded-[28px] bg-white px-5 py-8 text-center shadow-[0_10px_36px_rgba(16,59,43,0.06)] sm:rounded-[32px] sm:px-10 sm:py-10 md:rounded-[36px] md:px-14 md:py-12"
        >
          <div className="mx-auto flex max-w-xl flex-col items-center">
            <ChapterBadge>{content.eyebrow}</ChapterBadge>
            <h2 className="m-0 mt-3.5 font-extrabold tracking-[-0.03em] text-[#103B2B] text-[clamp(1.4rem,2.4vw,1.85rem)] leading-[1.22]">
              {content.title}
            </h2>
            <p className="mt-3 mb-0 text-[0.95rem] leading-[1.6] text-[#5E6A63]">{content.lead}</p>
            {content.cta ? (
              <div className="mt-6">
                <PillCta href={ctaHref} variant="dark">
                  {content.cta}
                </PillCta>
              </div>
            ) : null}
          </div>

          {perks.length ? (
            <ul className="m-0 mx-auto mt-8 grid max-w-3xl list-none grid-cols-1 gap-4 p-0 sm:grid-cols-3 sm:gap-5">
              {perks.map((perk) => {
                const Icon = perkIcon[perk.icon] ?? Shield;
                return (
                  <li key={perk.title} className="flex items-start gap-2.5 text-left sm:flex-col sm:items-center sm:text-center">
                    <span className="mt-0.5 grid h-9 w-9 shrink-0 place-items-center rounded-full bg-[#E8F6EE] text-[#1FA45A]">
                      <Icon className="h-4 w-4" strokeWidth={2.2} />
                    </span>
                    <div>
                      <strong className="block text-[0.84rem] font-extrabold text-[#1A2E28]">
                        {perk.title}
                      </strong>
                      <span className="mt-0.5 block text-[0.74rem] leading-snug text-[#7A8A80]">
                        {perk.hint}
                      </span>
                    </div>
                  </li>
                );
              })}
            </ul>
          ) : null}
        </article>
      </Container>
    </section>
  );
}
