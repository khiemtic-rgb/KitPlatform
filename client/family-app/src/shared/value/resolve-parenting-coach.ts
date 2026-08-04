import type {
  AccountabilityGlance,
  BehaviorCoach,
  DayFlow,
  FamilyBehaviorTwin,
  FamilyCoachInsight,
  FamilyDnaCard,
} from '@/shared/api/family-os.api';
import {
  buildParentingCoach,
  type ParentingCoachAdvice,
  type ParentingCoachScope,
} from '@/shared/value/parenting-coach';
import {
  isBlueprintSparse,
  preferDnaNextStep,
  sparseDnaCta,
  withBlueprintBecause,
} from '@/shared/value/blueprint-context';
import { resolvePlaybookId } from '@/shared/value/family-playbook-ids';

export type CoachTipSlot = 'morning' | 'evening' | 'anytime';

export type ParentCoachTip = ParentingCoachAdvice & {
  id: string;
  slot: CoachTipSlot;
  source: 'server_insight' | 'server_twin' | 'server_behavior' | 'local_fallback' | 'blueprint_sparse';
  titleVi: string;
  /** Wave B: domains cited from Blueprint DNA (values/focus/stage…). */
  blueprintDomains?: string[];
  /** Wave B playbook id (PB0001…). */
  playbookId?: string;
};

export type ResolvedParentCoach = {
  /** Max 2 tips — Parent Success P0b budget */
  tips: ParentCoachTip[];
  /** Primary tip (first) — backward compatible with sheet */
  primary: ParentCoachTip;
  sourceLabelVi: string;
  /** Visible context anchor: "Đang xem Nhi" or "Đang xem Cả nhà". */
  scopeLabelVi: string;
  isServerSot: boolean;
};

