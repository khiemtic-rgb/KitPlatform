import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button, Collapse, Drawer, Space, Tag, Typography, message } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { Link } from 'react-router-dom';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  ensureDayFlow,
  fetchAccountabilityGlance,
  fetchChildGratitude,
  fetchCoachInsight,
  fetchFamilies,
  fetchFamilyMoods,
  fetchFamilyOsOverview,
  fetchTeamDay,
  fetchTeamUnlocks,
  type AccountabilityDayGlance,
  type AccountabilityGlance,
  type ChildGratitude,
  type DayFlow,
  type DayFlowCommitment,
  type FamilyCoachInsight,
  type FamilyMemberMood,
  type FamilyOsOverview,
  type FamilySummary,
  type TeamDay,
  type TeamUnlock,
} from '@/shared/api/family-os.api';
import './family-os-overview.css';

type MemberPulse = {
  key: string;
  name: string;
  roleCode: string;
  total: number;
  done: number;
  open: number;
  hot: number;
  skipped: number;
  late: number;
  tone: 'good' | 'warn' | 'risk' | 'idle';
};

const CAPABILITY_LABELS: Record<string, string> = {
  family_graph: 'Gia đình & thành viên',
  membership_without_account: 'Thành viên chưa cần tài khoản',
  membership_admin: 'Quản lý thành viên (Admin)',
  routine: 'Nhịp sống (Routine)',
  commitment_template: 'Mẫu cam kết trong ngày',
  day_flow: 'Luồng ngày (Day Flow)',
  commitment_progress: 'Ghi nhận tiến độ cam kết',
  context_reminder: 'Nhắc việc theo ngữ cảnh',
  family_agreement: 'Thỏa thuận nhà',
  consequence_library: 'Catalog thỏa thuận khi chưa xong',
  reward_library: 'Catalog thưởng',
  accountability_options_config: 'Tùy chỉnh catalog theo nhà',
  reflection_skip_reason: 'Ghi lý do khi bỏ qua',
  consequence_pending_confirm: 'Xác nhận thỏa thuận trước khi áp dụng',
  streak_reward_lite: 'Thưởng streak (bản nhẹ)',
  overview_api: 'Tổng quan nhà',
  family_coach_insight: 'Coach / gợi ý trong ngày',
};

const NON_GOAL_LABELS: Record<string, string> = {
  finance: 'Tài chính gia đình',
  health_records: 'Hồ sơ sức khỏe',
  medication: 'Quản lý thuốc',
  continuous_gps: 'Theo dõi GPS liên tục',
  school_lms: 'Hệ thống học của trường',
  smart_home: 'Nhà thông minh',
  child_surveillance: 'Giám sát trẻ',
  replace_parenting: 'Thay thế cha mẹ',
  harmful_punishment: 'Hình phạt gây hại',
  ai_environment_sensing: 'AI cảm nhận môi trường',
  accountability_engine_full: 'Engine accountability đầy đủ',
  freeform_llm_chat: 'Chat AI tự do',
};

const PHASE_LABELS: Record<string, string> = {
  'F2.5_accountability_lite': 'Nhịp nhà nhẹ · giai đoạn F2.5',
};

type AvatarGender = 'girl' | 'boy' | 'neutral';

const GIRL_TOKENS = [
  'nhi', 'linh', 'my', 'vy', 'chau', 'huong', 'lan', 'mai', 'ngoc', 'phuong',
  'quynh', 'thao', 'trang', 'uyen', 'yen', 'ha', 'hang', 'hanh', 'me',
];
const BOY_TOKENS = [
  'huy', 'duc', 'nam', 'tuan', 'hung', 'dung', 'phong', 'khoa', 'long', 'khang',
  'minh', 'quan', 'bo', 'ba',
];

function stripDiacritics(value: string): string {
  return value
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/đ/gi, 'd');
}

function inferGender(name: string): AvatarGender {
  const parts = stripDiacritics(name).toLowerCase().trim().split(/[\s._-]+/).filter(Boolean);
  const given = parts[parts.length - 1] ?? '';
  const all = [...new Set([given, ...parts])];
  if (all.some((t) => GIRL_TOKENS.includes(t))) return 'girl';
  if (all.some((t) => BOY_TOKENS.includes(t))) return 'boy';
  if (/(nhi|linh|vy|my)$/.test(given)) return 'girl';
  return 'neutral';
}

function memberAvatar(name: string, roleCode: string): { emoji: string; cls: string } {
  const gender = inferGender(name);
  if (roleCode !== 'child') {
    if (/^m[eẹ]$/i.test(name.trim()) || gender === 'girl')
      return { emoji: '👩', cls: 'is-adult' };
    if (/^b[oố]$/i.test(name.trim()) || gender === 'boy')
      return { emoji: '👨', cls: 'is-adult' };
    return { emoji: '🧑', cls: 'is-adult' };
  }
  if (gender === 'girl') return { emoji: '👧', cls: 'is-girl' };
  if (gender === 'boy') return { emoji: '👦', cls: 'is-boy' };
  return { emoji: '🧒', cls: 'is-neutral' };
}

function phaseLabel(phase: string) {
  return PHASE_LABELS[phase] ?? phase;
}

function scopeLabel(code: string, map: Record<string, string>) {
  return map[code] ?? code.replaceAll('_', ' ');
}

const MOOD_LABELS: Record<string, { emoji: string; label: string }> = {
  mad: { emoji: '😠', label: 'Giận' },
  sad: { emoji: '😟', label: 'Buồn' },
  ok: { emoji: '😐', label: 'Bình thường' },
  happy: { emoji: '😊', label: 'Vui vẻ' },
  love: { emoji: '🤩', label: 'Tuyệt vời' },
};

