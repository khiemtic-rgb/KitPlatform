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

export function warmTaskSupportNote(input: {
  title: string;
  childShort: string;
  parentRole: ParentRole;
  kind: 'overdue' | 'awaiting' | 'upcoming';
  flowDate: string;
  itemId: string;
}): string {
  const { title, childShort: who, parentRole: parent, kind, flowDate, itemId } = input;
  const t = title.toLowerCase();
  const seed = `${flowDate}:${itemId}:${kind}`;

  if (kind === 'awaiting') {
    return voicePick(seed, [
      `${who} báo xong «${title}» — ${parent} kiểm tra giúp một chút nhé`,
      `Có ảnh/báo cáo từ ${who}. Duyệt sao để con thấy được ghi nhận`,
      `${who} đang chờ ${parent} xác nhận «${title}» (~15 giây)`,
    ]);
  }

  if (t.includes('cặp') || t.includes('balo')) {
    return voicePick(seed, [
      `${who} dễ quên cặp — tối chuẩn bị một lần sẽ cứu sáng mai`,
      `«${title}» hay bị trễ. ${parent} chỉ cần neo “sau ăn tối” là đủ`,
    ]);
  }
  if (t.includes('đánh răng')) {
    return voicePick(seed, [
      `${who} với đánh răng cần neo gần giường — nhắc tối đa 1 lần`,
      `Đánh răng đang nóng. Nhắc nhẹ rồi để ${who} tự làm`,
    ]);
  }
  if (t.includes('ngủ')) {
    return voicePick(seed, [
      `Giờ ngủ lệch sẽ kéo quên sáng — giữ neo cố định giúp ${who}`,
      `«${title}» ảnh hưởng cả ngày mai. Nhắc một lần, không đàm phán lại`,
    ]);
  }
  if (t.includes('đọc') || t.includes('sách') || t.includes('bài') || t.includes('học')) {
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
  const t = title.toLowerCase();
  const seed = `${flowDate}:${itemId}:tip`;

  if (t.includes('đánh răng')) {
    return voicePick(seed, [
      'Neo bàn chải gần giường — xong là tick ngay',
      `${who} tự làm được nếu có neo rõ`,
    ]);
  }
  if (t.includes('đọc') || t.includes('sách')) {
    return voicePick(seed, [
      '10–20 phút là đủ — khen cụ thể khi xong',
      `Đọc cùng ${parent === 'bố mẹ' ? 'nhà' : parent} cũng là kỷ niệm`,
    ]);
  }
  if (t.includes('cặp') || t.includes('balo') || t.includes('dọn')) {
    return voicePick(seed, [
      'Làm một lần vào tối — sáng sẽ nhẹ',
      'Chia nhỏ: sách / áo / hộp cơm',
    ]);
  }
  return voicePick(seed, [
    `${parent.charAt(0).toUpperCase()}${parent.slice(1)} tin ${who} làm được`,
    'Một việc một lúc — không cần hoàn hảo',
    'Xong rồi tick ngay để cả nhà thấy tiến bộ',
  ]);
}
