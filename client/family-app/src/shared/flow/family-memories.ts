import type {
  DayFlowCommitment,
  FamilyMemoryEntry,
  RewardRedemption,
  TeamUnlock,
} from '@/shared/api/family-os.api';
import { isSiblingComboUnlock } from '@/modules/flow/teamPlay';

export type FamilyMemory = {
  id: string;
  icon: string;
  title: string;
  date: string;
  sortAt: number;
  isNew?: boolean;
  pending?: boolean;
  locked?: boolean;
  /** Present when this card comes from pack_family.family_memory. */
  entry?: FamilyMemoryEntry;
  photoUrl?: string;
  /** Kind for kid album filters (saved entry.kind or synthetic). */
  filterKind?: string;
};

/** UI chip "Đêm xem phim" maps to this filter value (legacy id kept for sheet state). */
export type KidMemoryFilter = 'all' | 'team_unlock' | 'parent_voice' | 'beautiful_day' | 'kid_moment';

/** True only for Đêm xem phim — not High-five / sibling combo / other team unlocks. */
export function isMovieNightMemoryText(
  title: string,
  _icon?: string | null,
  rewardCode?: string | null,
): boolean {
  if (rewardCode && isSiblingComboUnlock(rewardCode)) return false;
  const t = title.toLowerCase();
  // Title wins over misleading 🍿 icons stored on other team unlocks.
  if (/high-?\s*five|highfive|sibling_combo/i.test(t)) return false;
  if (rewardCode && /movie/i.test(rewardCode)) return true;
  return /movie\s*night|\bmovie\b|đêm xem phim|(^|[^a-z])phim\b/i.test(t);
}

export function isMovieNightUnlock(u: {
  rewardCode?: string | null;
  labelVi?: string | null;
}): boolean {
  return isMovieNightMemoryText(u.labelVi ?? '', null, u.rewardCode);
}

export function memoryFilterKindOf(m: FamilyMemory): string {
  if (m.filterKind) return m.filterKind;
  if (m.entry?.kind) return m.entry.kind;
  if (m.id.startsWith('unlock-')) return 'team_other';
  if (m.id.startsWith('redeem-')) return 'reward';
  if (m.id.startsWith('milestone-garden')) return 'first_time';
  if (m.id.startsWith('milestone-read')) return 'first_time';
  return 'manual';
}

export function matchesKidMemoryFilter(m: FamilyMemory, filter: KidMemoryFilter): boolean {
  if (filter === 'all') return true;
  const kind = memoryFilterKindOf(m);
  if (filter === 'team_unlock') {
    return kind === 'movie_night' || isMovieNightMemoryText(m.title, m.icon);
  }
  if (filter === 'parent_voice') return kind === 'parent_voice';
  if (filter === 'kid_moment') return kind === 'kid_moment';
  if (filter === 'beautiful_day') {
    return (
      kind === 'beautiful_day' ||
      kind === 'first_time' ||
      m.icon === '🌸' ||
      /vườn|đã nở/i.test(m.title)
    );
  }
  return true;
}

export const FAMILY_MEMORY_VISIBLE = 4;
const NEW_DAYS = 7;

export function formatMemoryDate(iso: string): string {
  const d = new Date(iso);
  if (!Number.isNaN(d.getTime())) {
    const dd = String(d.getDate()).padStart(2, '0');
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const yyyy = d.getFullYear();
    return `${dd}/${mm}/${yyyy}`;
  }
  const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})/);
  if (m) return `${m[3]}/${m[2]}/${m[1]}`;
  return iso;
}

/** Relative chip for diary memory cards (“3 ngày trước”, not list index). */
export function memoryRelativeAgoLabel(sortAt: number, now = new Date()): string {
  if (!sortAt || sortAt <= 0) return 'Gần đây';
  const diffMs = now.getTime() - sortAt;
  if (diffMs < 0) return 'Gần đây';
  const days = Math.floor(diffMs / 86_400_000);
  if (days <= 0) return 'Hôm nay';
  if (days === 1) return 'Hôm qua';
  if (days < 7) return `${days} ngày trước`;
  const weeks = Math.floor(days / 7);
  if (days < 30) return weeks <= 1 ? '1 tuần trước' : `${weeks} tuần trước`;
  const months = Math.floor(days / 30.4375);
  if (months < 12) return months <= 1 ? '1 tháng trước' : `${months} tháng trước`;
  const years = Math.floor(days / 365.25);
  return years <= 1 ? '1 năm trước' : `${years} năm trước`;
}