function moodDisplay(code?: string) {
  return MOOD_LABELS[code ?? ''] ?? { emoji: '—', label: 'Chưa ghi' };
}

const DEFAULT_FAMILY_TZ = 'Asia/Ho_Chi_Minh';

function hourFromLocalTime(localTime?: string, timezone = DEFAULT_FAMILY_TZ): number {
  if (localTime) {
    const hour = Number(localTime.slice(0, 2));
    if (Number.isFinite(hour)) return hour;
  }
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone: timezone,
    hour: 'numeric',
    hour12: false,
  }).formatToParts(new Date());
  return Number(parts.find((p) => p.type === 'hour')?.value ?? 12);
}

function timeOfDayGreeting(hour: number): string {
  if (hour < 11) return 'Chào buổi sáng';
  if (hour < 14) return 'Chào buổi trưa';
  if (hour < 18) return 'Chào buổi chiều';
  return 'Chào buổi tối';
}

function weekdayShortVi(d: Date): string {
  const day = d.getDay();
  if (day === 0) return 'CN';
  return `Thứ ${day + 1}`;
}

function resolveGreetingDate(flowDate?: string, timezone = DEFAULT_FAMILY_TZ): Date {
  if (flowDate) {
    const d = new Date(`${flowDate}T12:00:00`);
    if (!Number.isNaN(d.getTime())) return d;
  }
  try {
    const parts = new Intl.DateTimeFormat('en-CA', {
      timeZone: timezone,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    }).formatToParts(new Date());
    const y = parts.find((p) => p.type === 'year')?.value;
    const m = parts.find((p) => p.type === 'month')?.value;
    const day = parts.find((p) => p.type === 'day')?.value;
    if (y && m && day) return new Date(`${y}-${m}-${day}T12:00:00`);
  } catch {
    /* invalid timezone — fall through */
  }
  return new Date();
}

function greetingFor(
  localTime?: string,
  flowDate?: string,
  timezone?: string,
): string {
  const tz = timezone?.trim() || DEFAULT_FAMILY_TZ;
  const hour = hourFromLocalTime(localTime, tz);
  const when = resolveGreetingDate(flowDate, tz);
  const timePart = timeOfDayGreeting(hour);
  const weekday = weekdayShortVi(when);
  const dayMonth = `${when.getDate()}/${when.getMonth() + 1}`;
  return `${timePart} ${weekday} ngày ${dayMonth}`;
}

function roleLabel(role: string): string {
  switch (role) {
    case 'guardian':
      return 'Người lớn trong nhà';
    case 'caregiver':
      return 'Người chăm sóc';
    case 'child':
      return 'Con yêu';
    case 'viewer':
      return 'Người xem';
    default:
      return role;
  }
}

function weekdayLabel(dateIso: string): string {
  const d = new Date(`${dateIso}T12:00:00`);
  if (Number.isNaN(d.getTime())) return '?';
  return ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'][d.getDay()] ?? '?';
}

