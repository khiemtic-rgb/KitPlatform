import { http } from '@/shared/api/http';

type UnknownRow = Record<string, unknown>;

function optionalString(value: unknown): string | undefined {
  if (value == null) return undefined;
  const text = String(value).trim();
  return text ? text : undefined;
}

function asArray(value: unknown): UnknownRow[] {
  return Array.isArray(value) ? (value as UnknownRow[]) : [];
}

function normalizePrescriber(row: UnknownRow): RxPrescriber {
  return {
    id: String(row.id ?? row.Id),
    fullName: String(row.fullName ?? row.FullName ?? ''),
    licenseNumber: optionalString(row.licenseNumber ?? row.LicenseNumber),
    phone: optionalString(row.phone ?? row.Phone),
    specialty: optionalString(row.specialty ?? row.Specialty),
    status: Number(row.status ?? row.Status ?? 1),
    notes: optionalString(row.notes ?? row.Notes),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    updatedAt: String(row.updatedAt ?? row.UpdatedAt ?? ''),
  };
}

function normalizePrescriptionLine(row: UnknownRow): RxPrescriptionLine {
  return {
    id: String(row.id ?? row.Id),
    productId: String(row.productId ?? row.ProductId),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    productUnitId: optionalString(row.productUnitId ?? row.ProductUnitId),
    unitName: optionalString(row.unitName ?? row.UnitName),
    lineDispensingClass: String(row.lineDispensingClass ?? row.LineDispensingClass ?? 'otc'),
    qtyPrescribed: Number(row.qtyPrescribed ?? row.QtyPrescribed ?? 0),
    qtyDispensed: Number(row.qtyDispensed ?? row.QtyDispensed ?? 0),
    qtyRemaining: Number(row.qtyRemaining ?? row.QtyRemaining ?? 0),
    dosageInstruction: optionalString(row.dosageInstruction ?? row.DosageInstruction),
    sortOrder: Number(row.sortOrder ?? row.SortOrder ?? 0),
  };
}

function normalizeAttachment(row: UnknownRow): RxPrescriptionAttachment {
  return {
    id: String(row.id ?? row.Id),
    fileUrl: String(row.fileUrl ?? row.FileUrl ?? ''),
    fileName: optionalString(row.fileName ?? row.FileName),
    uploadedBy: optionalString(row.uploadedBy ?? row.UploadedBy),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
  };
}

function normalizePrescriptionListItem(row: UnknownRow): RxPrescriptionListItem {
  return {
    id: String(row.id ?? row.Id),
    prescriptionCode: String(row.prescriptionCode ?? row.PrescriptionCode ?? ''),
    branchId: optionalString(row.branchId ?? row.BranchId),
    linkedPrescriberId: String(row.linkedPrescriberId ?? row.LinkedPrescriberId),
    prescriberName: String(row.prescriberName ?? row.PrescriberName ?? ''),
    customerId: optionalString(row.customerId ?? row.CustomerId),
    patientName: optionalString(row.patientName ?? row.PatientName),
    patientPhone: optionalString(row.patientPhone ?? row.PatientPhone),
    status: String(row.status ?? row.Status ?? 'draft'),
    source: String(row.source ?? row.Source ?? 'staff_entry'),
    verifiedAt: optionalString(row.verifiedAt ?? row.VerifiedAt),
    expiresAt: optionalString(row.expiresAt ?? row.ExpiresAt),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    lineCount: Number(row.lineCount ?? row.LineCount ?? 0),
    qtyRemaining: Number(row.qtyRemaining ?? row.QtyRemaining ?? 0),
  };
}

