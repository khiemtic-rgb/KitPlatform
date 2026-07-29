import type { ReactNode } from 'react';

type Props = {
  children: ReactNode;
  className?: string;
  muted?: boolean;
  size?: 'body' | 'small';
  as?: 'p' | 'span';
};

export function Text({
  children,
  className = '',
  muted = false,
  size = 'body',
  as: Tag = 'p',
}: Props) {
  const color = muted ? 'text-[var(--color-secondary)]' : 'text-[var(--color-text)]';
  const font = size === 'small' ? 'text-[var(--type-small)]' : 'text-[var(--type-body)]';
  return (
    <Tag className={`m-0 leading-relaxed ${font} ${color} ${className}`.trim()}>{children}</Tag>
  );
}
