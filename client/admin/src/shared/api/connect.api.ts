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

export interface ConnectOverview {
  packCode: string;
  displayName: string;
  phase: string;
  legalBoundary: string;
  enabledCapabilities: string[];
  explicitNonGoals: string[];
}

function asStringArray(value: unknown): string[] {
  return Array.isArray(value) ? value.map((item) => String(item)) : [];
}

export async function fetchConnectOverview(): Promise<ConnectOverview> {
  const { data } = await http.get<UnknownRow>('/connect/overview');
  return {
    packCode: String(data.packCode ?? data.PackCode ?? ''),
    displayName: String(data.displayName ?? data.DisplayName ?? ''),
    phase: String(data.phase ?? data.Phase ?? ''),
    legalBoundary: String(data.legalBoundary ?? data.LegalBoundary ?? ''),
    enabledCapabilities: asStringArray(data.enabledCapabilities ?? data.EnabledCapabilities),
    explicitNonGoals: asStringArray(data.explicitNonGoals ?? data.ExplicitNonGoals),
  };
}

export interface ConnectOrgLink {
  id: string;
  partnerTenantId: string;
  partnerTenantCode: string;
  partnerTenantName: string;
  ourOrgRole: string;
  partnerOrgRole: string;
  linkStatus: string;
  weAreInitiator: boolean;
  notes?: string;
  invitedAt: string;
  respondedAt?: string;
  createdAt: string;
}

export interface ConnectDirectoryEntry {
  tenantId: string;
  tenantCode: string;
  tenantName: string;
  orgKind?: string;
  address?: string;
  phone?: string;
}

function normalizeOrgLink(row: UnknownRow): ConnectOrgLink {
  return {
    id: String(row.id ?? row.Id),
    partnerTenantId: String(row.partnerTenantId ?? row.PartnerTenantId),
    partnerTenantCode: String(row.partnerTenantCode ?? row.PartnerTenantCode ?? ''),
    partnerTenantName: String(row.partnerTenantName ?? row.PartnerTenantName ?? ''),
    ourOrgRole: String(row.ourOrgRole ?? row.OurOrgRole ?? ''),
    partnerOrgRole: String(row.partnerOrgRole ?? row.PartnerOrgRole ?? ''),
    linkStatus: String(row.linkStatus ?? row.LinkStatus ?? ''),
    weAreInitiator: Boolean(row.weAreInitiator ?? row.WeAreInitiator ?? false),
    notes: optionalString(row.notes ?? row.Notes),
    invitedAt: String(row.invitedAt ?? row.InvitedAt ?? ''),
    respondedAt: optionalString(row.respondedAt ?? row.RespondedAt),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
  };
}

export async function fetchConnectOrgLinks(status?: string): Promise<ConnectOrgLink[]> {
  const { data } = await http.get<UnknownRow[]>('/connect/org-links', {
    params: status ? { status } : undefined,
  });
  return (Array.isArray(data) ? data : []).map((row) => normalizeOrgLink(row as UnknownRow));
}

export async function fetchConnectPendingOrgLinks(): Promise<ConnectOrgLink[]> {
  const { data } = await http.get<UnknownRow[]>('/connect/org-links/pending');
  return (Array.isArray(data) ? data : []).map((row) => normalizeOrgLink(row as UnknownRow));
}

export async function searchConnectDirectory(q?: string): Promise<ConnectDirectoryEntry[]> {
  const { data } = await http.get<UnknownRow[]>('/connect/org-links/directory', {
    params: q ? { q } : undefined,
  });
  return asArray(data).map((row) => ({
    tenantId: String(row.tenantId ?? row.TenantId),
    tenantCode: String(row.tenantCode ?? row.TenantCode ?? ''),
    tenantName: String(row.tenantName ?? row.TenantName ?? ''),
    orgKind: optionalString(row.orgKind ?? row.OrgKind),
    address: optionalString(row.address ?? row.Address),
    phone: optionalString(row.phone ?? row.Phone),
  }));
}

export interface ConnectOrgProfile {
  tenantId: string;
  tenantCode: string;
  tenantName: string;
  orgKind: string;
  displayName?: string;
}

export async function fetchConnectOrgProfile(): Promise<ConnectOrgProfile | null> {
  try {
    const { data } = await http.get<UnknownRow>('/connect/org-profile');
    return {
      tenantId: String(data.tenantId ?? data.TenantId),
      tenantCode: String(data.tenantCode ?? data.TenantCode ?? ''),
      tenantName: String(data.tenantName ?? data.TenantName ?? ''),
      orgKind: String(data.orgKind ?? data.OrgKind ?? ''),
      displayName: optionalString(data.displayName ?? data.DisplayName),
    };
  } catch {
    return null;
  }
}

