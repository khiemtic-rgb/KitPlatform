import { http } from '@/shared/api/http';

export type LearningProgramListItem = {
  id: string;
  code: string;
  packCode: string;
  title: string;
  summary?: string | null;
  locale: string;
  version: number;
  moduleCount: number;
  sortOrder: number;
};

export type LearningModuleListItem = {
  id: string;
  code: string;
  title: string;
  summary?: string | null;
  durationMinutes: number;
  levelCode: string;
  competencyCodes: string[];
  sortOrder: number;
  passScorePct: number;
  requireAck: boolean;
  questionCount: number;
};

export type LearningProgramDetail = {
  id: string;
  code: string;
  packCode: string;
  title: string;
  summary?: string | null;
  locale: string;
  version: number;
  modules: LearningModuleListItem[];
};

export type LearningEnrollment = {
  id: string;
  programId: string;
  programTitle: string;
  programCode: string;
  employeeId: string;
  employeeName: string;
  status: string;
  assignedAt: string;
  startedAt?: string | null;
  completedAt?: string | null;
  modulesTotal: number;
  modulesPassed: number;
};

export type LearningCompetencyRosterItem = {
  employeeId: string;
  employeeName: string;
  credentialCount: number;
  modulesPassed: number;
  modulesTotal: number;
  enrollmentStatus?: string | null;
  competencyCodes: string[];
};

export type LearningGateCheck = {
  permissionCode: string;
  mode: string;
  satisfied: boolean;
  hasOverride: boolean;
  requiredCompetencies: string[];
  missingCompetencies: string[];
  message?: string | null;
};

export async function fetchLearningPrograms() {
  const { data } = await http.get<LearningProgramListItem[]>('/learning/programs');
  return data;
}

export async function fetchLearningProgram(id: string) {
  const { data } = await http.get<LearningProgramDetail>(`/learning/programs/${id}`);
  return data;
}

export type LearningModuleDetail = {
  id: string;
  programId: string;
  code: string;
  title: string;
  summary?: string | null;
  bodyMarkdown: string;
  durationMinutes: number;
  levelCode: string;
  competencyCodes: string[];
  passScorePct: number;
  requireAck: boolean;
  questions: { id: string; sortOrder: number; prompt: string; options: string[] }[];
  isTenantCustomized?: boolean;
  requireObservation?: boolean;
  /** Lần làm bài mà server đã dùng để xáo câu/đáp án — gửi lại khi nộp. */
  quizVariant?: number | null;
};

export async function fetchLearningModule(id: string) {
  const { data } = await http.get<LearningModuleDetail>(`/learning/modules/${id}`);
  return data;
}

/** —— Learner (NV tự học + quiz trên Admin hoặc Staff) —— */

export type LearningModuleProgress = {
  moduleId: string;
  moduleCode: string;
  title: string;
  levelCode: string;
  sortOrder: number;
  status: string;
  scorePct?: number | null;
  quizAttempts: number;
  acknowledgedAt?: string | null;
  acknowledgeSelfieUrl?: string | null;
  requireAck: boolean;
  requireObservation?: boolean;
  observedAt?: string | null;
  observerName?: string | null;
};

export type LearningMyLearning = {
  enrollment?: {
    id: string;
    programId: string;
    programTitle: string;
    status: string;
    modulesTotal: number;
    modulesPassed: number;
  } | null;
  modules: LearningModuleProgress[];
  credentials?: { competencyCode: string; levelCode: string; scorePct?: number | null }[];
  /** Mọi lộ trình của cửa hàng (enrollment null = chưa ghi danh). */
  programs?: {
    program: LearningProgramListItem;
    enrollment?: LearningMyLearning['enrollment'];
    modules: LearningModuleProgress[];
  }[];
};

export type LearningQuizResult = {
  passed: boolean;
  scorePct: number;
  passScorePct: number;
  progressStatus: string;
};

export async function fetchMyLearning() {
  const { data } = await http.get<LearningMyLearning>('/learning/me');
  return data;
}

export async function enrollLearningProgram(programId: string) {
  const { data } = await http.post(`/learning/me/enroll/${programId}`);
  return data;
}

export async function startLearningModule(id: string) {
  await http.post(`/learning/modules/${id}/start`);
}

export async function acknowledgeLearningModule(id: string, selfieUrl?: string | null) {
  await http.post(`/learning/modules/${id}/acknowledge`, { selfieUrl: selfieUrl ?? null });
}

