import { http } from '@/shared/api/http';

export interface KitSalesHealth {
  pack: string;
  version: string;
  ok: boolean;
}

export interface KitSalesProduct {
  code: string;
  displayName: string;
  status: string;
}

export interface KitSalesBusiness {
  id: string;
  name: string;
  businessType: string;
  province?: string | null;
  phone?: string | null;
  status: string;
  source?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface KitSalesLead {
  id: string;
  businessId: string;
  businessName: string;
  productCode: string;
  leadStatus: string;
  leadTemperature: string;
  totalScore: number;
  source?: string | null;
  ownerUserId?: string | null;
  nextActionCode?: string | null;
  nextActionAt?: string | null;
  lastInteractionAt?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface KitSalesPipelineBucket {
  status: string;
  count: number;
}

export interface KitSalesPipelineSummary {
  totalLeads: number;
  byStatus: KitSalesPipelineBucket[];
}

export interface CreateKitSalesProspectBody {
  businessName: string;
  productCode?: string;
  businessType?: string;
  province?: string;
  phone?: string;
  source?: string;
  notes?: string;
}

export async function fetchKitSalesHealth(): Promise<KitSalesHealth> {
  const { data } = await http.get<KitSalesHealth>('/kit-sales/health');
  return data;
}

export async function fetchKitSalesProducts(): Promise<KitSalesProduct[]> {
  const { data } = await http.get<KitSalesProduct[]>('/kit-sales/products');
  return data;
}

export async function fetchKitSalesLeads(params?: {
  status?: string;
  limit?: number;
}): Promise<KitSalesLead[]> {
  const { data } = await http.get<KitSalesLead[]>('/kit-sales/leads', { params });
  return data;
}

export async function fetchKitSalesPipelineSummary(): Promise<KitSalesPipelineSummary> {
  const { data } = await http.get<KitSalesPipelineSummary>('/kit-sales/pipeline/summary');
  return data;
}

export async function createKitSalesProspect(
  body: CreateKitSalesProspectBody,
): Promise<KitSalesLead> {
  const { data } = await http.post<KitSalesLead>('/kit-sales/prospects', body);
  return data;
}