function parseSortTime(iso: string): number {
  const t = new Date(iso).getTime();
  if (!Number.isNaN(t)) return t;
  const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})/);
  if (m) return new Date(`${m[1]}-${m[2]}-${m[3]}T12:00:00`).getTime();
  return 0;
}

function isRecent(iso: string, now: Date): boolean {
  const t = parseSortTime(iso);
  if (!t) return false;
  const days = (now.getTime() - t) / 86_400_000;
  return days >= 0 && days <= NEW_DAYS;
}

function teamUnlockIcon(u: TeamUnlock): string {
  const code = u.rewardCode.toLowerCase();
  const label = u.labelVi.toLowerCase();
  if (code.includes('movie') || label.includes('phim') || label.includes('movie')) return '🍿';
  return '🎉';
}

function milestoneKind(title: string): 'garden' | 'read' | null {
  const t = title.toLowerCase();
  if ((t.includes('tưới') || t.includes('trồng')) && t.includes('cây')) return 'garden';
  if (t.includes('đọc') || t.includes('sách')) return 'read';
  return null;
}

export function memoryFromSaved(entry: FamilyMemoryEntry, now = new Date()): FamilyMemory {
  const at = entry.happenedAt || entry.flowDate;
  const icon = entry.icon || defaultIconForKind(entry.kind, entry.titleVi);
  let filterKind: string = entry.kind;
  if (entry.kind === 'team_unlock') {
    filterKind = isMovieNightMemoryText(entry.titleVi, icon) ? 'movie_night' : 'team_other';
  }
  return {
    id: `saved-${entry.id}`,
    icon,
    title: entry.titleVi,
    date: formatMemoryDate(entry.flowDate || at),
    sortAt: parseSortTime(at),
    isNew: isRecent(at, now) || entry.isFavorite,
    entry,
    photoUrl: entry.photoUrl,
    filterKind,
  };
}

/** Garden bloom / “vườn nở” moments for kid history. */
export function isGardenBloomMemory(entry: FamilyMemoryEntry): boolean {
  if (entry.icon === '🌸') return true;
  if (/vườn.*nở|đã nở|vườn nở/i.test(entry.titleVi)) return true;
  return entry.kind === 'first_time' && /vườn/i.test(entry.titleVi);
}

function defaultIconForKind(kind: string, titleVi = ''): string {
  switch (kind) {
    case 'beautiful_day':
      return '🌤️';
    case 'streak_milestone':
      return '🔥';
    case 'gratitude':
      return '💌';
    case 'photo':
      return '📸';
    case 'team_unlock':
      return isMovieNightMemoryText(titleVi) ? '🍿' : '🎉';
    case 'reward':
      return '🎁';
    case 'first_time':
      return '🌱';
    case 'help':
      return '🤝';
    case 'team_day':
      return '🏠';
    case 'parent_habit':
      return '🌿';
    default:
      return '💛';
  }
}

