import { http } from '@/shared/api/http';

export type AgentProposalStatus = 'Proposed' | 'Approved' | 'Rejected' | 'Cancelled' | 'Failed';

export type AgentProposal = {
  id: string;
  brandId: string;
  topicId: string;
  title: string;
  targetKey: string;
  targetLabel: string;
  connectorType: string;
  siteId?: string | null;
  channelId?: string | null;
  publishAt: string;
  status: AgentProposalStatus;
  reason: string;
  publishJobId?: string | null;
  decidedBy?: string | null;
  decidedAt?: string | null;
  lastError?: string | null;
  createdAt: string;
};

export type AgentSkipped = { topicId: string; title: string; reason: string };

export type AgentGenerateResult = {
  brandCode: string;
  created: number;
  superseded: number;
  proposals: AgentProposal[];
  skipped: AgentSkipped[];
  warnings: string[];
};

export async function generateAgentProposals(brand: string, days: number, maxPerDay: number) {
  const { data } = await http.post<AgentGenerateResult>('/content/agent/proposals/generate', null, {
    params: { brand, days, maxPerDay },
  });
  return data;
}

export async function fetchAgentProposals(params: { brand?: string; status?: string }) {
  const { data } = await http.get<AgentProposal[]>('/content/agent/proposals', { params });
  return data;
}

export async function approveAgentProposal(id: string, publishAt?: string) {
  const { data } = await http.post<AgentProposal>(`/content/agent/proposals/${id}/approve`, {
    publishAt: publishAt ?? null,
  });
  return data;
}

export async function rejectAgentProposal(id: string, note?: string) {
  const { data } = await http.post<AgentProposal>(`/content/agent/proposals/${id}/reject`, { note: note ?? null });
  return data;
}

export async function cancelAgentProposal(id: string, note?: string) {
  const { data } = await http.post<AgentProposal>(`/content/agent/proposals/${id}/cancel`, { note: note ?? null });
  return data;
}
