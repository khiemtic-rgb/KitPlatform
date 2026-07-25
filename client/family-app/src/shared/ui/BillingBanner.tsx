import { useEffect, useState } from 'react';
import {
  createFamilyCheckout,
  fetchFamilySubscription,
  type FamilyCheckout,
  type FamilySubscription,
} from '@/shared/api/family-os.api';

const TRIAL_WARN_DAYS = 7;

function daysUntil(iso?: string): number | null {
  if (!iso) return null;
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return null;
  return Math.ceil((t - Date.now()) / (24 * 60 * 60 * 1000));
}

function formatVnd(amount: number): string {
  return amount.toLocaleString('vi-VN') + '₫';
}

export function BillingBanner({ familyId }: { familyId: string }) {
  const [sub, setSub] = useState<FamilySubscription | null>(null);
  const [checkout, setCheckout] = useState<FamilyCheckout | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

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

  const onRenew = async () => {
    setLoading(true);
    setError(null);
    try {
      const created = await createFamilyCheckout(familyId, {
        returnUrl: window.location.href,
        cancelUrl: window.location.href,
      });
      setCheckout(created);
      if (created.checkoutUrl) {
        window.location.href = created.checkoutUrl;
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Không tạo được đơn thanh toán');
    } finally {
      setLoading(false);
    }
  };

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

  return (
    <div className="banner-now" style={{ animation: 'none' }}>
      <strong>{title}</strong>
      <span style={{ fontWeight: 600 }}>{message}</span>
      {error ? <div className="banner-error">{error}</div> : null}
      {checkout && !checkout.checkoutUrl ? (
        <p className="muted" style={{ margin: 0, fontWeight: 600 }}>
          Chuyển khoản {formatVnd(checkout.amountVnd)} — nội dung chuyển đúng mã{' '}
          <code>{checkout.description ?? String(checkout.orderCode)}</code>
          <br />
          <span>Hệ thống tự đối soát theo mã riêng từng giao dịch.</span>
        </p>
      ) : null}
      {checkout?.qrCode &&
      (checkout.qrCode.startsWith('data:') || checkout.qrCode.startsWith('http')) ? (
        <img
          src={checkout.qrCode}
          alt="VietQR thanh toán"
          style={{ width: 180, height: 180, objectFit: 'contain', marginTop: 8 }}
        />
      ) : null}
      <button
        type="button"
        className="btn btn-primary"
        disabled={loading}
        onClick={() => void onRenew()}
        style={{ alignSelf: 'flex-start', marginTop: 4 }}
      >
        {loading ? 'Đang tạo…' : actionLabel}
      </button>
    </div>
  );
}
