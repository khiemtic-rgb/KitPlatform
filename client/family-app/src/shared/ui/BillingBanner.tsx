import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { fetchFamilySubscription, type FamilySubscription } from '@/shared/api/family-os.api';
import { outcomeNameForTier, tierFromPlanCode } from '@/shared/billing/famixa-plan-copy';
import { buildTrialLifecycle } from '@/shared/billing/trial-lifecycle';

function daysUntil(iso?: string): number | null {
  if (!iso) return null;
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return null;
  return Math.ceil((t - Date.now()) / (24 * 60 * 60 * 1000));
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

  const trial = useMemo(() => buildTrialLifecycle(sub), [sub]);

  if (!sub) return null;

  const periodDays = daysUntil(sub.currentPeriodEnd);
  const tier = (sub.tierCode ?? 'free') as string;
  const showPeriodEnding =
    sub.isEntitled &&
    sub.status === 'active' &&
    periodDays != null &&
    periodDays <= 7;
  const showPlusNudge =
    sub.isEntitled && tier === 'plus' && !showPeriodEnding && trial.phase === 'paid';
  const showProAiNudge =
    sub.isEntitled &&
    tier === 'pro' &&
    sub.status === 'active' &&
    !showPeriodEnding &&
    Boolean(sub.recommendedUpgradePlanCode);

  const showTrialCard = trial.showCard && (trial.phase === 'trial' || trial.phase === 'grace' || trial.phase === 'free');

  if (!showTrialCard && !showPeriodEnding && !showPlusNudge && !showProAiNudge) {
    return null;
  }

  const planLabel = sub.outcomeNameVi || sub.displayNameVi || 'Famixa';
  const upgradePlan =
    sub.recommendedUpgradePlanCode ||
    (tier === 'pro' ? 'family_ai_plus_month' : 'family_pro_month');
  const upgradeTier = tierFromPlanCode(upgradePlan);
  const upgradeOutcome = outcomeNameForTier(upgradeTier);

  const title = showTrialCard
    ? trial.title
    : showPeriodEnding
      ? 'Sắp hết hạn'
      : showPlusNudge
        ? 'Family Growth Plan'
        : showProAiNudge
          ? 'Family Peace Plan'
          : planLabel;

  const message = showTrialCard
    ? trial.message
    : showPlusNudge
      ? sub.upgradeHintVi ||
        'Plus đã mở Twin/Timeline. Nâng Peace Plan để mở Coach, ROP và Letter.'
      : showProAiNudge
        ? sub.upgradeHintVi ||
          'Peace Plan đang chạy. AI+ thêm playbook tuần và đề xuất sâu hơn.'
        : periodDays != null
          ? `${planLabel} đang hoạt động — còn khoảng ${periodDays} ngày.`
          : `${planLabel} đang hoạt động.`;

  const actionLabel = showTrialCard
    ? trial.cta
    : showPlusNudge
      ? 'Nâng Peace Plan · 199.000đ'
      : showProAiNudge
        ? `Xem ${upgradeOutcome}`
        : 'Gia hạn';

  const goCheckout = () => {
    navigate('/family-admin/settings#billing');
  };

  return (
    <div
      className={`billing-banner${
        trial.warn || showPeriodEnding || showPlusNudge ? ' is-warn' : ''
      }${trial.urgency === 'grace' || trial.urgency === 'day0' ? ' is-urgent' : ''}`}
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