export function buildFamilyMemories(opts: {
  childShort: string;
  redemptions: RewardRedemption[];
  teamUnlocks: TeamUnlock[];
  doneItems?: DayFlowCommitment[];
  saved?: FamilyMemoryEntry[];
  voice?: 'kid' | 'parent';
  now?: Date;
}): FamilyMemory[] {
  const {
    childShort,
    redemptions,
    teamUnlocks,
    doneItems = [],
    saved = [],
    voice = 'parent',
    now = new Date(),
  } = opts;
  const out: FamilyMemory[] = [];
  const seenTitles = new Set<string>();

  for (const entry of saved) {
    const mem = memoryFromSaved(entry, now);
    out.push(mem);
    seenTitles.add(normalizeTitle(mem.title));
  }

  for (const r of redemptions) {
    const at = r.fulfilledAt ?? r.createdAt;
    const pending = r.status === 'pending';
    const title =
      voice === 'kid'
        ? pending
          ? `Đổi ${r.title} (chờ bố mẹ xác nhận)`
          : `Đổi ${r.title}`
        : pending
          ? `${childShort} đổi ${r.title} (chờ xác nhận)`
          : `${childShort} đổi ${r.title}`;
    if (seenTitles.has(normalizeTitle(title))) continue;
    out.push({
      id: `redeem-${r.id}`,
      icon: r.icon || '🎁',
      title,
      date: formatMemoryDate(at),
      sortAt: parseSortTime(at),
      isNew: isRecent(at, now),
      pending,
      filterKind: 'reward',
    });
  }

  for (const u of teamUnlocks) {
    if (u.status === 'deferred') continue;
    const at = u.confirmedAt ?? `${u.flowDate}T12:00:00`;
    const pending = u.status === 'pending_confirm';
    const label = u.labelVi || 'Đêm xem phim';
    const title =
      voice === 'kid'
        ? pending
          ? `${label} — chờ bố mẹ xác nhận`
          : `${label} cả nhà`
        : pending
          ? `${label} — chờ xác nhận`
          : `${label} cả nhà`;
    if (seenTitles.has(normalizeTitle(title))) continue;
    const movie = isMovieNightMemoryText(title, null, u.rewardCode);
    out.push({
      id: `unlock-${u.id}`,
      icon: teamUnlockIcon(u),
      title,
      date: formatMemoryDate(at),
      sortAt: parseSortTime(at),
      isNew: isRecent(at, now),
      pending,
      locked: pending,
      filterKind: movie ? 'movie_night' : 'team_other',
    });
  }

  const seenMilestones = new Set<string>();
  for (const c of doneItems) {
    if (c.status !== 'done') continue;
    const kind = milestoneKind(c.title);
    if (!kind || seenMilestones.has(kind)) continue;
    seenMilestones.add(kind);
    const at = c.completedAt ?? `${now.toISOString().slice(0, 10)}T12:00:00`;
    const title =
      kind === 'garden' ? `${childShort} trồng cây lần đầu` : `Đọc sách cùng bố`;
    if (seenTitles.has(normalizeTitle(title))) continue;
    out.push({
      id: `milestone-${kind}-${c.id}`,
      icon: kind === 'garden' ? '🌱' : '📚',
      title,
      date: formatMemoryDate(at),
      sortAt: parseSortTime(at),
      isNew: isRecent(at, now),
      filterKind: 'first_time',
    });
  }

  return out.sort((a, b) => {
    const favA = a.entry?.isFavorite ? 1 : 0;
    const favB = b.entry?.isFavorite ? 1 : 0;
    if (favA !== favB) return favB - favA;
    return b.sortAt - a.sortAt;
  });
}

function normalizeTitle(title: string): string {
  return title.trim().toLowerCase().replace(/\s+/g, ' ');
}

/** Prefer favorited / photo / newer when several cards share the same title. */
export function pickDistinctMemories(items: FamilyMemory[], limit: number): FamilyMemory[] {
  const score = (m: FamilyMemory) =>
    (m.entry?.isFavorite ? 8 : 0) +
    (m.photoUrl?.trim() ? 4 : 0) +
    (m.isNew ? 1 : 0);

  const bestByTitle = new Map<string, FamilyMemory>();
  for (const m of items) {
    const key = normalizeTitle(m.title);
    const prev = bestByTitle.get(key);
    if (!prev || score(m) > score(prev) || (score(m) === score(prev) && m.sortAt > prev.sortAt)) {
      bestByTitle.set(key, m);
    }
  }

  const out: FamilyMemory[] = [];
  const used = new Set<string>();
  for (const m of items) {
    const key = normalizeTitle(m.title);
    if (used.has(key)) continue;
    const best = bestByTitle.get(key);
    if (!best) continue;
    used.add(key);
    out.push(best);
    if (out.length >= limit) break;
  }
  return out;
}

export const FAMILY_MEMORY_EMPTY =
  'Chưa có kỷ niệm — khi nhà mở Đêm xem phim, đổi quà hoặc có ngày đẹp sẽ hiện ở đây';