function normalizePrescriptionDetail(row: UnknownRow): RxPrescriptionDetail {
  return {
    id: String(row.id ?? row.Id),
    prescriptionCode: String(row.prescriptionCode ?? row.PrescriptionCode ?? ''),
    branchId: optionalString(row.branchId ?? row.BranchId),
    linkedPrescriberId: String(row.linkedPrescriberId ?? row.LinkedPrescriberId),
    prescriberName: String(row.prescriberName ?? row.PrescriberName ?? ''),
    customerId: optionalString(row.customerId ?? row.CustomerId),
    patientName: optionalString(row.patientName ?? row.PatientName),
    patientPhone: optionalString(row.patientPhone ?? row.PatientPhone),
    status: String(row.status ?? row.Status ?? 'draft'),
    source: String(row.source ?? row.Source ?? 'staff_entry'),
    verificationMethod: optionalString(row.verificationMethod ?? row.VerificationMethod),
    verifiedBy: optionalString(row.verifiedBy ?? row.VerifiedBy),
    verifiedAt: optionalString(row.verifiedAt ?? row.VerifiedAt),
    signedAt: optionalString(row.signedAt ?? row.SignedAt),
    expiresAt: optionalString(row.expiresAt ?? row.ExpiresAt),
    dispensedAt: optionalString(row.dispensedAt ?? row.DispensedAt),
    notes: optionalString(row.notes ?? row.Notes),
    createdBy: optionalString(row.createdBy ?? row.CreatedBy),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    updatedAt: String(row.updatedAt ?? row.UpdatedAt ?? ''),
    cancelledAt: optionalString(row.cancelledAt ?? row.CancelledAt),
    lines: asArray(row.lines ?? row.Lines).map(normalizePrescriptionLine),
    attachments: asArray(row.attachments ?? row.Attachments).map(normalizeAttachment),
  };
}

function normalizePosLoadLine(row: UnknownRow): RxPrescriptionPosLoadLine {
  return {
    prescriptionLineId: String(row.prescriptionLineId ?? row.PrescriptionLineId),
    productId: String(row.productId ?? row.ProductId),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    productUnitId: optionalString(row.productUnitId ?? row.ProductUnitId),
    unitName: optionalString(row.unitName ?? row.UnitName),
    unitPrice: Number(row.unitPrice ?? row.UnitPrice ?? 0),
    qtyPrescribed: Number(row.qtyPrescribed ?? row.QtyPrescribed ?? 0),
    qtyDispensed: Number(row.qtyDispensed ?? row.QtyDispensed ?? 0),
    qtyRemaining: Number(row.qtyRemaining ?? row.QtyRemaining ?? 0),
    stockAvailable: Number(row.stockAvailable ?? row.StockAvailable ?? 0),
    lineDispensingClass: String(row.lineDispensingClass ?? row.LineDispensingClass ?? 'otc'),
    dosageInstruction: optionalString(row.dosageInstruction ?? row.DosageInstruction),
  };
}

function normalizePosLoad(row: UnknownRow): RxPrescriptionPosLoad {
  return {
    id: String(row.id ?? row.Id),
    prescriptionCode: String(row.prescriptionCode ?? row.PrescriptionCode ?? ''),
    status: String(row.status ?? row.Status ?? 'draft'),
    branchId: optionalString(row.branchId ?? row.BranchId),
    linkedPrescriberId: String(row.linkedPrescriberId ?? row.LinkedPrescriberId),
    prescriberName: String(row.prescriberName ?? row.PrescriberName ?? ''),
    customerId: optionalString(row.customerId ?? row.CustomerId),
    patientName: optionalString(row.patientName ?? row.PatientName),
    patientPhone: optionalString(row.patientPhone ?? row.PatientPhone),
    verifiedAt: optionalString(row.verifiedAt ?? row.VerifiedAt),
    expiresAt: optionalString(row.expiresAt ?? row.ExpiresAt),
    lines: asArray(row.lines ?? row.Lines).map(normalizePosLoadLine),
  };
}

export type RxPrescriber = {
  id: string;
  fullName: string;
  licenseNumber?: string;
  phone?: string;
  specialty?: string;
  status: number;
  notes?: string;
  createdAt: string;
  updatedAt: string;
};