export async function inviteConnectOrgLink(payload: {
  partnerTenantCode: string;
  ourOrgRole: string;
  partnerOrgRole: string;
  notes?: string;
}): Promise<ConnectOrgLink> {
  const { data } = await http.post<UnknownRow>('/connect/org-links/invite', payload);
  return normalizeOrgLink(data);
}

export async function acceptConnectOrgLink(id: string): Promise<ConnectOrgLink> {
  const { data } = await http.post<UnknownRow>(`/connect/org-links/${id}/accept`);
  return normalizeOrgLink(data);
}

export async function rejectConnectOrgLink(id: string): Promise<ConnectOrgLink> {
  const { data } = await http.post<UnknownRow>(`/connect/org-links/${id}/reject`);
  return normalizeOrgLink(data);
}

export async function revokeConnectOrgLink(id: string): Promise<ConnectOrgLink> {
  const { data } = await http.post<UnknownRow>(`/connect/org-links/${id}/revoke`);
  return normalizeOrgLink(data);
}

export interface ConnectDoctor {
  id: string;
  fullName: string;
  phone: string;
  licenseNumber?: string;
  specialty?: string;
  status: string;
  createdAt: string;
}

export interface ConnectDoctorMembership {
  id: string;
  doctorId: string;
  doctorFullName: string;
  doctorPhone: string;
  doctorLicenseNumber?: string;
  doctorSpecialty?: string;
  clinicTenantId: string;
  clinicTenantCode: string;
  clinicTenantName: string;
  membershipRole: string;
  membershipStatus: string;
  initiatedBy: string;
  notes?: string;
  invitedAt: string;
  respondedAt?: string;
  createdAt: string;
}

function normalizeMembership(row: UnknownRow): ConnectDoctorMembership {
  return {
    id: String(row.id ?? row.Id),
    doctorId: String(row.doctorId ?? row.DoctorId),
    doctorFullName: String(row.doctorFullName ?? row.DoctorFullName ?? ''),
    doctorPhone: String(row.doctorPhone ?? row.DoctorPhone ?? ''),
    doctorLicenseNumber: optionalString(row.doctorLicenseNumber ?? row.DoctorLicenseNumber),
    doctorSpecialty: optionalString(row.doctorSpecialty ?? row.DoctorSpecialty),
    clinicTenantId: String(row.clinicTenantId ?? row.ClinicTenantId),
    clinicTenantCode: String(row.clinicTenantCode ?? row.ClinicTenantCode ?? ''),
    clinicTenantName: String(row.clinicTenantName ?? row.ClinicTenantName ?? ''),
    membershipRole: String(row.membershipRole ?? row.MembershipRole ?? ''),
    membershipStatus: String(row.membershipStatus ?? row.MembershipStatus ?? ''),
    initiatedBy: String(row.initiatedBy ?? row.InitiatedBy ?? ''),
    notes: optionalString(row.notes ?? row.Notes),
    invitedAt: String(row.invitedAt ?? row.InvitedAt ?? ''),
    respondedAt: optionalString(row.respondedAt ?? row.RespondedAt),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
  };
}

function normalizeDoctor(row: UnknownRow): ConnectDoctor {
  return {
    id: String(row.id ?? row.Id),
    fullName: String(row.fullName ?? row.FullName ?? ''),
    phone: String(row.phone ?? row.Phone ?? ''),
    licenseNumber: optionalString(row.licenseNumber ?? row.LicenseNumber),
    specialty: optionalString(row.specialty ?? row.Specialty),
    status: String(row.status ?? row.Status ?? ''),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
  };
}

export async function fetchClinicMemberships(status?: string): Promise<ConnectDoctorMembership[]> {
  const { data } = await http.get<UnknownRow[]>('/connect/clinics/memberships', {
    params: status ? { status } : undefined,
  });
  return (Array.isArray(data) ? data : []).map((row) => normalizeMembership(row as UnknownRow));
}

export async function fetchClinicPendingMemberships(): Promise<ConnectDoctorMembership[]> {
  const { data } = await http.get<UnknownRow[]>('/connect/clinics/memberships/pending');
  return (Array.isArray(data) ? data : []).map((row) => normalizeMembership(row as UnknownRow));
}

