import type {
  DayFlow,
  DayFlowCommitment,
  FamilyBehaviorTwin,
  FamilyScore,
} from '@/shared/api/family-os.api';

export type ParentPulse = {
  familyScore: number;
  scoreSource: 'twin' | 'family_score' | 'heuristic';
  headlineVi: string;
  nudgeLineVi: string;
  autonomyLineVi: string;
  peaceLineVi: string;
  dayMoodVi: string;
  insightVi?: string;
  confidence: 'high' | 'medium' | 'low';
  nudgeTrend: 'down' | 'up' | 'flat' | 'unknown';
};

export type CoachInsightLite = {
  headline: string;
  strength?: string | null;
  proposal?: string | null;
};

function clampScore(n: number) {
  return Math.max(0, Math.min(100, Math.round(n)));
}

function isLearningTitle(title: string) {
  const t = title.toLowerCase();
  return /học|bài|toán|đọc|sách|luyện/.test(t);
}

function learningDoneCount(commitments: DayFlowCommitment[]) {
  return commitments.filter(
    (c) =>
      c.status === 'done' &&
      (c.isLearningMission === true || isLearningTitle(c.title)),
  ).length;
}

function selfStartProxyCount(commitments: DayFlowCommitment[]) {
  return commitments.filter(
    (c) =>
      c.status === 'done' &&
      (c.interventionLevel === 'observe_only' ||
        c.habitStage === 'autonomous' ||
        c.habitStage === 'maintained'),
  ).length;
}

function scoreFromTwin(twin: FamilyBehaviorTwin) {
  return clampScore(
    (twin.familyPeaceIndex +
      twin.familyAutonomyIndex +
      (100 - twin.parentalInterventionIndex)) /
      3,
  );
}

/**
 * P0a Parent Success pulse — 10-second “is today lighter?” narrative.
 * Prefers Family Twin indices; never leads with stars/routine counts.
 */
