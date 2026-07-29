'use client';

import {
  Check,
  Headphones,
  Leaf,
  RefreshCw,
  Shield,
  Sprout,
  TreeDeciduous,
  TreePine,
} from 'lucide-react';
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

const planIcon = {
  start: Sprout,
  peace: Leaf,
  growth: TreeDeciduous,
  thrive: TreePine,
} as const;

const cardTone: Record<
  LandingContent['chapter8']['plans'][number]['tone'],
  { card: string; title: string; btn: string; iconWrap: string; borderW: string }
> = {
  start: {
    card: 'bg-[#FBF3E6] border-transparent',
    title: 'text-[#9A7B4F]',
    btn: 'border-[#E4D2AE] bg-[#FBF3E6] text-[#8B6F47] hover:bg-[#F3E4C9]',
    iconWrap: 'bg-[#F3E4C9] text-[#9A7B4F]',
    borderW: 'border',
  },
  peace: {
    card: 'bg-white border-[#BFE6C9]',
    title: 'text-[#1FA45A]',
    btn: 'border-[#1FA45A] bg-transparent text-[#1A7A45] hover:bg-[#EAF6EE]',
    iconWrap: 'bg-[#DCEFE0] text-[#1FA45A]',
    borderW: 'border',
  },
  growth: {
    card: 'bg-white border-[#1FA45A] shadow-[0_14px_34px_rgba(31,164,90,0.16)]',
    title: 'text-[#1FA45A]',
    btn: 'border-[#1FA45A] bg-[#1FA45A] !text-white hover:bg-[#188A4B]',
    iconWrap: 'bg-[#DCEFE0] text-[#1FA45A]',
    borderW: 'border-2',
  },
  thrive: {
    card: 'bg-white border-[#BFE6C9]',
    title: 'text-[#1FA45A]',
    btn: 'border-[#103B2B] bg-[#103B2B] !text-white hover:bg-[#0C2E22]',
    iconWrap: 'bg-[#DCEFE0] text-[#16412C]',
    borderW: 'border',
  },
};

const perkIcon = {
  shield: Shield,
  refresh: RefreshCw,
  support: Headphones,
} as const;

/** Chương 8 — intro + perks trái, 4 gói rộng cân đối */
export function Pricing({ content, appUrl, locale }: Props) {
  const perks = content.perks ?? [];

  return (
    <section className="bg-[#FBF8F1] pb-6 pt-0 md:pb-7" aria-label={ui(locale).pricingAria}>
      <Container>
        <article
          id={content.id}
          className="scroll-mt-24 rounded-[28px] bg-white px-5 py-7 shadow-[0_10px_36px_rgba(16,59,43,0.06)] sm:rounded-[32px] sm:px-8 sm:py-9 md:rounded-[36px] md:px-10 md:py-10"
        >
          <div className="flex flex-col gap-7 xl:flex-row xl:items-stretch xl:gap-7">
            {/* Trái */}
            <div className="flex w-full shrink-0 flex-col justify-center xl:w-[220px]">
              <ChapterBadge>{content.eyebrow}</ChapterBadge>
              <h2 className="m-0 mt-3.5 font-extrabold tracking-[-0.03em] text-[#103B2B] text-[clamp(1.35rem,2vw,1.7rem)] leading-[1.22]">
                {content.title}
              </h2>
              <p className="mt-2.5 mb-0 text-[0.88rem] leading-[1.55] text-[#5E6A63]">{content.lead}</p>
              {content.cta ? (
                <div className="mt-5">
                  <PillCta href={content.ctaHref || '#pricing'} variant="ghost-white">
                    {content.cta}
                  </PillCta>
                </div>
              ) : null}

              {perks.length ? (
                <ul className="m-0 mt-6 list-none space-y-3.5 p-0">
                  {perks.map((perk) => {
                    const Icon = perkIcon[perk.icon] ?? Shield;
                    return (
                      <li key={perk.title} className="flex items-start gap-2.5">
                        <span className="mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-full bg-[#E8F6EE] text-[#1FA45A]">
                          <Icon className="h-4 w-4" strokeWidth={2.2} />
                        </span>
                        <div>
                          <strong className="block text-[0.82rem] font-extrabold text-[#1A2E28]">
                            {perk.title}
                          </strong>
                          <span className="mt-0.5 block text-[0.72rem] leading-snug text-[#7A8A80]">
                            {perk.hint}
                          </span>
                        </div>
                      </li>
                    );
                  })}
                </ul>
              ) : null}
            </div>

            {/* 4 gói — chiếm phần còn lại sau khi bỏ cột Fami */}
            <div className="grid min-w-0 flex-1 grid-cols-1 gap-3.5 sm:grid-cols-2 lg:grid-cols-4 lg:gap-3.5 xl:gap-4">
              {content.plans.map((p) => {
                const Icon = planIcon[p.tone] ?? Sprout;
                const tone = cardTone[p.tone];
                return (
                  <article
                    key={p.name}
                    className={`relative flex flex-col rounded-[20px] ${tone.borderW} px-4 py-5 text-center shadow-[0_4px_16px_rgba(16,59,43,0.04)] sm:px-4 sm:py-5 xl:px-5 ${tone.card}`}
                  >
                    {p.badge ? (
                      <span className="absolute -top-3 left-1/2 -translate-x-1/2 whitespace-nowrap rounded-full bg-[#0F241C] px-3 py-1 text-[0.62rem] font-extrabold uppercase tracking-wider text-white shadow-[0_4px_10px_rgba(16,59,43,0.25)]">
                        {p.badge}
                      </span>
                    ) : null}

                    <div className={`mx-auto mb-2.5 grid h-12 w-12 place-items-center rounded-full ${tone.iconWrap}`}>
                      <Icon className="h-6 w-6" strokeWidth={1.6} />
                    </div>

                    <h3 className={`m-0 text-[1.2rem] font-extrabold ${tone.title}`}>{p.name}</h3>
                    {p.tagline ? (
                      <p className="m-0 mt-1 text-[0.68rem] font-semibold leading-snug text-[#7A8A80]">
                        {p.tagline}
                      </p>
                    ) : null}

                    <div className="mt-2 text-[1rem] font-extrabold tracking-[-0.02em] text-[#1A2E28]">
                      {p.price}
                      <span className="ml-1 text-[0.74rem] font-bold text-[#6B7C72]">{p.period}</span>
                    </div>

                    <ul className="m-0 mt-3.5 flex flex-1 list-none flex-col gap-1.5 p-0 text-left">
                      {p.items.map((item) => (
                        <li
                          key={item}
                          className="flex items-start gap-1.5 text-[0.74rem] leading-snug text-[#3D4F46]"
                        >
                          <Check
                            className="mt-0.5 h-3.5 w-3.5 shrink-0 text-[#1FA45A]"
                            strokeWidth={2.6}
                          />
                          {item}
                        </li>
                      ))}
                    </ul>

                    <a
                      href={appUrl}
                      className={`mt-4 inline-flex min-h-[40px] items-center justify-center rounded-full border px-3.5 text-center text-[0.78rem] font-bold transition-colors ${tone.btn}`}
                    >
                      {p.cta}
                    </a>
                  </article>
                );
              })}
            </div>
          </div>
        </article>
      </Container>
    </section>
  );
}
