/**
 * Warm, context-aware Vietnamese copy for Family OS.
 * Same day + same signals → stable phrase; next day / new progress → rotates.
 */

export type ParentRole = 'mẹ' | 'bố' | 'bố mẹ';

export function shortPersonName(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  return parts.length ? parts[parts.length - 1]! : name.trim() || 'Con';
}

export function parentRoleFromName(name: string): ParentRole {
  const n = name.trim();
  if (!n) return 'bố mẹ';
  if (/^mẹ/i.test(n) || /\bmẹ\b/i.test(n) || /mẹ$/i.test(n)) return 'mẹ';
  if (/^bố/i.test(n) || /\bbố\b/i.test(n) || /bố$/i.test(n)) return 'bố';
  return 'bố mẹ';
}

/** Infer address from guardian display names in the household. */
export function parentRoleFromMembers(
  names: Array<string | null | undefined>,
): ParentRole {
  let hasMom = false;
  let hasDad = false;
  for (const raw of names) {
    const role = parentRoleFromName(raw ?? '');
    if (role === 'mẹ') hasMom = true;
    else if (role === 'bố') hasDad = true;
  }
  if (hasMom && hasDad) return 'bố mẹ';
  if (hasMom) return 'mẹ';
  if (hasDad) return 'bố';
  return 'bố mẹ';
}

/** "mẹ" → "Mẹ", "bố mẹ" → "Bố mẹ" — dùng đầu câu. */
export function capitalizeParentRole(role: ParentRole): string {
  if (!role) return 'Bố mẹ';
  return role.charAt(0).toUpperCase() + role.slice(1);
}

/** Nhãn ngắn trên chip/filter — luôn trung tính khi chưa rõ. */
export function parentSupportLabel(role: ParentRole): string {
  if (role === 'mẹ') return 'Cần mẹ hỗ trợ';
  if (role === 'bố') return 'Cần bố hỗ trợ';
  return 'Cần bố mẹ hỗ trợ';
}

