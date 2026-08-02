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
import { shortPersonName, voicePick } from '@/shared/voice/family-voice';

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

export type ParentingCoachScope =
  | { kind: 'family'; labelVi: string }
  | {
      kind: 'child';
      labelVi: string;
      childName: string;
      /** Optional — used by week playbook / child voice. */
      childMemberId?: string;
    };

export type ParentingCoachFaq = {
  id: string;
  question: string;
  answer: string;
};

function shortName(name: string) {
  return shortPersonName(name);
}

function isOpen(c: DayFlowCommitment) {
  return c.status !== 'done' && c.status !== 'skipped';
}

function ageStyle(age: AgeBand | undefined, flowDate: string): string {
  const seed = `${flowDate}:age:${age ?? 'na'}`;
  switch (age) {
    case '4-6':
      return voicePick(seed, [
        'Nhắc bằng hình / việc rất ngắn; khen ngay sau khi xong; tránh giọng lớn.',
        'Một việc — một lời khen cụ thể. Đừng mở cả list với trẻ nhỏ.',
        'Dùng đồng hồ cát hoặc bài hát ngắn làm neo giờ; dừng khi đã nhắc 1 lần.',
      ]);
    case '7-9':
      return voicePick(seed, [
        'Một việc tiếp theo + phần thưởng nhà; khen cụ thể, không so sánh anh chị.',
        'Hỏi “con tự chọn làm việc nào trước?” — vẫn giữ khung giờ.',
        'Khen hành vi (“con tự lấy bàn chải”), không chỉ khen kết quả.',
      ]);
    case '10-12':
      return voicePick(seed, [
        'Giao quyền tự chủ + thỏa thuận rõ; hỏi “con tự chọn giờ nào?” thay vì ra lệnh.',
        'Để con tick app trước, bố mẹ chỉ vào khi quá giờ.',
        'Nói ngắn, tôn trọng; hậu quả đã thỏa thuận sẵn thì giữ nhất quán.',
      ]);
    case '13+':
      return voicePick(seed, [
        'Tôn trọng không gian; thỏa thuận hậu quả trước; tránh “hỏi lại lần 3”.',
        'Nhắn một câu ấm (Zalo) hơn là đứng hỏi lại. Giữ khoảng cách.',
        'Coi con là đồng đội: “nhà mình cần việc này xong”, không phải mệnh lệnh.',
      ]);
    default:
      return voicePick(seed, [
        'Nhắc một lần, khen cụ thể, dừng khi đã nhắc đủ.',
        'Ưu tiên một việc nóng — đừng quản lý cả ngày trong một hơi.',
        'Giọng nhẹ + chờ 10 phút thường hiệu quả hơn nhắc liên tục.',
      ]);
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

function struggleAdvice(
  code: StruggleCode,
  who: string,
  flowDate: string,
): { insight: string; doThis: string; avoid: string } {
  const seed = `${flowDate}:struggle:${code}:${who}`;
  switch (code) {
    case 'brush_teeth':
      return {
        insight: voicePick(seed + ':i', [
          `Hôm nay pattern đánh răng của ${who} lại hiện — đúng điểm khó trong hồ sơ nhà mình.`,
          `${who} đang kẹt ở đánh răng; đây là việc “nhỏ nhưng hay thành cuộc chiến”.`,
          `Foxy thấy đánh răng đang nóng với ${who} — đừng mở rộng sang việc khác trước.`,
        ]),
        doThis: voicePick(seed + ':d', [
          'Đặt bàn chải gần giường; neo “xuống giường là đánh răng”; nhắc tối đa 1 lần.',
          'Nhắc nhẹ một lần trên app, rồi để yên 10 phút. Xong thì khen cụ thể.',
          'Gắn với bài hát/đồng hồ cát 2 phút — xong tick ngay để nhận sao.',
        ]),
        avoid: voicePick(seed + ':a', [
          'Không đứng hỏi lại nhiều lần hoặc dùng giọng to — dễ thành cuộc chiến.',
          'Đừng so sánh với anh chị / bạn. Một việc một lúc thôi.',
        ]),
      };
    case 'morning_forget':
      return {
        insight: voicePick(seed + ':i', [
          `${who} hay quên buổi sáng — việc trễ thường dồn khi dậy muộn.`,
          `Sáng nay đang lệch nhịp của ${who}. Cứu bằng 1 việc ưu tiên, không cả list.`,
        ]),
        doThis: voicePick(seed + ':d', [
          'Báo thức sớm 10 phút; tối chuẩn bị cặp; sáng chỉ 1 việc ưu tiên.',
          'Tối nay neo “cặp xong rồi mới xem gì đó” — sáng sẽ nhẹ hơn.',
        ]),
        avoid: voicePick(seed + ':a', [
          'Đừng mở cả checklist 8 việc lúc 6h45 — não trẻ sẽ “tắt”.',
          'Không quát vì quên — sửa bằng neo tối hôm trước.',
        ]),
      };
    case 'homework':
      return {
        insight: voicePick(seed + ':i', [
          `${who} dễ trì hoãn học — khung giờ cố định hơn “làm khi nào xong”.`,
          `Học đang cần khung ngắn với ${who}. 20 phút tập trung thường thắng cả buổi lê thê.`,
        ]),
        doThis: voicePick(seed + ':d', [
          'Timer 20 phút + chỗ học sạch distraction; xong tick ngay để nhận sao.',
          'Hỏi “con tự chọn bắt đầu lúc nào trong khung?” rồi giữ đúng thỏa thuận.',
        ]),
        avoid: voicePick(seed + ':a', [
          'Không ngồi cạnh soi từng phút — sát cánh ở đầu/cuối phiên.',
          'Đừng biến giờ học thành buổi kiểm tra tâm trạng.',
        ]),
      };
    case 'screen':
      return {
        insight: voicePick(seed + ':i', [
          `Màn hình đang là điểm nóng với ${who} — việc trước ngủ hay bị đẩy.`,
          `${who} dễ mất nhịp vì màn hình. Thỏa thuận trước hiệu quả hơn cấm đột ngột.`,
        ]),
        doThis: voicePick(seed + ':d', [
          'Thỏa thuận “xong việc rồi mới mở”; tắt màn hình trước ngủ 30 phút.',
          'Nhắc một lần + dùng soft-lock đã thỏa thuận — không cãi qua lại.',
        ]),
        avoid: voicePick(seed + ':a', [
          'Đừng cấm đột ngột không báo trước — dùng thỏa thuận nhà.',
          'Tránh giằng co máy lúc đang xem — cắt trước bằng hẹn giờ.',
        ]),
      };
    case 'sleep':
      return {
        insight: voicePick(seed + ':i', [
          `Giờ ngủ của ${who} đang lệch — sáng mai dễ quên việc.`,
          `Ngủ muộn hôm nay sẽ “đẻ” quên sáng. Đây là đòn bẩy lớn của nhà mình.`,
        ]),
        doThis: voicePick(seed + ':d', [
          'Neo giờ ngủ cố định; giảm kích thích 30 phút trước; khen khi đúng giờ.',
          'Tắt màn hình sớm + cùng nghi thức ngắn (đọc 1 trang / ôm).',
        ]),
        avoid: voicePick(seed + ':a', [
          'Tránh đàm phán lại giờ ngủ mỗi tối.',
          'Đừng đổi giờ ngủ vì “hôm nay ngoại lệ” quá thường xuyên.',
        ]),
      };
    case 'tidy':
      return {
        insight: voicePick(seed + ':i', [
          `${who} khó với chuẩn bị/dọn — tối làm một lần cứu sáng hôm sau.`,
          `Việc cặp/dọn của ${who} hay thành cuộc đua sáng. Chốt vào tối.`,
        ]),
        doThis: voicePick(seed + ':d', [
          '“Chuẩn bị cặp ngay sau ăn tối” — gắn với lời khen ngắn.',
          'Chia nhỏ: sách / áo / hộp cơm. Xong từng phần là tick.',
        ]),
        avoid: voicePick(seed + ':a', [
          'Đừng để sáng mới phát hiện thiếu đồ rồi quát.',
          'Không làm hộ toàn bộ — chỉ đứng cạnh 2 phút khởi động.',
        ]),
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
  scope?: ParentingCoachScope;
}): ParentingCoachAdvice {
  const { familyId, flow, glance, nudgeToday, focusChildName, scope } = input;
  const savedProfile = getOnboardingProfile(familyId);
  const isFamilyScope = scope?.kind === 'family';
  const selectedName =
    scope?.kind === 'child' ? scope.childName : focusChildName;
  const normalizedSelected = shortName(selectedName || '').toLocaleLowerCase('vi');
  const normalizedProfile = shortName(savedProfile?.childName || '').toLocaleLowerCase('vi');
  // Onboarding belongs to one child. Never apply that child's age/goals to another child or Cả nhà.
  const profile =
    !isFamilyScope &&
    savedProfile &&
    (!normalizedSelected || normalizedSelected === normalizedProfile)
      ? savedProfile
      : null;
  const who = isFamilyScope
    ? 'Cả nhà'
    : shortName(selectedName || profile?.childName || 'Con');
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

  const doneToday = flow.commitments.filter((c) => c.status === 'done').length;
  const totalToday = flow.commitments.length;
  const childProfile = isFamilyScope
    ? `Cả nhà · hôm nay ${doneToday}/${totalToday} việc đã xong`
    : profile && !profile.skipped
      ? `${who}${ageLabel ? ` · ${ageLabel}` : ''}${struggleLabels ? ` · khó: ${struggleLabels}` : ''}${goalLabel ? ` · mục tiêu 30 ngày: ${goalLabel}` : ''}`
      : `${who} · hôm nay ${doneToday}/${totalToday} việc đã xong`;

  const date = flow.flowDate;

  // 1) Hot mission matching onboarding struggle
  if (hot) {
    const linked = matchStruggle(hot.title, struggles);
    if (linked) {
      const tip = struggleAdvice(linked, who, date);
      return {
        childProfile,
        basedOn: `Hôm nay «${hot.title}» đang ${hot.reminderState === 'overdue' ? 'quá giờ' : 'đến giờ'} · khớp khó khăn đã ghi nhận${isFamilyScope ? ` · cả nhà đã nhắc ${nudgeToday} lần` : ''}.`,
        insight: tip.insight,
        doThis: tip.doThis,
        avoid: tip.avoid,
        styleTip: ageStyle(age, date),
        confidence,
      };
    }
    return {
      childProfile,
      basedOn: `«${hot.title}» (${hot.memberName?.trim() || who}) đang cần hỗ trợ${isFamilyScope ? ` · cả nhà đã nhắc ${nudgeToday} lần hôm nay` : ''}.`,
      insight: voicePick(`${date}:hot:i:${hot.id}`, [
        `${who} đang kẹt ở «${hot.title}» — ưu tiên một việc này trước, đừng mở cả list.`,
        `Việc nóng hôm nay là «${hot.title}». Xử lý xong rồi hãy nhìn việc khác.`,
        `Foxy thấy «${hot.title}» đang kéo năng lượng nhà mình — một lời nhắc nhẹ là đủ.`,
      ]),
      doThis: voicePick(`${date}:hot:d:${hot.id}`, [
        'Nhắc nhẹ một lần trên app, rồi dừng. Khi xong: khen cụ thể.',
        'Mở chia sẻ tin nhắc ấm → gửi Zalo → chờ. Không hỏi lại trong 15 phút.',
        'Đứng cạnh 2 phút khởi động, rồi để con tự tick.',
      ]),
      avoid: voicePick(`${date}:hot:a`, [
        'Không hỏi lại lần 2–3 trong 15 phút.',
        'Đừng biến một việc trễ thành bài giảng cả buổi.',
      ]),
      styleTip: ageStyle(age, date),
      confidence,
    };
  }

  // 2) Goal-oriented when day is calm
  if (isFamilyScope && goal === 'fewer_nudges' && nudgeToday >= 3) {
    return {
      childProfile,
      basedOn: `Mục tiêu onboarding là giảm nhắc · hôm nay đã nhắc ${nudgeToday} lần.`,
      insight: voicePick(`${date}:goal-nudge:i`, [
        `Đang lệch mục tiêu “ít nhắc” — hôm nay đã ${nudgeToday} lần.`,
        `Mỗi lần nhắc thêm đang kéo nhà mình xa mục tiêu giảm áp lực.`,
      ]),
      doThis: voicePick(`${date}:goal-nudge:d`, [
        'Chọn 1 việc duy nhất còn lại; tắt thông báo việc phụ trong 1 giờ.',
        'Chỉ nhắc việc quá giờ. Việc sắp tới để con tự chủ.',
      ]),
      avoid: 'Đừng nhắc “tất cả việc nóng” nếu mục tiêu là giảm áp lực.',
      styleTip: ageStyle(age, date),
      confidence,
    };
  }

  if (goal === 'bedtime' || struggles.includes('sleep')) {
    const sleep = flow.commitments.find((c) => /ngủ/i.test(c.title));
    if (sleep && isOpen(sleep)) {
      const tip = struggleAdvice('sleep', who, date);
      return {
        childProfile,
        basedOn: 'Mục tiêu/khó khăn liên quan giờ ngủ · việc ngủ còn mở.',
        insight: tip.insight,
        doThis: tip.doThis,
        avoid: tip.avoid,
        styleTip: ageStyle(age, date),
        confidence,
      };
    }
  }

  if (isFamilyScope && lateHeavy) {
    return {
      childProfile,
      basedOn: '7 ngày gần đây: nhiều ngày còn việc mở/xong muộn.',
      insight: voicePick(`${date}:late:i`, [
        `${who} đang cần “thắng nhỏ” hơn là thêm mission mới.`,
        `Chuỗi tuần hơi nặng việc muộn — giảm tải checklist sẽ giúp ${who} lấy đà.`,
      ]),
      doThis: voicePick(`${date}:late:d`, [
        'Giữ tối đa 4–5 việc/ngày trong 1 tuần; khen đúng giờ hơn khen số lượng.',
        'Chọn 2 neo cố định (răng + cặp). Tạm gác việc phụ.',
      ]),
      avoid: 'Không thêm routine mới khi chuỗi đang yếu.',
      styleTip: ageStyle(age, date),
      confidence: Math.min(95, confidence + 5),
    };
  }

  const done = flow.commitments.filter((c) => c.status === 'done').length;
  if (done > 0 && open.length === 0) {
    const streak = glance?.currentStreak ?? 0;
    return {
      childProfile,
      basedOn: `Hôm nay phần việc đã xong · streak nhà ${streak} ngày.`,
      insight: voicePick(`${date}:done:i:${streak}`, [
        `${who} vừa hoàn thành nhịp ngày — lúc củng cố, không phải lúc soi lỗi.`,
        `Ngày sạch việc! Chuỗi ${streak || 1} ngày — đây là tiến bộ đáng giữ.`,
        `Foxy thấy nhà mình xong việc mà ít căng. Ghi nhận khoảnh khắc này.`,
      ]),
      doThis: voicePick(`${date}:done:d`, [
        'Một lời khen cụ thể + lưu kỷ niệm. Movie Night nếu cả nhà sẵn sàng.',
        'Hỏi “hôm nay con tự hào điều gì nhất?” — 1 phút cũng đủ ấm.',
        'Không giao thêm việc. Chỉ khen + ở bên.',
      ]),
      avoid: 'Đừng ngay lập tức giao thêm việc “đã lỡ”.',
      styleTip: ageStyle(age, date),
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
    basedOn: isFamilyScope
      ? `Nhịp hôm nay ổn · ước lượng cả nhà nhắc 7 ngày khoảng ${weekNudge} lần.`
      : `${who} hôm nay chưa có việc nóng cần bố mẹ can thiệp.`,
    insight: voicePick(`${date}:calm:i:${goal ?? 'na'}`, [
      goal === 'more_autonomy'
        ? `${who} đang có không gian tự giác — giữ khoảng cách can thiệp.`
        : `Foxy chưa thấy việc nóng — lúc giữ thói quen, không mở rộng checklist.`,
      `Ngày đang êm. Đây là lúc ${who} luyện tự chủ, không phải lúc “quản lý thêm”.`,
      isFamilyScope && weekNudge <= 3
        ? `Tuần này nhắc khá ít (~${weekNudge}) — nhà mình đang đi đúng hướng.`
        : isFamilyScope
          ? `Nhịp hôm nay ổn; tuần vẫn còn khoảng ${weekNudge} lần nhắc — giữ nhẹ tay.`
          : `${who} chưa có việc nóng — giữ nhịp nhẹ và để con tự chủ.`,
    ]),
    doThis: voicePick(`${date}:calm:d:${goal ?? 'na'}`, [
      goal === 'quality_time'
        ? 'Chọn 10 phút đọc/kể chuyện hoặc Movie Night nếu đủ điều kiện mở khóa.'
        : 'Giữ 1 neo cố định (đánh răng hoặc cặp). Chỉ can thiệp khi quá giờ.',
      `Khen một việc ${who} đã làm tốt hôm nay — cụ thể, không chung chung.`,
      'Nếu rảnh: lưu một kỷ niệm ngắn. Không cần thêm nhiệm vụ.',
    ]),
    avoid: voicePick(`${date}:calm:a`, [
      'Tránh thêm việc “cho chắc” khi ngày đang êm.',
      'Đừng phá một ngày tốt bằng checklist mới.',
    ]),
    styleTip: ageStyle(age, date),
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
      answer: struggleAdvice('brush_teeth', who, input.flow.flowDate).doThis,
    });
  }
  if (profile?.struggles.includes('homework')) {
    faqs.push({
      id: 'hw',
      question: 'Con trì hoãn bài tập — xử lý thế nào?',
      answer: struggleAdvice('homework', who, input.flow.flowDate).doThis,
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
    '🦊 Famixa Family Coach',
    advice.childProfile,
    '',
    `Dựa trên: ${advice.basedOn}`,
    `Nhận thấy: ${advice.insight}`,
    `Làm: ${advice.doThis}`,
    `Tránh: ${advice.avoid}`,
    `Giọng: ${advice.styleTip}`,
  ].join('\n');
}
