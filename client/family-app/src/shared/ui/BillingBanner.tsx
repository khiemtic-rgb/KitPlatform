import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { fetchFamilySubscription, type FamilySubscription } from '@/shared/api/family-os.api';
import { outcomeNameForTier, tierFromPlanCode } from '@/shared/billing/famixa-plan-copy';

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
  const tier = (sub.tierCode ?? 'free') as string;
  const showFree = tier === 'free' || !sub.isEntitled;
  const showTrial = sub.isEntitled && sub.status === 'trial';
  const showTrialEnding =
    showTrial && trialDays != null && trialDays <= TRIAL_WARN_DAYS;
  const showPeriodEnding =
    sub.isEntitled &&
    sub.status === 'active' &&
    periodDays != null &&
    periodDays <= TRIAL_WARN_DAYS;
  const showPlusNudge = sub.isEntitled && tier === 'plus' && !showPeriodEnding;
  const showProAiNudge =
    sub.isEntitled &&
    tier === 'pro' &&
    sub.status === 'active' &&
    !showPeriodEnding &&
    Boolean(sub.recommendedUpgradePlanCode);

  // Quiet only for healthy AI+ (top tier) or quiet Pro without upgrade hint.
  if (
    !showFree &&
    !showTrial &&
    !showPeriodEnding &&
    !showPlusNudge &&
    !showProAiNudge
  ) {
    return null;
  }

  const planLabel = sub.outcomeNameVi || sub.displayNameVi || 'Famixa';
  const upgradePlan =
    sub.recommendedUpgradePlanCode ||
    (tier === 'pro' ? 'family_ai_plus_month' : 'family_pro_month');
  const upgradeTier = tierFromPlanCode(upgradePlan);
  const upgradeOutcome = outcomeNameForTier(upgradeTier);

  const message = showFree
    ? sub.upgradeHintVi ||
      'Free: routine cơ bản. Nâng Family Peace Plan để mở Coach, ROP và Letter.'
    : showTrial
      ? trialDays != null
        ? `Dùng thử ${planLabel} — còn khoảng ${trialDays} ngày (trải nghiệm tầng Pro).`
        : `Đang dùng thử ${planLabel}.`
      : showPlusNudge
        ? sub.upgradeHintVi ||
          'Plus đã mở Twin/Timeline. Nâng Peace Plan để mở Coach, ROP và Letter.'
        : showProAiNudge
          ? sub.upgradeHintVi ||
            'Peace Plan đang chạy. AI+ thêm playbook tuần và đề xuất sâu hơn.'
          : periodDays != null
            ? `${planLabel} đang hoạt động — còn khoảng ${periodDays} ngày.`
            : `${planLabel} đang hoạt động.`;

  const title = showFree
    ? 'Gói Free'
    : showTrialEnding || showPeriodEnding
      ? 'Sắp hết hạn'
      : showTrial
        ? 'Dùng thử Pro'
        : showPlusNudge
          ? 'Family Growth Plan'
          : showProAiNudge
            ? 'Family Peace Plan'
            : planLabel;

  const actionLabel = showTrial
    ? 'Giữ Family Peace Plan · 199.000đ'
    : showPlusNudge
      ? 'Nâng Peace Plan · 199.000đ'
      : showProAiNudge
        ? `Xem ${upgradeOutcome}`
        : showFree
          ? 'Nâng Family Peace Plan · 199.000đ'
          : 'Gia hạn';

  const goCheckout = () => {
    navigate('/family-admin/settings#billing');
  };

  return (
    <div
      className={`billing-banner${
        showFree || showTrialEnding || showPeriodEnding || showPlusNudge
          ? ' is-warn'
          : ''
      }`}
    >
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
