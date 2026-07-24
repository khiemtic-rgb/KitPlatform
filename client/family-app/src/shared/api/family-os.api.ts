import { http } from '@/shared/api/http';
import { normalizeLateStarLabelVi } from '@/shared/flow/late-duration';

type Row = Record<string, unknown>;

function asArray(value: unknown): Row[] {
  return Array.isArray(value) ? (value as Row[]) : [];
}

export interface FamilyMembership {
  id: string;
  displayName: string;
  roleCode: string;
  dateOfBirth?: string;
}

export interface FamilySummary {
  id: string;
  displayName: string;
  members: FamilyMembership[];
}

export interface DayFlowCommitment {
  id: string;
  title: string;
  memberId?: string;
  memberName?: string;
  status: string;
  skipReason?: string;
  windowStart?: string;
  windowEnd?: string;
  completedAt?: string;
  isLateDone?: boolean;
  reminderState: string;
  reminderLabel?: string;
  evidenceUrl?: string;
  allowEarlyComplete?: boolean;
  earlyLeadMinutes?: number;
  onTimeGraceMinutes?: number;
  starReward?: number;
  starDelta?: number;
  starTier?: string;
  starLabelVi?: string;
  projectedStarDelta?: number;
  projectedStarLabelVi?: string;
  memberStarBalance?: number;
  starPosted?: boolean;
  starComputedAt?: string;
}

export interface DayFlow {
  id: string;
  familyId: string;
  routineName: string;
  flowDate: string;
  totalCommitments: number;
  doneCount: number;
  pendingCount: number;
  dueNowCount: number;
  overdueCount: number;
  upcomingCount: number;
  localTime?: string;
  commitments: DayFlowCommitment[];
}

export const SKIP_REASON_OPTIONS = [
  { value: 'forgot', label: 'Mình quên mất' },
  { value: 'busy', label: 'Mình đang bận việc khác' },
  { value: 'need_help', label: 'Mình cần bố mẹ giúp' },
  { value: 'not_ready', label: 'Mình chưa sẵn sàng' },
  { value: 'sick', label: 'Mình hơi mệt' },
  { value: 'other', label: 'Có chuyện khác' },
] as const;

export type SkipReasonCode = (typeof SKIP_REASON_OPTIONS)[number]['value'];

export function skipReasonLabel(code?: string): string | undefined {
  return SKIP_REASON_OPTIONS.find((o) => o.value === code)?.label;
}

export async function loginFamilyParent(input: {
  tenantCode: string;
  username: string;
  password: string;
}): Promise<{ accessToken: string; refreshToken: string | null }> {
  const { data } = await http.post<Row>('/auth/login', {
    tenantCode: input.tenantCode.trim().toUpperCase(),
    username: input.username.trim(),
    password: input.password,
  });
  const accessToken = String(data.accessToken ?? data.AccessToken ?? '');
  if (!accessToken) throw new Error('Đăng nhập không trả về token');
  const refreshRaw = data.refreshToken ?? data.RefreshToken;
  return {
    accessToken,
    refreshToken: refreshRaw != null ? String(refreshRaw) : null,
  };
}

export async function fetchFamilies(): Promise<FamilySummary[]> {
  const { data } = await http.get<unknown>('/family-os/families');
  return asArray(data).map((r) => ({
    id: String(r.id ?? r.Id),
    displayName: String(r.displayName ?? r.DisplayName ?? ''),
    members: asArray(r.members ?? r.Members).map((m) => ({
      id: String(m.id ?? m.Id),
      displayName: String(m.displayName ?? m.DisplayName ?? ''),
      roleCode: String(m.roleCode ?? m.RoleCode ?? ''),
      dateOfBirth:
        m.dateOfBirth != null || m.DateOfBirth != null
          ? String(m.dateOfBirth ?? m.DateOfBirth)
          : undefined,
    })),
  }));
}

