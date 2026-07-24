import type { AccountabilityGlance, DayFlow, DayFlowCommitment } from '@/shared/api/family-os.api';
import {
  AGE_OPTIONS,
  GOAL_OPTIONS,
  STRUGGLE_OPTIONS,
  getOnboardingProfile,
  type AgeBand,
  type StruggleCode,
} from '@/shared/onboarding/onboarding';
import { getNudgeCount } from '@/shared/nudge/nudge-stats';
import { estimateNudgeProxy } from '@/shared/value/family-health-score';

export type ParentingCoachAdvice = {
  /** Short profile line parents trust */
  childProfile: string;
  /** What data says happened */
  basedOn: string;
  /** The insight (not generic) */
  insight: string;
  /** Concrete next action */
  doThis: string;
  /** What to avoid */
  avoid: string;
  /** Age-appropriate style tip */
  styleTip: string;
  /** Confidence 0–100 from how much data we have */
  confidence: number;
};

export type ParentingCoachFaq = {
  id: string;
  question: string;
  answer: string;
};

function shortName(name: string) {
  return name.split(/\s+/).pop() || name;
}

function isOpen(c: DayFlowCommitment) {
  return c.status !== 'done' && c.status !== 'skipped';
}

function ageStyle(age: AgeBand | undefined): string {
  switch (age) {
    case '4-6':
      return 'Nhắc bằng hình / việc rất ngắn; khen ngay sau khi xong; tránh giọng lớn.';
    case '7-9':
      return 'Một việc tiếp theo + phần thưởng nhà; khen cụ thể, không so sánh anh chị.';
    case '10-12':
      return 'Giao quyền tự chủ + thỏa thuận rõ; hỏi “con tự chọn giờ nào?” thay vì ra lệnh.';
    case '13+':
      return 'Tôn trọng không gian; thỏa thuận hậu quả trước; tránh “hỏi lại lần 3”.';
    default:
      return 'Nhắc một lần, khen cụ thể, dừng khi đã nhắc đủ.';
  }
}

function matchStruggle(title: string, struggles: StruggleCode[]): StruggleCode | null {
  const t = title.toLowerCase();
  if (/đánh răng|rang/.test(t) && struggles.includes('brush_teeth')) return 'brush_teeth';
  if (/ngủ|đi ngủ/.test(t) && struggles.includes('sleep')) return 'sleep';
  if (/bài|học|đọc/.test(t) && struggles.includes('homework')) return 'homework';
  if (/cặp|balo|dọn|gấp/.test(t) && struggles.includes('tidy')) return 'tidy';
  if (/màn hình|tắt/.test(t) && struggles.includes('screen')) return 'screen';
  if (struggles.includes('morning_forget') && /sáng|răng|ăn sáng|cặp/.test(t)) {
    return 'morning_forget';
  }
  return null;
}

function struggleAdvice(code: StruggleCode, who: string): { insight: string; doThis: string; avoid: string } {
  switch (code) {
    case 'brush_teeth':
      return {
        insight: `Theo hồ sơ onboarding, ${who} khó với đánh răng — hôm nay dữ liệu đang khớp pattern đó.`,
        doThis: 'Đặt bàn chải gần giường/cửa phòng; neo “đánh răng ngay sau khi xuống giường”; nhắc tối đa 1 lần.',
        avoid: 'Không đứng hỏi lại nhiều lần hoặc dùng giọng to — dễ thành cuộc chiến.',
      };
    case 'morning_forget':
      return {
        insight: `${who} thuộc nhóm hay quên buổi sáng — việc đang trễ thường dồn vào khung dậy muộn.`,
        doThis: 'Báo thức sớm hơn 10 phút; tối qua chuẩn bị cặp trước; sáng chỉ 1 việc ưu tiên.',
        avoid: 'Đừng mở cả checklist 8 việc lúc 6h45 — não trẻ sẽ “tắt”.',
      };
    case 'homework':
      return {
        insight: `${who} dễ trì hoãn học — khung giờ cố định hiệu quả hơn “làm khi nào xong”.`,
        doThis: 'Timer 20 phút + chỗ học sạch distraction; xong thì tick ngay trên app để nhận sao.',
        avoid: 'Không ngồi cạnh soi từng phút — sat cánh ở đầu/cuối phiên.',
      };
    case 'screen':
      return {
        insight: `Màn hình đang là điểm nóng với ${who} — việc trước ngủ hay bị đẩy.`,
        doThis: 'Thỏa thuận “xong việc rồi mới mở”; tắt màn hình trước ngủ 30 phút (đã có trong starter nếu onboard).',
        avoid: 'Đừng cấm đột ngột không báo trước — dùng thỏa thuận nhà + soft-lock.',
      };
    case 'sleep':
      return {
        insight: `Giờ ngủ của ${who} đang lệch — sáng mai dễ quên việc.`,
        doThis: 'Neo giờ ngủ cố định 7 ngày; giảm kích thích 30 phút trước; khen khi đúng giờ.',
        avoid: 'Tránh đàm phán lại giờ ngủ mỗi tối.',
      };
    case 'tidy':
      return {
        insight: `${who} khó với việc chuẩn bị/dọn — tối làm một lần sẽ cứu sáng hôm sau.`,
        doThis: '“Chuẩn bị cặp ngay sau ăn tối” — gắn với khoảnh khắc khen ngắn.',
        avoid: 'Đừng để sáng mới phát hiện thiếu đồ rồi quát.',
      };
  }
}

