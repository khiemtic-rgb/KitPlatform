'use client';

import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Card';
import { Section, SectionHeader } from '@/components/ui/Section';
import type { LandingContent } from '@/content/landing';

type Props = {
  content: LandingContent['chapter3'];
};

/** Chương 3 — 4 moment cards */
export function ChapterMoments({ content }: Props) {
  return (
    <Section id={content.id}>
      <div className="mb-10 flex flex-col gap-6 lg:mb-12 lg:grid lg:grid-cols-[minmax(240px,0.9fr)_2.2fr] lg:items-end lg:gap-10">
        <SectionHeader
          align="left"
          eyebrow={content.eyebrow}
          title={content.title}
          lead={content.lead}
          className="mb-0"
          action={
            <Button href={content.ctaHref} variant="soft">
              {content.cta}
            </Button>
          }
        />
        <div className="hidden lg:block" aria-hidden />
      </div>
      <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
        {content.moments.map((m) => (
          <Card key={m.title} className="overflow-hidden !rounded-[var(--radius-20)] !p-0">
            <div className="aspect-[4/3] overflow-hidden bg-[var(--color-muted)]">
              <img
                src={m.image.src}
                alt={m.image.alt}
                width={640}
                height={480}
                className="h-full w-full object-cover"
                loading="lazy"
              />
            </div>
            <div className="p-5">
              <h3 className="m-0 text-[1.15rem] font-extrabold">{m.title}</h3>
              <p className="mt-2 mb-0 text-[0.9rem] leading-relaxed text-[var(--color-secondary)]">
                {m.body}
              </p>
            </div>
          </Card>
        ))}
      </div>
    </Section>
  );
}
