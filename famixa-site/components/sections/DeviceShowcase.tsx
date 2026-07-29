'use client';

import { Button } from '@/components/ui/Button';
import { Section, SectionHeader } from '@/components/ui/Section';
import { FadeUp } from '@/components/motion/FadeUp';
import type { LandingContent } from '@/content/landing';

type Props = {
  content: LandingContent['chapter5'];
  appUrl: string;
};

export function DeviceShowcase({ content, appUrl }: Props) {
  return (
    <Section id={content.id}>
      <div className="grid items-center gap-10 lg:grid-cols-2 lg:gap-14">
        <FadeUp>
          <SectionHeader
            align="left"
            className="mb-0 max-w-lg"
            eyebrow={content.eyebrow}
            title={content.title}
            lead={content.lead}
            action={
              <Button href={appUrl} variant="soft">
                {content.cta}
              </Button>
            }
          />
        </FadeUp>
        <FadeUp delay={0.1}>
          <img
            src={content.image.src}
            alt={content.image.alt}
            width={1400}
            height={788}
            className="w-full rounded-[var(--radius-32)] shadow-[var(--shadow-soft)]"
            loading="lazy"
          />
        </FadeUp>
      </div>
    </Section>
  );
}