/** Chuẩn hoá để so trùng câu chữ (bỏ dấu câu / hoa thường / khoảng trắng thừa). */
function sameCopy(text: string | null | undefined): string {
  return (text ?? '')
    .toLowerCase()
    .replace(/[.,;:!?«»“”"']/g, '')
    .replace(/\s+/g, ' ')
    .trim();
}

function hourInFamilyLocal(now = new Date()): number {
  return now.getHours();
}

function detectSlot(now = new Date()): CoachTipSlot {
  const h = hourInFamilyLocal(now);
  if (h < 12) return 'morning';
  if (h >= 17) return 'evening';
  return 'anytime';
}

function avoidFromProposal(
  proposal: string | undefined,
  attention: string | undefined,
  focusTitle?: string | null,
): string {
  const task = (focusTitle ?? '').trim().toLocaleLowerCase('vi');
  if (/^dậy|thức dậy|rời giường/.test(task)) {
    return 'Đừng gọi dồn nhiều lần hoặc mở ngay cả danh sách việc buổi sáng.';
  }
  if (/đánh răng|bàn chải|súc miệng/.test(task)) {
    return 'Đừng vừa nhắc đánh răng vừa giục mặc đồ, ăn sáng hay chuẩn bị cặp.';
  }
  if (/ăn sáng|bữa sáng/.test(task)) {
    return 'Đừng biến bữa sáng thành lúc kiểm tra toàn bộ việc con chưa làm.';
  }
  if (attention && /bỏ qua|quên|sau giờ|căng/.test(attention.toLowerCase())) {
    return 'Đừng hỏi “học chưa?” lần 2 trong 15 phút — đổi sang một câu mở (dễ nhất / khó nhất).';
  }
  if (proposal && /chuyển|neo|sau/.test(proposal.toLowerCase())) {
    return 'Đừng chỉ nhắc thêm — hãy chỉnh khung giờ nếu pattern đang lặp.';
  }
  return 'Tránh mở cả list kế hoạch khi đang nóng — một việc, một lời nhắc.';
}

function styleFromSlot(slot: CoachTipSlot): string {
  if (slot === 'morning') {
    return 'Buổi sáng: hỏi con muốn bắt đầu việc nào trước, rồi chỉ nhắc đúng việc đó.';
  }
  if (slot === 'evening') {
    return 'Buổi tối: nếu con mệt, đừng ép học thêm — giữ một việc nhỏ hoặc nghỉ có chủ đích.';
  }
  return 'Giọng ngắn, cụ thể, dừng sau một lần nhắc.';
}

/** Cách tương tác phải khớp việc đang nóng, không chỉ khớp giờ trong ngày. */
function styleFromTask(
  title: string | null | undefined,
  slot: CoachTipSlot,
  who = 'con',
): string {
  const task = (title ?? '').trim().toLocaleLowerCase('vi');
  if (/^dậy|thức dậy|rời giường/.test(task)) {
    return `Với ${who}: gọi một câu ngắn, mở rèm hoặc ánh sáng nhẹ, rồi cho con 2 phút tự ngồi dậy.`;
  }
  if (/đánh răng|bàn chải|súc miệng/.test(task)) {
    return `Với ${who}: hỏi “đánh răng trước hay rửa mặt trước?” để con tự chọn bước bắt đầu.`;
  }
  if (/ăn sáng|bữa sáng/.test(task)) {
    return `Với ${who}: báo còn bao nhiêu phút trước giờ đi; tránh vừa giục ăn vừa nhắc việc khác.`;
  }
  if (/bài tập|học|đọc|môn/.test(task)) {
    return `Với ${who}: hỏi “phần nào dễ bắt đầu nhất?” thay vì hỏi chung “học xong chưa?”.`;
  }
  if (/cặp|balo|chuẩn bị đồ/.test(task)) {
    return `Với ${who}: chia thành ba bước ngắn — sách, đồ dùng, hộp nước — rồi để con tự kiểm tra.`;
  }
  if (/ngủ|lên giường/.test(task)) {
    return `Với ${who}: báo trước 10 phút, giảm ánh sáng và dừng màn hình; không mở thêm cuộc tranh luận.`;
  }
  return styleFromSlot(slot);
}

/**
 * Map server FamilyCoachInsight → parent-facing tip (SoT).
 * Server headline often counts tasks — we prefer strength/proposal/attention for parent emotion.
 */
export function tipFromCoachInsight(
  insight: FamilyCoachInsight,
  slot: CoachTipSlot,
  context?: {
    scope: ParentingCoachScope;
    doneCount: number;
    totalCount: number;
    overdueCount: number;
    topAttentionTitle?: string;
  },
): ParentCoachTip | null {
  const scopedOverdueLine =
    context && context.overdueCount > 0
      ? `${context.scope.labelVi} có ${context.overdueCount} việc quá giờ${
          context.topAttentionTitle ? `; ưu tiên «${context.topAttentionTitle}»` : ''
        }.`
      : '';
  const insightLine =
    scopedOverdueLine ||
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

  const playbookId =
    insight.playbookId ||
    resolvePlaybookId({
      proposalCode: insight.proposalCode,
      focusTitle: insight.focusCommitmentTitle,
      patternForgotCount: insight.patternForgotCount,
    }) ||
    undefined;

  // Chỉ nói bằng ngôn ngữ bố mẹ hiểu: không lộ mã playbook / tên hệ thống.
  const basedParts = [
    insight.blueprintBecauseVi?.trim() || null,
    // Server pattern/attention currently summarizes Cả nhà. Hide it when the UI
    // has already projected the tip to one selected child.
    context?.scope.kind === 'child' ? null : insight.pattern?.trim() || null,
    context?.scope.kind === 'child' ? null : insight.attention?.trim() || null,
    (context?.totalCount ?? insight.totalCount) > 0
      ? `${context?.scope.labelVi ?? 'Cả nhà'} hôm nay ${context?.doneCount ?? insight.doneCount}/${context?.totalCount ?? insight.totalCount} việc đã xong`
      : null,
  ].filter(Boolean);

  return {
    id: 'server-insight',
    slot,
    source: 'server_insight',
    titleVi: slot === 'morning' ? 'Gợi ý sáng' : slot === 'evening' ? 'Gợi ý tối' : 'Gợi ý hôm nay',
    childProfile: `${context?.scope.labelVi ?? 'Cả nhà'} · Famixa đồng hành từ dữ liệu nhà mình`,
    basedOn: basedParts.join(' · '),
    insight:
      insightLine ||
      (slot === 'morning'
        ? 'Sáng nay ưu tiên cách hỏi — không ưu tiên kiểm soát checklist.'
        : 'Hôm nay nhà cần một tương tác nhẹ hơn là thêm nhắc.'),
    doThis:
      doThis ||
      'Chọn một việc nóng nhất, nhắc một lần, rồi để yên 10–15 phút.',
    avoid: avoidFromProposal(
      insight.proposal,
      insight.attention,
      context?.topAttentionTitle ?? insight.focusCommitmentTitle,
    ),
    styleTip: styleFromTask(
      context?.topAttentionTitle ?? insight.focusCommitmentTitle,
      slot,
      context?.scope.labelVi ?? 'con',
    ),
    confidence: insight.totalCount > 0 ? 82 : 55,
    playbookId,
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
    titleVi: 'Gợi ý tối',
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
  scope?: ParentingCoachScope,
): ParentCoachTip | null {
  if (!behaviorCoach?.hints?.length) return null;
  const scopedHints =
    scope?.kind === 'child'
      ? behaviorCoach.hints.filter(
          (h) =>
            !h.memberName ||
            sameCopy(h.memberName).includes(sameCopy(scope.childName)) ||
            sameCopy(scope.childName).includes(sameCopy(h.memberName)),
        )
      : behaviorCoach.hints;
  if (!scopedHints.length) return null;
  const hint =
    scopedHints.find((h) => h.allowParentPush && h.parentAdviceVi) ??
    scopedHints.find((h) => h.parentAdviceVi) ??
    scopedHints[0];
  if (!hint?.parentAdviceVi?.trim()) return null;

  const who = hint.memberName?.trim() || 'Con';
  return {
    id: `server-behavior-${hint.commitmentId}`,
    slot,
    source: 'server_behavior',
    titleVi: 'Lời khuyên hôm nay',
    childProfile: `${who} · ${hint.interventionLabelVi || hint.interventionLevel}`,
    basedOn:
      scope?.kind === 'child'
        ? `việc «${hint.title}» của ${scope.labelVi}`
        : `việc «${hint.title}» · hôm nay cả nhà đã nhắc ${behaviorCoach.parentNudgesUsedToday}/${behaviorCoach.parentNudgeBudget} lần`,
    insight: `Việc «${hint.title}» đang ở mức ${hint.interventionLabelVi || hint.interventionLevel}.`,
    doThis: hint.parentAdviceVi,
    avoid:
      hint.interventionLevel === 'observe_only'
        ? 'Đừng nhắc thêm — lúc này chỉ nên quan sát.'
        : 'Đừng tăng lên quát hay ép khi chưa thử cách trên.',
    styleTip: styleFromTask(hint.title, slot, scope?.labelVi ?? who),
    confidence: 80,
  };
}

function tipFromLocalFallback(input: {
  familyId: string;
  flow: DayFlow;
  glance: AccountabilityGlance | null;
  nudgeToday: number;
  focusChildName?: string | null;
  scope: ParentingCoachScope;
  slot: CoachTipSlot;
}): ParentCoachTip {
  const local = buildParentingCoach(input);
  return {
    ...local,
    id: 'local-fallback',
    slot: input.slot,
    source: 'local_fallback',
    titleVi: 'Gợi ý tạm',
    styleTip: local.styleTip || styleFromSlot(input.slot),
  };
}

/**
 * P0b: server coach-insight (+ Twin evening / behavior coach) = SoT.
 * Local Foxy heuristic only if server tips missing.
 * Wave B: always annotate with Blueprint DNA when present.
 * Hard cap: 2 tips.
 */
export function resolveParentCoach(input: {
  familyId: string;
  flow: DayFlow;
  glance: AccountabilityGlance | null;
  nudgeToday: number;
  focusChildName?: string | null;
  scope?: ParentingCoachScope;
  coachInsight: FamilyCoachInsight | null;
  familyTwin: FamilyBehaviorTwin | null;
  behaviorCoach?: BehaviorCoach | null;
  /** Wave B — DNA card; Coach must read Blueprint before speaking. */
  dna?: FamilyDnaCard | null;
  now?: Date;
}): ResolvedParentCoach {
  const slot = detectSlot(input.now);
  const tips: ParentCoachTip[] = [];
  const dna = input.dna ?? null;
  const sparse = isBlueprintSparse(dna);
  const scope: ParentingCoachScope =
    input.scope ??
    (input.focusChildName
      ? {
          kind: 'child',
          labelVi: input.focusChildName,
          childName: input.focusChildName,
        }
      : { kind: 'family', labelVi: 'Cả nhà' });

  if (sparse) {
    const cta = sparseDnaCta();
    tips.push({
      id: 'blueprint-sparse-pb0020',
      slot,
      source: 'blueprint_sparse',
      titleVi: cta.titleVi,
      childProfile: 'Famixa · đang tìm hiểu nhà mình',
      basedOn: 'Famixa chưa có đủ thông tin về giá trị và nhịp sống của nhà bạn',
      insight: cta.moodLineVi,
      doThis: cta.doThisVi,
      avoid: 'Đừng làm theo lời khuyên chung chung — mỗi nhà một nhịp khác nhau.',
      styleTip: styleFromSlot(slot),
      confidence: 90,
      playbookId: cta.playbookId,
    });
  }

  const scopedDoneCount = input.flow.commitments.filter((c) => c.status === 'done').length;
  const scopedTotalCount = input.flow.commitments.length;
  const scopedOverdue = input.flow.commitments.filter(
    (c) => c.status !== 'done' && c.status !== 'skipped' && c.reminderState === 'overdue',
  );
  const fromInsight = !sparse && input.coachInsight
    ? tipFromCoachInsight(input.coachInsight, slot, {
        scope,
        doneCount: scopedDoneCount,
        totalCount: scopedTotalCount,
        overdueCount: scopedOverdue.length,
        topAttentionTitle: scopedOverdue[0]?.title,
      })
    : null;
  if (fromInsight && tips.length < 2) tips.push(fromInsight);

  const fromTwin = !sparse ? tipFromTwinEvening(input.familyTwin, slot) : null;
  if (fromTwin && tips.length < 2) tips.push(fromTwin);

  if (!sparse && tips.length < 2) {
    const fromBehavior = tipFromBehaviorCoach(input.behaviorCoach ?? null, slot, scope);
    if (fromBehavior) {
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
        scope,
        slot,
      }),
    );
  }

  const usedDoThis = new Set<string>();
  const usedStyleTip = new Set<string>();
  const capped = tips.slice(0, 2).map((tip) => {
    if (tip.source === 'blueprint_sparse') {
      usedDoThis.add(sameCopy(tip.doThis));
      if (tip.styleTip) usedStyleTip.add(sameCopy(tip.styleTip));
      return tip;
    }
    const merged = withBlueprintBecause(
      [
        tip.basedOn,
        input.coachInsight?.blueprintBecauseVi,
      ]
        .filter(Boolean)
        .join(' · ') || tip.basedOn,
      dna,
    );
    // Không lặp lại câu nhận định ngay bên dưới chính nó.
    const basedOn = merged
      .split(' · ')
      .filter((part) => sameCopy(part) && sameCopy(part) !== sameCopy(tip.insight))
      .join(' · ');
    const playbookId =
      tip.playbookId ||
      resolvePlaybookId({
        blueprintSparse: sparse,
        proposalCode: input.coachInsight?.proposalCode,
        focusTitle: input.coachInsight?.focusCommitmentTitle,
        patternForgotCount: input.coachInsight?.patternForgotCount,
      }) ||
      undefined;
    // DNA next step chỉ thay được 1 tip — 2 tip trùng lời khuyên thì vô nghĩa.
    const preferred = preferDnaNextStep(tip.doThis, dna);
    const doThis = usedDoThis.has(sameCopy(preferred)) ? tip.doThis : preferred;
    usedDoThis.add(sameCopy(doThis));
    // Cách tương tác chỉ nói một lần cho cả phiên, không lặp ở tip thứ hai.
    const styleKey = sameCopy(tip.styleTip);
    const styleTip = styleKey && usedStyleTip.has(styleKey) ? '' : tip.styleTip;
    if (styleKey) usedStyleTip.add(styleKey);
    return {
      ...tip,
      // playbookId giữ lại cho analytics/QA, không hiển thị thành chữ cho bố mẹ.
      basedOn,
      doThis,
      styleTip,
      playbookId,
    };
  });
  const isServerSot = capped.some(
    (t) => t.source !== 'local_fallback' && t.source !== 'blueprint_sparse',
  );
  return {
    tips: capped,
    primary: capped[0],
    isServerSot,
    scopeLabelVi: `Đang xem ${scope.labelVi}`,
    sourceLabelVi: sparse
      ? 'Famixa · chưa đủ dữ liệu nhà mình'
      : isServerSot
        ? dna?.hasBlueprint
          ? 'Famixa · dựa trên hồ sơ nhà mình'
          : 'Famixa · dựa trên tín hiệu hôm nay'
        : 'Famixa · gợi ý tạm (đang học nhịp nhà)',
  };
}