export async function inviteClinicDoctor(payload: {
  fullName: string;
  phone: string;
  licenseNumber?: string;
  specialty?: string;
  membershipRole?: string;
  notes?: string;
}): Promise<ConnectDoctorMembership> {
  const { data } = await http.post<UnknownRow>('/connect/clinics/memberships/invite', payload);
  return normalizeMembership(data);
}

export async function confirmClinicMembership(id: string): Promise<ConnectDoctorMembership> {
  const { data } = await http.post<UnknownRow>(`/connect/clinics/memberships/${id}/confirm`);
  return normalizeMembership(data);
}

export async function approveClinicMembership(id: string): Promise<ConnectDoctorMembership> {
  const { data } = await http.post<UnknownRow>(`/connect/clinics/memberships/${id}/approve`);
  return normalizeMembership(data);
}

export async function rejectClinicMembership(id: string): Promise<ConnectDoctorMembership> {
  const { data } = await http.post<UnknownRow>(`/connect/clinics/memberships/${id}/reject`);
  return normalizeMembership(data);
}

export async function revokeClinicMembership(id: string): Promise<ConnectDoctorMembership> {
  const { data } = await http.post<UnknownRow>(`/connect/clinics/memberships/${id}/revoke`);
  return normalizeMembership(data);
}

export async function fetchPartnerClinicDoctors(partnerTenantId: string): Promise<ConnectDoctor[]> {
  const { data } = await http.get<UnknownRow[]>(`/connect/partners/${partnerTenantId}/doctors`);
  return (Array.isArray(data) ? data : []).map((row) => normalizeDoctor(row as UnknownRow));
}

export interface ConnectPartnerProduct {
  productId: string;
  productCode: string;
  productName: string;
  genericName?: string;
  defaultUnitName?: string;
  stockAvailableQty: number;
}

export async function fetchPartnerPharmacyProducts(
  pharmacyTenantId: string,
  q: string,
): Promise<ConnectPartnerProduct[]> {
  const { data } = await http.get<UnknownRow[]>(`/connect/partners/${pharmacyTenantId}/products`, {
    params: { q },
  });
  return (Array.isArray(data) ? data : []).map((row) => {
    const r = row as UnknownRow;
    return {
      productId: String(r.productId ?? r.ProductId),
      productCode: String(r.productCode ?? r.ProductCode ?? ''),
      productName: String(r.productName ?? r.ProductName ?? ''),
      genericName: optionalString(r.genericName ?? r.GenericName),
      defaultUnitName: optionalString(r.defaultUnitName ?? r.DefaultUnitName),
      stockAvailableQty: Number(r.stockAvailableQty ?? r.StockAvailableQty ?? 0),
    };
  });
}

export interface ConnectReferral {
  id: string;
  pharmacyTenantId: string;
  pharmacyTenantCode: string;
  pharmacyTenantName: string;
  clinicTenantId: string;
  clinicTenantCode: string;
  clinicTenantName: string;
  doctorId?: string;
  doctorFullName?: string;
  patientDisplayName: string;
  patientPhone?: string;
  pharmacyCustomerId?: string;
  clinicCustomerId?: string;
  reason?: string;
  notes?: string;
  referralStatus: string;
  createdAt: string;
  respondedAt?: string;
  completedAt?: string;
}

function normalizeReferral(row: UnknownRow): ConnectReferral {
  return {
    id: String(row.id ?? row.Id),
    pharmacyTenantId: String(row.pharmacyTenantId ?? row.PharmacyTenantId),
    pharmacyTenantCode: String(row.pharmacyTenantCode ?? row.PharmacyTenantCode ?? ''),
    pharmacyTenantName: String(row.pharmacyTenantName ?? row.PharmacyTenantName ?? ''),
    clinicTenantId: String(row.clinicTenantId ?? row.ClinicTenantId),
    clinicTenantCode: String(row.clinicTenantCode ?? row.ClinicTenantCode ?? ''),
    clinicTenantName: String(row.clinicTenantName ?? row.ClinicTenantName ?? ''),
    doctorId: optionalString(row.doctorId ?? row.DoctorId),
    doctorFullName: optionalString(row.doctorFullName ?? row.DoctorFullName),
    patientDisplayName: String(row.patientDisplayName ?? row.PatientDisplayName ?? ''),
    patientPhone: optionalString(row.patientPhone ?? row.PatientPhone),
    pharmacyCustomerId: optionalString(row.pharmacyCustomerId ?? row.PharmacyCustomerId),
    clinicCustomerId: optionalString(row.clinicCustomerId ?? row.ClinicCustomerId),
    reason: optionalString(row.reason ?? row.Reason),
    notes: optionalString(row.notes ?? row.Notes),
    referralStatus: String(row.referralStatus ?? row.ReferralStatus ?? ''),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    respondedAt: optionalString(row.respondedAt ?? row.RespondedAt),
    completedAt: optionalString(row.completedAt ?? row.CompletedAt),
  };
}