export type RxPrescriptionLine = {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  productUnitId?: string;
  unitName?: string;
  lineDispensingClass: string;
  qtyPrescribed: number;
  qtyDispensed: number;
  qtyRemaining: number;
  dosageInstruction?: string;
  sortOrder: number;
};

export type RxPrescriptionAttachment = {
  id: string;
  fileUrl: string;
  fileName?: string;
  uploadedBy?: string;
  createdAt: string;
};

export type RxPrescriptionListItem = {
  id: string;
  prescriptionCode: string;
  branchId?: string;
  linkedPrescriberId: string;
  prescriberName: string;
  customerId?: string;
  patientName?: string;
  patientPhone?: string;
  status: string;
  source: string;
  verifiedAt?: string;
  expiresAt?: string;
  createdAt: string;
  lineCount: number;
  qtyRemaining: number;
};

export type RxPrescriptionDetail = {
  id: string;
  prescriptionCode: string;
  branchId?: string;
  linkedPrescriberId: string;
  prescriberName: string;
  customerId?: string;
  patientName?: string;
  patientPhone?: string;
  status: string;
  source: string;
  verificationMethod?: string;
  verifiedBy?: string;
  verifiedAt?: string;
  signedAt?: string;
  expiresAt?: string;
  dispensedAt?: string;
  notes?: string;
  createdBy?: string;
  createdAt: string;
  updatedAt: string;
  cancelledAt?: string;
  lines: RxPrescriptionLine[];
  attachments: RxPrescriptionAttachment[];
};

export type RxPrescriptionPosLoadLine = {
  prescriptionLineId: string;
  productId: string;
  productCode: string;
  productName: string;
  productUnitId?: string;
  unitName?: string;
  unitPrice: number;
  qtyPrescribed: number;
  qtyDispensed: number;
  qtyRemaining: number;
  stockAvailable: number;
  lineDispensingClass: string;
  dosageInstruction?: string;
};

export type RxPrescriptionPosLoad = {
  id: string;
  prescriptionCode: string;
  status: string;
  branchId?: string;
  linkedPrescriberId: string;
  prescriberName: string;
  customerId?: string;
  patientName?: string;
  patientPhone?: string;
  verifiedAt?: string;
  expiresAt?: string;
  lines: RxPrescriptionPosLoadLine[];
};

export type RxPrescriptionPagedResult = {
  items: RxPrescriptionListItem[];
  total: number;
  page: number;
  pageSize: number;
};

export type UpsertPrescriptionLineInput = {
  productId: string;
  productUnitId?: string;
  qtyPrescribed: number;
  dosageInstruction?: string;
  sortOrder?: number;
};

export type UpsertPrescriptionInput = {
  branchId?: string;
  linkedPrescriberId: string;
  customerId?: string;
  patientName?: string;
  patientPhone?: string;
  source?: string;
  notes?: string;
  lines: UpsertPrescriptionLineInput[];
};

export async function fetchPrescribers(search?: string, activeOnly = false): Promise<RxPrescriber[]> {
  const { data } = await http.get<UnknownRow[]>('/pharmacy/prescribers', {
    params: {
      ...(search?.trim() ? { search: search.trim() } : {}),
      activeOnly,
    },
  });
  return data.map(normalizePrescriber);
}

export async function createPrescriber(payload: {
  fullName: string;
  licenseNumber?: string;
  phone?: string;
  specialty?: string;
  notes?: string;
}): Promise<RxPrescriber> {
  const { data } = await http.post<UnknownRow>('/pharmacy/prescribers', payload);
  return normalizePrescriber(data);
}

export async function updatePrescriber(
  id: string,
  payload: {
    fullName: string;
    licenseNumber?: string;
    phone?: string;
    specialty?: string;
    status?: number;
    notes?: string;
  },
): Promise<RxPrescriber> {
  const { data } = await http.put<UnknownRow>(`/pharmacy/prescribers/${id}`, payload);
  return normalizePrescriber(data);
}

