import { http } from '@/shared/api/http';
import { normalizeLateStarLabelVi } from '@/shared/flow/late-duration';
import {
  deepMergeLayers,
  parseLayersJson,
} from '@/shared/value/soft-calibration';

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
  /** Behavior OS Wave 1 */
  habitStage?: string;
  habitStageLabelVi?: string;
  habitStreakDays?: number;
  reminderSuppressed?: boolean;
  needsReflection?: boolean;
  suggestedReflectionPrompt?: string;
  /** Behavior OS Wave 2 */
  evidenceLevel?: number;
  evidenceLevelLabelVi?: string;
  confidenceScore?: number;
  confidenceLabelVi?: string;
  needsRetrievalCheck?: boolean;
  isLearningMission?: boolean;
  /** Behavior OS Wave 3 */
  motivationDriver?: string;
  motivationCueVi?: string;
  interventionLevel?: string;
  interventionLabelVi?: string;
  allowParentPush?: boolean;
  allowChildChime?: boolean;
  parentAdviceVi?: string;
  eveningRiskBand?: string;
  eveningRiskLabelVi?: string;
  eveningRiskActionVi?: string;
  /** S1–S2 behavior pattern / tactic */
  behaviorPatternCode?: string;
  behaviorTacticCode?: string;
  /** Evidence P0 */
  commitmentKind?: 'chore' | 'study_focus' | 'relation' | string;
  evidencePolicy?: 'optional' | 'required_soft' | 'required_hard' | string;
  evidenceSatisfied?: boolean;
  evidenceSatisfiedAt?: string;
  evidenceSatisfiedBy?: string;
  evidenceGateLabelVi?: string;
  /** Evidence P0.5 */
  startedAt?: string;
  expectedDurationMinutes?: number;
  studyDurationMet?: boolean;
  studyMinDurationMinutes?: number;
  /** Photo submitted but not yet parent/retrieval-satisfied */
  evidenceSubmitted?: boolean;
  /** SCH-02 — server-derived school phase for commitment owner */
  schoolPhase?: string;
  schoolQuiet?: boolean;
}

export const REFLECTION_PROMPT_OPTIONS = [
  { value: 'hardest', label: 'Điều khó nhất hôm nay là gì?' },
  { value: 'learned', label: 'Con học được gì?' },
  { value: 'improve_tomorrow', label: 'Mai con muốn cải thiện điều gì?' },
] as const;

export type ReflectionPromptCode = (typeof REFLECTION_PROMPT_OPTIONS)[number]['value'];

export function reflectionPromptLabel(code?: string): string {
  return (
    REFLECTION_PROMPT_OPTIONS.find((o) => o.value === code)?.label ??
    'Con muốn chia sẻ gì?'
  );
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
  /** SCH-02 — phase/quiet per child */
  schoolMembers?: Array<{
    memberId: string;
    phase: string;
    quietNow: boolean;
    quietEnd: string;
  }>;
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

export interface AuthWorkspace {
  userId: string;
  tenantId: string;
  tenantCode: string;
  tenantName: string;
  productCode: string;
  username: string;
  isDefault: boolean;
}

export type FamilyLoginResult =
  | { kind: 'session'; accessToken: string; refreshToken: string | null; tenantCode: string }
  | { kind: 'choice'; selectionToken: string; workspaces: AuthWorkspace[] };

function mapWorkspaces(raw: unknown): AuthWorkspace[] {
  return asArray(raw).map((w) => ({
    userId: String(w.userId ?? w.UserId ?? ''),
    tenantId: String(w.tenantId ?? w.TenantId ?? ''),
    tenantCode: String(w.tenantCode ?? w.TenantCode ?? ''),
    tenantName: String(w.tenantName ?? w.TenantName ?? ''),
    productCode: String(w.productCode ?? w.ProductCode ?? ''),
    username: String(w.username ?? w.Username ?? ''),
    isDefault: Boolean(w.isDefault ?? w.IsDefault),
  }));
}

function mapLoginResponse(data: Row): FamilyLoginResult {
  if (data.requiresWorkspaceChoice === true || data.RequiresWorkspaceChoice === true) {
    return {
      kind: 'choice',
      selectionToken: String(data.selectionToken ?? data.SelectionToken ?? ''),
      workspaces: mapWorkspaces(data.workspaces ?? data.Workspaces),
    };
  }
  const accessToken = String(data.accessToken ?? data.AccessToken ?? '');
  if (!accessToken) throw new Error('Đăng nhập không trả về token');
  const refreshRaw = data.refreshToken ?? data.RefreshToken;
  const user = (data.user ?? data.User) as Row | undefined;
  return {
    kind: 'session',
    accessToken,
    refreshToken: refreshRaw != null ? String(refreshRaw) : null,
    tenantCode: String(user?.tenantCode ?? user?.TenantCode ?? ''),
  };
}

/** Legacy: tenant code + username */
export async function loginFamilyParent(input: {
  tenantCode: string;
  username: string;
  password: string;
}): Promise<FamilyLoginResult> {
  const { data } = await http.post<Row>('/auth/login', {
    tenantCode: input.tenantCode.trim().toUpperCase(),
    username: input.username.trim(),
    password: input.password,
  });
  return mapLoginResponse(data);
}

/** Kit email login — no tenant code; may return workspace choice */
export async function loginFamilyByEmail(input: {
  email: string;
  password: string;
}): Promise<FamilyLoginResult> {
  const { data } = await http.post<Row>('/auth/login', {
    username: input.email.trim().toLowerCase(),
    password: input.password,
  });
  return mapLoginResponse(data);
}

export async function selectFamilyWorkspace(input: {
  selectionToken: string;
  userId: string;
}): Promise<{ accessToken: string; refreshToken: string | null; tenantCode: string }> {
  const { data } = await http.post<Row>('/auth/select-workspace', {
    selectionToken: input.selectionToken,
    userId: input.userId,
  });
  const mapped = mapLoginResponse(data);
  if (mapped.kind !== 'session') throw new Error('Không chọn được workspace');
  return mapped;
}


function mapAuthSession(session: Row): { accessToken: string; refreshToken: string | null; tenantCode: string } {
  const accessToken = String(session.accessToken ?? session.AccessToken ?? '');
  if (!accessToken) throw new Error('Phiên đăng nhập không trả về token');
  const refreshRaw = session.refreshToken ?? session.RefreshToken;
  return {
    accessToken,
    refreshToken: refreshRaw != null ? String(refreshRaw) : null,
    tenantCode: String(session.tenantCode ?? session.TenantCode ?? ''),
  };
}

export async function registerFamily(input: {
  familyName: string;
  parentDisplayName: string;
  username: string;
  email: string;
  password: string;
  parentPin: string;
  child1Name?: string;
  child2Name?: string;
}): Promise<{
  tenantCode: string;
  familyId: string;
  familyName: string;
  accessToken: string;
  refreshToken: string | null;
}> {
  const { data } = await http.post<Row>('/family-os/register', {
    familyName: input.familyName.trim(),
    parentDisplayName: input.parentDisplayName.trim(),
    username: input.username.trim(),
    email: input.email.trim(),
    password: input.password,
    parentPin: input.parentPin,
    child1Name: input.child1Name?.trim() || null,
    child2Name: input.child2Name?.trim() || null,
  });
  const session = mapAuthSession((data.session ?? data.Session ?? {}) as Row);
  return {
    tenantCode: String(data.tenantCode ?? data.TenantCode ?? session.tenantCode),
    familyId: String(data.familyId ?? data.FamilyId ?? ''),
    familyName: String(data.familyName ?? data.FamilyName ?? ''),
    accessToken: session.accessToken,
    refreshToken: session.refreshToken,
  };
}

export async function acceptFamilyInvite(input: {
  code: string;
  parentDisplayName: string;
  username: string;
  email: string;
  password: string;
  parentPin?: string;
}): Promise<{
  tenantCode: string;
  familyId: string;
  familyName: string;
  accessToken: string;
  refreshToken: string | null;
}> {
  const { data } = await http.post<Row>('/family-os/invites/accept', {
    code: input.code.trim().toUpperCase(),
    parentDisplayName: input.parentDisplayName.trim(),
    username: input.username.trim(),
    email: input.email.trim(),
    password: input.password,
    parentPin: input.parentPin || null,
  });
  const session = mapAuthSession((data.session ?? data.Session ?? {}) as Row);
  return {
    tenantCode: String(data.tenantCode ?? data.TenantCode ?? session.tenantCode),
    familyId: String(data.familyId ?? data.FamilyId ?? ''),
    familyName: String(data.familyName ?? data.FamilyName ?? ''),
    accessToken: session.accessToken,
    refreshToken: session.refreshToken,
  };
}

export interface FamilyInvite {
  id: string;
  code: string;
  roleCode: string;
  expiresAt: string;
  maxUses: number;
  usedCount: number;
}

export async function createFamilyInvite(
  familyId: string,
  input?: { roleCode?: string; maxUses?: number; validDays?: number },
): Promise<FamilyInvite> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/invites`, {
    roleCode: input?.roleCode ?? null,
    maxUses: input?.maxUses ?? null,
    validDays: input?.validDays ?? null,
  });
  return {
    id: String(data.id ?? data.Id ?? ''),
    code: String(data.code ?? data.Code ?? '').toUpperCase(),
    roleCode: String(data.roleCode ?? data.RoleCode ?? 'guardian'),
    expiresAt: String(data.expiresAt ?? data.ExpiresAt ?? ''),
    maxUses: Number(data.maxUses ?? data.MaxUses ?? 0),
    usedCount: Number(data.usedCount ?? data.UsedCount ?? 0),
  };
}

/** Soft share text for Zalo / Messenger / SMS via system share sheet. */
export function formatFamilyInviteShare(input: {
  code: string;
  familyName?: string | null;
  expiresAt?: string;
}): string {
  const house = (input.familyName || 'gia đình mình').trim();
  const code = input.code.trim().toUpperCase();
  let expiry = '';
  if (input.expiresAt) {
    const t = Date.parse(input.expiresAt);
    if (!Number.isNaN(t)) {
      const d = new Date(t);
      const dd = String(d.getDate()).padStart(2, '0');
      const mm = String(d.getMonth() + 1).padStart(2, '0');
      expiry = ` (hết hạn ${dd}/${mm})`;
    }
  }
  return [
    `Mời vào nhà «${house}» trên Famixa.`,
    `Mở app → Tham gia bằng mã → nhập: ${code}${expiry}.`,
    'Bố/mẹ tạo tài khoản một lần, sau đó cả nhà chạm tên để dùng.',
  ].join('\n');
}

export interface FamilySubscription {
  familyId: string;
  planCode: string;
  status: string;
  trialEndsAt?: string;
  currentPeriodEnd?: string;
  isEntitled: boolean;
  trialDaysRemaining?: number;
  trialDaysTotal?: number;
  /** Days left in post-trial soft grace (Pro still entitled). */
  trialGraceDaysRemaining?: number;
  tierCode?: string;
  displayNameVi?: string;
  outcomeNameVi?: string;
  maxChildren?: number | null;
  capabilities?: string[];
  recommendedUpgradePlanCode?: string;
  upgradeHintVi?: string;
}

export interface FamilyCheckout {
  id: string;
  familyId: string;
  orderCode: number;
  planCode: string;
  amountVnd: number;
  status: string;
  checkoutUrl?: string;
  qrCode?: string;
  description?: string;
  paidAt?: string;
  createdAt: string;
  expiresAt?: string;
}

function mapSubscription(r: Row): FamilySubscription {
  const remainingRaw = r.trialDaysRemaining ?? r.TrialDaysRemaining;
  const totalRaw = r.trialDaysTotal ?? r.TrialDaysTotal;
  const graceRaw = r.trialGraceDaysRemaining ?? r.TrialGraceDaysRemaining;
  const maxChildrenRaw = r.maxChildren ?? r.MaxChildren;
  return {
    familyId: String(r.familyId ?? r.FamilyId ?? ''),
    planCode: String(r.planCode ?? r.PlanCode ?? ''),
    status: String(r.status ?? r.Status ?? ''),
    trialEndsAt:
      r.trialEndsAt != null || r.TrialEndsAt != null
        ? String(r.trialEndsAt ?? r.TrialEndsAt)
        : undefined,
    currentPeriodEnd:
      r.currentPeriodEnd != null || r.CurrentPeriodEnd != null
        ? String(r.currentPeriodEnd ?? r.CurrentPeriodEnd)
        : undefined,
    isEntitled: Boolean(r.isEntitled ?? r.IsEntitled ?? false),
    trialDaysRemaining:
      remainingRaw != null && remainingRaw !== ''
        ? Number(remainingRaw)
        : undefined,
    trialDaysTotal:
      totalRaw != null && totalRaw !== '' ? Number(totalRaw) : undefined,
    trialGraceDaysRemaining:
      graceRaw != null && graceRaw !== '' ? Number(graceRaw) : undefined,
    tierCode:
      r.tierCode != null || r.TierCode != null
        ? String(r.tierCode ?? r.TierCode)
        : undefined,
    displayNameVi:
      r.displayNameVi != null || r.DisplayNameVi != null
        ? String(r.displayNameVi ?? r.DisplayNameVi)
        : undefined,
    outcomeNameVi:
      r.outcomeNameVi != null || r.OutcomeNameVi != null
        ? String(r.outcomeNameVi ?? r.OutcomeNameVi)
        : undefined,
    maxChildren:
      maxChildrenRaw === null
        ? null
        : maxChildrenRaw != null && maxChildrenRaw !== ''
          ? Number(maxChildrenRaw)
          : undefined,
    capabilities: asArray(r.capabilities ?? r.Capabilities).map((x) => String(x)),
    recommendedUpgradePlanCode:
      r.recommendedUpgradePlanCode != null || r.RecommendedUpgradePlanCode != null
        ? String(r.recommendedUpgradePlanCode ?? r.RecommendedUpgradePlanCode)
        : undefined,
    upgradeHintVi:
      r.upgradeHintVi != null || r.UpgradeHintVi != null
        ? String(r.upgradeHintVi ?? r.UpgradeHintVi)
        : undefined,
  };
}

function mapCheckout(r: Row): FamilyCheckout {
  return {
    id: String(r.id ?? r.Id ?? ''),
    familyId: String(r.familyId ?? r.FamilyId ?? ''),
    orderCode: Number(r.orderCode ?? r.OrderCode ?? 0),
    planCode: String(r.planCode ?? r.PlanCode ?? ''),
    amountVnd: Number(r.amountVnd ?? r.AmountVnd ?? 0),
    status: String(r.status ?? r.Status ?? ''),
    checkoutUrl:
      r.checkoutUrl != null || r.CheckoutUrl != null
        ? String(r.checkoutUrl ?? r.CheckoutUrl)
        : undefined,
    qrCode:
      r.qrCode != null || r.QrCode != null ? String(r.qrCode ?? r.QrCode) : undefined,
    description:
      r.description != null || r.Description != null
        ? String(r.description ?? r.Description)
        : undefined,
    paidAt:
      r.paidAt != null || r.PaidAt != null ? String(r.paidAt ?? r.PaidAt) : undefined,
    createdAt: String(r.createdAt ?? r.CreatedAt ?? ''),
    expiresAt:
      r.expiresAt != null || r.ExpiresAt != null
        ? String(r.expiresAt ?? r.ExpiresAt)
        : undefined,
  };
}

export async function fetchFamilySubscription(familyId: string): Promise<FamilySubscription> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/subscription`);
  return mapSubscription(data);
}

