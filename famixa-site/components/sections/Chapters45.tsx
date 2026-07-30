'use client';

import { Container } from '@/components/ui/Container';
import { ChapterBadge, PillCta } from '@/components/ui/ChapterChrome';
import { getIcon } from '@/lib/icons';
import { ui } from '@/lib/ui-strings';
import type { AppLocale } from '@/lib/cms/getLanding';
import type { LandingContent } from '@/content/landing';

type Props = {
  chapter4: LandingContent['chapter4'];
  chapter5: LandingContent['chapter5'];
  appUrl: string;
  locale: AppLocale;
};

/** Chương 4–5 */
const CH4_STAGE_CENTERS = [8.0, 24.2, 42.1, 65.6, 88.5];

export function Chapters45({ chapter4, chapter5, appUrl, locale }: Props) {
  const accent4 = chapter4.titleAccent;
  const titleLead4 =
    accent4 && chapter4.title.endsWith(accent4)
      ? chapter4.title.slice(0, -accent4.length).trim()
      : null;

  return (
    <section className="bg-[#FBF8F1] pb-6 pt-0 md:pb-7" aria-label={ui(locale).journeyAria}>
      <Container className="flex flex-col gap-6 md:gap-7">
        {/* ——— Chương 4 ——— */}
        <article
          id={chapter4.id}
          className="relative scroll-mt-24 rounded-[28px] bg-white px-5 py-8 shadow-[0_10px_36px_rgba(16,59,43,0.06)] sm:rounded-[32px] sm:px-8 sm:py-10 md:rounded-[36px] md:px-10 md:py-12"
        >
          <div className="relative z-[1] grid items-center gap-8 lg:grid-cols-[minmax(220px,0.3fr)_minmax(0,0.7fr)] lg:gap-8 xl:gap-10">
            {/* Left copy */}
            <div className="flex min-w-0 flex-col justify-center lg:pr-2">
              <ChapterBadge>{chapter4.eyebrow}</ChapterBadge>

              <h2 className="m-0 mt-3.5 max-w-[12.5em] font-extrabold tracking-[-0.03em] text-[#103B2B] text-[clamp(1.5rem,2.45vw,2rem)] leading-[1.16]">
                {titleLead4 && accent4 ? (
                  <>
                    <span className="block">{titleLead4}</span>
                    <span className="text-[#1FA45A]">{accent4}</span>
                  </>
                ) : (
                  chapter4.title
                )}
              </h2>

              <p className="mt-3.5 mb-0 max-w-[17rem] text-[0.95rem] leading-[1.6] text-[#6B7C72]">
                {chapter4.lead}
              </p>

              <div className="mt-8 lg:mt-9">
                <PillCta href={chapter4.ctaHref} variant="solid">
                  {chapter4.cta}
                </PillCta>
              </div>
            </div>

            {/* Right: 1 ảnh liền (không chồng cột) + chữ căn theo tâm từng bước */}
            <div className="relative min-w-0">
              <div
                className="pointer-events-none absolute inset-x-[8%] top-[28%] h-[36%] rounded-full bg-[#C8EFD6]/45 blur-[42px]"
                aria-hidden
              />
              <div
                className="pointer-events-none absolute inset-x-[22%] top-[18%] h-[42%] rounded-full bg-[#F3E7A8]/28 blur-[48px]"
                aria-hidden
              />

              <div className="relative z-[1]">
                <img
                  src={`${chapter4.image.src}?v=art1`}
                  alt={chapter4.image.alt}
                  width={1477}
                  height={630}
                  className="mx-auto block h-auto w-full object-contain object-bottom"
                  loading="lazy"
                />

                {/* Tâm mặt nhân vật trong art (đo pixel), không dùng lưới đều */}
                <ol className="relative m-0 mt-2 h-[5.75rem] list-none p-0 sm:mt-3 sm:h-[6.25rem]">
                  {chapter4.stages.map((s, i) => {
                    const Icon = getIcon(s.icon);
                    return (
                      <li
                        key={s.title}
                        className="absolute top-0 flex w-[19%] -translate-x-1/2 flex-col items-center px-0.5 text-center"
                        style={{ left: `${CH4_STAGE_CENTERS[i]}%` }}
                      >
                        <span className="grid h-7 w-7 shrink-0 place-items-center rounded-full bg-white shadow-[0_2px_8px_rgba(16,59,43,0.12)] ring-1 ring-[#D5E8DA]">
                          <Icon className="h-3.5 w-3.5 text-[#1FA45A] sm:h-4 sm:w-4" strokeWidth={2} />
                        </span>
                        <h3 className="m-0 mt-2 text-[0.7rem] font-extrabold leading-snug text-[#1FA45A] sm:text-[0.82rem] md:text-[0.88rem]">
                          {s.title}
                        </h3>
                        <p className="m-0 mt-0.5 max-w-[11em] text-[0.6rem] font-medium leading-[1.3] text-[#8A9A90] sm:text-[0.68rem]">
                          {s.hint.replace(/\n/g, ' ')}
                        </p>
                      </li>
                    );
                  })}
                </ol>
              </div>
            </div>
          </div>
        </article>

        {/* ——— Chương 5: chữ trái + cảnh devices/Fami phải ——— */}
        <article
          id={chapter5.id}
          className="relative scroll-mt-24 overflow-hidden rounded-[28px] border border-[#D5E2D4]/80 shadow-[0_10px_36px_rgba(16,59,43,0.08)] sm:rounded-[32px] md:rounded-[36px]"
        >
          {/* Nền scenic — desktop only (ẩn/hiện trên wrapper, không dùng lg:hidden trên img) */}
          <div className="absolute inset-0 hidden overflow-hidden lg:block" aria-hidden>
            <img
              src={`${chapter5.image.src}?v=17`}
              alt=""
              width={1024}
              height={441}
              className="absolute inset-0 h-full w-full object-cover object-[76%_48%]"
              loading="lazy"
            />
            <div className="pointer-events-none absolute inset-0 bg-gradient-to-r from-[#E8F2E4]/30 via-transparent via-[20%] to-transparent" />
          </div>

          <div className="relative grid min-h-0 lg:min-h-[440px] lg:grid-cols-[minmax(220px,0.34fr)_minmax(0,0.66fr)] xl:min-h-[480px]">
            {/* Mobile: ảnh trên — wrapper lg:hidden (globals img{display:block} ghi đè lg:hidden trên img) */}
            <div className="relative order-1 min-h-[210px] overflow-hidden sm:min-h-[240px] lg:hidden">
              <img
                src={`${chapter5.image.src}?v=17`}
                alt={chapter5.image.alt}
                width={1024}
                height={441}
                className="absolute inset-0 h-full w-full object-cover object-[82%_44%]"
                loading="lazy"
              />
              {chapter5.quote ? (
                <div className="absolute right-[5%] top-[10%] z-[2] flex min-h-[4.25rem] w-[10.5rem] items-center justify-center rounded-[1.15rem] bg-white px-3.5 pb-3.5 pt-2 text-center text-[0.66rem] font-medium leading-snug text-[#1A2E28] shadow-[0_6px_18px_rgba(16,59,43,0.14)] sm:min-h-[4.5rem] sm:w-[11rem] sm:text-[0.7rem]">
                  <span className="block max-w-[9.5em]">{chapter5.quote}</span>
                  <span
                    className="pointer-events-none absolute bottom-[-8px] left-[36%] h-4 w-4 rotate-45 bg-white"
                    aria-hidden
                  />
                </div>
              ) : null}
            </div>

            {/* Copy — mobile: nền solid dưới ảnh; desktop: panel mờ */}
            <div className="relative z-[1] order-2 flex min-w-0 flex-col justify-center bg-[#E8F2E4] px-6 py-8 sm:px-8 sm:py-10 lg:order-1 lg:max-w-[17.5rem] lg:rounded-[1.35rem] lg:bg-[#E8F2E4]/86 lg:px-9 lg:py-10 lg:shadow-[0_8px_28px_rgba(16,59,43,0.06)] lg:ring-1 lg:ring-white/75 xl:max-w-[18rem]">
              <ChapterBadge>{chapter5.eyebrow}</ChapterBadge>
              <h2 className="m-0 mt-3 max-w-[12.5em] font-extrabold tracking-[-0.03em] text-[#103B2B] text-[clamp(1.45rem,2.4vw,1.95rem)] leading-[1.18]">
                {chapter5.title}
              </h2>
              <p className="mt-3 mb-0 max-w-[18rem] text-[0.95rem] leading-[1.65] text-[#5E6A63]">
                {chapter5.lead}
              </p>
              <div className="mt-6">
                <PillCta href={appUrl} variant="ghost-white">
                  {chapter5.cta}
                </PillCta>
              </div>
            </div>

            {/* Desktop: cột phải trống — nền scenic từ absolute layer */}
            <div className="relative hidden lg:order-2 lg:block lg:min-h-full" aria-hidden />
          </div>

          {/* Bubble desktop */}
          {chapter5.quote ? (
            <div className="absolute left-[84%] top-[18%] z-[2] hidden min-h-[6rem] w-[13.75rem] -translate-x-1/2 items-center justify-center rounded-[1.25rem] bg-white px-5 pb-5 pt-3 text-center text-[0.78rem] font-medium leading-snug text-[#1A2E28] shadow-[0_6px_18px_rgba(16,59,43,0.14)] lg:flex">
              <span className="block max-w-[11em]">{chapter5.quote}</span>
              <span
                className="pointer-events-none absolute bottom-[-9px] left-[36%] h-5 w-5 rotate-45 bg-white"
                aria-hidden
              />
            </div>
          ) : null}
        </article>
      </Container>
    </section>
  );
}
