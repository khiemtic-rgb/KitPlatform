import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { fetchFamilySubscription, type FamilySubscription } from '@/shared/api/family-os.api';
import { buildCheckoutPath } from '@/shared/api/payment.api';

const TRIAL_WARN_DAYS = 7;

function daysUntil(iso?: string): number | null {
  if (!iso) return null;
  const t = DateParseSafe(iso);
  if (t == null) return null;
  return Math.ceil((t - Date.now()) / (24 * 60 * 60 * 1000));
}

function DateParseSafe(iso: string): number | null {
  const t = Date.parse(iso);
  return Number.isNaN(t) ? null : t;
}

export function BillingBanner({ familyId }: { familyId: string }) {
  const navigate = useNavigate();
  const [sub, setSub] = useState<FamilySubscription | null>(null);

  useEffect(() => {
    let cancelled = false;
    void fetchFamilySubscription(familyId)
      .then((s) => {
        if (!cancelled) setSub(s);
      })
      .catch(() => {
        /* banner is optional — ignore load errors */
      });
    return () => {
      cancelled = true;
    };
  }, [familyId]);

  if (!sub) return null;

  const trialDays = daysUntil(sub.trialEndsAt);
  const periodDays = daysUntil(sub.currentPeriodEnd);
  const tier = sub.tierCode ?? 'free';
  const showFree = tier === 'free' || !sub.isEntitled;
  const showTrialEnding =
    sub.isEntitled &&
    sub.status === 'trial' &&
    trialDays != null &&
    trialDays <= TRIAL_WARN_DAYS;
  const showPeriodEnding =
    sub.isEntitled &&
    sub.status === 'active' &&
    periodDays != null &&
    periodDays <= TRIAL_WARN_DAYS;

  const planLabel = sub.outcomeNameVi || sub.displayNameVi || 'Famixa';
  const upgradePlan =
    sub.recommendedUpgradePlanCode ||
    (tier === 'plus' ? 'family_pro_month' : 'family_pro_month');

  const message = showFree
    ? sub.upgradeHintVi ||
      'Free: routine cơ bản. Nâng Family Peace Plan để mở Coach, ROP và Letter.'
    : sub.status === 'trial'
      ? trialDays != null
        ? `Dùng thử ${planLabel} — còn khoảng ${trialDays} ngày (trải nghiệm tầng Pro).`
        : `Đang dùng thử ${planLabel}.`
      : periodDays != null
        ? `${planLabel} đang hoạt động — còn khoảng ${periodDays} ngày.`
        : `${planLabel} đang hoạt động.`;

  const title = showFree
    ? 'Gói Free'
    : showTrialEnding || showPeriodEnding
      ? 'Sắp hết hạn'
      : planLabel;

  const actionLabel =
    tier === 'pro' || tier === 'ai_plus'
      ? 'Xem gói / gia hạn'
      : 'Nâng Family Peace Plan · 199.000đ';

  const goCheckout = () => {
    navigate(
      buildCheckoutPath({
        productCode: 'family_os',
        subjectType: 'family',
        subjectId: familyId,
        planCode: upgradePlan,
        returnPath: '/who',
      }),
    );
  };

  return (
    <div className={`billing-banner${showFree || showTrialEnding || showPeriodEnding ? ' is-warn' : ''}`}>
      <div>
        <strong>{title}</strong>
        <p>{message}</p>
      </div>
      <button type="button" className="pill" onClick={goCheckout}>
        {actionLabel}
      </button>
    </div>
  );
}
