'use client';

import { Button } from '@/components/ui/Button';
import { Section, SectionHeader } from '@/components/ui/Section';
import { Float } from '@/components/motion/Float';
import { FadeUp } from '@/components/motion/FadeUp';
import type { LandingContent } from '@/content/landing';

type Props = {
  content: LandingContent['chapter1'];
};

/** Chương 1 — Fami xuất hiện */
export function ChapterFamiAppear({ content }: Props) {
  return (
    <Section id={content.id} tone="warm">
      <div className="grid items-center gap-10 lg:grid-cols-2 lg:gap-14">
        <FadeUp>
          <SectionHeader
            align="left"
            className="mb-0 max-w-lg"
            eyebrow={content.eyebrow}
            title={content.title}
            lead={content.lead}
            action={
              <Button href={content.ctaHref} variant="soft">
                {content.cta}
              </Button>
            }
          />
        </FadeUp>
        <Float>
          <FadeUp delay={0.08}>
            <div className="mx-auto w-full max-w-lg overflow-hidden rounded-[var(--radius-32)] shadow-[var(--shadow-soft)]">
              <img
                src={content.image.src}
                alt={content.image.alt}
                width={840}
                height={630}
                className="w-full object-cover"
                loading="lazy"
              />
            </div>
          </FadeUp>
        </Float>
      </div>
    </Section>
  );
}
