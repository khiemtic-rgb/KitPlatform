import { http } from '@/shared/api/http';

type UnknownRow = Record<string, unknown>;

function optionalString(value: unknown): string | undefined {
  if (value == null || value === '') return undefined;
  return String(value);
}

export interface ClinicProvider {
  id: string;
  providerCode: string;
  displayName: string;
  specialty?: string;
  licenseNo?: string;
  status: number;
  connectDoctorId?: string;
  createdAt: string;
  updatedAt: string;
  phone?: string;
  email?: string;
  title?: string;
  notes?: string;
}

function normalizeProvider(row: UnknownRow): ClinicProvider {
  return {
    id: String(row.id ?? row.Id),
    providerCode: String(row.providerCode ?? row.ProviderCode ?? ''),
    displayName: String(row.displayName ?? row.DisplayName ?? ''),
    specialty: optionalString(row.specialty ?? row.Specialty),
    licenseNo: optionalString(row.licenseNo ?? row.LicenseNo),
    status: Number(row.status ?? row.Status ?? 1),
    connectDoctorId: optionalString(row.connectDoctorId ?? row.ConnectDoctorId),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    updatedAt: String(row.updatedAt ?? row.UpdatedAt ?? ''),
    phone: optionalString(row.phone ?? row.Phone),
    email: optionalString(row.email ?? row.Email),
    title: optionalString(row.title ?? row.Title),
    notes: optionalString(row.notes ?? row.Notes),
  };
}

export async function fetchClinicProviders(includeInactive = false): Promise<ClinicProvider[]> {
  const { data } = await http.get<UnknownRow[] | { items?: UnknownRow[]; Items?: UnknownRow[] }>(
    '/clinic/providers',
    { params: { includeInactive } },
  );
  const rows = Array.isArray(data)
    ? data
    : Array.isArray((data as { items?: UnknownRow[] }).items)
      ? ((data as { items: UnknownRow[] }).items)
      : Array.isArray((data as { Items?: UnknownRow[] }).Items)
        ? ((data as { Items: UnknownRow[] }).Items)
        : [];
  return rows.map((row) => normalizeProvider(row as UnknownRow));
}

export async function createClinicProvider(payload: {
  providerCode: string;
  displayName: string;
  specialty?: string;
  licenseNo?: string;
  status?: number;
  connectDoctorId?: string;
  phone?: string;
  email?: string;
  title?: string;
  notes?: string;
}): Promise<ClinicProvider> {
  const { data } = await http.post<UnknownRow>('/clinic/providers', payload);
  return normalizeProvider(data);
}

export async function updateClinicProvider(
  id: string,
  payload: {
    displayName?: string;
    specialty?: string;
    licenseNo?: string;
    status?: number;
    connectDoctorId?: string | null;
    clearConnectDoctorId?: boolean;
    phone?: string;
    email?: string;
    title?: string;
    notes?: string;
  },
): Promise<ClinicProvider> {
  const { data } = await http.patch<UnknownRow>(`/clinic/providers/${id}`, payload);
  return normalizeProvider(data);
}

export async function upsertClinicProviderFromConnect(payload: {
  connectDoctorId: string;
  providerCode?: string;
  displayName?: string;
  specialty?: string;
  licenseNo?: string;
}): Promise<ClinicProvider> {
  const { data } = await http.post<UnknownRow>('/clinic/providers/from-connect', payload);
  return normalizeProvider(data);
}

export interface ClinicTenantSettings {
  name: string;
  address?: string;
  phone?: string;
  workingHours?: string;
}

function normalizeClinicSettings(row: UnknownRow): ClinicTenantSettings {
  return {
    name: String(row.name ?? row.Name ?? ''),
    address: optionalString(row.address ?? row.Address),
    phone: optionalString(row.phone ?? row.Phone),
    workingHours: optionalString(row.workingHours ?? row.WorkingHours),
  };
}

export async function fetchClinicSettings(): Promise<ClinicTenantSettings> {
  const { data } = await http.get<UnknownRow>('/clinic/settings');
  return normalizeClinicSettings(data);
}