/**
 * Parenting Coach grounded on onboarding + today flow + glance/nudges.
 * Rule-based (trustworthy numbers) — not free-form LLM.
 */
export function buildParentingCoach(input: {
  familyId: string;
  flow: DayFlow;
  glance: AccountabilityGlance | null;
  nudgeToday: number;
  focusChildName?: string | null;
}): ParentingCoachAdvice {
  const { familyId, flow, glance, nudgeToday, focusChildName } = input;
  const profile = getOnboardingProfile(familyId);
  const who =
    shortName(focusChildName || profile?.childName || 'Con');
  const struggles = profile?.struggles ?? [];
  const age = profile?.ageBand;
  const goal = profile?.goal;

  const open = flow.commitments.filter(isOpen);
  const overdue = open.filter((c) => c.reminderState === 'overdue');
  const dueNow = open.filter((c) => c.reminderState === 'due_now');
  const hot = overdue[0] ?? dueNow[0];

  const days = [...(glance?.days ?? [])].slice(-7);
  const lateHeavy =
    days.length > 0 &&
    days.filter((d) => d.childLateDone > 0 || d.childOpen > 0).length >= Math.ceil(days.length * 0.5);

  let confidence = 45;
  if (profile && !profile.skipped) confidence += 25;
  if (days.length >= 5) confidence += 15;
  if (hot) confidence += 10;
  confidence = Math.min(95, confidence);

  const ageLabel = AGE_OPTIONS.find((a) => a.value === age)?.label;
  const goalLabel = GOAL_OPTIONS.find((g) => g.value === goal)?.label;
  const struggleLabels = struggles
    .map((s) => STRUGGLE_OPTIONS.find((x) => x.value === s)?.label)
    .filter(Boolean)
    .join(', ');

  const childProfile = profile && !profile.skipped
    ? `${who}${ageLabel ? ` · ${ageLabel}` : ''}${struggleLabels ? ` · khó: ${struggleLabels}` : ''}${goalLabel ? ` · mục tiêu 30 ngày: ${goalLabel}` : ''}`
    : `${who} · chưa onboard đầy đủ — Foxy dùng dữ liệu ngày hôm nay`;

  // 1) Hot mission matching onboarding struggle
  if (hot) {
    const linked = matchStruggle(hot.title, struggles);
    if (linked) {
      const tip = struggleAdvice(linked, who);
      return {
        childProfile,
        basedOn: `Hôm nay «${hot.title}» đang ${hot.reminderState === 'overdue' ? 'quá giờ' : 'đến giờ'} · khớp khó khăn onboarding · đã nhắc ${nudgeToday} lần hôm nay.`,
        insight: tip.insight,
        doThis: tip.doThis,
        avoid: tip.avoid,
        styleTip: ageStyle(age),
        confidence,
      };
    }
    return {
      childProfile,
      basedOn: `«${hot.title}» (${hot.memberName?.trim() || who}) đang cần can thiệp · nhắc hôm nay: ${nudgeToday}.`,
      insight: `${who} đang kẹt ở một việc nóng — ưu tiên một việc trước, đừng mở cả list.`,
      doThis: 'Nhắc nhẹ một lần trên app, rồi dừng. Khi xong: khen cụ thể (“con tự làm được đánh răng”).',
      avoid: 'Không hỏi lại lần 2–3 trong 15 phút.',
      styleTip: ageStyle(age),
      confidence,
    };
  }

  // 2) Goal-oriented when day is calm
  if (goal === 'fewer_nudges' && nudgeToday >= 3) {
    return {
      childProfile,
      basedOn: `Mục tiêu onboarding là giảm nhắc · hôm nay đã nhắc ${nudgeToday} lần.`,
      insight: 'Đang lệch mục tiêu “ít nhắc” — mỗi lần nhắc thêm làm Health Score tụt.',
      doThis: 'Chọn 1 việc duy nhất còn lại; tắt thông báo việc phụ trong 1 giờ.',
      avoid: 'Đừng nhắc “tất cả việc nóng” nếu mục tiêu là giảm áp lực.',
      styleTip: ageStyle(age),
      confidence,
    };
  }

  if (goal === 'bedtime' || struggles.includes('sleep')) {
    const sleep = flow.commitments.find((c) => /ngủ/i.test(c.title));
    if (sleep && isOpen(sleep)) {
      return {
        childProfile,
        basedOn: 'Mục tiêu/khó khăn liên quan giờ ngủ · việc ngủ còn mở.',
        insight: `Giữ giờ ngủ của ${who} ổn định sẽ kéo giảm quên sáng.`,
        doThis: struggleAdvice('sleep', who).doThis,
        avoid: struggleAdvice('sleep', who).avoid,
        styleTip: ageStyle(age),
        confidence,
      };
    }
  }

  if (lateHeavy) {
    return {
      childProfile,
      basedOn: `7 ngày gần đây: nhiều ngày còn việc mở/xong muộn (proxy từ accountability glance).`,
      insight: `${who} đang cần “thắng nhỏ” hơn là thêm mission mới.`,
      doThis: 'Giữ tối đa 4–5 việc/ngày trong 1 tuần; khen đúng giờ quan trọng hơn khen số lượng.',
      avoid: 'Không thêm routine mới khi chuỗi đang yếu.',
      styleTip: ageStyle(age),
      confidence: Math.min(95, confidence + 5),
    };
  }

  const done = flow.commitments.filter((c) => c.status === 'done').length;
  if (done > 0 && open.length === 0) {
    return {
      childProfile,
      basedOn: `Hôm nay phần việc đã xong · streak nhà ${glance?.currentStreak ?? 0} ngày.`,
      insight: `${who} vừa hoàn thành nhịp ngày — đây là lúc củng cố, không phải lúc soi lỗi.`,
      doThis: 'Một lời khen cụ thể + ghi khoảnh khắc. Nếu mục tiêu là thời gian chất lượng: mở Movie Night khi cả nhà sẵn sàng.',
      avoid: 'Đừng ngay lập tức giao thêm việc “đã lỡ”.',
      styleTip: ageStyle(age),
      confidence,
    };
  }

  // Proxy weekly nudges for calm day
  let weekNudge = 0;
  for (let i = 0; i < 7; i++) {
    const d = new Date(`${flow.flowDate}T12:00:00`);
    d.setDate(d.getDate() - i);
    const iso = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    weekNudge += getNudgeCount(familyId, iso);
  }
  if (weekNudge === 0 && days.length) {
    weekNudge = Math.round(days.reduce((s, d) => s + estimateNudgeProxy(d), 0));
  }

  return {
    childProfile,
    basedOn: `Nhịp hôm nay ổn · ước lượng nhắc 7 ngày ≈ ${weekNudge}.`,
    insight:
      goal === 'more_autonomy'
        ? `${who} đang có không gian tự giác — giữ khoảng cách can thiệp.`
        : `Foxy chưa thấy việc nóng — đây là lúc giữ thói quen, không mở rộng checklist.`,
    doThis:
      goal === 'quality_time'
        ? 'Chọn 10 phút đọc/kể chuyện hoặc Movie Night nếu đủ điều kiện mở khóa.'
        : 'Giữ 1 neo cố định (đánh răng hoặc cặp). Chỉ can thiệp khi quá giờ.',
    avoid: 'Tránh thêm việc “cho chắc” khi ngày đang êm.',
    styleTip: ageStyle(age),
    confidence,
  };
}