export async function createFamilyCheckout(
  familyId: string,
  input?: { planCode?: string; returnUrl?: string; cancelUrl?: string },
): Promise<FamilyCheckout> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/billing/checkout`, {
    planCode: input?.planCode ?? null,
    returnUrl: input?.returnUrl ?? null,
    cancelUrl: input?.cancelUrl ?? null,
  });
  return mapCheckout(data);
}

export async function getFamilyCheckout(
  familyId: string,
  orderCode: number,
): Promise<FamilyCheckout> {
  const { data } = await http.get<Row>(
    `/family-os/families/${familyId}/billing/checkout/${orderCode}`,
  );
  return mapCheckout(data);
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
    habitStage:
      c.habitStage != null || c.HabitStage != null
        ? String(c.habitStage ?? c.HabitStage)
        : undefined,
    habitStageLabelVi:
      c.habitStageLabelVi != null || c.HabitStageLabelVi != null
        ? String(c.habitStageLabelVi ?? c.HabitStageLabelVi)
        : undefined,
    habitStreakDays: Number(c.habitStreakDays ?? c.HabitStreakDays ?? 0) || 0,
    reminderSuppressed: Boolean(c.reminderSuppressed ?? c.ReminderSuppressed ?? false),
    needsReflection: Boolean(c.needsReflection ?? c.NeedsReflection ?? false),
    suggestedReflectionPrompt:
      c.suggestedReflectionPrompt != null || c.SuggestedReflectionPrompt != null
        ? String(c.suggestedReflectionPrompt ?? c.SuggestedReflectionPrompt)
        : undefined,
    evidenceLevel: Number(c.evidenceLevel ?? c.EvidenceLevel ?? 0) || 0,
    evidenceLevelLabelVi:
      c.evidenceLevelLabelVi != null || c.EvidenceLevelLabelVi != null
        ? String(c.evidenceLevelLabelVi ?? c.EvidenceLevelLabelVi)
        : undefined,
    confidenceScore:
      c.confidenceScore != null || c.ConfidenceScore != null
        ? Number(c.confidenceScore ?? c.ConfidenceScore)
        : undefined,
    confidenceLabelVi:
      c.confidenceLabelVi != null || c.ConfidenceLabelVi != null
        ? String(c.confidenceLabelVi ?? c.ConfidenceLabelVi)
        : undefined,
    needsRetrievalCheck: Boolean(c.needsRetrievalCheck ?? c.NeedsRetrievalCheck ?? false),
    isLearningMission: Boolean(c.isLearningMission ?? c.IsLearningMission ?? false),
    motivationDriver:
      c.motivationDriver != null || c.MotivationDriver != null
        ? String(c.motivationDriver ?? c.MotivationDriver)
        : undefined,
    motivationCueVi:
      c.motivationCueVi != null || c.MotivationCueVi != null
        ? String(c.motivationCueVi ?? c.MotivationCueVi)
        : undefined,
    interventionLevel:
      c.interventionLevel != null || c.InterventionLevel != null
        ? String(c.interventionLevel ?? c.InterventionLevel)
        : undefined,
    interventionLabelVi:
      c.interventionLabelVi != null || c.InterventionLabelVi != null
        ? String(c.interventionLabelVi ?? c.InterventionLabelVi)
        : undefined,
    allowParentPush: Boolean(c.allowParentPush ?? c.AllowParentPush ?? false),
    allowChildChime: Boolean(c.allowChildChime ?? c.AllowChildChime ?? false),
    parentAdviceVi:
      c.parentAdviceVi != null || c.ParentAdviceVi != null
        ? String(c.parentAdviceVi ?? c.ParentAdviceVi)
        : undefined,
    eveningRiskBand:
      c.eveningRiskBand != null || c.EveningRiskBand != null
        ? String(c.eveningRiskBand ?? c.EveningRiskBand)
        : undefined,
    eveningRiskLabelVi:
      c.eveningRiskLabelVi != null || c.EveningRiskLabelVi != null
        ? String(c.eveningRiskLabelVi ?? c.EveningRiskLabelVi)
        : undefined,
    eveningRiskActionVi:
      c.eveningRiskActionVi != null || c.EveningRiskActionVi != null
        ? String(c.eveningRiskActionVi ?? c.EveningRiskActionVi)
        : undefined,
    behaviorPatternCode:
      c.behaviorPatternCode != null || c.BehaviorPatternCode != null
        ? String(c.behaviorPatternCode ?? c.BehaviorPatternCode)
        : undefined,
    behaviorTacticCode:
      c.behaviorTacticCode != null || c.BehaviorTacticCode != null
        ? String(c.behaviorTacticCode ?? c.BehaviorTacticCode)
        : undefined,
    commitmentKind: String(
      c.commitmentKind ?? c.CommitmentKind ?? 'chore',
    ).toLowerCase(),
    evidencePolicy: String(
      c.evidencePolicy ?? c.EvidencePolicy ?? 'optional',
    ).toLowerCase(),
    evidenceSatisfied: (() => {
      const kind = String(
        c.commitmentKind ?? c.CommitmentKind ?? 'chore',
      ).toLowerCase();
      const raw = c.evidenceSatisfied ?? c.EvidenceSatisfied;
      if (raw != null) return Boolean(raw);
      // Missing field: study_focus must not default to satisfied.
      return kind !== 'study_focus';
    })(),
    evidenceSatisfiedAt:
      c.evidenceSatisfiedAt != null || c.EvidenceSatisfiedAt != null
        ? String(c.evidenceSatisfiedAt ?? c.EvidenceSatisfiedAt)
        : undefined,
    evidenceSatisfiedBy:
      c.evidenceSatisfiedBy != null || c.EvidenceSatisfiedBy != null
        ? String(c.evidenceSatisfiedBy ?? c.EvidenceSatisfiedBy)
        : undefined,
    evidenceGateLabelVi:
      c.evidenceGateLabelVi != null || c.EvidenceGateLabelVi != null
        ? String(c.evidenceGateLabelVi ?? c.EvidenceGateLabelVi)
        : undefined,
    startedAt:
      c.startedAt != null || c.StartedAt != null
        ? String(c.startedAt ?? c.StartedAt)
        : undefined,
    expectedDurationMinutes: (() => {
      const raw = c.expectedDurationMinutes ?? c.ExpectedDurationMinutes;
      return raw != null && raw !== '' ? Number(raw) : undefined;
    })(),
    studyDurationMet: (() => {
      const raw = c.studyDurationMet ?? c.StudyDurationMet;
      return raw == null ? true : Boolean(raw);
    })(),
    studyMinDurationMinutes: (() => {
      const raw = c.studyMinDurationMinutes ?? c.StudyMinDurationMinutes;
      return raw != null && raw !== '' ? Number(raw) : undefined;
    })(),
    evidenceSubmitted: (() => {
      const raw = c.evidenceSubmitted ?? c.EvidenceSubmitted;
      return raw == null ? false : Boolean(raw);
    })(),
    schoolPhase:
      c.schoolPhase != null || c.SchoolPhase != null
        ? String(c.schoolPhase ?? c.SchoolPhase)
        : undefined,
    schoolQuiet: Boolean(c.schoolQuiet ?? c.SchoolQuiet ?? false),
  };
}

export interface CommitmentProgressResult {
  commitment: DayFlowCommitment;
  memberStarBalance?: number;
}

function mapDayFlow(data: Row): DayFlow {
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
    schoolMembers: asArray(data.schoolMembers ?? data.SchoolMembers).map((r) => ({
      memberId: String(r.memberId ?? r.MemberId ?? ''),
      phase: String(r.phase ?? r.Phase ?? ''),
      quietNow: Boolean(r.quietNow ?? r.QuietNow ?? false),
      quietEnd: String(r.quietEnd ?? r.QuietEnd ?? ''),
    })).filter((m) => m.memberId),
  };
}

export async function ensureDayFlow(
  familyId: string,
  forceRebuild = false,
  flowDate?: string,
): Promise<DayFlow> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/day-flows/ensure`, {
    forceRebuild,
    flowDate: flowDate ?? null,
  });
  return mapDayFlow(data);
}

/** GET existing day flow for a date; null if never materialized. */
export async function fetchDayFlow(
  familyId: string,
  flowDate: string,
): Promise<DayFlow | null> {
  const res = await http.get<Row>(`/family-os/families/${familyId}/day-flows/${flowDate}`, {
    validateStatus: (s) => s === 200 || s === 404,
  });
  if (res.status === 404 || !res.data) return null;
  return mapDayFlow(res.data as Row);
}

export type EvidenceUploadResult = {
  url: string;
  looksLikeStudy?: boolean;
  warningCodes?: string[];
  warningMessageVi?: string;
};


export type MomentMediaUploadResult = {
  url: string;
  mediaKind: 'photo' | 'audio';
};

export async function uploadFamilyMomentMedia(
  familyId: string,
  file: File,
  memberId?: string,
): Promise<MomentMediaUploadResult> {
  const form = new FormData();
  form.append('file', file);
  if (memberId) form.append('memberId', memberId);
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/moment-media`, form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  const url = String(data.url ?? data.Url ?? '');
  if (!url) throw new Error('Upload khong tra ve URL');
  const rawKind = String(data.mediaKind ?? data.MediaKind ?? 'photo').toLowerCase();
  return {
    url,
    mediaKind: rawKind === 'audio' ? 'audio' : 'photo',
  };
}

export async function uploadCommitmentEvidence(
  familyId: string,
  file: File,
  memberId?: string,
): Promise<EvidenceUploadResult> {
  const form = new FormData();
  form.append('file', file);
  if (memberId) form.append('memberId', memberId);
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/evidence`, form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  const url = String(data.url ?? data.Url ?? '');
  if (!url) throw new Error('Upload khong tra ve URL');
  const rawWarn = data.warningCodes ?? data.WarningCodes;
  const warningCodes = Array.isArray(rawWarn)
    ? (rawWarn as unknown[]).map(String)
    : undefined;
  return {
    url,
    looksLikeStudy:
      data.looksLikeStudy != null || data.LooksLikeStudy != null
        ? Boolean(data.looksLikeStudy ?? data.LooksLikeStudy)
        : true,
    warningCodes,
    warningMessageVi:
      data.warningMessageVi != null || data.WarningMessageVi != null
        ? String(data.warningMessageVi ?? data.WarningMessageVi)
        : undefined,
  };
}

export type MorningNote = {
  flowDate: string;
  memberId?: string;
  bodyVi: string;
  tone: string;
  focusTitles: string[];
  ageYears: number;
  ageBand: string;
  parentNudgesLast7Days: number;
  recentStudyDoneRate: number;
  isTemplate: boolean;
};

export async function getMorningNote(
  familyId: string,
  memberId?: string,
  date?: string,
): Promise<MorningNote> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/morning-note`, {
    params: {
      memberId: memberId || undefined,
      date: date || undefined,
    },
  });
  const titles = data.focusTitles ?? data.FocusTitles ?? [];
  return {
    flowDate: String(data.flowDate ?? data.FlowDate ?? ''),
    memberId:
      data.memberId != null || data.MemberId != null
        ? String(data.memberId ?? data.MemberId)
        : undefined,
    bodyVi: String(data.bodyVi ?? data.BodyVi ?? ''),
    tone: String(data.tone ?? data.Tone ?? 'encourage'),
    focusTitles: Array.isArray(titles) ? titles.map(String) : [],
    ageYears: Number(data.ageYears ?? data.AgeYears ?? 0),
    ageBand: String(data.ageBand ?? data.AgeBand ?? 'unknown'),
    parentNudgesLast7Days: Number(data.parentNudgesLast7Days ?? data.ParentNudgesLast7Days ?? 0),
    recentStudyDoneRate: Number(data.recentStudyDoneRate ?? data.RecentStudyDoneRate ?? 0.5),
    isTemplate: Boolean(data.isTemplate ?? data.IsTemplate ?? true),
  };
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

export async function submitCommitmentReflection(
  familyId: string,
  commitmentId: string,
  promptCode: ReflectionPromptCode,
  answerText: string,
): Promise<{
  id: string;
  promptCode: string;
  answerText: string;
  confidenceScore?: number;
  confidenceLabelVi?: string;
  evidenceLevel?: number;
  needsRetrievalCheck?: boolean;
}> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/commitments/${commitmentId}/reflection`,
    { promptCode, answerText },
  );
  return {
    id: String(data.id ?? data.Id ?? ''),
    promptCode: String(data.promptCode ?? data.PromptCode ?? promptCode),
    answerText: String(data.answerText ?? data.AnswerText ?? answerText),
    confidenceScore:
      data.confidenceScore != null || data.ConfidenceScore != null
        ? Number(data.confidenceScore ?? data.ConfidenceScore)
        : undefined,
    confidenceLabelVi:
      data.confidenceLabelVi != null || data.ConfidenceLabelVi != null
        ? String(data.confidenceLabelVi ?? data.ConfidenceLabelVi)
        : undefined,
    evidenceLevel:
      data.evidenceLevel != null || data.EvidenceLevel != null
        ? Number(data.evidenceLevel ?? data.EvidenceLevel)
        : undefined,
    needsRetrievalCheck: Boolean(data.needsRetrievalCheck ?? data.NeedsRetrievalCheck ?? false),
  };
}

export type RetrievalMethodAnswer = 'skim' | 'practice' | 'retrieve';
export type RetrievalRecallAnswer = 'can_explain' | 'vaguely' | 'need_review';

export interface RetrievalCheckResult {
  commitmentId: string;
  methodAnswer: string;
  recallAnswer: string;
  illusionRisk: boolean;
  confidenceScore: number;
  confidenceLabelVi: string;
  evidenceLevel: number;
  evidenceLevelLabelVi?: string;
}

export async function submitRetrievalCheck(
  familyId: string,
  commitmentId: string,
  methodAnswer: RetrievalMethodAnswer,
  recallAnswer: RetrievalRecallAnswer,
): Promise<RetrievalCheckResult> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/commitments/${commitmentId}/retrieval-check`,
    { methodAnswer, recallAnswer },
  );
  return {
    commitmentId: String(data.commitmentId ?? data.CommitmentId ?? commitmentId),
    methodAnswer: String(data.methodAnswer ?? data.MethodAnswer ?? methodAnswer),
    recallAnswer: String(data.recallAnswer ?? data.RecallAnswer ?? recallAnswer),
    illusionRisk: Boolean(data.illusionRisk ?? data.IllusionRisk ?? false),
    confidenceScore: Number(data.confidenceScore ?? data.ConfidenceScore ?? 0),
    confidenceLabelVi: String(
      data.confidenceLabelVi ?? data.ConfidenceLabelVi ?? 'Cần thêm dữ liệu',
    ),
    evidenceLevel: Number(data.evidenceLevel ?? data.EvidenceLevel ?? 0),
    evidenceLevelLabelVi:
      data.evidenceLevelLabelVi != null || data.EvidenceLevelLabelVi != null
        ? String(data.evidenceLevelLabelVi ?? data.EvidenceLevelLabelVi)
        : undefined,
  };
}

export async function selfStartCommitment(
  familyId: string,
  commitmentId: string,
): Promise<CommitmentProgressResult> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/commitments/${commitmentId}/self-start`,
  );
  const commitment = mapCommitment(data);
  return {
    commitment,
    memberStarBalance: commitment.memberStarBalance,
  };
}

export interface BehaviorCoachHint {
  memberId?: string;
  memberName?: string;
  commitmentId: string;
  title: string;
  interventionLevel: string;
  interventionLabelVi: string;
  parentAdviceVi: string;
  allowParentPush: boolean;
  motivationCueVi?: string;
  behaviorPatternCode?: string;
  behaviorTacticCode?: string;
}

export interface BehaviorCoach {
  flowDate: string;
  parentNudgesUsedToday: number;
  parentNudgeBudget: number;
  observeOnlyCount: number;
  allowParentPushCount: number;
  hints: BehaviorCoachHint[];
}

export async function fetchBehaviorCoach(
  familyId: string,
  flowDate?: string,
): Promise<BehaviorCoach> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/behavior/coach`, {
    params: flowDate ? { flowDate } : undefined,
  });
  const hintsRaw = asArray(data.hints ?? data.Hints);
  return {
    flowDate: String(data.flowDate ?? data.FlowDate ?? ''),
    parentNudgesUsedToday: Number(data.parentNudgesUsedToday ?? data.ParentNudgesUsedToday ?? 0),
    parentNudgeBudget: Number(data.parentNudgeBudget ?? data.ParentNudgeBudget ?? 3),
    observeOnlyCount: Number(data.observeOnlyCount ?? data.ObserveOnlyCount ?? 0),
    allowParentPushCount: Number(data.allowParentPushCount ?? data.AllowParentPushCount ?? 0),
    hints: hintsRaw.map((h) => ({
      memberId:
        h.memberId != null || h.MemberId != null ? String(h.memberId ?? h.MemberId) : undefined,
      memberName:
        h.memberName != null || h.MemberName != null
          ? String(h.memberName ?? h.MemberName)
          : undefined,
      commitmentId: String(h.commitmentId ?? h.CommitmentId ?? ''),
      title: String(h.title ?? h.Title ?? ''),
      interventionLevel: String(h.interventionLevel ?? h.InterventionLevel ?? ''),
      interventionLabelVi: String(h.interventionLabelVi ?? h.InterventionLabelVi ?? ''),
      parentAdviceVi: String(h.parentAdviceVi ?? h.ParentAdviceVi ?? ''),
      allowParentPush: Boolean(h.allowParentPush ?? h.AllowParentPush ?? false),
      motivationCueVi:
        h.motivationCueVi != null || h.MotivationCueVi != null
          ? String(h.motivationCueVi ?? h.MotivationCueVi)
          : undefined,
      behaviorPatternCode:
        h.behaviorPatternCode != null || h.BehaviorPatternCode != null
          ? String(h.behaviorPatternCode ?? h.BehaviorPatternCode)
          : undefined,
      behaviorTacticCode:
        h.behaviorTacticCode != null || h.BehaviorTacticCode != null
          ? String(h.behaviorTacticCode ?? h.BehaviorTacticCode)
          : undefined,
    })),
  };
}

export interface BehaviorTwinDimension {
  code: string;
  labelVi: string;
  score: number;
  whyVi: string;
}

export interface BehaviorTwinMember {
  memberId: string;
  memberName: string;
  overallScore: number;
  overallLabelVi: string;
  disclaimerVi: string;
  eveningRiskBand?: string;
  eveningRiskLabelVi?: string;
  eveningReasonsVi: string[];
  eveningSuggestedActionVi?: string;
  dimensions: BehaviorTwinDimension[];
  snapshotDate: string;
}

export interface BehaviorTwin {
  asOfDate: string;
  disclaimerVi: string;
  members: BehaviorTwinMember[];
}

export async function fetchBehaviorTwin(
  familyId: string,
  memberId?: string,
): Promise<BehaviorTwin> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/behavior/twin`, {
    params: memberId ? { memberId } : undefined,
  });
  const membersRaw = asArray(data.members ?? data.Members);
  return {
    asOfDate: String(data.asOfDate ?? data.AsOfDate ?? ''),
    disclaimerVi: String(
      data.disclaimerVi ??
        data.DisclaimerVi ??
        'Mô hình tín hiệu — không đánh giá tính cách.',
    ),
    members: membersRaw.map((m) => {
      const dims = asArray(m.dimensions ?? m.Dimensions);
      const reasons = m.eveningReasonsVi ?? m.EveningReasonsVi;
      return {
        memberId: String(m.memberId ?? m.MemberId ?? ''),
        memberName: String(m.memberName ?? m.MemberName ?? ''),
        overallScore: Number(m.overallScore ?? m.OverallScore ?? 0),
        overallLabelVi: String(m.overallLabelVi ?? m.OverallLabelVi ?? ''),
        disclaimerVi: String(m.disclaimerVi ?? m.DisclaimerVi ?? ''),
        eveningRiskBand:
          m.eveningRiskBand != null || m.EveningRiskBand != null
            ? String(m.eveningRiskBand ?? m.EveningRiskBand)
            : undefined,
        eveningRiskLabelVi:
          m.eveningRiskLabelVi != null || m.EveningRiskLabelVi != null
            ? String(m.eveningRiskLabelVi ?? m.EveningRiskLabelVi)
            : undefined,
        eveningReasonsVi: Array.isArray(reasons) ? reasons.map((x) => String(x)) : [],
        eveningSuggestedActionVi:
          m.eveningSuggestedActionVi != null || m.EveningSuggestedActionVi != null
            ? String(m.eveningSuggestedActionVi ?? m.EveningSuggestedActionVi)
            : undefined,
        dimensions: dims.map((d) => ({
          code: String(d.code ?? d.Code ?? ''),
          labelVi: String(d.labelVi ?? d.LabelVi ?? ''),
          score: Number(d.score ?? d.Score ?? 0),
          whyVi: String(d.whyVi ?? d.WhyVi ?? ''),
        })),
        snapshotDate: String(m.snapshotDate ?? m.SnapshotDate ?? ''),
      };
    }),
  };
}

export interface BehaviorRetirementPolicy {
  observeOnly: boolean;
  retirementStage?: string;
  retirementLabelVi?: string;
  parentNudgeBudget?: number;
  notesVi?: string;
  updatedAt?: string;
}

export interface FamilyBehaviorTwin {
  asOfDate: string;
  disclaimerVi: string;
  familyPeaceIndex: number;
  familyAutonomyIndex: number;
  parentalInterventionIndex: number;
  retirementStage: string;
  retirementLabelVi: string;
  retirementAdviceVi: string;
  siblingBalance: string;
  siblingBalanceLabelVi: string;
  siblingAdviceVi: string;
  dependenceWarning: boolean;
  dependenceWarningVi?: string;
  recommendObserveOnly: boolean;
  observeOnlyActive: boolean;
  policy: BehaviorRetirementPolicy;
  children: BehaviorTwinMember[];
}

function mapRetirementPolicy(p: Row | undefined | null): BehaviorRetirementPolicy {
  if (!p) return { observeOnly: false };
  return {
    observeOnly: Boolean(p.observeOnly ?? p.ObserveOnly ?? false),
    retirementStage:
      p.retirementStage != null || p.RetirementStage != null
        ? String(p.retirementStage ?? p.RetirementStage)
        : undefined,
    retirementLabelVi:
      p.retirementLabelVi != null || p.RetirementLabelVi != null
        ? String(p.retirementLabelVi ?? p.RetirementLabelVi)
        : undefined,
    parentNudgeBudget:
      p.parentNudgeBudget != null || p.ParentNudgeBudget != null
        ? Number(p.parentNudgeBudget ?? p.ParentNudgeBudget)
        : undefined,
    notesVi:
      p.notesVi != null || p.NotesVi != null ? String(p.notesVi ?? p.NotesVi) : undefined,
    updatedAt:
      p.updatedAt != null || p.UpdatedAt != null
        ? String(p.updatedAt ?? p.UpdatedAt)
        : undefined,
  };
}

