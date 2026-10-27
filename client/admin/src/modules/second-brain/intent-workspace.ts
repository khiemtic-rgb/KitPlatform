export type IntentPlanStep = {
  capabilityId?: string | null;
  disposition?: string | null;
  slots?: Record<string, string> | null;
  missingSlots?: string[] | null;
  reason?: string | null;
  runHandler?: boolean | null;
};

export type IntentExecutionStep = {
  capabilityId?: string | null;
  disposition?: string | null;
  status?: string | null;
  capabilityCompletionMet?: boolean | null;
  callerOutcomeMet?: boolean | null;
  wroteData?: boolean | null;
  externalCall?: boolean | null;
  evidenceJson?: string | null;
};

export type IntentEvidence = {
  outcome?: string | null;
  understanding?: string | null;
  goal?: string | null;
  completionConditions?: string[] | null;
  contextUsed?: string[] | null;
  missingInformation?: { slot?: string | null; why?: string | null }[] | null;
  humanDecisions?: { capabilityId?: string | null; why?: string | null }[] | null;
  ambiguous?: boolean | null;
  modelLimit?: string | null;
  callerOutcomeMet?: boolean | null;
  nextStep?: string | null;
  plan?: IntentPlanStep[] | null;
  execution?: IntentExecutionStep[] | null;
  catalogIds?: string[] | null;
};

export type IntentInput =
  | { status: 'idle' }
  | { status: 'waiting' }
  | { status: 'http'; code: number; message?: string }
  | { status: 'evidence'; evidence: IntentEvidence };

export type IntentPhase =
  | 'idle'
  | 'waiting'
  | 'needs_clarification'
  | 'completed'
  | 'partial'
  | 'blocked'
  | 'rejected'
  | 'unreadable'
  | 'model_unavailable'
  | 'transport_failed';

export type EvidenceFact = {
  label: string;
  value: string;
};

export type IntentPresentation = {
  phase: IntentPhase;
  phaseLabel: string;
  backendOutcome: string;
  understood: boolean;
  goal: string;
  completionConditions: string[];
  selectedCapabilities: string[];
  plan: { capabilityId: string; disposition: string; reason: string; runHandler: boolean }[];
  missing: { slot: string; why: string }[];
  humanDecisions: { capabilityId: string; why: string; inCatalog: boolean }[];
  clarification: string;
  limit: string;
  nextStep: string;
  facts: EvidenceFact[];
  sideEffect: boolean;
  concluded: boolean;
  goalConfirmed: boolean;
  retryable: boolean;
  storedDecision: { id: string; status: string; operationKey: string } | null;
};

const PHASE_LABEL: Record<IntentPhase, string> = {
  idle: 'Chưa gửi',
  waiting: 'Đang chờ API',
  needs_clarification: 'Cần làm rõ',
  completed: 'Hoàn thành',
  partial: 'Hoàn thành một phần',
  blocked: 'Bị chặn',
  rejected: 'Bị từ chối',
  unreadable: 'Không đọc được diễn giải',
  model_unavailable: 'Không kết nối được mô hình',
  transport_failed: 'Không kết nối được API',
};

const TRADING_PERMISSIONS = [
  'personal_intelligence.trading.run',
  'personal_intelligence.exchange.connect',
  'personal_intelligence.exchange.read',
];

const INTENT_PERMISSIONS = [
  ...TRADING_PERMISSIONS,
  'personal_intelligence.trading.approve',
  'personal_intelligence.research.read',
];

export function secondBrainAccess(roles: readonly string[], permissions: readonly string[]) {
  const admin = roles.includes('ADMIN');
  const has = (permission: string) => permissions.includes(permission);
  const canTrading = admin || TRADING_PERMISSIONS.some(has);
  const canIntent = admin || INTENT_PERMISSIONS.some(has);
  const canApprove = admin || has('personal_intelligence.trading.approve');
  return { canTrading, canIntent, canApprove };
}

