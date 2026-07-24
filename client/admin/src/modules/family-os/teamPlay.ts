/** Family Team Play — client helpers (TP0–TP1 prototype, no API yet). */

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
  childrenWithMissions: number;
  children: TeamChildSlice[];
  /** Team-facing copy — never names the lagging child on the hero. */
  heroMissionLine: string;
};

export type CooperationScoreStub = {
  score: number;
  teamCompletion: number;
  familyStreak: number;
  helpEachOther: number;
  teamUnlock: number;
  familyHarmony: number;
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
  const teamComplete = teamTotal > 0 && remainingMissions === 0;

  let heroMissionLine: string;
  if (teamTotal === 0) {
    heroMissionLine = 'Hôm nay nhà chưa có Mission — mở nhịp sống để cùng bắt đầu.';
  } else if (teamComplete) {
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
    teamComplete,
    childrenWithMissions: active.length,
    children: active,
    heroMissionLine,
  };
}

/** Aggregate child commitments from a day flow (role filter applied by caller). */
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

export function computeCooperationScoreStub(args: {
  team: TeamDaySnapshot;
  currentStreak: number;
  todayBeautiful: boolean;
  weekBeautifulDays: number;
  weekDayCount: number;
  teamOverdueOrHot?: number;
}): CooperationScoreStub {
  const { team, currentStreak, todayBeautiful, weekBeautifulDays, weekDayCount, teamOverdueOrHot = 0 } =
    args;

  const teamCompletion =
    weekDayCount > 0
      ? Math.round((weekBeautifulDays / weekDayCount) * 100)
      : team.teamPercent;

  const familyStreak = Math.min(100, currentStreak * 8 + (todayBeautiful ? 10 : 0));
  const helpEachOther = 0; // TP3+
  const teamUnlock = todayBeautiful || team.teamComplete ? 40 : 0;
  const familyHarmony = Math.max(
    0,
    100 - teamOverdueOrHot * 18 - (team.remainingMissions > 3 ? 10 : 0),
  );

  const score = Math.round(
    teamCompletion * 0.35 +
      familyStreak * 0.25 +
      helpEachOther * 0.2 +
      teamUnlock * 0.1 +
      familyHarmony * 0.1,
  );

  return {
    score: Math.max(0, Math.min(100, score)),
    teamCompletion,
    familyStreak,
    helpEachOther,
    teamUnlock,
    familyHarmony,
  };
}