export async function deletePrescriber(id: string): Promise<void> {
  await http.delete(`/pharmacy/prescribers/${id}`);
}

export async function fetchPrescriptions(filters?: {
  status?: string;
  phoneSearch?: string;
  page?: number;
  pageSize?: number;
}): Promise<RxPrescriptionPagedResult> {
  const params: Record<string, string | number> = {};
  if (filters?.status) params.status = filters.status;
  if (filters?.phoneSearch?.trim()) params.phoneSearch = filters.phoneSearch.trim();
  if (filters?.page != null) params.page = filters.page;
  if (filters?.pageSize != null) params.pageSize = filters.pageSize;
  const { data } = await http.get<UnknownRow>('/pharmacy/prescriptions', {
    params: Object.keys(params).length > 0 ? params : undefined,
  });
  const rows = asArray(data.items ?? data.Items);
  return {
    items: rows.map(normalizePrescriptionListItem),
    total: Number(data.total ?? data.Total ?? rows.length),
    page: Number(data.page ?? data.Page ?? 1),
    pageSize: Number(data.pageSize ?? data.PageSize ?? 50),
  };
}

export async function fetchPrescription(id: string): Promise<RxPrescriptionDetail> {
  const { data } = await http.get<UnknownRow>(`/pharmacy/prescriptions/${id}`);
  return normalizePrescriptionDetail(data);
}

function mapUpsertPayload(payload: UpsertPrescriptionInput) {
  return {
    branchId: payload.branchId ?? null,
    linkedPrescriberId: payload.linkedPrescriberId,
    customerId: payload.customerId ?? null,
    patientName: payload.patientName?.trim() || null,
    patientPhone: payload.patientPhone?.trim() || null,
    source: payload.source ?? 'staff_entry',
    notes: payload.notes?.trim() || null,
    lines: payload.lines.map((line, index) => ({
      productId: line.productId,
      productUnitId: line.productUnitId ?? null,
      qtyPrescribed: line.qtyPrescribed,
      dosageInstruction: line.dosageInstruction?.trim() || null,
      sortOrder: line.sortOrder ?? index,
    })),
  };
}

export async function createPrescription(payload: UpsertPrescriptionInput): Promise<RxPrescriptionDetail> {
  const { data } = await http.post<UnknownRow>('/pharmacy/prescriptions', mapUpsertPayload(payload));
  return normalizePrescriptionDetail(data);
}

export async function updatePrescription(
  id: string,
  payload: UpsertPrescriptionInput,
): Promise<RxPrescriptionDetail> {
  const { data } = await http.put<UnknownRow>(`/pharmacy/prescriptions/${id}`, mapUpsertPayload(payload));
  return normalizePrescriptionDetail(data);
}

export async function submitPrescription(id: string): Promise<RxPrescriptionDetail> {
  const { data } = await http.post<UnknownRow>(`/pharmacy/prescriptions/${id}/submit`);
  return normalizePrescriptionDetail(data);
}

export async function verifyPrescription(
  id: string,
  payload: { verificationMethod: string; signedAt?: string },
): Promise<RxPrescriptionDetail> {
  const { data } = await http.post<UnknownRow>(`/pharmacy/prescriptions/${id}/verify`, {
    verificationMethod: payload.verificationMethod,
    signedAt: payload.signedAt ?? null,
  });
  return normalizePrescriptionDetail(data);
}

export async function cancelPrescription(id: string, reason?: string): Promise<RxPrescriptionDetail> {
  const { data } = await http.post<UnknownRow>(`/pharmacy/prescriptions/${id}/cancel`, {
    reason: reason?.trim() || null,
  });
  return normalizePrescriptionDetail(data);
}

export async function addPrescriptionAttachment(
  id: string,
  payload: { fileUrl: string; fileName?: string },
): Promise<RxPrescriptionAttachment> {
  const { data } = await http.post<UnknownRow>(`/pharmacy/prescriptions/${id}/attachments`, {
    fileUrl: payload.fileUrl,
    fileName: payload.fileName ?? null,
  });
  return normalizeAttachment(data);
}

