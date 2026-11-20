import { spokenLine } from './film-production-orchestrator';

export type ShotSpan = { from: number; to: number; first?: string };

/** Place each shot on the current spoken lines. A [ngắn] line between two anchors keeps the earlier shot. */
export function alignShotSpans(shots: ShotSpan[], lines: string[]): ShotSpan[] {
  if (!shots.length) return [];
  if (!lines.length || shots.some((shot) => !shot.first)) return shots.map((shot) => ({ from: shot.from, to: shot.to }));
  const anchors: number[] = [];
  let cursor = 0;
  for (const shot of shots) {
    const want = spokenLine(shot.first || '').normalize('NFC').trim();
    let found = -1;
    for (let i = cursor; i < lines.length; i += 1) {
      if (lines[i].normalize('NFC').trim() === want) {
        found = i;
        break;
      }
    }
    if (found < 0) return shots.map((item) => ({ from: item.from, to: item.to }));
    anchors.push(found);
    cursor = found + 1;
  }
  return anchors.map((from, index) => ({
    from,
    to: (index + 1 < anchors.length ? anchors[index + 1] : lines.length) - 1,
    first: shots[index]?.first,
  }));
}

export function geminiStillPath(path: string) {
  return /-g\d{6,12}\.(png|jpg)$/i.test((path || '').split(/[/\\]/).pop() || '');
}

export function shotOf(shots: ShotSpan[] | null | undefined, index: number) {
  return shots?.find((shot) => shot.from <= index && index <= shot.to) ?? null;
}

/** Shots that still need one Gemini still. An approved still, or one already from Gemini, keeps the whole shot. */
export function geminiShotsToMake<T extends { index: number; path: string; approved?: boolean }>(shots: ShotSpan[], stills: T[]) {
  const queue: { from: number; to: number; indexes: number[] }[] = [];
  for (const shot of shots) {
    const rows = stills.filter((item) => item.index >= shot.from && item.index <= shot.to);
    if (!rows.length) continue;
    if (rows.some((item) => item.approved || geminiStillPath(item.path))) continue;
    queue.push({ from: shot.from, to: shot.to, indexes: rows.map((item) => item.index) });
  }
  return queue;
}