export async function fetchFamilyBehaviorTwin(familyId: string): Promise<FamilyBehaviorTwin> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/behavior/family-twin`);
  const childrenRaw = asArray(data.children ?? data.Children);
  const children = childrenRaw.map((m) => {
    const dims = asArray(m.dimensions ?? m.Dimensions);
    const reasons = m.eveningReasonsVi ?? m.EveningReasonsVi;
    return {
      memberId: String(m.memberId ?? m.MemberId ?? ''),
      memberName: String(m.memberName ?? m.MemberName ?? ''),
      overallScore: Number(m.overallScore ?? m.OverallScore ?? 0),
      overallLabelVi: String(m.overallLabelVi ?? m.OverallLabelVi ?? ''),
      disclaimerVi: String(m.disclaimerVi ?? m.DisclaimerVi ?? ''),
      eveningRiskBand:
        m.eveningRiskBand != null || m.EveningRiskBand != null
          ? String(m.eveningRiskBand ?? m.EveningRiskBand)
          : undefined,
      eveningRiskLabelVi:
        m.eveningRiskLabelVi != null || m.EveningRiskLabelVi != null
          ? String(m.eveningRiskLabelVi ?? m.EveningRiskLabelVi)
          : undefined,
      eveningReasonsVi: Array.isArray(reasons) ? reasons.map((x) => String(x)) : [],
      eveningSuggestedActionVi:
        m.eveningSuggestedActionVi != null || m.EveningSuggestedActionVi != null
          ? String(m.eveningSuggestedActionVi ?? m.EveningSuggestedActionVi)
          : undefined,
      dimensions: dims.map((d) => ({
        code: String(d.code ?? d.Code ?? ''),
        labelVi: String(d.labelVi ?? d.LabelVi ?? ''),
        score: Number(d.score ?? d.Score ?? 0),
        whyVi: String(d.whyVi ?? d.WhyVi ?? ''),
      })),
      snapshotDate: String(m.snapshotDate ?? m.SnapshotDate ?? ''),
    };
  });

  const policyRaw = (data.policy ?? data.Policy) as Row | undefined;
  return {
    asOfDate: String(data.asOfDate ?? data.AsOfDate ?? ''),
    disclaimerVi: String(data.disclaimerVi ?? data.DisclaimerVi ?? ''),
    familyPeaceIndex: Number(data.familyPeaceIndex ?? data.FamilyPeaceIndex ?? 0),
    familyAutonomyIndex: Number(data.familyAutonomyIndex ?? data.FamilyAutonomyIndex ?? 0),
    parentalInterventionIndex: Number(
      data.parentalInterventionIndex ?? data.ParentalInterventionIndex ?? 0,
    ),
    retirementStage: String(data.retirementStage ?? data.RetirementStage ?? ''),
    retirementLabelVi: String(data.retirementLabelVi ?? data.RetirementLabelVi ?? ''),
    retirementAdviceVi: String(data.retirementAdviceVi ?? data.RetirementAdviceVi ?? ''),
    siblingBalance: String(data.siblingBalance ?? data.SiblingBalance ?? ''),
    siblingBalanceLabelVi: String(
      data.siblingBalanceLabelVi ?? data.SiblingBalanceLabelVi ?? '',
    ),
    siblingAdviceVi: String(data.siblingAdviceVi ?? data.SiblingAdviceVi ?? ''),
    dependenceWarning: Boolean(data.dependenceWarning ?? data.DependenceWarning ?? false),
    dependenceWarningVi:
      data.dependenceWarningVi != null || data.DependenceWarningVi != null
        ? String(data.dependenceWarningVi ?? data.DependenceWarningVi)
        : undefined,
    recommendObserveOnly: Boolean(
      data.recommendObserveOnly ?? data.RecommendObserveOnly ?? false,
    ),
    observeOnlyActive: Boolean(data.observeOnlyActive ?? data.ObserveOnlyActive ?? false),
    policy: mapRetirementPolicy(policyRaw),
    children,
  };
}

export interface BehaviorPatternCard {
  code: string;
  titleVi: string;
  whyVi: string;
  activeTacticCode?: string;
  activeTacticLabelVi?: string;
  childCueVi?: string;
  parentAdviceVi?: string;
  tacticLabelsVi: string[];
}

export interface ChildVoiceWeek {
  memberId?: string;
  weekStart: string;
  hardestCode?: string;
  wantParentCode?: string;
  wishVi?: string;
  parentTipsVi: string[];
  submittedAt?: string;
}

export interface WeekPlaybook {
  weekStart: string;
  asOfDate: string;
  patternCode?: string;
  patternTitleVi?: string;
  patternWhyVi?: string;
  tacticCode?: string;
  tacticLabelVi?: string;
  childCueVi?: string;
  parentAdviceVi?: string;
  parentStrategyTipVi: string;
  childVoice?: ChildVoiceWeek | null;
  catalog: BehaviorPatternCard[];
  activePatterns: BehaviorPatternCard[];
  parentNudgesThisWeek: number;
  selfStartsThisWeek: number;
}

function mapPatternCard(p: Row): BehaviorPatternCard {
  const tactics = p.tacticLabelsVi ?? p.TacticLabelsVi;
  return {
    code: String(p.code ?? p.Code ?? ''),
    titleVi: String(p.titleVi ?? p.TitleVi ?? ''),
    whyVi: String(p.whyVi ?? p.WhyVi ?? ''),
    activeTacticCode:
      p.activeTacticCode != null || p.ActiveTacticCode != null
        ? String(p.activeTacticCode ?? p.ActiveTacticCode)
        : undefined,
    activeTacticLabelVi:
      p.activeTacticLabelVi != null || p.ActiveTacticLabelVi != null
        ? String(p.activeTacticLabelVi ?? p.ActiveTacticLabelVi)
        : undefined,
    childCueVi:
      p.childCueVi != null || p.ChildCueVi != null
        ? String(p.childCueVi ?? p.ChildCueVi)
        : undefined,
    parentAdviceVi:
      p.parentAdviceVi != null || p.ParentAdviceVi != null
        ? String(p.parentAdviceVi ?? p.ParentAdviceVi)
        : undefined,
    tacticLabelsVi: Array.isArray(tactics) ? tactics.map((x) => String(x)) : [],
  };
}

function mapChildVoice(v: Row | null | undefined): ChildVoiceWeek | null {
  if (!v) return null;
  const tips = v.parentTipsVi ?? v.ParentTipsVi;
  return {
    memberId:
      v.memberId != null || v.MemberId != null ? String(v.memberId ?? v.MemberId) : undefined,
    weekStart: String(v.weekStart ?? v.WeekStart ?? ''),
    hardestCode:
      v.hardestCode != null || v.HardestCode != null
        ? String(v.hardestCode ?? v.HardestCode)
        : undefined,
    wantParentCode:
      v.wantParentCode != null || v.WantParentCode != null
        ? String(v.wantParentCode ?? v.WantParentCode)
        : undefined,
    wishVi:
      v.wishVi != null || v.WishVi != null ? String(v.wishVi ?? v.WishVi) : undefined,
    parentTipsVi: Array.isArray(tips) ? tips.map((x) => String(x)) : [],
    submittedAt:
      v.submittedAt != null || v.SubmittedAt != null
        ? String(v.submittedAt ?? v.SubmittedAt)
        : undefined,
  };
}

export async function fetchWeekPlaybook(
  familyId: string,
  opts?: { memberId?: string; asOf?: string },
): Promise<WeekPlaybook> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/behavior/week-playbook`, {
    params: {
      memberId: opts?.memberId,
      asOf: opts?.asOf,
    },
  });
  const catalog = asArray(data.catalog ?? data.Catalog).map(mapPatternCard);
  const active = asArray(data.activePatterns ?? data.ActivePatterns).map(mapPatternCard);
  const voiceRaw = (data.childVoice ?? data.ChildVoice) as Row | null | undefined;
  return {
    weekStart: String(data.weekStart ?? data.WeekStart ?? ''),
    asOfDate: String(data.asOfDate ?? data.AsOfDate ?? ''),
    patternCode:
      data.patternCode != null || data.PatternCode != null
        ? String(data.patternCode ?? data.PatternCode)
        : undefined,
    patternTitleVi:
      data.patternTitleVi != null || data.PatternTitleVi != null
        ? String(data.patternTitleVi ?? data.PatternTitleVi)
        : undefined,
    patternWhyVi:
      data.patternWhyVi != null || data.PatternWhyVi != null
        ? String(data.patternWhyVi ?? data.PatternWhyVi)
        : undefined,
    tacticCode:
      data.tacticCode != null || data.TacticCode != null
        ? String(data.tacticCode ?? data.TacticCode)
        : undefined,
    tacticLabelVi:
      data.tacticLabelVi != null || data.TacticLabelVi != null
        ? String(data.tacticLabelVi ?? data.TacticLabelVi)
        : undefined,
    childCueVi:
      data.childCueVi != null || data.ChildCueVi != null
        ? String(data.childCueVi ?? data.ChildCueVi)
        : undefined,
    parentAdviceVi:
      data.parentAdviceVi != null || data.ParentAdviceVi != null
        ? String(data.parentAdviceVi ?? data.ParentAdviceVi)
        : undefined,
    parentStrategyTipVi: String(
      data.parentStrategyTipVi ?? data.ParentStrategyTipVi ?? '',
    ),
    childVoice: mapChildVoice(voiceRaw),
    catalog,
    activePatterns: active,
    parentNudgesThisWeek: Number(
      data.parentNudgesThisWeek ?? data.ParentNudgesThisWeek ?? 0,
    ),
    selfStartsThisWeek: Number(data.selfStartsThisWeek ?? data.SelfStartsThisWeek ?? 0),
  };
}

export async function submitChildVoiceWeek(
  familyId: string,
  body: {
    memberId: string;
    weekStart?: string;
    hardestCode?: string;
    wantParentCode?: string;
    wishVi?: string;
  },
): Promise<ChildVoiceWeek> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/behavior/child-voice`,
    {
      memberId: body.memberId,
      weekStart: body.weekStart,
      hardestCode: body.hardestCode,
      wantParentCode: body.wantParentCode,
      wishVi: body.wishVi,
    },
  );
  return mapChildVoice(data) ?? {
    memberId: body.memberId,
    weekStart: body.weekStart ?? '',
    parentTipsVi: [],
  };
}

export interface FamilyCoachInsight {
  flowDate: string;
  headline: string;
  strength?: string;
  attention?: string;
  pattern?: string;
  proposal?: string;
  proposalCode?: string;
  ctaLabel?: string;
  doneCount: number;
  skippedCount: number;
  openCount: number;
  totalCount: number;
  /** Wave B — "Vì nhà bạn…" from Blueprint DNA. */
  blueprintBecauseVi?: string;
  /** Wave B — stable playbook id PB0001… */
  playbookId?: string;
  focusCommitmentTitle?: string;
  patternForgotCount?: number;
}

export async function fetchFamilyCoachInsight(
  familyId: string,
  date?: string,
): Promise<FamilyCoachInsight> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/coach-insight`, {
    params: date ? { date } : undefined,
  });
  return {
    flowDate: String(data.flowDate ?? data.FlowDate ?? date ?? ''),
    headline: String(data.headline ?? data.Headline ?? ''),
    strength:
      data.strength != null || data.Strength != null
        ? String(data.strength ?? data.Strength)
        : undefined,
    attention:
      data.attention != null || data.Attention != null
        ? String(data.attention ?? data.Attention)
        : undefined,
    pattern:
      data.pattern != null || data.Pattern != null
        ? String(data.pattern ?? data.Pattern)
        : undefined,
    proposal:
      data.proposal != null || data.Proposal != null
        ? String(data.proposal ?? data.Proposal)
        : undefined,
    proposalCode:
      data.proposalCode != null || data.ProposalCode != null
        ? String(data.proposalCode ?? data.ProposalCode)
        : undefined,
    ctaLabel:
      data.ctaLabel != null || data.CtaLabel != null
        ? String(data.ctaLabel ?? data.CtaLabel)
        : undefined,
    doneCount: Number(data.doneCount ?? data.DoneCount ?? 0),
    skippedCount: Number(data.skippedCount ?? data.SkippedCount ?? 0),
    openCount: Number(data.openCount ?? data.OpenCount ?? 0),
    totalCount: Number(data.totalCount ?? data.TotalCount ?? 0),
    blueprintBecauseVi:
      data.blueprintBecauseVi != null || data.BlueprintBecauseVi != null
        ? String(data.blueprintBecauseVi ?? data.BlueprintBecauseVi)
        : undefined,
    playbookId:
      data.playbookId != null || data.PlaybookId != null
        ? String(data.playbookId ?? data.PlaybookId)
        : undefined,
    focusCommitmentTitle:
      data.focusCommitmentTitle != null || data.FocusCommitmentTitle != null
        ? String(data.focusCommitmentTitle ?? data.FocusCommitmentTitle)
        : undefined,
    patternForgotCount:
      data.patternForgotCount != null || data.PatternForgotCount != null
        ? Number(data.patternForgotCount ?? data.PatternForgotCount)
        : undefined,
  };
}

export async function updateRetirementPolicy(
  familyId: string,
  body: { observeOnly?: boolean; parentNudgeBudget?: number; notesVi?: string },
): Promise<BehaviorRetirementPolicy> {
  const { data } = await http.put<Row>(
    `/family-os/families/${familyId}/behavior/retirement-policy`,
    body,
  );
  return mapRetirementPolicy(data);
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

export async function verifyCommitmentEvidence(
  familyId: string,
  commitmentId: string,
  checklist: {
    isTodaysWork: boolean;
    withinCommitmentWindow: boolean;
    matchesCommitment: boolean;
    overrideDuration?: boolean;
    note?: string;
  },
): Promise<CommitmentProgressResult> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/commitments/${commitmentId}/verify-evidence`,
    {
      isTodaysWork: checklist.isTodaysWork,
      withinCommitmentWindow: checklist.withinCommitmentWindow,
      matchesCommitment: checklist.matchesCommitment,
      overrideDuration: checklist.overrideDuration ?? false,
      note: checklist.note ?? null,
    },
  );
  const commitment = mapCommitment(data);
  return {
    commitment,
    memberStarBalance: commitment.memberStarBalance,
  };
}

export const EVIDENCE_REJECT_REASONS = [
  {
    code: 'wrong_content',
    labelVi: 'Nội dung không khớp cam kết',
  },
  {
    code: 'not_study',
    labelVi: 'Không thấy bài / vở / màn hình học',
  },
  {
    code: 'not_todays',
    labelVi: 'Không phải bài hôm nay',
  },
  {
    code: 'unclear',
    labelVi: 'Ảnh chưa rõ — cần gửi lại',
  },
] as const;

export type EvidenceRejectReasonCode = (typeof EVIDENCE_REJECT_REASONS)[number]['code'];

export function evidenceRejectChildMessageVi(
  code: EvidenceRejectReasonCode | string,
  commitmentTitle: string,
): string {
  const title = (commitmentTitle || 'việc học').trim() || 'việc học';
  switch (code) {
    case 'wrong_content':
      return `Ảnh «${title}» chưa khớp nội dung cam kết. Con gửi lại bài / vở / màn hình học đúng việc nhé.`;
    case 'not_study':
      return `Ảnh «${title}» chưa thấy bài học. Con chụp lại vở / sách / màn hình đang học giúp bố mẹ nhé.`;
    case 'not_todays':
      return `Ảnh «${title}» có vẻ không phải bài hôm nay. Con gửi bằng chứng của hôm nay giúp bố mẹ nhé.`;
    case 'unclear':
      return `Ảnh «${title}» chưa rõ. Con gửi lại ảnh rõ hơn để bố mẹ xác nhận được sao nhé.`;
    default:
      return `Bằng chứng «${title}» chưa đạt. Con gửi lại giúp bố mẹ nhé.`;
  }
}

export async function rejectCommitmentEvidence(
  familyId: string,
  commitmentId: string,
  input: { reasonCode: EvidenceRejectReasonCode | string; note?: string },
): Promise<CommitmentProgressResult> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/commitments/${commitmentId}/reject-evidence`,
    {
      reasonCode: input.reasonCode,
      note: input.note ?? null,
    },
  );
  const commitment = mapCommitment(data);
  return {
    commitment,
    memberStarBalance: commitment.memberStarBalance,
  };
}

export async function setCommitmentEvidencePolicy(
  familyId: string,
  commitmentId: string,
  evidencePolicy: 'optional' | 'required_soft' | 'required_hard' | string,
): Promise<CommitmentProgressResult> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/commitments/${commitmentId}/evidence-policy`,
    { evidencePolicy },
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
  description?: string;
  memberId?: string;
  windowStart?: string;
  windowEnd?: string;
  sortOrder: number;
  isActive: boolean;
  priority?: string;
  starReward?: number;
  commitmentKind?: 'chore' | 'study_focus' | 'relation' | string;
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
  const startRaw = t.windowStart ?? t.WindowStart;
  const endRaw = t.windowEnd ?? t.WindowEnd;
  return {
    id: String(t.id ?? t.Id),
    title: String(t.title ?? t.Title ?? ''),
    description:
      t.description != null || t.Description != null
        ? String(t.description ?? t.Description)
        : undefined,
    memberId:
      t.memberId != null || t.MemberId != null ? String(t.memberId ?? t.MemberId) : undefined,
    windowStart: startRaw != null ? String(startRaw).slice(0, 5) : undefined,
    windowEnd: endRaw != null ? String(endRaw).slice(0, 5) : undefined,
    sortOrder: Number(t.sortOrder ?? t.SortOrder ?? 0),
    isActive: Boolean(t.isActive ?? t.IsActive ?? true),
    priority:
      t.priority != null || t.Priority != null ? String(t.priority ?? t.Priority) : undefined,
    starReward: Number(t.starReward ?? t.StarReward ?? 0) || undefined,
    commitmentKind: String(
      t.commitmentKind ?? t.CommitmentKind ?? 'chore',
    ).toLowerCase(),
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
    commitmentKind?: string;
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
      commitmentKind: input.commitmentKind ?? null,
    },
  );
  return mapTemplate(data);
}

export async function updateCommitmentTemplate(
  familyId: string,
  routineId: string,
  templateId: string,
  input: {
    title: string;
    description?: string;
    memberId?: string;
    windowStart?: string;
    windowEnd?: string;
    sortOrder: number;
    isActive: boolean;
    priority?: string;
    starReward?: number;
    commitmentKind?: string;
  },
): Promise<CommitmentTemplateDto> {
  const { data } = await http.patch<Row>(
    `/family-os/families/${familyId}/routines/${routineId}/templates/${templateId}`,
    {
      title: input.title,
      description: input.description ?? null,
      memberId: input.memberId ?? null,
      windowStart: input.windowStart ?? null,
      windowEnd: input.windowEnd ?? null,
      sortOrder: input.sortOrder,
      isActive: input.isActive,
      priority: input.priority ?? 'normal',
      starReward: input.starReward ?? null,
      commitmentKind: input.commitmentKind ?? null,
    },
  );
  return mapTemplate(data);
}

export interface ResolvedCalendarRoutine {
  flowDate: string;
  routineId: string;
  routineDisplayName: string;
  periodDisplayName?: string;
  periodKind?: string;
}

export async function resolveCalendarRoutine(
  familyId: string,
  date?: string,
): Promise<ResolvedCalendarRoutine> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/calendar-periods/resolve`, {
    params: date ? { date } : undefined,
  });
  return {
    flowDate: String(data.flowDate ?? data.FlowDate ?? ''),
    routineId: String(data.routineId ?? data.RoutineId ?? ''),
    routineDisplayName: String(data.routineDisplayName ?? data.RoutineDisplayName ?? ''),
    periodDisplayName:
      data.periodDisplayName != null || data.PeriodDisplayName != null
        ? String(data.periodDisplayName ?? data.PeriodDisplayName)
        : undefined,
    periodKind:
      data.periodKind != null || data.PeriodKind != null
        ? String(data.periodKind ?? data.PeriodKind)
        : undefined,
  };
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

