import type { ReactNode } from 'react';

type Props = {
  children: ReactNode;
  className?: string;
  gap?: 8 | 12 | 16 | 24 | 32 | 48;
  direction?: 'row' | 'col';
};

export function Stack({ children, className = '', gap = 16, direction = 'col' }: Props) {
  const dir = direction === 'row' ? 'flex-row flex-wrap' : 'flex-col';
  return (
    <div className={`flex ${dir} ${className}`.trim()} style={{ gap }}>
      {children}
    </div>
  );
}