export async function uploadPrescriptionFile(file: File): Promise<string> {
  const formData = new FormData();
  formData.append('file', file);
  const { data } = await http.post<{ url?: string; Url?: string }>('/files/upload-prescription', formData);
  return String(data.url ?? data.Url ?? '');
}

export async function fetchPrescriptionPosLoad(
  prescriptionId: string,
  warehouseId: string,
  priceType = 1,
): Promise<RxPrescriptionPosLoad> {
  const { data } = await http.get<UnknownRow>(`/pharmacy/prescriptions/${prescriptionId}/pos-load`, {
    params: { warehouseId, priceType },
  });
  return normalizePosLoad(data);
}

export type RxPrescriberLink = {
  id: string;
  prescriberId: string;
  tenantId: string;
  tenantCode: string;
  tenantName: string;
  linkedPrescriberId: string | null;
  linkStatus: string;
  initiatedBy: string;
  invitedAt: string;
  respondedAt: string | null;
  prescriberName: string | null;
  prescriberPhone: string | null;
  prescriberLicenseNumber: string | null;
};

function normalizePrescriberLink(row: UnknownRow): RxPrescriberLink {
  return {
    id: String(row.id ?? row.Id),
    prescriberId: String(row.prescriberId ?? row.PrescriberId),
    tenantId: String(row.tenantId ?? row.TenantId),
    tenantCode: String(row.tenantCode ?? row.TenantCode ?? ''),
    tenantName: String(row.tenantName ?? row.TenantName ?? ''),
    linkedPrescriberId: row.linkedPrescriberId != null || row.LinkedPrescriberId != null
      ? String(row.linkedPrescriberId ?? row.LinkedPrescriberId)
      : null,
    linkStatus: String(row.linkStatus ?? row.LinkStatus ?? ''),
    initiatedBy: String(row.initiatedBy ?? row.InitiatedBy ?? ''),
    invitedAt: String(row.invitedAt ?? row.InvitedAt ?? ''),
    respondedAt: row.respondedAt != null || row.RespondedAt != null
      ? String(row.respondedAt ?? row.RespondedAt)
      : null,
    prescriberName: row.prescriberName != null || row.PrescriberName != null
      ? String(row.prescriberName ?? row.PrescriberName)
      : null,
    prescriberPhone: row.prescriberPhone != null || row.PrescriberPhone != null
      ? String(row.prescriberPhone ?? row.PrescriberPhone)
      : null,
    prescriberLicenseNumber:
      row.prescriberLicenseNumber != null || row.PrescriberLicenseNumber != null
        ? String(row.prescriberLicenseNumber ?? row.PrescriberLicenseNumber)
        : null,
  };
}

export async function fetchPrescriberLinks(status?: string): Promise<RxPrescriberLink[]> {
  const { data } = await http.get<UnknownRow[]>('/pharmacy/prescribers/links', {
    params: status ? { status } : undefined,
  });
  return data.map(normalizePrescriberLink);
}

export async function fetchPendingPrescriberLinks(): Promise<RxPrescriberLink[]> {
  const { data } = await http.get<UnknownRow[]>('/pharmacy/prescribers/links/pending-approval');
  return data.map(normalizePrescriberLink);
}

export async function invitePrescriberLink(payload: {
  phone: string;
  fullName: string;
  licenseNumber?: string;
  specialty?: string;
  notes?: string;
}): Promise<RxPrescriberLink> {
  const { data } = await http.post<UnknownRow>('/pharmacy/prescribers/links/invite', payload);
  return normalizePrescriberLink(data);
}

export async function approvePrescriberLink(id: string): Promise<RxPrescriberLink> {
  const { data } = await http.post<UnknownRow>(`/pharmacy/prescribers/links/${id}/approve`);
  return normalizePrescriberLink(data);
}

