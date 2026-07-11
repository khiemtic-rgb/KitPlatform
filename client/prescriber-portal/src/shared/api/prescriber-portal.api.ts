import { prescriberHttp } from '@/shared/api/http';
import type {
  PharmacyDirectoryEntry,
  PortalCustomerItem,
  PortalPrescriberDashboard,
  PortalPrescriptionDetail,
  PortalPrescriptionShare,
  PortalPrescriptionSummary,
  PortalProductItem,
  PrescriberLoginResponse,
  PrescriberOtpSentResponse,
  PrescriberPharmacyLink,
  PrescriberProfile,
} from '@/shared/api/prescriber-portal.types';

export type {
  PharmacyDirectoryEntry,
  PortalCustomerItem,
  PortalPrescriberDashboard,
  PortalPrescriptionDetail,
  PortalPrescriptionShare,
  PortalPrescriptionSummary,
  PortalProductItem,
  PrescriberLoginResponse,
  PrescriberOtpSentResponse,
  PrescriberPharmacyLink,
  PrescriberProfile,
};

export async function requestPrescriberOtp(phone: string): Promise<PrescriberOtpSentResponse> {
  const { data } = await prescriberHttp.post<PrescriberOtpSentResponse>('/auth/otp-request', { phone });
  return data;
}

export async function verifyPrescriberOtp(phone: string, code: string): Promise<PrescriberLoginResponse> {
  const { data } = await prescriberHttp.post<PrescriberLoginResponse>('/auth/otp-verify', { phone, code });
  return data;
}

export async function fetchPrescriberProfile(): Promise<PrescriberProfile> {
  const { data } = await prescriberHttp.get<PrescriberProfile>('/auth/me');
  return data;
}

export async function fetchMyPharmacies(activeOnly = true): Promise<PrescriberPharmacyLink[]> {
  const { data } = await prescriberHttp.get<PrescriberPharmacyLink[]>('/pharmacies', { params: { activeOnly } });
  return data;
}

export async function fetchPendingInvites(): Promise<PrescriberPharmacyLink[]> {
  const { data } = await prescriberHttp.get<PrescriberPharmacyLink[]>('/links/pending-invites');
  return data;
}

export async function searchPharmacyDirectory(query?: string): Promise<PharmacyDirectoryEntry[]> {
  const { data } = await prescriberHttp.get<PharmacyDirectoryEntry[]>('/pharmacies/directory', {
    params: { q: query },
  });
  return data;
}

export async function requestPharmacyLink(tenantCode: string): Promise<PrescriberPharmacyLink> {
  const { data } = await prescriberHttp.post<PrescriberPharmacyLink>('/links/request', { tenantCode });
  return data;
}

export async function acceptPharmacyInvite(linkId: string): Promise<PrescriberPharmacyLink> {
  const { data } = await prescriberHttp.post<PrescriberPharmacyLink>(`/links/${linkId}/accept`);
  return data;
}

export async function rejectPharmacyInvite(linkId: string, reason?: string): Promise<PrescriberPharmacyLink> {
  const { data } = await prescriberHttp.post<PrescriberPharmacyLink>(`/links/${linkId}/reject`, { reason });
  return data;
}

export async function fetchPortalPrescriptions(tenantId?: string): Promise<PortalPrescriptionSummary[]> {
  const { data } = await prescriberHttp.get<PortalPrescriptionSummary[]>('/prescriptions', {
    params: tenantId ? { tenantId } : undefined,
  });
  return data;
}

export async function fetchPortalPrescription(prescriptionId: string): Promise<PortalPrescriptionDetail> {
  const { data } = await prescriberHttp.get<PortalPrescriptionDetail>(`/prescriptions/${prescriptionId}`);
  return data;
}

export async function amendPortalPrescription(
  prescriptionId: string,
  payload: {
    notes?: string;
    lines: Array<{
      productId: string;
      productUnitId?: string | null;
      qtyPrescribed: number;
      dosageInstruction?: string;
    }>;
  },
): Promise<PortalPrescriptionDetail> {
  const { data } = await prescriberHttp.put<PortalPrescriptionDetail>(`/prescriptions/${prescriptionId}`, payload);
  return data;
}

export async function cancelPortalPrescription(
  prescriptionId: string,
  reason?: string,
): Promise<PortalPrescriptionDetail> {
  const { data } = await prescriberHttp.post<PortalPrescriptionDetail>(`/prescriptions/${prescriptionId}/cancel`, {
    reason,
  });
  return data;
}

export async function createPortalPrescription(payload: {
  tenantId: string;
  customerId: string;
  patientName?: string;
  patientPhone?: string;
  notes?: string;
  lines: Array<{
    productId: string;
    productUnitId?: string | null;
    qtyPrescribed: number;
    dosageInstruction?: string;
  }>;
}): Promise<PortalPrescriptionDetail> {
  const { data } = await prescriberHttp.post<PortalPrescriptionDetail>('/prescriptions', payload);
  return data;
}

export async function searchPortalCustomers(tenantId: string, q?: string): Promise<PortalCustomerItem[]> {
  const { data } = await prescriberHttp.get<PortalCustomerItem[]>('/customers', {
    params: { tenantId, q },
  });
  return data;
}

export async function searchPortalProducts(tenantId: string, q?: string): Promise<PortalProductItem[]> {
  const { data } = await prescriberHttp.get<PortalProductItem[]>('/products', {
    params: { tenantId, q },
  });
  return data;
}

export async function fetchPrescriberDashboard(): Promise<PortalPrescriberDashboard> {
  const { data } = await prescriberHttp.get<PortalPrescriberDashboard>('/dashboard');
  return data;
}

export async function fetchPrescriptionShare(prescriptionId: string): Promise<PortalPrescriptionShare> {
  const { data } = await prescriberHttp.get<PortalPrescriptionShare>(`/prescriptions/${prescriptionId}/share`);
  return data;
}

export function getApiErrorMessage(error: unknown, fallback: string): string {
  if (typeof error === 'object' && error !== null && 'response' in error) {
    const response = (error as { response?: { data?: { message?: string } } }).response;
    const message = response?.data?.message;
    if (message) return message;
  }
  if (typeof error === 'object' && error !== null && 'code' in error) {
    const code = String((error as { code?: string }).code ?? '');
    if (code === 'ECONNABORTED' || code === 'ERR_NETWORK') {
      return 'Không kết nối được máy chủ. Thử lại sau vài giây.';
    }
  }
  return fallback;
}
