import type {
  AccountabilityGlance,
  BehaviorCoach,
  DayFlow,
  FamilyBehaviorTwin,
  FamilyCoachInsight,
} from '@/shared/api/family-os.api';
import {
  buildParentingCoach,
  type ParentingCoachAdvice,
} from '@/shared/value/parenting-coach';

export type CoachTipSlot = 'morning' | 'evening' | 'anytime';

export type ParentCoachTip = ParentingCoachAdvice & {
  id: string;
  slot: CoachTipSlot;
  source: 'server_insight' | 'server_twin' | 'server_behavior' | 'local_fallback';
  titleVi: string;
};

export type ResolvedParentCoach = {
  /** Max 2 tips — Parent Success P0b budget */
  tips: ParentCoachTip[];
  /** Primary tip (first) — backward compatible with sheet */
  primary: ParentCoachTip;
  sourceLabelVi: string;
  isServerSot: boolean;
};

function hourInFamilyLocal(now = new Date()): number {
  return now.getHours();
}

function detectSlot(now = new Date()): CoachTipSlot {
  const h = hourInFamilyLocal(now);
  if (h < 12) return 'morning';
  if (h >= 17) return 'evening';
  return 'anytime';
}

function avoidFromProposal(proposal: string | undefined, attention: string | undefined): string {
  if (attention && /bỏ qua|quên|sau giờ|căng/.test(attention.toLowerCase())) {
    return 'Đừng hỏi “học chưa?” lần 2 trong 15 phút — đổi sang một câu mở (dễ nhất / khó nhất).';
  }
  if (proposal && /chuyển|neo|sau/.test(proposal.toLowerCase())) {
    return 'Đừng chỉ nhắc thêm — hãy chỉnh khung giờ nếu pattern đang lặp.';
  }
  return 'Tránh mở cả list nhiệm vụ khi đang nóng — một việc, một lời nhắc.';
}

function styleFromSlot(slot: CoachTipSlot): string {
  if (slot === 'morning') {
    return 'Buổi sáng: hỏi “Con thấy hôm nay môn nào dễ nhất?” thay vì nhắc điểm số.';
  }
  if (slot === 'evening') {
    return 'Buổi tối: nếu con mệt, đừng ép học thêm — giữ một việc nhỏ hoặc nghỉ có chủ đích.';
  }
  return 'Giọng ngắn, cụ thể, dừng sau một lần nhắc.';
}

/**
 * Map server FamilyCoachInsight → parent-facing tip (SoT).
 * Server headline often counts tasks — we prefer strength/proposal/attention for parent emotion.
 */
export function tipFromCoachInsight(
  insight: FamilyCoachInsight,
  slot: CoachTipSlot,
): ParentCoachTip | null {
  const insightLine =
    insight.strength?.trim() ||
    insight.attention?.trim() ||
    insight.pattern?.trim() ||
    '';
  const doThis =
    insight.proposal?.trim() ||
    (slot === 'morning'
      ? 'Hôm nay đừng mở đầu bằng điểm số — hỏi một câu nhẹ về môn dễ nhất.'
      : slot === 'evening'
        ? 'Tối nay ưu tiên 1 việc hoặc nghỉ nếu tín hiệu mệt — không ép thêm list.'
        : insight.headline?.trim() || '');

  if (!insightLine && !doThis) return null;

  const basedParts = [
    insight.pattern ? `Pattern: ${insight.pattern}` : null,
    insight.attention ? insight.attention : null,
    `Server coach-insight · ${insight.doneCount}/${insight.totalCount} cam kết`,
  ].filter(Boolean);

  return {
    id: 'server-insight',
    slot,
    source: 'server_insight',
    titleVi: slot === 'morning' ? 'Gợi ý sáng' : slot === 'evening' ? 'Gợi ý tối' : 'Gợi ý hôm nay',
    childProfile: 'Famixa · đồng hành bố mẹ từ dữ liệu nhà mình',
    basedOn: basedParts.join(' · '),
    insight:
      insightLine ||
      (slot === 'morning'
        ? 'Sáng nay ưu tiên cách hỏi — không ưu tiên kiểm soát checklist.'
        : 'Hôm nay nhà cần một tương tác nhẹ hơn là thêm nhắc.'),
    doThis:
      doThis ||
      'Chọn một việc nóng nhất, nhắc một lần, rồi để yên 10–15 phút.',
    avoid: avoidFromProposal(insight.proposal, insight.attention),
    styleTip: styleFromSlot(slot),
    confidence: insight.totalCount > 0 ? 82 : 55,
  };
}

