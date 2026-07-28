/** Shared Famixa plan tier copy for paywall / checkout (no LLM). */

export type FamixaTier = 'free' | 'plus' | 'pro' | 'ai_plus';

export function tierFromPlanCode(planCode?: string | null): FamixaTier {
  const code = (planCode ?? '').trim().toLowerCase();
  if (!code || code === 'free') return 'free';
  if (code.includes('ai_plus') || code.includes('ai-plus')) return 'ai_plus';
  if (code.includes('pro')) return 'pro';
  if (code.includes('plus') || code === 'starter_month' || code === 'starter_trial') return 'plus';
  if (code.includes('trial')) return 'pro';
  return 'pro';
}

export function outcomeNameForTier(tier: FamixaTier): string {
  switch (tier) {
    case 'ai_plus':
      return 'Đồng hành AI chuyên sâu';
    case 'pro':
      return 'Family Peace Plan';
    case 'plus':
      return 'Family Growth Plan';
    default:
      return 'Trải nghiệm Famixa';
  }
}

export function priceLabelForPlan(planCode?: string | null, amountVnd?: number): string {
  if (amountVnd != null && amountVnd > 0) {
    return `${amountVnd.toLocaleString('vi-VN')}đ`;
  }
  const tier = tierFromPlanCode(planCode);
  switch (tier) {
    case 'ai_plus':
      return planCode?.includes('year') ? '3.990.000đ' : '399.000đ';
    case 'pro':
      return planCode?.includes('year') ? '1.990.000đ' : '199.000đ';
    case 'plus':
      return planCode?.includes('year') ? '990.000đ' : '99.000đ';
    default:
      return '0đ';
  }
}

export function blurbForPlan(planCode?: string | null): string {
  const tier = tierFromPlanCode(planCode);
  switch (tier) {
    case 'ai_plus':
      return 'Playbook tuần sâu hơn, Letter/Replay giàu tín hiệu, đề xuất thích nghi mở rộng.';
    case 'pro':
      return 'Coach + ROP + Letter/Replay — định vị AI đồng hành, không phải checklist.';
    case 'plus':
      return 'Timeline, Twin hành vi và AI đề xuất — theo dõi tăng trưởng nhà.';
    default:
      return 'Routine cơ bản và insight tuần.';
  }
}

export const PRO_BENEFITS = [
  'AI Parenting Coach — bớt nhắc, nhẹ tay hơn',
  'ROP / Growth Report — chứng cứ 30 ngày',
  'AI Letter + Family Replay — kỷ niệm tháng',
  'Không giới hạn thành viên',
];

export const AI_PLUS_BENEFITS = [
  'Weekly Deep Playbook — lịch đồng hành tuần',
  'Letter & Replay sâu hơn (nhiều con / twin / tip đã thử)',
  'Adaptive scan mở rộng — cân bằng anh chị, rủi ro tối',
];

export const PLUS_BENEFITS = [
  'Timeline kỷ niệm nhà',
  'Behavior Twin — tín hiệu hành vi',
  'AI đề xuất thích nghi (inbox)',
  'Tối đa 2 trẻ',
];
