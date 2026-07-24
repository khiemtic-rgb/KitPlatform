import type { DayFlowCommitment, RewardRedemption, TeamUnlock } from '@/shared/api/family-os.api';

export type FamilyMemory = {
  id: string;
  icon: string;
  title: string;
  date: string;
  sortAt: number;
  isNew?: boolean;
  pending?: boolean;
  locked?: boolean;
};

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

export function buildFamilyMemories(opts: {
  childShort: string;
  redemptions: RewardRedemption[];
  teamUnlocks: TeamUnlock[];
  doneItems?: DayFlowCommitment[];
  voice?: 'kid' | 'parent';
  now?: Date;
}): FamilyMemory[] {
  const {
    childShort,
    redemptions,
    teamUnlocks,
    doneItems = [],
    voice = 'parent',
    now = new Date(),
  } = opts;
  const out: FamilyMemory[] = [];

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
    out.push({
      id: `redeem-${r.id}`,
      icon: r.icon || '🎁',
      title,
      date: formatMemoryDate(at),
      sortAt: parseSortTime(at),
      isNew: isRecent(at, now),
      pending,
    });
  }

  for (const u of teamUnlocks) {
    if (u.status === 'deferred') continue;
    const at = u.confirmedAt ?? `${u.flowDate}T12:00:00`;
    const pending = u.status === 'pending_confirm';
    const label = u.labelVi || 'Movie Night';
    const title =
      voice === 'kid'
        ? pending
          ? `${label} — chờ bố mẹ xác nhận`
          : `${label} cả nhà`
        : pending
          ? `${label} — chờ xác nhận`
          : `${label} cả nhà`;
    out.push({
      id: `unlock-${u.id}`,
      icon: teamUnlockIcon(u),
      title,
      date: formatMemoryDate(at),
      sortAt: parseSortTime(at),
      isNew: isRecent(at, now),
      pending,
      locked: pending,
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
      kind === 'garden'
        ? `${childShort} trồng cây lần đầu`
        : `Đọc sách cùng bố`;
    out.push({
      id: `milestone-${kind}-${c.id}`,
      icon: kind === 'garden' ? '🌱' : '📚',
      title,
      date: formatMemoryDate(at),
      sortAt: parseSortTime(at),
      isNew: isRecent(at, now),
    });
  }

  return out.sort((a, b) => b.sortAt - a.sortAt);
}

export const FAMILY_MEMORY_EMPTY =
  'Chưa có kỷ niệm — khi nhà làm Movie Night hoặc đổi quà sẽ hiện ở đây';
