export const CHARACTER_DESIGN_LANGUAGE_V1_ID = 'FAMIXA_CHARACTER_DESIGN_LANGUAGE_V1';
export const CHARACTER_DESIGN_LANGUAGE_SUITE = 'FAMIXA_CHARACTER_DESIGN_LANGUAGE_V1_REGRESSION';
export const CHARACTER_DESIGN_LANGUAGE_V2_ID = 'FAMIXA_CHARACTER_DESIGN_LANGUAGE_V2';
export const CHARACTER_DESIGN_LANGUAGE_V2_SUITE = 'FAMIXA_CHARACTER_DESIGN_LANGUAGE_V2_REGRESSION';

export function designLanguageReady(row?: { sha?: string | null; status?: string | null } | null) {
  return /^[0-9a-f]{64}$/i.test((row?.sha || '').trim()) && !!row?.status;
}

export function designLanguageStatusLabel(status?: string | null) {
  const s = (status || '').toUpperCase();
  if (s === 'ACTIVE' || s === 'LOCKED') return s;
  if (s === 'DRAFT') return 'DRAFT';
  return status || 'DRAFT';
}
