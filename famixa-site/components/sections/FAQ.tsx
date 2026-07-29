'use client';

import { useState } from 'react';
import { ChevronDown } from 'lucide-react';
import { Container } from '@/components/ui/Container';
import { ChapterBadge } from '@/components/ui/ChapterChrome';
import { ui } from '@/lib/ui-strings';
import type { AppLocale } from '@/lib/cms/getLanding';
import type { LandingContent } from '@/content/landing';

type Props = {
  content: LandingContent['faq'];
  locale: AppLocale;
};

/** FAQ — cùng nhịp shell với các chương cream */
export function FAQ({ content, locale }: Props) {
  const [open, setOpen] = useState<number | null>(0);

  return (
    <section className="bg-[#FBF8F1] pb-6 pt-0 md:pb-7" aria-label={ui(locale).faqAria}>
      <Container>
        <article
          id={content.id}
          className="scroll-mt-24 rounded-[28px] bg-white px-5 py-7 shadow-[0_10px_36px_rgba(16,59,43,0.06)] sm:rounded-[32px] sm:px-8 sm:py-9 md:rounded-[36px] md:px-10 md:py-10"
        >
          <div className="mx-auto max-w-2xl text-center">
            <div className="flex justify-center">
              <ChapterBadge>{content.eyebrow}</ChapterBadge>
            </div>
            <h2 className="m-0 mt-3.5 font-extrabold tracking-[-0.03em] text-[#103B2B] text-[clamp(1.4rem,2.2vw,1.85rem)] leading-[1.22]">
              {content.title}
            </h2>
            {content.lead ? (
              <p className="mt-2.5 mb-0 text-[0.92rem] leading-[1.55] text-[#5E6A63]">{content.lead}</p>
            ) : null}
          </div>

          <div className="mx-auto mt-7 max-w-2xl space-y-2.5">
            {content.items.map((item, i) => {
              const isOpen = open === i;
              return (
                <div
                  key={item.q}
                  className="overflow-hidden rounded-[16px] border border-[#E6EAE4] bg-[#FDFBF7]"
                >
                  <button
                    type="button"
                    className="flex w-full items-center justify-between gap-4 px-4 py-3.5 text-left text-[0.92rem] font-bold text-[#103B2B] sm:px-5 sm:py-4 sm:text-[0.95rem]"
                    aria-expanded={isOpen}
                    onClick={() => setOpen(isOpen ? null : i)}
                  >
                    {item.q}
                    <ChevronDown
                      className={`h-5 w-5 shrink-0 text-[#1FA45A] transition-transform ${isOpen ? 'rotate-180' : ''}`}
                      strokeWidth={2.2}
                    />
                  </button>
                  {isOpen ? (
                    <p className="m-0 border-t border-[#E6EAE4] px-4 py-3.5 text-[0.88rem] leading-[1.55] text-[#5E6A63] sm:px-5 sm:py-4 sm:text-[0.9rem]">
                      {item.a}
                    </p>
                  ) : null}
                </div>
              );
            })}
          </div>
        </article>
      </Container>
    </section>
  );
}