export function tipFromTwinEvening(
  twin: FamilyBehaviorTwin | null,
  slot: CoachTipSlot,
): ParentCoachTip | null {
  if (!twin || slot !== 'evening') return null;
  const risky = twin.children.find(
    (c) => c.eveningRiskBand === 'high' || c.eveningRiskBand === 'medium',
  );
  if (!risky?.eveningSuggestedActionVi && !risky?.eveningRiskLabelVi) return null;

  const who = risky.memberName?.trim() || 'Con';
  return {
    id: 'server-twin-evening',
    slot: 'evening',
    source: 'server_twin',
    titleVi: 'Famixa · gợi ý tối',
    childProfile: `${who} · Famixa đang lắng nghe`,
    basedOn: [
      risky.eveningRiskLabelVi,
      ...(risky.eveningReasonsVi ?? []).slice(0, 2),
    ]
      .filter(Boolean)
      .join(' · '),
    insight:
      risky.eveningRiskLabelVi ||
      `${who} đang có tín hiệu buổi tối cần nhẹ tay.`,
    doThis:
      risky.eveningSuggestedActionVi ||
      'Tối nay không nên ép học thêm — giữ không khí yên.',
    avoid: 'Đừng kiểm tra bài / điểm khi Twin đang báo mệt hoặc rủi ro cao.',
    styleTip: styleFromSlot('evening'),
    confidence: 78,
  };
}

export function tipFromBehaviorCoach(
  behaviorCoach: BehaviorCoach | null,
  slot: CoachTipSlot,
): ParentCoachTip | null {
  if (!behaviorCoach?.hints?.length) return null;
  const hint =
    behaviorCoach.hints.find((h) => h.allowParentPush && h.parentAdviceVi) ??
    behaviorCoach.hints.find((h) => h.parentAdviceVi) ??
    behaviorCoach.hints[0];
  if (!hint?.parentAdviceVi?.trim()) return null;

  const who = hint.memberName?.trim() || 'Con';
  return {
    id: `server-behavior-${hint.commitmentId}`,
    slot,
    source: 'server_behavior',
    titleVi: 'Famixa · lời khuyên',
    childProfile: `${who} · ${hint.interventionLabelVi || hint.interventionLevel}`,
    basedOn: `«${hint.title}» · nudge hôm nay ${behaviorCoach.parentNudgesUsedToday}/${behaviorCoach.parentNudgeBudget}`,
    insight: `Việc «${hint.title}» đang ở mức ${hint.interventionLabelVi || hint.interventionLevel}.`,
    doThis: hint.parentAdviceVi,
    avoid:
      hint.interventionLevel === 'observe_only'
        ? 'Đừng nhắc thêm — Observe-only đang khuyên đứng nhìn.'
        : 'Không escalate lên quát / ép nếu chưa thử lời khuyên này.',
    styleTip: styleFromSlot(slot),
    confidence: 80,
  };
}

function tipFromLocalFallback(input: {
  familyId: string;
  flow: DayFlow;
  glance: AccountabilityGlance | null;
  nudgeToday: number;
  focusChildName?: string | null;
  slot: CoachTipSlot;
}): ParentCoachTip {
  const local = buildParentingCoach(input);
  return {
    ...local,
    id: 'local-fallback',
    slot: input.slot,
    source: 'local_fallback',
    titleVi: 'Famixa · gợi ý tạm',
    styleTip: local.styleTip || styleFromSlot(input.slot),
  };
}

/**
 * P0b: server coach-insight (+ Twin evening / behavior coach) = SoT.
 * Local Foxy heuristic only if server tips missing.
 * Hard cap: 2 tips.
 */
export function resolveParentCoach(input: {
  familyId: string;
  flow: DayFlow;
  glance: AccountabilityGlance | null;
  nudgeToday: number;
  focusChildName?: string | null;
  coachInsight: FamilyCoachInsight | null;
  familyTwin: FamilyBehaviorTwin | null;
  behaviorCoach?: BehaviorCoach | null;
  now?: Date;
}): ResolvedParentCoach {
  const slot = detectSlot(input.now);
  const tips: ParentCoachTip[] = [];

  const fromInsight = input.coachInsight
    ? tipFromCoachInsight(input.coachInsight, slot)
    : null;
  if (fromInsight) tips.push(fromInsight);

  const fromTwin = tipFromTwinEvening(input.familyTwin, slot);
  if (fromTwin && tips.length < 2) tips.push(fromTwin);

  if (tips.length < 2) {
    const fromBehavior = tipFromBehaviorCoach(input.behaviorCoach ?? null, slot);
    if (fromBehavior) {
      // Prefer not duplicating same doThis
      const dup = tips.some(
        (t) => t.doThis.trim() === fromBehavior.doThis.trim(),
      );
      if (!dup) tips.push(fromBehavior);
    }
  }

  if (tips.length === 0) {
    tips.push(
      tipFromLocalFallback({
        familyId: input.familyId,
        flow: input.flow,
        glance: input.glance,
        nudgeToday: input.nudgeToday,
        focusChildName: input.focusChildName,
        slot,
      }),
    );
  }

  const capped = tips.slice(0, 2);
  const isServerSot = capped.some((t) => t.source !== 'local_fallback');
  return {
    tips: capped,
    primary: capped[0],
    isServerSot,
    sourceLabelVi: isServerSot
      ? 'Famixa · từ tín hiệu nhà mình'
      : 'Famixa · gợi ý tạm (đang học nhịp nhà)',
  };
}