/** Deterministic index — same seed always yields same pick. */
export function voicePickIndex(seed: string, poolLength: number): number {
  if (poolLength <= 0) return 0;
  let h = 2166136261;
  for (let i = 0; i < seed.length; i++) {
    h ^= seed.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return Math.abs(h) % poolLength;
}

export function voicePick<T>(seed: string, pool: readonly T[]): T {
  return pool[voicePickIndex(seed, pool.length)]!;
}

export type ProgressSignals = {
  flowDate: string;
  childShort: string;
  parentRole: ParentRole;
  done: number;
  total: number;
  open: number;
  nudgeToday: number;
  nudgeYesterday: number;
  streak: number;
  beautifulToday: boolean;
  needHelp: number;
};

function daySeed(flowDate: string, slot: string): string {
  return `${flowDate}:${slot}`;
}

/** Foxy strip on parent home — celebrates change, not the same line every day. */
export function buildFoxyNotice(s: ProgressSignals): string {
  const who = s.childShort;
  const parent = s.parentRole;
  const pct = s.total > 0 ? Math.round((s.done / s.total) * 100) : 0;
  const nudgeDown =
    s.nudgeYesterday > 0
      ? Math.round(((s.nudgeYesterday - s.nudgeToday) / s.nudgeYesterday) * 100)
      : null;

  if (s.beautifulToday || (s.total > 0 && s.open === 0 && s.done > 0)) {
    return voicePick(daySeed(s.flowDate, 'foxy-done'), [
      `Hôm nay ${who} xong hết việc — ${parent} gần như không phải nhắc. Đây là tiến bộ thật.`,
      `Ngày đẹp của nhà mình: ${who} giữ nhịp xong việc. Chuỗi đang ${s.streak || 1} ngày.`,
      `${who} tự giác hôm nay — ghi nhận khoảnh khắc này, đừng vội giao thêm việc.`,
    ]);
  }

  if (nudgeDown != null && nudgeDown >= 20) {
    return voicePick(daySeed(s.flowDate, 'foxy-nudge-down'), [
      `Số lần ${parent} phải nhắc giảm ${nudgeDown}% so với hôm qua — nhà đang nhẹ hơn.`,
      `${parent} nhắc ít hơn ${nudgeDown}% so với hôm qua. Giữ khoảng cách can thiệp nhé.`,
      `Tiến bộ rõ: nhắc giảm ${nudgeDown}% so với hôm qua. ${who} đang tự chủ hơn.`,
    ]);
  }

  if (s.nudgeToday >= 4) {
    return voicePick(daySeed(s.flowDate, 'foxy-nudge-high'), [
      `Hôm nay đã nhắc ${s.nudgeToday} lần — hãy chọn 1 việc nóng rồi dừng.`,
      `${s.nudgeToday} lần nhắc rồi. Mỗi lần thêm dễ thành cuộc chiến — ưu tiên một việc.`,
      `Foxy thấy hơi nhiều lần nhắc (${s.nudgeToday}). Một lời nhẹ + chờ ${who} tự làm.`,
    ]);
  }

  if (s.needHelp > 0) {
    return voicePick(daySeed(s.flowDate, 'foxy-help'), [
      `Có ${s.needHelp} việc cần ${parent} xác nhận (~15 giây) — đổi lấy ít phải nhắc hơn.`,
      `${s.needHelp} việc đang chờ ${parent}. Duyệt nhanh giúp ${who} nhận sao đúng lúc.`,
      `Hàng chờ nhỏ: ${s.needHelp} việc. Xác nhận xong là nhà nhẹ cả buổi.`,
    ]);
  }

  if (s.streak >= 3) {
    return voicePick(daySeed(s.flowDate, `foxy-streak-${s.streak}`), [
      `Chuỗi ${s.streak} ngày đẹp — nhà mình đang lớn lên từng ngày.`,
      `${s.streak} ngày giữ nhịp rồi. Đừng phá bằng checklist mới hôm nay.`,
      `Foxy ghi nhận chuỗi ${s.streak} ngày. Khen cụ thể quan trọng hơn thêm việc.`,
    ]);
  }

  if (pct >= 60 && s.open > 0) {
    return voicePick(daySeed(s.flowDate, 'foxy-mid'), [
      `Đã xong ${pct}% — chỉ còn ${s.open} việc. ${who} gần cán đích rồi.`,
      `Nhịp tốt: ${s.done}/${s.total} việc. Giữ nhẹ tay với phần còn lại.`,
      `${who} đang ở ${pct}%. Một việc tiếp theo là đủ — không cần mở cả list.`,
    ]);
  }

  if (s.total === 0) {
    return voicePick(daySeed(s.flowDate, 'foxy-empty'), [
      `Hôm nay chưa có việc — lúc tốt để nói chuyện với ${who}, không phải quản lý.`,
      `Không có checklist hôm nay. Dành 10 phút bên ${who} cũng là kỷ niệm.`,
    ]);
  }

  return voicePick(daySeed(s.flowDate, 'foxy-calm'), [
    `Nhịp hôm nay đang ổn. Chỉ can thiệp khi việc quá giờ.`,
    `Foxy chưa thấy việc nóng — giữ thói quen, đừng mở rộng checklist.`,
    `${who} đang có không gian tự giác. ${parent.charAt(0).toUpperCase()}${parent.slice(1)} đứng lùi một bước cũng là hỗ trợ.`,
  ]);
}

/** Match việc theo ngữ cảnh — tránh nhầm "cây"/"học"/"rang" lung tung. */
export function taskKindOf(title: string):
  | 'wake'
  | 'brush'
  | 'read'
  | 'pack'
  | 'sleep'
  | 'study'
  | 'garden'
  | 'tidy'
  | 'meal'
  | 'other' {
  const t = title.toLowerCase().trim();
  if (
    t === 'dậy' ||
    t.includes('thức dậy') ||
    t.includes('ngủ dậy') ||
    /\bdậy sớm\b/.test(t) ||
    /^dậy\b/.test(t)
  ) {
    return 'wake';
  }
  if (t.includes('đánh răng') || t.includes('dánh răng') || t.includes('danh rang')) return 'brush';
  if (t.includes('đi ngủ') || t.includes('ngủ sớm') || /(^|\s)ngủ(\s|$)/.test(t)) return 'sleep';
  if (
    t.includes('cặp sách') ||
    t.includes('chuẩn bị cặp') ||
    t.includes('balo') ||
    (t.includes('cặp') && (t.includes('chuẩn bị') || t.includes('sách')))
  ) {
    return 'pack';
  }
  if (t.includes('tưới') || t.includes('chăm cây') || t.includes('vườn')) {
    return 'garden';
  }
  if (t.includes('đọc sách') || t.includes('đọc truyện') || (t.includes('đọc') && t.includes('sách'))) {
    return 'read';
  }
  if (t.includes('bài tập') || t.includes('học bài') || t.includes('làm bài') || t.includes('ôn bài')) {
    return 'study';
  }
  if (t.includes('dọn phòng') || t.includes('dọn nhà') || t.includes('gấp quần') || t.includes('dọn')) {
    return 'tidy';
  }
  if (
    t.includes('ăn sáng') ||
    t.includes('ăn trưa') ||
    t.includes('ăn tối') ||
    t.includes('uống sữa') ||
    t.includes('ăn cơm') ||
    t.includes('ăn xế')
  ) {
    return 'meal';
  }
  return 'other';
}

/** Trạng thái nhật ký — quyết định từ ngữ, không đoán "đang làm". */
export type DiaryNoteStatus = 'done' | 'pending' | 'awaiting' | 'skipped';

/**
 * Tổng kết ngày: khớp tiến độ thật.
 * 0/12 → không "Tuyệt vời"; xong hết → mới khen.
 */
export function diaryDaySummaryLine(
  childShort: string,
  done: number,
  total: number,
): { prefix: string; ratio: string | null; suffix: string } {
  const who = childShort;
  const t = Math.max(0, total);
  const d = Math.max(0, done);
  if (t === 0) {
    return { prefix: `Hôm nay chưa có việc cho ${who}`, ratio: null, suffix: '' };
  }
  const ratio = `${d}/${t}`;
  if (d <= 0) {
    return {
      prefix: `Hôm nay ${who} chưa hoàn thành việc nào`,
      ratio,
      suffix: '',
    };
  }
  if (d >= t) {
    return {
      prefix: `Tuyệt vời! ${who} đã hoàn thành hết`,
      ratio,
      suffix: 'việc',
    };
  }
  const left = t - d;
  if (d / t >= 0.6) {
    return {
      prefix: `Tiến bộ tốt — ${who} đã hoàn thành`,
      ratio,
      suffix: `việc · còn ${left} việc`,
    };
  }
  return {
    prefix: `${who} đã hoàn thành`,
    ratio,
    suffix: `việc · còn ${left} việc`,
  };
}

/**
 * Câu tiến độ cả nhà trên Home — nói đúng trạng thái, không khen khi chưa có gì xong.
 */
export function familyProgressLine(done: number, total: number): string {
  const t = Math.max(0, total);
  const d = Math.min(Math.max(0, done), t);
  if (t === 0) return 'Hôm nay chưa có việc nào được giao';
  if (d <= 0) return `Chưa việc nào xong — ${t} việc đang chờ hôm nay`;
  if (d >= t) return `Cả nhà đã xong hết ${t} việc hôm nay`;
  return `Đã xong ${d}/${t} việc — còn ${t - d} việc hôm nay`;
}

export function warmTaskSupportNote(input: {
  title: string;
  childShort: string;
  parentRole: ParentRole;
  kind: 'overdue' | 'awaiting' | 'upcoming';
  flowDate: string;
  itemId: string;
}): string {
  const { title, childShort: who, parentRole: parent, kind, flowDate, itemId } = input;
  const kindTask = taskKindOf(title);
  const seed = `${flowDate}:${itemId}:${kind}`;

  if (kind === 'awaiting') {
    return voicePick(seed, [
      `${who} báo xong «${title}» — ${parent} kiểm tra giúp một chút nhé`,
      `Có ảnh/báo cáo từ ${who}. Duyệt sao để con thấy được ghi nhận`,
      `${who} đang chờ ${parent} xác nhận «${title}» (~15 giây)`,
    ]);
  }

  if (kindTask === 'pack') {
    return voicePick(seed, [
      `${who} dễ quên cặp — tối chuẩn bị một lần sẽ cứu sáng mai`,
      `«${title}» hay bị trễ. ${parent} chỉ cần neo “sau ăn tối” là đủ`,
    ]);
  }
  if (kindTask === 'brush') {
    return voicePick(seed, [
      `${who} với đánh răng cần neo gần giường — nhắc tối đa 1 lần`,
      `Đánh răng đang nóng. Nhắc nhẹ rồi để ${who} tự làm`,
    ]);
  }
  if (kindTask === 'sleep') {
    return voicePick(seed, [
      `Giờ ngủ lệch sẽ kéo quên sáng — giữ neo cố định giúp ${who}`,
      `«${title}» ảnh hưởng cả ngày mai. Nhắc một lần, không đàm phán lại`,
    ]);
  }
  if (kindTask === 'read' || kindTask === 'study') {
    return voicePick(seed, [
      `${who} cần khung 20 phút yên — xong thì tick ngay để nhận sao`,
      `Học/đọc đang chờ. Timer ngắn hiệu quả hơn ngồi soi`,
    ]);
  }

  if (kind === 'upcoming') {
    return voicePick(seed, [
      `Sắp tới — để ${who} tự làm, ${parent} chỉ theo dõi`,
      `Chưa đến giờ nóng. Giữ khoảng cách can thiệp`,
    ]);
  }

  return voicePick(seed, [
    `${who} cần một lời nhắc nhẹ với «${title}»`,
    `«${title}» đang trễ — nhắc một lần rồi dừng`,
    `${parent} đồng hành ngắn với «${title}», không cần mở cả list`,
  ]);
}

export function warmTaskTip(input: {
  title: string;
  childShort: string;
  parentRole: ParentRole;
  flowDate: string;
  itemId: string;
}): string {
  const { title, childShort: who, parentRole: parent, flowDate, itemId } = input;
  const kindTask = taskKindOf(title);
  const seed = `${flowDate}:${itemId}:tip`;
  const Parent = capitalizeParentRole(parent);

  if (kindTask === 'brush') {
    return voicePick(seed, [
      'Neo bàn chải gần giường — xong là tick ngay',
      `${who} tự làm được nếu có neo rõ`,
    ]);
  }
  if (kindTask === 'read') {
    return voicePick(seed, [
      '10–20 phút là đủ — khen cụ thể khi xong',
      `Đọc cùng ${parent === 'bố mẹ' ? 'nhà' : parent} cũng là kỷ niệm`,
    ]);
  }
  if (kindTask === 'pack' || kindTask === 'tidy') {
    return voicePick(seed, [
      'Làm một lần vào tối — sáng sẽ nhẹ',
      'Chia nhỏ: sách / áo / hộp cơm',
    ]);
  }
  if (kindTask === 'garden') {
    return voicePick(seed, [
      `${who} tưới cây giúp nhà — khen cụ thể khi xong`,
      'Một lần tưới đủ nước là được — không cần nhắc lại',
    ]);
  }
  return voicePick(seed, [
    `${Parent} tin ${who} làm được`,
    'Một việc một lúc — không cần hoàn hảo',
    'Xong rồi tick ngay để cả nhà thấy tiến bộ',
  ]);
}

/** Nhật ký — câu phụ khớp trạng thái thật (chưa làm ≠ đang làm ≠ đã xong). */
export function diaryTaskNote(
  title: string,
  childShort: string,
  status: DiaryNoteStatus | boolean,
  parentRole: ParentRole = 'bố mẹ',
): string {
  const who = childShort;
  const parent = parentRole;
  const kind = taskKindOf(title);
  const state: DiaryNoteStatus =
    typeof status === 'boolean' ? (status ? 'done' : 'pending') : status;
  const titleLower = title.trim().toLowerCase();

  if (state === 'skipped') {
    return `${who} chưa làm được «${title}» lần này`;
  }

  if (state === 'awaiting') {
    return `${who} đã báo xong «${title}» — đang chờ ${parent} kiểm tra`;
  }

  if (state === 'done') {
    if (kind === 'wake') return `${who} đã dậy đúng giờ`;
    if (kind === 'brush') return `${who} đã tự đánh răng mà không cần ${parent} nhắc!`;
    if (kind === 'read') return `${who} đọc sách rất chăm chỉ hôm nay`;
    if (kind === 'garden') return `${who} đã tưới cây giúp nhà rất tốt`;
    if (kind === 'sleep') return `${who} đi ngủ đúng giờ — giấc ngủ ngon!`;
    if (kind === 'pack') return `${who} đã chuẩn bị cặp xong`;
    if (kind === 'study') return `${who} đã hoàn thành bài học`;
    if (kind === 'tidy') return `${who} đã dọn xong «${title}»`;
    if (kind === 'meal') return `${who} đã ${titleLower} xong`;
    if (titleLower.includes('gia đình') || titleLower.includes('cùng')) {
      return 'Khoảnh khắc ấm áp bên gia đình';
    }
    return `${who} đã hoàn thành «${title}» rất tốt!`;
  }

  // pending — chưa bắt đầu / chưa xong: không dùng "đang làm"
  if (kind === 'wake') return `${who} chưa dậy`;
  if (kind === 'brush') return `${who} cần hoàn thành đánh răng`;
  if (kind === 'read') return `${who} chưa đọc sách`;
  if (kind === 'garden') return `${who} chưa tưới cây`;
  if (kind === 'sleep') return `${who} sắp đến giờ đi ngủ`;
  if (kind === 'pack') return `${who} cần chuẩn bị cặp`;
  if (kind === 'study') return `${who} chưa làm bài`;
  if (kind === 'tidy') return `${who} chưa dọn «${title}»`;
  if (kind === 'meal') return `${who} chưa ${titleLower}`;
  if (titleLower.includes('gia đình') || titleLower.includes('cùng')) {
    return `Chưa tới khoảnh khắc «${title}»`;
  }
  return `${who} chưa làm «${title}»`;
}