export function presentIntent(input: IntentInput): IntentPresentation {
  if (input.status === 'idle') return blank('idle', '');
  if (input.status === 'waiting') return blank('waiting', '');
  if (input.status === 'http') {
    const rejected = input.code === 401 || input.code === 403;
    return {
      ...blank(rejected ? 'rejected' : 'transport_failed', ''),
      nextStep: input.message || (rejected
        ? 'API từ chối yêu cầu. Giao diện không thực thi thay.'
        : 'Không kết nối được API. Chưa có kết luận nghiệp vụ. Có thể thử lại.'),
      retryable: !rejected,
    };
  }

  const evidence = input.evidence;
  const outcome = evidence.outcome ?? '';
  const ambiguous = evidence.ambiguous === true;
  const execution = evidence.execution ?? [];
  const sideEffect = execution.some((step) => step.wroteData === true || step.externalCall === true);
  const callerMet = evidence.callerOutcomeMet === true && !sideEffect;
  const connectionReason = (evidence.plan ?? []).some((step) => {
    const reason = step.reason?.trim() ?? '';
    return reason === 'model_unreachable' || reason.startsWith('model_http_status_');
  });
  let phase: IntentPhase;
  if (ambiguous) phase = 'needs_clarification';
  else if (sideEffect) phase = 'blocked';
  else if (outcome === 'unavailable' || connectionReason) phase = 'model_unavailable';
  else if (outcome === 'unreadable') phase = 'unreadable';
  else if (outcome === 'completed' && callerMet) phase = 'completed';
  else if (outcome === 'ran_incomplete') phase = 'partial';
  else phase = 'blocked';
  const phaseLabel = phase === 'model_unavailable' && outcome === 'unavailable'
    ? 'Model chưa được cấu hình'
    : PHASE_LABEL[phase];

  const plan = (evidence.plan ?? []).map((step) => ({
    capabilityId: step.capabilityId?.trim() || '—',
    disposition: step.disposition?.trim() || '—',
    reason: step.reason?.trim() || '',
    runHandler: step.runHandler === true,
  }));
  const selectedCapabilities = ambiguous
    ? []
    : plan
      .filter((step) => step.capabilityId !== '—' && (step.disposition === 'run' || step.disposition === 'evaluate'))
      .map((step) => step.capabilityId);

  const missing = [
    ...(evidence.missingInformation ?? []).map((item) => ({
      slot: item.slot?.trim() || '—',
      why: item.why?.trim() || '',
    })),
    ...(evidence.plan ?? []).flatMap((step) => (step.missingSlots ?? []).map((slot) => ({
      slot: slot.trim() || '—',
      why: step.capabilityId?.trim() || '',
    }))),
  ];

  const catalog = new Set((evidence.catalogIds ?? []).filter((id): id is string => Boolean(id)));
  const goal = evidence.goal?.trim() || '';

  return {
    phase,
    phaseLabel,
    backendOutcome: outcome || '—',
    understood: evidence.understanding === 'model' && Boolean(goal) && !ambiguous,
    goal,
    completionConditions: (evidence.completionConditions ?? []).filter((item): item is string => Boolean(item)),
    selectedCapabilities,
    plan,
    missing,
    humanDecisions: (evidence.humanDecisions ?? []).map((item) => {
      const capabilityId = item.capabilityId?.trim() || '—';
      return {
        capabilityId,
        why: item.why?.trim() || '',
        inCatalog: catalog.size > 0 && catalog.has(capabilityId),
      };
    }),
    clarification: ambiguous ? (evidence.nextStep?.trim() || 'Chưa đủ căn cứ để chọn hành động. Hãy nói rõ đối tượng và việc cần làm.') : '',
    limit: evidence.modelLimit?.trim() || '',
    nextStep: evidence.nextStep?.trim() || '',
    facts: execution.flatMap(factsFor),
    sideEffect,
    concluded: phase === 'completed',
    goalConfirmed: !ambiguous && Boolean(goal),
    retryable: phase === 'model_unavailable' || phase === 'unreadable',
    storedDecision: storedDecisionFrom(execution),
  };
}

function blank(phase: IntentPhase, nextStep: string): IntentPresentation {
  return {
    phase,
    phaseLabel: PHASE_LABEL[phase],
    backendOutcome: '—',
    understood: false,
    goal: '',
    completionConditions: [],
    selectedCapabilities: [],
    plan: [],
    missing: [],
    humanDecisions: [],
    clarification: '',
    limit: '',
    nextStep,
    facts: [],
    sideEffect: false,
    concluded: false,
    goalConfirmed: false,
    retryable: phase === 'transport_failed',
    storedDecision: null,
  };
}

