import type { AccountabilityGlance, DayFlow } from '@/shared/api/family-os.api';
import { getNudgeCount } from '@/shared/nudge/nudge-stats';
import { averageHealthLastDays, estimateNudgeProxy } from '@/shared/value/family-health-score';

export type WeeklyReview = {
  weekLabel: string;
  autonomyDeltaPct: number;
  nudgeThisWeek: number;
  nudgeLastWeek: number;
  nudgeDelta: number;
  focusNextWeek: string;
  wins: string[];
  coachNote: string;
};

function isoOffset(flowDate: string, daysBack: number): string {
  const d = new Date(`${flowDate}T12:00:00`);
  d.setDate(d.getDate() - daysBack);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export function buildWeeklyReview(input: {
  familyId: string;
  flow: DayFlow;
  glance: AccountabilityGlance | null;
}): WeeklyReview {
  const { familyId, flow, glance } = input;
  const days = [...(glance?.days ?? [])].sort((a, b) => a.date.localeCompare(b.date));
  const thisWeek = days.slice(-7);
  const lastWeek = days.slice(-14, -7);

  const ratio = (list: typeof days) => {
    if (list.length === 0) return 0.5;
    return (
      list.reduce((s, d) => {
        const t = Math.max(1, d.childDone + d.childSkipped + d.childOpen);
        return s + d.childDone / t;
      }, 0) / list.length
    );
  };

  const autoThis = ratio(thisWeek);
  const autoLast = ratio(lastWeek.length ? lastWeek : thisWeek);
  const autonomyDeltaPct = Math.round((autoThis - autoLast) * 100);

  let nudgeThisWeek = 0;
  let nudgeLastWeek = 0;
  for (let i = 0; i < 7; i++) {
    nudgeThisWeek += getNudgeCount(familyId, isoOffset(flow.flowDate, i));
    nudgeLastWeek += getNudgeCount(familyId, isoOffset(flow.flowDate, i + 7));
  }
  if (nudgeThisWeek === 0 && nudgeLastWeek === 0) {
    nudgeThisWeek = Math.round(
      thisWeek.reduce((s, d) => s + estimateNudgeProxy(d), 0) || flow.overdueCount * 2,
    );
    nudgeLastWeek = Math.round(
      lastWeek.reduce((s, d) => s + estimateNudgeProxy(d), 0) || nudgeThisWeek + 3,
    );
  }

  const overdueTitles = flow.commitments
    .filter((c) => c.reminderState === 'overdue')
    .map((c) => c.title.toLowerCase());
  let focusNextWeek = 'Giữ nhịp đánh răng / chuẩn bị cặp — 1 việc cố định mỗi sáng.';
  if (overdueTitles.some((t) => t.includes('ngủ'))) {
    focusNextWeek = 'Tuần tới tập trung giờ đi ngủ — báo thức sớm hơn 10 phút.';
  } else if (overdueTitles.some((t) => t.includes('răng'))) {
    focusNextWeek = 'Tuần tới neo đánh răng ngay sau khi xuống giường.';
  } else if (overdueTitles.some((t) => t.includes('cặp') || t.includes('balo'))) {
    focusNextWeek = 'Tuần tới chuẩn bị cặp ngay sau ăn tối.';
  }

  const wins: string[] = [];
  if (autonomyDeltaPct > 0) wins.push(`Con tự giác hơn khoảng ${autonomyDeltaPct}%.`);
  if (nudgeThisWeek < nudgeLastWeek) {
    wins.push(`Cha mẹ nhắc ít hơn ${nudgeLastWeek - nudgeThisWeek} lần so với tuần trước.`);
  }
  if (glance?.currentStreak) wins.push(`Team Streak đang ở ${glance.currentStreak} ngày.`);
  if (wins.length === 0) wins.push('Nhà đang giữ nhịp — tuần tới chọn 1 thói quen để “thắng nhỏ”.');

  const healthAvg = averageHealthLastDays(familyId, flow.flowDate, 7);

  return {
    weekLabel: 'Tuần này',
    autonomyDeltaPct,
    nudgeThisWeek,
    nudgeLastWeek,
    nudgeDelta: nudgeLastWeek - nudgeThisWeek,
    focusNextWeek,
    wins,
    coachNote:
      healthAvg != null
        ? `Health Score trung bình 7 ngày: ${healthAvg}/100. ${focusNextWeek}`
        : focusNextWeek,
  };
}
