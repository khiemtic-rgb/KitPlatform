'use client';

import { Button } from '@/components/ui/Button';
import { Section, SectionHeader } from '@/components/ui/Section';
import { FadeUp } from '@/components/motion/FadeUp';
import type { LandingContent } from '@/content/landing';

type Props = {
  content: LandingContent['chapter4'];
};

export function JourneyTimeline({ content }: Props) {
  return (
    <Section id={content.id} tone="mint">
      <SectionHeader
        align="left"
        eyebrow={content.eyebrow}
        title={content.title}
        lead={content.lead}
        action={
          <Button href={content.ctaHref} variant="soft">
            {content.cta}
          </Button>
        }
      />
      <FadeUp>
        <div className="overflow-hidden rounded-[var(--radius-32)] border border-[var(--color-border)] bg-[var(--color-bg-elevated)] p-4 shadow-[var(--shadow-soft)] md:p-6">
          <img
            src={content.image.src}
            alt={content.image.alt}
            width={1400}
            height={788}
            className="w-full rounded-[var(--radius-20)]"
            loading="lazy"
          />
        </div>
      </FadeUp>
    </Section>
  );
}
