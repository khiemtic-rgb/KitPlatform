import { useLocation, useNavigate } from 'react-router-dom';
import { buildCheckoutPath } from '@/shared/api/payment.api';
import type { FamilySubscription } from '@/shared/api/family-os.api';
import {
  AI_PLUS_BENEFITS,
  PRO_BENEFITS,
  outcomeNameForTier,
  priceLabelForPlan,
  tierFromPlanCode,
  type FamixaTier,
} from '@/shared/billing/famixa-plan-copy';

type Props = {
  open: boolean;
  onClose: () => void;
  familyId: string;
  subscription?: FamilySubscription | null;
  /** Optional reason from API capability error. */
  reasonVi?: string | null;
  /** Force a target upgrade plan (overrides subscription recommendation). */
  forcePlanCode?: string | null;
};

function resolveUpgrade(
  subscription?: FamilySubscription | null,
  forcePlanCode?: string | null,
): { planCode: string; targetTier: FamixaTier } {
  if (forcePlanCode?.trim()) {
    const code = forcePlanCode.trim();
    return { planCode: code, targetTier: tierFromPlanCode(code) };
  }
  const recommended = subscription?.recommendedUpgradePlanCode?.trim();
  if (recommended) {
    return { planCode: recommended, targetTier: tierFromPlanCode(recommended) };
  }
  const current = (subscription?.tierCode ?? 'free') as FamixaTier;
  if (current === 'pro') {
    return { planCode: 'family_ai_plus_month', targetTier: 'ai_plus' };
  }
  if (current === 'ai_plus') {
    return { planCode: 'family_ai_plus_month', targetTier: 'ai_plus' };
  }
  return { planCode: 'family_pro_month', targetTier: 'pro' };
}

export function PaywallSheet({
  open,
  onClose,
  familyId,
  subscription,
  reasonVi,
  forcePlanCode,
}: Props) {
  const navigate = useNavigate();
  const location = useLocation();
  if (!open) return null;

  const { planCode, targetTier } = resolveUpgrade(subscription, forcePlanCode);
  const outcome = outcomeNameForTier(targetTier);
  const price =
    targetTier === 'ai_plus'
      ? `${priceLabelForPlan(planCode)} / tháng`
      : targetTier === 'plus'
        ? `${priceLabelForPlan(planCode)} / tháng`
        : `${priceLabelForPlan(planCode)} / tháng`;
  const isFree = !subscription?.isEntitled || subscription?.tierCode === 'free';
  const isPlus = subscription?.tierCode === 'plus' && subscription.isEntitled;
  const isPro = subscription?.tierCode === 'pro' && subscription.isEntitled;
  const isTrial =
    (subscription?.status === 'trial' || subscription?.status === 'trial_grace') &&
    subscription.isEntitled;
  const benefits = targetTier === 'ai_plus' ? AI_PLUS_BENEFITS : PRO_BENEFITS;

  const defaultReason =
    targetTier === 'ai_plus'
      ? isPro
        ? 'Peace Plan đã mở Coach/ROP. AI+ thêm playbook tuần và đề xuất sâu hơn.'
        : 'AI+ dành cho nhà muốn đồng hành chuyên sâu hơn (rule/template, không chat LLM).'
      : isFree
        ? 'Free giữ routine cơ bản. Peace Plan mở Coach, ROP và Letter — đúng pain “mệt vì phải nhắc”.'
        : isPlus
          ? 'Plus đã có Twin/Timeline. Peace Plan mở Coach, ROP và Letter.'
          : isTrial
            ? 'Giữ nhịp Pro sau trial để không mất Coach / ROP / Replay.'
            : 'Nâng gói để mở tính năng AI đồng hành này.';

  const goCheckout = () => {
    onClose();
    navigate(
      buildCheckoutPath({
        productCode: 'family_os',
        subjectType: 'family',
        subjectId: familyId,
        planCode,
        returnPath: `${location.pathname}${location.search}`,
      }),
    );
  };

  return (
    <div className="sheet-backdrop" role="presentation" onClick={onClose}>
      <div
        className="sheet ph-paywall-sheet"
        role="dialog"
        aria-modal="true"
        aria-label={outcome}
        onClick={(e) => e.stopPropagation()}
      >
        <p className="ph-paywall-eyebrow">
          {targetTier === 'ai_plus'
            ? 'Famixa · AI+'
            : 'Famixa · kết quả, không phải checklist'}
        </p>
        <h2>{outcome}</h2>
        <p className="ph-paywall-price">{price}</p>
        <p className="muted">{reasonVi?.trim() || defaultReason}</p>
        <ul className="ph-paywall-benefits">
          {benefits.map((b) => (
            <li key={b}>{b}</li>
          ))}
        </ul>
        <p className="muted ph-paywall-guarantee">
          Nếu sau thời gian dùng bạn không thấy nhà nhẹ hơn hoặc con chủ động hơn — hủy bất cứ lúc nào.
        </p>
        <div className="ph-diary-mem-sheet-actions">
          <button type="button" className="pill" onClick={goCheckout}>
            Chọn {outcome}
          </button>
          <button type="button" className="pill is-soft" onClick={onClose}>
            Để sau
          </button>
        </div>
        <style>{`
          .ph-paywall-sheet h2 { margin: 0 0 4px; font-size: 1.25rem; }
          .ph-paywall-eyebrow {
            margin: 0 0 6px;
            font-size: 11px;
            letter-spacing: 0.07em;
            text-transform: uppercase;
            opacity: 0.65;
          }
          .ph-paywall-price {
            margin: 0 0 12px;
            font-size: 1.05rem;
            font-weight: 700;
            color: #2a5f4a;
          }
          .ph-paywall-benefits {
            margin: 12px 0;
            padding-left: 18px;
            line-height: 1.45;
            font-size: 0.92rem;
          }
          .ph-paywall-guarantee {
            margin: 0 0 14px;
            font-size: 0.85rem;
            line-height: 1.4;
          }
        `}</style>
      </div>
    </div>
  );
}
