import { http } from '@/shared/api/http';

type UnknownRow = Record<string, unknown>;

function asStringArray(value: unknown): string[] {
  return Array.isArray(value) ? value.map((item) => String(item)) : [];
}

export interface FamilyOsOverview {
  packCode: string;
  displayName: string;
  phase: string;
  tagline: string;
  legalBoundary: string;
  enabledCapabilities: string[];
  explicitNonGoals: string[];
}

export interface FamilyMembership {
  id: string;
  familyId: string;
  displayName: string;
  roleCode: string;
  dateOfBirth?: string;
  sortOrder?: number;
  status: string;
}

export interface FamilySummary {
  id: string;
  displayName: string;
  timezone: string;
  status: string;
  members: FamilyMembership[];
}

export interface CommitmentTemplate {
  id: string;
  title: string;
  description?: string;
  memberId?: string;
  windowStart?: string;
  windowEnd?: string;
  sortOrder: number;
  isActive: boolean;
  priority: string;
  expectedDurationMinutes?: number;
  contextAnchor?: string;
  dependsOnTemplateIds: string[];
  allowEarlyComplete?: boolean;
  earlyLeadMinutes?: number;
  onTimeGraceMinutes?: number;
  starReward?: number;
}

export interface FamilyRoutine {
  id: string;
  familyId: string;
  code: string;
  displayName: string;
  kind: string;
  weekdays: number[];
  isActive: boolean;
  sortOrder?: number;
  templates: CommitmentTemplate[];
}

export type UpdateRoutinePayload = {
  displayName?: string;
  kind?: string;
  weekdays?: number[];
  isActive?: boolean;
  sortOrder?: number;
};

export type UpsertTemplatePayload = {
  title: string;
  description?: string | null;
  memberId?: string | null;
  windowStart?: string | null;
  windowEnd?: string | null;
  sortOrder: number;
  isActive?: boolean;
  priority?: string;
  expectedDurationMinutes?: number | null;
  contextAnchor?: string | null;
  dependsOnTemplateIds?: string[];
  allowEarlyComplete?: boolean | null;
  earlyLeadMinutes?: number | null;
  onTimeGraceMinutes?: number | null;
  starReward?: number | null;
};


export interface DayFlowCommitment {
  id: string;
  title: string;
  memberName?: string;
  memberId?: string;
  status: string;
  skipReason?: string;
  windowStart?: string;
  windowEnd?: string;
  completedAt?: string;
  isLateDone?: boolean;
  reminderState?: string;
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
}

export const SKIP_REASON_OPTIONS = [
  { value: 'forgot', label: 'Quên' },
  { value: 'busy', label: 'Bận việc khác' },
  { value: 'need_help', label: 'Cần giúp' },
  { value: 'not_ready', label: 'Chưa sẵn sàng' },
  { value: 'sick', label: 'Ốm / không khỏe' },
  { value: 'other', label: 'Lý do khác' },
] as const;

export function skipReasonLabel(code?: string): string | undefined {
  return SKIP_REASON_OPTIONS.find((o) => o.value === code)?.label;
}

export interface DayFlow {
  id: string;
  familyId: string;
  routineName: string;
  flowDate: string;
  status: string;
  totalCommitments: number;
  doneCount: number;
  pendingCount: number;
  dueNowCount?: number;
  overdueCount?: number;
  upcomingCount?: number;
  localTime?: string;
  commitments: DayFlowCommitment[];
}

export async function fetchFamilyOsOverview(): Promise<FamilyOsOverview> {
  const { data } = await http.get<UnknownRow>('/family-os/overview');
  return {
    packCode: String(data.packCode ?? data.PackCode ?? ''),
    displayName: String(data.displayName ?? data.DisplayName ?? ''),
    phase: String(data.phase ?? data.Phase ?? ''),
    tagline: String(data.tagline ?? data.Tagline ?? ''),
    legalBoundary: String(data.legalBoundary ?? data.LegalBoundary ?? ''),
    enabledCapabilities: asStringArray(data.enabledCapabilities ?? data.EnabledCapabilities),
    explicitNonGoals: asStringArray(data.explicitNonGoals ?? data.ExplicitNonGoals),
  };
}

export async function fetchFamilies(): Promise<FamilySummary[]> {
  const { data } = await http.get<unknown>('/family-os/families');
  const rows = Array.isArray(data) ? data : [];
  return rows.map((row) => {
    const r = row as UnknownRow;
    const membersRaw = Array.isArray(r.members) ? r.members : Array.isArray(r.Members) ? r.Members : [];
    return {
      id: String(r.id ?? r.Id),
      displayName: String(r.displayName ?? r.DisplayName ?? ''),
      timezone: String(r.timezone ?? r.Timezone ?? ''),
      status: String(r.status ?? r.Status ?? ''),
      members: membersRaw.map((m) => mapMembership(m as UnknownRow)),
    };
  });
}

export async function updateFamily(
  familyId: string,
  payload: { displayName?: string; timezone?: string },
): Promise<FamilySummary> {
  const { data } = await http.patch<UnknownRow>(`/family-os/families/${familyId}`, payload);
  const membersRaw = Array.isArray(data.members)
    ? data.members
    : Array.isArray(data.Members)
      ? data.Members
      : [];
  return {
    id: String(data.id ?? data.Id ?? familyId),
    displayName: String(data.displayName ?? data.DisplayName ?? ''),
    timezone: String(data.timezone ?? data.Timezone ?? ''),
    status: String(data.status ?? data.Status ?? ''),
    members: membersRaw.map((m) => mapMembership(m as UnknownRow)),
  };
}

