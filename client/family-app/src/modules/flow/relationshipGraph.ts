/** P1.9–P1.12 Relationship graph helpers — adult care, birthday picker, grandpa share. */

export type BirthdayPickerOption = {
  code: string;
  labelVi: string;
  draftVi: (childShort: string, parentLabel: string) => string;
  treatTitleVi?: string;
};

export const BIRTHDAY_PICKER_OPTIONS: BirthdayPickerOption[] = [
  {
    code: 'wish',
    labelVi: 'Lời chúc ấm',
    draftVi: (c, p) => `${c} ơi, sinh nhật vui vẻ! ${p} thương con nhiều.`,
  },
  {
    code: 'hug',
    labelVi: 'Ôm + lời yêu',
    draftVi: (c, p) => `${c} ơi, hôm nay ${p.toLowerCase()} muốn ôm con thật chặt. Sinh nhật vui nhé!`,
  },
  {
    code: 'movie',
    labelVi: 'Chọn phim cuối tuần',
    draftVi: (c, p) =>
      `${c} ơi, sinh nhật này ${p.toLowerCase()} để con chọn một bộ phim xem cùng nhà nhé!`,
    treatTitleVi: 'Sinh nhật — chọn phim cả nhà',
  },
];

export const ADULT_VOICE_TEMPLATES = [
  { code: 'thanks_partner' as const, labelVi: 'Cảm ơn', hint: 'Một câu nhớ ơn ngắn' },
  { code: 'help_offer' as const, labelVi: 'Phụ việc', hint: 'Mời cùng gánh nhẹ' },
  { code: 'warm_adult' as const, labelVi: 'Lời ấm', hint: 'Không gắn % việc' },
];

export function formatWeeklyStoryShare(input: {
  familyName?: string;
  headlineVi: string;
  lines: Array<{ icon: string; textVi: string }>;
}): string {
  const house = (input.familyName ?? 'Nhà mình').trim() || 'Nhà mình';
  const body = input.lines.map((l) => `${l.icon} ${l.textVi}`).join('\n');
  return `📖 Câu chuyện tuần — ${house}\n\n${input.headlineVi}\n\n${body}\n\n— Gửi từ Famixa (chỉ xem trong nhà)`;
}

export function shortMemberName(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  return parts.length ? parts[parts.length - 1]! : name.trim() || 'bạn';
}

/**
 * Default draft when parent opens “gửi lời cho con” without a trigger.
 * Prefer situational / ask-for-share over generic “nghĩ đến con”.
 */
export function defaultChildVoiceDraftVi(input: {
  childShort: string;
  parentRole: string;
  doneCount?: number;
  totalCount?: number;
  streak?: number;
  teamComplete?: boolean;
}): string {
  const c = (input.childShort || 'con').trim() || 'con';
  const pRaw = (input.parentRole || 'bố mẹ').trim().toLowerCase();
  const p = pRaw === 'bố' || pRaw === 'mẹ' ? pRaw : 'bố mẹ';
  const done = Math.max(0, input.doneCount ?? 0);
  const total = Math.max(0, input.totalCount ?? 0);
  const streak = Math.max(0, input.streak ?? 0);

  if (input.teamComplete || (total > 0 && done >= total && done > 0)) {
    return `${c} ơi, hôm nay con giữ nhịp cả ngày — ${p} thấy rồi, tự hào lắm.`;
  }
  if (done > 0) {
    return `${c} ơi, ${p} thấy con đã xong ${done} việc rồi — cố thêm một chút nữa nhé.`;
  }
  if (streak >= 2) {
    return `${c} ơi, chuỗi ${streak} ngày của con ${p} đang theo dõi — giữ nhẹ nhàng thôi.`;
  }
  return `${c} ơi, tối nay kể ${p} nghe một điều vui trong ngày của con nhé?`;
}

/** Prefill when opening voice sheet from Home AI cards. */
export function childVoiceDraftForIntent(input: {
  childShort: string;
  parentRole: string;
  intent: 'encourage' | 'praise' | 'soft' | 'moment';
  taskTitle?: string;
}): string {
  const c = (input.childShort || 'con').trim() || 'con';
  const pRaw = (input.parentRole || 'bố mẹ').trim().toLowerCase();
  const p = pRaw === 'bố' || pRaw === 'mẹ' ? pRaw : 'bố mẹ';
  const task = (input.taskTitle || '').trim();

  switch (input.intent) {
    case 'encourage':
      return task
        ? `${c} ơi, ${p} biết «${task}» hơi khó — mình cùng giữ nhẹ nhé, ${p} ở đây với con.`
        : `${c} ơi, hôm nay hơi chùng đúng không? ${p} ở đây với con — làm một việc nhỏ thôi cũng được.`;
    case 'praise':
      return task
        ? `${c} ơi, ${p} thấy con đã xong «${task}» rồi — tự hào lắm, giỏi lắm!`
        : `${c} ơi, ${p} thấy con cố gắng hôm nay — tự hào lắm!`;
    case 'moment':
      return `${c} ơi, ${p} đã nhìn thấy khoảnh khắc của con rồi — cảm ơn con đã chia sẻ nhé.`;
    case 'soft':
    default:
      return `${c} ơi, ${p} nghĩ đến con và muốn gửi một lời ấm — hôm nay con ổn chứ?`;
  }
}