export async function fetchConnectReferrals(status?: string): Promise<ConnectReferral[]> {
  const { data } = await http.get<UnknownRow[]>('/connect/referrals', {
    params: status ? { status } : undefined,
  });
  return (Array.isArray(data) ? data : []).map((row) => normalizeReferral(row as UnknownRow));
}

export async function fetchConnectReferralInbox(): Promise<ConnectReferral[]> {
  const { data } = await http.get<UnknownRow[]>('/connect/referrals/inbox');
  return (Array.isArray(data) ? data : []).map((row) => normalizeReferral(row as UnknownRow));
}

export async function createConnectReferral(payload: {
  clinicTenantId: string;
  pharmacyCustomerId: string;
  patientDisplayName?: string;
  patientPhone?: string;
  reason?: string;
  notes?: string;
  doctorId?: string;
}): Promise<ConnectReferral> {
  const { data } = await http.post<UnknownRow>('/connect/referrals', payload);
  return normalizeReferral(data);
}

export async function acceptConnectReferral(id: string): Promise<ConnectReferral> {
  const { data } = await http.post<UnknownRow>(`/connect/referrals/${id}/accept`);
  return normalizeReferral(data);
}

export async function rejectConnectReferral(id: string): Promise<ConnectReferral> {
  const { data } = await http.post<UnknownRow>(`/connect/referrals/${id}/reject`);
  return normalizeReferral(data);
}

export async function completeConnectReferral(id: string): Promise<ConnectReferral> {
  const { data } = await http.post<UnknownRow>(`/connect/referrals/${id}/complete`);
  return normalizeReferral(data);
}

export async function cancelConnectReferral(id: string): Promise<ConnectReferral> {
  const { data } = await http.post<UnknownRow>(`/connect/referrals/${id}/cancel`);
  return normalizeReferral(data);
}

export interface ConnectBooking {
  id: string;
  clinicTenantId: string;
  clinicTenantCode: string;
  clinicTenantName: string;
  pharmacyTenantId?: string;
  pharmacyTenantCode?: string;
  pharmacyTenantName?: string;
  referralId?: string;
  doctorId?: string;
  doctorFullName?: string;
  patientDisplayName: string;
  patientPhone?: string;
  pharmacyCustomerId?: string;
  scheduledAt: string;
  durationMinutes: number;
  bookingStatus: string;
  encounterModality: string;
  notes?: string;
  notifiedAt?: string;
  createdAt: string;
}

function normalizeBooking(row: UnknownRow): ConnectBooking {
  return {
    id: String(row.id ?? row.Id),
    clinicTenantId: String(row.clinicTenantId ?? row.ClinicTenantId),
    clinicTenantCode: String(row.clinicTenantCode ?? row.ClinicTenantCode ?? ''),
    clinicTenantName: String(row.clinicTenantName ?? row.ClinicTenantName ?? ''),
    pharmacyTenantId: optionalString(row.pharmacyTenantId ?? row.PharmacyTenantId),
    pharmacyTenantCode: optionalString(row.pharmacyTenantCode ?? row.PharmacyTenantCode),
    pharmacyTenantName: optionalString(row.pharmacyTenantName ?? row.PharmacyTenantName),
    referralId: optionalString(row.referralId ?? row.ReferralId),
    doctorId: optionalString(row.doctorId ?? row.DoctorId),
    doctorFullName: optionalString(row.doctorFullName ?? row.DoctorFullName),
    patientDisplayName: String(row.patientDisplayName ?? row.PatientDisplayName ?? ''),
    patientPhone: optionalString(row.patientPhone ?? row.PatientPhone),
    pharmacyCustomerId: optionalString(row.pharmacyCustomerId ?? row.PharmacyCustomerId),
    scheduledAt: String(row.scheduledAt ?? row.ScheduledAt ?? ''),
    durationMinutes: Number(row.durationMinutes ?? row.DurationMinutes ?? 30),
    bookingStatus: String(row.bookingStatus ?? row.BookingStatus ?? ''),
    encounterModality: String(
      row.encounterModality ?? row.EncounterModality ?? 'in_person',
    ),
    notes: optionalString(row.notes ?? row.Notes),
    notifiedAt: optionalString(row.notifiedAt ?? row.NotifiedAt),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
  };
}