export interface FamilyDnaCard {
  familyId: string;
  hasBlueprint: boolean;
  isTeaser: boolean;
  tierCode: string;
  stageLabelVi?: string;
  valuesLabelsVi: string[];
  focusLabelsVi: string[];
  nextStepVi?: string;
  upgradeHintVi?: string;
  calibrationPhaseCode?: string;
  calibrationLabelVi?: string;
  coachTipVi?: string;
  needsCalibrationCapture?: boolean;
  careValueVi?: string;
  growthBalanceLabelVi?: string;
  primaryWorryCode?: string;
}

export interface FamilyBlueprint {
  familyId: string;
  layersJson: string;
  dnaJson: string;
  schemaVersion: number;
  hydratedAt?: string;
  updatedAt?: string;
}

function mapDnaCard(r: Row): FamilyDnaCard {
  return {
    familyId: String(r.familyId ?? r.FamilyId ?? ''),
    hasBlueprint: Boolean(r.hasBlueprint ?? r.HasBlueprint),
    isTeaser: Boolean(r.isTeaser ?? r.IsTeaser),
    tierCode: String(r.tierCode ?? r.TierCode ?? 'free'),
    stageLabelVi:
      r.stageLabelVi != null || r.StageLabelVi != null
        ? String(r.stageLabelVi ?? r.StageLabelVi)
        : undefined,
    valuesLabelsVi: asArray(r.valuesLabelsVi ?? r.ValuesLabelsVi).map((x) => String(x)),
    focusLabelsVi: asArray(r.focusLabelsVi ?? r.FocusLabelsVi).map((x) => String(x)),
    nextStepVi:
      r.nextStepVi != null || r.NextStepVi != null
        ? String(r.nextStepVi ?? r.NextStepVi)
        : undefined,
    upgradeHintVi:
      r.upgradeHintVi != null || r.UpgradeHintVi != null
        ? String(r.upgradeHintVi ?? r.UpgradeHintVi)
        : undefined,
    calibrationPhaseCode:
      r.calibrationPhaseCode != null || r.CalibrationPhaseCode != null
        ? String(r.calibrationPhaseCode ?? r.CalibrationPhaseCode)
        : undefined,
    calibrationLabelVi:
      r.calibrationLabelVi != null || r.CalibrationLabelVi != null
        ? String(r.calibrationLabelVi ?? r.CalibrationLabelVi)
        : undefined,
    coachTipVi:
      r.coachTipVi != null || r.CoachTipVi != null
        ? String(r.coachTipVi ?? r.CoachTipVi)
        : undefined,
    needsCalibrationCapture: Boolean(
      r.needsCalibrationCapture ?? r.NeedsCalibrationCapture ?? false,
    ),
    careValueVi:
      r.careValueVi != null || r.CareValueVi != null
        ? String(r.careValueVi ?? r.CareValueVi)
        : undefined,
    growthBalanceLabelVi:
      r.growthBalanceLabelVi != null || r.GrowthBalanceLabelVi != null
        ? String(r.growthBalanceLabelVi ?? r.GrowthBalanceLabelVi)
        : undefined,
    primaryWorryCode:
      r.primaryWorryCode != null || r.PrimaryWorryCode != null
        ? String(r.primaryWorryCode ?? r.PrimaryWorryCode)
        : undefined,
  };
}

export async function fetchFamilyDnaCard(familyId: string): Promise<FamilyDnaCard> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/blueprint/dna`);
  return mapDnaCard(data);
}

export async function captureFamilyCalibration(
  familyId: string,
  input: {
    schoolContextCode?: string;
    selfViewCode?: string;
    peerShockCode?: string;
    childShortName?: string;
    noteVi?: string;
    resourceBandCode?: string;
    primaryWorryCode?: string;
  },
): Promise<FamilyDnaCard> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/blueprint/calibration`, {
    schoolContextCode: input.schoolContextCode ?? null,
    selfViewCode: input.selfViewCode ?? null,
    peerShockCode: input.peerShockCode ?? null,
    childShortName: input.childShortName ?? null,
    noteVi: input.noteVi ?? null,
    resourceBandCode: input.resourceBandCode ?? null,
    primaryWorryCode: input.primaryWorryCode ?? null,
  });
  return mapDnaCard(data);
}

export async function hydrateFamilyBlueprint(
  familyId: string,
  onboardingPayloadJson?: string,
): Promise<FamilyBlueprint> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/blueprint/hydrate`, {
    onboardingPayloadJson: onboardingPayloadJson ?? null,
  });
  return {
    familyId: String(data.familyId ?? data.FamilyId ?? familyId),
    layersJson: String(data.layersJson ?? data.LayersJson ?? '{}'),
    dnaJson: String(data.dnaJson ?? data.DnaJson ?? '{}'),
    schemaVersion: Number(data.schemaVersion ?? data.SchemaVersion ?? 1),
    hydratedAt:
      data.hydratedAt != null || data.HydratedAt != null
        ? String(data.hydratedAt ?? data.HydratedAt)
        : undefined,
    updatedAt:
      data.updatedAt != null || data.UpdatedAt != null
        ? String(data.updatedAt ?? data.UpdatedAt)
        : undefined,
  };
}

function mapFamilyBlueprint(data: Row, familyId: string): FamilyBlueprint {
  return {
    familyId: String(data.familyId ?? data.FamilyId ?? familyId),
    layersJson: String(data.layersJson ?? data.LayersJson ?? '{}'),
    dnaJson: String(data.dnaJson ?? data.DnaJson ?? '{}'),
    schemaVersion: Number(data.schemaVersion ?? data.SchemaVersion ?? 1),
    hydratedAt:
      data.hydratedAt != null || data.HydratedAt != null
        ? String(data.hydratedAt ?? data.HydratedAt)
        : undefined,
    updatedAt:
      data.updatedAt != null || data.UpdatedAt != null
        ? String(data.updatedAt ?? data.UpdatedAt)
        : undefined,
  };
}

export async function fetchFamilyBlueprint(familyId: string): Promise<FamilyBlueprint | null> {
  try {
    const { data } = await http.get<Row>(`/family-os/families/${familyId}/blueprint`);
    if (!data) return null;
    return mapFamilyBlueprint(data, familyId);
  } catch {
    return null;
  }
}

/** Upsert layers. Prefer patchFamilyBlueprintLayers for nested keys (server merge is shallow). */
export async function putFamilyBlueprintLayers(
  familyId: string,
  layersJson: string,
  replace = false,
): Promise<FamilyBlueprint> {
  const { data } = await http.put<Row>(`/family-os/families/${familyId}/blueprint`, {
    layersJson,
    dnaJson: null,
    replace,
  });
  return mapFamilyBlueprint(data, familyId);
}

/** Client deep-merge then replace layers (keeps dna). */
export async function patchFamilyBlueprintLayers(
  familyId: string,
  patch: Record<string, unknown>,
): Promise<FamilyBlueprint> {
  const existing = await fetchFamilyBlueprint(familyId);
  const merged = deepMergeLayers(parseLayersJson(existing?.layersJson), patch);
  return putFamilyBlueprintLayers(familyId, JSON.stringify(merged), true);
}

/** SCH-02 — one child schedule + derived phase/quiet. */
export type FamilySchoolScheduleMember = {
  memberId: string;
  displayName?: string;
  schedule: {
    schemaVersion: number;
    seasonOn: boolean;
    mode: string;
    weekdays: number[];
    schoolStart: string;
    schoolEnd: string;
    hasExtraClass: boolean;
    extraEnd?: string;
    source: string;
    updatedAt: string;
    updatedByMemberId?: string;
  } | null;
  derived: {
    phase: string;
    quietNow: boolean;
    quietEnd: string;
    asOf: string;
    timeZone: string;
  };
};

export type FamilySchoolQuietMap = {
  asOf: string;
  timeZone: string;
  members: Array<{
    memberId: string;
    displayName?: string;
    quietNow: boolean;
    phase: string;
    quietEnd: string;
  }>;
};

function mapSchoolDerived(raw: Row): FamilySchoolScheduleMember['derived'] {
  return {
    phase: String(raw.phase ?? raw.Phase ?? 'season_off'),
    quietNow: Boolean(raw.quietNow ?? raw.QuietNow ?? false),
    quietEnd: String(raw.quietEnd ?? raw.QuietEnd ?? '07:00'),
    asOf: String(raw.asOf ?? raw.AsOf ?? ''),
    timeZone: String(raw.timeZone ?? raw.TimeZone ?? 'Asia/Ho_Chi_Minh'),
  };
}

function mapSchoolPayload(raw: Row | null | undefined): FamilySchoolScheduleMember['schedule'] {
  if (!raw) return null;
  const weekdays = asArray(raw.weekdays ?? raw.Weekdays)
    .map((x) => Number(x))
    .filter((n) => n >= 1 && n <= 7);
  return {
    schemaVersion: Number(raw.schemaVersion ?? raw.SchemaVersion ?? 1) || 1,
    seasonOn: Boolean(raw.seasonOn ?? raw.SeasonOn ?? true),
    mode: String(raw.mode ?? raw.Mode ?? 'full'),
    weekdays: weekdays.length > 0 ? weekdays : [1, 2, 3, 4, 5],
    schoolStart: String(raw.schoolStart ?? raw.SchoolStart ?? '07:00'),
    schoolEnd: String(raw.schoolEnd ?? raw.SchoolEnd ?? '16:30'),
    hasExtraClass: Boolean(raw.hasExtraClass ?? raw.HasExtraClass ?? false),
    extraEnd:
      raw.extraEnd != null || raw.ExtraEnd != null
        ? String(raw.extraEnd ?? raw.ExtraEnd)
        : undefined,
    source: String(raw.source ?? raw.Source ?? 'parent_settings'),
    updatedAt: String(raw.updatedAt ?? raw.UpdatedAt ?? ''),
    updatedByMemberId:
      raw.updatedByMemberId != null || raw.UpdatedByMemberId != null
        ? String(raw.updatedByMemberId ?? raw.UpdatedByMemberId)
        : undefined,
  };
}

export async function fetchMemberSchoolSchedule(
  familyId: string,
  memberId: string,
  asOf?: string,
): Promise<FamilySchoolScheduleMember> {
  const { data } = await http.get<Row>(
    `/family-os/families/${familyId}/members/${memberId}/school-schedule`,
    { params: asOf ? { asOf } : undefined },
  );
  const derivedRaw = (data.derived ?? data.Derived ?? {}) as Row;
  const scheduleRaw = (data.schedule ?? data.Schedule) as Row | null | undefined;
  return {
    memberId: String(data.memberId ?? data.MemberId ?? memberId),
    displayName:
      data.displayName != null || data.DisplayName != null
        ? String(data.displayName ?? data.DisplayName)
        : undefined,
    schedule: mapSchoolPayload(scheduleRaw),
    derived: mapSchoolDerived(derivedRaw),
  };
}

export async function fetchSchoolQuietMap(
  familyId: string,
  asOf?: string,
): Promise<FamilySchoolQuietMap> {
  const { data } = await http.get<Row>(
    `/family-os/families/${familyId}/school-schedule/quiet-map`,
    { params: asOf ? { asOf } : undefined },
  );
  return {
    asOf: String(data.asOf ?? data.AsOf ?? ''),
    timeZone: String(data.timeZone ?? data.TimeZone ?? 'Asia/Ho_Chi_Minh'),
    members: asArray(data.members ?? data.Members).map((r) => ({
      memberId: String(r.memberId ?? r.MemberId ?? ''),
      displayName:
        r.displayName != null || r.DisplayName != null
          ? String(r.displayName ?? r.DisplayName)
          : undefined,
      quietNow: Boolean(r.quietNow ?? r.QuietNow ?? false),
      phase: String(r.phase ?? r.Phase ?? 'season_off'),
      quietEnd: String(r.quietEnd ?? r.QuietEnd ?? ''),
    })).filter((m) => m.memberId),
  };
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

export type TeamNudgeStatus = 'draft' | 'sent' | 'seen' | 'thanks' | 'deferred' | string;
export type TeamNudgeTemplate = 'cheer_up' | 'one_left' | 'you_got_this' | 'thanks_back';

export interface TeamNudge {
  id: string;
  familyId: string;
  flowDate: string;
  fromMemberId: string;
  fromName: string;
  toMemberId: string;
  toName: string;
  commitmentId?: string;
  templateCode: TeamNudgeTemplate | string;
  messageVi: string;
  status: TeamNudgeStatus;
  sentAt?: string;
  ackAt?: string;
  createdAt: string;
}

export interface TeamNudgeCandidate {
  memberId: string;
  displayName: string;
  stageCode: string;
  canInvite: boolean;
  missionsComplete: boolean;
}

function mapTeamNudge(r: Row): TeamNudge {
  return {
    id: String(r.id ?? r.Id ?? ''),
    familyId: String(r.familyId ?? r.FamilyId ?? ''),
    flowDate: String(r.flowDate ?? r.FlowDate ?? ''),
    fromMemberId: String(r.fromMemberId ?? r.FromMemberId ?? ''),
    fromName: String(r.fromName ?? r.FromName ?? ''),
    toMemberId: String(r.toMemberId ?? r.ToMemberId ?? ''),
    toName: String(r.toName ?? r.ToName ?? ''),
    commitmentId:
      r.commitmentId != null || r.CommitmentId != null
        ? String(r.commitmentId ?? r.CommitmentId)
        : undefined,
    templateCode: String(r.templateCode ?? r.TemplateCode ?? 'cheer_up'),
    messageVi: String(r.messageVi ?? r.MessageVi ?? ''),
    status: String(r.status ?? r.Status ?? ''),
    sentAt:
      r.sentAt != null || r.SentAt != null ? String(r.sentAt ?? r.SentAt) : undefined,
    ackAt: r.ackAt != null || r.AckAt != null ? String(r.ackAt ?? r.AckAt) : undefined,
    createdAt: String(r.createdAt ?? r.CreatedAt ?? ''),
  };
}

function mapTeamNudgeCandidate(r: Row): TeamNudgeCandidate {
  return {
    memberId: String(r.memberId ?? r.MemberId ?? ''),
    displayName: String(r.displayName ?? r.DisplayName ?? ''),
    stageCode: String(r.stageCode ?? r.StageCode ?? ''),
    canInvite: Boolean(r.canInvite ?? r.CanInvite ?? false),
    missionsComplete: Boolean(r.missionsComplete ?? r.MissionsComplete ?? false),
  };
}

export async function fetchTeamNudges(
  familyId: string,
  opts?: { flowDate?: string; forMemberId?: string },
): Promise<TeamNudge[]> {
  const { data } = await http.get<unknown>(`/family-os/families/${familyId}/team-nudges`, {
    params: { flowDate: opts?.flowDate, forMemberId: opts?.forMemberId },
  });
  return asArray(data).map(mapTeamNudge);
}

export async function fetchTeamNudgeFromCandidates(
  familyId: string,
  flowDate?: string,
): Promise<TeamNudgeCandidate[]> {
  const { data } = await http.get<unknown>(
    `/family-os/families/${familyId}/team-nudges/from-candidates`,
    { params: { flowDate } },
  );
  return asArray(data).map(mapTeamNudgeCandidate);
}

export async function createTeamNudge(
  familyId: string,
  input: {
    fromMemberId: string;
    toMemberId: string;
    templateCode: TeamNudgeTemplate;
    flowDate?: string;
    commitmentId?: string;
  },
): Promise<TeamNudge> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/team-nudges`, {
    fromMemberId: input.fromMemberId,
    toMemberId: input.toMemberId,
    templateCode: input.templateCode,
    flowDate: input.flowDate ?? null,
    commitmentId: input.commitmentId ?? null,
  });
  return mapTeamNudge(data);
}

export async function sendTeamNudge(familyId: string, nudgeId: string): Promise<TeamNudge> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/team-nudges/${nudgeId}/send`,
    null,
  );
  return mapTeamNudge(data);
}

export async function ackTeamNudge(
  familyId: string,
  nudgeId: string,
  status: 'seen' | 'thanks' | 'deferred',
): Promise<TeamNudge> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/team-nudges/${nudgeId}/ack`,
    { status },
  );
  return mapTeamNudge(data);
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

/** Relationship Engine P0 — max 2 triggers / viewer / day. */
export type RelationshipTrigger = {
  code: string;
  titleVi: string;
  bodyVi: string;
  ctaLabelVi: string;
  toMemberId?: string;
  toMemberName?: string;
  draftBodyVi?: string;
  templateCode?: string;
  whyNow: string;
  isGolden?: boolean;
};

export type ParentVoiceMessage = {
  id: string;
  familyId: string;
  fromMemberId: string;
  fromMemberName: string;
  toMemberId: string;
  toMemberName: string;
  flowDate: string;
  templateCode: string;
  bodyVi: string;
  status: string;
  sentAt: string;
  ackAt?: string;
};

function mapRelationshipTrigger(r: Row): RelationshipTrigger {
  return {
    code: String(r.code ?? r.Code ?? ''),
    titleVi: String(r.titleVi ?? r.TitleVi ?? ''),
    bodyVi: String(r.bodyVi ?? r.BodyVi ?? ''),
    ctaLabelVi: String(r.ctaLabelVi ?? r.CtaLabelVi ?? 'Gửi'),
    toMemberId:
      r.toMemberId != null || r.ToMemberId != null
        ? String(r.toMemberId ?? r.ToMemberId)
        : undefined,
    toMemberName:
      r.toMemberName != null || r.ToMemberName != null
        ? String(r.toMemberName ?? r.ToMemberName)
        : undefined,
    draftBodyVi:
      r.draftBodyVi != null || r.DraftBodyVi != null
        ? String(r.draftBodyVi ?? r.DraftBodyVi)
        : undefined,
    templateCode:
      r.templateCode != null || r.TemplateCode != null
        ? String(r.templateCode ?? r.TemplateCode)
        : undefined,
    whyNow: String(r.whyNow ?? r.WhyNow ?? ''),
    isGolden: Boolean(r.isGolden ?? r.IsGolden ?? false),
  };
}

