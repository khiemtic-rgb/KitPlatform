'use client';

import type { ReactNode } from 'react';
import { motion } from 'framer-motion';

type Variant = 'primary' | 'outline' | 'ghost' | 'soft';

type Props = {
  children: ReactNode;
  variant?: Variant;
  href?: string;
  className?: string;
  type?: 'button' | 'submit' | 'reset';
  onClick?: () => void;
  'aria-label'?: string;
};

const styles: Record<Variant, string> = {
  primary:
    'bg-[var(--color-primary)] !text-white shadow-[var(--shadow-soft)] hover:brightness-95',
  outline:
    'bg-transparent border border-[var(--color-border)] text-[var(--color-text)] hover:border-[var(--color-primary)]',
  ghost:
    'bg-[#fff] border border-[var(--color-border)] text-[var(--color-text)] shadow-sm hover:border-[var(--color-primary)]',
  soft: 'bg-transparent text-[var(--color-primary)] px-0 min-h-0 shadow-none rounded-none',
};

export function Button({
  children,
  variant = 'primary',
  href,
  className = '',
  type = 'button',
  onClick,
  'aria-label': ariaLabel,
}: Props) {
  const base =
    `inline-flex items-center justify-center gap-2 min-h-12 px-6 font-bold rounded-2xl transition-colors ${styles[variant]} ${className}`.trim();

  if (href) {
    return (
      <motion.a
        href={href}
        className={base}
        aria-label={ariaLabel}
        whileHover={{ scale: 1.02 }}
        whileTap={{ scale: 0.98 }}
      >
        {children}
      </motion.a>
    );
  }

  return (
    <motion.button
      type={type}
      className={base}
      aria-label={ariaLabel}
      onClick={onClick}
      whileHover={{ scale: 1.02 }}
      whileTap={{ scale: 0.98 }}
    >
      {children}
    </motion.button>
  );
}