function mapCommitment(c: Row): DayFlowCommitment {
  return {
    id: String(c.id ?? c.Id),
    title: String(c.title ?? c.Title ?? ''),
    memberId:
      c.memberId != null || c.MemberId != null ? String(c.memberId ?? c.MemberId) : undefined,
    memberName:
      c.memberName != null || c.MemberName != null
        ? String(c.memberName ?? c.MemberName)
        : undefined,
    status: String(c.status ?? c.Status ?? 'pending'),
    skipReason:
      c.skipReason != null || c.SkipReason != null
        ? String(c.skipReason ?? c.SkipReason)
        : undefined,
    windowStart:
      c.windowStart != null || c.WindowStart != null
        ? String(c.windowStart ?? c.WindowStart)
        : undefined,
    windowEnd:
      c.windowEnd != null || c.WindowEnd != null
        ? String(c.windowEnd ?? c.WindowEnd)
        : undefined,
    reminderState: String(c.reminderState ?? c.ReminderState ?? 'none'),
    reminderLabel:
      c.reminderLabel != null || c.ReminderLabel != null
        ? String(c.reminderLabel ?? c.ReminderLabel)
        : undefined,
    isLateDone: Boolean(c.isLateDone ?? c.IsLateDone ?? false),
    completedAt:
      c.completedAt != null || c.CompletedAt != null
        ? String(c.completedAt ?? c.CompletedAt)
        : undefined,
    evidenceUrl:
      c.evidenceUrl != null || c.EvidenceUrl != null
        ? String(c.evidenceUrl ?? c.EvidenceUrl)
        : undefined,
    allowEarlyComplete: Boolean(c.allowEarlyComplete ?? c.AllowEarlyComplete ?? false),
    earlyLeadMinutes: Number(c.earlyLeadMinutes ?? c.EarlyLeadMinutes ?? 0) || 0,
    onTimeGraceMinutes: Number(c.onTimeGraceMinutes ?? c.OnTimeGraceMinutes ?? 0) || 0,
    starReward: Number(c.starReward ?? c.StarReward ?? 0) || undefined,
    starDelta:
      c.starDelta != null || c.StarDelta != null
        ? Number(c.starDelta ?? c.StarDelta)
        : undefined,
    starTier:
      c.starTier != null || c.StarTier != null
        ? String(c.starTier ?? c.StarTier)
        : undefined,
    starLabelVi:
      c.starLabelVi != null || c.StarLabelVi != null
        ? normalizeLateStarLabelVi(String(c.starLabelVi ?? c.StarLabelVi))
        : undefined,
    projectedStarDelta:
      c.projectedStarDelta != null || c.ProjectedStarDelta != null
        ? Number(c.projectedStarDelta ?? c.ProjectedStarDelta)
        : undefined,
    projectedStarLabelVi:
      c.projectedStarLabelVi != null || c.ProjectedStarLabelVi != null
        ? normalizeLateStarLabelVi(String(c.projectedStarLabelVi ?? c.ProjectedStarLabelVi))
        : undefined,
    memberStarBalance:
      c.memberStarBalance != null || c.MemberStarBalance != null
        ? Number(c.memberStarBalance ?? c.MemberStarBalance)
        : undefined,
    starPosted: Boolean(c.starPosted ?? c.StarPosted ?? false),
    starComputedAt:
      c.starComputedAt != null || c.StarComputedAt != null
        ? String(c.starComputedAt ?? c.StarComputedAt)
        : undefined,
  };
}

export interface CommitmentProgressResult {
  commitment: DayFlowCommitment;
  memberStarBalance?: number;
}

export async function ensureDayFlow(familyId: string): Promise<DayFlow> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/day-flows/ensure`, {});
  return {
    id: String(data.id ?? data.Id),
    familyId: String(data.familyId ?? data.FamilyId ?? ''),
    routineName: String(data.routineName ?? data.RoutineName ?? ''),
    flowDate: String(data.flowDate ?? data.FlowDate ?? ''),
    totalCommitments: Number(data.totalCommitments ?? data.TotalCommitments ?? 0),
    doneCount: Number(data.doneCount ?? data.DoneCount ?? 0),
    pendingCount: Number(data.pendingCount ?? data.PendingCount ?? 0),
    dueNowCount: Number(data.dueNowCount ?? data.DueNowCount ?? 0),
    overdueCount: Number(data.overdueCount ?? data.OverdueCount ?? 0),
    upcomingCount: Number(data.upcomingCount ?? data.UpcomingCount ?? 0),
    localTime:
      data.localTime != null || data.LocalTime != null
        ? String(data.localTime ?? data.LocalTime)
        : undefined,
    commitments: asArray(data.commitments ?? data.Commitments).map(mapCommitment),
  };
}

export async function uploadCommitmentEvidence(
  familyId: string,
  file: File,
): Promise<string> {
  const form = new FormData();
  form.append('file', file);
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/evidence`, form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  const url = String(data.url ?? data.Url ?? '');
  if (!url) throw new Error('Upload không trả về URL');
  return url;
}