function mapParentVoice(r: Row): ParentVoiceMessage {
  return {
    id: String(r.id ?? r.Id ?? ''),
    familyId: String(r.familyId ?? r.FamilyId ?? ''),
    fromMemberId: String(r.fromMemberId ?? r.FromMemberId ?? ''),
    fromMemberName: String(r.fromMemberName ?? r.FromMemberName ?? ''),
    toMemberId: String(r.toMemberId ?? r.ToMemberId ?? ''),
    toMemberName: String(r.toMemberName ?? r.ToMemberName ?? ''),
    flowDate: String(r.flowDate ?? r.FlowDate ?? ''),
    templateCode: String(r.templateCode ?? r.TemplateCode ?? 'praise'),
    bodyVi: String(r.bodyVi ?? r.BodyVi ?? ''),
    status: String(r.status ?? r.Status ?? 'sent'),
    sentAt: String(r.sentAt ?? r.SentAt ?? ''),
    ackAt: r.ackAt != null || r.AckAt != null ? String(r.ackAt ?? r.AckAt) : undefined,
  };
}

export async function fetchRelationshipTriggers(
  familyId: string,
  forMemberId: string,
  flowDate?: string,
): Promise<RelationshipTrigger[]> {
  const { data } = await http.get<unknown>(
    `/family-os/families/${familyId}/relationship/triggers`,
    { params: { forMemberId, flowDate } },
  );
  return asArray(data).map((r) => mapRelationshipTrigger(r as Row));
}

/** P1.2 — persisted trigger UI state (opened / dismissed / sent). */
export type RelationshipTriggerState = {
  id: string;
  familyId: string;
  viewerMemberId: string;
  flowDate: string;
  triggerCode: string;
  toMemberId?: string;
  state: 'opened' | 'dismissed' | 'sent' | string;
  draftBodyVi?: string;
  templateCode?: string;
  titleVi?: string;
  bodyVi?: string;
  updatedAt: string;
};

function mapRelationshipTriggerState(r: Row): RelationshipTriggerState {
  return {
    id: String(r.id ?? r.Id ?? ''),
    familyId: String(r.familyId ?? r.FamilyId ?? ''),
    viewerMemberId: String(r.viewerMemberId ?? r.ViewerMemberId ?? ''),
    flowDate: String(r.flowDate ?? r.FlowDate ?? ''),
    triggerCode: String(r.triggerCode ?? r.TriggerCode ?? ''),
    toMemberId:
      r.toMemberId != null || r.ToMemberId != null
        ? String(r.toMemberId ?? r.ToMemberId)
        : undefined,
    state: String(r.state ?? r.State ?? ''),
    draftBodyVi:
      r.draftBodyVi != null || r.DraftBodyVi != null
        ? String(r.draftBodyVi ?? r.DraftBodyVi)
        : undefined,
    templateCode:
      r.templateCode != null || r.TemplateCode != null
        ? String(r.templateCode ?? r.TemplateCode)
        : undefined,
    titleVi:
      r.titleVi != null || r.TitleVi != null ? String(r.titleVi ?? r.TitleVi) : undefined,
    bodyVi: r.bodyVi != null || r.BodyVi != null ? String(r.bodyVi ?? r.BodyVi) : undefined,
    updatedAt: String(r.updatedAt ?? r.UpdatedAt ?? ''),
  };
}

export async function fetchRelationshipTriggerStates(
  familyId: string,
  viewerMemberId: string,
  flowDate?: string,
): Promise<RelationshipTriggerState[]> {
  const { data } = await http.get<unknown>(
    `/family-os/families/${familyId}/relationship/trigger-states`,
    { params: { viewerMemberId, flowDate } },
  );
  return asArray(data).map((r) => mapRelationshipTriggerState(r as Row));
}

export async function upsertRelationshipTriggerState(
  familyId: string,
  input: {
    viewerMemberId: string;
    triggerCode: string;
    state: 'opened' | 'dismissed' | 'sent';
    toMemberId?: string;
    flowDate?: string;
    draftBodyVi?: string;
    templateCode?: string;
    titleVi?: string;
    bodyVi?: string;
  },
): Promise<RelationshipTriggerState> {
  const { data } = await http.put<Row>(
    `/family-os/families/${familyId}/relationship/trigger-states`,
    {
      viewerMemberId: input.viewerMemberId,
      triggerCode: input.triggerCode,
      state: input.state,
      toMemberId: input.toMemberId ?? null,
      flowDate: input.flowDate ?? null,
      draftBodyVi: input.draftBodyVi ?? null,
      templateCode: input.templateCode ?? null,
      titleVi: input.titleVi ?? null,
      bodyVi: input.bodyVi ?? null,
    },
  );
  return mapRelationshipTriggerState(data);
}

export async function fetchParentVoice(
  familyId: string,
  opts?: { forMemberId?: string; fromMemberId?: string; flowDate?: string },
): Promise<ParentVoiceMessage[]> {
  const { data } = await http.get<unknown>(`/family-os/families/${familyId}/parent-voice`, {
    params: {
      forMemberId: opts?.forMemberId,
      fromMemberId: opts?.fromMemberId,
      flowDate: opts?.flowDate,
    },
  });
  return asArray(data).map((r) => mapParentVoice(r as Row));
}

export async function sendParentVoice(
  familyId: string,
  input: {
    fromMemberId: string;
    toMemberId: string;
    templateCode: string;
    bodyVi: string;
    flowDate?: string;
  },
): Promise<ParentVoiceMessage> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/parent-voice`, {
    fromMemberId: input.fromMemberId,
    toMemberId: input.toMemberId,
    templateCode: input.templateCode,
    bodyVi: input.bodyVi,
    flowDate: input.flowDate ?? null,
  });
  return mapParentVoice(data);
}

export async function ackParentVoice(
  familyId: string,
  messageId: string,
  status: 'read' | 'thanks',
): Promise<void> {
  await http.post(`/family-os/families/${familyId}/parent-voice/${messageId}/ack`, { status });
}

export type EveningCircleAnswer = {
  memoryId: string;
  memberId: string;
  memberName: string;
  answerVi: string;
  happenedAt: string;
};

export type EveningCircle = {
  flowDate: string;
  promptVi: string;
  alreadyAnswered: boolean;
  answers: EveningCircleAnswer[];
};

export type WeeklyStoryLine = {
  icon: string;
  textVi: string;
  memoryId?: string;
};

export type WeeklyStory = {
  from: string;
  to: string;
  headlineVi: string;
  parentVoiceCount: number;
  helpCount: number;
  gratitudeCount: number;
  ritualCount: number;
  streakMilestoneCount: number;
  eveningCircleCount: number;
  lines: WeeklyStoryLine[];
};

function mapEveningCircle(r: Row): EveningCircle {
  const answersRaw = (r.answers ?? r.Answers ?? []) as unknown[];
  return {
    flowDate: String(r.flowDate ?? r.FlowDate ?? ''),
    promptVi: String(r.promptVi ?? r.PromptVi ?? ''),
    alreadyAnswered: Boolean(r.alreadyAnswered ?? r.AlreadyAnswered ?? false),
    answers: asArray(answersRaw).map((a) => {
      const row = a as Row;
      return {
        memoryId: String(row.memoryId ?? row.MemoryId ?? ''),
        memberId: String(row.memberId ?? row.MemberId ?? ''),
        memberName: String(row.memberName ?? row.MemberName ?? ''),
        answerVi: String(row.answerVi ?? row.AnswerVi ?? ''),
        happenedAt: String(row.happenedAt ?? row.HappenedAt ?? ''),
      };
    }),
  };
}

function mapWeeklyStory(r: Row): WeeklyStory {
  const linesRaw = (r.lines ?? r.Lines ?? []) as unknown[];
  return {
    from: String(r.from ?? r.From ?? ''),
    to: String(r.to ?? r.To ?? ''),
    headlineVi: String(r.headlineVi ?? r.HeadlineVi ?? ''),
    parentVoiceCount: Number(r.parentVoiceCount ?? r.ParentVoiceCount ?? 0),
    helpCount: Number(r.helpCount ?? r.HelpCount ?? 0),
    gratitudeCount: Number(r.gratitudeCount ?? r.GratitudeCount ?? 0),
    ritualCount: Number(r.ritualCount ?? r.RitualCount ?? 0),
    streakMilestoneCount: Number(r.streakMilestoneCount ?? r.StreakMilestoneCount ?? 0),
    eveningCircleCount: Number(r.eveningCircleCount ?? r.EveningCircleCount ?? 0),
    lines: asArray(linesRaw).map((line) => {
      const row = line as Row;
      return {
        icon: String(row.icon ?? row.Icon ?? '🌱'),
        textVi: String(row.textVi ?? row.TextVi ?? ''),
        memoryId:
          row.memoryId != null || row.MemoryId != null
            ? String(row.memoryId ?? row.MemoryId)
            : undefined,
      };
    }),
  };
}

export async function fetchEveningCircle(
  familyId: string,
  opts?: { forMemberId?: string; flowDate?: string },
): Promise<EveningCircle> {
  const { data } = await http.get<Row>(
    `/family-os/families/${familyId}/relationship/evening-circle`,
    { params: { forMemberId: opts?.forMemberId, flowDate: opts?.flowDate } },
  );
  return mapEveningCircle(data);
}

export async function answerEveningCircle(
  familyId: string,
  input: { memberId: string; answerVi: string; flowDate?: string },
): Promise<EveningCircle> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/relationship/evening-circle`,
    {
      memberId: input.memberId,
      answerVi: input.answerVi,
      flowDate: input.flowDate ?? null,
    },
  );
  return mapEveningCircle(data);
}

export async function fetchWeeklyStory(
  familyId: string,
  asOf?: string,
  forMemberId?: string,
): Promise<WeeklyStory> {
  const { data } = await http.get<Row>(
    `/family-os/families/${familyId}/relationship/weekly-story`,
    { params: { asOf, forMemberId } },
  );
  return mapWeeklyStory(data);
}

export type FamilyMemoryKind =
  | 'beautiful_day'
  | 'streak_milestone'
  | 'gratitude'
  | 'photo'
  | 'team_unlock'
  | 'reward'
  | 'first_time'
  | 'manual'
  | 'help'
  | 'team_day'
  | 'parent_habit'
  | 'parent_voice'
  | 'evening_circle'
  | 'kid_moment';

export interface CooperationScore {
  period: string;
  from: string;
  to: string;
  total: number;
  headlineVi: string;
  pillars: {
    teamCompletion: number;
    familyStreak: number;
    helpEachOther: number;
    teamUnlock: number;
    familyHarmony: number;
  };
  sparkline: Array<{ scoreDate: string; total: number }>;
}

export interface FamilyRitual {
  code: string;
  labelVi: string;
  cadence: string;
  doneThisPeriod: boolean;
  periodStart: string;
  doneAt?: string;
}

export async function fetchFamilyRituals(
  familyId: string,
  asOf?: string,
): Promise<FamilyRitual[]> {
  const { data } = await http.get<unknown>(`/family-os/families/${familyId}/rituals`, {
    params: { asOf },
  });
  return asArray(data).map((r) => ({
    code: String(r.code ?? r.Code ?? ''),
    labelVi: String(r.labelVi ?? r.LabelVi ?? ''),
    cadence: String(r.cadence ?? r.Cadence ?? 'weekly'),
    doneThisPeriod: Boolean(r.doneThisPeriod ?? r.DoneThisPeriod ?? false),
    periodStart: String(r.periodStart ?? r.PeriodStart ?? ''),
    doneAt:
      r.doneAt != null || r.DoneAt != null ? String(r.doneAt ?? r.DoneAt) : undefined,
  }));
}

export async function checkinFamilyRitual(
  familyId: string,
  input: { ritualCode: string; notedBy?: string; noteVi?: string },
): Promise<FamilyRitual> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/rituals/checkin`, {
    ritualCode: input.ritualCode,
    notedBy: input.notedBy ?? null,
    noteVi: input.noteVi ?? null,
  });
  return {
    code: String(data.code ?? data.Code ?? ''),
    labelVi: String(data.labelVi ?? data.LabelVi ?? ''),
    cadence: String(data.cadence ?? data.Cadence ?? 'weekly'),
    doneThisPeriod: Boolean(data.doneThisPeriod ?? data.DoneThisPeriod ?? true),
    periodStart: String(data.periodStart ?? data.PeriodStart ?? ''),
    doneAt:
      data.doneAt != null || data.DoneAt != null
        ? String(data.doneAt ?? data.DoneAt)
        : undefined,
  };
}

export async function fetchCooperationScore(
  familyId: string,
  period: 'week' | 'month' = 'week',
): Promise<CooperationScore> {
  const { data } = await http.get<Row>(
    `/family-os/families/${familyId}/cooperation-score`,
    { params: { period } },
  );
  const pillars = (data.pillars ?? data.Pillars ?? {}) as Row;
  return {
    period: String(data.period ?? data.Period ?? period),
    from: String(data.from ?? data.From ?? ''),
    to: String(data.to ?? data.To ?? ''),
    total: Number(data.total ?? data.Total ?? 0),
    headlineVi: String(data.headlineVi ?? data.HeadlineVi ?? ''),
    pillars: {
      teamCompletion: Number(pillars.teamCompletion ?? pillars.TeamCompletion ?? 0),
      familyStreak: Number(pillars.familyStreak ?? pillars.FamilyStreak ?? 0),
      helpEachOther: Number(pillars.helpEachOther ?? pillars.HelpEachOther ?? 0),
      teamUnlock: Number(pillars.teamUnlock ?? pillars.TeamUnlock ?? 0),
      familyHarmony: Number(pillars.familyHarmony ?? pillars.FamilyHarmony ?? 0),
    },
    sparkline: asArray(data.sparkline ?? data.Sparkline).map((r) => ({
      scoreDate: String(r.scoreDate ?? r.ScoreDate ?? ''),
      total: Number(r.total ?? r.Total ?? 0),
    })),
  };
}

export interface FamilyMemoryEntry {
  id: string;
  familyId: string;
  memberId?: string;
  memberName?: string;
  flowDate: string;
  kind: FamilyMemoryKind;
  titleVi: string;
  noteVi?: string;
  icon?: string;
  photoUrl?: string;
  isFavorite: boolean;
  happenedAt: string;
}

export interface FamilyMemoryRecap {
  from: string;
  to: string;
  totalCount: number;
  beautifulDays: number;
  gratitudeCount: number;
  photoCount: number;
  celebrationCount: number;
  bestStreak: number;
  headlineVi: string;
  highlights: FamilyMemoryEntry[];
}

function mapFamilyMemory(r: Row): FamilyMemoryEntry {
  return {
    id: String(r.id ?? r.Id ?? ''),
    familyId: String(r.familyId ?? r.FamilyId ?? ''),
    memberId:
      r.memberId != null || r.MemberId != null ? String(r.memberId ?? r.MemberId) : undefined,
    memberName:
      r.memberName != null || r.MemberName != null
        ? String(r.memberName ?? r.MemberName)
        : undefined,
    flowDate: String(r.flowDate ?? r.FlowDate ?? ''),
    kind: String(r.kind ?? r.Kind ?? 'manual') as FamilyMemoryKind,
    titleVi: String(r.titleVi ?? r.TitleVi ?? ''),
    noteVi: r.noteVi != null || r.NoteVi != null ? String(r.noteVi ?? r.NoteVi) : undefined,
    icon: r.icon != null || r.Icon != null ? String(r.icon ?? r.Icon) : undefined,
    photoUrl:
      r.photoUrl != null || r.PhotoUrl != null ? String(r.photoUrl ?? r.PhotoUrl) : undefined,
    isFavorite: Boolean(r.isFavorite ?? r.IsFavorite ?? false),
    happenedAt: String(r.happenedAt ?? r.HappenedAt ?? ''),
  };
}

export async function fetchFamilyMemories(
  familyId: string,
  params?: {
    from?: string;
    to?: string;
    favoritesOnly?: boolean;
    limit?: number;
    memberId?: string;
  },
): Promise<FamilyMemoryEntry[]> {
  const { data } = await http.get<unknown>(`/family-os/families/${familyId}/memories`, {
    params: {
      from: params?.from,
      to: params?.to,
      favoritesOnly: params?.favoritesOnly,
      limit: params?.limit,
      memberId: params?.memberId,
    },
  });
  return asArray(data).map((r) => mapFamilyMemory(r as Row));
}

export async function createFamilyMemory(
  familyId: string,
  input: {
    titleVi: string;
    flowDate?: string;
    memberId?: string;
    kind?: FamilyMemoryKind;
    noteVi?: string;
    icon?: string;
    photoUrl?: string;
  },
): Promise<FamilyMemoryEntry> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/memories`, {
    titleVi: input.titleVi,
    flowDate: input.flowDate ?? null,
    memberId: input.memberId ?? null,
    kind: input.kind ?? null,
    noteVi: input.noteVi ?? null,
    icon: input.icon ?? null,
    photoUrl: input.photoUrl ?? null,
  });
  return mapFamilyMemory(data);
}

export async function setFamilyMemoryFavorite(
  familyId: string,
  memoryId: string,
  value: boolean,
): Promise<void> {
  await http.post(
    `/family-os/families/${familyId}/memories/${memoryId}/favorite`,
    null,
    { params: { value } },
  );
}

export async function deleteFamilyMemory(familyId: string, memoryId: string): Promise<void> {
  await http.delete(`/family-os/families/${familyId}/memories/${memoryId}`);
}

