'use client';

import { useCallback, useState } from 'react';
import { Quote, User } from 'lucide-react';
import { Container } from '@/components/ui/Container';
import { ChapterBadge, PillCta } from '@/components/ui/ChapterChrome';
import { ui } from '@/lib/ui-strings';
import type { AppLocale } from '@/lib/cms/getLanding';
import type { LandingContent } from '@/content/landing';

type Props = {
  content: LandingContent['chapter7'];
  locale: AppLocale;
};

type QuoteItem = LandingContent['chapter7']['quotes'][number];

function highlightText(text: string, phrases: string[] = []) {
  if (!phrases.length) return text;
  const sorted = [...phrases].sort((a, b) => b.length - a.length);
  const escaped = sorted.map((p) => p.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));
  const re = new RegExp(`(${escaped.join('|')})`, 'gi');
  const parts = text.split(re);
  return parts.map((part, i) => {
    const match = sorted.some((p) => p.toLowerCase() === part.toLowerCase());
    return match ? (
      <strong key={i} className="font-extrabold text-[#1FA45A]">
        {part}
      </strong>
    ) : (
      <span key={i}>{part}</span>
    );
  });
}

function QuoteCard({ q, starsAria }: { q: QuoteItem; starsAria: string }) {
  const stars = q.rating ?? 5;
  return (
    <article className="relative flex h-full w-full flex-col overflow-visible rounded-[20px] bg-white shadow-[0_8px_24px_rgba(16,59,43,0.07)]">
      <span
        className="pointer-events-none absolute -top-1 left-3.5 z-[2] text-[#1FA45A] drop-shadow-[0_2px_3px_rgba(16,59,43,0.15)]"
        aria-hidden
      >
        <Quote className="h-8 w-8" strokeWidth={0} fill="currentColor" />
      </span>
      <div className="relative aspect-[5/4] w-full shrink-0 overflow-hidden rounded-t-[20px] bg-[#F3F0E8]">
        <img
          src={`${q.image.src}?v=full1`}
          alt={q.image.alt}
          width={800}
          height={640}
          className="absolute inset-0 h-full w-full max-w-none object-cover object-center"
          loading="lazy"
        />
      </div>
      <div className="flex flex-1 flex-col overflow-hidden rounded-b-[20px] bg-white px-3.5 pb-3.5 pt-3">
        <p className="m-0 text-[0.78rem] font-medium leading-[1.45] text-[#2A3830] sm:text-[0.82rem]">
          “{highlightText(q.text, q.highlights)}”
        </p>
        <div className="mt-auto flex items-center gap-2.5 pt-3">
          <span className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-[#E8F6EE] text-[#1FA45A]">
            <User className="h-4 w-4" strokeWidth={2.2} />
          </span>
          <div className="min-w-0">
            <strong className="block text-[0.78rem] font-extrabold text-[#1A2E28]">{q.name}</strong>
            <span className="block text-[0.68rem] text-[#7A8A80]">{q.place}</span>
            <div className="mt-0.5 text-[0.65rem] tracking-[0.08em] text-[#E0B43A]" aria-label={starsAria}>
              {'★'.repeat(stars)}
            </div>
          </div>
        </div>
      </div>
    </article>
  );
}

/** Chương 7 — 3 thẻ đều + Fami đứng riêng (không nằm trong carousel ảnh) */
export function Testimonials({ content, locale }: Props) {
  const t = ui(locale);
  const accent = content.titleAccent ?? '';
  const titleBase = accent ? content.title.replace(accent, '').trim() : content.title;
  const quotes = content.quotes;
  const pageSize = 3;
  const pageCount = Math.max(1, Math.ceil(quotes.length / pageSize));
  const [page, setPage] = useState(0);

  const go = useCallback(
    (next: number) => {
      setPage(((next % pageCount) + pageCount) % pageCount);
    },
    [pageCount],
  );

  const visible = quotes.slice(page * pageSize, page * pageSize + pageSize);

  return (
    <section className="bg-[#FBF8F1] pb-6 pt-0 md:pb-7" aria-label={t.testimonialsAria}>
      <Container>
        <article
          id={content.id}
          className="scroll-mt-24 rounded-[28px] bg-white px-5 py-7 shadow-[0_10px_36px_rgba(16,59,43,0.06)] sm:rounded-[32px] sm:px-8 sm:py-9 md:rounded-[36px] md:px-10 md:py-10"
        >
          <div className="flex flex-col gap-8 lg:flex-row lg:items-center lg:gap-6 xl:gap-8">
            {/* Trái */}
            <div className="w-full shrink-0 lg:w-[220px] xl:w-[240px]">
              <ChapterBadge>{content.eyebrow}</ChapterBadge>
              <h2 className="m-0 mt-3.5 font-extrabold tracking-[-0.03em] text-[#103B2B] text-[clamp(1.4rem,2.15vw,1.8rem)] leading-[1.22]">
                {titleBase}{' '}
                <span className="text-[#1FA45A]">{accent}</span>
              </h2>
              <p className="mt-3 mb-0 text-[0.9rem] leading-[1.55] text-[#5E6A63]">{content.lead}</p>
              {content.cta ? (
                <div className="mt-6">
                  <PillCta href={content.ctaHref || '#pricing'} variant="ghost-white">
                    {content.cta}
                  </PillCta>
                </div>
              ) : null}
            </div>

            {/* Phải — chỉ quotes trong track; Fami ngoài track */}
            <div className="min-w-0 flex-1">
              <div className="flex items-stretch gap-3 sm:gap-3.5">
                <div className="min-w-0 flex-1">
                  <div
                    className="grid grid-cols-1 gap-3 overflow-visible pt-3 sm:grid-cols-3 sm:gap-3.5"
                    aria-live="polite"
                  >
                    {visible.map((q) => (
                      <div key={`${page}-${q.name}`} className="min-w-0 overflow-visible">
                        <QuoteCard q={q} starsAria={t.starsAria(q.rating ?? 5)} />
                      </div>
                    ))}
                  </div>

                  {pageCount > 1 ? (
                    <div className="mt-4 flex justify-center gap-1.5" role="tablist" aria-label={t.storiesPagesAria}>
                      {Array.from({ length: pageCount }, (_, i) => (
                        <button
                          key={i}
                          type="button"
                          role="tab"
                          aria-selected={i === page}
                          aria-label={t.pageN(i + 1)}
                          onClick={() => go(i)}
                          className={
                            i === page
                              ? 'h-1.5 w-5 rounded-full bg-[#1FA45A]'
                              : 'h-1.5 w-1.5 rounded-full bg-[#D0D8D2] transition-colors hover:bg-[#A8B5AE]'
                          }
                        />
                      ))}
                    </div>
                  ) : null}
                </div>

                {content.mascot ? (
                  <aside className="hidden w-[148px] shrink-0 flex-col items-center justify-center self-center sm:flex lg:w-[168px] xl:w-[180px]">
                    <img
                      src={`${content.mascot.src}?v=cut3`}
                      alt={content.mascot.alt}
                      width={400}
                      height={480}
                      className="h-auto w-[132px] max-w-none select-none object-contain drop-shadow-[0_12px_24px_rgba(16,59,43,0.16)] lg:w-[150px] xl:w-[164px]"
                      loading="lazy"
                    />
                    <strong className="mt-2.5 text-[1.15rem] font-extrabold text-[#1FA45A] lg:text-[1.25rem]">
                      {content.mascotLabel ?? 'Fami'}
                    </strong>
                    <span className="mt-1 px-1 text-center text-[0.7rem] font-medium leading-snug text-[#7A8A80] lg:text-[0.74rem]">
                      {content.mascotTagline ?? content.lead}
                    </span>
                  </aside>
                ) : null}
              </div>
            </div>
          </div>
        </article>
      </Container>
    </section>
  );
}