export async function updateCommitmentProgress(
  familyId: string,
  commitmentId: string,
  status: 'done' | 'pending' | 'skipped',
  skipReason?: SkipReasonCode,
  evidenceUrl?: string,
  parentOverride = false,
): Promise<CommitmentProgressResult> {
  const { data } = await http.patch<Row>(
    `/family-os/families/${familyId}/commitments/${commitmentId}`,
    {
      status,
      skipReason: skipReason ?? null,
      evidenceUrl: evidenceUrl ?? null,
      parentOverride,
    },
  );
  const commitment = mapCommitment(data);
  return {
    commitment,
    memberStarBalance: commitment.memberStarBalance,
  };
}

export async function approveCommitmentStars(
  familyId: string,
  commitmentId: string,
): Promise<CommitmentProgressResult> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/commitments/${commitmentId}/approve-stars`,
  );
  const commitment = mapCommitment(data);
  return {
    commitment,
    memberStarBalance: commitment.memberStarBalance,
  };
}

export async function fetchMemberStarBalance(
  familyId: string,
  memberId: string,
): Promise<number> {
  const { data } = await http.get<Row>(
    `/family-os/families/${familyId}/members/${memberId}/star-balance`,
  );
  return Number(data.balance ?? data.Balance ?? 0);
}

export interface SoftLockGuide {
  titleVi: string;
  bodyVi: string;
  iosUrl: string;
  androidUrl: string;
  shareTextVi: string;
}

export interface ConsequenceEvent {
  id: string;
  commitmentId: string;
  commitmentTitle: string;
  memberName?: string;
  consequenceCode: string;
  labelVi: string;
  triggerSkipReason?: string;
  status: string;
  softLockGuide?: SoftLockGuide;
}

function mapSoftLockGuide(row: Row | undefined): SoftLockGuide | undefined {
  if (!row) return undefined;
  const title = row.titleVi ?? row.TitleVi;
  if (title == null) return undefined;
  return {
    titleVi: String(title),
    bodyVi: String(row.bodyVi ?? row.BodyVi ?? ''),
    iosUrl: String(row.iosUrl ?? row.IosUrl ?? ''),
    androidUrl: String(row.androidUrl ?? row.AndroidUrl ?? ''),
    shareTextVi: String(row.shareTextVi ?? row.ShareTextVi ?? ''),
  };
}

function mapConsequenceEvent(row: Row): ConsequenceEvent {
  const guideRaw = (row.softLockGuide ?? row.SoftLockGuide) as Row | undefined;
  return {
    id: String(row.id ?? row.Id ?? ''),
    commitmentId: String(row.commitmentId ?? row.CommitmentId ?? ''),
    commitmentTitle: String(row.commitmentTitle ?? row.CommitmentTitle ?? ''),
    memberName:
      row.memberName != null || row.MemberName != null
        ? String(row.memberName ?? row.MemberName)
        : undefined,
    consequenceCode: String(row.consequenceCode ?? row.ConsequenceCode ?? ''),
    labelVi: String(row.labelVi ?? row.LabelVi ?? ''),
    triggerSkipReason:
      row.triggerSkipReason != null || row.TriggerSkipReason != null
        ? String(row.triggerSkipReason ?? row.TriggerSkipReason)
        : undefined,
    status: String(row.status ?? row.Status ?? ''),
    softLockGuide: mapSoftLockGuide(guideRaw),
  };
}

export async function fetchConsequenceEvents(
  familyId: string,
  flowDate?: string,
): Promise<ConsequenceEvent[]> {
  const { data } = await http.get<unknown>(`/family-os/families/${familyId}/consequence-events`, {
    params: flowDate ? { flowDate } : undefined,
  });
  return asArray(data).map(mapConsequenceEvent);
}

export async function decideConsequenceEvent(
  familyId: string,
  eventId: string,
  payload: { status: 'applied' | 'waived'; decidedBy: string; decisionNote?: string },
): Promise<ConsequenceEvent> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/consequence-events/${eventId}/decide`,
    payload,
  );
  return mapConsequenceEvent(data);
}