function mapMembership(member: UnknownRow): FamilyMembership {
  return {
    id: String(member.id ?? member.Id),
    familyId: String(member.familyId ?? member.FamilyId ?? ''),
    displayName: String(member.displayName ?? member.DisplayName ?? ''),
    roleCode: String(member.roleCode ?? member.RoleCode ?? ''),
    dateOfBirth:
      member.dateOfBirth != null || member.DateOfBirth != null
        ? String(member.dateOfBirth ?? member.DateOfBirth)
        : undefined,
    sortOrder: Number(member.sortOrder ?? member.SortOrder ?? 0),
    status: String(member.status ?? member.Status ?? ''),
  };
}

export async function addFamilyMember(
  familyId: string,
  payload: {
    displayName: string;
    roleCode: string;
    dateOfBirth?: string | null;
    sortOrder?: number;
  },
): Promise<FamilyMembership> {
  const { data } = await http.post<UnknownRow>(`/family-os/families/${familyId}/members`, payload);
  return mapMembership(data);
}

export async function updateFamilyMember(
  familyId: string,
  memberId: string,
  payload: {
    displayName?: string;
    roleCode?: string;
    dateOfBirth?: string | null;
    clearDateOfBirth?: boolean;
    sortOrder?: number;
    status?: string;
  },
): Promise<FamilyMembership> {
  const { data } = await http.patch<UnknownRow>(
    `/family-os/families/${familyId}/members/${memberId}`,
    payload,
  );
  return mapMembership(data);
}

export async function fetchFamilyRoutines(familyId: string): Promise<FamilyRoutine[]> {
  const { data } = await http.get<unknown>(`/family-os/families/${familyId}/routines`);
  const rows = Array.isArray(data) ? data : [];
  return rows.map((row) => {
    const r = row as UnknownRow;
    const templatesRaw = Array.isArray(r.templates)
      ? r.templates
      : Array.isArray(r.Templates)
        ? r.Templates
        : [];
    return {
      id: String(r.id ?? r.Id),
      familyId: String(r.familyId ?? r.FamilyId ?? ''),
      code: String(r.code ?? r.Code ?? ''),
      displayName: String(r.displayName ?? r.DisplayName ?? ''),
      kind: String(r.kind ?? r.Kind ?? ''),
      weekdays: Array.isArray(r.weekdays ?? r.Weekdays)
        ? ((r.weekdays ?? r.Weekdays) as unknown[]).map((d) => Number(d))
        : [],
      isActive: Boolean(r.isActive ?? r.IsActive ?? true),
      templates: templatesRaw.map((t) => mapTemplate(t as UnknownRow)),
    };
  });
}

function mapGuidList(raw: unknown): string[] {
  if (!Array.isArray(raw)) return [];
  return raw.map((x) => String(x)).filter(Boolean);
}

function mapTemplate(data: UnknownRow): CommitmentTemplate {
  return {
    id: String(data.id ?? data.Id),
    title: String(data.title ?? data.Title ?? ''),
    description:
      data.description != null || data.Description != null
        ? String(data.description ?? data.Description)
        : undefined,
    memberId:
      data.memberId != null || data.MemberId != null
        ? String(data.memberId ?? data.MemberId)
        : undefined,
    windowStart:
      data.windowStart != null || data.WindowStart != null
        ? String(data.windowStart ?? data.WindowStart)
        : undefined,
    windowEnd:
      data.windowEnd != null || data.WindowEnd != null
        ? String(data.windowEnd ?? data.WindowEnd)
        : undefined,
    sortOrder: Number(data.sortOrder ?? data.SortOrder ?? 0),
    isActive: Boolean(data.isActive ?? data.IsActive ?? true),
    priority: String(data.priority ?? data.Priority ?? 'normal'),
    expectedDurationMinutes:
      data.expectedDurationMinutes != null || data.ExpectedDurationMinutes != null
        ? Number(data.expectedDurationMinutes ?? data.ExpectedDurationMinutes)
        : undefined,
    contextAnchor:
      data.contextAnchor != null || data.ContextAnchor != null
        ? String(data.contextAnchor ?? data.ContextAnchor)
        : undefined,
    dependsOnTemplateIds: mapGuidList(
      data.dependsOnTemplateIds ?? data.DependsOnTemplateIds,
    ),
    allowEarlyComplete: Boolean(data.allowEarlyComplete ?? data.AllowEarlyComplete ?? false),
    earlyLeadMinutes: Number(data.earlyLeadMinutes ?? data.EarlyLeadMinutes ?? 0) || 0,
    onTimeGraceMinutes: Number(data.onTimeGraceMinutes ?? data.OnTimeGraceMinutes ?? 0) || 0,
    starReward:
      data.starReward != null || data.StarReward != null
        ? Number(data.starReward ?? data.StarReward)
        : undefined,
  };
}