export async function uploadLearningAckSelfie(file: File): Promise<string> {
  const form = new FormData();
  form.append('file', file);
  const { data } = await http.post<{ url: string }>('/learning/upload-ack-selfie', form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return data.url;
}

export async function submitLearningQuiz(
  id: string,
  answers: number[],
  opts?: { practice?: boolean; questionIds?: string[]; quizVariant?: number | null },
) {
  const { data } = await http.post<LearningQuizResult>(`/learning/modules/${id}/quiz`, {
    answers,
    practice: Boolean(opts?.practice),
    questionIds: opts?.questionIds,
    quizVariant: opts?.quizVariant ?? undefined,
  });
  return data;
}

export type LearningMonthlyDrillStatus = {
  periodYear: number;
  periodMonth: number;
  eligible: boolean;
  completed: boolean;
  completedAt?: string | null;
  scorePct?: number | null;
  passedModuleCount: number;
  hint?: string | null;
};

export type LearningMonthlyDrillQuestion = {
  id: string;
  prompt: string;
  options: string[];
  moduleTitle: string;
  levelCode: string;
};

export async function fetchMonthlyDrillStatus() {
  const { data } = await http.get<LearningMonthlyDrillStatus>('/learning/me/monthly-drill');
  return data;
}

export async function startMonthlyDrill() {
  const { data } = await http.post<{
    periodYear: number;
    periodMonth: number;
    questions: LearningMonthlyDrillQuestion[];
  }>('/learning/me/monthly-drill/start');
  return data;
}

export async function submitMonthlyDrill(answers: { questionId: string; selectedIndex: number }[]) {
  const { data } = await http.post<{
    passed: boolean;
    scorePct: number;
    passScorePct: number;
    correctCount: number;
    questionCount: number;
    alreadyCompleted: boolean;
  }>('/learning/me/monthly-drill/submit', { answers });
  return data;
}

export type LearningObservationPending = {
  employeeId: string;
  employeeName: string;
  moduleId: string;
  moduleTitle: string;
  moduleCode: string;
  levelCode: string;
  scorePct?: number | null;
  completedAt?: string | null;
};

export type LearningObservation = {
  id: string;
  employeeId: string;
  employeeName: string;
  moduleId: string;
  moduleTitle: string;
  criteria: Record<string, boolean>;
  note?: string | null;
  observedAt: string;
  observerName: string;
  /** false = lần quan sát chưa đạt (vẫn lưu lịch sử). */
  passed?: boolean;
  scorePct?: number | null;
};

export async function fetchPendingLearningObservations() {
  const { data } = await http.get<LearningObservationPending[]>('/learning/observations/pending');
  return data;
}

export async function submitLearningObservation(payload: {
  employeeId: string;
  moduleId: string;
  criteria: Record<string, boolean>;
  note?: string | null;
}) {
  const { data } = await http.post<LearningObservation>('/learning/observations', payload);
  return data;
}

export async function fetchMyModuleObservation(moduleId: string) {
  const { data } = await http.get<LearningObservation>(`/learning/modules/${moduleId}/my-observation`);
  return data;
}

export async function upsertLearningModuleTenantContent(
  id: string,
  payload: { title?: string | null; summary?: string | null; bodyMarkdown: string },
) {
  const { data } = await http.put<LearningModuleDetail>(`/learning/modules/${id}/tenant-content`, payload);
  return data;
}

export async function revertLearningModuleTenantContent(id: string) {
  const { data } = await http.delete<LearningModuleDetail>(`/learning/modules/${id}/tenant-content`);
  return data;
}

export async function fetchLearningEnrollments() {
  const { data } = await http.get<LearningEnrollment[]>('/learning/enrollments');
  return data;
}

export async function fetchLearningRoster() {
  const { data } = await http.get<LearningCompetencyRosterItem[]>('/learning/roster');
  return data;
}

export async function assignLearningProgram(employeeId: string, programId: string) {
  const { data } = await http.post<LearningEnrollment>('/learning/enrollments', {
    employeeId,
    programId,
  });
  return data;
}

export async function checkLearningGate(permission: string) {
  const { data } = await http.get<LearningGateCheck>('/learning/gates/check', {
    params: { permission },
  });
  return data;
}

export async function createLearningGateOverride(payload: {
  employeeId: string;
  permissionCode: string;
  reason: string;
  expiresAt?: string | null;
}) {
  await http.post('/learning/gates/overrides', payload);
}

export type LearningEvaluation = {
  id: string;
  employeeId: string;
  employeeName: string;
  periodYear: number;
  periodMonth: number;
  scoreKnowledge: number;
  scoreAttitude: number;
  scoreCare: number;
  scoreStock: number;
  scoreDiscipline: number;
  averageScore: number;
  comment?: string | null;
  reviewedAt: string;
  employeeFeedback?: string | null;
  nextMonthGoal?: string | null;
  employeeRespondedAt?: string | null;
  engagementPulse?: number | null;
};

export async function fetchLearningEvaluations(year?: number, month?: number) {
  const { data } = await http.get<LearningEvaluation[]>('/learning/evaluations', {
    params: { year, month },
  });
  return data;
}

export async function fetchMyEvaluations() {
  const { data } = await http.get<LearningEvaluation[]>('/learning/me/evaluations');
  return data;
}

export async function submitMyEvaluationFeedback(
  id: string,
  payload: {
    employeeFeedback?: string;
    nextMonthGoal?: string;
    engagementPulse?: number | null;
  },
) {
  const { data } = await http.put<LearningEvaluation>(
    `/learning/evaluations/${id}/my-feedback`,
    payload,
  );
  return data;
}

export async function upsertLearningEvaluation(payload: {
  employeeId: string;
  periodYear: number;
  periodMonth: number;
  scoreKnowledge: number;
  scoreAttitude: number;
  scoreCare: number;
  scoreStock: number;
  scoreDiscipline: number;
  comment?: string;
}) {
  const { data } = await http.post<LearningEvaluation>('/learning/evaluations', payload);
  return data;
}

export type LearningRecognition = {
  id: string;
  employeeId: string;
  employeeName: string;
  kind: string;
  title: string;
  body?: string | null;
  badgeCode?: string | null;
  createdAt: string;
  /** Sao 1–5 từ phản hồi app khách (nếu có). */
  customerRating?: number | null;
  /** false = góp ý riêng: chỉ người nhận + quản lý thấy. */
  isPublic?: boolean;
};

export type LearningBadge = {
  badgeCode: string;
  title: string;
  earnedAt: string;
};

export async function fetchLearningRecognitions(
  take = 30,
  opts?: { scope?: 'me' | 'team' },
) {
  const { data } = await http.get<LearningRecognition[]>('/learning/recognitions', {
    params: {
      take,
      scope: opts?.scope === 'me' ? 'me' : undefined,
    },
  });
  return data;
}

export type LearningCustomerFeedback = {
  id: string;
  employeeId?: string | null;
  employeeName: string;
  rating: number;
  comment?: string | null;
  createdAt: string;
  recognitionId?: string | null;
};

export async function fetchRecentCustomerFeedback(hours = 48, take = 20) {
  const { data } = await http.get<LearningCustomerFeedback[]>(
    '/learning/customer-feedback/recent',
    { params: { hours, take } },
  );
  return data;
}

export async function createLearningRecognition(payload: {
  employeeId: string;
  kind: string;
  title: string;
  body?: string;
  badgeCode?: string | null;
  isPublic?: boolean;
  /** Sao 1–5 khi ghi nhận khen khách (tay hoặc từ app). */
  customerRating?: number | null;
}) {
  const { data } = await http.post<LearningRecognition>('/learning/recognitions', payload);
  return data;
}

export async function fetchMyLearningBadges() {
  const { data } = await http.get<LearningBadge[]>('/learning/me/badges');
  return data;
}

export type LearningCareerLevel = {
  id: string;
  code: string;
  title: string;
  summary?: string | null;
  sortOrder: number;
  minMonthsTenure: number;
  minAvgEvaluate: number;
  requiredCompetencyCodes: string[];
};

export type LearningCareerRosterItem = {
  employeeId: string;
  employeeName: string;
  currentLevelId?: string | null;
  currentLevelCode?: string | null;
  currentLevelTitle?: string | null;
  nextLevelId?: string | null;
  nextLevelCode?: string | null;
  nextLevelTitle?: string | null;
  eligibleForNext: boolean;
  missingReasons: string[];
  tenureMonths: number;
  latestAvgEvaluate?: number | null;
  credentialCount: number;
};

export type LearningCareerPromotion = {
  id: string;
  employeeId: string;
  employeeName: string;
  fromLevelTitle?: string | null;
  toLevelTitle: string;
  status: string;
  eligibilityOk: boolean;
  missingReasons: string[];
  comment?: string | null;
  decidedAt: string;
};

export async function fetchCareerLevels() {
  const { data } = await http.get<LearningCareerLevel[]>('/learning/career/levels');
  return data;
}

export async function fetchCareerRoster() {
  const { data } = await http.get<LearningCareerRosterItem[]>('/learning/career/roster');
  return data;
}

export async function fetchCareerPromotions(take = 30) {
  const { data } = await http.get<LearningCareerPromotion[]>('/learning/career/promotions', {
    params: { take },
  });
  return data;
}

export async function promoteCareer(payload: {
  employeeId: string;
  toLevelId: string;
  comment?: string;
  force?: boolean;
}) {
  const { data } = await http.post<LearningCareerPromotion>('/learning/career/promotions', payload);
  return data;
}

export type LearningPeopleDashboard = {
  employeeCount: number;
  enrolledCount: number;
  completedEnrollmentCount: number;
  modulesPassedTotal: number;
  modulesTotalAssigned: number;
  trainingCompletionPct: number;
  credentialCount: number;
  avgEvaluateScore?: number | null;
  evaluationsThisMonth: number;
  recognitionCount30d: number;
  badgeCount: number;
  careerLevelCounts: { levelCode: string; levelTitle: string; employeeCount: number }[];
  unevaluatedThisMonth: number;
  missingPosBasicCount: number;
  eligiblePromotionCount: number;
  actionItems: string[];
  modulesPassedThisWeek?: number;
  perfectScoresThisWeek?: number;
  recognitionsThisWeek?: number;
  promotionsThisWeek?: number;
  celebrationItems?: string[];
  pendingFeedbackCount?: number;
  missingCloseChecklistBranchesToday?: number;
};

export async function fetchPeopleDashboard() {
  const { data } = await http.get<LearningPeopleDashboard>('/learning/people/dashboard');
  return data;
}

export type LearningEmployeeEvidence = {
  employeeId: string;
  employeeName: string;
  modulesPassed: number;
  modulesTotal: number;
  enrollmentStatus?: string | null;
  competencyCodes: string[];
  hasPosBasic: boolean;
  currentLevelTitle?: string | null;
  nextLevelTitle?: string | null;
  eligibleForNext: boolean;
  careerMissingReasons: string[];
  tenureMonths: number;
  suggestedKnowledge?: number | null;
  suggestedAttitude?: number | null;
  suggestedCare?: number | null;
  suggestedStock?: number | null;
  suggestedDiscipline?: number | null;
  suggestionNote: string;
  closeChecklistDaysThisMonth?: number;
  closeStreakDays?: number;
  orderCountThisMonth?: number | null;
  salesNetThisMonth?: number | null;
  latestEngagementPulse?: number | null;
  /** Căn cứ từng điểm gợi ý (luật cố định từ dữ liệu, không phải AI). */
  suggestionBasis?: {
    criterion: 'knowledge' | 'attitude' | 'care' | 'stock' | 'discipline';
    score: number;
    reason: string;
  }[];
};

export type LearningWeakQuestion = {
  questionId: string;
  moduleId: string;
  moduleTitle: string;
  prompt: string;
  answered: number;
  wrong: number;
  wrongPct: number;
};

export type LearningEmployeeGap = {
  employeeId: string;
  employeeName: string;
  moduleId: string;
  moduleTitle: string;
  signal: 'quiz_failed' | 'observation_failed';
  failCount: number;
  lastAt: string;
  suggestedAction: string;
};

export type LearningModuleOutcome = {
  employeeId: string;
  employeeName: string;
  moduleId: string;
  moduleTitle: string;
  passedOn: string;
  ordersPerDayBefore: number;
  ordersPerDayAfter: number;
  daysAfter: number;
};

export type LearningGapReport = {
  days: number;
  weakQuestions: LearningWeakQuestion[];
  employeeGaps: LearningEmployeeGap[];
  outcomes: LearningModuleOutcome[];
};

export async function fetchLearningGapReport(days?: number) {
  const { data } = await http.get<LearningGapReport>('/learning/gaps', {
    params: days ? { days } : undefined,
  });
  return data;
}

export async function fetchEmployeeEvidence(employeeId: string) {
  const { data } = await http.get<LearningEmployeeEvidence>(
    `/learning/employees/${employeeId}/evidence`,
  );
  return data;
}

export type LearningMyHabits = {
  closeChecklistDaysThisMonth: number;
  closeStreakDays: number;
  closedToday: boolean;
  openedToday: boolean;
  tenureMonths: number;
  hasTenure12Badge: boolean;
  hasCloseStreak7Badge: boolean;
  tips: string[];
};

export async function fetchMyHabits() {
  const { data } = await http.get<LearningMyHabits>('/learning/me/habits');
  return data;
}

/** Private coaching mail (People mailbox). */
export type LearningMailThreadListItem = {
  id: string;
  subject: string;
  createdByUserId: string;
  createdByName: string;
  recipientEmployeeId: string;
  recipientEmployeeName: string;
  unreadCount: number;
  updatedAt: string;
  createdAt: string;
};

export type LearningMailMessage = {
  id: string;
  body: string;
  senderName: string;
  isMine: boolean;
  createdAt: string;
};

export type LearningMailThreadDetail = LearningMailThreadListItem & {
  relatedEventLabel?: string | null;
  messages: LearningMailMessage[];
};

export type LearningMailCreateResult = {
  createdCount: number;
  threads: LearningMailThreadListItem[];
};

export async function fetchLearningMailThreads() {
  const { data } = await http.get<LearningMailThreadListItem[]>('/learning/mail/threads');
  return data ?? [];
}

export async function fetchLearningMailUnreadCount() {
  const { data } = await http.get<{ unreadCount?: number; count?: number } | number>(
    '/learning/mail/unread-count',
  );
  if (typeof data === 'number') return data;
  return data?.unreadCount ?? data?.count ?? 0;
}

export async function fetchLearningMailThread(id: string) {
  const { data } = await http.get<LearningMailThreadDetail>(`/learning/mail/threads/${id}`);
  return data;
}

export async function markLearningMailThreadRead(id: string) {
  await http.post(`/learning/mail/threads/${id}/read`);
}

export async function createLearningMailThread(payload: {
  recipientEmployeeIds: string[];
  subject: string;
  body: string;
  relatedRecognitionId?: string | null;
  relatedFeedbackId?: string | null;
}) {
  const { data } = await http.post<LearningMailCreateResult>('/learning/mail/threads', payload);
  return data;
}

export async function replyLearningMailThread(threadId: string, body: string) {
  await http.post(`/learning/mail/threads/${threadId}/messages`, { body });
}

export type OperationalTask = {
  id: string;
  branchId?: string | null;
  salesShiftId?: string | null;
  assigneeEmployeeId: string;
  assigneeName: string;
  title: string;
  dueAt?: string | null;
  status: string;
  evidenceNote?: string | null;
  createdByUserId: string;
  completedAt?: string | null;
  createdAt: string;
  isOverdue?: boolean;
};

export type OperationalShiftOption = {
  id: string;
  shiftNumber: string;
  warehouseName: string;
  branchId: string;
};

export async function fetchOperationalTasks(salesShiftId?: string, overdue?: boolean) {
  const params: Record<string, string | boolean> = {};
  if (salesShiftId) params.salesShiftId = salesShiftId;
  if (overdue) params.overdue = true;
  const { data } = await http.get<OperationalTask[]>('/learning/operational-tasks', {
    params: Object.keys(params).length ? params : undefined,
  });
  return data;
}

export async function fetchOperationalOpenShifts() {
  const { data } = await http.get<OperationalShiftOption[]>('/learning/operational-tasks/open-shifts');
  return data;
}

export async function createOperationalTask(payload: {
  assigneeEmployeeId: string;
  title: string;
  salesShiftId?: string | null;
  branchId?: string | null;
  dueAt?: string | null;
}) {
  const { data } = await http.post<OperationalTask>('/learning/operational-tasks', payload);
  return data;
}

export async function updateOperationalTaskStatus(
  id: string,
  status: string,
  evidenceNote?: string,
) {
  const { data } = await http.post<OperationalTask>(`/learning/operational-tasks/${id}/status`, {
    status,
    evidenceNote: evidenceNote ?? null,
  });
  return data;
}