export async function fetchConnectBookings(status?: string): Promise<ConnectBooking[]> {
  const { data } = await http.get<UnknownRow[]>('/connect/bookings', {
    params: status ? { status } : undefined,
  });
  return (Array.isArray(data) ? data : []).map((row) => normalizeBooking(row as UnknownRow));
}

export async function createConnectBooking(payload: {
  scheduledAt: string;
  patientDisplayName: string;
  patientPhone?: string;
  referralId?: string;
  pharmacyTenantId?: string;
  doctorId?: string;
  durationMinutes?: number;
  notes?: string;
  encounterModality?: string;
}): Promise<ConnectBooking> {
  const { data } = await http.post<UnknownRow>('/connect/bookings', payload);
  return normalizeBooking(data);
}

export async function confirmConnectBooking(id: string): Promise<ConnectBooking> {
  const { data } = await http.post<UnknownRow>(`/connect/bookings/${id}/confirm`);
  return normalizeBooking(data);
}

export async function cancelConnectBooking(id: string): Promise<ConnectBooking> {
  const { data } = await http.post<UnknownRow>(`/connect/bookings/${id}/cancel`);
  return normalizeBooking(data);
}

export async function completeConnectBooking(id: string): Promise<ConnectBooking> {
  const { data } = await http.post<UnknownRow>(`/connect/bookings/${id}/complete`);
  return normalizeBooking(data);
}

export async function noShowConnectBooking(id: string): Promise<ConnectBooking> {
  const { data } = await http.post<UnknownRow>(`/connect/bookings/${id}/no-show`);
  return normalizeBooking(data);
}

export interface ConnectStatusEvent {
  id: string;
  pharmacyTenantId: string;
  pharmacyTenantCode: string;
  pharmacyTenantName: string;
  clinicTenantId: string;
  clinicTenantCode: string;
  clinicTenantName: string;
  eventType: string;
  sourceType: string;
  sourceId?: string;
  patientDisplayName?: string;
  patientPhone?: string;
  summary?: string;
  eventStatus: string;
  createdAt: string;
  consumedAt?: string;
}

function normalizeStatusEvent(row: UnknownRow): ConnectStatusEvent {
  return {
    id: String(row.id ?? row.Id),
    pharmacyTenantId: String(row.pharmacyTenantId ?? row.PharmacyTenantId),
    pharmacyTenantCode: String(row.pharmacyTenantCode ?? row.PharmacyTenantCode ?? ''),
    pharmacyTenantName: String(row.pharmacyTenantName ?? row.PharmacyTenantName ?? ''),
    clinicTenantId: String(row.clinicTenantId ?? row.ClinicTenantId),
    clinicTenantCode: String(row.clinicTenantCode ?? row.ClinicTenantCode ?? ''),
    clinicTenantName: String(row.clinicTenantName ?? row.ClinicTenantName ?? ''),
    eventType: String(row.eventType ?? row.EventType ?? ''),
    sourceType: String(row.sourceType ?? row.SourceType ?? ''),
    sourceId: optionalString(row.sourceId ?? row.SourceId),
    patientDisplayName: optionalString(row.patientDisplayName ?? row.PatientDisplayName),
    patientPhone: optionalString(row.patientPhone ?? row.PatientPhone),
    summary: optionalString(row.summary ?? row.Summary),
    eventStatus: String(row.eventStatus ?? row.EventStatus ?? ''),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    consumedAt: optionalString(row.consumedAt ?? row.ConsumedAt),
  };
}

export async function fetchConnectStatusEvents(status?: string): Promise<ConnectStatusEvent[]> {
  const { data } = await http.get<UnknownRow[]>('/connect/status-events', {
    params: status ? { status } : undefined,
  });
  return (Array.isArray(data) ? data : []).map((row) => normalizeStatusEvent(row as UnknownRow));
}

export async function fetchConnectPendingStatusEvents(): Promise<ConnectStatusEvent[]> {
  const { data } = await http.get<UnknownRow[]>('/connect/status-events/pending');
  return (Array.isArray(data) ? data : []).map((row) => normalizeStatusEvent(row as UnknownRow));
}

export async function createConnectStatusEvent(payload: {
  pharmacyTenantId: string;
  patientDisplayName?: string;
  patientPhone?: string;
  summary?: string;
}): Promise<ConnectStatusEvent> {
  const { data } = await http.post<UnknownRow>('/connect/status-events', payload);
  return normalizeStatusEvent(data);
}