export function buildParentPulse(input: {
  flow: DayFlow;
  twin: FamilyBehaviorTwin | null;
  familyScore: FamilyScore | null;
  nudgeToday: number;
  nudgeYesterday: number;
  coachInsight?: CoachInsightLite | null;
}): ParentPulse {
  const { flow, twin, familyScore, nudgeToday, nudgeYesterday, coachInsight } = input;
  const commitments = flow.commitments;

  let familyScoreValue: number;
  let scoreSource: ParentPulse['scoreSource'];
  if (twin) {
    familyScoreValue = scoreFromTwin(twin);
    scoreSource = 'twin';
  } else if (familyScore) {
    familyScoreValue = clampScore(familyScore.score);
    scoreSource = 'family_score';
  } else {
    const total = Math.max(1, commitments.length);
    const done = commitments.filter((c) => c.status === 'done').length;
    const observe = commitments.filter((c) => c.interventionLevel === 'observe_only').length;
    familyScoreValue = clampScore((done / total) * 55 + (observe / total) * 45);
    scoreSource = 'heuristic';
  }

  let nudgeTrend: ParentPulse['nudgeTrend'] = 'unknown';
  if (nudgeYesterday > 0 || nudgeToday > 0) {
    if (nudgeToday < nudgeYesterday) nudgeTrend = 'down';
    else if (nudgeToday > nudgeYesterday) nudgeTrend = 'up';
    else nudgeTrend = 'flat';
  }

  let headlineVi: string;
  if (nudgeTrend === 'down') {
    headlineVi = 'Hôm nay bạn đã phải nhắc con ít hơn hôm qua.';
  } else if (nudgeTrend === 'up') {
    headlineVi = 'Hôm nay nhà hơi cần nhắc nhiều hơn — vẫn còn cơ hội kết thúc nhẹ.';
  } else if (nudgeToday === 0 && (nudgeYesterday > 0 || twin?.observeOnlyActive)) {
    headlineVi = 'Hôm nay bạn chưa phải nhắc — đây là tín hiệu tốt.';
  } else if (twin?.observeOnlyActive) {
    headlineVi = 'Observe-only đang bật — AI đang để nhà tự chạy.';
  } else if (twin && twin.familyPeaceIndex >= 70 && twin.parentalInterventionIndex <= 35) {
    headlineVi = 'Gia đình đang trong nhịp yên — ít can thiệp, nhiều tự chủ.';
  } else {
    headlineVi = 'Hôm nay nhà mình đang thế nào — nhìn nhanh dưới đây.';
  }

  const nudgeLineVi =
    nudgeYesterday > 0
      ? `Cha mẹ phải nhắc: ${nudgeToday} lần (${
          nudgeTrend === 'down' ? '↓' : nudgeTrend === 'up' ? '↑' : '→'
        } từ ${nudgeYesterday})`
      : `Cha mẹ phải nhắc: ${nudgeToday} lần hôm nay`;

  const learningDone = learningDoneCount(commitments);
  const selfStarts = selfStartProxyCount(commitments);
  let autonomyLineVi: string;
  if (learningDone > 0) {
    autonomyLineVi =
      selfStarts > 0
        ? `Con chủ động hoàn thành ${learningDone} việc học · ${selfStarts} việc gần như không cần nhắc`
        : `Con đã xong ${learningDone} việc học/đọc hôm nay`;
  } else if (selfStarts > 0) {
    autonomyLineVi = `${selfStarts} việc hoàn thành ở mức quan sát / tự chủ`;
  } else if (twin) {
    autonomyLineVi = `Chỉ số tự chủ gia đình: ${twin.familyAutonomyIndex}/100`;
  } else {
    autonomyLineVi = 'Chưa có tín hiệu tự học rõ hôm nay — bình thường vào đầu ngày.';
  }

  let peaceLineVi: string;
  if (twin?.dependenceWarning && twin.dependenceWarningVi) {
    peaceLineVi = twin.dependenceWarningVi;
  } else if (twin && twin.familyPeaceIndex >= 75) {
    peaceLineVi = 'Không có tín hiệu căng thẳng nổi bật — Famixa đang yên.';
  } else if (twin) {
    peaceLineVi = `Peace ${twin.familyPeaceIndex} · Intervention ${twin.parentalInterventionIndex} — ${twin.retirementLabelVi}`;
  } else {
    const skipped = commitments.filter((c) => c.status === 'skipped').length;
    peaceLineVi =
      skipped === 0
        ? 'Chưa có skip / xung đột ghi nhận hôm nay.'
        : `${skipped} việc bị bỏ qua — nên hỏi nhẹ thay vì ép.`;
  }

  let dayMoodVi: string;
  if (twin?.dependenceWarning) {
    dayMoodVi = 'Famixa nhắc: đang có nguy cơ nuôi phụ thuộc vào nhắc.';
  } else if (
    nudgeTrend === 'down' ||
    (twin && twin.familyPeaceIndex >= 70 && twin.parentalInterventionIndex <= 40)
  ) {
    dayMoodVi = 'Famixa thấy hôm nay là một ngày tích cực.';
  } else if (nudgeTrend === 'up') {
    dayMoodVi = 'Ngày hơi nặng nhắc — tối nay ưu tiên 1 việc, đừng mở cả list.';
  } else {
    dayMoodVi = familyScore?.headlineVi ?? 'Famixa đang học nhịp nhà trong ngày.';
  }

  const insightVi =
    coachInsight?.strength?.trim() ||
    coachInsight?.proposal?.trim() ||
    coachInsight?.headline?.trim() ||
    undefined;

  const confidence: ParentPulse['confidence'] = twin
    ? 'high'
    : familyScore
      ? 'medium'
      : 'low';

  return {
    familyScore: familyScoreValue,
    scoreSource,
    headlineVi,
    nudgeLineVi,
    autonomyLineVi,
    peaceLineVi,
    dayMoodVi,
    insightVi,
    confidence,
    nudgeTrend,
  };
}
