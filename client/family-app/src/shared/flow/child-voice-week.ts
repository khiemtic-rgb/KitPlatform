/** Shared child-voice week form — keep parent Insight and kid Treasure in sync. */

export const CHILD_VOICE_WEEK_COPY = {
  title: 'ĐIỀU CON MUỐN NÓI',
  subtitle: 'Không phải kiểm tra — chỉ để nhà mình hiểu con hơn một chút.',
  parentSubtitle: 'Lời con gửi tuần này — bố mẹ chỉ lắng nghe, không trả lời thay con.',
  parentWaiting: 'Con chưa gửi lời tuần này. Nhắc nhẹ trên app của con khi tiện — đừng điền hộ.',
  hardestLabel: 'Tuần này việc nào hơi khó với con?',
  wantLabel: 'Con thấy bố mẹ giúp kiểu nào dễ chịu hơn?',
  wishLabel: 'Con muốn đề xuất gì thêm không?',
  wishPlaceholder: 'Ví dụ: muốn giữ giờ đọc sách… (tuỳ chọn)',
  submit: 'Gửi lời của con',
  submitting: 'Đang gửi…',
  kidDone: 'Cảm ơn con đã chia sẻ. Bố mẹ sẽ lắng nghe và thử cách dịu hơn.',
  sendFailed: 'Chưa gửi được — thử lại sau nhé.',
} as const;

export const CHILD_VOICE_HARDEST_OPTIONS = [
  { value: 'evening', label: 'Buổi tối / hơi mệt' },
  { value: 'subject', label: 'Học / môn khó' },
  { value: 'alone', label: 'Làm một mình' },
  { value: 'long', label: 'Việc hơi dài' },
  { value: 'other', label: 'Khác' },
] as const;

export const CHILD_VOICE_WANT_OPTIONS = [
  { value: 'less_remind', label: 'Nhắc ít hơn một chút' },
  { value: 'praise', label: 'Khen khi con tự làm' },
  { value: 'together', label: 'Cùng làm một đoạn ngắn' },
  { value: 'choose_time', label: 'Cho con chọn giờ' },
  { value: 'friends', label: 'Có người cùng làm' },
] as const;

export const CHILD_VOICE_DEFAULTS = {
  hardest: 'evening',
  want: 'praise',
} as const;

export function childVoiceHardestLabel(code?: string | null): string | undefined {
  if (!code) return undefined;
  return CHILD_VOICE_HARDEST_OPTIONS.find((o) => o.value === code)?.label ?? code;
}

export function childVoiceWantLabel(code?: string | null): string | undefined {
  if (!code) return undefined;
  return CHILD_VOICE_WANT_OPTIONS.find((o) => o.value === code)?.label ?? code;
}
