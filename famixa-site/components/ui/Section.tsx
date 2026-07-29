import type { ReactNode } from 'react';
import { Container } from './Container';

type SectionProps = {
  id?: string;
  children: ReactNode;
  className?: string;
  tone?: 'default' | 'warm' | 'mint';
};

const tones = {
  default: '',
  warm: 'bg-gradient-to-b from-[var(--color-warm)] to-transparent',
  mint: 'bg-gradient-to-b from-[var(--color-mint)] to-transparent',
};

export function Section({ id, children, className = '', tone = 'default' }: SectionProps) {
  return (
    <section id={id} className={`scroll-mt-24 py-16 md:py-20 lg:py-24 ${tones[tone]} ${className}`.trim()}>
      <Container>{children}</Container>
    </section>
  );
}

type HeaderProps = {
  eyebrow?: string;
  title: string;
  lead?: string;
  align?: 'left' | 'center';
  action?: ReactNode;
  className?: string;
};

export function SectionHeader({
  eyebrow,
  title,
  lead,
  align = 'center',
  action,
  className = '',
}: HeaderProps) {
  const alignCls = align === 'center' ? 'mx-auto text-center' : 'text-left';
  return (
    <div className={`mb-10 max-w-2xl ${alignCls} ${className}`.trim()}>
      {eyebrow ? (
        <p className="mb-3 text-[var(--type-small)] font-extrabold uppercase tracking-[0.12em] text-[var(--color-primary)]">
          {eyebrow}
        </p>
      ) : null}
      <h2
        className="m-0 font-extrabold tracking-[-0.035em] text-[var(--color-text)]"
        style={{ fontSize: 'var(--type-section)', lineHeight: 1.2 }}
      >
        {title}
      </h2>
      {lead ? (
        <p className="mt-3 mb-0 text-[1.05rem] leading-relaxed text-[var(--color-secondary)] md:text-[var(--type-body)]">
          {lead}
        </p>
      ) : null}
      {action ? <div className="mt-5">{action}</div> : null}
    </div>
  );
}