/** FAQ answers grounded on the same profile — for Value tab. */
export function buildParentingCoachFaqs(input: {
  familyId: string;
  flow: DayFlow;
  glance: AccountabilityGlance | null;
  nudgeToday: number;
}): ParentingCoachFaq[] {
  const advice = buildParentingCoach(input);
  const profile = getOnboardingProfile(input.familyId);
  const who = shortName(profile?.childName || 'con');
  const faqs: ParentingCoachFaq[] = [
    {
      id: 'now',
      question: `Bây giờ mình nên làm gì với ${who}?`,
      answer: `${advice.doThis} (${advice.avoid})`,
    },
    {
      id: 'why',
      question: 'Foxy kết luận dựa trên gì?',
      answer: `${advice.basedOn} Độ tin cậy ~${advice.confidence}%.`,
    },
    {
      id: 'style',
      question: 'Nên nói với con kiểu gì?',
      answer: advice.styleTip,
    },
  ];

  if (profile?.struggles.includes('brush_teeth')) {
    faqs.push({
      id: 'brush',
      question: `${who} không chịu đánh răng thì sao?`,
      answer: struggleAdvice('brush_teeth', who).doThis,
    });
  }
  if (profile?.struggles.includes('homework')) {
    faqs.push({
      id: 'hw',
      question: 'Con trì hoãn bài tập — xử lý thế nào?',
      answer: struggleAdvice('homework', who).doThis,
    });
  }
  if (profile?.goal === 'fewer_nudges') {
    faqs.push({
      id: 'nudge',
      question: 'Làm sao để ít phải nhắc hơn?',
      answer:
        'Chỉ nhắc việc quá giờ; tối đa 1 lần/việc; tối chuẩn bị sẵn (cặp/bàn chải); đo bằng Health Score & báo cáo 30 ngày.',
    });
  }

  return faqs.slice(0, 5);
}

export function formatCoachShare(advice: ParentingCoachAdvice): string {
  return [
    '🦊 Famixa Parenting Coach',
    advice.childProfile,
    '',
    `Dựa trên: ${advice.basedOn}`,
    `Nhận thấy: ${advice.insight}`,
    `Làm: ${advice.doThis}`,
    `Tránh: ${advice.avoid}`,
    `Giọng: ${advice.styleTip}`,
  ].join('\n');
}