export async function updateClinicSettings(payload: {
  name: string;
  address?: string;
  phone?: string;
  workingHours?: string;
}): Promise<ClinicTenantSettings> {
  const { data } = await http.put<UnknownRow>('/clinic/settings', payload);
  return normalizeClinicSettings(data);
}

export interface ClinicAppointment {
  id: string;
  customerId: string;
  customerName?: string;
  customerPhone?: string;
  providerId?: string;
  providerDisplayName?: string;
  branchId?: string;
  appointmentAt: string;
  durationMinutes: number;
  appointmentStatus: string;
  encounterModality: string;
  reason?: string;
  notes?: string;
  createdAt: string;
}

function normalizeAppointment(row: UnknownRow): ClinicAppointment {
  return {
    id: String(row.id ?? row.Id),
    customerId: String(row.customerId ?? row.CustomerId),
    customerName: optionalString(row.customerName ?? row.CustomerName),
    customerPhone: optionalString(row.customerPhone ?? row.CustomerPhone),
    providerId: optionalString(row.providerId ?? row.ProviderId),
    providerDisplayName: optionalString(row.providerDisplayName ?? row.ProviderDisplayName),
    branchId: optionalString(row.branchId ?? row.BranchId),
    appointmentAt: String(row.appointmentAt ?? row.AppointmentAt ?? ''),
    durationMinutes: Number(row.durationMinutes ?? row.DurationMinutes ?? 30),
    appointmentStatus: String(row.appointmentStatus ?? row.AppointmentStatus ?? ''),
    encounterModality: String(
      row.encounterModality ?? row.EncounterModality ?? 'in_person',
    ),
    reason: optionalString(row.reason ?? row.Reason),
    notes: optionalString(row.notes ?? row.Notes),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
  };
}

export async function fetchClinicAppointments(params?: {
  from?: string;
  to?: string;
  status?: string;
}): Promise<ClinicAppointment[]> {
  const { data } = await http.get<UnknownRow[]>('/clinic/appointments', { params });
  return (Array.isArray(data) ? data : []).map((row) => normalizeAppointment(row as UnknownRow));
}

export async function createClinicAppointment(payload: {
  customerId: string;
  providerId?: string;
  branchId?: string;
  appointmentAt: string;
  durationMinutes?: number;
  reason?: string;
  notes?: string;
  encounterModality?: string;
}): Promise<ClinicAppointment> {
  const { data } = await http.post<UnknownRow>('/clinic/appointments', payload);
  return normalizeAppointment(data);
}

export async function updateClinicAppointmentStatus(
  id: string,
  appointmentStatus: string,
): Promise<ClinicAppointment> {
  const { data } = await http.post<UnknownRow>(`/clinic/appointments/${id}/status`, {
    appointmentStatus,
  });
  return normalizeAppointment(data);
}

export async function rescheduleClinicAppointment(
  id: string,
  payload: {
    appointmentAt: string;
    providerId?: string | null;
    durationMinutes?: number;
    reason?: string;
    notes?: string;
  },
): Promise<ClinicAppointment> {
  const { data } = await http.patch<UnknownRow>(`/clinic/appointments/${id}`, payload);
  return normalizeAppointment(data);
}

export async function checkInClinicAppointment(id: string): Promise<ClinicVisit> {
  const { data } = await http.post<UnknownRow>(`/clinic/appointments/${id}/check-in`);
  return normalizeVisit(data);
}

export interface ClinicVisit {
  id: string;
  appointmentId?: string;
  customerId: string;
  customerName?: string;
  customerPhone?: string;
  providerId?: string;
  providerDisplayName?: string;
  visitStatus: string;
  encounterModality: string;
  chiefComplaint?: string;
  diagnosisSummary?: string;
  startedAt: string;
  closedAt?: string;
  createdAt: string;
  /** Nhà thuốc Connect nguồn (referral/booking) — khóa khi gửi đơn. */
  preferredPharmacyTenantId?: string;
  preferredPharmacyName?: string;
  preferredPharmacyCode?: string;
  /** referral | booking | connect */
  connectSource?: string;
}

