export interface PrescriberProfile {
  id: string;
  fullName: string;
  licenseNumber: string | null;
  phone: string;
  specialty: string | null;
  status: string;
}

export interface PrescriberLoginResponse {
  accessToken: string;
  expiresAt: string;
  profile: PrescriberProfile;
}

export interface PrescriberOtpSentResponse {
  expiresInSeconds: number;
  cooldownSeconds: number;
  message: string;
  pilotCode?: string | null;
}

export interface PrescriberPharmacyLink {
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
}

export interface PharmacyDirectoryEntry {
  tenantId: string;
  tenantCode: string;
  tenantName: string;
  address: string | null;
  phone: string | null;
}

export interface PortalCustomerItem {
  id: string;
  customerCode: string;
  fullName: string;
  phone: string | null;
}

export interface PortalProductUnit {
  id: string;
  unitName: string;
  isBaseUnit: boolean;
  conversionFactor?: number;
}

export interface PortalProductItem {
  productId: string;
  productCode: string;
  productName: string;
  dispensingClass: string;
  defaultUnitId: string | null;
  defaultUnitName: string | null;
  units?: PortalProductUnit[];
  /** Base-unit stock across warehouses (warning only). */
  stockAvailableQty?: number;
}

export interface PortalPrescriptionSummary {
  id: string;
  tenantId: string;
  tenantCode: string;
  tenantName: string;
  prescriptionCode: string;
  status: string;
  source: string;
  patientName: string | null;
  patientPhone: string | null;
  signedAt: string | null;
  expiresAt: string | null;
  createdAt: string;
  lineCount: number;
}

export interface PortalPrescriptionLine {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  productUnitId?: string | null;
  unitName?: string | null;
  lineDispensingClass?: string;
  qtyPrescribed: number;
  qtyDispensed?: number;
  qtyRemaining?: number;
  dosageInstruction: string | null;
  sortOrder?: number;
}

export interface PortalPrescriptionDetail extends PortalPrescriptionSummary {
  customerId: string | null;
  notes: string | null;
  lines: PortalPrescriptionLine[];
  posDeepLink?: string | null;
}

export function canAmendPortalPrescription(detail: PortalPrescriptionDetail): boolean {
  if (detail.status !== 'signed') return false;
  return detail.lines.every((line) => Number(line.qtyDispensed ?? 0) <= 0);
}

export interface PortalPrescriberDashboard {
  signedThisMonth: number;
  signedTotal: number;
  activePharmacyCount: number;
  byTenant: Array<{
    tenantId: string;
    tenantCode: string;
    tenantName: string;
    signedThisMonth: number;
    signedTotal: number;
  }>;
}

export interface PortalPrescriptionShare {
  prescriptionId: string;
  prescriptionCode: string;
  posDeepLink: string;
}