export async function updateFamilyRoutine(
  familyId: string,
  routineId: string,
  payload: UpdateRoutinePayload,
): Promise<void> {
  await http.patch(`/family-os/families/${familyId}/routines/${routineId}`, payload);
}

export async function addCommitmentTemplate(
  familyId: string,
  routineId: string,
  payload: UpsertTemplatePayload,
): Promise<CommitmentTemplate> {
  const { data } = await http.post<UnknownRow>(
    `/family-os/families/${familyId}/routines/${routineId}/templates`,
    {
      title: payload.title,
      description: payload.description ?? null,
      memberId: payload.memberId ?? null,
      windowStart: payload.windowStart ?? null,
      windowEnd: payload.windowEnd ?? null,
      sortOrder: payload.sortOrder,
      priority: payload.priority ?? 'normal',
      expectedDurationMinutes: payload.expectedDurationMinutes ?? null,
      contextAnchor: payload.contextAnchor ?? null,
      dependsOnTemplateIds: payload.dependsOnTemplateIds ?? [],
      allowEarlyComplete: payload.allowEarlyComplete ?? null,
      earlyLeadMinutes: payload.earlyLeadMinutes ?? null,
      onTimeGraceMinutes: payload.onTimeGraceMinutes ?? null,
      starReward: payload.starReward ?? null,
    },
  );
  return mapTemplate(data);
}

export async function updateCommitmentTemplate(
  familyId: string,
  routineId: string,
  templateId: string,
  payload: UpsertTemplatePayload,
): Promise<CommitmentTemplate> {
  const { data } = await http.patch<UnknownRow>(
    `/family-os/families/${familyId}/routines/${routineId}/templates/${templateId}`,
    {
      title: payload.title,
      description: payload.description ?? null,
      memberId: payload.memberId ?? null,
      windowStart: payload.windowStart ?? null,
      windowEnd: payload.windowEnd ?? null,
      sortOrder: payload.sortOrder,
      isActive: payload.isActive ?? true,
      priority: payload.priority ?? 'normal',
      expectedDurationMinutes: payload.expectedDurationMinutes ?? null,
      contextAnchor: payload.contextAnchor ?? null,
      dependsOnTemplateIds: payload.dependsOnTemplateIds ?? [],
      allowEarlyComplete: payload.allowEarlyComplete ?? null,
      earlyLeadMinutes: payload.earlyLeadMinutes ?? null,
      onTimeGraceMinutes: payload.onTimeGraceMinutes ?? null,
      starReward: payload.starReward ?? null,
    },
  );
  return mapTemplate(data);
}

export async function removeCommitmentTemplate(
  familyId: string,
  routineId: string,
  templateId: string,
): Promise<void> {
  await http.delete(`/family-os/families/${familyId}/routines/${routineId}/templates/${templateId}`);
}

export interface FamilyStarSettings {
  lateT1Minutes: number;
  lateT2Minutes: number;
  lateT3Minutes: number;
  lateHalfPct: number;
  lateZeroPct: number;
  latePenaltyHalfPct: number;
  latePenaltyFullPct: number;
  isConfigured: boolean;
}

export type UpdateFamilyStarSettingsPayload = {
  lateT1Minutes: number;
  lateT2Minutes: number;
  lateT3Minutes: number;
  lateHalfPct: number;
  lateZeroPct: number;
  latePenaltyHalfPct: number;
  latePenaltyFullPct: number;
};

function mapFamilyStarSettings(data: UnknownRow): FamilyStarSettings {
  return {
    lateT1Minutes: Number(data.lateT1Minutes ?? data.LateT1Minutes ?? 30),
    lateT2Minutes: Number(data.lateT2Minutes ?? data.LateT2Minutes ?? 60),
    lateT3Minutes: Number(data.lateT3Minutes ?? data.LateT3Minutes ?? 90),
    lateHalfPct: Number(data.lateHalfPct ?? data.LateHalfPct ?? 50),
    lateZeroPct: Number(data.lateZeroPct ?? data.LateZeroPct ?? 0),
    latePenaltyHalfPct: Number(data.latePenaltyHalfPct ?? data.LatePenaltyHalfPct ?? -50),
    latePenaltyFullPct: Number(data.latePenaltyFullPct ?? data.LatePenaltyFullPct ?? -100),
    isConfigured: Boolean(data.isConfigured ?? data.IsConfigured ?? false),
  };
}

export async function fetchFamilyStarSettings(familyId: string): Promise<FamilyStarSettings> {
  const { data } = await http.get<UnknownRow>(`/family-os/families/${familyId}/star-settings`);
  return mapFamilyStarSettings(data);
}

export async function updateFamilyStarSettings(
  familyId: string,
  payload: UpdateFamilyStarSettingsPayload,
): Promise<FamilyStarSettings> {
  const { data } = await http.put<UnknownRow>(
    `/family-os/families/${familyId}/star-settings`,
    payload,
  );
  return mapFamilyStarSettings(data);
}

export async function ensureDayFlow(familyId: string): Promise<DayFlow> {
  const { data } = await http.post<UnknownRow>(`/family-os/families/${familyId}/day-flows/ensure`, {});
  const commitmentsRaw = Array.isArray(data.commitments)
    ? data.commitments
    : Array.isArray(data.Commitments)
      ? data.Commitments
      : [];
  return {
    id: String(data.id ?? data.Id),
    familyId: String(data.familyId ?? data.FamilyId ?? ''),
    routineName: String(data.routineName ?? data.RoutineName ?? ''),
    flowDate: String(data.flowDate ?? data.FlowDate ?? ''),
    status: String(data.status ?? data.Status ?? ''),
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
    commitments: commitmentsRaw.map((c) => mapDayFlowCommitment(c as UnknownRow)),
  };
}