export async function consumeConnectStatusEvent(id: string): Promise<ConnectStatusEvent> {
  const { data } = await http.post<UnknownRow>(`/connect/status-events/${id}/consume`);
  return normalizeStatusEvent(data);
}

export async function dismissConnectStatusEvent(id: string): Promise<ConnectStatusEvent> {
  const { data } = await http.post<UnknownRow>(`/connect/status-events/${id}/dismiss`);
  return normalizeStatusEvent(data);
}

export interface ConnectRxHandoffLine {
  drugName: string;
  strength?: string;
  quantity: number;
  unit?: string;
  dosageInstruction?: string;
  sortOrder: number;
}

export interface ConnectRxHandoff {
  id: string;
  clinicTenantId: string;
  clinicTenantCode: string;
  clinicTenantName: string;
  pharmacyTenantId: string;
  pharmacyTenantCode: string;
  pharmacyTenantName: string;
  clinicPrescriptionId: string;
  prescriptionCode: string;
  patientDisplayName?: string;
  patientPhone?: string;
  providerDisplayName?: string;
  diagnosisText?: string;
  notes?: string;
  pdfSha256?: string;
  handoffStatus: string;
  statusEventId?: string;
  createdAt: string;
  consumedAt?: string;
  lines: ConnectRxHandoffLine[];
}

function normalizeRxHandoff(row: UnknownRow): ConnectRxHandoff {
  const linesRaw = (row.lines ?? row.Lines ?? []) as UnknownRow[];
  return {
    id: String(row.id ?? row.Id),
    clinicTenantId: String(row.clinicTenantId ?? row.ClinicTenantId),
    clinicTenantCode: String(row.clinicTenantCode ?? row.ClinicTenantCode ?? ''),
    clinicTenantName: String(row.clinicTenantName ?? row.ClinicTenantName ?? ''),
    pharmacyTenantId: String(row.pharmacyTenantId ?? row.PharmacyTenantId),
    pharmacyTenantCode: String(row.pharmacyTenantCode ?? row.PharmacyTenantCode ?? ''),
    pharmacyTenantName: String(row.pharmacyTenantName ?? row.PharmacyTenantName ?? ''),
    clinicPrescriptionId: String(row.clinicPrescriptionId ?? row.ClinicPrescriptionId),
    prescriptionCode: String(row.prescriptionCode ?? row.PrescriptionCode ?? ''),
    patientDisplayName: optionalString(row.patientDisplayName ?? row.PatientDisplayName),
    patientPhone: optionalString(row.patientPhone ?? row.PatientPhone),
    providerDisplayName: optionalString(row.providerDisplayName ?? row.ProviderDisplayName),
    diagnosisText: optionalString(row.diagnosisText ?? row.DiagnosisText),
    notes: optionalString(row.notes ?? row.Notes),
    pdfSha256: optionalString(row.pdfSha256 ?? row.PdfSha256),
    handoffStatus: String(row.handoffStatus ?? row.HandoffStatus ?? ''),
    statusEventId: optionalString(row.statusEventId ?? row.StatusEventId),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    consumedAt: optionalString(row.consumedAt ?? row.ConsumedAt),
    lines: (Array.isArray(linesRaw) ? linesRaw : []).map((l) => {
      const line = l as UnknownRow;
      return {
        drugName: String(line.drugName ?? line.DrugName ?? ''),
        strength: optionalString(line.strength ?? line.Strength),
        quantity: Number(line.quantity ?? line.Quantity ?? 1),
        unit: optionalString(line.unit ?? line.Unit),
        dosageInstruction: optionalString(line.dosageInstruction ?? line.DosageInstruction),
        sortOrder: Number(line.sortOrder ?? line.SortOrder ?? 0),
      };
    }),
  };
}

export async function fetchConnectRxHandoff(id: string): Promise<ConnectRxHandoff> {
  const { data } = await http.get<UnknownRow>(`/connect/rx-handoffs/${id}`);
  return normalizeRxHandoff(data);
}

export async function fetchConnectRxHandoffs(status?: string): Promise<ConnectRxHandoff[]> {
  const { data } = await http.get<UnknownRow[]>('/connect/rx-handoffs', {
    params: status ? { status } : undefined,
  });
  return (Array.isArray(data) ? data : []).map((row) => normalizeRxHandoff(row as UnknownRow));
}

