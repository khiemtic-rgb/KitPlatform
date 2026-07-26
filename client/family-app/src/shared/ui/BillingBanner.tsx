import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { fetchFamilySubscription, type FamilySubscription } from '@/shared/api/family-os.api';
import { buildCheckoutPath } from '@/shared/api/payment.api';

const TRIAL_WARN_DAYS = 7;

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

  if (!sub) return null;

  const trialDays = daysUntil(sub.trialEndsAt);
  const periodDays = daysUntil(sub.currentPeriodEnd);
  const showExpired = !sub.isEntitled;
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

  const message = showExpired
    ? 'Gói Family OS đã hết hạn — gia hạn để mở lại Daily Flow.'
    : sub.status === 'trial'
      ? trialDays != null
        ? `Đang dùng thử — còn khoảng ${trialDays} ngày.`
        : 'Đang dùng thử Family OS.'
      : periodDays != null
        ? `Gói Starter đang hoạt động — còn khoảng ${periodDays} ngày.`
        : 'Gói Starter đang hoạt động.';
  const title = showExpired
    ? 'Cần gia hạn'
    : showTrialEnding || showPeriodEnding
      ? 'Sắp hết hạn'
      : 'Gói Family OS';
  const actionLabel =
    sub.status === 'trial' && sub.isEntitled
      ? 'Nâng cấp 99.000₫ / tháng'
      : 'Gia hạn 99.000₫ / tháng';

  const goCheckout = () => {
    navigate(
      buildCheckoutPath({
        productCode: 'family_os',
        subjectType: 'family',
        subjectId: familyId,
        planCode: 'starter_month',
        returnPath: '/who',
      }),
    );
  };

  return (
    <div className="banner-now" style={{ animation: 'none' }}>
      <strong>{title}</strong>
      <span style={{ fontWeight: 600 }}>{message}</span>
      <button
        type="button"
        className="btn btn-primary"
        onClick={goCheckout}
        style={{ alignSelf: 'flex-start', marginTop: 4 }}
      >
        {actionLabel}
      </button>
    </div>
  );
}
