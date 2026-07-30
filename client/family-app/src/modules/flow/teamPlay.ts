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