function mapDayFlowCommitment(row: UnknownRow): DayFlowCommitment {
  return {
    id: String(row.id ?? row.Id),
    title: String(row.title ?? row.Title ?? ''),
    memberName:
      row.memberName != null || row.MemberName != null
        ? String(row.memberName ?? row.MemberName)
        : undefined,
    memberId:
      row.memberId != null || row.MemberId != null
        ? String(row.memberId ?? row.MemberId)
        : undefined,
    status: String(row.status ?? row.Status ?? ''),
    windowStart:
      row.windowStart != null || row.WindowStart != null
        ? String(row.windowStart ?? row.WindowStart)
        : undefined,
    windowEnd:
      row.windowEnd != null || row.WindowEnd != null
        ? String(row.windowEnd ?? row.WindowEnd)
        : undefined,
    reminderState: String(row.reminderState ?? row.ReminderState ?? 'none'),
    reminderLabel:
      row.reminderLabel != null || row.ReminderLabel != null
        ? String(row.reminderLabel ?? row.ReminderLabel)
        : undefined,
    isLateDone: Boolean(row.isLateDone ?? row.IsLateDone ?? false),
    completedAt:
      row.completedAt != null || row.CompletedAt != null
        ? String(row.completedAt ?? row.CompletedAt)
        : undefined,
    skipReason:
      row.skipReason != null || row.SkipReason != null
        ? String(row.skipReason ?? row.SkipReason)
        : undefined,
    evidenceUrl:
      row.evidenceUrl != null || row.EvidenceUrl != null
        ? String(row.evidenceUrl ?? row.EvidenceUrl)
        : undefined,
    allowEarlyComplete: Boolean(row.allowEarlyComplete ?? row.AllowEarlyComplete ?? false),
    earlyLeadMinutes: Number(row.earlyLeadMinutes ?? row.EarlyLeadMinutes ?? 0) || 0,
    onTimeGraceMinutes: Number(row.onTimeGraceMinutes ?? row.OnTimeGraceMinutes ?? 0) || 0,
    starReward:
      row.starReward != null || row.StarReward != null
        ? Number(row.starReward ?? row.StarReward)
        : undefined,
    starDelta:
      row.starDelta != null || row.StarDelta != null
        ? Number(row.starDelta ?? row.StarDelta)
        : undefined,
    starTier:
      row.starTier != null || row.StarTier != null
        ? String(row.starTier ?? row.StarTier)
        : undefined,
    starLabelVi:
      row.starLabelVi != null || row.StarLabelVi != null
        ? String(row.starLabelVi ?? row.StarLabelVi)
        : undefined,
    projectedStarDelta:
      row.projectedStarDelta != null || row.ProjectedStarDelta != null
        ? Number(row.projectedStarDelta ?? row.ProjectedStarDelta)
        : undefined,
    projectedStarLabelVi:
      row.projectedStarLabelVi != null || row.ProjectedStarLabelVi != null
        ? String(row.projectedStarLabelVi ?? row.ProjectedStarLabelVi)
        : undefined,
    memberStarBalance:
      row.memberStarBalance != null || row.MemberStarBalance != null
        ? Number(row.memberStarBalance ?? row.MemberStarBalance)
        : undefined,
    starPosted: Boolean(row.starPosted ?? row.StarPosted ?? false),
  };
}

export async function updateCommitmentProgress(
  familyId: string,
  commitmentId: string,
  status: string,
  skipReason?: string,
  evidenceUrl?: string,
): Promise<DayFlowCommitment> {
  const { data } = await http.patch<UnknownRow>(
    `/family-os/families/${familyId}/commitments/${commitmentId}`,
    { status, skipReason: skipReason ?? null, evidenceUrl: evidenceUrl ?? null },
  );
  return mapDayFlowCommitment(data);
}

export interface FamilyAgreement {
  id: string;
  familyId: string;
  proposedBy: string;
  proposedByName?: string;
  title: string;
  proposalBody: string;
  targetType: string;
  targetId?: string;
  status: string;
  termsJson: string;
  decidedAt?: string;
  decidedBy?: string;
  decisionNote?: string;
  createdAt: string;
  purpose?: string;
  effectiveOn?: string;
  reviewAfterDays?: number;
  appliesToMemberId?: string;
}

export interface ConsequenceLibraryItem {
  code: string;
  group: string;
  labelVi: string;
  descriptionVi: string;
}