export interface AccountabilityDayGlance {
  date: string;
  isScored: boolean;
  isBeautifulDay: boolean;
  childDone: number;
  childSkipped: number;
  childOpen: number;
  childLateDone: number;
}

export interface AccountabilityGlance {
  todayIsBeautifulDay: boolean;
  currentStreak: number;
  days: AccountabilityDayGlance[];
}

export async function fetchAccountabilityGlance(familyId: string): Promise<AccountabilityGlance> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/accountability-glance`);
  return {
    todayIsBeautifulDay: Boolean(data.todayIsBeautifulDay ?? data.TodayIsBeautifulDay ?? false),
    currentStreak: Number(data.currentStreak ?? data.CurrentStreak ?? 0),
    days: asArray(data.days ?? data.Days).map((d) => ({
      date: String(d.date ?? d.Date ?? ''),
      isScored: Boolean(d.isScored ?? d.IsScored ?? false),
      isBeautifulDay: Boolean(d.isBeautifulDay ?? d.IsBeautifulDay ?? false),
      childDone: Number(d.childDone ?? d.ChildDone ?? 0),
      childSkipped: Number(d.childSkipped ?? d.ChildSkipped ?? 0),
      childOpen: Number(d.childOpen ?? d.ChildOpen ?? 0),
      childLateDone: Number(d.childLateDone ?? d.ChildLateDone ?? 0),
    })),
  };
}

export interface CommitmentTemplateDto {
  id: string;
  title: string;
  memberId?: string;
  windowStart?: string;
  windowEnd?: string;
  sortOrder: number;
  isActive: boolean;
  starReward?: number;
}

export interface FamilyRoutineDto {
  id: string;
  code: string;
  displayName: string;
  kind: string;
  weekdays: number[];
  isActive: boolean;
  templates: CommitmentTemplateDto[];
}

function mapTemplate(t: Row): CommitmentTemplateDto {
  return {
    id: String(t.id ?? t.Id),
    title: String(t.title ?? t.Title ?? ''),
    memberId:
      t.memberId != null || t.MemberId != null ? String(t.memberId ?? t.MemberId) : undefined,
    windowStart:
      t.windowStart != null || t.WindowStart != null
        ? String(t.windowStart ?? t.WindowStart)
        : undefined,
    windowEnd:
      t.windowEnd != null || t.WindowEnd != null ? String(t.windowEnd ?? t.WindowEnd) : undefined,
    sortOrder: Number(t.sortOrder ?? t.SortOrder ?? 0),
    isActive: Boolean(t.isActive ?? t.IsActive ?? true),
    starReward: Number(t.starReward ?? t.StarReward ?? 0) || undefined,
  };
}

function mapRoutine(r: Row): FamilyRoutineDto {
  return {
    id: String(r.id ?? r.Id),
    code: String(r.code ?? r.Code ?? ''),
    displayName: String(r.displayName ?? r.DisplayName ?? ''),
    kind: String(r.kind ?? r.Kind ?? ''),
    weekdays: asArray(r.weekdays ?? r.Weekdays).map((d) => Number(d)),
    isActive: Boolean(r.isActive ?? r.IsActive ?? true),
    templates: asArray(r.templates ?? r.Templates).map((t) => mapTemplate(t as Row)),
  };
}

export async function fetchFamilyRoutines(familyId: string): Promise<FamilyRoutineDto[]> {
  const { data } = await http.get<unknown>(`/family-os/families/${familyId}/routines`);
  return asArray(data).map((r) => mapRoutine(r as Row));
}

export async function createFamilyRoutine(
  familyId: string,
  input: {
    code: string;
    displayName: string;
    kind?: string;
    weekdays?: number[];
    templates?: Array<{
      title: string;
      description?: string;
      memberId?: string;
      windowStart?: string;
      windowEnd?: string;
      sortOrder?: number;
      priority?: string;
    }>;
  },
): Promise<FamilyRoutineDto> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/routines`, {
    code: input.code,
    displayName: input.displayName,
    kind: input.kind ?? 'daily',
    weekdays: input.weekdays ?? [1, 2, 3, 4, 5, 6, 0],
    templates: (input.templates ?? []).map((t) => ({
      title: t.title,
      description: t.description ?? null,
      memberId: t.memberId ?? null,
      windowStart: t.windowStart ?? null,
      windowEnd: t.windowEnd ?? null,
      sortOrder: t.sortOrder ?? 0,
      priority: t.priority ?? 'normal',
    })),
  });
  return mapRoutine(data);
}

