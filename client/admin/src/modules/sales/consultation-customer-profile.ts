import type { CustomerDetail } from '@/shared/api/customer-admin.types';
import type { ConsultationFacts } from '@/shared/api/pharmacy-consultation.api';

export type ConsultationCustomerProfileSnapshot = {
  customerId: string;
  fullName: string;
  customerCode?: string;
  ageYears?: number | null;
  ageMonths?: number | null;
  gender?: string | null;
  dateOfBirth?: string | null;
  clinicalNotes?: string | null;
  capturedAt: string;
};

export function ageYearsFromDateOfBirth(dateOfBirth?: string | null): number | null {
  if (!dateOfBirth?.trim()) return null;
  const dob = new Date(dateOfBirth.trim());
  if (Number.isNaN(dob.getTime())) return null;
  const today = new Date();
  let age = today.getFullYear() - dob.getFullYear();
  const monthDiff = today.getMonth() - dob.getMonth();
  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < dob.getDate())) age -= 1;
  return age >= 0 && age <= 120 ? age : null;
}

/** Customer gender enum: 1 = Nam, 2 = Nữ */
export function consultationGenderFromCustomer(gender?: number | null): string | null {
  if (gender === 1) return 'male';
  if (gender === 2) return 'female';
  return null;
}

export function customerGenderLabelVi(gender?: number | null): string | null {
  if (gender === 1) return 'Nam';
  if (gender === 2) return 'Nữ';
  return null;
}

export function buildCustomerProfileSnapshot(detail: CustomerDetail): ConsultationCustomerProfileSnapshot {
  const ageYears = ageYearsFromDateOfBirth(detail.dateOfBirth);
  return {
    customerId: detail.id,
    fullName: detail.fullName,
    customerCode: detail.customerCode,
    ageYears,
    gender: consultationGenderFromCustomer(detail.gender),
    dateOfBirth: detail.dateOfBirth ?? null,
    clinicalNotes: detail.clinicalNotes?.trim() || null,
    capturedAt: new Date().toISOString(),
  };
}

/** Apply stable profile fields — does not set pregnancy/breastfeeding (session-only). */
export function applyCustomerProfileToFacts(
  facts: ConsultationFacts,
  profile: ConsultationCustomerProfileSnapshot,
): ConsultationFacts {
  return {
    ...facts,
    ageYears: profile.ageYears ?? facts.ageYears ?? null,
    gender: profile.gender ?? facts.gender ?? null,
  };
}

/** Fields commonly needed for safe OTC consultation — used for inline “thiếu thông tin” hints. */
export function getCustomerProfileGaps(detail: CustomerDetail): string[] {
  const gaps: string[] = [];
  if (!detail.dateOfBirth?.trim()) gaps.push('Ngày sinh');
  if (detail.gender !== 1 && detail.gender !== 2) gaps.push('Giới tính');
  if (!detail.addressLine?.trim()) gaps.push('Địa chỉ');
  if (!detail.clinicalNotes?.trim()) gaps.push('Dị ứng / ghi chú');
  return gaps;
}

export function hasIncompleteCustomerProfile(detail: CustomerDetail): boolean {
  return getCustomerProfileGaps(detail).length > 0;
}

export function formatCustomerProfileHeadline(profile: ConsultationCustomerProfileSnapshot): string {
  const parts = [profile.fullName];
  if (profile.ageYears != null) parts.push(`${profile.ageYears} tuổi`);
  const genderVi =
    profile.gender === 'male' ? 'Nam' : profile.gender === 'female' ? 'Nữ' : null;
  if (genderVi) parts.push(genderVi);
  return parts.join(' · ');
}
