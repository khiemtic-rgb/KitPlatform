'use client';

import { useEffect } from 'react';
import type { AppLocale } from '@/lib/cms/getLanding';

/** Sets <html lang> for the active locale (static export keeps a single root layout). */
export function LocaleHtmlLang({ locale }: { locale: AppLocale }) {
  useEffect(() => {
    document.documentElement.lang = locale;
  }, [locale]);

  return null;
}
