import type { AccountabilityGlance, DayFlow } from '@/shared/api/family-os.api';

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

/** Family Journey — accumulating timeline parents stay for years. */
export function buildFamilyJourney(input: {
  flow: DayFlow;
  glance: AccountabilityGlance | null;
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

  const onTime = flow.commitments.filter((c) => c.status === 'done' && !c.isLateDone);
  for (const c of onTime.slice(0, 3)) {
    const title = c.title.toLowerCase();
    let icon = '⭐';
    let headline = `Tự hoàn thành «${c.title}»`;
    if (title.includes('cặp') || title.includes('balo')) {
      icon = '❤️';
      headline = 'Lần đầu / hôm nay tự chuẩn bị cặp';
    } else if (title.includes('răng')) {
      icon = '🪥';
      headline = 'Tự đánh răng đúng nhịp';
    } else if (title.includes('đọc') || title.includes('sách')) {
      icon = '📚';
      headline = 'Đọc sách — trang nhật ký trưởng thành';
    }
    items.push({
      id: `done-${c.id}`,
      date: flow.flowDate,
      icon,
      title: headline,
      detail: `${c.memberName?.trim() || 'Con'} · ${formatVi(flow.flowDate)}`,
      kind: title.includes('cặp') ? 'first' : 'habit',
    });
  }

  if (flow.doneCount > 0 && flow.doneCount >= flow.totalCommitments && flow.totalCommitments > 0) {
    items.push({
      id: 'full-day',
      date: flow.flowDate,
      icon: '🎬',
      title: 'Xong hết cam kết trong ngày',
      detail: 'Sẵn sàng mở Movie Night — thời gian chất lượng cả nhà.',
      kind: 'family',
    });
  }

  // Newest first for feed feel
  return items
    .sort((a, b) => b.date.localeCompare(a.date))
    .slice(0, 12)
    .map((m) => ({ ...m, detail: m.detail.includes('/') ? m.detail : `${m.detail}` }));
}
