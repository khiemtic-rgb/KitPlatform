'use client';

import { Leaf } from 'lucide-react';
import { Container } from '@/components/ui/Container';
import { PillCta } from '@/components/ui/ChapterChrome';
import type { LandingContent } from '@/content/landing';

type Props = {
  content: LandingContent['finalCta'];
  brand: LandingContent['brand'];
  appUrl: string;
};

/** CTA cuối — khớp mẫu: panel chữ trái + scene campfire/Fami phải */
export function FinalCTA({ content, brand, appUrl }: Props) {
  const accent = content.titleAccent ?? '';
  const titleBase = accent ? content.title.replace(accent, '').trim() : content.title;
  const midMatch = titleBase.match(/^(.*?)\s+(là một|is a)\s*$/i);
  const titleLead = midMatch ? midMatch[1] : titleBase;
  const titleMid = midMatch ? midMatch[2] : '';

  return (
    <section id="final-cta" className="scroll-mt-24 bg-[#FBF8F1] pb-0 pt-0">
      <Container>
        <div className="relative overflow-hidden rounded-[28px] bg-[#FDFBF7] shadow-[0_12px_40px_rgba(16,59,43,0.1)] sm:rounded-[32px] md:rounded-[36px]">
          {/* Desktop: art ghim full-bleed mép phải/đáy card */}
          <div className="pointer-events-none absolute inset-0 right-0 left-auto z-0 hidden w-[56%] overflow-hidden md:block lg:w-[58%]">
            <img
              src={`${content.image.src}?v=8`}
              alt=""
              width={1536}
              height={1024}
              className="block h-full w-full min-h-full object-cover object-[55%_100%]"
              loading="lazy"
              aria-hidden
            />
            <div
              className="absolute inset-y-0 left-0 z-[1] w-[40%]"
              style={{
                background:
                  'linear-gradient(90deg, #FDFBF7 0%, rgba(253,251,247,0.88) 32%, rgba(253,251,247,0.35) 62%, transparent 100%)',
              }}
              aria-hidden
            />
          </div>

          <div className="relative z-[1] grid min-h-[360px] md:min-h-[420px] md:grid-cols-[minmax(280px,0.92fr)_minmax(0,1.18fr)] lg:min-h-[460px]">
            <div className="relative flex flex-col justify-center bg-[#FDFBF7] px-7 py-10 sm:px-10 md:bg-transparent md:px-11 md:py-12 lg:px-12">
              <div className="mb-5 flex flex-wrap items-center gap-x-2.5 gap-y-1.5">
                <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-[#1FA45A] text-white shadow-[0_4px_12px_rgba(31,164,90,0.28)] sm:h-11 sm:w-11">
                  <Leaf
                    className="h-[1.15rem] w-[1.15rem] sm:h-5 sm:w-5"
                    strokeWidth={2.4}
                    fill="currentColor"
                    aria-hidden
                  />
                </span>
                <strong className="text-[1.05rem] font-extrabold tracking-tight text-[#103B2B] sm:text-[1.12rem]">
                  {brand.name}
                </strong>
                <span className="hidden h-4 w-px shrink-0 bg-[#C5CFC8] sm:block" aria-hidden />
                <span className="text-[0.8rem] font-medium leading-snug text-[#5E6A63] sm:text-[0.84rem]">
                  {content.brandLine ?? brand.tagline}
                </span>
              </div>

              <h2 className="m-0 font-extrabold tracking-[-0.03em] text-[#103B2B] text-[clamp(1.65rem,2.8vw,2.35rem)] leading-[1.16]">
                <span className="block">{titleLead}</span>
                <span className="block">
                  {titleMid ? `${titleMid} ` : null}
                  <span className="text-[#1FA45A]">{accent}</span>
                </span>
              </h2>

              {content.lead ? (
                <p className="mt-3.5 mb-0 max-w-[21rem] whitespace-pre-line text-[0.95rem] font-medium leading-[1.55] text-[#3D4F46] md:text-[1.02rem]">
                  {content.lead}
                </p>
              ) : null}

              <div className="mt-7">
                <PillCta href={appUrl} variant="dark">
                  {content.cta}
                </PillCta>
              </div>
            </div>

            {/* Mobile: ảnh dưới; desktop: spacer (art absolute phía trên) */}
            <div
              className="relative min-h-[260px] overflow-hidden md:min-h-full"
              role="img"
              aria-label={content.image.alt}
            >
              <img
                src={`${content.image.src}?v=8`}
                alt=""
                width={1536}
                height={1024}
                className="block h-full w-full min-h-full object-cover object-[55%_100%] md:hidden"
                loading="lazy"
                aria-hidden
              />
            </div>
          </div>
        </div>
      </Container>
    </section>
  );
}