export async function fetchFamilyMemoryRecap(
  familyId: string,
  params?: { from?: string; to?: string },
): Promise<FamilyMemoryRecap> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/memories/recap`, {
    params: { from: params?.from, to: params?.to },
  });
  const r = (data ?? {}) as Row;
  return {
    from: String(r.from ?? r.From ?? ''),
    to: String(r.to ?? r.To ?? ''),
    totalCount: Number(r.totalCount ?? r.TotalCount ?? 0),
    beautifulDays: Number(r.beautifulDays ?? r.BeautifulDays ?? 0),
    gratitudeCount: Number(r.gratitudeCount ?? r.GratitudeCount ?? 0),
    photoCount: Number(r.photoCount ?? r.PhotoCount ?? 0),
    celebrationCount: Number(r.celebrationCount ?? r.CelebrationCount ?? 0),
    bestStreak: Number(r.bestStreak ?? r.BestStreak ?? 0),
    headlineVi: String(r.headlineVi ?? r.HeadlineVi ?? ''),
    highlights: asArray(r.highlights ?? r.Highlights).map((x) => mapFamilyMemory(x as Row)),
  };
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

/** Evidence Engine — weekly report aggregated server-side from day-flow / stars / reminders. */
export interface FamilyWeeklyInsight {
  familyId: string;
  periodStart: string;
  periodEnd: string;
  days: number;
  dataDays: number;
  isPartial: boolean;
  note?: string;
  totalCommitments: number;
  doneCount: number;
  onTimeDoneCount: number;
  lateDoneCount: number;
  skippedCount: number;
  pendingCount: number;
  completionRate?: number;
  onTimeRate?: number;
  starsEarned: number;
    health: {
      score?: number;
      completion?: number;
      reminderCalm?: number;
      streak?: number;
      onTime?: number;
      label?: string;
      promiseLine?: string;
      parentProgress?: number;
    };
  reminders: {
    tracked: boolean;
    count: number;
    previousCount: number;
    deltaPct?: number;
  };
  members: Array<{
    memberId?: string;
    name: string;
    totalCommitments: number;
    doneCount: number;
    onTimeDoneCount: number;
    skippedCount: number;
    completionRate?: number;
    starsEarned: number;
    currentStreakDays: number;
  }>;
  habits: Array<{
    templateId?: string;
    title: string;
    memberName?: string;
    occurrences: number;
    doneCount: number;
    forgotCount: number;
    doneRate?: number;
    previousDoneRate?: number;
    trend: string;
  }>;
  highlights: string[];
  /** Evidence P0 pilot metrics */
  studyFocusDoneCount?: number;
  studyFocusDoneWithEvidenceCount?: number;
  studyEvidenceRate?: number;
  studyTickOnlyStarPosts?: number;
  evidenceGateBlockedCount?: number;
  /** Family Mirror — reflective weekly view (opt-in parents only). */
  mirror: {
    child: {
      memberCount: number;
      totalCommitments: number;
      doneCount: number;
      completionRate?: number;
      starsEarned: number;
      bestStreakDays: number;
      members: Array<{
        memberId?: string;
        name: string;
        totalCommitments: number;
        doneCount: number;
        onTimeDoneCount: number;
        skippedCount: number;
        completionRate?: number;
        starsEarned: number;
        currentStreakDays: number;
      }>;
    };
    parent: {
      anyShared: boolean;
      sharedGoalCount: number;
      checkinDoneCount: number;
      checkinExpectedCount: number;
      checkinRate?: number;
      goals: Array<{
        goalId: string;
        memberId: string;
        memberName: string;
        title: string;
        emoji?: string;
        targetDaysPerWeek: number;
        doneDays: number;
        todayDone: boolean;
      }>;
    };
    household: {
      teamUnlocksConfirmed: number;
      starsEarned: number;
      reminderCount: number;
      remindersTracked: boolean;
    };
    reflections: string[];
    challenge?: {
      challengeId: string;
      title: string;
      status: string;
      rewardLabel: string;
      legsComplete: number;
      legsTotal: number;
    };
  };
}

function mapWeeklyInsight(data: Row): FamilyWeeklyInsight {
  const health = (data.health ?? data.Health ?? {}) as Row;
  const reminders = (data.reminders ?? data.Reminders ?? {}) as Row;
  return {
    familyId: String(data.familyId ?? data.FamilyId ?? ''),
    periodStart: String(data.periodStart ?? data.PeriodStart ?? ''),
    periodEnd: String(data.periodEnd ?? data.PeriodEnd ?? ''),
    days: Number(data.days ?? data.Days ?? 7),
    dataDays: Number(data.dataDays ?? data.DataDays ?? 0),
    isPartial: Boolean(data.isPartial ?? data.IsPartial),
    note: (data.note ?? data.Note) != null ? String(data.note ?? data.Note) : undefined,
    totalCommitments: Number(data.totalCommitments ?? data.TotalCommitments ?? 0),
    doneCount: Number(data.doneCount ?? data.DoneCount ?? 0),
    onTimeDoneCount: Number(data.onTimeDoneCount ?? data.OnTimeDoneCount ?? 0),
    lateDoneCount: Number(data.lateDoneCount ?? data.LateDoneCount ?? 0),
    skippedCount: Number(data.skippedCount ?? data.SkippedCount ?? 0),
    pendingCount: Number(data.pendingCount ?? data.PendingCount ?? 0),
    completionRate:
      data.completionRate != null || data.CompletionRate != null
        ? Number(data.completionRate ?? data.CompletionRate)
        : undefined,
    onTimeRate:
      data.onTimeRate != null || data.OnTimeRate != null
        ? Number(data.onTimeRate ?? data.OnTimeRate)
        : undefined,
    starsEarned: Number(data.starsEarned ?? data.StarsEarned ?? 0),
    health: {
      score:
        health.score != null || health.Score != null
          ? Number(health.score ?? health.Score)
          : undefined,
      completion:
        health.completion != null || health.Completion != null
          ? Number(health.completion ?? health.Completion)
          : undefined,
      reminderCalm:
        health.reminderCalm != null || health.ReminderCalm != null
          ? Number(health.reminderCalm ?? health.ReminderCalm)
          : undefined,
      streak:
        health.streak != null || health.Streak != null
          ? Number(health.streak ?? health.Streak)
          : undefined,
      onTime:
        health.onTime != null || health.OnTime != null
          ? Number(health.onTime ?? health.OnTime)
          : undefined,
      label:
        (health.label ?? health.Label) != null
          ? String(health.label ?? health.Label)
          : undefined,
      promiseLine:
        (health.promiseLine ?? health.PromiseLine) != null
          ? String(health.promiseLine ?? health.PromiseLine)
          : undefined,
      parentProgress:
        health.parentProgress != null || health.ParentProgress != null
          ? Number(health.parentProgress ?? health.ParentProgress)
          : undefined,
    },
    reminders: {
      tracked: Boolean(reminders.tracked ?? reminders.Tracked),
      count: Number(reminders.count ?? reminders.Count ?? 0),
      previousCount: Number(reminders.previousCount ?? reminders.PreviousCount ?? 0),
      deltaPct:
        reminders.deltaPct != null || reminders.DeltaPct != null
          ? Number(reminders.deltaPct ?? reminders.DeltaPct)
          : undefined,
    },
    members: asArray(data.members ?? data.Members).map((m) => ({
      memberId: (m.memberId ?? m.MemberId) != null ? String(m.memberId ?? m.MemberId) : undefined,
      name: String(m.name ?? m.Name ?? 'Thành viên'),
      totalCommitments: Number(m.totalCommitments ?? m.TotalCommitments ?? 0),
      doneCount: Number(m.doneCount ?? m.DoneCount ?? 0),
      onTimeDoneCount: Number(m.onTimeDoneCount ?? m.OnTimeDoneCount ?? 0),
      skippedCount: Number(m.skippedCount ?? m.SkippedCount ?? 0),
      completionRate:
        m.completionRate != null || m.CompletionRate != null
          ? Number(m.completionRate ?? m.CompletionRate)
          : undefined,
      starsEarned: Number(m.starsEarned ?? m.StarsEarned ?? 0),
      currentStreakDays: Number(m.currentStreakDays ?? m.CurrentStreakDays ?? 0),
    })),
    habits: asArray(data.habits ?? data.Habits).map((h) => ({
      templateId:
        (h.templateId ?? h.TemplateId) != null ? String(h.templateId ?? h.TemplateId) : undefined,
      title: String(h.title ?? h.Title ?? ''),
      memberName:
        (h.memberName ?? h.MemberName) != null ? String(h.memberName ?? h.MemberName) : undefined,
      occurrences: Number(h.occurrences ?? h.Occurrences ?? 0),
      doneCount: Number(h.doneCount ?? h.DoneCount ?? 0),
      forgotCount: Number(h.forgotCount ?? h.ForgotCount ?? 0),
      doneRate:
        h.doneRate != null || h.DoneRate != null ? Number(h.doneRate ?? h.DoneRate) : undefined,
      previousDoneRate:
        h.previousDoneRate != null || h.PreviousDoneRate != null
          ? Number(h.previousDoneRate ?? h.PreviousDoneRate)
          : undefined,
      trend: String(h.trend ?? h.Trend ?? 'flat'),
    })),
    highlights: (() => {
      const raw = data.highlights ?? data.Highlights;
      return Array.isArray(raw) ? raw.map((x) => String(x)) : [];
    })(),
    studyFocusDoneCount: Number(
      data.studyFocusDoneCount ?? data.StudyFocusDoneCount ?? 0,
    ),
    studyFocusDoneWithEvidenceCount: Number(
      data.studyFocusDoneWithEvidenceCount ?? data.StudyFocusDoneWithEvidenceCount ?? 0,
    ),
    studyEvidenceRate:
      data.studyEvidenceRate != null || data.StudyEvidenceRate != null
        ? Number(data.studyEvidenceRate ?? data.StudyEvidenceRate)
        : undefined,
    studyTickOnlyStarPosts: Number(
      data.studyTickOnlyStarPosts ?? data.StudyTickOnlyStarPosts ?? 0,
    ),
    evidenceGateBlockedCount: Number(
      data.evidenceGateBlockedCount ?? data.EvidenceGateBlockedCount ?? 0,
    ),
    mirror: mapWeeklyMirror(data.mirror ?? data.Mirror),
  };
}

function mapWeeklyMirror(raw: unknown): FamilyWeeklyInsight['mirror'] {
  const m = (raw && typeof raw === 'object' ? raw : {}) as Row;
  const child = (m.child ?? m.Child ?? {}) as Row;
  const parent = (m.parent ?? m.Parent ?? {}) as Row;
  const household = (m.household ?? m.Household ?? {}) as Row;
  const reflectionsRaw = m.reflections ?? m.Reflections;
  return {
    child: {
      memberCount: Number(child.memberCount ?? child.MemberCount ?? 0),
      totalCommitments: Number(child.totalCommitments ?? child.TotalCommitments ?? 0),
      doneCount: Number(child.doneCount ?? child.DoneCount ?? 0),
      completionRate:
        child.completionRate != null || child.CompletionRate != null
          ? Number(child.completionRate ?? child.CompletionRate)
          : undefined,
      starsEarned: Number(child.starsEarned ?? child.StarsEarned ?? 0),
      bestStreakDays: Number(child.bestStreakDays ?? child.BestStreakDays ?? 0),
      members: asArray(child.members ?? child.Members).map((row) => ({
        memberId:
          (row.memberId ?? row.MemberId) != null
            ? String(row.memberId ?? row.MemberId)
            : undefined,
        name: String(row.name ?? row.Name ?? 'Thành viên'),
        totalCommitments: Number(row.totalCommitments ?? row.TotalCommitments ?? 0),
        doneCount: Number(row.doneCount ?? row.DoneCount ?? 0),
        onTimeDoneCount: Number(row.onTimeDoneCount ?? row.OnTimeDoneCount ?? 0),
        skippedCount: Number(row.skippedCount ?? row.SkippedCount ?? 0),
        completionRate:
          row.completionRate != null || row.CompletionRate != null
            ? Number(row.completionRate ?? row.CompletionRate)
            : undefined,
        starsEarned: Number(row.starsEarned ?? row.StarsEarned ?? 0),
        currentStreakDays: Number(row.currentStreakDays ?? row.CurrentStreakDays ?? 0),
      })),
    },
    parent: {
      anyShared: Boolean(parent.anyShared ?? parent.AnyShared),
      sharedGoalCount: Number(parent.sharedGoalCount ?? parent.SharedGoalCount ?? 0),
      checkinDoneCount: Number(parent.checkinDoneCount ?? parent.CheckinDoneCount ?? 0),
      checkinExpectedCount: Number(
        parent.checkinExpectedCount ?? parent.CheckinExpectedCount ?? 0,
      ),
      checkinRate:
        parent.checkinRate != null || parent.CheckinRate != null
          ? Number(parent.checkinRate ?? parent.CheckinRate)
          : undefined,
      goals: asArray(parent.goals ?? parent.Goals).map((g) => {
        const emoji = g.emoji ?? g.Emoji;
        return {
          goalId: String(g.goalId ?? g.GoalId ?? ''),
          memberId: String(g.memberId ?? g.MemberId ?? ''),
          memberName: String(g.memberName ?? g.MemberName ?? ''),
          title: String(g.title ?? g.Title ?? ''),
          emoji: emoji != null ? String(emoji) : undefined,
          targetDaysPerWeek: Number(g.targetDaysPerWeek ?? g.TargetDaysPerWeek ?? 5),
          doneDays: Number(g.doneDays ?? g.DoneDays ?? 0),
          todayDone: Boolean(g.todayDone ?? g.TodayDone),
        };
      }),
    },
    household: {
      teamUnlocksConfirmed: Number(
        household.teamUnlocksConfirmed ?? household.TeamUnlocksConfirmed ?? 0,
      ),
      starsEarned: Number(household.starsEarned ?? household.StarsEarned ?? 0),
      reminderCount: Number(household.reminderCount ?? household.ReminderCount ?? 0),
      remindersTracked: Boolean(household.remindersTracked ?? household.RemindersTracked),
    },
    reflections: Array.isArray(reflectionsRaw)
      ? reflectionsRaw.map((x) => String(x))
      : [],
    challenge: (() => {
      const c = (m.challenge ?? m.Challenge) as Row | undefined;
      if (!c || typeof c !== 'object') return undefined;
      const id = c.challengeId ?? c.ChallengeId;
      if (id == null) return undefined;
      return {
        challengeId: String(id),
        title: String(c.title ?? c.Title ?? ''),
        status: String(c.status ?? c.Status ?? ''),
        rewardLabel: String(c.rewardLabel ?? c.RewardLabel ?? 'Đêm xem phim'),
        legsComplete: Number(c.legsComplete ?? c.LegsComplete ?? 0),
        legsTotal: Number(c.legsTotal ?? c.LegsTotal ?? 0),
      };
    })(),
  };
}

export async function fetchWeeklyInsight(
  familyId: string,
  opts?: { asOf?: string; days?: number },
): Promise<FamilyWeeklyInsight> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/insight/weekly`, {
    params: {
      asOf: opts?.asOf ?? undefined,
      days: opts?.days ?? 7,
    },
  });
  return mapWeeklyInsight(data);
}

/** Parent Success P0c — Return on Parenting + Growth Report (server SoT). */
export interface ParentSuccessMetric {
  id: string;
  labelVi: string;
  beforeDisplay: string;
  afterDisplay: string;
  deltaLabelVi: string;
  positive: boolean;
  unit?: string;
}

export interface ParentSuccessRop {
  familyId: string;
  windowDays: number;
  periodStart: string;
  periodEnd: string;
  dataDays: number;
  isPartial: boolean;
  partialNoteVi?: string;
  generatedAt: string;
  growthScore: number | null;
  headlineVi: string;
  summaryVi: string;
  readyToRenewLineVi: string;
  metrics: ParentSuccessMetric[];
  growthBulletsVi: string[];
  outcomesVi: string[];
  minutesSavedEstimate: number;
  parentNudgesEarly: number;
  parentNudgesLate: number;
  selfStartsEarly: number;
  selfStartsLate: number;
  habitGraduations: number;
  qualityMoments: number;
  deepPlaybookVi?: string;
  deepActionsVi?: string[];
  hasAiPlusDeep?: boolean;
}

function mapParentSuccessRop(data: Row): ParentSuccessRop {
  const metricsRaw = asArray(data.metrics ?? data.Metrics);
  const bulletsRaw = data.growthBulletsVi ?? data.GrowthBulletsVi;
  const outcomesRaw = data.outcomesVi ?? data.OutcomesVi;
  const deepActionsRaw = data.deepActionsVi ?? data.DeepActionsVi;
  return {
    familyId: String(data.familyId ?? data.FamilyId ?? ''),
    windowDays: Number(data.windowDays ?? data.WindowDays ?? 30),
    periodStart: String(data.periodStart ?? data.PeriodStart ?? ''),
    periodEnd: String(data.periodEnd ?? data.PeriodEnd ?? ''),
    dataDays: Number(data.dataDays ?? data.DataDays ?? 0),
    isPartial: Boolean(data.isPartial ?? data.IsPartial ?? false),
    partialNoteVi:
      data.partialNoteVi != null || data.PartialNoteVi != null
        ? String(data.partialNoteVi ?? data.PartialNoteVi)
        : undefined,
    generatedAt: String(data.generatedAt ?? data.GeneratedAt ?? ''),
    growthScore:
      data.growthScore == null && data.GrowthScore == null
        ? null
        : Number(data.growthScore ?? data.GrowthScore),
    headlineVi: String(data.headlineVi ?? data.HeadlineVi ?? ''),
    summaryVi: String(data.summaryVi ?? data.SummaryVi ?? ''),
    readyToRenewLineVi: String(data.readyToRenewLineVi ?? data.ReadyToRenewLineVi ?? ''),
    metrics: metricsRaw.map((m) => ({
      id: String(m.id ?? m.Id ?? ''),
      labelVi: String(m.labelVi ?? m.LabelVi ?? ''),
      beforeDisplay: String(m.beforeDisplay ?? m.BeforeDisplay ?? ''),
      afterDisplay: String(m.afterDisplay ?? m.AfterDisplay ?? ''),
      deltaLabelVi: String(m.deltaLabelVi ?? m.DeltaLabelVi ?? ''),
      positive: Boolean(m.positive ?? m.Positive ?? false),
      unit:
        m.unit != null || m.Unit != null ? String(m.unit ?? m.Unit) : undefined,
    })),
    growthBulletsVi: Array.isArray(bulletsRaw)
      ? bulletsRaw.map((x) => String(x))
      : [],
    outcomesVi: Array.isArray(outcomesRaw) ? outcomesRaw.map((x) => String(x)) : [],
    minutesSavedEstimate: Number(
      data.minutesSavedEstimate ?? data.MinutesSavedEstimate ?? 0,
    ),
    parentNudgesEarly: Number(data.parentNudgesEarly ?? data.ParentNudgesEarly ?? 0),
    parentNudgesLate: Number(data.parentNudgesLate ?? data.ParentNudgesLate ?? 0),
    selfStartsEarly: Number(data.selfStartsEarly ?? data.SelfStartsEarly ?? 0),
    selfStartsLate: Number(data.selfStartsLate ?? data.SelfStartsLate ?? 0),
    habitGraduations: Number(data.habitGraduations ?? data.HabitGraduations ?? 0),
    qualityMoments: Number(data.qualityMoments ?? data.QualityMoments ?? 0),
    deepPlaybookVi:
      data.deepPlaybookVi != null || data.DeepPlaybookVi != null
        ? String(data.deepPlaybookVi ?? data.DeepPlaybookVi)
        : undefined,
    deepActionsVi: Array.isArray(deepActionsRaw)
      ? deepActionsRaw.map((x) => String(x))
      : undefined,
    hasAiPlusDeep: Boolean(data.hasAiPlusDeep ?? data.HasAiPlusDeep ?? false),
  };
}