function normalizeVisit(row: UnknownRow): ClinicVisit {
  return {
    id: String(row.id ?? row.Id),
    appointmentId: optionalString(row.appointmentId ?? row.AppointmentId),
    customerId: String(row.customerId ?? row.CustomerId),
    customerName: optionalString(row.customerName ?? row.CustomerName),
    customerPhone: optionalString(row.customerPhone ?? row.CustomerPhone),
    providerId: optionalString(row.providerId ?? row.ProviderId),
    providerDisplayName: optionalString(row.providerDisplayName ?? row.ProviderDisplayName),
    visitStatus: String(row.visitStatus ?? row.VisitStatus ?? ''),
    encounterModality: String(
      row.encounterModality ?? row.EncounterModality ?? 'in_person',
    ),
    chiefComplaint: optionalString(row.chiefComplaint ?? row.ChiefComplaint),
    diagnosisSummary: optionalString(row.diagnosisSummary ?? row.DiagnosisSummary),
    startedAt: String(row.startedAt ?? row.StartedAt ?? ''),
    closedAt: optionalString(row.closedAt ?? row.ClosedAt),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    preferredPharmacyTenantId: optionalString(
      row.preferredPharmacyTenantId ?? row.PreferredPharmacyTenantId,
    ),
    preferredPharmacyName: optionalString(
      row.preferredPharmacyName ?? row.PreferredPharmacyName,
    ),
    preferredPharmacyCode: optionalString(
      row.preferredPharmacyCode ?? row.PreferredPharmacyCode,
    ),
    connectSource: optionalString(row.connectSource ?? row.ConnectSource),
  };
}

export async function fetchClinicVisits(params?: {
  customerId?: string;
  status?: string;
  from?: string;
  to?: string;
}): Promise<ClinicVisit[]> {
  const { data } = await http.get<UnknownRow[]>('/clinic/visits', { params });
  return (Array.isArray(data) ? data : []).map((row) => normalizeVisit(row as UnknownRow));
}

export async function fetchClinicVisit(id: string): Promise<ClinicVisit> {
  const { data } = await http.get<UnknownRow>(`/clinic/visits/${id}`);
  return normalizeVisit(data);
}

export interface ClinicDaySummary {
  date: string;
  appointmentsToday: number;
  appointmentsRemoteToday: number;
  appointmentsCheckedIn: number;
  appointmentsNoShow: number;
  visitsOpen: number;
  visitsClosed: number;
  prescriptionsDraft: number;
  prescriptionsFinalized: number;
  prescriptionsSentToPharmacy: number;
}

export async function fetchClinicDaySummary(date?: string): Promise<ClinicDaySummary> {
  const { data } = await http.get<UnknownRow>('/clinic/day-summary', {
    params: date ? { date } : undefined,
  });
  return {
    date: String(data.date ?? data.Date ?? ''),
    appointmentsToday: Number(data.appointmentsToday ?? data.AppointmentsToday ?? 0),
    appointmentsRemoteToday: Number(
      data.appointmentsRemoteToday ?? data.AppointmentsRemoteToday ?? 0,
    ),
    appointmentsCheckedIn: Number(data.appointmentsCheckedIn ?? data.AppointmentsCheckedIn ?? 0),
    appointmentsNoShow: Number(data.appointmentsNoShow ?? data.AppointmentsNoShow ?? 0),
    visitsOpen: Number(data.visitsOpen ?? data.VisitsOpen ?? 0),
    visitsClosed: Number(data.visitsClosed ?? data.VisitsClosed ?? 0),
    prescriptionsDraft: Number(data.prescriptionsDraft ?? data.PrescriptionsDraft ?? 0),
    prescriptionsFinalized: Number(data.prescriptionsFinalized ?? data.PrescriptionsFinalized ?? 0),
    prescriptionsSentToPharmacy: Number(
      data.prescriptionsSentToPharmacy ?? data.PrescriptionsSentToPharmacy ?? 0,
    ),
  };
}

