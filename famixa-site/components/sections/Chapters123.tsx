'use client';

import { ArrowRight, Leaf } from 'lucide-react';
import { Container } from '@/components/ui/Container';
import { ChapterBadge, LeafRule, PillCta } from '@/components/ui/ChapterChrome';
import { getIcon } from '@/lib/icons';
import { ui } from '@/lib/ui-strings';
import type { AppLocale } from '@/lib/cms/getLanding';
import type { LandingContent } from '@/content/landing';

type Props = {
  chapter1: LandingContent['chapter1'];
  chapter2: LandingContent['chapter2'];
  chapter3: LandingContent['chapter3'];
  locale: AppLocale;
};

type MomentTone = 'night' | 'study' | 'home' | 'weekend';

const titleCls =
  'm-0 mt-2.5 font-extrabold tracking-[-0.03em] text-[#103B2B] text-[clamp(1.55rem,2.4vw,2rem)] leading-[1.15]';
const leadCls = 'mb-0 text-[0.95rem] leading-[1.65] text-[#5E6A63]';

/** Badge góc ảnh — đúng màu mẫu */
const TAG_STYLES: Record<MomentTone, string> = {
  night: 'bg-[#1E2A44] text-white',
  study: 'bg-[#1FA45A] text-white',
  home: 'bg-[#E2872F] text-white',
  weekend: 'bg-[#E15A72] text-white',
};

/** Nền pastel phía trên thẻ moment — theo mẫu */
const PANEL_STYLES: Record<MomentTone, string> = {
  night: 'bg-[#E8F1FA]',
  study: 'bg-[#E8F6EE]',
  home: 'bg-[#F8EFE4]',
  weekend: 'bg-[#F9EBEF]',
};

/** Icon nổi giữa ảnh/body — night+study xanh lá theo mẫu */
const ICON_STYLES: Record<MomentTone, string> = {
  night: 'bg-[#1FA45A] text-white',
  study: 'bg-[#1FA45A] text-white',
  home: 'bg-[#E2872F] text-white',
  weekend: 'bg-[#E15A72] text-white',
};

/**
 * Ch1 + Ch2 cùng cao belt (~bottom 3.05rem): Ch1 U nông dưới ảnh → nhún lên lá;
 * Ch2 ngang qua tâm icon. Ảnh z cao hơn path để không bị đâm xuyên.
 */
function BridgePath({ side }: { side: 'left' | 'right' }) {
  if (side === 'left') {
    // U nông dưới ảnh: chấm dưới CTA, giữ thấp dưới Fami, nhún muộn lên belt (y=50)
    const d = 'M34 88 C 180 110, 350 116, 470 100 S 535 54, 560 50';
    return (
      <div
        data-ch12-path="left"
        className="pointer-events-none absolute bottom-[3.05rem] left-0 z-[1] hidden h-[120px] w-[calc(100%+1.75rem)] lg:block"
        aria-hidden
      >
        <svg viewBox="0 0 560 120" preserveAspectRatio="none" className="h-full w-full overflow-visible">
          <defs>
            <linearGradient id="ch12-glow-left" x1="0" y1="0" x2="1" y2="0">
              <stop offset="0%" stopColor="#A8E85A" />
              <stop offset="50%" stopColor="#DFFF7A" />
              <stop offset="100%" stopColor="#A8E85A" />
            </linearGradient>
            <filter id="ch12-blur-left" x="-8%" y="-180%" width="116%" height="460%">
              <feGaussianBlur stdDeviation="4" />
            </filter>
          </defs>
          <path
            d={d}
            fill="none"
            stroke="url(#ch12-glow-left)"
            strokeWidth="10"
            strokeLinecap="round"
            opacity="0.3"
            filter="url(#ch12-blur-left)"
          />
          <path
            d={d}
            fill="none"
            stroke="url(#ch12-glow-left)"
            strokeWidth="2.2"
            strokeLinecap="round"
          />
          <circle cx="34" cy="88" r="6" fill="#1FA45A" />
        </svg>
      </div>
    );
  }

  const d = 'M0 50 C 80 48, 180 54, 300 50 S 420 52, 520 50';
  return (
    <div
      data-ch12-path="right"
      className="pointer-events-none absolute inset-x-0 bottom-[3.05rem] z-[1] hidden h-[120px] lg:block"
      aria-hidden
    >
      <svg viewBox="0 0 560 120" preserveAspectRatio="none" className="h-full w-full overflow-visible">
        <defs>
          <linearGradient id="ch12-glow-right" x1="0" y1="0" x2="1" y2="0">
            <stop offset="0%" stopColor="#A8E85A" />
            <stop offset="50%" stopColor="#DFFF7A" />
            <stop offset="100%" stopColor="#A8E85A" />
          </linearGradient>
          <filter id="ch12-blur-right" x="-8%" y="-180%" width="116%" height="460%">
            <feGaussianBlur stdDeviation="4" />
          </filter>
        </defs>
        <path
          d={d}
          fill="none"
          stroke="url(#ch12-glow-right)"
          strokeWidth="10"
          strokeLinecap="round"
          opacity="0.3"
          filter="url(#ch12-blur-right)"
        />
        <path
          d={d}
          fill="none"
          stroke="url(#ch12-glow-right)"
          strokeWidth="2.2"
          strokeLinecap="round"
        />
        <circle cx="520" cy="50" r="6" fill="#1FA45A" />
      </svg>
    </div>
  );
}

