/** Human-readable Vietnamese late duration (515 → "8 giờ 35 phút"). */
export function formatLateDuration(minutes: number): string {
  const n = Math.max(0, Math.floor(minutes));
  if (n < 60) return `${n} phút`;
  const hrs = Math.floor(n / 60);
  const mins = n % 60;
  if (mins === 0) return `${hrs} giờ`;
  return `${hrs} giờ ${mins} phút`;
}

/** Rewrite API labels like "Muộn 515′ — …" into human duration text. */
export function normalizeLateStarLabelVi(label: string): string {
  return label.replace(
    /^Muộn (\d+)(?:['′]| phút)?(\s*—)/,
    (_, raw, sep) => `Muộn ${formatLateDuration(Number(raw))}${sep}`,
  );
}

/** Subtext under task title: duration only (star badge shows delta separately). */
export function stripLateStarSuffixVi(label: string): string {
  return normalizeLateStarLabelVi(label)
    .replace(/\s*[—–-]\s*[+-]?\d+\s*⭐.*$/u, '')
    .replace(/\s*[—–-]\s*không được sao\s*$/iu, '')
    .replace(/\s*[—–-]\s*0\s*⭐\s*$/u, '')
    .trim();
}

export function formatLateDurationCaption(lateMinutes: number): string {
  return `Muộn ${formatLateDuration(lateMinutes)}`;
}