function mapAgreement(data: UnknownRow): FamilyAgreement {
  return {
    id: String(data.id ?? data.Id),
    familyId: String(data.familyId ?? data.FamilyId ?? ''),
    proposedBy: String(data.proposedBy ?? data.ProposedBy ?? ''),
    proposedByName:
      data.proposedByName != null || data.ProposedByName != null
        ? String(data.proposedByName ?? data.ProposedByName)
        : undefined,
    title: String(data.title ?? data.Title ?? ''),
    proposalBody: String(data.proposalBody ?? data.ProposalBody ?? ''),
    targetType: String(data.targetType ?? data.TargetType ?? ''),
    targetId:
      data.targetId != null || data.TargetId != null
        ? String(data.targetId ?? data.TargetId)
        : undefined,
    status: String(data.status ?? data.Status ?? ''),
    termsJson: String(data.termsJson ?? data.TermsJson ?? '{}'),
    decidedAt:
      data.decidedAt != null || data.DecidedAt != null
        ? String(data.decidedAt ?? data.DecidedAt)
        : undefined,
    decidedBy:
      data.decidedBy != null || data.DecidedBy != null
        ? String(data.decidedBy ?? data.DecidedBy)
        : undefined,
    decisionNote:
      data.decisionNote != null || data.DecisionNote != null
        ? String(data.decisionNote ?? data.DecisionNote)
        : undefined,
    createdAt: String(data.createdAt ?? data.CreatedAt ?? ''),
    purpose:
      data.purpose != null || data.Purpose != null
        ? String(data.purpose ?? data.Purpose)
        : undefined,
    effectiveOn:
      data.effectiveOn != null || data.EffectiveOn != null
        ? String(data.effectiveOn ?? data.EffectiveOn)
        : undefined,
    reviewAfterDays:
      data.reviewAfterDays != null || data.ReviewAfterDays != null
        ? Number(data.reviewAfterDays ?? data.ReviewAfterDays)
        : undefined,
    appliesToMemberId:
      data.appliesToMemberId != null || data.AppliesToMemberId != null
        ? String(data.appliesToMemberId ?? data.AppliesToMemberId)
        : undefined,
  };
}

export async function fetchFamilyAgreements(
  familyId: string,
  status?: string,
): Promise<FamilyAgreement[]> {
  const { data } = await http.get<unknown>(`/family-os/families/${familyId}/agreements`, {
    params: status ? { status } : undefined,
  });
  const rows = Array.isArray(data) ? data : [];
  return rows.map((row) => mapAgreement(row as UnknownRow));
}

export async function createFamilyAgreement(
  familyId: string,
  payload: {
    proposedBy: string;
    title: string;
    proposalBody: string;
    targetType?: string;
    targetId?: string | null;
    termsJson?: string;
    purpose?: string | null;
    effectiveOn?: string | null;
    reviewAfterDays?: number | null;
    appliesToMemberId?: string | null;
  },
): Promise<FamilyAgreement> {
  const { data } = await http.post<UnknownRow>(`/family-os/families/${familyId}/agreements`, payload);
  return mapAgreement(data);
}

export async function decideFamilyAgreement(
  familyId: string,
  agreementId: string,
  payload: { status: string; decidedBy: string; decisionNote?: string },
): Promise<FamilyAgreement> {
  const { data } = await http.post<UnknownRow>(
    `/family-os/families/${familyId}/agreements/${agreementId}/decide`,
    payload,
  );
  return mapAgreement(data);
}

export async function fetchConsequenceLibrary(): Promise<ConsequenceLibraryItem[]> {
  const { data } = await http.get<unknown>('/family-os/consequence-library');
  const rows = Array.isArray(data) ? data : [];
  return rows.map((row) => {
    const r = row as UnknownRow;
    return {
      code: String(r.code ?? r.Code ?? ''),
      group: String(r.group ?? r.Group ?? ''),
      labelVi: String(r.labelVi ?? r.LabelVi ?? ''),
      descriptionVi: String(r.descriptionVi ?? r.DescriptionVi ?? ''),
    };
  });
}

export interface AccountabilityOption {
  id: string;
  familyId: string;
  kind: 'consequence' | 'reward' | string;
  code: string;
  optionGroup: string;
  labelVi: string;
  descriptionVi: string;
  isSystem: boolean;
  sortOrder: number;
  status: string;
}

function mapAccountabilityOption(row: UnknownRow): AccountabilityOption {
  return {
    id: String(row.id ?? row.Id ?? ''),
    familyId: String(row.familyId ?? row.FamilyId ?? ''),
    kind: String(row.kind ?? row.Kind ?? ''),
    code: String(row.code ?? row.Code ?? ''),
    optionGroup: String(row.optionGroup ?? row.OptionGroup ?? ''),
    labelVi: String(row.labelVi ?? row.LabelVi ?? ''),
    descriptionVi: String(row.descriptionVi ?? row.DescriptionVi ?? ''),
    isSystem: Boolean(row.isSystem ?? row.IsSystem ?? false),
    sortOrder: Number(row.sortOrder ?? row.SortOrder ?? 0),
    status: String(row.status ?? row.Status ?? ''),
  };
}

export async function fetchAccountabilityOptions(
  familyId: string,
  kind?: string,
): Promise<AccountabilityOption[]> {
  const { data } = await http.get<unknown>(
    `/family-os/families/${familyId}/accountability-options`,
    { params: kind ? { kind } : undefined },
  );
  const rows = Array.isArray(data) ? data : [];
  return rows.map((row) => mapAccountabilityOption(row as UnknownRow));
}