export async function createClinicVisit(payload: {
  customerId: string;
  appointmentId?: string;
  providerId?: string;
  chiefComplaint?: string;
  encounterModality?: string;
}): Promise<ClinicVisit> {
  const { data } = await http.post<UnknownRow>('/clinic/visits', payload);
  return normalizeVisit(data);
}

export async function updateClinicVisit(
  id: string,
  payload: {
    chiefComplaint?: string;
    diagnosisSummary?: string;
    visitStatus?: string;
    providerId?: string | null;
  },
): Promise<ClinicVisit> {
  const { data } = await http.patch<UnknownRow>(`/clinic/visits/${id}`, payload);
  return normalizeVisit(data);
}

export interface ClinicVisitNote {
  id: string;
  visitId: string;
  noteType: string;
  noteBody: string;
  authorUserId?: string;
  createdAt: string;
}

function normalizeNote(row: UnknownRow): ClinicVisitNote {
  return {
    id: String(row.id ?? row.Id),
    visitId: String(row.visitId ?? row.VisitId),
    noteType: String(row.noteType ?? row.NoteType ?? ''),
    noteBody: String(row.noteBody ?? row.NoteBody ?? ''),
    authorUserId: optionalString(row.authorUserId ?? row.AuthorUserId),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
  };
}

export async function fetchClinicVisitNotes(visitId: string): Promise<ClinicVisitNote[]> {
  const { data } = await http.get<UnknownRow[]>(`/clinic/visits/${visitId}/notes`);
  return (Array.isArray(data) ? data : []).map((row) => normalizeNote(row as UnknownRow));
}

export async function addClinicVisitNote(
  visitId: string,
  payload: { noteBody: string; noteType?: string },
): Promise<ClinicVisitNote> {
  const { data } = await http.post<UnknownRow>(`/clinic/visits/${visitId}/notes`, payload);
  return normalizeNote(data);
}

export interface ClinicPrescriptionLine {
  id?: string;
  drugName: string;
  strength?: string;
  quantity: number;
  unit?: string;
  dosageInstruction?: string;
  sortOrder?: number;
}

export interface ClinicPrescription {
  id: string;
  visitId: string;
  customerId: string;
  customerName?: string;
  customerPhone?: string;
  providerId?: string;
  providerDisplayName?: string;
  prescriptionCode: string;
  prescriptionStatus: string;
  diagnosisText?: string;
  notes?: string;
  finalizedAt?: string;
  pdfSha256?: string;
  pharmacyTenantId?: string;
  sentAt?: string;
  connectHandoffId?: string;
  signedAt?: string;
  signatureProvider?: string;
  createdAt: string;
  lines: ClinicPrescriptionLine[];
}

function normalizeRxLine(row: UnknownRow): ClinicPrescriptionLine {
  return {
    id: optionalString(row.id ?? row.Id),
    drugName: String(row.drugName ?? row.DrugName ?? ''),
    strength: optionalString(row.strength ?? row.Strength),
    quantity: Number(row.quantity ?? row.Quantity ?? 1),
    unit: optionalString(row.unit ?? row.Unit),
    dosageInstruction: optionalString(row.dosageInstruction ?? row.DosageInstruction),
    sortOrder: Number(row.sortOrder ?? row.SortOrder ?? 0),
  };
}

