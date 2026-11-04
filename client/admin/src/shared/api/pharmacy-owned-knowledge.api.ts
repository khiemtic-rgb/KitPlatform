import { http } from '@/shared/api/http';

export interface OwnedKnowledgeSummary {
  id: string;
  kind: string;
  title: string;
  classification?: string | null;
  version: number;
  lifecycleStatus: string;
  updatedAt: string;
  applicationStatus?: string | null;
  reviewDueOn?: string | null;
  reviewOverdue?: boolean;
}

export interface OwnedKnowledgeEvent {
  id: string;
  action: string;
  version: number;
  kind: string;
  title: string;
  body?: string | null;
  classification?: string | null;
  snapshotFidelity: 'EXACT' | 'LEGACY_UNAVAILABLE';
  actorUserId: string;
  createdAt: string;
  note?: string | null;
}

export interface OwnedKnowledgeVersionBrief {
  versionId: string;
  version: number;
  kind: string;
  title: string;
  lifecycleStatus: string;
  updatedAt: string;
  body?: string | null;
}

export interface OwnedKnowledgeActivation {
  versionId: string;
  version: number;
  status: string;
}

export interface OwnedKnowledgeDetail extends OwnedKnowledgeSummary {
  body: string;
  createdAt: string;
  history: OwnedKnowledgeEvent[];
  versions?: OwnedKnowledgeVersionBrief[];
  activations?: OwnedKnowledgeActivation[];
}

export interface OwnedKnowledgeWrite {
  kind: string;
  title: string;
  body: string;
  classification?: string | null;
}

export async function listOwnedKnowledge(filter?: {
  kind?: string;
  q?: string;
  status?: string;
  classification?: string;
}) {
  const { data } = await http.get<OwnedKnowledgeSummary[]>('/pharmacy/owned-knowledge', {
    params: {
      kind: filter?.kind || undefined,
      q: filter?.q || undefined,
      status: filter?.status || undefined,
      classification: filter?.classification || undefined,
    },
  });
  return data;
}

export interface OwnedKnowledgeInUse {
  id: string;
  kind: string;
  title: string;
  classification?: string | null;
  version: number;
  status: string;
  body: string;
  updatedAt: string;
  reviewDueOn?: string | null;
  reviewOverdue?: boolean;
}

export async function listOwnedKnowledgeInUse(filter?: {
  kind?: string;
  q?: string;
  classification?: string;
}) {
  const { data } = await http.get<OwnedKnowledgeInUse[]>('/pharmacy/owned-knowledge/in-use', {
    params: {
      kind: filter?.kind || undefined,
      q: filter?.q || undefined,
      classification: filter?.classification || undefined,
    },
  });
  return data;
}

export async function getOwnedKnowledgeInUse(id: string) {
  const { data } = await http.get<OwnedKnowledgeInUse>(`/pharmacy/owned-knowledge/in-use/${id}`);
  return data;
}

export async function getOwnedKnowledge(id: string) {
  const { data } = await http.get<OwnedKnowledgeDetail>(`/pharmacy/owned-knowledge/${id}`);
  return data;
}

export async function createOwnedKnowledge(input: OwnedKnowledgeWrite) {
  const { data } = await http.post<OwnedKnowledgeDetail>('/pharmacy/owned-knowledge', input);
  return data;
}

export async function updateOwnedKnowledge(id: string, input: OwnedKnowledgeWrite) {
  const { data } = await http.put<OwnedKnowledgeDetail>(`/pharmacy/owned-knowledge/${id}`, input);
  return data;
}

export async function transitionOwnedKnowledge(
  id: string,
  action: 'submit' | 'return' | 'approve' | 'activate' | 'stop' | 'resume' | 'next-draft',
  input: { versionId: string; expectedUpdatedAt: string; note?: string | null; reviewDueOn?: string | null },
) {
  const { data } = await http.post<OwnedKnowledgeDetail>(`/pharmacy/owned-knowledge/${id}/${action}`, input);
  return data;
}

export interface KnowledgeFeedbackCount {
  knowledgeId: string;
  openCount: number;
  notHelpfulCount: number;
  missingCount: number;
}

export interface KnowledgeFeedbackSummary {
  items: KnowledgeFeedbackCount[];
  recentMissing: { query: string; createdAt: string }[];
  storeReady: boolean;
}

export async function sendKnowledgeFeedback(input: {
  knowledgeId?: string | null;
  kind: 'missing' | 'not_helpful';
  query?: string | null;
}) {
  await http.post('/pharmacy/owned-knowledge/feedback', input);
}

export async function getKnowledgeFeedbackSummary() {
  const { data } = await http.get<KnowledgeFeedbackSummary>('/pharmacy/owned-knowledge/feedback/summary');
  return data;
}

/** Shared tag text for items past their review date. They stay visible. */
export const REVIEW_OVERDUE_LABEL = 'Quá hạn rà soát';
