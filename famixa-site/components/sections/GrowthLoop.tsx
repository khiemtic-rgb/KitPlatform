'use client';

import { Container } from '@/components/ui/Container';
import { ChapterBadge, PillCta } from '@/components/ui/ChapterChrome';
import { ui } from '@/lib/ui-strings';
import type { AppLocale } from '@/lib/cms/getLanding';
import type { LandingContent } from '@/content/landing';

type Props = {
  content: LandingContent['chapter6'];
  locale: AppLocale;
};

const STEP_ICONS = [
  '/images/loop/01-observe.png',
  '/images/loop/02-heart.png',
  '/images/loop/03-sprout.png',
  '/images/loop/04-family.png',
  '/images/loop/05-chart.png',
] as const;

/**
 * Desktop mock — đúng 3 đường trên cụm compact.
 * Đo @758×263 → viewBox 760×264 (khớp tỉ lệ, không bẹp cung).
 * Tâm: 01=58 02=186 Fami=319 03=451 04=580 05=708 · badge≈65 · bottom≈209
 */
function LoopArrows() {
  return (
    <svg
      className="pointer-events-none absolute inset-0 z-0 h-full w-full overflow-visible"
      viewBox="0 0 760 264"
      fill="none"
      preserveAspectRatio="none"
      aria-hidden
    >
      <defs>
        <marker id="ch6-arrow" markerWidth="9" markerHeight="9" refX="7" refY="4.5" orient="auto">
          <path d="M0 0l9 4.5L0 9z" fill="#1FA45A" />
        </marker>
      </defs>

      {/* 1. trên 01 → vồng cao qua 02 → ↓ cạnh Fami (không đè lá) */}
      <path
        d="M58 65 C 155 -8, 245 -10, 300 88"
        stroke="#1FA45A"
        strokeWidth="1.8"
        strokeDasharray="4.5 5.5"
        strokeLinecap="round"
        markerEnd="url(#ch6-arrow)"
      />

      {/* 2. Fami → vồng cao qua 03–04 → ↓ 05 */}
      <path
        d="M338 88 C 450 -10, 600 -8, 708 65"
        stroke="#1FA45A"
        strokeWidth="1.8"
        strokeDasharray="4.5 5.5"
        strokeLinecap="round"
        markerEnd="url(#ch6-arrow)"
      />

      {/* 3. dưới 05 → cung dưới → ↑ 01 */}
      <path
        d="M708 220 C 530 246, 230 246, 58 220 L 58 208"
        stroke="#1FA45A"
        strokeWidth="1.8"
        strokeDasharray="4.5 5.5"
        strokeLinecap="round"
        markerEnd="url(#ch6-arrow)"
      />
    </svg>
  );
}

function StepCard({
  index,
  title,
  hint,
}: {
  index: number;
  title: string;
  hint: string;
}) {
  const num = String(index + 1).padStart(2, '0');
  return (
    <div className="relative flex h-[160px] w-[132px] flex-col items-center overflow-visible rounded-[16px] bg-white px-1.5 pb-2.5 pt-7 text-center shadow-[0_5px_16px_rgba(16,59,43,0.08)] sm:h-[164px] sm:w-[136px] sm:rounded-[18px]">
      <span className="absolute -top-3 left-1/2 z-[1] grid h-7 w-7 -translate-x-1/2 place-items-center rounded-full bg-[#1FA45A] text-[0.68rem] font-extrabold leading-none text-white sm:h-8 sm:w-8 sm:text-[0.72rem]">
        {num}
      </span>
      <span className="grid h-20 w-20 shrink-0 place-items-center overflow-visible">
        <img
          src={`${STEP_ICONS[index]}?v=pad11`}
          alt=""
          width={80}
          height={80}
          className="h-20 w-20 max-w-none select-none object-contain"
          loading="lazy"
        />
      </span>
      <strong className="mt-1.5 block whitespace-nowrap text-[0.68rem] font-extrabold leading-none tracking-tight text-[#1A2E28] sm:text-[0.72rem]">
        {title}
      </strong>
      <span className="mt-1 block whitespace-nowrap text-[0.56rem] font-medium leading-none tracking-tight text-[#7A8A80] sm:text-[0.6rem]">
        {hint}
      </span>
    </div>
  );
}