export async function addCommitmentTemplate(
  familyId: string,
  routineId: string,
  input: {
    title: string;
    description?: string;
    memberId?: string;
    windowStart?: string;
    windowEnd?: string;
    sortOrder?: number;
    priority?: string;
  },
): Promise<CommitmentTemplateDto> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/routines/${routineId}/templates`,
    {
      title: input.title,
      description: input.description ?? null,
      memberId: input.memberId ?? null,
      windowStart: input.windowStart ?? null,
      windowEnd: input.windowEnd ?? null,
      sortOrder: input.sortOrder ?? 0,
      priority: input.priority ?? 'normal',
    },
  );
  return mapTemplate(data);
}

export interface FamilyValueState {
  healthScores: Record<string, number>;
  nudgeCounts: Record<string, number>;
  onboarding: { payloadJson: string; completedAt: string } | null;
}

function mapIntMap(raw: unknown): Record<string, number> {
  if (!raw || typeof raw !== 'object') return {};
  const out: Record<string, number> = {};
  for (const [k, v] of Object.entries(raw as Record<string, unknown>)) {
    const n = Number(v);
    if (Number.isFinite(n)) out[k] = n;
  }
  return out;
}

export async function fetchFamilyValueState(
  familyId: string,
  from?: string,
  to?: string,
): Promise<FamilyValueState> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/value/state`, {
    params: { from, to },
  });
  const onboardRaw = (data.onboarding ?? data.Onboarding) as Row | null | undefined;
  return {
    healthScores: mapIntMap(data.healthScores ?? data.HealthScores),
    nudgeCounts: mapIntMap(data.nudgeCounts ?? data.NudgeCounts),
    onboarding:
      onboardRaw == null
        ? null
        : {
            payloadJson: String(onboardRaw.payloadJson ?? onboardRaw.PayloadJson ?? '{}'),
            completedAt: String(onboardRaw.completedAt ?? onboardRaw.CompletedAt ?? ''),
          },
  };
}

export async function putFamilyHealthScore(
  familyId: string,
  scoreDate: string,
  score: number,
  breakdownJson?: string,
): Promise<void> {
  await http.put(`/family-os/families/${familyId}/value/health-score`, {
    scoreDate,
    score,
    breakdownJson: breakdownJson ?? null,
  });
}

