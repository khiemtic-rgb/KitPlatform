import type { FamilyMemoryEntry } from '@/shared/api/family-os.api';

export type JourneyMilestone = {
  id: string;
  date: string;
  icon: string;
  title: string;
  detail: string;
  kind: 'first' | 'streak' | 'habit' | 'family' | 'moment';
};

function formatVi(iso: string): string {
  const d = new Date(`${iso}T12:00:00`);
  if (Number.isNaN(d.getTime())) return iso;
  return `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`;
}

function kindFromMemory(kind: string): JourneyMilestone['kind'] {
  switch (kind) {
    case 'streak_milestone':
    case 'beautiful_day':
      return 'streak';
    case 'first_time':
      return 'first';
    case 'team_unlock':
    case 'team_day':
    case 'reward':
    case 'help':
      return 'family';
    case 'gratitude':
    case 'photo':
      return 'moment';
    case 'parent_habit':
      return 'habit';
    default:
      return 'habit';
  }
}

function iconFromMemory(m: FamilyMemoryEntry): string {
  if (m.icon?.trim()) return m.icon.trim();
  switch (m.kind) {
    case 'beautiful_day':
      return '☀️';
    case 'streak_milestone':
      return '🔥';
    case 'gratitude':
      return '💌';
    case 'photo':
      return '📷';
    case 'team_unlock':
      return '🎬';
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
      return '✨';
  }
}

/**
 * Family Journey / Timeline — Memory is the durable SoT.
 * Client glance/flow heuristics are only a thin empty-state fallback.
 */
export function buildFamilyJourneyFromMemories(
  memories: FamilyMemoryEntry[],
  familyName: string,
): JourneyMilestone[] {
  if (memories.length === 0) {
    return [
      {
        id: 'empty',
        date: new Date().toISOString().slice(0, 10),
        icon: '🌱',
        title: 'Hành trình đang mở',
        detail: `Gia đình ${familyName} — kỷ niệm sẽ hiện ở đây khi nhà mình sống thêm vài ngày.`,
        kind: 'family',
      },
    ];
  }

  return [...memories]
    .sort((a, b) => {
      const byDate = b.flowDate.localeCompare(a.flowDate);
      if (byDate !== 0) return byDate;
      return (b.happenedAt || '').localeCompare(a.happenedAt || '');
    })
    .slice(0, 40)
    .map((m) => ({
      id: m.id,
      date: m.flowDate,
      icon: iconFromMemory(m),
      title: m.titleVi,
      detail: [m.noteVi?.trim(), m.memberName?.trim(), formatVi(m.flowDate)]
        .filter(Boolean)
        .join(' · '),
      kind: kindFromMemory(m.kind),
    }));
}

/** @deprecated Prefer buildFamilyJourneyFromMemories — kept for rare offline fallbacks. */
export function buildFamilyJourney(input: {
  flow: { flowDate: string; commitments: Array<{ id: string; title: string; status: string; isLateDone?: boolean; memberName?: string }>; doneCount: number; totalCommitments: number };
  glance: { days?: Array<{ date: string; isBeautifulDay?: boolean }>; currentStreak?: number } | null;
  familyName: string;
}): JourneyMilestone[] {
  const { flow, glance, familyName } = input;
  const items: JourneyMilestone[] = [];
  const days = [...(glance?.days ?? [])].sort((a, b) => a.date.localeCompare(b.date));

  if (days[0]) {
    items.push({
      id: 'start',
      date: days[0].date,
      icon: '🌱',
      title: 'Bắt đầu hành trình Famixa',
      detail: `Gia đình ${familyName} mở ngày đầu tiên.`,
      kind: 'family',
    });
  }

  const firstBeautiful = days.find((d) => d.isBeautifulDay);
  if (firstBeautiful) {
    items.push({
      id: 'beautiful',
      date: firstBeautiful.date,
      icon: '🏆',
      title: 'Ngày đẹp đầu tiên',
      detail: 'Cả nhà giữ được cam kết — Movie Night gần hơn.',
      kind: 'streak',
    });
  }

  if ((glance?.currentStreak ?? 0) >= 7) {
    items.push({
      id: 'streak7',
      date: flow.flowDate,
      icon: '🔥',
      title: `${glance!.currentStreak} ngày Team Streak`,
      detail: 'Chuỗi ngày đẹp — giá trị gắn kết đang tích lũy.',
      kind: 'streak',
    });
  }

  return items.sort((a, b) => b.date.localeCompare(a.date)).slice(0, 12);
}
