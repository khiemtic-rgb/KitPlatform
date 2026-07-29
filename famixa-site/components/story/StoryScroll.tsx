'use client';

import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';

export type StoryChapter = { id: string; label: string };

type Props = {
  chapters: StoryChapter[];
};

/**
 * Story Scroll — fixed chapter rail + progress.
 * Highlights the chapter currently in view (IntersectionObserver).
 */
export function StoryScroll({ chapters }: Props) {
  const [active, setActive] = useState(chapters[0]?.id ?? '');
  const [progress, setProgress] = useState(0);

  useEffect(() => {
    const ids = chapters.map((c) => c.id);
    const elements = ids
      .map((id) => document.getElementById(id))
      .filter((el): el is HTMLElement => Boolean(el));

    if (!elements.length) return;

    const observer = new IntersectionObserver(
      (entries) => {
        const visible = entries
          .filter((e) => e.isIntersecting)
          .sort((a, b) => b.intersectionRatio - a.intersectionRatio);
        if (visible[0]?.target.id) {
          setActive(visible[0].target.id);
        }
      },
      { rootMargin: '-25% 0px -45% 0px', threshold: [0.1, 0.25, 0.5] },
    );

    elements.forEach((el) => observer.observe(el));

    const onScroll = () => {
      const doc = document.documentElement;
      const max = doc.scrollHeight - doc.clientHeight;
      setProgress(max > 0 ? doc.scrollTop / max : 0);
    };
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });

    return () => {
      observer.disconnect();
      window.removeEventListener('scroll', onScroll);
    };
  }, [chapters]);

  return (
    <>
      {/* Top progress bar */}
      <div
        className="pointer-events-none fixed left-0 top-0 z-50 h-[3px] w-full bg-[var(--color-border)]"
        aria-hidden
      >
        <motion.div
          className="h-full bg-[var(--color-primary)] origin-left"
          style={{ scaleX: progress }}
        />
      </div>

      {/* Chapter rail — desktop */}
      <nav
        className="pointer-events-none fixed right-4 top-1/2 z-40 hidden -translate-y-1/2 flex-col gap-2 xl:flex"
        aria-label="Story chapters"
      >
        {chapters.map((ch) => {
          const isActive = ch.id === active;
          return (
            <a
              key={ch.id}
              href={`#${ch.id}`}
              className="pointer-events-auto group flex items-center justify-end gap-2"
              aria-current={isActive ? 'true' : undefined}
            >
              <span
                className={`text-[0.7rem] font-semibold transition-opacity ${
                  isActive
                    ? 'text-[var(--color-primary)] opacity-100'
                    : 'text-[var(--color-secondary)] opacity-0 group-hover:opacity-100'
                }`}
              >
                {ch.label}
              </span>
              <span
                className={`block h-2.5 w-2.5 rounded-full border-2 transition-all ${
                  isActive
                    ? 'scale-125 border-[var(--color-primary)] bg-[var(--color-primary)]'
                    : 'border-[var(--color-border)] bg-[var(--color-bg-elevated)]'
                }`}
              />
            </a>
          );
        })}
      </nav>
    </>
  );
}
