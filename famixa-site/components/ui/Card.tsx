'use client';

import type { ReactNode } from 'react';
import { motion } from 'framer-motion';
import { FadeUp } from '../motion/FadeUp';

type Props = {
  children: ReactNode;
  className?: string;
  hoverLift?: boolean;
};

export function Card({ children, className = '', hoverLift = true }: Props) {
  return (
    <FadeUp>
      <motion.div
        className={`rounded-[var(--radius-20)] border border-[var(--color-border)] bg-[var(--color-bg-elevated)] p-6 shadow-[var(--shadow-soft)] ${className}`.trim()}
        whileHover={hoverLift ? { y: -6 } : undefined}
        transition={{ type: 'spring', stiffness: 320, damping: 24 }}
      >
        {children}
      </motion.div>
    </FadeUp>
  );
}
