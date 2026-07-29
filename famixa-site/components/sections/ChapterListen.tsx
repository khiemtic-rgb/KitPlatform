'use client';

import { ChevronRight } from 'lucide-react';
import { IconBox } from '@/components/ui/Badge';
import { Section, SectionHeader } from '@/components/ui/Section';
import { FadeUp } from '@/components/motion/FadeUp';
import { getIcon } from '@/lib/icons';
import type { LandingContent } from '@/content/landing';

type Props = {
  content: LandingContent['chapter2'];
};

/** Chương 2 — process strip with dashed connectors (mock) */
export function ChapterListen({ content }: Props) {
  return (
    <Section id={content.id} tone="mint">
      <SectionHeader eyebrow={content.eyebrow} title={content.title} lead={content.lead} />
      <div className="mx-auto flex max-w-5xl flex-col items-stretch gap-6 md:flex-row md:items-start md:justify-between md:gap-0">
        {content.steps.map((s, i) => {
          const Icon = getIcon(s.icon);
          const last = i === content.steps.length - 1;
          return (
            <FadeUp key={s.title} delay={i * 0.06} className="relative flex flex-1 flex-col items-center text-center md:px-2">
              <div className="flex w-full items-center md:contents">
                <IconBox className="mb-0 h-14 w-14 shrink-0 md:mb-4">
                  <Icon className="h-6 w-6" />
                </IconBox>
                {!last ? (
                  <div
                    className="mx-3 h-px flex-1 border-t-2 border-dashed border-[var(--color-primary)]/45 md:absolute md:left-[calc(50%+2.1rem)] md:right-[-50%] md:top-7 md:mx-0 md:h-auto md:flex-none md:border-t-2"
                    aria-hidden
                  />
                ) : null}
              </div>
              {!last ? (
                <ChevronRight
                  className="absolute top-5 right-0 hidden h-4 w-4 translate-x-1/2 text-[var(--color-primary)]/70 md:block"
                  aria-hidden
                />
              ) : null}
              <h3 className="mt-4 mb-0 text-[1.15rem] font-extrabold text-[var(--color-text)] md:mt-0">
                {s.title}
              </h3>
              <p className="mt-2 mb-0 max-w-[14rem] text-[0.92rem] leading-relaxed text-[var(--color-secondary)]">
                {s.body}
              </p>
            </FadeUp>
          );
        })}
      </div>
    </Section>
  );
}