function normalizePrescription(row: UnknownRow): ClinicPrescription {
  const linesRaw = (row.lines ?? row.Lines ?? []) as UnknownRow[];
  return {
    id: String(row.id ?? row.Id),
    visitId: String(row.visitId ?? row.VisitId),
    customerId: String(row.customerId ?? row.CustomerId),
    customerName: optionalString(row.customerName ?? row.CustomerName),
    customerPhone: optionalString(row.customerPhone ?? row.CustomerPhone),
    providerId: optionalString(row.providerId ?? row.ProviderId),
    providerDisplayName: optionalString(row.providerDisplayName ?? row.ProviderDisplayName),
    prescriptionCode: String(row.prescriptionCode ?? row.PrescriptionCode ?? ''),
    prescriptionStatus: String(row.prescriptionStatus ?? row.PrescriptionStatus ?? ''),
    diagnosisText: optionalString(row.diagnosisText ?? row.DiagnosisText),
    notes: optionalString(row.notes ?? row.Notes),
    finalizedAt: optionalString(row.finalizedAt ?? row.FinalizedAt),
    pdfSha256: optionalString(row.pdfSha256 ?? row.PdfSha256),
    pharmacyTenantId: optionalString(row.pharmacyTenantId ?? row.PharmacyTenantId),
    sentAt: optionalString(row.sentAt ?? row.SentAt),
    connectHandoffId: optionalString(row.connectHandoffId ?? row.ConnectHandoffId),
    signedAt: optionalString(row.signedAt ?? row.SignedAt),
    signatureProvider: optionalString(row.signatureProvider ?? row.SignatureProvider),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    lines: (Array.isArray(linesRaw) ? linesRaw : []).map((l) => normalizeRxLine(l as UnknownRow)),
  };
}

export async function fetchClinicPrescriptions(visitId: string): Promise<ClinicPrescription[]> {
  const { data } = await http.get<UnknownRow[]>('/clinic/prescriptions', { params: { visitId } });
  return (Array.isArray(data) ? data : []).map((row) => normalizePrescription(row as UnknownRow));
}

export async function createClinicPrescription(payload: {
  visitId: string;
  providerId?: string;
  diagnosisText?: string;
  notes?: string;
  lines: Array<{
    drugName: string;
    strength?: string;
    quantity?: number;
    unit?: string;
    dosageInstruction?: string;
  }>;
}): Promise<ClinicPrescription> {
  const { data } = await http.post<UnknownRow>('/clinic/prescriptions', payload);
  return normalizePrescription(data);
}

export async function updateClinicPrescription(
  id: string,
  payload: {
    providerId?: string;
    diagnosisText?: string;
    notes?: string;
    lines?: Array<{
      drugName: string;
      strength?: string;
      quantity?: number;
      unit?: string;
      dosageInstruction?: string;
    }>;
  },
): Promise<ClinicPrescription> {
  const { data } = await http.patch<UnknownRow>(`/clinic/prescriptions/${id}`, payload);
  return normalizePrescription(data);
}

export async function finalizeClinicPrescription(id: string): Promise<ClinicPrescription> {
  const { data } = await http.post<UnknownRow>(`/clinic/prescriptions/${id}/finalize`);
  return normalizePrescription(data);
}

export async function signClinicPrescription(id: string): Promise<ClinicPrescription> {
  const { data } = await http.post<UnknownRow>(`/clinic/prescriptions/${id}/sign`);
  return normalizePrescription(data);
}

export async function cancelClinicPrescription(id: string): Promise<ClinicPrescription> {
  const { data } = await http.post<UnknownRow>(`/clinic/prescriptions/${id}/cancel`);
  return normalizePrescription(data);
}

export async function sendClinicPrescriptionToPharmacy(
  id: string,
  pharmacyTenantId: string,
): Promise<ClinicPrescription> {
  const { data } = await http.post<UnknownRow>(`/clinic/prescriptions/${id}/send-to-pharmacy`, {
    pharmacyTenantId,
  });
  return normalizePrescription(data);
}

export function clinicPrescriptionPdfUrl(id: string): string {
  return `/api/clinic/prescriptions/${id}/pdf`;
}

export async function downloadClinicPrescriptionPdf(id: string, fileName?: string): Promise<void> {
  const { data } = await http.get<Blob>(`/clinic/prescriptions/${id}/pdf`, {
    responseType: 'blob',
  });
  const url = URL.createObjectURL(data);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName || `${id}.pdf`;
  a.click();
  URL.revokeObjectURL(url);
}