export async function rejectPrescriberLink(id: string, reason?: string): Promise<RxPrescriberLink> {
  const { data } = await http.post<UnknownRow>(`/pharmacy/prescribers/links/${id}/reject`, { reason });
  return normalizePrescriberLink(data);
}

export async function revokePrescriberLink(id: string): Promise<RxPrescriberLink> {
  const { data } = await http.post<UnknownRow>(`/pharmacy/prescribers/links/${id}/revoke`);
  return normalizePrescriberLink(data);
}

export interface RxTenantDashboard {
  signedThisMonth: number;
  signedTotal: number;
  pendingDispense: number;
  overduePendingDispense: number;
  pendingVerification: number;
  pendingLinkApprovals: number;
  activePrescriberLinks: number;
  portalSignedThisMonth: number;
  staffSignedThisMonth: number;
  avgHoursToDispense: number | null;
  topPrescribers: Array<{
    prescriberId: string | null;
    prescriberName: string;
    phone: string | null;
    signedThisMonth: number;
    signedTotal: number;
  }>;
  recentPendingDispense: Array<{
    id: string;
    prescriptionCode: string;
    prescriberName: string | null;
    patientName: string | null;
    status: string;
    signedAt: string | null;
    hoursWaiting: number | null;
  }>;
}

export async function fetchTenantRxDashboard(): Promise<RxTenantDashboard> {
  const { data } = await http.get<UnknownRow>('/pharmacy/prescriptions/dashboard');
  const top = asArray(data.topPrescribers ?? data.TopPrescribers).map((row) => ({
    prescriberId: optionalString(row.prescriberId ?? row.PrescriberId) ?? null,
    prescriberName: String(row.prescriberName ?? row.PrescriberName ?? '—'),
    phone: optionalString(row.phone ?? row.Phone) ?? null,
    signedThisMonth: Number(row.signedThisMonth ?? row.SignedThisMonth ?? 0),
    signedTotal: Number(row.signedTotal ?? row.SignedTotal ?? 0),
  }));
  const pending = asArray(data.recentPendingDispense ?? data.RecentPendingDispense).map((row) => ({
    id: String(row.id ?? row.Id),
    prescriptionCode: String(row.prescriptionCode ?? row.PrescriptionCode ?? ''),
    prescriberName: optionalString(row.prescriberName ?? row.PrescriberName) ?? null,
    patientName: optionalString(row.patientName ?? row.PatientName) ?? null,
    status: String(row.status ?? row.Status ?? ''),
    signedAt: optionalString(row.signedAt ?? row.SignedAt) ?? null,
    hoursWaiting:
      row.hoursWaiting != null || row.HoursWaiting != null
        ? Number(row.hoursWaiting ?? row.HoursWaiting)
        : null,
  }));
  return {
    signedThisMonth: Number(data.signedThisMonth ?? data.SignedThisMonth ?? 0),
    signedTotal: Number(data.signedTotal ?? data.SignedTotal ?? 0),
    pendingDispense: Number(data.pendingDispense ?? data.PendingDispense ?? 0),
    overduePendingDispense: Number(data.overduePendingDispense ?? data.OverduePendingDispense ?? 0),
    pendingVerification: Number(data.pendingVerification ?? data.PendingVerification ?? 0),
    pendingLinkApprovals: Number(data.pendingLinkApprovals ?? data.PendingLinkApprovals ?? 0),
    activePrescriberLinks: Number(data.activePrescriberLinks ?? data.ActivePrescriberLinks ?? 0),
    portalSignedThisMonth: Number(data.portalSignedThisMonth ?? data.PortalSignedThisMonth ?? 0),
    staffSignedThisMonth: Number(data.staffSignedThisMonth ?? data.StaffSignedThisMonth ?? 0),
    avgHoursToDispense:
      data.avgHoursToDispense != null || data.AvgHoursToDispense != null
        ? Number(data.avgHoursToDispense ?? data.AvgHoursToDispense)
        : null,
    topPrescribers: top,
    recentPendingDispense: pending,
  };
}