export async function incrementFamilyNudge(
  familyId: string,
  nudgeDate: string,
  increment = 1,
): Promise<number> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/value/nudges/increment`, {
    nudgeDate,
    increment,
  });
  return Number(data.count ?? data.Count ?? 0);
}

export async function putFamilyNudgeCount(
  familyId: string,
  nudgeDate: string,
  count: number,
): Promise<void> {
  await http.put(`/family-os/families/${familyId}/value/nudges`, {
    nudgeDate,
    count,
  });
}

export async function putFamilyOnboarding(
  familyId: string,
  payloadJson: string,
  completedAt?: string,
): Promise<void> {
  await http.put(`/family-os/families/${familyId}/value/onboarding`, {
    payloadJson,
    completedAt: completedAt ?? null,
  });
}

export async function deleteFamilyOnboarding(familyId: string): Promise<void> {
  await http.delete(`/family-os/families/${familyId}/value/onboarding`);
}

export interface TeamChildSlice {
  memberId: string;
  displayName: string;
  done: number;
  total: number;
  open: number;
  skipped: number;
}

export interface TeamDay {
  flowDate: string;
  dayFlowId?: string;
  teamDone: number;
  teamTotal: number;
  teamPercent: number;
  remainingMissions: number;
  teamComplete: boolean;
  heroMissionLine: string;
  children: TeamChildSlice[];
}

export interface TeamUnlock {
  id: string;
  familyId: string;
  dayFlowId: string;
  flowDate: string;
  rewardCode: string;
  labelVi: string;
  agreementId?: string;
  teamDone: number;
  teamTotal: number;
  teamPercent: number;
  status: 'pending_confirm' | 'confirmed' | 'deferred' | string;
  confirmedBy?: string;
  confirmedAt?: string;
  decisionNote?: string;
  createdAt: string;
}

function mapTeamChild(r: Row): TeamChildSlice {
  return {
    memberId: String(r.memberId ?? r.MemberId ?? ''),
    displayName: String(r.displayName ?? r.DisplayName ?? ''),
    done: Number(r.done ?? r.Done ?? 0),
    total: Number(r.total ?? r.Total ?? 0),
    open: Number(r.open ?? r.Open ?? 0),
    skipped: Number(r.skipped ?? r.Skipped ?? 0),
  };
}

export async function fetchTeamDay(familyId: string, flowDate?: string): Promise<TeamDay> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/team-day`, {
    params: { flowDate },
  });
  return {
    flowDate: String(data.flowDate ?? data.FlowDate ?? ''),
    dayFlowId:
      data.dayFlowId != null || data.DayFlowId != null
        ? String(data.dayFlowId ?? data.DayFlowId)
        : undefined,
    teamDone: Number(data.teamDone ?? data.TeamDone ?? 0),
    teamTotal: Number(data.teamTotal ?? data.TeamTotal ?? 0),
    teamPercent: Number(data.teamPercent ?? data.TeamPercent ?? 0),
    remainingMissions: Number(data.remainingMissions ?? data.RemainingMissions ?? 0),
    teamComplete: Boolean(data.teamComplete ?? data.TeamComplete ?? false),
    heroMissionLine: String(data.heroMissionLine ?? data.HeroMissionLine ?? ''),
    children: asArray(data.children ?? data.Children).map(mapTeamChild),
  };
}

function mapTeamUnlock(r: Row): TeamUnlock {
  return {
    id: String(r.id ?? r.Id ?? ''),
    familyId: String(r.familyId ?? r.FamilyId ?? ''),
    dayFlowId: String(r.dayFlowId ?? r.DayFlowId ?? ''),
    flowDate: String(r.flowDate ?? r.FlowDate ?? ''),
    rewardCode: String(r.rewardCode ?? r.RewardCode ?? ''),
    labelVi: String(r.labelVi ?? r.LabelVi ?? ''),
    agreementId:
      r.agreementId != null || r.AgreementId != null
        ? String(r.agreementId ?? r.AgreementId)
        : undefined,
    teamDone: Number(r.teamDone ?? r.TeamDone ?? 0),
    teamTotal: Number(r.teamTotal ?? r.TeamTotal ?? 0),
    teamPercent: Number(r.teamPercent ?? r.TeamPercent ?? 0),
    status: String(r.status ?? r.Status ?? ''),
    confirmedBy:
      r.confirmedBy != null || r.ConfirmedBy != null
        ? String(r.confirmedBy ?? r.ConfirmedBy)
        : undefined,
    confirmedAt:
      r.confirmedAt != null || r.ConfirmedAt != null
        ? String(r.confirmedAt ?? r.ConfirmedAt)
        : undefined,
    decisionNote:
      r.decisionNote != null || r.DecisionNote != null
        ? String(r.decisionNote ?? r.DecisionNote)
        : undefined,
    createdAt: String(r.createdAt ?? r.CreatedAt ?? ''),
  };
}

export async function fetchTeamUnlocks(
  familyId: string,
  flowDate?: string,
  ensure = false,
): Promise<TeamUnlock[]> {
  const { data } = await http.get<unknown>(`/family-os/families/${familyId}/team-unlocks`, {
    params: { flowDate, ensure },
  });
  return asArray(data).map(mapTeamUnlock);
}

export async function ensureTeamUnlock(
  familyId: string,
  flowDate?: string,
): Promise<TeamUnlock | null> {
  const res = await http.post<Row | ''>(
    `/family-os/families/${familyId}/team-unlocks/ensure`,
    null,
    { params: { flowDate }, validateStatus: (s) => s === 200 || s === 204 },
  );
  if (res.status === 204 || !res.data) return null;
  return mapTeamUnlock(res.data as Row);
}

