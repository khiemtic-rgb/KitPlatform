/** Shared Famixa plan tier copy for paywall / checkout (no LLM). */

export type FamixaTier = 'free' | 'plus' | 'pro' | 'ai_plus';

export type CheckoutPlanLike = {
  planCode: string;
  amountVnd: number;
  intervalDays: number;
  displayName?: string;
};

export function tierFromPlanCode(planCode?: string | null): FamixaTier {
  const code = (planCode ?? '').trim().toLowerCase();
  if (!code || code === 'free') return 'free';
  if (code.includes('ai_plus') || code.includes('ai-plus')) return 'ai_plus';
  if (code.includes('pro') || code.includes('peace')) return 'pro';
  if (code.includes('plus') || code === 'starter_month' || code === 'starter_trial') return 'plus';
  if (code.includes('growth')) return 'plus';
  if (code.includes('trial')) return 'pro';
  return 'pro';
}

export function outcomeNameForTier(tier: FamixaTier): string {
  switch (tier) {
    case 'ai_plus':
      return 'Đồng hành AI chuyên sâu';
    case 'pro':
      return 'Family Peace';
    case 'plus':
      return 'Famixa Growth';
    default:
      return 'Trải nghiệm Famixa';
  }
}

/** Outcome-led headline — bán kết quả, không bán SKU. */
export function outcomeHeadlineForTier(tier: FamixaTier): string {
  switch (tier) {
    case 'ai_plus':
      return 'AI đồng hành sâu hơn';
    case 'pro':
      return 'Gia đình tự giác hơn';
    case 'plus':
      return 'Nhà có nhịp rõ hơn';
    default:
      return 'Bắt đầu cùng Famixa';
  }
}

export function isYearlyPlan(plan: CheckoutPlanLike): boolean {
  const code = plan.planCode.toLowerCase();
  return plan.intervalDays >= 300 || code.includes('year') || code.includes('_yr');
}

/** Checkout wave-1: tối đa 3 hero — Growth · Growth+/Peace tháng · Peace năm. Ẩn AI SKU. */
export function pickHeroCheckoutPlans<T extends CheckoutPlanLike>(plans: T[]): T[] {
  const paid = plans.filter(
    (p) => p.planCode !== 'free' && (p.amountVnd ?? 0) > 0 && tierFromPlanCode(p.planCode) !== 'ai_plus',
  );
  if (paid.length === 0) return [];

  const monthly = paid
    .filter((p) => !isYearlyPlan(p))
    .sort((a, b) => a.amountVnd - b.amountVnd);
  const yearly = paid
    .filter((p) => isYearlyPlan(p))
    .sort((a, b) => b.amountVnd - a.amountVnd);

  const growth =
    monthly.find((p) => tierFromPlanCode(p.planCode) === 'plus') ?? monthly[0] ?? null;
  const recommended =
    monthly.find(
      (p) =>
        p.planCode === 'family_pro_month' ||
        (tierFromPlanCode(p.planCode) === 'pro' && p !== growth),
    ) ??
    monthly.find((p) => p !== growth) ??
    null;
  const peaceYear =
    yearly.find((p) => tierFromPlanCode(p.planCode) === 'pro') ?? yearly[0] ?? null;

  const slots: T[] = [];
  const push = (p: T | null) => {
    if (!p) return;
    if (slots.some((s) => s.planCode === p.planCode)) return;
    if (slots.length >= 3) return;
    slots.push(p);
  };
  push(growth);
  push(recommended);
  push(peaceYear);
  for (const p of paid) push(p);
  return slots;
}

export function heroSkuLabel(plan: CheckoutPlanLike, slotIndex: number): string {
  const tier = tierFromPlanCode(plan.planCode);
  if (isYearlyPlan(plan) && tier === 'pro') return 'Famixa Peace';
  if (slotIndex === 0 || tier === 'plus') return isYearlyPlan(plan) ? 'Famixa Growth+' : 'Famixa Growth';
  if (tier === 'pro') return isYearlyPlan(plan) ? 'Famixa Peace' : 'Famixa Growth+';
  return outcomeNameForTier(tier);
}

export function benefitsForTier(tier: FamixaTier): string[] {
  switch (tier) {
    case 'ai_plus':
      return AI_PLUS_BENEFITS.slice(0, 4);
    case 'pro':
      return [
        'AI Coach',
        'Không giới hạn thành viên',
        'Routine thông minh',
        'Báo cáo gia đình',
      ];
    case 'plus':
      return ['Timeline kỷ niệm', 'Behavior Twin', 'AI đề xuất', 'Tối đa 2 trẻ'];
    default:
      return ['Routine cơ bản', 'Insight tuần'];
  }
}

/** Ước tiết kiệm so với trả tháng (nếu có gói tháng cùng tier). */
export function savingsPercentVsMonthly(
  yearly: CheckoutPlanLike,
  monthly: CheckoutPlanLike | null,
): number | null {
  if (!monthly || monthly.amountVnd <= 0 || yearly.amountVnd <= 0) return null;
  const months = Math.max(1, Math.round(yearly.intervalDays / 30));
  const full = monthly.amountVnd * months;
  if (full <= yearly.amountVnd) return null;
  return Math.round(((full - yearly.amountVnd) / full) * 100);
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
