import type {
  ConsultationFacts,
  ConsultationPreliminaryAssessment,
  ConsultationProductSuggestion,
} from '@/shared/api/pharmacy-consultation.api';
import type { ConsultationCustomerProfileSnapshot } from '@/modules/sales/consultation-customer-profile';

export function formatDaysAgo(isoDate: string): string {
  const then = new Date(isoDate);
  if (Number.isNaN(then.getTime())) return '';
  const diffMs = Date.now() - then.getTime();
  const days = Math.floor(diffMs / (1000 * 60 * 60 * 24));
  if (days <= 0) return 'Hôm nay';
  if (days === 1) return 'Hôm qua';
  return `${days} ngày trước`;
}

export function formatGenderVi(gender?: string | null): string | null {
  if (gender === 'male') return 'Nam';
  if (gender === 'female') return 'Nữ';
  return null;
}

export function buildSessionSummaryLines(input: {
  profile?: ConsultationCustomerProfileSnapshot | null;
  facts: ConsultationFacts;
  labelByCode: Map<string, string>;
  preliminaryAssessment?: ConsultationPreliminaryAssessment | null;
  naturalLanguage?: string;
  pickedProductNames?: string[];
  suggestedCount?: number;
}): string[] {
  const lines: string[] = [];
  const gender = formatGenderVi(input.facts.gender ?? input.profile?.gender);
  const age = input.facts.ageYears ?? input.profile?.ageYears;
  const demo: string[] = [];
  if (gender) demo.push(gender);
  if (age != null) demo.push(`${age} tuổi`);
  if (demo.length > 0) lines.push(demo.join(', '));

  const symptomLabels = input.facts.symptoms.map((c) => input.labelByCode.get(c) ?? c);
  if (symptomLabels.length > 0) lines.push(symptomLabels.join(' · '));

  if (input.facts.durationDays != null && input.facts.durationDays > 0) {
    lines.push(`Khởi phát khoảng ${input.facts.durationDays} ngày`);
  }
  if (input.facts.hasFever === true) lines.push('Có sốt');
  else if (input.facts.hasFever === false) lines.push('Chưa ghi nhận sốt');

  if (input.naturalLanguage?.trim()) {
    lines.push(`Khách mô tả: “${input.naturalLanguage.trim()}”`);
  }

  if (input.preliminaryAssessment?.headlineVi) {
    lines.push(`Nhận định hỗ trợ: ${input.preliminaryAssessment.headlineVi}`);
  }

  if (input.pickedProductNames && input.pickedProductNames.length > 0) {
    lines.push(`Sản phẩm đã tư vấn: ${input.pickedProductNames.join(', ')}`);
  } else if (input.suggestedCount != null && input.suggestedCount > 0) {
    lines.push(`Có ${input.suggestedCount} sản phẩm tham khảo (chưa chọn)`);
  }

  return lines;
}

export function symptomLabelsFromCodes(codes: string[], labelByCode: Map<string, string>): string {
  return codes.map((c) => labelByCode.get(c) ?? c.replace(/_/g, ' ')).join(' · ');
}

export function pickedProductNamesFromSuggestions(
  suggestions: ConsultationProductSuggestion[],
  picked: Set<string>,
): string[] {
  return suggestions.filter((s) => picked.has(s.lookupCode)).map((s) => s.productName);
}