export async function confirmTeamUnlock(
  familyId: string,
  unlockId: string,
  input: { confirmedBy: string; status?: 'confirmed' | 'deferred'; decisionNote?: string },
): Promise<TeamUnlock> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/team-unlocks/${unlockId}/confirm`,
    {
      status: input.status ?? 'confirmed',
      confirmedBy: input.confirmedBy,
      decisionNote: input.decisionNote ?? null,
    },
  );
  return mapTeamUnlock(data);
}

export interface ChildGratitude {
  id: string;
  familyId: string;
  fromMemberId: string;
  fromMemberName: string;
  toMemberId?: string;
  toMemberName?: string;
  flowDate: string;
  messageVi: string;
  praiseContext?: string;
  createdAt: string;
  readAt?: string;
  alreadySent: boolean;
}

function mapChildGratitude(r: Row): ChildGratitude {
  return {
    id: String(r.id ?? r.Id ?? ''),
    familyId: String(r.familyId ?? r.FamilyId ?? ''),
    fromMemberId: String(r.fromMemberId ?? r.FromMemberId ?? ''),
    fromMemberName: String(r.fromMemberName ?? r.FromMemberName ?? ''),
    toMemberId:
      r.toMemberId != null || r.ToMemberId != null
        ? String(r.toMemberId ?? r.ToMemberId)
        : undefined,
    toMemberName:
      r.toMemberName != null || r.ToMemberName != null
        ? String(r.toMemberName ?? r.ToMemberName)
        : undefined,
    flowDate: String(r.flowDate ?? r.FlowDate ?? ''),
    messageVi: String(r.messageVi ?? r.MessageVi ?? ''),
    praiseContext:
      r.praiseContext != null || r.PraiseContext != null
        ? String(r.praiseContext ?? r.PraiseContext)
        : undefined,
    createdAt: String(r.createdAt ?? r.CreatedAt ?? ''),
    readAt:
      r.readAt != null || r.ReadAt != null ? String(r.readAt ?? r.ReadAt) : undefined,
    alreadySent: Boolean(r.alreadySent ?? r.AlreadySent ?? false),
  };
}

export async function fetchChildGratitude(
  familyId: string,
  flowDate?: string,
  fromMemberId?: string,
): Promise<ChildGratitude[]> {
  const { data } = await http.get<unknown>(`/family-os/families/${familyId}/gratitude`, {
    params: { flowDate, fromMemberId },
  });
  return asArray(data).map((r) => mapChildGratitude(r as Row));
}

export async function sendChildGratitude(
  familyId: string,
  input: {
    fromMemberId: string;
    flowDate?: string;
    praiseContext?: string;
    messageVi?: string;
  },
): Promise<ChildGratitude> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/gratitude`, {
    fromMemberId: input.fromMemberId,
    flowDate: input.flowDate ?? null,
    praiseContext: input.praiseContext ?? null,
    messageVi: input.messageVi ?? null,
  });
  return mapChildGratitude(data);
}

export async function markChildGratitudeRead(
  familyId: string,
  gratitudeId: string,
): Promise<void> {
  await http.post(`/family-os/families/${familyId}/gratitude/${gratitudeId}/read`);
}

export interface RewardCatalogItem {
  id: string;
  title: string;
  icon: string;
  cost: number | null;
  tone?: string;
  isSpecial?: boolean;
  sortOrder?: number;
}

export interface RewardRedemption {
  id: string;
  catalogId: string;
  title: string;
  icon: string;
  starCost: number;
  status: string;
  createdAt: string;
  fulfilledAt?: string;
}

function mapRewardCatalogItem(r: Row): RewardCatalogItem {
  const costRaw = r.cost ?? r.Cost;
  return {
    id: String(r.id ?? r.Id),
    title: String(r.title ?? r.Title ?? ''),
    icon: String(r.icon ?? r.Icon ?? '🎁'),
    cost: costRaw == null ? null : Number(costRaw),
    tone:
      r.tone != null || r.Tone != null ? String(r.tone ?? r.Tone) : undefined,
    isSpecial: Boolean(r.isSpecial ?? r.IsSpecial ?? false),
    sortOrder: Number(r.sortOrder ?? r.SortOrder ?? 0),
  };
}