export async function fetchParentSuccessRop(
  familyId: string,
  opts?: { days?: 30 | 60 | 90; asOf?: string },
): Promise<ParentSuccessRop> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/parent-success/rop`, {
    params: {
      days: opts?.days ?? 30,
      asOf: opts?.asOf ?? undefined,
    },
  });
  return mapParentSuccessRop(data);
}

export function formatParentSuccessRopShare(rop: ParentSuccessRop, familyName: string): string {
  const lines = [
    `ROP Famixa · ${rop.windowDays} ngày · ${familyName}`,
    rop.headlineVi,
    '',
    ...rop.metrics.map(
      (m) => `• ${m.labelVi}: ${m.beforeDisplay} → ${m.afterDisplay} (${m.deltaLabelVi})`,
    ),
    '',
    rop.summaryVi,
    rop.readyToRenewLineVi,
  ];
  return lines.join('\n');
}

/** Parent Success P1 — AI Wins digest (Memory SoT, templated). */
export interface FamilyAiWin {
  id: string;
  kind: string;
  titleVi: string;
  noteVi?: string;
  flowDate: string;
  icon?: string;
  isFavorite: boolean;
  happenedAt: string;
}

export interface FamilyAiWinsDigest {
  from: string;
  to: string;
  totalCount: number;
  headlineVi: string;
  subheadVi: string;
  wins: FamilyAiWin[];
}

/** Parent Success P1 — monthly AI Letter (templated, no LLM). */
export interface FamilyAiLetter {
  familyId: string;
  familyName: string;
  periodStart: string;
  periodEnd: string;
  generatedAt: string;
  monthLabelVi: string;
  greetingVi: string;
  bodyVi: string;
  highlightsVi: string[];
  closingVi: string;
  isThinData: boolean;
  deepHighlightsVi?: string[];
  hasAiPlusDeep?: boolean;
}

function mapFamilyAiWin(r: Row): FamilyAiWin {
  return {
    id: String(r.id ?? r.Id ?? ''),
    kind: String(r.kind ?? r.Kind ?? ''),
    titleVi: String(r.titleVi ?? r.TitleVi ?? ''),
    noteVi: r.noteVi != null || r.NoteVi != null ? String(r.noteVi ?? r.NoteVi) : undefined,
    flowDate: String(r.flowDate ?? r.FlowDate ?? ''),
    icon: r.icon != null || r.Icon != null ? String(r.icon ?? r.Icon) : undefined,
    isFavorite: Boolean(r.isFavorite ?? r.IsFavorite ?? false),
    happenedAt: String(r.happenedAt ?? r.HappenedAt ?? ''),
  };
}

export async function fetchFamilyAiWinsDigest(
  familyId: string,
  opts?: { from?: string; to?: string; limit?: number },
): Promise<FamilyAiWinsDigest> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/ai/wins-digest`, {
    params: {
      from: opts?.from,
      to: opts?.to,
      limit: opts?.limit ?? 10,
    },
  });
  const r = (data ?? {}) as Row;
  return {
    from: String(r.from ?? r.From ?? ''),
    to: String(r.to ?? r.To ?? ''),
    totalCount: Number(r.totalCount ?? r.TotalCount ?? 0),
    headlineVi: String(r.headlineVi ?? r.HeadlineVi ?? ''),
    subheadVi: String(r.subheadVi ?? r.SubheadVi ?? ''),
    wins: asArray(r.wins ?? r.Wins).map((x) => mapFamilyAiWin(x as Row)),
  };
}

export async function fetchFamilyAiLetter(
  familyId: string,
  opts?: { month?: string },
): Promise<FamilyAiLetter> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/ai/letter`, {
    params: { month: opts?.month },
  });
  const r = (data ?? {}) as Row;
  return {
    familyId: String(r.familyId ?? r.FamilyId ?? familyId),
    familyName: String(r.familyName ?? r.FamilyName ?? ''),
    periodStart: String(r.periodStart ?? r.PeriodStart ?? ''),
    periodEnd: String(r.periodEnd ?? r.PeriodEnd ?? ''),
    generatedAt: String(r.generatedAt ?? r.GeneratedAt ?? ''),
    monthLabelVi: String(r.monthLabelVi ?? r.MonthLabelVi ?? ''),
    greetingVi: String(r.greetingVi ?? r.GreetingVi ?? ''),
    bodyVi: String(r.bodyVi ?? r.BodyVi ?? ''),
    highlightsVi: asArray(r.highlightsVi ?? r.HighlightsVi).map((x) => String(x)),
    closingVi: String(r.closingVi ?? r.ClosingVi ?? ''),
    isThinData: Boolean(r.isThinData ?? r.IsThinData ?? false),
    deepHighlightsVi: asArray(r.deepHighlightsVi ?? r.DeepHighlightsVi).map((x) => String(x)),
    hasAiPlusDeep: Boolean(r.hasAiPlusDeep ?? r.HasAiPlusDeep ?? false),
  };
}

export function formatFamilyAiLetterShare(letter: FamilyAiLetter): string {
  return [
    letter.greetingVi,
    '',
    letter.bodyVi,
    '',
    ...letter.highlightsVi.map((h) => `• ${h}`),
    '',
    letter.closingVi,
  ].join('\n');
}

/** Family Replay chữ — EOM narrative (Memory + ROP, no video). */
export interface FamilyReplayScene {
  date?: string;
  icon: string;
  titleVi: string;
  detailVi?: string;
  kind: string;
}

export interface FamilyReplay {
  familyId: string;
  familyName: string;
  periodStart: string;
  periodEnd: string;
  generatedAt: string;
  monthLabelVi: string;
  titleVi: string;
  openingVi: string;
  scenes: FamilyReplayScene[];
  closingVi: string;
  shareTextVi: string;
  isThinData: boolean;
  hasAiPlusDeep?: boolean;
}

export async function fetchFamilyReplay(
  familyId: string,
  opts?: { month?: string },
): Promise<FamilyReplay> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/ai/replay`, {
    params: { month: opts?.month },
  });
  const r = (data ?? {}) as Row;
  return {
    familyId: String(r.familyId ?? r.FamilyId ?? familyId),
    familyName: String(r.familyName ?? r.FamilyName ?? ''),
    periodStart: String(r.periodStart ?? r.PeriodStart ?? ''),
    periodEnd: String(r.periodEnd ?? r.PeriodEnd ?? ''),
    generatedAt: String(r.generatedAt ?? r.GeneratedAt ?? ''),
    monthLabelVi: String(r.monthLabelVi ?? r.MonthLabelVi ?? ''),
    titleVi: String(r.titleVi ?? r.TitleVi ?? ''),
    openingVi: String(r.openingVi ?? r.OpeningVi ?? ''),
    scenes: asArray(r.scenes ?? r.Scenes).map((x) => {
      const row = x as Row;
      return {
        date:
          row.date != null || row.Date != null ? String(row.date ?? row.Date) : undefined,
        icon: String(row.icon ?? row.Icon ?? '✨'),
        titleVi: String(row.titleVi ?? row.TitleVi ?? ''),
        detailVi:
          row.detailVi != null || row.DetailVi != null
            ? String(row.detailVi ?? row.DetailVi)
            : undefined,
        kind: String(row.kind ?? row.Kind ?? ''),
      };
    }),
    closingVi: String(r.closingVi ?? r.ClosingVi ?? ''),
    shareTextVi: String(r.shareTextVi ?? r.ShareTextVi ?? ''),
    isThinData: Boolean(r.isThinData ?? r.IsThinData ?? false),
    hasAiPlusDeep: Boolean(r.hasAiPlusDeep ?? r.HasAiPlusDeep ?? false),
  };
}

/** Parent Success P2 — evening 3Q check-in. */
export interface ParentSuccessCheckin {
  id: string;
  familyId: string;
  memberId: string;
  flowDate: string;
  qLessNudge: boolean;
  qLessTension: boolean;
  qQualityTime: boolean;
  note?: string;
  updatedAt: string;
  reflectionVi: string;
}

export interface ParentAchievement {
  code: string;
  titleVi: string;
  detailVi: string;
  icon: string;
  unlocked: boolean;
  progressHintVi: string;
}

export interface ParentAchievements {
  familyId: string;
  asOf: string;
  headlineVi: string;
  items: ParentAchievement[];
}

function mapParentSuccessCheckin(data: Row): ParentSuccessCheckin {
  return {
    id: String(data.id ?? data.Id ?? ''),
    familyId: String(data.familyId ?? data.FamilyId ?? ''),
    memberId: String(data.memberId ?? data.MemberId ?? ''),
    flowDate: String(data.flowDate ?? data.FlowDate ?? ''),
    qLessNudge: Boolean(data.qLessNudge ?? data.QLessNudge ?? false),
    qLessTension: Boolean(data.qLessTension ?? data.QLessTension ?? false),
    qQualityTime: Boolean(data.qQualityTime ?? data.QQualityTime ?? false),
    note: data.note != null || data.Note != null ? String(data.note ?? data.Note) : undefined,
    updatedAt: String(data.updatedAt ?? data.UpdatedAt ?? ''),
    reflectionVi: String(data.reflectionVi ?? data.ReflectionVi ?? ''),
  };
}

export async function fetchParentSuccessEveningCheckin(
  familyId: string,
  memberId: string,
  date?: string,
): Promise<ParentSuccessCheckin | null> {
  try {
    const { data, status } = await http.get<Row | null>(
      `/family-os/families/${familyId}/parent-success/evening-checkin`,
      {
        params: { memberId, date },
        validateStatus: (s) => (s >= 200 && s < 300) || s === 204,
      },
    );
    if (status === 204 || data == null) return null;
    return mapParentSuccessCheckin(data);
  } catch {
    return null;
  }
}

export async function upsertParentSuccessEveningCheckin(
  familyId: string,
  input: {
    memberId: string;
    flowDate?: string;
    qLessNudge: boolean;
    qLessTension: boolean;
    qQualityTime: boolean;
    note?: string;
  },
): Promise<ParentSuccessCheckin> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/parent-success/evening-checkin`,
    {
      memberId: input.memberId,
      flowDate: input.flowDate ?? null,
      qLessNudge: input.qLessNudge,
      qLessTension: input.qLessTension,
      qQualityTime: input.qQualityTime,
      note: input.note ?? null,
    },
  );
  return mapParentSuccessCheckin(data);
}

export async function fetchParentAchievements(
  familyId: string,
  opts?: { asOf?: string },
): Promise<ParentAchievements> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/parent-success/achievements`, {
    params: { asOf: opts?.asOf },
  });
  const r = (data ?? {}) as Row;
  return {
    familyId: String(r.familyId ?? r.FamilyId ?? familyId),
    asOf: String(r.asOf ?? r.AsOf ?? ''),
    headlineVi: String(r.headlineVi ?? r.HeadlineVi ?? ''),
    items: asArray(r.items ?? r.Items).map((x) => {
      const row = x as Row;
      return {
        code: String(row.code ?? row.Code ?? ''),
        titleVi: String(row.titleVi ?? row.TitleVi ?? ''),
        detailVi: String(row.detailVi ?? row.DetailVi ?? ''),
        icon: String(row.icon ?? row.Icon ?? '✨'),
        unlocked: Boolean(row.unlocked ?? row.Unlocked ?? false),
        progressHintVi: String(row.progressHintVi ?? row.ProgressHintVi ?? ''),
      };
    }),
  };
}

/** Parent Success P3 — Trust Flywheel tip acted. */
export interface ParentCoachActed {
  familyId: string;
  memberId: string;
  flowDate: string;
  tipId: string;
  alreadyActed: boolean;
  messageVi: string;
  actedTipIdsToday: string[];
}

function mapParentCoachActed(data: Row): ParentCoachActed {
  return {
    familyId: String(data.familyId ?? data.FamilyId ?? ''),
    memberId: String(data.memberId ?? data.MemberId ?? ''),
    flowDate: String(data.flowDate ?? data.FlowDate ?? ''),
    tipId: String(data.tipId ?? data.TipId ?? ''),
    alreadyActed: Boolean(data.alreadyActed ?? data.AlreadyActed ?? false),
    messageVi: String(data.messageVi ?? data.MessageVi ?? ''),
    actedTipIdsToday: asArray(data.actedTipIdsToday ?? data.ActedTipIdsToday).map((x) =>
      String(x),
    ),
  };
}

export async function fetchParentCoachActedToday(
  familyId: string,
  memberId: string,
  date?: string,
): Promise<ParentCoachActed> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/parent-success/coach-acted`, {
    params: { memberId, date },
  });
  return mapParentCoachActed(data ?? {});
}

export async function recordParentCoachActed(
  familyId: string,
  input: {
    memberId: string;
    tipId: string;
    tipSource?: string;
    slot?: string;
    titleVi?: string;
    flowDate?: string;
  },
): Promise<ParentCoachActed> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/parent-success/coach-acted`, {
    memberId: input.memberId,
    tipId: input.tipId,
    tipSource: input.tipSource ?? null,
    slot: input.slot ?? null,
    titleVi: input.titleVi ?? null,
    flowDate: input.flowDate ?? null,
  });
  return mapParentCoachActed(data ?? {});
}

/** Parent Progress (opt-in) — a guardian/caregiver's own light habit goals. */
export interface ParentGoal {
  id: string;
  familyId: string;
  memberId: string;
  memberName: string;
  title: string;
  emoji?: string;
  targetDaysPerWeek: number;
  shareWithFamily: boolean;
  isActive: boolean;
  sortOrder: number;
  todayStatus?: 'done' | 'skip' | null;
  weekDoneCount: number;
  currentStreak: number;
}

export interface SharedParentProgress {
  memberId: string;
  memberName: string;
  goalId: string;
  title: string;
  emoji?: string;
  targetDaysPerWeek: number;
  todayDone: boolean;
  weekDoneCount: number;
}

function mapParentGoal(r: Row): ParentGoal {
  const today = r.todayStatus ?? r.TodayStatus;
  const emoji = r.emoji ?? r.Emoji;
  return {
    id: String(r.id ?? r.Id ?? ''),
    familyId: String(r.familyId ?? r.FamilyId ?? ''),
    memberId: String(r.memberId ?? r.MemberId ?? ''),
    memberName: String(r.memberName ?? r.MemberName ?? ''),
    title: String(r.title ?? r.Title ?? ''),
    emoji: emoji != null ? String(emoji) : undefined,
    targetDaysPerWeek: Number(r.targetDaysPerWeek ?? r.TargetDaysPerWeek ?? 5),
    shareWithFamily: Boolean(r.shareWithFamily ?? r.ShareWithFamily ?? false),
    isActive: Boolean(r.isActive ?? r.IsActive ?? true),
    sortOrder: Number(r.sortOrder ?? r.SortOrder ?? 0),
    todayStatus: today != null ? (String(today) as 'done' | 'skip') : null,
    weekDoneCount: Number(r.weekDoneCount ?? r.WeekDoneCount ?? 0),
    currentStreak: Number(r.currentStreak ?? r.CurrentStreak ?? 0),
  };
}

function mapSharedParentProgress(r: Row): SharedParentProgress {
  const emoji = r.emoji ?? r.Emoji;
  return {
    memberId: String(r.memberId ?? r.MemberId ?? ''),
    memberName: String(r.memberName ?? r.MemberName ?? ''),
    goalId: String(r.goalId ?? r.GoalId ?? ''),
    title: String(r.title ?? r.Title ?? ''),
    emoji: emoji != null ? String(emoji) : undefined,
    targetDaysPerWeek: Number(r.targetDaysPerWeek ?? r.TargetDaysPerWeek ?? 5),
    todayDone: Boolean(r.todayDone ?? r.TodayDone ?? false),
    weekDoneCount: Number(r.weekDoneCount ?? r.WeekDoneCount ?? 0),
  };
}

export async function fetchParentGoals(
  familyId: string,
  memberId: string,
): Promise<ParentGoal[]> {
  const { data } = await http.get<unknown>(
    `/family-os/families/${familyId}/members/${memberId}/parent-goals`,
  );
  return asArray(data).map((r) => mapParentGoal(r));
}

export async function createParentGoal(
  familyId: string,
  input: {
    memberId: string;
    title: string;
    emoji?: string;
    targetDaysPerWeek?: number;
    shareWithFamily?: boolean;
  },
): Promise<ParentGoal> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/parent-goals`, {
    memberId: input.memberId,
    title: input.title,
    emoji: input.emoji ?? null,
    targetDaysPerWeek: input.targetDaysPerWeek ?? null,
    shareWithFamily: input.shareWithFamily ?? null,
  });
  return mapParentGoal(data);
}

export async function updateParentGoal(
  familyId: string,
  goalId: string,
  input: {
    title?: string;
    emoji?: string;
    targetDaysPerWeek?: number;
    shareWithFamily?: boolean;
    isActive?: boolean;
  },
): Promise<ParentGoal> {
  const { data } = await http.patch<Row>(
    `/family-os/families/${familyId}/parent-goals/${goalId}`,
    input,
  );
  return mapParentGoal(data);
}

export async function deleteParentGoal(familyId: string, goalId: string): Promise<void> {
  await http.delete(`/family-os/families/${familyId}/parent-goals/${goalId}`);
}

export async function checkinParentGoal(
  familyId: string,
  goalId: string,
  status: 'done' | 'skip' | 'clear',
  opts?: { date?: string; note?: string },
): Promise<ParentGoal> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/parent-goals/${goalId}/checkin`,
    { status, date: opts?.date ?? null, note: opts?.note ?? null },
  );
  return mapParentGoal(data);
}

export async function fetchSharedParentProgress(
  familyId: string,
): Promise<SharedParentProgress[]> {
  const { data } = await http.get<unknown>(
    `/family-os/families/${familyId}/parent-goals/shared`,
  );
  return asArray(data).map((r) => mapSharedParentProgress(r));
}

/** Weekly whole-family challenge (P0.3). */
export interface FamilyChallengeLeg {
  id: string;
  memberId?: string;
  memberName?: string;
  legKind: 'parent' | 'child' | 'household' | string;
  title: string;
  emoji?: string;
  targetDays: number;
  doneDays: number;
  todayDone: boolean;
  isComplete: boolean;
  sortOrder: number;
}

export interface FamilyChallenge {
  id: string;
  familyId: string;
  weekStart: string;
  weekEnd: string;
  status: string;
  title: string;
  rewardCode: string;
  rewardLabel: string;
  acceptedBy?: string;
  completedAt?: string;
  unlockId?: string;
  legsComplete: number;
  legsTotal: number;
  legs: FamilyChallengeLeg[];
}

function mapChallengeLeg(r: Row): FamilyChallengeLeg {
  const emoji = r.emoji ?? r.Emoji;
  const memberId = r.memberId ?? r.MemberId;
  const memberName = r.memberName ?? r.MemberName;
  return {
    id: String(r.id ?? r.Id ?? ''),
    memberId: memberId != null ? String(memberId) : undefined,
    memberName: memberName != null ? String(memberName) : undefined,
    legKind: String(r.legKind ?? r.LegKind ?? ''),
    title: String(r.title ?? r.Title ?? ''),
    emoji: emoji != null ? String(emoji) : undefined,
    targetDays: Number(r.targetDays ?? r.TargetDays ?? 5),
    doneDays: Number(r.doneDays ?? r.DoneDays ?? 0),
    todayDone: Boolean(r.todayDone ?? r.TodayDone),
    isComplete: Boolean(r.isComplete ?? r.IsComplete),
    sortOrder: Number(r.sortOrder ?? r.SortOrder ?? 0),
  };
}

function mapChallenge(r: Row): FamilyChallenge {
  const acceptedBy = r.acceptedBy ?? r.AcceptedBy;
  const completedAt = r.completedAt ?? r.CompletedAt;
  const unlockId = r.unlockId ?? r.UnlockId;
  return {
    id: String(r.id ?? r.Id ?? ''),
    familyId: String(r.familyId ?? r.FamilyId ?? ''),
    weekStart: String(r.weekStart ?? r.WeekStart ?? ''),
    weekEnd: String(r.weekEnd ?? r.WeekEnd ?? ''),
    status: String(r.status ?? r.Status ?? ''),
    title: String(r.title ?? r.Title ?? ''),
    rewardCode: String(r.rewardCode ?? r.RewardCode ?? ''),
    rewardLabel: String(r.rewardLabel ?? r.RewardLabel ?? 'Đêm xem phim'),
    acceptedBy: acceptedBy != null ? String(acceptedBy) : undefined,
    completedAt: completedAt != null ? String(completedAt) : undefined,
    unlockId: unlockId != null ? String(unlockId) : undefined,
    legsComplete: Number(r.legsComplete ?? r.LegsComplete ?? 0),
    legsTotal: Number(r.legsTotal ?? r.LegsTotal ?? 0),
    legs: asArray(r.legs ?? r.Legs).map(mapChallengeLeg),
  };
}

