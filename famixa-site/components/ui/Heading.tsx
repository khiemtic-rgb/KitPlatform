import type { ReactNode } from 'react';

type Props = {
  as?: 'h1' | 'h2' | 'h3' | 'h4';
  children: ReactNode;
  className?: string;
  size?: 'hero' | 'section' | 'card';
};

const sizes = {
  hero: 'var(--type-hero)',
  section: 'var(--type-section)',
  card: 'var(--type-card)',
};

export function Heading({ as: Tag = 'h2', children, className = '', size = 'section' }: Props) {
  return (
    <Tag
      className={`m-0 font-extrabold tracking-[-0.03em] text-[var(--color-text)] ${className}`.trim()}
      style={{ fontSize: sizes[size], lineHeight: size === 'hero' ? 1.12 : 1.2 }}
    >
      {children}
    </Tag>
  );
}
