import type { ConsultationSymptomCatalog, ConsultationSymptomOption } from '@/shared/api/pharmacy-consultation.api';

export type CartSymptomInferLine = {
  productName: string;
  genericName?: string | null;
};

const FALLBACK_CODES = [
  'cough',
  'fever',
  'headache',
  'sore_throat',
  'runny_nose',
  'diarrhea',
  'heartburn',
  'body_ache',
] as const;

function normalizeText(value: string): string {
  return value
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .toLowerCase();
}

/** Soft suggestions from cart product names vs catalog aliases; pad with common OTC chips. */
export function buildCheckoutSymptomChips(
  catalog: ConsultationSymptomCatalog | null,
  cartLines: CartSymptomInferLine[],
  limit = 10,
): ConsultationSymptomOption[] {
  if (!catalog?.flat?.length) return [];

  const byCode = new Map(catalog.flat.map((o) => [o.code, o]));
  const haystacks = cartLines
    .map((l) => normalizeText(`${l.productName} ${l.genericName ?? ''}`))
    .filter((h) => h.length > 0);

  const scores = new Map<string, number>();

  for (const opt of catalog.flat) {
    if (!opt.code) continue;
    const aliases = [
      opt.label,
      opt.code.replaceAll('_', ' '),
      ...(catalog.aliasesByCode?.[opt.code] ?? []),
    ];
    let score = 0;
    for (const alias of aliases) {
      const needle = normalizeText(alias);
      if (needle.length < 2) continue;
      if (haystacks.some((h) => h.includes(needle))) {
        score += needle.length >= 4 ? 3 : 2;
      }
    }
    if (score > 0) scores.set(opt.code, (scores.get(opt.code) ?? 0) + score);
  }

  const inferred = [...scores.entries()]
    .sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))
    .map(([code]) => byCode.get(code))
    .filter((o): o is ConsultationSymptomOption => Boolean(o?.code));

  const fallback = FALLBACK_CODES.map((code) => byCode.get(code)).filter(
    (o): o is ConsultationSymptomOption => Boolean(o?.code),
  );

  const seen = new Set<string>();
  const result: ConsultationSymptomOption[] = [];
  for (const opt of [...inferred, ...fallback]) {
    if (seen.has(opt.code)) continue;
    seen.add(opt.code);
    result.push(opt);
    if (result.length >= limit) break;
  }
  return result;
}