export async function fetchCurrentChallenge(
  familyId: string,
): Promise<FamilyChallenge | null> {
  try {
    const { data, status } = await http.get<Row | ''>(
      `/family-os/families/${familyId}/challenges/current`,
      { validateStatus: (s) => (s >= 200 && s < 300) || s === 204 },
    );
    if (status === 204 || !data) return null;
    return mapChallenge(data as Row);
  } catch (err: unknown) {
    const status = (err as { response?: { status?: number } })?.response?.status;
    if (status === 204) return null;
    throw err;
  }
}

export async function acceptFamilyChallenge(
  familyId: string,
  acceptedBy: string,
): Promise<FamilyChallenge> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/challenges/accept`, {
    acceptedBy,
  });
  return mapChallenge(data);
}

export async function checkinChallengeLeg(
  familyId: string,
  legId: string,
  actorMemberId: string,
  status: 'done' | 'skip' | 'clear',
): Promise<FamilyChallenge> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/challenges/legs/${legId}/checkin`,
    { actorMemberId, status },
  );
  return mapChallenge(data);
}

// ─── Adaptive Family Engine (AFE) ────────────────────────────────────────────

export type DecisionKind =
  | 'awaiting_stars'
  | 'consequence_confirm'
  | 'team_unlock'
  | 'reward_fulfill'
  | 'child_request'
  | 'ai_proposal';

export interface DecisionItem {
  kind: DecisionKind;
  id: string;
  titleVi: string;
  bodyVi: string;
  recommend?: string;
  memberId?: string;
  memberName?: string;
  createdAt: string;
  refType?: string;
  refId?: string;
}

export interface DecisionInbox {
  totalCount: number;
  headlineVi: string;
  items: DecisionItem[];
}

export interface ChildRequest {
  id: string;
  familyId: string;
  memberId: string;
  memberName: string;
  flowDate: string;
  kind: string;
  amountMinutes?: number;
  titleVi?: string;
  windowStart?: string;
  windowEnd?: string;
  reasonCodes: string[];
  reasonNote?: string;
  status: string;
  aiSummaryVi?: string;
  aiRecommend?: string;
  grantedMinutes?: number;
  createdAt: string;
  decidedAt?: string;
}

export interface ScreenWallet {
  id: string;
  familyId: string;
  memberId: string;
  memberName: string;
  isoYear: number;
  isoWeek: number;
  budgetMinutes: number;
  spentMinutes: number;
  earnedMinutes: number;
  grantedMinutes: number;
  remainingMinutes: number;
  status: string;
}

export interface FamilyScore {
  score: number;
  band: string;
  headlineVi: string;
  allowBonusMinutes: boolean;
  beautifulDays: number;
  bestStreak: number;
  routinePct: number;
  challengeActive: boolean;
}

export interface FamilyModeResult {
  mode: string;
  labelVi: string;
  messageVi: string;
  primaryRoutineId?: string;
  primaryRoutineName?: string;
  primaryTemplateCount?: number;
}

export const CHILD_REQUEST_REASONS = [
  { value: 'no_extra_class', label: 'Hôm nay không có học thêm' },
  { value: 'chores_done', label: 'Đã hoàn thành việc nhà' },
  { value: 'homework_done', label: 'Đã học xong' },
  { value: 'play_with_friend', label: 'Muốn chơi cùng bạn' },
  { value: 'other', label: 'Lý do khác' },
] as const;

export const FAMILY_MODE_OPTIONS = [
  { value: 'normal', label: 'Bình thường', hint: 'Theo lịch năm học' },
  {
    value: 'summer',
    label: 'Nghỉ hè',
    hint: 'Nhịp nhẹ · ưu tiên rủ chơi chung',
  },
  {
    value: 'exam',
    label: 'Thi học kỳ',
    hint: 'Ưu tiên ôm / động viên — bớt áp lực',
  },
  { value: 'travel', label: 'Du lịch', hint: 'Tạm đổi routine · lời ấm' },
  { value: 'weekend', label: 'Cuối tuần', hint: 'T7–CN' },
  {
    value: 'holiday',
    label: 'Nghỉ lễ',
    hint: 'Vài ngày · gợi ý chơi chung',
  },
] as const;

function mapDecisionItem(r: Row): DecisionItem {
  const memberId = r.memberId ?? r.MemberId;
  const recommend = r.recommend ?? r.Recommend;
  const refType = r.refType ?? r.RefType;
  const refId = r.refId ?? r.RefId;
  return {
    kind: String(r.kind ?? r.Kind ?? '') as DecisionKind,
    id: String(r.id ?? r.Id ?? ''),
    titleVi: String(r.titleVi ?? r.TitleVi ?? ''),
    bodyVi: String(r.bodyVi ?? r.BodyVi ?? ''),
    recommend: recommend != null ? String(recommend) : undefined,
    memberId: memberId != null ? String(memberId) : undefined,
    memberName: r.memberName != null || r.MemberName != null
      ? String(r.memberName ?? r.MemberName)
      : undefined,
    createdAt: String(r.createdAt ?? r.CreatedAt ?? ''),
    refType: refType != null ? String(refType) : undefined,
    refId: refId != null ? String(refId) : undefined,
  };
}

function mapDecisionInbox(r: Row): DecisionInbox {
  return {
    totalCount: Number(r.totalCount ?? r.TotalCount ?? 0),
    headlineVi: String(r.headlineVi ?? r.HeadlineVi ?? ''),
    items: asArray(r.items ?? r.Items).map(mapDecisionItem),
  };
}

function mapChildRequest(r: Row): ChildRequest {
  const codes = r.reasonCodes ?? r.ReasonCodes;
  const amount = r.amountMinutes ?? r.AmountMinutes;
  return {
    id: String(r.id ?? r.Id ?? ''),
    familyId: String(r.familyId ?? r.FamilyId ?? ''),
    memberId: String(r.memberId ?? r.MemberId ?? ''),
    memberName: String(r.memberName ?? r.MemberName ?? ''),
    flowDate: String(r.flowDate ?? r.FlowDate ?? ''),
    kind: String(r.kind ?? r.Kind ?? ''),
    amountMinutes: amount != null ? Number(amount) : undefined,
    titleVi: r.titleVi != null || r.TitleVi != null ? String(r.titleVi ?? r.TitleVi) : undefined,
    windowStart:
      r.windowStart != null || r.WindowStart != null
        ? String(r.windowStart ?? r.WindowStart).slice(0, 5)
        : undefined,
    windowEnd:
      r.windowEnd != null || r.WindowEnd != null
        ? String(r.windowEnd ?? r.WindowEnd).slice(0, 5)
        : undefined,
    reasonCodes: Array.isArray(codes) ? codes.map(String) : [],
    reasonNote: r.reasonNote != null || r.ReasonNote != null
      ? String(r.reasonNote ?? r.ReasonNote)
      : undefined,
    status: String(r.status ?? r.Status ?? ''),
    aiSummaryVi: r.aiSummaryVi != null || r.AiSummaryVi != null
      ? String(r.aiSummaryVi ?? r.AiSummaryVi)
      : undefined,
    aiRecommend: r.aiRecommend != null || r.AiRecommend != null
      ? String(r.aiRecommend ?? r.AiRecommend)
      : undefined,
    grantedMinutes: r.grantedMinutes != null || r.GrantedMinutes != null
      ? Number(r.grantedMinutes ?? r.GrantedMinutes)
      : undefined,
    createdAt: String(r.createdAt ?? r.CreatedAt ?? ''),
    decidedAt: r.decidedAt != null || r.DecidedAt != null
      ? String(r.decidedAt ?? r.DecidedAt)
      : undefined,
  };
}

function mapScreenWallet(r: Row): ScreenWallet {
  return {
    id: String(r.id ?? r.Id ?? ''),
    familyId: String(r.familyId ?? r.FamilyId ?? ''),
    memberId: String(r.memberId ?? r.MemberId ?? ''),
    memberName: String(r.memberName ?? r.MemberName ?? ''),
    isoYear: Number(r.isoYear ?? r.IsoYear ?? 0),
    isoWeek: Number(r.isoWeek ?? r.IsoWeek ?? 0),
    budgetMinutes: Number(r.budgetMinutes ?? r.BudgetMinutes ?? 0),
    spentMinutes: Number(r.spentMinutes ?? r.SpentMinutes ?? 0),
    earnedMinutes: Number(r.earnedMinutes ?? r.EarnedMinutes ?? 0),
    grantedMinutes: Number(r.grantedMinutes ?? r.GrantedMinutes ?? 0),
    remainingMinutes: Number(r.remainingMinutes ?? r.RemainingMinutes ?? 0),
    status: String(r.status ?? r.Status ?? ''),
  };
}

function mapFamilyScore(r: Row): FamilyScore {
  return {
    score: Number(r.score ?? r.Score ?? 0),
    band: String(r.band ?? r.Band ?? ''),
    headlineVi: String(r.headlineVi ?? r.HeadlineVi ?? ''),
    allowBonusMinutes: Boolean(r.allowBonusMinutes ?? r.AllowBonusMinutes ?? false),
    beautifulDays: Number(r.beautifulDays ?? r.BeautifulDays ?? 0),
    bestStreak: Number(r.bestStreak ?? r.BestStreak ?? 0),
    routinePct: Number(r.routinePct ?? r.RoutinePct ?? 0),
    challengeActive: Boolean(r.challengeActive ?? r.ChallengeActive ?? false),
  };
}

export async function fetchDecisionInbox(familyId: string): Promise<DecisionInbox> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/decision-inbox`);
  return mapDecisionInbox(data);
}

export async function createChildRequest(
  familyId: string,
  body: {
    memberId: string;
    amountMinutes?: number;
    reasonCodes?: string[];
    reasonNote?: string;
    flowDate?: string;
    kind?: string;
    titleVi?: string;
    windowStart?: string;
    windowEnd?: string;
  },
): Promise<ChildRequest> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/requests`, body);
  return mapChildRequest(data);
}

export async function fetchChildRequests(
  familyId: string,
  opts?: { status?: string; memberId?: string },
): Promise<ChildRequest[]> {
  const { data } = await http.get<unknown>(`/family-os/families/${familyId}/requests`, {
    params: {
      status: opts?.status,
      memberId: opts?.memberId,
    },
  });
  return asArray(data).map(mapChildRequest);
}

export async function addAdHocCommitment(
  familyId: string,
  body: {
    memberId?: string;
    title: string;
    description?: string;
    flowDate?: string;
    windowStart?: string;
    windowEnd?: string;
    expectedDurationMinutes?: number;
  },
): Promise<DayFlowCommitment> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/commitments/ad-hoc`,
    body,
  );
  return mapCommitment(data);
}

export async function addFamilyMember(
  familyId: string,
  body: {
    displayName: string;
    roleCode: string;
    dateOfBirth?: string;
    sortOrder?: number;
  },
): Promise<FamilyMembership> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/members`, body);
  return {
    id: String(data.id ?? data.Id ?? ''),
    displayName: String(data.displayName ?? data.DisplayName ?? ''),
    roleCode: String(data.roleCode ?? data.RoleCode ?? ''),
    dateOfBirth:
      data.dateOfBirth != null || data.DateOfBirth != null
        ? String(data.dateOfBirth ?? data.DateOfBirth)
        : undefined,
  };
}

export async function updateFamilyMember(
  familyId: string,
  memberId: string,
  body: {
    displayName?: string;
    roleCode?: string;
    dateOfBirth?: string;
    status?: string;
  },
): Promise<FamilyMembership> {
  const { data } = await http.patch<Row>(
    `/family-os/families/${familyId}/members/${memberId}`,
    body,
  );
  return {
    id: String(data.id ?? data.Id ?? memberId),
    displayName: String(data.displayName ?? data.DisplayName ?? ''),
    roleCode: String(data.roleCode ?? data.RoleCode ?? ''),
    dateOfBirth:
      data.dateOfBirth != null || data.DateOfBirth != null
        ? String(data.dateOfBirth ?? data.DateOfBirth)
        : undefined,
  };
}

export async function decideChildRequest(
  familyId: string,
  requestId: string,
  body: {
    decidedByMemberId: string;
    decision: 'approve' | 'reject' | 'partial';
    grantedMinutes?: number;
    note?: string;
  },
): Promise<ChildRequest> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/requests/${requestId}/decide`,
    body,
  );
  return mapChildRequest(data);
}

export async function decideAiProposal(
  familyId: string,
  proposalId: string,
  body: { decidedByMemberId: string; decision: 'approve' | 'reject' },
): Promise<void> {
  await http.post(`/family-os/families/${familyId}/ai-proposals/${proposalId}/decide`, body);
}

export async function scanAdaptiveProposals(familyId: string): Promise<number> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/ai-proposals/scan`, {});
  return Number(data.created ?? data.Created ?? 0);
}

export async function activateFamilyMode(
  familyId: string,
  body: {
    mode: string;
    startDate?: string;
    endDate?: string;
    activatedByMemberId?: string;
    confirmNow?: boolean;
  },
): Promise<FamilyModeResult> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/family-modes/activate`,
    body,
  );
  const routineId = data.primaryRoutineId ?? data.PrimaryRoutineId;
  const routineName = data.primaryRoutineName ?? data.PrimaryRoutineName;
  const templateCount = data.primaryTemplateCount ?? data.PrimaryTemplateCount;
  return {
    mode: String(data.mode ?? data.Mode ?? ''),
    labelVi: String(data.labelVi ?? data.LabelVi ?? ''),
    messageVi: String(data.messageVi ?? data.MessageVi ?? ''),
    primaryRoutineId: routineId != null ? String(routineId) : undefined,
    primaryRoutineName: routineName != null ? String(routineName) : undefined,
    primaryTemplateCount:
      templateCount != null && Number.isFinite(Number(templateCount))
        ? Number(templateCount)
        : undefined,
  };
}

export async function fetchScreenWallet(familyId: string): Promise<ScreenWallet[]> {
  const { data } = await http.get<unknown>(`/family-os/families/${familyId}/screen-wallet`);
  return asArray(data).map(mapScreenWallet);
}

export async function proposeScreenWallet(
  familyId: string,
  body: { memberId: string; budgetMinutes?: number; proposedByMemberId?: string },
): Promise<ScreenWallet> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/screen-wallet/propose`,
    body,
  );
  return mapScreenWallet(data);
}

export async function spendScreenWallet(
  familyId: string,
  body: { memberId: string; minutes: number; flowDate?: string; note?: string },
): Promise<ScreenWallet> {
  const { data } = await http.post<Row>(
    `/family-os/families/${familyId}/screen-wallet/spend`,
    body,
  );
  return mapScreenWallet(data);
}

export async function fetchFamilyScore(familyId: string): Promise<FamilyScore> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/family-score`);
  return mapFamilyScore(data);
}

/** Daily Digital Mirror M1 — agent heartbeat + evening summary */
export type FamilyMirrorAppSlice = {
  appKey: string;
  appLabel: string;
  kind: string;
  seconds: number;
};

export type FamilyMirrorParentNote = {
  id: string;
  memberId: string;
  flowDate: string;
  fromMembershipId: string;
  fromMemberName: string;
  tone: string;
  bodyVi: string;
  createdAt: string;
};

export type FamilyMirrorDay = {
  flowDate: string;
  memberId: string;
  memberName: string;
  agentOnline: boolean;
  lastHeartbeatAt: string | null;
  lastForegroundApp: string | null;
  topApps: FamilyMirrorAppSlice[];
  totalSeconds: number;
  insightVi: string | null;
  suggestedActions: string[];
  parentNotes: FamilyMirrorParentNote[];
};

function mapMirrorApp(r: Row): FamilyMirrorAppSlice {
  return {
    appKey: String(r.appKey ?? r.AppKey ?? ''),
    appLabel: String(r.appLabel ?? r.AppLabel ?? ''),
    kind: String(r.kind ?? r.Kind ?? 'app'),
    seconds: Number(r.seconds ?? r.Seconds ?? 0),
  };
}

function mapMirrorNote(r: Row): FamilyMirrorParentNote {
  return {
    id: String(r.id ?? r.Id ?? ''),
    memberId: String(r.memberId ?? r.MemberId ?? ''),
    flowDate: String(r.flowDate ?? r.FlowDate ?? ''),
    fromMembershipId: String(r.fromMembershipId ?? r.FromMembershipId ?? ''),
    fromMemberName: String(r.fromMemberName ?? r.FromMemberName ?? ''),
    tone: String(r.tone ?? r.Tone ?? ''),
    bodyVi: String(r.bodyVi ?? r.BodyVi ?? ''),
    createdAt: String(r.createdAt ?? r.CreatedAt ?? ''),
  };
}

function mapMirrorDay(r: Row): FamilyMirrorDay {
  const apps = asArray(r.topApps ?? r.TopApps).map((x) => mapMirrorApp(x as Row));
  const notes = asArray(r.parentNotes ?? r.ParentNotes).map((x) => mapMirrorNote(x as Row));
  const actions = asArray(r.suggestedActions ?? r.SuggestedActions).map((x) => String(x));
  const hb = r.lastHeartbeatAt ?? r.LastHeartbeatAt;
  return {
    flowDate: String(r.flowDate ?? r.FlowDate ?? ''),
    memberId: String(r.memberId ?? r.MemberId ?? ''),
    memberName: String(r.memberName ?? r.MemberName ?? ''),
    agentOnline: Boolean(r.agentOnline ?? r.AgentOnline),
    lastHeartbeatAt: hb != null ? String(hb) : null,
    lastForegroundApp:
      r.lastForegroundApp != null || r.LastForegroundApp != null
        ? String(r.lastForegroundApp ?? r.LastForegroundApp)
        : null,
    topApps: apps,
    totalSeconds: Number(r.totalSeconds ?? r.TotalSeconds ?? 0),
    insightVi:
      r.insightVi != null || r.InsightVi != null
        ? String(r.insightVi ?? r.InsightVi)
        : null,
    suggestedActions: actions,
    parentNotes: notes,
  };
}

export async function fetchMirrorDay(
  familyId: string,
  opts?: { memberId?: string; flowDate?: string },
): Promise<FamilyMirrorDay> {
  const { data } = await http.get<Row>(`/family-os/families/${familyId}/mirror/day`, {
    params: {
      memberId: opts?.memberId,
      flowDate: opts?.flowDate,
    },
  });
  return mapMirrorDay(data);
}

export async function postMirrorParentNote(
  familyId: string,
  body: {
    memberId: string;
    fromMembershipId: string;
    tone: 'praise' | 'soft' | 'renegotiate';
    bodyVi: string;
    flowDate?: string;
  },
): Promise<FamilyMirrorParentNote> {
  const { data } = await http.post<Row>(`/family-os/families/${familyId}/mirror/notes`, body);
  return mapMirrorNote(data);
}
