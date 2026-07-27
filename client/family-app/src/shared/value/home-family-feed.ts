import type {
  ChildGratitude,
  DayFlowCommitment,
  FamilyMemoryEntry,
  RewardRedemption,
} from '@/shared/api/family-os.api';

export type HomeFeedKind = 'gratitude' | 'memory' | 'star' | 'reward';

export type HomeFeedItem = {
  id: string;
  kind: HomeFeedKind;
  icon: string;
  titleVi: string;
  detailVi?: string;
  /** Tab / anchor hint for parent board. */
  go: 'value' | 'tasks' | 'rewards' | 'diary';
};

/**
 * P1 Family Feed — only events that happened on flowDate. Empty → hide section.
 */
export function buildHomeFamilyFeed(input: {
  flowDate: string;
  gratitudes: ChildGratitude[];
  memories: FamilyMemoryEntry[];
  doneCommitments: DayFlowCommitment[];
  redemptions?: RewardRedemption[];
  max?: number;
}): HomeFeedItem[] {
  const max = input.max ?? 4;
  const items: HomeFeedItem[] = [];

  for (const g of input.gratitudes) {
    items.push({
      id: `grat-${g.id}`,
      kind: 'gratitude',
      icon: '💛',
      titleVi: g.messageVi,
      detailVi:
        (g.fromMemberName || 'Con') +
        (g.praiseContext ? ` · vì «${g.praiseContext}»` : ''),
      go: 'value',
    });
  }

  const todayMemories = input.memories.filter((m) => m.flowDate === input.flowDate);
  // Prefer first_time / streak / beautiful_day for feed signal.
  const ranked = [...todayMemories].sort((a, b) => {
    const rank = (k: string) =>
      k === 'first_time' ? 0 : k === 'streak_milestone' ? 1 : k === 'beautiful_day' ? 2 : 3;
    return rank(a.kind) - rank(b.kind);
  });
  for (const m of ranked) {
    if (items.some((x) => x.kind === 'gratitude' && m.kind === 'gratitude')) continue;
    items.push({
      id: `mem-${m.id}`,
      kind: 'memory',
      icon: m.icon || (m.kind === 'first_time' ? '✨' : '📖'),
      titleVi: m.titleVi,
      detailVi: m.noteVi || m.memberName,
      go: 'diary',
    });
  }

  for (const c of input.doneCommitments) {
    if (c.status !== 'done' || !c.starPosted) continue;
    const stars = c.starDelta ?? 0;
    if (stars <= 0) continue;
    items.push({
      id: `star-${c.id}`,
      kind: 'star',
      icon: '⭐',
      titleVi: c.title,
      detailVi: `+${stars} sao hôm nay`,
      go: 'tasks',
    });
  }

  for (const r of input.redemptions ?? []) {
    if (r.status !== 'fulfilled') continue;
    const when = (r.fulfilledAt || r.createdAt || '').slice(0, 10);
    if (when && when !== input.flowDate) continue;
    items.push({
      id: `rew-${r.id}`,
      kind: 'reward',
      icon: r.icon || '🎁',
      titleVi: r.title || 'Đã nhận phần thưởng',
      detailVi: 'Đã xác nhận nhận quà',
      go: 'rewards',
    });
  }

  // Dedupe by title-ish for star overlapping memory of same mission — keep first.
  const seen = new Set<string>();
  const out: HomeFeedItem[] = [];
  for (const it of items) {
    const key = `${it.kind}:${it.titleVi.trim().toLowerCase()}`;
    if (seen.has(key)) continue;
    seen.add(key);
    out.push(it);
    if (out.length >= max) break;
  }
  return out;
}

/** Pick one memory win line for Brief (P2) — prefer first_time today. */
export function pickMemoryWinVi(
  memories: FamilyMemoryEntry[],
  flowDate: string,
): string | undefined {
  const today = memories.filter((m) => m.flowDate === flowDate);
  const first =
    today.find((m) => m.kind === 'first_time') ||
    today.find((m) => m.kind === 'streak_milestone') ||
    today.find((m) => m.kind === 'beautiful_day') ||
    today[0];
  if (!first) return undefined;
  if (first.kind === 'first_time') {
    return `Lần đầu: ${first.titleVi}`;
  }
  return first.titleVi;
}
