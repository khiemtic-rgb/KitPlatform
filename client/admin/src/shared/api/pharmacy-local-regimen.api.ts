import { http } from '@/shared/api/http';

export interface LocalRegimenItem {
  id?: string;
  ingredient: string;
  strength?: string | null;
  dosageForm?: string | null;
  productId?: string | null;
  catalogProductName?: string | null;
  catalogProductCode?: string | null;
  productLabel?: string | null;
  matchStatus: string;
  validChoice?: boolean;
}

export interface LocalRegimenSummary {
  id: string;
  versionId: string;
  version: number;
  title: string;
  conditionGroup: string;
  lifecycleStatus: string;
  activationStatus?: string | null;
  updatedAt: string;
  reviewDueOn?: string | null;
  reviewOverdue?: boolean;
}

export interface LocalRegimenDetail extends LocalRegimenSummary {
  audience: string;
  symptoms: string;
  applicability: string;
  limits: string;
  createdBy: string;
  approvedBy?: string | null;
  items: LocalRegimenItem[];
  events: { action: string; version: number; actorUserId: string; note?: string | null; createdAt: string }[];
}

export interface LocalRegimenInUse {
  id: string;
  versionId: string;
  version: number;
  title: string;
  conditionGroup: string;
  audience: string;
  symptoms: string;
  applicability: string;
  limits: string;
  items: LocalRegimenItem[];
  reviewDueOn?: string | null;
  reviewOverdue?: boolean;
}

export interface LocalRegimenWrite {
  title: string;
  conditionGroup: string;
  audience: string;
  symptoms: string;
  applicability: string;
  limits: string;
  items: LocalRegimenItem[];
}

export async function listLocalRegimens(): Promise<LocalRegimenSummary[]> {
  const { data } = await http.get<LocalRegimenSummary[]>('/pharmacy/local-regimens');
  return data;
}

export async function getLocalRegimen(id: string): Promise<LocalRegimenDetail> {
  const { data } = await http.get<LocalRegimenDetail>(`/pharmacy/local-regimens/${id}`);
  return data;
}

export async function listLocalRegimensInUse(q: string): Promise<{ items: LocalRegimenInUse[]; storeReady: boolean }> {
  const { data } = await http.get<{ items: LocalRegimenInUse[]; storeReady: boolean }>('/pharmacy/local-regimens/in-use', {
    params: { q: q || undefined },
  });
  return data;
}

export async function createLocalRegimen(input: LocalRegimenWrite): Promise<LocalRegimenDetail> {
  const { data } = await http.post<LocalRegimenDetail>('/pharmacy/local-regimens', input);
  return data;
}

export async function updateLocalRegimen(id: string, input: LocalRegimenWrite): Promise<LocalRegimenDetail> {
  const { data } = await http.put<LocalRegimenDetail>(`/pharmacy/local-regimens/${id}`, input);
  return data;
}

export async function transitionLocalRegimen(
  id: string,
  action: 'submit' | 'return' | 'approve' | 'activate' | 'stop' | 'next-draft',
  versionId: string,
  expectedUpdatedAt: string,
  note?: string,
  reviewDueOn?: string | null,
): Promise<LocalRegimenDetail> {
  const { data } = await http.post<LocalRegimenDetail>(`/pharmacy/local-regimens/${id}/${action}`, {
    versionId,
    expectedUpdatedAt,
    note,
    reviewDueOn: reviewDueOn || undefined,
  });
  return data;
}
