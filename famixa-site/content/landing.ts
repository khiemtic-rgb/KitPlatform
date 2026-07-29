/**
 * Editable landing content — re-exports CMS document.
 * Prefer editing `content/landing.json` (CMS-shaped). This module keeps
 * section prop types stable (`LandingContent`).
 */
import { getLandingContentSync } from '@/lib/cms/getLanding';
import type { LandingDocument } from '@/content/schema';

export const landing = getLandingContentSync();
export type LandingContent = LandingDocument;
