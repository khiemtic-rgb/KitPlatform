'use client';

import { ArrowRight, Leaf } from 'lucide-react';

/** Badge chương — pill xanh nhạt + lá */
export function ChapterBadge({
  children,
  solid = false,
}: {
  children: React.ReactNode;
  solid?: boolean;
}) {
  if (solid) {
    return (
      <p className="m-0 inline-flex w-fit max-w-full items-center gap-1.5 self-start rounded-full bg-[#1FA45A] px-3 py-1 text-[0.68rem] font-extrabold uppercase tracking-[0.14em] !text-white">
        <Leaf className="h-3.5 w-3.5 shrink-0 text-white" strokeWidth={2.5} aria-hidden />
        {children}
      </p>
    );
  }
  return (
      <p className="m-0 inline-flex w-fit max-w-full items-center gap-1.5 self-start rounded-full bg-[#E8F6EE] px-3 py-1 text-[0.68rem] font-extrabold uppercase tracking-[0.14em] text-[#1A7A45]">
      <Leaf className="h-3.5 w-3.5 shrink-0 fill-[#1FA45A] text-[#1FA45A]" strokeWidth={2.2} aria-hidden />
      {children}
    </p>
  );
}

/** Gạch ngang + lá — `end` (Ch1–2) hoặc `center` (Ch3) theo mẫu */
export function LeafRule({
  className = '',
  variant = 'end',
}: {
  className?: string;
  variant?: 'end' | 'center';
}) {
  if (variant === 'center') {
    return (
      <div className={`my-3.5 flex w-[7.5rem] max-w-full items-center gap-1.5 ${className}`} aria-hidden>
        <span className="h-[2px] min-w-0 flex-1 rounded-full bg-[#1A5C38]/85" />
        <Leaf className="h-3.5 w-3.5 shrink-0 fill-[#1FA45A] text-[#1FA45A]" strokeWidth={2.2} />
        <span className="h-[2px] min-w-0 flex-1 rounded-full bg-[#1A5C38]/85" />
      </div>
    );
  }
  return (
    <div className={`my-3.5 flex w-[7.5rem] max-w-full items-center gap-1.5 ${className}`} aria-hidden>
      <span className="h-[2px] min-w-0 flex-1 rounded-full bg-[#1A5C38]/85" />
      <Leaf className="h-3.5 w-3.5 shrink-0 fill-[#1FA45A] text-[#1FA45A]" strokeWidth={2.2} />
    </div>
  );
}

export function PillCta({
  href,
  children,
  variant = 'outline',
}: {
  href: string;
  children: React.ReactNode;
  variant?: 'outline' | 'solid' | 'ghost-white' | 'dark';
}) {
  if (variant === 'solid') {
    return (
      <a
        href={href}
        className="inline-flex min-h-[44px] w-fit items-center gap-2.5 rounded-full bg-[#1FA45A] px-5 pr-1.5 text-[0.9rem] font-bold !text-white shadow-[0_8px_20px_rgba(31,164,90,0.28)] transition-[filter] hover:brightness-105"
      >
        {children}
        <span className="grid h-8 w-8 place-items-center rounded-full bg-white text-[#1FA45A]">
          <ArrowRight className="h-3.5 w-3.5" strokeWidth={2.6} />
        </span>
      </a>
    );
  }
  if (variant === 'dark') {
    return (
      <a
        href={href}
        className="inline-flex min-h-[48px] w-fit items-center gap-2.5 rounded-full bg-[#103B2B] px-5 pr-1.5 text-[0.92rem] font-bold !text-white shadow-[0_10px_24px_rgba(16,59,43,0.28)] transition-[filter] hover:brightness-110"
      >
        {children}
        <span className="grid h-9 w-9 place-items-center rounded-full bg-white text-[#103B2B]">
          <ArrowRight className="h-4 w-4" strokeWidth={2.6} />
        </span>
      </a>
    );
  }
  if (variant === 'ghost-white') {
    return (
      <a
        href={href}
        className="inline-flex min-h-[42px] w-fit items-center gap-2.5 rounded-full border border-[#D0D5CE] bg-white px-4 pr-1.5 text-[0.84rem] font-bold text-[#1D1D1F] shadow-[0_4px_14px_rgba(16,59,43,0.06)] transition-colors hover:border-[#1FA45A]"
      >
        {children}
        <span className="grid h-7 w-7 place-items-center rounded-full bg-[#1FA45A] text-white">
          <ArrowRight className="h-3.5 w-3.5" strokeWidth={2.5} />
        </span>
      </a>
    );
  }
  return (
    <a
      href={href}
      className="inline-flex min-h-[44px] w-fit items-center gap-2.5 rounded-full border border-[#1FA45A]/75 bg-white px-5 pr-1.5 text-[0.9rem] font-bold text-[#103B2B] transition-colors hover:bg-[#EAF6EE]"
    >
      {children}
      <span className="grid h-7 w-7 place-items-center rounded-full border border-[#1FA45A] text-[#1FA45A]">
        <ArrowRight className="h-3.5 w-3.5" strokeWidth={2.5} />
      </span>
    </a>
  );
}