export async function createAccountabilityOption(
  familyId: string,
  payload: {
    kind: string;
    code: string;
    optionGroup: string;
    labelVi: string;
    descriptionVi?: string;
    sortOrder?: number;
  },
): Promise<AccountabilityOption> {
  const { data } = await http.post<UnknownRow>(
    `/family-os/families/${familyId}/accountability-options`,
    payload,
  );
  return mapAccountabilityOption(data);
}

export async function updateAccountabilityOption(
  familyId: string,
  optionId: string,
  payload: {
    optionGroup?: string;
    labelVi?: string;
    descriptionVi?: string;
    sortOrder?: number;
    status?: string;
  },
): Promise<AccountabilityOption> {
  const { data } = await http.patch<UnknownRow>(
    `/family-os/families/${familyId}/accountability-options/${optionId}`,
    payload,
  );
  return mapAccountabilityOption(data);
}

export async function deleteAccountabilityOption(
  familyId: string,
  optionId: string,
): Promise<void> {
  await http.delete(`/family-os/families/${familyId}/accountability-options/${optionId}`);
}

export interface ConsequenceEvent {
  id: string;
  familyId: string;
  dayFlowId: string;
  commitmentId: string;
  agreementId: string;
  memberId?: string;
  memberName?: string;
  flowDate: string;
  consequenceCode: string;
  labelVi: string;
  triggerSkipReason?: string;
  commitmentTitle: string;
  status: string;
  decidedBy?: string;
  decidedAt?: string;
  decisionNote?: string;
  createdAt: string;
}

function mapConsequenceEvent(row: UnknownRow): ConsequenceEvent {
  return {
    id: String(row.id ?? row.Id ?? ''),
    familyId: String(row.familyId ?? row.FamilyId ?? ''),
    dayFlowId: String(row.dayFlowId ?? row.DayFlowId ?? ''),
    commitmentId: String(row.commitmentId ?? row.CommitmentId ?? ''),
    agreementId: String(row.agreementId ?? row.AgreementId ?? ''),
    memberId:
      row.memberId != null || row.MemberId != null
        ? String(row.memberId ?? row.MemberId)
        : undefined,
    memberName:
      row.memberName != null || row.MemberName != null
        ? String(row.memberName ?? row.MemberName)
        : undefined,
    flowDate: String(row.flowDate ?? row.FlowDate ?? ''),
    consequenceCode: String(row.consequenceCode ?? row.ConsequenceCode ?? ''),
    labelVi: String(row.labelVi ?? row.LabelVi ?? ''),
    triggerSkipReason:
      row.triggerSkipReason != null || row.TriggerSkipReason != null
        ? String(row.triggerSkipReason ?? row.TriggerSkipReason)
        : undefined,
    commitmentTitle: String(row.commitmentTitle ?? row.CommitmentTitle ?? ''),
    status: String(row.status ?? row.Status ?? ''),
    decidedBy:
      row.decidedBy != null || row.DecidedBy != null
        ? String(row.decidedBy ?? row.DecidedBy)
        : undefined,
    decidedAt:
      row.decidedAt != null || row.DecidedAt != null
        ? String(row.decidedAt ?? row.DecidedAt)
        : undefined,
    decisionNote:
      row.decisionNote != null || row.DecisionNote != null
        ? String(row.decisionNote ?? row.DecisionNote)
        : undefined,
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
  };
}

export async function fetchConsequenceEvents(
  familyId: string,
  params?: { flowDate?: string; status?: string },
): Promise<ConsequenceEvent[]> {
  const { data } = await http.get<unknown>(`/family-os/families/${familyId}/consequence-events`, {
    params,
  });
  const rows = Array.isArray(data) ? data : [];
  return rows.map((row) => mapConsequenceEvent(row as UnknownRow));
}