/** Chương 6 — HTML/CSS + SVG, khớp mẫu desktop */
export function GrowthLoop({ content, locale }: Props) {
  const accent = content.titleAccent ?? '';
  const titleBase = accent ? content.title.replace(accent, '').trim() : content.title;
  const steps = content.loop;

  return (
    <section className="bg-[#FBF8F1] pb-6 pt-0 md:pb-7" aria-label={ui(locale).growthLoopAria}>
      <Container>
        <article
          id={content.id}
          className="scroll-mt-24 overflow-visible rounded-[28px] bg-white px-5 py-7 shadow-[0_10px_36px_rgba(16,59,43,0.06)] sm:rounded-[32px] sm:px-8 sm:py-9 md:rounded-[36px] md:px-9 md:py-10"
        >
          <div className="flex flex-col gap-7 lg:flex-row lg:items-center lg:gap-5 xl:gap-6">
            {/* Trái */}
            <div className="w-full shrink-0 lg:w-[200px] xl:w-[220px]">
              <ChapterBadge solid>{content.eyebrow}</ChapterBadge>
              <h2 className="m-0 mt-3.5 font-extrabold tracking-[-0.03em] text-[#103B2B] text-[clamp(1.4rem,2.15vw,1.8rem)] leading-[1.22]">
                {titleBase}{' '}
                <span className="text-[#1FA45A]">{accent}</span>
              </h2>
              <p className="mt-2.5 mb-0 text-[0.9rem] leading-[1.55] text-[#5E6A63]">{content.lead}</p>
              <div className="mt-5">
                <PillCta href={content.ctaHref} variant="solid">
                  {content.cta}
                </PillCta>
              </div>
            </div>

            {/* Phải — lệch trái một chút so với mép phải card */}
            <div className="min-w-0 flex-1 overflow-visible lg:flex lg:justify-start lg:pl-0 lg:pr-2 xl:pr-4">
              <div
                className="relative mx-auto w-fit max-w-full overflow-visible px-1 pb-11 pt-[5.75rem] sm:pb-12 sm:pt-24 lg:mx-0 lg:-translate-x-3 xl:-translate-x-5"
                data-ch6-loop
              >
                <LoopArrows />

                <ol className="relative z-[1] m-0 flex list-none items-center gap-1.5 overflow-visible p-0 sm:gap-2 md:gap-2">
                  {steps.slice(0, 2).map((step, i) => (
                    <li key={step.title} className="shrink-0">
                      <StepCard index={i} title={step.title} hint={step.hint} />
                    </li>
                  ))}

                  <li className="relative flex w-[112px] shrink-0 flex-col items-center justify-center overflow-visible sm:w-[116px]">
                    <div
                      className="pointer-events-none absolute bottom-8 left-1/2 h-16 w-16 -translate-x-1/2 rounded-full bg-[#F0E68C]/45 blur-xl"
                      aria-hidden
                    />
                    <img
                      src={`${content.mascot.src}?v=pad10`}
                      alt={content.mascot.alt}
                      width={160}
                      height={180}
                      className="relative z-[2] -mt-7 h-auto w-[100px] max-w-none select-none object-contain drop-shadow-[0_8px_18px_rgba(180,160,40,0.25)] sm:-mt-8 sm:w-[108px]"
                      loading="lazy"
                    />
                    <strong className="relative z-[2] mt-0.5 whitespace-nowrap text-[1.05rem] font-extrabold tracking-tight text-[#1FA45A] sm:text-[1.15rem]">
                      {content.mascotLabel}
                    </strong>
                    <span className="relative z-[2] whitespace-nowrap text-center text-[0.64rem] font-medium leading-none text-[#8A9590] sm:text-[0.7rem]">
                      {content.mascotTagline}
                    </span>
                  </li>

                  {steps.slice(2).map((step, i) => (
                    <li key={step.title} className="shrink-0">
                      <StepCard index={i + 2} title={step.title} hint={step.hint} />
                    </li>
                  ))}
                </ol>
              </div>
            </div>
          </div>
        </article>
      </Container>
    </section>
  );
}
