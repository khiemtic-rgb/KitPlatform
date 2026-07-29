'use client';

import { useState } from 'react';
import { ArrowRight, Menu, X } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import type { LandingContent } from '@/content/landing';
import type { AppLocale } from '@/lib/cms/getLanding';

type Props = {
  content: LandingContent['nav'];
  brand: LandingContent['brand'];
  appUrl: string;
  locale: AppLocale;
};

const UI = {
  vi: {
    navAria: 'Điều hướng chính',
    openMenu: 'Mở menu',
    closeMenu: 'Đóng menu',
    langAria: 'Chọn ngôn ngữ',
  },
  en: {
    navAria: 'Main navigation',
    openMenu: 'Open menu',
    closeMenu: 'Close menu',
    langAria: 'Choose language',
  },
} as const;

/** Header: Fami logo · nav · locale · login · CTA */
export function Navbar({ content, brand, appUrl, locale }: Props) {
  const [open, setOpen] = useState(false);
  const ui = UI[locale];
  const homeHref = `/${locale}/`;

  return (
    <header className="sticky top-0 z-40 border-b border-[#E8E6DF]/70 bg-[#FBF8F1]/95 backdrop-blur-sm">
      <Container className="flex min-h-[72px] items-center gap-5 md:min-h-[78px]">
        <a href={homeHref} className="flex shrink-0 items-center gap-3">
          <img
            src={brand.logo}
            alt=""
            width={48}
            height={48}
            className="h-11 w-11 drop-shadow-[0_4px_12px_rgba(123,194,74,0.35)] md:h-12 md:w-12"
          />
          <span>
            <strong className="block text-[1.25rem] font-extrabold leading-none tracking-[-0.02em] text-[#103B2B]">
              {brand.name}
            </strong>
            <span className="mt-1.5 hidden text-[0.65rem] font-medium leading-tight text-[#5A7A68] sm:block">
              {brand.tagline}
            </span>
          </span>
        </a>

        <nav
          className="mx-auto hidden items-center gap-6 text-[0.92rem] font-medium text-[#4B5563] xl:flex 2xl:gap-7"
          aria-label={ui.navAria}
        >
          {content.links.map((l) => (
            <a
              key={l.href}
              href={l.href}
              className="whitespace-nowrap transition-colors hover:text-[#1FA45A]"
            >
              {l.label}
            </a>
          ))}
        </nav>

        <div className="ml-auto flex shrink-0 items-center gap-2.5">
          <div
            className="hidden items-center rounded-xl border border-[#E4E7E2] bg-white/80 p-0.5 text-[0.75rem] font-bold sm:flex"
            role="group"
            aria-label={ui.langAria}
          >
            <a
              href="/vi/"
              className={
                locale === 'vi'
                  ? 'rounded-[10px] bg-[#103B2B] px-2.5 py-1.5 text-white'
                  : 'rounded-[10px] px-2.5 py-1.5 text-[#5A7A68] hover:text-[#103B2B]'
              }
              aria-current={locale === 'vi' ? 'page' : undefined}
            >
              VI
            </a>
            <a
              href="/en/"
              className={
                locale === 'en'
                  ? 'rounded-[10px] bg-[#103B2B] px-2.5 py-1.5 text-white'
                  : 'rounded-[10px] px-2.5 py-1.5 text-[#5A7A68] hover:text-[#103B2B]'
              }
              aria-current={locale === 'en' ? 'page' : undefined}
            >
              EN
            </a>
          </div>

          <Button
            href={appUrl}
            variant="outline"
            className="hidden !min-h-[42px] !rounded-xl !border-transparent !bg-[#EEF0ED] !px-4 !text-[0.9rem] !font-semibold !text-[#1D1D1F] hover:!bg-[#E4E7E2] md:inline-flex"
          >
            {content.login}
          </Button>
          <Button
            href={appUrl}
            className="hidden !min-h-[42px] !rounded-xl !bg-[#103B2B] !px-4 !text-[0.9rem] !font-semibold !text-[#fff] hover:!bg-[#0c2f22] md:inline-flex lg:!px-5"
          >
            {content.cta}
          </Button>
          <Button
            variant="outline"
            className="min-h-10 rounded-xl px-3 xl:hidden"
            aria-label={open ? ui.closeMenu : ui.openMenu}
            onClick={() => setOpen((v) => !v)}
          >
            {open ? <X className="h-4 w-4" /> : <Menu className="h-4 w-4" />}
          </Button>
        </div>
      </Container>

      {open ? (
        <div className="border-t border-[#E8E6DF] bg-[#FBF8F1] xl:hidden">
          <Container className="flex flex-col gap-1 py-3">
            {content.links.map((l) => (
              <a
                key={l.href}
                href={l.href}
                className="rounded-xl px-3 py-2.5 text-sm font-semibold hover:bg-[#EAF5EE]"
                onClick={() => setOpen(false)}
              >
                {l.label}
              </a>
            ))}
            <div className="mt-2 flex items-center gap-2 px-1 sm:hidden" role="group" aria-label={ui.langAria}>
              <a
                href="/vi/"
                className={
                  locale === 'vi'
                    ? 'rounded-lg bg-[#103B2B] px-3 py-1.5 text-xs font-bold text-white'
                    : 'rounded-lg border border-[#E4E7E2] px-3 py-1.5 text-xs font-bold text-[#5A7A68]'
                }
              >
                VI
              </a>
              <a
                href="/en/"
                className={
                  locale === 'en'
                    ? 'rounded-lg bg-[#103B2B] px-3 py-1.5 text-xs font-bold text-white'
                    : 'rounded-lg border border-[#E4E7E2] px-3 py-1.5 text-xs font-bold text-[#5A7A68]'
                }
              >
                EN
              </a>
            </div>
            <div className="mt-2 flex flex-col gap-2 sm:flex-row">
              <Button href={appUrl} variant="outline" className="min-h-11 rounded-xl text-sm">
                {content.login}
              </Button>
              <Button
                href={appUrl}
                className="min-h-11 rounded-xl !bg-[#103B2B] !text-white text-sm hover:!bg-[#0c2f22]"
              >
                {content.cta}
                <ArrowRight className="h-4 w-4" />
              </Button>
            </div>
          </Container>
        </div>
      ) : null}
    </header>
  );
}