function mapRewardRedemption(r: Row): RewardRedemption {
  return {
    id: String(r.id ?? r.Id),
    catalogId: String(r.catalogId ?? r.CatalogId ?? ''),
    title: String(r.title ?? r.Title ?? ''),
    icon: String(r.icon ?? r.Icon ?? '🎁'),
    starCost: Number(r.starCost ?? r.StarCost ?? 0),
    status: String(r.status ?? r.Status ?? ''),
    createdAt: String(r.createdAt ?? r.CreatedAt ?? ''),
    fulfilledAt:
      r.fulfilledAt != null || r.FulfilledAt != null
        ? String(r.fulfilledAt ?? r.FulfilledAt)
        : undefined,
  };
}

export async function fetchRewardCatalog(familyId: string): Promise<RewardCatalogItem[]> {
  const { data } = await http.get<unknown>(`/family-os/families/${familyId}/reward-catalog`);
  return asArray(data).map((r) => mapRewardCatalogItem(r as Row));
}

export async function fetchRewardRedemptions(
  familyId: string,
  memberId?: string,
): Promise<RewardRedemption[]> {
  const path = memberId
    ? `/family-os/families/${familyId}/members/${memberId}/reward-redemptions`
    : `/family-os/families/${familyId}/reward-redemptions`;
  const { data } = await http.get<unknown>(path);
  return asArray(data).map((r) => mapRewardRedemption(r as Row));
}

export async function redeemReward(
  familyId: string,
  memberId: string,
  catalogId: string,
): Promise<{ balance: number; redemption: RewardRedemption }> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/members/${memberId}/reward-redeem`,
    { catalogId },
  );
  const redemptionRaw = (data.redemption ?? data.Redemption) as Row | undefined;
  return {
    balance: Number(data.balance ?? data.Balance ?? 0),
    redemption: mapRewardRedemption(redemptionRaw ?? {}),
  };
}

export async function fulfillRewardRedemption(
  familyId: string,
  redemptionId: string,
  fulfilledBy: string,
): Promise<RewardRedemption> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/reward-redemptions/${redemptionId}/fulfill`,
    { fulfilledBy },
  );
  return mapRewardRedemption(data);
}

export interface FamilyMemberMood {
  id: string;
  familyId: string;
  memberId: string;
  memberName: string;
  flowDate: string;
  moodCode: string;
  note?: string;
  createdAt: string;
  updatedAt: string;
}

function mapFamilyMemberMood(r: Row): FamilyMemberMood {
  return {
    id: String(r.id ?? r.Id ?? ''),
    familyId: String(r.familyId ?? r.FamilyId ?? ''),
    memberId: String(r.memberId ?? r.MemberId ?? ''),
    memberName: String(r.memberName ?? r.MemberName ?? ''),
    flowDate: String(r.flowDate ?? r.FlowDate ?? ''),
    moodCode: String(r.moodCode ?? r.MoodCode ?? ''),
    note:
      r.note != null || r.Note != null ? String(r.note ?? r.Note) : undefined,
    createdAt: String(r.createdAt ?? r.CreatedAt ?? ''),
    updatedAt: String(r.updatedAt ?? r.UpdatedAt ?? ''),
  };
}

export async function fetchFamilyMoods(
  familyId: string,
  flowDate: string,
): Promise<FamilyMemberMood[]> {
  const { data } = await http.get<unknown>(`/family-os/families/${familyId}/moods`, {
    params: { flowDate },
  });
  return asArray(data).map((r) => mapFamilyMemberMood(r as Row));
}

export async function fetchMemberMood(
  familyId: string,
  memberId: string,
  flowDate: string,
): Promise<FamilyMemberMood | null> {
  try {
    const { data } = await http.get<Row>(
      `/family-os/families/${familyId}/members/${memberId}/mood`,
      { params: { flowDate } },
    );
    return mapFamilyMemberMood(data);
  } catch (err: unknown) {
    const status = (err as { response?: { status?: number } })?.response?.status;
    if (status === 404) return null;
    throw err;
  }
}

export async function upsertMemberMood(
  familyId: string,
  memberId: string,
  input: { flowDate?: string; moodCode: string; note?: string },
): Promise<FamilyMemberMood> {
  const { data } = await http.put<Row>(
    `/family-os/families/${familyId}/members/${memberId}/mood`,
    {
      flowDate: input.flowDate ?? null,
      moodCode: input.moodCode,
      note: input.note ?? null,
    },
  );
  return mapFamilyMemberMood(data);
}