function factsFor(step: IntentExecutionStep): EvidenceFact[] {
  const facts: EvidenceFact[] = [];
  const capability = step.capabilityId?.trim() || '—';
  facts.push({ label: `${capability} · status`, value: step.status?.trim() || '—' });
  facts.push({ label: `${capability} · wroteData`, value: String(step.wroteData === true) });
  facts.push({ label: `${capability} · externalCall`, value: String(step.externalCall === true) });
  if (!step.evidenceJson) return facts;
  try {
    const body = JSON.parse(step.evidenceJson) as Record<string, unknown>;
    const publicationId = text(body.publicationId);
    if (publicationId) facts.push({ label: 'publicationId', value: publicationId });
    const availability = text(body.availability);
    if (availability) facts.push({ label: 'availability', value: availability });
    if (Array.isArray(body.objects)) {
      for (const item of body.objects) {
        if (!item || typeof item !== 'object') continue;
        const row = item as Record<string, unknown>;
        const id = text(row.id) || 'object';
        const hash = text(row.sha256);
        if (hash) facts.push({ label: `${id} sha256`, value: hash });
      }
    }
    if (Array.isArray(body.missingData) && body.missingData.length > 0) {
      facts.push({ label: 'missingData', value: body.missingData.map(String).join(', ') });
    }
    if (typeof body.approvalAccepted === 'boolean') facts.push({ label: 'approvalAccepted', value: String(body.approvalAccepted) });
    const approvalReason = text(body.approvalReason);
    if (approvalReason) facts.push({ label: 'approvalReason', value: approvalReason });
    const paperStatus = text(body.paperStatus);
    if (paperStatus) facts.push({ label: 'paperStatus', value: paperStatus });
    if (typeof body.fillRecorded === 'boolean') facts.push({ label: 'fillRecorded', value: String(body.fillRecorded) });
    if (typeof body.persisted === 'boolean') facts.push({ label: 'persisted', value: String(body.persisted) });
    if (typeof body.riskAllowed === 'boolean') facts.push({ label: 'riskAllowed', value: String(body.riskAllowed) });
    if (Array.isArray(body.paperReasons) && body.paperReasons.length > 0) {
      facts.push({ label: 'paperReasons', value: body.paperReasons.map(String).join(' · ') });
    }
    if (typeof body.decisionPersisted === 'boolean') facts.push({ label: 'decisionPersisted', value: String(body.decisionPersisted) });
    const decisionId = text(body.decisionId);
    if (decisionId) facts.push({ label: 'decisionId', value: decisionId });
    const decisionStatus = text(body.decisionStatus);
    if (decisionStatus) facts.push({ label: 'decisionStatus', value: decisionStatus });
    const operationKey = text(body.operationKey);
    if (operationKey) facts.push({ label: 'operationKey', value: operationKey });
    const taskId = text(body.taskId);
    if (taskId) facts.push({ label: 'taskId', value: taskId });
    const taskStatus = text(body.taskStatus);
    if (taskStatus) facts.push({ label: 'taskStatus', value: taskStatus });
    const taskGoal = text(body.taskGoal);
    if (taskGoal) facts.push({ label: 'taskGoal', value: taskGoal });
    const decisionError = text(body.decisionError);
    if (decisionError) facts.push({ label: 'decisionError', value: decisionError });
    const taskError = text(body.taskError);
    if (taskError) facts.push({ label: 'taskError', value: taskError });
    if (Array.isArray(body.missing)) facts.push({ label: 'missing slots', value: body.missing.map(String).join(', ') });
  } catch {
    facts.push({ label: `${capability} · evidence`, value: 'JSON bằng chứng không đọc được' });
  }
  return facts;
}

function text(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

function storedDecisionFrom(execution: IntentExecutionStep[]): { id: string; status: string; operationKey: string } | null {
  for (const step of execution) {
    if (!step.evidenceJson) continue;
    try {
      const body = JSON.parse(step.evidenceJson) as Record<string, unknown>;
      const id = text(body.decisionId);
      if (!id) continue;
      return {
        id,
        status: text(body.decisionStatus) || '—',
        operationKey: text(body.operationKey),
      };
    } catch {
      /* the fact list reports unreadable evidence */
    }
  }
  return null;
}

export type IntentRuntimeStatus = {
  modelConfigured?: boolean;
  modelName?: string | null;
  publicationConfigured?: boolean;
  publicationAvailability?: string | null;
  publicationId?: string | null;
  sourceLabel?: string | null;
};

export function intentRuntimeNote(status: IntentRuntimeStatus): string {
  const model = status.modelConfigured
    ? `Model đã cấu hình: ${status.modelName?.trim() || 'không ghi tên'}.`
    : 'Model chưa được cấu hình. Gửi yêu cầu sẽ không chạy handler.';
  if (status.sourceLabel?.trim()) {
    const id = status.publicationId?.trim() ? ` (${status.publicationId.trim()})` : '';
    return `${model} Nguồn publication: ${status.sourceLabel.trim()}${id}. Đây là cấu hình local, không phải dữ liệu tenant.`;
  }
  if (status.publicationConfigured) {
    const id = status.publicationId?.trim() ? `: ${status.publicationId.trim()}` : '';
    return `${model} Publication đã cấu hình${id}. Availability ${status.publicationAvailability?.trim() || '—'}.`;
  }
  return `${model} Publication chưa được cấu hình.`;
}
