'use client';

import { ArrowRight, Play, Sprout } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { getIcon } from '@/lib/icons';
import { ui } from '@/lib/ui-strings';
import type { AppLocale } from '@/lib/cms/getLanding';
import type { LandingContent } from '@/content/landing';

type Props = {
  content: LandingContent['hero'];
  appUrl: string;
  locale: AppLocale;
};

/** Hero banner — khớp mock 100% */
export function Hero({ content, appUrl, locale }: Props) {
  const t = ui(locale);
  return (
    <section
      id="hero"
      className="relative scroll-mt-24 overflow-hidden bg-[#FBF8F1] pb-6 pt-5 md:pb-7 md:pt-6 lg:pt-7"
    >
      <Container>
        <div className="grid items-center gap-8 lg:grid-cols-[minmax(0,0.92fr)_minmax(0,1.18fr)] lg:gap-6 xl:gap-8">
          {/* ——— Copy ——— */}
          <div className="relative z-20 w-full max-w-[32rem] text-left">
            <p className="m-0 inline-flex items-center gap-2 rounded-full border border-[#D7EBDD] bg-[#EAF6EE] px-3.5 py-1.5 text-[0.68rem] font-extrabold uppercase tracking-[0.08em] text-[#1FA45A]">
              <Sprout className="h-3.5 w-3.5 shrink-0" strokeWidth={2.25} />
              {content.badge}
            </p>

            <h1
              className="m-0 mt-5 font-extrabold tracking-[-0.035em] text-[#1D1D1F]"
              style={{ fontSize: 'clamp(2.1rem, 3.5vw, 3.15rem)', lineHeight: 1.15 }}
            >
              <span className="block">{content.titleLine1}</span>
              <span className="mt-1 block text-[#1FA45A]">{content.titleLine2}</span>
            </h1>

            <p
              className="mt-4 mb-0 text-[#6B7280] md:mt-5"
              style={{ maxWidth: '28rem', fontSize: '1.05rem', lineHeight: 1.65 }}
            >
              {content.lead}
            </p>

            <div className="mt-7 flex flex-wrap items-center gap-3">
              <Button
                href={appUrl}
                className="!min-h-[50px] !rounded-full !bg-[#103B2B] !px-6 !text-[0.95rem] !font-bold !text-white hover:!bg-[#0c2f22]"
              >
                {content.primaryCta}
                <ArrowRight className="h-4 w-4" strokeWidth={2.5} />
              </Button>
              {content.secondaryCta && content.secondaryHref ? (
                <Button
                  href={content.secondaryHref}
                  variant="ghost"
                  className="!min-h-[50px] !rounded-full !border-[#D5D8D4] !bg-white !px-5 !text-[0.95rem] !font-bold !text-[#1D1D1F] !shadow-[0_2px_10px_rgba(16,59,43,0.04)]"
                >
                  <span className="grid h-7 w-7 place-items-center rounded-full border-[1.5px] border-[#1FA45A] text-[#1FA45A]">
                    <Play className="h-3 w-3 fill-current" />
                  </span>
                  {content.secondaryCta}
                </Button>
              ) : null}
            </div>
          </div>

          {/* ——— Art ——— */}
          <div className="relative z-10 min-w-0 self-center">
            <div className="relative w-full [container-type:inline-size]">
              <img
                src={`${content.image.src}?v=happy-bubbles-5`}
                alt={content.image.alt}
                width={1536}
                height={1024}
                className="block h-auto w-full object-cover object-center"
                style={{
                  WebkitMaskImage:
                    'linear-gradient(90deg, transparent 0%, rgba(0,0,0,.45) 8%, #000 20%)',
                  maskImage:
                    'linear-gradient(90deg, transparent 0%, rgba(0,0,0,.45) 8%, #000 20%)',
                }}
                fetchPriority="high"
              />
              {/*
                Ô thoại HTML theo locale — đè chữ baked trong PNG (VI cố định trong ảnh).
              */}
              {(content.bubbles ?? []).map((b) => (
                <div
                  key={`${b.left}-${b.top}-${locale}`}
                  className="absolute z-[2] -translate-x-1/2"
                  style={{ left: b.left, top: b.top, width: b.width }}
                  title={t.bubbleCopyHint}
                >
                  <div
                    className={[
                      'relative flex w-full items-center justify-center rounded-[1.35rem] border border-[#EDE8DF] bg-white',
                      'px-[14%] py-[12%] text-center shadow-[0_6px_18px_rgba(61,43,31,0.12)]',
                      b.tail === 'bl'
                        ? 'after:absolute after:bottom-[-7px] after:left-[26%] after:h-3.5 after:w-3.5 after:rotate-45 after:rounded-[2px] after:border-b after:border-r after:border-[#EDE8DF] after:bg-white after:content-[""]'
                        : b.tail === 'br'
                          ? 'after:absolute after:bottom-[-7px] after:right-[26%] after:h-3.5 after:w-3.5 after:rotate-45 after:rounded-[2px] after:border-b after:border-r after:border-[#EDE8DF] after:bg-white after:content-[""]'
                          : 'after:absolute after:bottom-[-7px] after:left-1/2 after:h-3.5 after:w-3.5 after:-translate-x-1/2 after:rotate-45 after:rounded-[2px] after:border-b after:border-r after:border-[#EDE8DF] after:bg-white after:content-[""]',
                    ].join(' ')}
                  >
                    <p
                      className="relative z-[1] m-0 w-full select-text font-bold tracking-[-0.015em] text-[#3D2B1F]"
                      style={{
                        fontSize: 'clamp(0.68rem, 1.85cqw, 0.95rem)',
                        lineHeight: 1.25,
                        whiteSpace: 'pre-line',
                      }}
                    >
                      {b.text}
                    </p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* ——— Proof cards (full width under hero) ——— */}
        <div className="relative z-20 mt-8 grid grid-cols-2 gap-4 lg:mt-8 lg:grid-cols-4">
          {content.proof.map((p) => {
            const Icon = getIcon(p.icon);
            return (
              <div
                key={p.title}
                className="flex h-full min-h-[4.75rem] items-center gap-3 rounded-2xl border border-[#EEEDE8] bg-white px-4 py-3.5 shadow-[0_6px_20px_rgba(16,59,43,0.05)] sm:px-5 sm:py-4"
              >
                <span className="inline-grid h-10 w-10 shrink-0 place-items-center rounded-full bg-[#EAF6EE] text-[#1FA45A]">
                  <Icon className="h-[18px] w-[18px]" strokeWidth={2} />
                </span>
                <span className="min-w-0">
                  <strong className="block text-[0.95rem] font-extrabold leading-tight text-[#1D1D1F] sm:text-[1rem]">
                    {p.title}
                  </strong>
                  <span className="mt-0.5 block text-[0.78rem] leading-snug text-[#6B7280] sm:text-[0.82rem]">
                    {p.sub}
                  </span>
                </span>
              </div>
            );
          })}
        </div>
      </Container>
    </section>
  );
}
