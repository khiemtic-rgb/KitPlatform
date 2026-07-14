import { http } from '@/shared/api/http';

export interface CustomerGroup {
  id: string;
  groupCode: string;
  groupName: string;
  discountPercent: number;
  status: number;
}

function normalize(row: Record<string, unknown>): CustomerGroup {
  return {
    id: String(row.id ?? row.Id),
    groupCode: String(row.groupCode ?? row.GroupCode ?? ''),
    groupName: String(row.groupName ?? row.GroupName ?? ''),
    discountPercent: Number(row.discountPercent ?? row.DiscountPercent ?? 0),
    status: Number(row.status ?? row.Status ?? 1),
  };
}

export async function fetchCustomerGroups(activeOnly = false): Promise<CustomerGroup[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/customer-groups', {
    params: { activeOnly },
  });
  return (data ?? []).map((row) => normalize(row));
}

export async function createCustomerGroup(payload: {
  groupCode: string;
  groupName: string;
  discountPercent?: number;
}): Promise<CustomerGroup> {
  const { data } = await http.post<Record<string, unknown>>('/customer-groups', payload);
  return normalize(data);
}

export async function updateCustomerGroup(
  id: string,
  payload: { groupName: string; discountPercent: number; status: number },
): Promise<CustomerGroup> {
  const { data } = await http.put<Record<string, unknown>>(`/customer-groups/${id}`, payload);
  return normalize(data);
}

export async function deleteCustomerGroup(id: string): Promise<void> {
  await http.delete(`/customer-groups/${id}`);
}