function toDateIso(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

function emptyDayGlance(dateIso: string): AccountabilityDayGlance {
  return {
    date: dateIso,
    isScored: false,
    isBeautifulDay: false,
    childDone: 0,
    childSkipped: 0,
    childOpen: 0,
    childLateDone: 0,
    appliedConsequences: 0,
  };
}

/** Always Mon–Sun so day chips stay clickable even when glance is empty. */
function buildWeekDayChips(glance: AccountabilityGlance | null, flowDate?: string) {
  const byDate = new Map((glance?.days ?? []).map((d) => [d.date, d]));
  const anchorIso = glance?.today ?? flowDate;
  const anchor = anchorIso ? new Date(`${anchorIso}T12:00:00`) : new Date();
  const todayIso = glance?.today ?? flowDate ?? toDateIso(anchor);
  const dow = anchor.getDay();
  const mondayOffset = dow === 0 ? -6 : 1 - dow;
  const monday = new Date(anchor);
  monday.setDate(anchor.getDate() + mondayOffset);

  return Array.from({ length: 7 }, (_, i) => {
    const d = new Date(monday);
    d.setDate(monday.getDate() + i);
    const dateIso = toDateIso(d);
    const day = byDate.get(dateIso) ?? emptyDayGlance(dateIso);
    return {
      key: dateIso,
      label: weekdayLabel(dateIso),
      on: day.isBeautifulDay,
      isToday: dateIso === todayIso,
      day,
    };
  });
}

function buildMemberPulses(
  members: FamilySummary['members'],
  commitments: DayFlowCommitment[],
): MemberPulse[] {
  return members.map((m) => {
    const mine = commitments.filter((c) => c.memberId === m.id);
    const doneOnly = mine.filter((c) => c.status === 'done').length;
    const skipped = mine.filter((c) => c.status === 'skipped').length;
    const late = mine.filter((c) => c.status === 'done' && c.isLateDone).length;
    const open = mine.filter((c) => c.status !== 'done' && c.status !== 'skipped').length;
    const hot = mine.filter(
      (c) =>
        c.status !== 'done' &&
        c.status !== 'skipped' &&
        (c.reminderState === 'overdue' || c.reminderState === 'due_now'),
    ).length;
    let tone: MemberPulse['tone'] = 'idle';
    if (mine.length === 0) tone = 'idle';
    else if (hot > 0 || skipped > 0) tone = 'risk';
    else if (open > 0 || late > 0) tone = 'warn';
    else tone = 'good';
    return {
      key: m.id,
      name: m.displayName,
      roleCode: m.roleCode,
      total: mine.length,
      done: doneOnly,
      open,
      hot,
      skipped,
      late,
      tone,
    };
  });
}

function computeFamilyHealth(flow: DayFlow | null, glance: AccountabilityGlance | null) {
  if (!flow || flow.totalCommitments <= 0) {
    return { score: null as number | null, label: 'Chưa mở ngày', tone: 'idle' as const };
  }

  const pct = flow.doneCount / flow.totalCommitments;
  let score = Math.round(pct * 70);
  if (glance?.todayIsBeautifulDay) score += 15;
  score += Math.min(15, (glance?.currentStreak ?? 0) * 2);
  score -= Math.min(25, (flow.overdueCount ?? 0) * 8);
  score = Math.max(0, Math.min(100, score));

  if (score >= 85) return { score, label: 'Nhà đang ấm', tone: 'good' as const };
  if (score >= 65) return { score, label: 'Nhà đang ổn', tone: 'calm' as const };
  if (score >= 40) return { score, label: 'Cần gần nhau hơn', tone: 'warn' as const };
  return { score, label: 'Cần bên cạnh con', tone: 'risk' as const };
}

function tonePill(tone: MemberPulse['tone']) {
  switch (tone) {
    case 'good':
      return <span className="fo-pill good">Đang ngoan</span>;
    case 'warn':
      return <span className="fo-pill warn">Đang làm</span>;
    case 'risk':
      return <span className="fo-pill risk">Cần sát cánh</span>;
    default:
      return <span className="fo-pill idle">Chưa có việc</span>;
  }
}

/** Coach card follows the child being viewed — not the API's default focus member. */
function childDoneTiming(items: DayFlowCommitment[]) {
  const doneItems = items.filter((c) => c.status === 'done');
  const late = doneItems.filter((c) => c.isLateDone).length;
  return {
    done: doneItems.length,
    late,
    onTime: Math.max(0, doneItems.length - late),
  };
}

function childLateAttention(done: number, late: number, onTime: number): string | undefined {
  if (late <= 0 || late < onTime) return undefined;
  return late === done
    ? `Xong ${done} việc nhưng đều sau giờ — mai tranh thủ đúng giờ hơn.`
    : `Xong ${done} việc nhưng ${late} việc sau giờ — mai tranh thủ đúng giờ hơn.`;
}

function childOnTimeStrength(name: string, onTime: number): string | undefined {
  if (onTime <= 0) return undefined;
  return onTime >= 2
    ? `${name} đã xong ${onTime} việc đúng giờ — đang góp phần cho điểm đội.`
    : `${name} đã xong ${onTime} việc — đang góp phần cho điểm đội.`;
}

function coachInsightForChild(
  base: FamilyCoachInsight | null,
  child: MemberPulse | null,
  childItems: DayFlowCommitment[],
  teamRemaining: number,
): FamilyCoachInsight | null {
  if (!base || !child) return base;

  const done = childItems.filter((c) => c.status === 'done').length;
  const skipped = childItems.filter((c) => c.status === 'skipped').length;
  const open = childItems.filter((c) => c.status !== 'done' && c.status !== 'skipped').length;
  const total = childItems.length;
  const { late: lateDone, onTime: onTimeDone } = childDoneTiming(childItems);
  const overdue = childItems.filter(
    (c) =>
      c.status !== 'done' &&
      c.status !== 'skipped' &&
      c.reminderState === 'overdue',
  );
  const aboutThisChild =
    base.focusMemberId === child.key ||
    (!!base.focusCommitmentTitle &&
      childItems.some(
        (c) =>
          c.title.toLowerCase() === (base.focusCommitmentTitle ?? '').toLowerCase(),
      ));

  const headline =
    total <= 0
      ? `${child.name} hôm nay chưa có Mission`
      : skipped > 0
        ? `${child.name}: ${done}/${total} xong · ${skipped} bỏ qua`
        : `${child.name}: ${done}/${total} cam kết hoàn thành`;

  if (total === 0) {
    return {
      ...base,
      headline,
      focusMemberId: child.key,
      focusMemberName: child.name,
      doneCount: 0,
      skippedCount: 0,
      openCount: 0,
      totalCount: 0,
      strength:
        teamRemaining > 0
          ? `${child.name} chưa có Mission riêng — cả đội vẫn còn ${teamRemaining} Mission trên hero nhà.`
          : `${child.name} hôm nay được nghỉ Mission — giữ nhịp vui với cả nhà.`,
      attention: undefined,
      pattern: undefined,
      proposal:
        teamRemaining > 0
          ? `💙 Team Play: nếu anh/chị đã xong, có thể mời nhắc nhẹ để cả đội giữ chuỗi — không gọi tên ai trên hero.`
          : 'Xem điểm đội phía trên — Hero luôn là cả nhà.',
      proposalCode: 'invite_sibling_nudge',
      ctaPath: '/family-os/day-flow',
      ctaLabel: 'Xem hôm nay cùng nhà',
    };
  }

  if (aboutThisChild) {
    const lateNote = childLateAttention(done, lateDone, onTimeDone);
    return {
      ...base,
      headline,
      focusMemberId: child.key,
      focusMemberName: child.name,
      doneCount: done,
      skippedCount: skipped,
      openCount: open,
      totalCount: total,
      strength:
        base.strength && lateDone >= onTimeDone && lateDone > 0
          ? undefined
          : base.strength ?? childOnTimeStrength(child.name, onTimeDone),
      attention:
        base.attention
          ? lateNote && !base.attention.includes('sau giờ')
            ? `${base.attention} ${lateNote}`
            : base.attention
          : lateNote ?? (open > 0 ? `Còn ${open} Mission của ${child.name} trong ngày.` : undefined),
    };
  }

  // API coach is about another sibling — rebuild for the child being viewed.
  let attention: string | undefined;
  let proposal: string | undefined;
  let proposalCode: string | undefined = 'open_today';
  let ctaPath = '/family-os/day-flow';
  let ctaLabel = 'Mở hôm nay';
  let strength: string | undefined = childOnTimeStrength(child.name, onTimeDone);
  const lateNote = childLateAttention(done, lateDone, onTimeDone);

  if (overdue.length > 0) {
    attention =
      overdue.length === 1
        ? `“${overdue[0].title}” của ${child.name} đang quá giờ — nhắc nhẹ, không đánh giá.`
        : `${child.name} có ${overdue.length} việc quá giờ; ưu tiên “${overdue[0].title}”.`;
    proposal = 'Mở Hôm nay và hỗ trợ đúng việc đang kẹt — Team Play, không so sánh anh chị em.';
    proposalCode = 'support_overdue';
    ctaLabel = 'Mở hôm nay';
  } else if (skipped > 0) {
    const s = childItems.find((c) => c.status === 'skipped');
    attention = s
      ? `${child.name} đã bỏ qua “${s.title}” — mở lại nếu đã làm xong, hoặc hỗ trợ nếu cần.`
      : `${child.name} có việc bỏ qua hôm nay.`;
    proposal = 'Cùng xem Hôm nay — giữ nhịp đội, không gọi tên trên hero nhà.';
    ctaLabel = 'Mở hôm nay';
  } else if (open > 0) {
    attention =
      open === 1
        ? `🎯 Còn 1 Mission của ${child.name} — góp phần để cả đội hoàn thành ngày.`
        : `🎯 Còn ${open} Mission của ${child.name} trong ngày — giữ nhịp đều.`;
    if (done === total - open && open > 0 && teamRemaining > open) {
      proposal = `Phần của ${child.name} gần xong. Cả đội vẫn còn Mission — nhìn hero nhà, không xếp hạng.`;
    } else {
      proposal = 'Theo dõi Hôm nay khi đến khung giờ tiếp theo.';
    }
    ctaLabel = 'Mở hôm nay';
  } else {
    strength = onTimeDone > 0 ? `${child.name} đã xong ${onTimeDone} việc đúng giờ (${done}/${total}).` : undefined;
    attention =
      lateNote ??
      (teamRemaining > 0
        ? `💙 Team Play: cả đội còn ${teamRemaining} Mission. ${child.name} có thể nhắc anh/chị — nếu muốn.`
        : 'Cả đội đã xong — giữ Celebration nhà phía trên.');
    proposal =
      teamRemaining > 0
        ? 'Không phải “em chưa làm” — mà là “cả đội còn Mission”. Mở Hôm nay để sát cánh.'
        : 'Xem Celebration / Team Unlock khi bố mẹ xác nhận.';
    proposalCode = teamRemaining > 0 ? 'invite_sibling_nudge' : 'celebrate_team_day';
    ctaLabel = 'Xem hôm nay cùng nhà';
  }

  return {
    ...base,
    headline,
    focusMemberId: child.key,
    focusMemberName: child.name,
    focusCommitmentTitle: overdue[0]?.title ?? base.focusCommitmentTitle,
    doneCount: done,
    skippedCount: skipped,
    openCount: open,
    totalCount: total,
    strength,
    attention,
    pattern: undefined,
    proposal,
    proposalCode,
    ctaPath,
    ctaLabel,
  };
}

function CoachInsightBlocks({ insight }: { insight: FamilyCoachInsight }) {
  return (
    <div>
      <div className="fo-card-title" style={{ marginBottom: 8 }}>
        {insight.headline}
      </div>
      {insight.strength ? (
        <div className="fo-coach-block strength">
          <small>Điều nhà làm tốt</small>
          <p>{insight.strength}</p>
        </div>
      ) : null}
      {insight.attention ? (
        <div className="fo-coach-block attention">
          <small>Nhà cần sát cánh</small>
          <p>{insight.attention}</p>
        </div>
      ) : null}
      {insight.pattern ? (
        <div className="fo-coach-block">
          <small>Nhịp {insight.patternWindowDays} ngày gần đây</small>
          <p>{insight.pattern}</p>
        </div>
      ) : null}
      {insight.proposal ? (
        <div className="fo-coach-block idea">
          <small>Gợi ý cùng nhau</small>
          <p>{insight.proposal}</p>
          <div className="fo-link-row">
            {insight.ctaPath ? (
              <Link to={insight.ctaPath}>
                <Button type="primary">{insight.ctaLabel || 'Cùng chỉnh nhịp'}</Button>
              </Link>
            ) : null}
            <Link to="/family-os/day-flow">
              <Button>Xem hôm nay</Button>
            </Link>
          </div>
        </div>
      ) : (
        <div className="fo-link-row">
          <Link to="/family-os/day-flow">
            <Button type="primary">Mở hôm nay cùng nhà</Button>
          </Link>
        </div>
      )}
      <p className="fo-footer-note">Bạn Coach quan sát nhịp nhà — hỗ trợ, không thay cha mẹ.</p>
    </div>
  );
}

export function FamilyOsOverviewPage() {
  const [loading, setLoading] = useState(true);
  const [overview, setOverview] = useState<FamilyOsOverview | null>(null);
  const [family, setFamily] = useState<FamilySummary | null>(null);
  const [flow, setFlow] = useState<DayFlow | null>(null);
  const [glance, setGlance] = useState<AccountabilityGlance | null>(null);
  const [coach, setCoach] = useState<FamilyCoachInsight | null>(null);
  const [teamDay, setTeamDay] = useState<TeamDay | null>(null);
  const [teamUnlocks, setTeamUnlocks] = useState<TeamUnlock[]>([]);
  const [gratitudes, setGratitudes] = useState<ChildGratitude[]>([]);
  const [moods, setMoods] = useState<FamilyMemberMood[]>([]);
  const [dayOpen, setDayOpen] = useState(false);
  const [selectedDay, setSelectedDay] = useState<AccountabilityDayGlance | null>(null);
  /** null = tự chọn (Coach / con nhiều việc nhất). */
  const [pickedChildId, setPickedChildId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [ov, families] = await Promise.all([fetchFamilyOsOverview(), fetchFamilies()]);
      setOverview(ov);
      const first = families[0] ?? null;
      setFamily(first);
      if (!first) {
        setFlow(null);
        setGlance(null);
        setCoach(null);
        setTeamDay(null);
        setTeamUnlocks([]);
        setGratitudes([]);
        setMoods([]);
        return;
      }
      const day = await ensureDayFlow(first.id);
      setFlow(day);
      const [gl, insight, team, unlockRows, thanks, moodRows] = await Promise.all([
        fetchAccountabilityGlance(first.id),
        fetchCoachInsight(first.id, day.flowDate),
        fetchTeamDay(first.id, day.flowDate),
        fetchTeamUnlocks(first.id, day.flowDate, true).then(() =>
          fetchTeamUnlocks(first.id),
        ),
        fetchChildGratitude(first.id, day.flowDate).catch(() => [] as ChildGratitude[]),
        fetchFamilyMoods(first.id, day.flowDate).catch(() => [] as FamilyMemberMood[]),
      ]);
      setGlance(gl);
      setCoach(insight);
      setTeamDay(team);
      setTeamUnlocks(unlockRows);
      setGratitudes(thanks);
      setMoods(moodRows);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không mở được ngôi nhà hôm nay'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const pulses = useMemo(
    () => (family ? buildMemberPulses(family.members, flow?.commitments ?? []) : []),
    [family, flow],
  );

  const children = useMemo(
    () => pulses.filter((p) => p.roleCode === 'child'),
    [pulses],
  );
  const childrenWithWork = useMemo(
    () => [...children].filter((p) => p.total > 0).sort((a, b) => b.total - a.total),
    [children],
  );

  /** Một con tại một thời điểm — không gộp 5 con vào một con số. */
  const focusChild = useMemo(() => {
    if (pickedChildId) {
      const picked = children.find((p) => p.key === pickedChildId);
      if (picked) return picked;
    }
    if (coach?.focusMemberId) {
      const hit = children.find((p) => p.key === coach.focusMemberId);
      if (hit) return hit;
    }
    return childrenWithWork[0] ?? children[0] ?? null;
  }, [pickedChildId, children, childrenWithWork, coach]);

  const selectChild = (id: string) => {
    setPickedChildId(id);
  };

  const focusItems = useMemo(() => {
    if (!flow || !focusChild) return [] as DayFlowCommitment[];
    return flow.commitments.filter((c) => c.memberId === focusChild.key);
  }, [flow, focusChild]);

  const focusDone = focusItems.filter((c) => c.status === 'done').length;
  const focusSkipped = focusItems.filter((c) => c.status === 'skipped').length;
  const focusLate = focusItems.filter((c) => c.status === 'done' && c.isLateDone).length;
  const focusOnTime = Math.max(0, focusDone - focusLate);
  const focusOverdue = focusItems.filter(
    (c) =>
      c.status !== 'done' &&
      c.status !== 'skipped' &&
      c.reminderState === 'overdue',
  ).length;
  const focusTotal = focusItems.length;
  const focusPercent =
    focusTotal > 0 ? Math.round((focusDone / focusTotal) * 100) : 0;

  const health = useMemo(() => computeFamilyHealth(flow, glance), [flow, glance]);
  const greeting = greetingFor(flow?.localTime, flow?.flowDate, family?.timezone);

  const teamSnapshot = teamDay ?? {
    teamDone: 0,
    teamTotal: 0,
    teamPercent: 0,
    remainingMissions: 0,
    teamComplete: false,
    heroMissionLine: 'Hôm nay nhà chưa có Mission — mở nhịp sống để cùng bắt đầu.',
    flowDate: flow?.flowDate ?? '',
    children: [],
  };

  const todayUnlock = useMemo(
    () =>
      teamUnlocks.find((u) => u.flowDate === flow?.flowDate) ??
      teamUnlocks[0] ??
      null,
    [teamUnlocks, flow?.flowDate],
  );

  const focusChildMood = useMemo(
    () => (focusChild ? moods.find((m) => m.memberId === focusChild.key) ?? null : null),
    [moods, focusChild],
  );

  const todayGratitudes = useMemo(
    () =>
      focusChild
        ? gratitudes.filter((g) => g.fromMemberId === focusChild.key)
        : gratitudes,
    [gratitudes, focusChild],
  );

  const weekBeautiful = useMemo(
    () => (glance?.days ?? []).filter((d) => d.isBeautifulDay).length,
    [glance],
  );

  const focusCoach = useMemo(
    () =>
      coachInsightForChild(
        coach,
        focusChild,
        focusItems,
        teamSnapshot.remainingMissions,
      ),
    [coach, focusChild, focusItems, teamSnapshot.remainingMissions],
  );

  const nudgeCandidate = useMemo(() => {
    if (childrenWithWork.length < 2 || teamSnapshot.remainingMissions === 0) return null;
    const doneKids = childrenWithWork.filter((c) => c.open === 0 && c.total > 0);
    const helper = doneKids[0] ?? null;
    if (!helper) return null;
    return { helperName: helper.name, remaining: teamSnapshot.remainingMissions };
  }, [childrenWithWork, teamSnapshot.remainingMissions]);

  const weekDays = useMemo(
    () => buildWeekDayChips(glance, flow?.flowDate),
    [glance, flow?.flowDate],
  );

  const openDay = (day: AccountabilityDayGlance) => {
    setSelectedDay(day);
    setDayOpen(true);
  };

  const flowTone =
    focusOverdue > 0 || focusSkipped > 0
      ? 'risk'
      : focusLate > 0
        ? 'warn'
        : 'good';

  const formatDayTitle = (dateIso: string) => {
    const d = new Date(`${dateIso}T12:00:00`);
    if (Number.isNaN(d.getTime())) return dateIso;
    const map = ['Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy'];
    const dd = String(d.getDate()).padStart(2, '0');
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    return `${map[d.getDay()]}, ${dd}/${mm}`;
  };

  return (
    <div className="fo-home">
      <div className="fo-hero">
        <div className="fo-hero-left">
          <div className="fo-hero-avatar" aria-hidden>
            🏡
          </div>
          <div>
            <h1>
              {greeting}, {family?.displayName ?? 'ngôi nhà mình'}!{' '}
              <span aria-hidden>👋</span>
            </h1>
            <p>
              Cùng giữ những lời đã hứa với nhau. FamilyOS chỉ là người bạn đồng hành — không thay
              cha mẹ, không giám sát trẻ.
            </p>
          </div>
        </div>
        <Space wrap>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            Làm mới
          </Button>
          <Link to="/family-os/day-flow">
            <Button type="primary" size="large">
              Mở hôm nay cùng nhà
            </Button>
          </Link>
        </Space>
      </div>

      <div className="fo-grid" style={{ opacity: loading ? 0.65 : 1 }}>
        {teamSnapshot.teamComplete ? (
          <section className="fo-card span-12 fo-celebrate">
            <div className="fo-celebrate-inner">
              <span className="fo-celebrate-emoji" aria-hidden>
                🎉
              </span>
              <div>
                <div className="fo-card-label">Family Team Play</div>
                <div className="fo-card-title">Mission Complete!</div>
                <p className="fo-footer-note" style={{ marginTop: 4 }}>
                  Cả đội đã xong ngày hôm nay — không phải từng người, mà là ngôi nhà mình.
                </p>
              </div>
            </div>
          </section>
        ) : null}

        <section className={`fo-card span-4 fo-team-hero${teamSnapshot.teamComplete ? ' is-complete' : ''}`}>
          <div className="fo-card-label">🏡 Gia đình hôm nay</div>
          <div className="fo-team-score-row">
            <strong className="fo-team-pct">
              {teamSnapshot.teamTotal > 0 ? `${teamSnapshot.teamPercent}%` : '—'}
            </strong>
            <span className={`fo-pill ${health.tone}`}>
              {teamSnapshot.teamComplete
                ? 'Cả đội xong'
                : teamSnapshot.teamTotal > 0
                  ? 'Đang cùng làm'
                  : health.label}
            </span>
          </div>
          <div
            className={`fo-team-bar${teamSnapshot.teamComplete ? ' is-complete' : ''}`}
            role="progressbar"
            aria-valuenow={teamSnapshot.teamPercent}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-label="Tiến độ cả nhà hôm nay"
          >
            <span style={{ width: `${teamSnapshot.teamPercent}%` }} />
          </div>
          <p className="fo-team-mission">{teamSnapshot.heroMissionLine}</p>
          <div className="fo-coop-row" title="Chuỗi nhà từ API accountability-glance">
            <small>Chuỗi & ngày đẹp</small>
            <strong>{glance?.currentStreak ?? 0} ngày</strong>
            <span className="fo-coop-parts">
              🌟{weekBeautiful} ngày đẹp tuần này
              {glance?.todayIsBeautifulDay ? ' · hôm nay ✓' : ''}
            </span>
          </div>
          {nudgeCandidate ? (
            <div className="fo-nudge-tease">
              <p>
                💙 <strong>{nudgeCandidate.helperName}</strong> đã xong phần mình — cả đội còn{' '}
                {nudgeCandidate.remaining} Mission. Nhắc nhẹ trên app con (TP3).
              </p>
            </div>
          ) : null}
          {children.length > 1 ? (
            <div className="fo-child-switch" role="tablist" aria-label="Xem chi tiết từng con">
              {children.map((c) => (
                <button
                  key={c.key}
                  type="button"
                  role="tab"
                  aria-selected={focusChild?.key === c.key}
                  className={`fo-child-chip${focusChild?.key === c.key ? ' on' : ''}${
                    c.total === 0 ? ' idle' : ''
                  }`}
                  onClick={() => selectChild(c.key)}
                >
                  {c.name}
                  {c.total > 0 ? (
                    <em>
                      {c.done}/{c.total}
                    </em>
                  ) : (
                    <em>—</em>
                  )}
                </button>
              ))}
            </div>
          ) : null}
          <p className="fo-footer-note" style={{ marginTop: 10 }}>
            Hero là cả nhà. Chạm tên để xem nhịp riêng — không xếp hạng anh chị em.
          </p>
          <div className="fo-illust" aria-hidden>
            🏡
          </div>
        </section>

        <section className="fo-card span-4">
          <div className="fo-card-label">
            {focusChild ? `Nhịp sống của ${focusChild.name}` : 'Nhịp sống hôm nay'}
          </div>
          <div className="fo-card-title">{flow?.routineName ?? 'Chưa mở ngày'}</div>
          {focusChild && focusTotal > 0 ? (
            <>
              <div style={{ marginTop: 6, fontWeight: 650 }}>
                {focusDone}/{focusTotal} việc xong · {focusPercent}%
                {focusSkipped > 0 ? ` · ${focusSkipped} bỏ qua` : ''}
              </div>
              <div className={`fo-progress is-${flowTone}`}>
                <span style={{ width: `${focusPercent}%` }} />
              </div>
              <div className="fo-stat-row">
                <span className="fo-stat good">✓ Xong đúng giờ · {focusOnTime}</span>
                {focusLate > 0 ? (
                  <span className="fo-stat warn">⏰ Xong muộn · {focusLate}</span>
                ) : null}
                {focusSkipped > 0 ? (
                  <span className="fo-stat risk">♡ Bỏ qua · {focusSkipped}</span>
                ) : null}
                {focusOverdue > 0 ? (
                  <span className="fo-stat risk">⚠ Quá hạn · {focusOverdue}</span>
                ) : null}
                {focusOverdue === 0 &&
                focusChild.open === 0 &&
                focusSkipped === 0 &&
                focusDone === focusTotal ? (
                  <span className="fo-pill good">Xong đủ rồi</span>
                ) : null}
              </div>
              <p className="fo-footer-note">
                Chi tiết của <strong>{focusChild.name}</strong> — hero phía trên vẫn là điểm đội.
              </p>
            </>
          ) : (
            <p className="fo-footer-note">
              {flow
                ? 'Hôm nay chưa có việc gắn cho con. Thêm mẫu cam kết trong Nhịp sống.'
                : 'Mở hôm nay để nhà cùng bắt đầu nhịp sống.'}
            </p>
          )}
          <div className="fo-illust" aria-hidden>
            🎒
          </div>
        </section>

        <section className="fo-card span-4">
          <div className="fo-card-label">🏡 Team Streak · chuỗi nhà</div>
          <div className="fo-card-title">
            {(glance?.currentStreak ?? 0) > 0
              ? `${glance?.currentStreak} ngày đẹp của nhà`
              : 'Bắt đầu chuỗi nhà từ hôm nay'}
          </div>
          <p className="fo-footer-note" style={{ marginBottom: 6 }}>
            Chuỗi tính cả đội. Chạm ngày để xem chi tiết.
          </p>
          <div className="fo-week" role="group" aria-label="Ngày trong tuần · chuỗi nhà">
            {weekDays.map((d) => (
              <button
                key={d.key}
                type="button"
                className={`fo-day${d.on ? ' on' : ''}${d.isToday ? ' is-today' : ''}`}
                aria-current={d.isToday ? 'date' : undefined}
                onClick={() => openDay(d.day)}
                title={
                  d.isToday
                    ? `Hôm nay · ${formatDayTitle(d.day.date)}`
                    : `Xem ${formatDayTitle(d.day.date)}`
                }
              >
                {d.label}
              </button>
            ))}
          </div>
          <Link to="/family-os/day-flow" className="fo-reward-hit">
            <div className="fo-card-label">
              Team Unlock
              {focusChild ? ` · ${focusChild.name} cùng đội` : ''}
            </div>
            <div className="fo-card-title" style={{ fontSize: '1.05rem' }}>
              {todayUnlock?.labelVi ?? 'Chưa có thưởng đội hôm nay'}
            </div>
            <p className="fo-footer-note">
              {todayUnlock?.status === 'confirmed'
                ? 'Đã xác nhận — cả nhà cùng tận hưởng trên app.'
                : todayUnlock?.status === 'pending_confirm' &&
                    (teamSnapshot.teamComplete || glance?.todayIsBeautifulDay)
                  ? `${todayUnlock.labelVi} sẵn sàng — xác nhận trên app Mẹ hoặc tab Hôm nay.`
                  : todayUnlock
                    ? `Tiến độ đội: ${todayUnlock.teamPercent}% (${todayUnlock.teamDone}/${todayUnlock.teamTotal})`
                    : 'Thiết lập thưởng đội trong Thỏa thuận nhà hoặc seed gia đình.'}
            </p>
          </Link>
          {focusChildMood ? (
            <div className="fo-mood-snippet" style={{ marginTop: 10 }}>
              <span className="fo-card-label">Tâm trạng {focusChild?.name}</span>
              <p>
                {moodDisplay(focusChildMood.moodCode).emoji}{' '}
                {moodDisplay(focusChildMood.moodCode).label}
                {focusChildMood.note ? ` · «${focusChildMood.note}»` : ''}
              </p>
            </div>
          ) : focusChild ? (
            <p className="fo-footer-note" style={{ marginTop: 10 }}>
              {focusChild.name} chưa ghi tâm trạng hôm nay trên app con.
            </p>
          ) : null}
          {todayGratitudes.length > 0 ? (
            <div className="fo-gratitude-snippet" style={{ marginTop: 8 }}>
              <span className="fo-card-label">Lời cảm ơn từ con</span>
              {todayGratitudes.slice(0, 2).map((g) => (
                <p key={g.id}>
                  💜 {g.messageVi}
                  {g.praiseContext ? ` · «${g.praiseContext}»` : ''}
                </p>
              ))}
            </div>
          ) : null}
          <div className="fo-illust" aria-hidden>
            🎁
          </div>
        </section>

        <section className="fo-card span-5">
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
            <div className="fo-card-label">Người trong nhà</div>
            <Link to="/family-os/members">Đổi tên nhà / thành viên</Link>
          </div>
          {children.length > 1 ? (
            <p className="fo-footer-note" style={{ marginBottom: 8 }}>
              Chạm vào từng con để xem nhịp sống riêng — không gộp chung một bảng.
            </p>
          ) : null}
          {pulses.length === 0 ? (
            <p className="fo-footer-note">Chưa có thành viên trong ngôi nhà.</p>
          ) : (
            pulses.map((p) => {
              const av = memberAvatar(p.name, p.roleCode);
              const isFocus = focusChild?.key === p.key;
              const canSelect = p.roleCode === 'child';
              const RowTag = canSelect ? 'button' : 'div';
              return (
                <RowTag
                  key={p.key}
                  type={canSelect ? 'button' : undefined}
                  className={`fo-member${isFocus ? ' is-focus' : ''}${
                    canSelect ? ' is-selectable' : ''
                  }`}
                  onClick={canSelect ? () => selectChild(p.key) : undefined}
                >
                  <div className={`fo-member-avatar ${av.cls}`} aria-hidden>
                    {av.emoji}
                  </div>
                  <div className="fo-member-body">
                    <strong>
                      {p.name}
                      {isFocus ? ' · đang xem' : ''}
                    </strong>
                    <span>
                      {roleLabel(p.roleCode)}
                      {p.total > 0
                        ? ` · ${p.done} xong${p.skipped > 0 ? ` · ${p.skipped} bỏ qua` : ''}${
                            p.late > 0 ? ` · ${p.late} muộn` : ''
                          }`
                        : ''}
                    </span>
                  </div>
                  <Space size={8} direction="vertical" align="end">
                    <Typography.Text>
                      {p.total === 0 ? '—' : `${p.done}/${p.total}`}
                    </Typography.Text>
                    {tonePill(p.tone)}
                  </Space>
                </RowTag>
              );
            })
          )}
        </section>

        <section className="fo-card span-7">
          <div className="fo-card-label">
            Bạn Coach hôm nay · Team Play
            {focusChild ? ` · ${focusChild.name}` : ''}
          </div>
          {focusCoach ? (
            <CoachInsightBlocks insight={focusCoach} key={focusChild?.key ?? 'coach'} />
          ) : (
            <p className="fo-footer-note">
              Mở hôm nay để Coach ngồi cạnh nhà, quan sát nhịp sống và gợi ý ấm áp.
            </p>
          )}
          <div className="fo-illust" aria-hidden>
            🤖
          </div>
        </section>
      </div>

      <Drawer
        title={
          selectedDay
            ? `Ngày ${formatDayTitle(selectedDay.date)}`
            : 'Chi tiết ngày'
        }
        open={dayOpen}
        onClose={() => {
          setDayOpen(false);
          setSelectedDay(null);
        }}
        width={400}
      >
        {selectedDay ? (
          <Space direction="vertical" size={14} style={{ width: '100%' }}>
            <div>
              {selectedDay.isBeautifulDay ? (
                <span className="fo-pill good">Ngày đẹp</span>
              ) : selectedDay.isScored ? (
                <span className="fo-pill warn">Đã ghi nhận · chưa đủ ngày đẹp</span>
              ) : (
                <span className="fo-pill idle">Chưa ghi nhận đủ</span>
              )}
            </div>
            <div className="fo-stat-row">
              <span className="fo-stat good">✓ Xong · {selectedDay.childDone}</span>
              {selectedDay.childLateDone > 0 ? (
                <span className="fo-stat warn">⏰ Muộn · {selectedDay.childLateDone}</span>
              ) : null}
              {selectedDay.childSkipped > 0 ? (
                <span className="fo-stat risk">♡ Bỏ qua · {selectedDay.childSkipped}</span>
              ) : null}
              {selectedDay.childOpen > 0 ? (
                <span className="fo-stat calm">○ Còn mở · {selectedDay.childOpen}</span>
              ) : null}
            </div>
            {selectedDay.appliedConsequences > 0 ? (
              <Typography.Text type="secondary">
                Thỏa thuận đã áp dụng: {selectedDay.appliedConsequences}
              </Typography.Text>
            ) : null}
            {!selectedDay.isScored &&
            selectedDay.childDone + selectedDay.childSkipped + selectedDay.childOpen === 0 ? (
              <Typography.Text type="secondary">
                Ngày này chưa có dữ liệu nhịp sống — mở Hôm nay để xem / ghi nhận việc của nhà.
              </Typography.Text>
            ) : null}
            <Link to="/family-os/day-flow">
              <Button type="primary" block>
                Mở hôm nay cùng nhà
              </Button>
            </Link>
          </Space>
        ) : null}
      </Drawer>

      {overview ? (
        <Collapse
          className="fo-scope"
          items={[
            {
              key: 'dev',
              label: 'Phạm vi giai đoạn này (đang làm / chưa làm)',
              children: (
                <Space direction="vertical" style={{ width: '100%' }} size={12}>
                  <div>
                    <Typography.Text type="secondary">Giai đoạn: </Typography.Text>
                    <Tag color="processing">{phaseLabel(overview.phase)}</Tag>
                  </div>
                  <div>
                    <Typography.Text strong>Đang có trong ngôi nhà này</Typography.Text>
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 8, marginTop: 4 }}>
                      Những phần FamilyOS đã bật cho gia đình này (theo API overview).
                    </Typography.Paragraph>
                    <div>
                      {overview.enabledCapabilities.map((c) => (
                        <Tag key={c} color="blue" style={{ marginBottom: 4 }} title={c}>
                          {scopeLabel(c, CAPABILITY_LABELS)}
                        </Tag>
                      ))}
                    </div>
                  </div>
                  <div>
                    <Typography.Text strong>Cố ý chưa làm</Typography.Text>
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 8, marginTop: 4 }}>
                      Ngoài phạm vi giai đoạn này — không phải thiếu sót tạm thời trên màn hình.
                    </Typography.Paragraph>
                    <div>
                      {overview.explicitNonGoals.map((c) => (
                        <Tag key={c} style={{ marginBottom: 4 }} title={c}>
                          {scopeLabel(c, NON_GOAL_LABELS)}
                        </Tag>
                      ))}
                    </div>
                  </div>
                  <Typography.Text type="secondary">{overview.legalBoundary}</Typography.Text>
                </Space>
              ),
            },
          ]}
        />
      ) : null}
    </div>
  );
}
