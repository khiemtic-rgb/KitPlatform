import type { ReactNode } from 'react';

type Props = {
  children: ReactNode;
  className?: string;
};

export function Badge({ children, className = '' }: Props) {
  return (
    <span
      className={`inline-flex items-center rounded-full bg-[var(--color-primary)] px-3 py-1 text-[var(--type-small)] font-extrabold !text-white ${className}`.trim()}
    >
      {children}
    </span>
  );
}

export function IconBox({ children, className = '' }: Props) {
  return (
    <span
      className={`inline-grid h-12 w-12 place-items-center rounded-full bg-[var(--color-icon-bg)] text-[var(--color-primary)] ${className}`.trim()}
    >
      {children}
    </span>
  );
}
