import { http } from '@/shared/api/http';
import type { CustomerKnowledgeEntryDto } from '@/shared/api/pos-customer-context.types';

export interface CustomerKnowledgeWrite {
  customerId: string;
  kind: string;
  body: string;
  sourceKind: string;
  needsVerification: boolean;
}

export async function listCustomerKnowledge(
  customerId: string,
): Promise<{ entries: CustomerKnowledgeEntryDto[]; storeReady: boolean }> {
  const { data } = await http.get<{ entries: CustomerKnowledgeEntryDto[]; storeReady: boolean }>(
    '/pharmacy/customer-knowledge',
    { params: { customerId } },
  );
  return data;
}

export async function createCustomerKnowledge(input: CustomerKnowledgeWrite): Promise<CustomerKnowledgeEntryDto> {
  const { data } = await http.post<CustomerKnowledgeEntryDto>('/pharmacy/customer-knowledge', input);
  return data;
}

export async function revokeCustomerKnowledge(id: string): Promise<void> {
  await http.post(`/pharmacy/customer-knowledge/${id}/revoke`);
}