function BridgeLeaf() {
  return (
    <span
      data-ch12-leaf
      className="pointer-events-none absolute left-1/2 z-[5] hidden h-11 w-11 -translate-x-1/2 place-items-center rounded-full bg-white shadow-[0_6px_18px_rgba(16,59,43,0.12)] ring-1 ring-[#E4F0E6] lg:grid"
      style={{ bottom: 'calc(3.05rem + 60px)' }}
      aria-hidden
    >
      <Leaf className="h-[18px] w-[18px] fill-[#1FA45A] text-[#1FA45A]" strokeWidth={2} />
    </span>
  );
}

/** Chương 1–3 — Ch1/Ch2 khớp mock ch1-2.png */
export function Chapters123({ chapter1, chapter2, chapter3, locale }: Props) {
  const t = ui(locale);
  return (
    <section
      className="relative overflow-x-clip bg-[#FBF8F1] pb-6 pt-0 md:pb-7"
      aria-label={t.storyAria}
    >
      {/* Lá mờ nền */}
      <div className="pointer-events-none absolute inset-0" aria-hidden>
        <div className="absolute -left-20 bottom-10 h-80 w-80 rounded-full bg-[#D7EEDC]/55 blur-3xl" />
        <div className="absolute -right-16 top-8 h-72 w-72 rounded-full bg-[#E4F3E8]/70 blur-3xl" />
        <Leaf className="absolute bottom-8 left-6 h-28 w-28 -rotate-12 text-[#C8E6CF]/40" strokeWidth={1} />
        <Leaf className="absolute right-10 top-16 h-24 w-24 rotate-[25deg] text-[#C8E6CF]/35" strokeWidth={1} />
      </div>

      <Container className="relative flex flex-col gap-6 md:gap-7">
        {/* Ch1 | Ch2 */}
        <div className="relative grid items-stretch gap-6 overflow-x-clip lg:grid-cols-2 lg:gap-6 lg:overflow-visible">
          <BridgeLeaf />

          {/* ——— Chương 1 ——— */}
          <article
            id={chapter1.id}
            className="relative scroll-mt-24 flex flex-col overflow-visible rounded-[28px] bg-white p-5 pb-8 shadow-[0_10px_36px_rgba(16,59,43,0.06)] sm:rounded-[32px] sm:p-6 sm:pb-9 md:rounded-[36px] md:p-7 md:pb-10 lg:p-7 lg:pb-10"
          >
            <BridgePath side="left" />
            <div className="relative grid flex-1 gap-4 pb-8 md:grid-cols-[minmax(0,1.1fr)_minmax(0,0.9fr)] md:items-end md:gap-5 md:pb-10">
              <div className="relative z-[3] flex flex-col items-start justify-end py-1 md:py-2">
                <ChapterBadge>{chapter1.eyebrow}</ChapterBadge>
                <h2 className={`${titleCls} max-w-[8em] whitespace-pre-line text-[#103B2B]`}>
                  {chapter1.title}
                </h2>
                <LeafRule />
                <p className={`${leadCls} max-w-[16rem]`}>{chapter1.lead}</p>
                <div className="mt-6">
                  <PillCta href={chapter1.ctaHref}>{chapter1.cta}</PillCta>
                </div>
              </div>

              <div className="relative z-[2] mb-0 overflow-hidden rounded-[22px] bg-[#EAF5EE] md:rounded-[24px]">
                <img
                  src={`${chapter1.image.src}?v=ch12align`}
                  alt={chapter1.image.alt}
                  width={640}
                  height={960}
                  className="aspect-[2/3] h-auto w-full object-cover object-[50%_34%]"
                  loading="lazy"
                />
              </div>
            </div>
          </article>

          {/* ——— Chương 2 ——— */}
          <article
            id={chapter2.id}
            className="relative flex flex-col overflow-hidden rounded-[28px] bg-white p-5 shadow-[0_10px_36px_rgba(16,59,43,0.06)] sm:rounded-[32px] sm:p-6 md:rounded-[36px] md:p-7 lg:p-7"
          >
            <BridgePath side="right" />
            <Leaf
              className="pointer-events-none absolute -right-2 top-4 z-[2] h-28 w-28 rotate-[28deg] text-[#D8E6D8]/40"
              strokeWidth={1}
              aria-hidden
            />
            <Leaf
              className="pointer-events-none absolute right-12 top-0 z-[2] h-16 w-16 -rotate-[18deg] text-[#D8E6D8]/35"
              strokeWidth={1}
              aria-hidden
            />

            <div className="relative z-[2] flex flex-col items-start">
              <ChapterBadge>{chapter2.eyebrow}</ChapterBadge>
              <h2 className={`${titleCls} max-w-[12em] whitespace-pre-line text-[#103B2B]`}>
                {chapter2.title}
              </h2>
              <LeafRule />
              <p className={`${leadCls} max-w-[26rem]`}>{chapter2.lead}</p>
            </div>

            <div className="relative z-[2] mt-auto flex items-start justify-between gap-1 pt-10 sm:gap-2">
              {chapter2.steps.map((s, i) => {
                const Icon = getIcon(s.icon);
                const last = i === chapter2.steps.length - 1;
                return (
                  <div key={s.title} className="relative flex flex-1 flex-col items-center text-center">
                    {!last ? (
                      <span
                        className="pointer-events-none absolute top-[22px] left-full z-0 hidden h-2 w-2 -translate-x-1/2 rounded-full bg-[#B8F06A] shadow-[0_0_10px_rgba(184,240,106,1)] sm:block"
                        aria-hidden
                      />
                    ) : null}
                    <span
                      data-ch2-icon
                      className="relative z-[1] inline-grid h-12 w-12 place-items-center rounded-full bg-white text-[#1A5C38] shadow-[0_6px_16px_rgba(16,59,43,0.1)] ring-1 ring-[#E6EEE8]"
                    >
                      <Icon className="h-5 w-5" strokeWidth={1.7} />
                    </span>
                    <span className="mt-2.5 text-[0.82rem] font-extrabold text-[#103B2B] sm:text-[0.88rem]">
                      {s.title}
                    </span>
                    {s.body ? (
                      <span className="mt-1 max-w-[7rem] text-[0.68rem] leading-snug text-[#7A8A80] sm:text-[0.72rem]">
                        {s.body}
                      </span>
                    ) : null}
                  </div>
                );
              })}
            </div>
          </article>
        </div>

        {/* Quote — vòng lá đặc + gạch ngang; chữ đậm không nghiêng */}
        {chapter1.bridgeQuote ? (
          <div
            data-ch12-quote
            className="relative mx-auto max-w-[48rem] px-4 text-center"
          >
            <div className="mx-auto mb-4 flex max-w-[11rem] items-center justify-center gap-2" aria-hidden>
              <span className="h-px flex-1 bg-[#C5D4C8]" />
              <span className="grid h-6 w-6 shrink-0 place-items-center rounded-full bg-[#1FA45A]">
                <Leaf className="h-3 w-3 fill-white text-white" strokeWidth={2.4} />
              </span>
              <span className="h-px flex-1 bg-[#C5D4C8]" />
            </div>
            <div className="relative mx-auto flex w-fit max-w-full items-start gap-1 px-1 sm:gap-1.5">
              <span
                className="mt-[-0.15em] select-none font-serif text-[2.5rem] leading-none text-[#C5E6CC] sm:text-[2.85rem]"
                aria-hidden
              >
                “
              </span>
              <p className="m-0 text-[0.95rem] font-bold leading-[1.55] text-[#103B2B] sm:text-[1.05rem]">
                {chapter1.bridgeQuote}
              </p>
              <span
                className="mt-[-0.15em] select-none font-serif text-[2.5rem] leading-none text-[#C5E6CC] sm:text-[2.85rem]"
                aria-hidden
              >
                ”
              </span>
            </div>
          </div>
        ) : null}

        {/* ——— Chương 3 ——— */}
        <article
          id={chapter3.id}
          className="relative scroll-mt-24 overflow-hidden rounded-[28px] bg-white p-6 shadow-[0_10px_36px_rgba(16,59,43,0.06)] sm:rounded-[32px] sm:p-7 md:rounded-[36px] md:p-9 lg:p-10"
        >
          <Leaf
            className="pointer-events-none absolute -bottom-8 -left-6 z-0 h-44 w-44 -rotate-12 text-[#D5EBD9]/35"
            strokeWidth={1}
            aria-hidden
          />
          <Leaf
            className="pointer-events-none absolute bottom-10 left-14 z-0 h-24 w-24 rotate-[22deg] text-[#D5EBD9]/25"
            strokeWidth={1}
            aria-hidden
          />

          <div className="relative z-[1] grid gap-8 lg:grid-cols-[minmax(220px,0.7fr)_minmax(0,2.3fr)] lg:items-stretch lg:gap-9">
            <div className="flex min-w-0 flex-col">
              <ChapterBadge>{chapter3.eyebrow}</ChapterBadge>
              <h2 className={`${titleCls} max-w-[11em]`}>{chapter3.title}</h2>
              <LeafRule variant="center" />
              <p className={`${leadCls} max-w-[17.5rem]`}>{chapter3.lead}</p>
              <div className="mt-8 lg:mt-auto lg:pt-12">
                <PillCta href={chapter3.ctaHref} variant="solid">
                  {chapter3.cta}
                </PillCta>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3 sm:gap-3.5 lg:grid-cols-4 lg:gap-3.5">
              {chapter3.moments.map((m) => {
                const Icon = getIcon(m.icon);
                const tagTone = TAG_STYLES[m.tone];
                const iconTone = ICON_STYLES[m.tone];
                const panelTone = PANEL_STYLES[m.tone];
                return (
                  <article
                    key={m.title}
                    className={`flex flex-col overflow-hidden rounded-[20px] shadow-[0_6px_20px_rgba(16,59,43,0.07)] ${panelTone}`}
                  >
                    <div className="relative px-2.5 pb-0 pt-2.5 sm:px-3 sm:pt-3">
                      <span
                        className={`relative z-[1] mb-2 inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-[0.62rem] font-bold shadow-[0_2px_8px_rgba(0,0,0,0.12)] ${tagTone}`}
                      >
                        <Icon className="h-3 w-3" strokeWidth={2.4} />
                        {m.tag}
                      </span>
                      <div className="relative aspect-[3/2] w-full overflow-hidden rounded-[14px] bg-white/40">
                        <img
                          src={`${m.image.src}?v=ch3fullbg`}
                          alt={m.image.alt}
                          className="absolute inset-0 object-cover object-[50%_35%]"
                          style={{ width: '100%', height: '100%' }}
                          loading="lazy"
                        />
                      </div>
                      <span
                        className={`absolute bottom-0 left-1/2 z-[2] grid h-10 w-10 -translate-x-1/2 translate-y-1/2 place-items-center rounded-full border-[2.5px] border-white shadow-[0_4px_12px_rgba(16,59,43,0.14)] sm:h-11 sm:w-11 ${iconTone}`}
                      >
                        <Icon className="h-4 w-4 sm:h-[18px] sm:w-[18px]" strokeWidth={2} />
                      </span>
                    </div>
                    <div className="flex flex-1 flex-col px-3 pb-4 pt-7 text-center sm:px-3.5 sm:pb-5 sm:pt-8">
                      <h3 className="m-0 text-[0.92rem] font-extrabold text-[#178A4C] sm:text-[0.98rem]">
                        {m.title}
                      </h3>
                      <p className="mt-1.5 mb-0 text-[0.75rem] leading-snug text-[#6B7670] sm:text-[0.8rem]">
                        {m.body}
                      </p>
                      <a
                        href={chapter3.ctaHref}
                        className="mt-auto inline-flex items-center justify-center gap-0.5 self-center pt-3.5 text-[0.78rem] font-bold text-[#1FA45A] transition-colors hover:text-[#103B2B]"
                      >
                        {chapter3.cta}
                        <ArrowRight className="h-3 w-3" strokeWidth={2.4} aria-hidden />
                      </a>
                    </div>
                  </article>
                );
              })}
            </div>
          </div>

          <div className="relative z-[1] mt-8 grid grid-cols-1 gap-5 pt-1 sm:grid-cols-2 md:mt-8 lg:grid-cols-4 lg:gap-5">
            {chapter3.trust.map((t) => {
              const Icon = getIcon(t.icon);
              return (
                <div key={t.title} className="flex items-start gap-3">
                  <Icon
                    className="mt-0.5 h-7 w-7 shrink-0 text-[#1FA45A]"
                    strokeWidth={1.65}
                    aria-hidden
                  />
                  <div className="min-w-0">
                    <p className="m-0 text-[0.86rem] font-extrabold leading-snug text-[#103B2B]">
                      {t.title}
                    </p>
                    <p className="m-0 mt-1 text-[0.75rem] leading-snug text-[#7A8A80]">{t.body}</p>
                  </div>
                </div>
              );
            })}
          </div>
        </article>
      </Container>
    </section>
  );
}
