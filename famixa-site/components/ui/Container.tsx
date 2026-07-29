import type { ReactNode } from 'react';

type Props = {
  children: ReactNode;
  className?: string;
  as?: 'div' | 'section' | 'header' | 'footer' | 'nav';
};

export function Container({ children, className = '', as: Tag = 'div' }: Props) {
  return (
    <Tag className={`mx-auto w-full max-w-[var(--max)] px-4 sm:px-6 ${className}`.trim()}>
      {children}
    </Tag>
  );
}
