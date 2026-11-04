import { http } from '@/shared/api/http';

export interface KnowledgeVersion {
  ruleCode: string;
  version: string;
  lifecycleStatus: string;
  source: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  symptomCode?: string | null;
  categoryCodes: string[];
  keywords: string[];
  excludeKeywords: string[];
  reasonVi: string;
  priority: number;
  sourceVerificationStatus?: string;
  referenceSource?: string | null;
  sourceIdentity?: string | null;
  sourceUrl?: string | null;
  documentRef?: string | null;
  issuingOrganization?: string | null;
  sourcePublishedOn?: string | null;
  evidenceNote?: string | null;
  verifiedBy?: string | null;
  verifiedAt?: string | null;
}

export interface KnowledgeAdoption {
  ruleCode: string;
  ruleVersion: string;
  status: string;
  referenceSource?: string | null;
  productMatchingEnabled: boolean;
  extraExcludeKeywords: string[];
  priorityOverride?: number | null;
}

export interface KnowledgeCatalog {
  publishedVersions: KnowledgeVersion[];
  adoptions: KnowledgeAdoption[];
}

export interface KnowledgePublicationEvent {
  ruleCode: string;
  version: string;
  fromStatus: string;
  toStatus: string;
  actorUserId: string;
  createdAt: string;
}

export interface KnowledgeApprovalEvent {
  ruleCode: string;
  version?: string | null;
  fromStatus: string;
  toStatus: string;
  actorUserId: string;
  referenceSource?: string | null;
  createdAt: string;
}

export async function fetchKnowledgeVersions(): Promise<KnowledgeVersion[]> {
  const { data } = await http.get<KnowledgeVersion[]>('/platform/pharmacy-knowledge/versions');
  return data;
}

export async function fetchPublicationHistory(ruleCode?: string): Promise<KnowledgePublicationEvent[]> {
  const { data } = await http.get<KnowledgePublicationEvent[]>('/platform/pharmacy-knowledge/history', {
    params: ruleCode ? { ruleCode } : undefined,
  });
  return data;
}

export async function publishKnowledgeVersion(ruleCode: string, version: string): Promise<KnowledgeVersion> {
  const { data } = await http.post<KnowledgeVersion>(
    `/platform/pharmacy-knowledge/versions/${encodeURIComponent(ruleCode)}/${encodeURIComponent(version)}/publish`,
  );
  return data;
}

export async function retireKnowledgeVersion(ruleCode: string, version: string): Promise<KnowledgeVersion> {
  const { data } = await http.post<KnowledgeVersion>(
    `/platform/pharmacy-knowledge/versions/${encodeURIComponent(ruleCode)}/${encodeURIComponent(version)}/retire`,
  );
  return data;
}

export async function submitKnowledgeReview(ruleCode: string, version: string): Promise<KnowledgeVersion> {
  const { data } = await http.post<KnowledgeVersion>(
    `/platform/pharmacy-knowledge/versions/${encodeURIComponent(ruleCode)}/${encodeURIComponent(version)}/submit-review`,
  );
  return data;
}

export async function returnKnowledgeToDraft(ruleCode: string, version: string): Promise<KnowledgeVersion> {
  const { data } = await http.post<KnowledgeVersion>(
    `/platform/pharmacy-knowledge/versions/${encodeURIComponent(ruleCode)}/${encodeURIComponent(version)}/return-draft`,
  );
  return data;
}

export async function updateKnowledgeContent(
  ruleCode: string,
  version: string,
  body: {
    reasonVi: string;
    keywords: string[];
    excludeKeywords: string[];
    categoryCodes: string[];
  },
): Promise<KnowledgeVersion> {
  const { data } = await http.put<KnowledgeVersion>(
    `/platform/pharmacy-knowledge/versions/${encodeURIComponent(ruleCode)}/${encodeURIComponent(version)}/content`,
    body,
  );
  return data;
}

export async function verifyKnowledgeSource(
  ruleCode: string,
  version: string,
  body: {
    status: 'ACCEPTED' | 'REJECTED';
    referenceSource?: string | null;
    sourceIdentity?: string | null;
    sourceUrl?: string | null;
    documentRef?: string | null;
    issuingOrganization?: string | null;
    sourcePublishedOn?: string | null;
    evidenceNote?: string | null;
  },
): Promise<KnowledgeVersion> {
  const { data } = await http.put<KnowledgeVersion>(
    `/platform/pharmacy-knowledge/versions/${encodeURIComponent(ruleCode)}/${encodeURIComponent(version)}/source`,
    body,
  );
  return data;
}

export async function fetchPharmacyKnowledgeCatalog(): Promise<KnowledgeCatalog> {
  const { data } = await http.get<KnowledgeCatalog>('/pharmacy/knowledge-rules');
  return data;
}

export async function fetchPharmacyKnowledgeVersion(ruleCode: string, version: string): Promise<KnowledgeVersion> {
  const { data } = await http.get<KnowledgeVersion>(
    `/pharmacy/knowledge-rules/${encodeURIComponent(ruleCode)}/versions/${encodeURIComponent(version)}`,
  );
  return data;
}

export async function pinKnowledgeVersion(
  ruleCode: string,
  version: string,
  referenceSource: string,
): Promise<void> {
  await http.post(`/pharmacy/knowledge-rules/${encodeURIComponent(ruleCode)}/approval`, {
    status: 'APPROVED',
    referenceSource,
    version,
  });
}

export async function retireKnowledgePin(ruleCode: string, version: string): Promise<void> {
  await http.post(`/pharmacy/knowledge-rules/${encodeURIComponent(ruleCode)}/approval`, {
    status: 'RETIRED',
    referenceSource: null,
    version,
  });
}

export async function saveKnowledgePolicy(
  ruleCode: string,
  body: {
    productMatchingEnabled: boolean;
    extraExcludeKeywords: string[];
    priorityOverride: number | null;
  },
): Promise<void> {
  await http.post(`/pharmacy/knowledge-rules/${encodeURIComponent(ruleCode)}/policy`, {
    ...body,
    addsKeywords: false,
    addsCategories: false,
  });
}

export async function fetchKnowledgeApprovalHistory(ruleCode: string): Promise<KnowledgeApprovalEvent[]> {
  const { data } = await http.get<KnowledgeApprovalEvent[]>(
    `/pharmacy/knowledge-rules/${encodeURIComponent(ruleCode)}/approval`,
  );
  return data;
}