export async function decideConsequenceEvent(
  familyId: string,
  eventId: string,
  payload: { status: 'applied' | 'waived'; decidedBy: string; decisionNote?: string },
): Promise<ConsequenceEvent> {
  const { data } = await http.post<UnknownRow>(
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
  appliedConsequences: number;
}

export interface AccountabilityGlance {
  from: string;
  to: string;
  today: string;
  todayIsBeautifulDay: boolean;
  currentStreak: number;
  days: AccountabilityDayGlance[];
}

export async function fetchAccountabilityGlance(
  familyId: string,
  params?: { from?: string; to?: string },
): Promise<AccountabilityGlance> {
  const { data } = await http.get<UnknownRow>(
    `/family-os/families/${familyId}/accountability-glance`,
    { params },
  );
  const daysRaw = Array.isArray(data.days)
    ? data.days
    : Array.isArray(data.Days)
      ? data.Days
      : [];
  return {
    from: String(data.from ?? data.From ?? ''),
    to: String(data.to ?? data.To ?? ''),
    today: String(data.today ?? data.Today ?? ''),
    todayIsBeautifulDay: Boolean(data.todayIsBeautifulDay ?? data.TodayIsBeautifulDay ?? false),
    currentStreak: Number(data.currentStreak ?? data.CurrentStreak ?? 0),
    days: daysRaw.map((row) => {
      const d = row as UnknownRow;
      return {
        date: String(d.date ?? d.Date ?? ''),
        isScored: Boolean(d.isScored ?? d.IsScored ?? false),
        isBeautifulDay: Boolean(d.isBeautifulDay ?? d.IsBeautifulDay ?? false),
        childDone: Number(d.childDone ?? d.ChildDone ?? 0),
        childSkipped: Number(d.childSkipped ?? d.ChildSkipped ?? 0),
        childOpen: Number(d.childOpen ?? d.ChildOpen ?? 0),
        childLateDone: Number(d.childLateDone ?? d.ChildLateDone ?? 0),
        appliedConsequences: Number(d.appliedConsequences ?? d.AppliedConsequences ?? 0),
      };
    }),
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
  ctaPath?: string;
  ctaLabel?: string;
  focusMemberId?: string;
  focusMemberName?: string;
  focusTemplateId?: string;
  focusCommitmentTitle?: string;
  doneCount: number;
  skippedCount: number;
  openCount: number;
  totalCount: number;
  patternForgotCount: number;
  patternWindowDays: number;
}

export async function fetchCoachInsight(
  familyId: string,
  date?: string,
): Promise<FamilyCoachInsight> {
  const { data } = await http.get<UnknownRow>(
    `/family-os/families/${familyId}/coach-insight`,
    { params: date ? { date } : undefined },
  );
  return {
    flowDate: String(data.flowDate ?? data.FlowDate ?? ''),
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
    ctaPath:
      data.ctaPath != null || data.CtaPath != null
        ? String(data.ctaPath ?? data.CtaPath)
        : undefined,
    ctaLabel:
      data.ctaLabel != null || data.CtaLabel != null
        ? String(data.ctaLabel ?? data.CtaLabel)
        : undefined,
    focusMemberId:
      data.focusMemberId != null || data.FocusMemberId != null
        ? String(data.focusMemberId ?? data.FocusMemberId)
        : undefined,
    focusMemberName:
      data.focusMemberName != null || data.FocusMemberName != null
        ? String(data.focusMemberName ?? data.FocusMemberName)
        : undefined,
    focusTemplateId:
      data.focusTemplateId != null || data.FocusTemplateId != null
        ? String(data.focusTemplateId ?? data.FocusTemplateId)
        : undefined,
    focusCommitmentTitle:
      data.focusCommitmentTitle != null || data.FocusCommitmentTitle != null
        ? String(data.focusCommitmentTitle ?? data.FocusCommitmentTitle)
        : undefined,
    doneCount: Number(data.doneCount ?? data.DoneCount ?? 0),
    skippedCount: Number(data.skippedCount ?? data.SkippedCount ?? 0),
    openCount: Number(data.openCount ?? data.OpenCount ?? 0),
    totalCount: Number(data.totalCount ?? data.TotalCount ?? 0),
    patternForgotCount: Number(data.patternForgotCount ?? data.PatternForgotCount ?? 0),
    patternWindowDays: Number(data.patternWindowDays ?? data.PatternWindowDays ?? 7),
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

function mapTeamChild(row: UnknownRow): TeamChildSlice {
  return {
    memberId: String(row.memberId ?? row.MemberId ?? ''),
    displayName: String(row.displayName ?? row.DisplayName ?? ''),
    done: Number(row.done ?? row.Done ?? 0),
    total: Number(row.total ?? row.Total ?? 0),
    open: Number(row.open ?? row.Open ?? 0),
    skipped: Number(row.skipped ?? row.Skipped ?? 0),
  };
}

function mapTeamUnlock(row: UnknownRow): TeamUnlock {
  return {
    id: String(row.id ?? row.Id ?? ''),
    familyId: String(row.familyId ?? row.FamilyId ?? ''),
    dayFlowId: String(row.dayFlowId ?? row.DayFlowId ?? ''),
    flowDate: String(row.flowDate ?? row.FlowDate ?? ''),
    rewardCode: String(row.rewardCode ?? row.RewardCode ?? ''),
    labelVi: String(row.labelVi ?? row.LabelVi ?? ''),
    agreementId:
      row.agreementId != null || row.AgreementId != null
        ? String(row.agreementId ?? row.AgreementId)
        : undefined,
    teamDone: Number(row.teamDone ?? row.TeamDone ?? 0),
    teamTotal: Number(row.teamTotal ?? row.TeamTotal ?? 0),
    teamPercent: Number(row.teamPercent ?? row.TeamPercent ?? 0),
    status: String(row.status ?? row.Status ?? ''),
    confirmedBy:
      row.confirmedBy != null || row.ConfirmedBy != null
        ? String(row.confirmedBy ?? row.ConfirmedBy)
        : undefined,
    confirmedAt:
      row.confirmedAt != null || row.ConfirmedAt != null
        ? String(row.confirmedAt ?? row.ConfirmedAt)
        : undefined,
    decisionNote:
      row.decisionNote != null || row.DecisionNote != null
        ? String(row.decisionNote ?? row.DecisionNote)
        : undefined,
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
  };
}

export async function fetchTeamDay(familyId: string, flowDate?: string): Promise<TeamDay> {
  const { data } = await http.get<UnknownRow>(`/family-os/families/${familyId}/team-day`, {
    params: flowDate ? { flowDate } : undefined,
  });
  const childrenRaw = Array.isArray(data.children)
    ? data.children
    : Array.isArray(data.Children)
      ? data.Children
      : [];
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
    children: childrenRaw.map((row) => mapTeamChild(row as UnknownRow)),
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
  const rows = Array.isArray(data) ? data : [];
  return rows.map((row) => mapTeamUnlock(row as UnknownRow));
}

export interface RewardCatalogItem {
  id: string;
  title: string;
  icon: string;
  cost: number | null;
  tone?: string;
  isSpecial?: boolean;
  sortOrder?: number;
  description?: string;
  active?: boolean;
}

function mapRewardCatalogItem(row: UnknownRow): RewardCatalogItem {
  const costRaw = row.cost ?? row.Cost;
  return {
    id: String(row.id ?? row.Id),
    title: String(row.title ?? row.Title ?? ''),
    icon: String(row.icon ?? row.Icon ?? '🎁'),
    cost: costRaw == null ? null : Number(costRaw),
    tone:
      row.tone != null || row.Tone != null ? String(row.tone ?? row.Tone) : undefined,
    isSpecial: Boolean(row.isSpecial ?? row.IsSpecial ?? false),
    sortOrder: Number(row.sortOrder ?? row.SortOrder ?? 0),
    description:
      row.description != null || row.Description != null
        ? String(row.description ?? row.Description)
        : undefined,
    active: row.active != null || row.Active != null ? Boolean(row.active ?? row.Active) : true,
  };
}

export async function fetchRewardCatalog(familyId: string): Promise<RewardCatalogItem[]> {
  const { data } = await http.get<unknown>(`/family-os/families/${familyId}/reward-catalog`);
  const rows = Array.isArray(data) ? data : [];
  return rows.map((row) => mapRewardCatalogItem(row as UnknownRow));
}

export type UpsertRewardCatalogPayload = {
  title: string;
  icon: string;
  cost: number;
  description?: string;
  tone?: string;
  sortOrder: number;
};

export async function createRewardCatalogItem(
  familyId: string,
  payload: UpsertRewardCatalogPayload,
): Promise<RewardCatalogItem> {
  const { data } = await http.post<UnknownRow>(
    `/family-os/families/${familyId}/reward-catalog`,
    payload,
  );
  return mapRewardCatalogItem(data);
}

export async function updateRewardCatalogItem(
  familyId: string,
  catalogId: string,
  payload: UpsertRewardCatalogPayload,
): Promise<RewardCatalogItem> {
  const { data } = await http.patch<UnknownRow>(
    `/family-os/families/${familyId}/reward-catalog/${catalogId}`,
    payload,
  );
  return mapRewardCatalogItem(data);
}

export async function deactivateRewardCatalogItem(
  familyId: string,
  catalogId: string,
): Promise<void> {
  await http.delete(`/family-os/families/${familyId}/reward-catalog/${catalogId}`);
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
}

function mapChildGratitude(row: UnknownRow): ChildGratitude {
  return {
    id: String(row.id ?? row.Id ?? ''),
    familyId: String(row.familyId ?? row.FamilyId ?? ''),
    fromMemberId: String(row.fromMemberId ?? row.FromMemberId ?? ''),
    fromMemberName: String(row.fromMemberName ?? row.FromMemberName ?? ''),
    toMemberId:
      row.toMemberId != null || row.ToMemberId != null
        ? String(row.toMemberId ?? row.ToMemberId)
        : undefined,
    toMemberName:
      row.toMemberName != null || row.ToMemberName != null
        ? String(row.toMemberName ?? row.ToMemberName)
        : undefined,
    flowDate: String(row.flowDate ?? row.FlowDate ?? ''),
    messageVi: String(row.messageVi ?? row.MessageVi ?? ''),
    praiseContext:
      row.praiseContext != null || row.PraiseContext != null
        ? String(row.praiseContext ?? row.PraiseContext)
        : undefined,
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    readAt:
      row.readAt != null || row.ReadAt != null ? String(row.readAt ?? row.ReadAt) : undefined,
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
  const rows = Array.isArray(data) ? data : [];
  return rows.map((row) => mapChildGratitude(row as UnknownRow));
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

function mapFamilyMemberMood(row: UnknownRow): FamilyMemberMood {
  return {
    id: String(row.id ?? row.Id ?? ''),
    familyId: String(row.familyId ?? row.FamilyId ?? ''),
    memberId: String(row.memberId ?? row.MemberId ?? ''),
    memberName: String(row.memberName ?? row.MemberName ?? ''),
    flowDate: String(row.flowDate ?? row.FlowDate ?? ''),
    moodCode: String(row.moodCode ?? row.MoodCode ?? ''),
    note: row.note != null || row.Note != null ? String(row.note ?? row.Note) : undefined,
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    updatedAt: String(row.updatedAt ?? row.UpdatedAt ?? ''),
  };
}

export async function fetchFamilyMoods(
  familyId: string,
  flowDate: string,
): Promise<FamilyMemberMood[]> {
  const { data } = await http.get<unknown>(`/family-os/families/${familyId}/moods`, {
    params: { flowDate },
  });
  const rows = Array.isArray(data) ? data : [];
  return rows.map((row) => mapFamilyMemberMood(row as UnknownRow));
}

export async function fetchMemberStarBalance(
  familyId: string,
  memberId: string,
): Promise<number> {
  const { data } = await http.get<UnknownRow>(
    `/family-os/families/${familyId}/members/${memberId}/star-balance`,
  );
  return Number(data.balance ?? data.Balance ?? 0);
}
