/** Family Team Play — client helpers (TP0–TP1 prototype). */

export type TeamChildSlice = {
  id: string;
  name: string;
  total: number;
  done: number;
  open: number;
  skipped: number;
};

export type TeamDaySnapshot = {
  teamDone: number;
  teamTotal: number;
  teamPercent: number;
  remainingMissions: number;
  teamComplete: boolean;
  heroMissionLine: string;
};

type CommitmentLike = {
  memberId?: string | null;
  status: string;
};

export function buildTeamDayFromChildren(children: TeamChildSlice[]): TeamDaySnapshot {
  const active = children.filter((c) => c.total > 0);
  const teamDone = active.reduce((s, c) => s + c.done, 0);
  const teamTotal = active.reduce((s, c) => s + c.total, 0);
  const remainingMissions = active.reduce(
    (s, c) => s + Math.max(0, c.total - c.done - c.skipped),
    0,
  );
  const teamPercent = teamTotal > 0 ? Math.round((teamDone / teamTotal) * 100) : 0;

  let heroMissionLine: string;
  if (teamTotal === 0) {
    heroMissionLine = 'Hôm nay nhà chưa có Mission — mình nghỉ vui cũng được!';
  } else if (remainingMissions === 0) {
    heroMissionLine = '🎉 Mission Complete! Cả đội đã xong ngày hôm nay.';
  } else if (remainingMissions === 1) {
    heroMissionLine = '🎯 Cả đội còn 1 Mission nữa để hoàn thành ngày hôm nay.';
  } else {
    heroMissionLine = `🎯 Cả đội còn ${remainingMissions} Mission nữa để hoàn thành ngày hôm nay.`;
  }

  return {
    teamDone,
    teamTotal,
    teamPercent,
    remainingMissions,
    teamComplete: teamTotal > 0 && remainingMissions === 0,
    heroMissionLine,
  };
}

/** Fixed house line for parent Home — never names a lagging child. */
export function familyTeamHeroLine(percent: number, remaining: number, teamTotal = 1): string {
  if (teamTotal <= 0) return 'Cả nhà hôm nay chưa có Mission.';
  if (remaining <= 0) return `Cả nhà hôm nay ${percent}% · đã xong ngày.`;
  if (remaining === 1) return `Cả nhà hôm nay ${percent}% · còn 1 việc.`;
  return `Cả nhà hôm nay ${percent}% · còn ${remaining} việc.`;
}

/** Light DNA/brief tip from school stage — sibling nudge roles (Đợt D). */
export function roleMatrixBriefTip(stageLabelVi?: string | null): string | null {
  const s = (stageLabelVi ?? '').toLowerCase();
  if (!s.trim()) return null;
  if (s.includes('mầm') || s.includes('nhà trẻ') || s.includes('mẫu giáo')) {
    return 'Con nhỏ chủ yếu được nhắc nhẹ — chưa mời làm người gửi lời nhắc.';
  }
  if (s.includes('tiểu')) {
    return 'Tiểu học: khi đã xong 100% có thể nhắc em — giữ giọng cổ vũ, không duyệt thay bố mẹ.';
  }
  if (s.includes('thcs') || s.includes('trung học cơ')) {
    return 'THCS: ưu tiên con đã xong / lớn hơn làm người nhắc anh chị em.';
  }
  if (s.includes('thpt') || s.includes('trung học phổ')) {
    return 'THPT: con lớn có thể dẫn đội bằng lời nhắc ngắn — vẫn không gắn tên lên hero nhà.';
  }
  return null;
}

export type NudgeTemplateOption = {
  code: 'cheer_up' | 'one_left' | 'you_got_this';
  title: string;
  hint: string;
};

/** Fixed nudge phrasings — same set the API accepts (FamilyTeamNudgeTemplates). */
export const NUDGE_TEMPLATE_OPTIONS: NudgeTemplateOption[] = [
  { code: 'cheer_up', title: 'Nhắc nhẹ', hint: 'Rủ nhau cùng xong ngày hôm nay' },
  { code: 'one_left', title: 'Còn 1 việc nữa thôi', hint: 'Khi cả đội gần về đích' },
  { code: 'you_got_this', title: 'Tin em làm được', hint: 'Cổ vũ khi em đang ngại bắt đầu' },
];

/** Mirrors FamilyTeamNudgeTemplates.MessageVi so the sheet can preview the exact text. */
export function nudgeMessagePreview(
  template: string,
  fromShort: string,
  toShort: string,
): string {
  switch (template) {
    case 'one_left':
      return `${toShort} ơi, cả đội còn 1 việc nữa thôi — ${fromShort} cổ vũ em!`;
    case 'you_got_this':
      return `${toShort} cố lên nhé! ${fromShort} tin em làm được.`;
    default:
      return `${toShort} ơi, ${fromShort} nhắc nhẹ — mình cùng xong ngày hôm nay nhé!`;
  }
}

export function slicesFromCommitments(
  children: { id: string; name: string }[],
  commitments: CommitmentLike[],
): TeamChildSlice[] {
  return children.map((ch) => {
    const mine = commitments.filter((c) => c.memberId === ch.id);
    const done = mine.filter((c) => c.status === 'done').length;
    const skipped = mine.filter((c) => c.status === 'skipped').length;
    const open = mine.filter((c) => c.status !== 'done' && c.status !== 'skipped').length;
    return {
      id: ch.id,
      name: ch.name,
      total: mine.length,
      done,
      open,
      skipped,
    };
  });
}
