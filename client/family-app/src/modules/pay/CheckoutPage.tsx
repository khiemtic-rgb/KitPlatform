import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import axios from 'axios';
import {
  buildCheckoutPath,
  createPaymentOrder,
  fetchPaymentSubscription,
  getPaymentOrder,
  listPaymentMethods,
  listPaymentPlans,
  type PaymentMethod,
  type PaymentOrder,
  type PaymentPlan,
  type PaymentSubscription,
} from '@/shared/api/payment.api';
import {
  benefitsForTier,
  heroSkuLabel,
  isYearlyPlan,
  outcomeHeadlineForTier,
  outcomeNameForTier,
  pickHeroCheckoutPlans,
  savingsPercentVsMonthly,
  tierFromPlanCode,
} from '@/shared/billing/famixa-plan-copy';
import {
  buildKitVietQrUrl,
  KIT_BANK_TRANSFER,
} from '@/shared/billing/kit-bank-transfer';
import { useGoBack } from '@/shared/nav/use-go-back';

type Step = 'plan' | 'pay';
/** UI mode: QR scan (PayOS hoặc VietQR công ty) vs chuyển khoản thủ công. */
type PayUiMode = 'qr' | 'transfer';

function formatVnd(amount: number): string {
  return amount.toLocaleString('vi-VN') + 'đ';
}

function errMessage(err: unknown, fallback: string): string {
  if (axios.isAxiosError(err)) {
    const data = err.response?.data as { message?: string } | undefined;
    if (data?.message) return data.message;
  }
  if (err instanceof Error && err.message) return err.message;
  return fallback;
}

function daysUntil(iso?: string): number | null {
  if (!iso) return null;
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return null;
  return Math.ceil((t - Date.now()) / (24 * 60 * 60 * 1000));
}

function isPaid(status: string): boolean {
  return status.trim().toLowerCase() === 'paid';
}

function safeReturnPath(raw: string | null): string {
  if (!raw) return '/today';
  if (!raw.startsWith('/') || raw.startsWith('//')) return '/today';
  return raw;
}

function isAutoPay(code?: string): boolean {
  return (code ?? '').toLowerCase() === 'payos';
}

function formatCountdown(msLeft: number): string {
  if (msLeft <= 0) return '00:00';
  const totalSec = Math.floor(msLeft / 1000);
  const mm = String(Math.floor(totalSec / 60)).padStart(2, '0');
  const ss = String(totalSec % 60).padStart(2, '0');
  return `${mm}:${ss}`;
}

function currentPlanLabel(sub: PaymentSubscription | null): string {
  if (!sub) return 'Famixa Free';
  if (sub.status === 'trial') return 'Famixa Free · dùng thử';
  if (!sub.isEntitled) return outcomeNameForTier(tierFromPlanCode(sub.planCode)) || 'Famixa Free';
  return outcomeNameForTier(tierFromPlanCode(sub.planCode));
}

/**
 * Famixa checkout: Chọn gói → Thanh toán (wave 2 conversion UI).
 * Deep-link: /pay?product=family_os&subjectType=family&subjectId=…&plan=…&return=/who
 */
export function CheckoutPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();

  const productCode = (params.get('product') ?? '').trim();
  const subjectType = (params.get('subjectType') ?? '').trim();
  const subjectId = (params.get('subjectId') ?? '').trim();
  const planCodeParam = (params.get('plan') ?? '').trim();
  const returnPath = safeReturnPath(params.get('return'));
  const backToApp = useGoBack(returnPath);
  const initialOrderCode = Number(params.get('orderCode') ?? 0) || 0;

  const [plans, setPlans] = useState<PaymentPlan[]>([]);
  const [methods, setMethods] = useState<PaymentMethod[]>([]);
  const [sub, setSub] = useState<PaymentSubscription | null>(null);
  const [order, setOrder] = useState<PaymentOrder | null>(null);
  const [selectedPlan, setSelectedPlan] = useState(planCodeParam || 'family_pro_month');
  const [selectedMethod, setSelectedMethod] = useState('payos');
  const [payUiMode, setPayUiMode] = useState<PayUiMode>('qr');
  const [step, setStep] = useState<Step>(initialOrderCode > 0 ? 'pay' : 'plan');
  const [loading, setLoading] = useState(true);
  const [paying, setPaying] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [copyFeedback, setCopyFeedback] = useState<string | null>(null);
  const [nowTick, setNowTick] = useState(() => Date.now());
  const [otherSheetOpen, setOtherSheetOpen] = useState(false);

  const paramsOk = Boolean(productCode && subjectType && subjectId);

  const heroPlans = useMemo(() => pickHeroCheckoutPlans(plans), [plans]);

  const activePlan = useMemo(() => {
    if (!heroPlans.length) {
      const paid = plans.filter((p) => p.planCode !== 'free' && (p.amountVnd ?? 0) > 0);
      return (
        paid.find((p) => p.planCode === selectedPlan) ??
        paid.find((p) => p.planCode === 'family_pro_month') ??
        paid[0] ??
        null
      );
    }
    return (
      heroPlans.find((p) => p.planCode === selectedPlan) ??
      heroPlans.find((p) => p.planCode === 'family_pro_month') ??
      heroPlans[Math.min(1, heroPlans.length - 1)] ??
      heroPlans[0]
    );
  }, [heroPlans, plans, selectedPlan]);

  const payosReady = useMemo(
    () => methods.some((m) => m.providerCode === 'payos' && m.available),
    [methods],
  );
  const manualReady = useMemo(
    () => methods.some((m) => m.providerCode === 'manual' && m.available),
    [methods],
  );

  /** Provider thật gửi API — QR ưu tiên PayOS, không thì manual + VietQR công ty. */
  const activeProvider = useMemo(() => {
    if (payUiMode === 'qr' && payosReady) return 'payos';
    if (manualReady) return 'manual';
    if (payosReady) return 'payos';
    return selectedMethod;
  }, [payUiMode, payosReady, manualReady, selectedMethod]);

  const activeMethod = useMemo(
    () => methods.find((m) => m.providerCode === activeProvider && m.available) ?? null,
    [methods, activeProvider],
  );

  const monthlyForSavings = useMemo(() => {
    if (!activePlan || !isYearlyPlan(activePlan)) return null;
    const tier = tierFromPlanCode(activePlan.planCode);
    return (
      plans.find(
        (p) =>
          !isYearlyPlan(p) &&
          tierFromPlanCode(p.planCode) === tier &&
          (p.amountVnd ?? 0) > 0,
      ) ?? null
    );
  }, [activePlan, plans]);

  const savePct = activePlan
    ? savingsPercentVsMonthly(activePlan, monthlyForSavings)
    : null;

  const loadCatalog = useCallback(async () => {
    if (!paramsOk) {
      setLoading(false);
      setError('Thiếu tham số thanh toán (product, subjectType, subjectId).');
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const [planList, methodList, subscription] = await Promise.all([
        listPaymentPlans(productCode),
        listPaymentMethods(),
        fetchPaymentSubscription({ productCode, subjectType, subjectId }),
      ]);
      setPlans(planList);
      setMethods(methodList);
      setSub(subscription);

      const heroes = pickHeroCheckoutPlans(planList);
      if (planCodeParam && heroes.some((p) => p.planCode === planCodeParam)) {
        setSelectedPlan(planCodeParam);
      } else if (heroes.find((p) => p.planCode === 'family_pro_month')) {
        setSelectedPlan('family_pro_month');
      } else if (heroes[1]) {
        setSelectedPlan(heroes[1].planCode);
      } else if (heroes[0]) {
        setSelectedPlan(heroes[0].planCode);
      }

      const preferred =
        methodList.find((m) => m.providerCode === 'payos' && m.available) ??
        methodList.find((m) => m.providerCode === 'manual' && m.available) ??
        methodList.find((m) => m.available);
      if (preferred) {
        setSelectedMethod(preferred.providerCode);
        setPayUiMode('qr');
      }

      if (initialOrderCode > 0) {
        const existing = await getPaymentOrder({
          orderCode: initialOrderCode,
          productCode,
          subjectId,
        });
        setOrder(existing);
        setStep('pay');
      }
    } catch (err) {
      setError(errMessage(err, 'Không tải được thông tin thanh toán.'));
    } finally {
      setLoading(false);
    }
  }, [
    paramsOk,
    productCode,
    subjectType,
    subjectId,
    planCodeParam,
    initialOrderCode,
  ]);

  useEffect(() => {
    void loadCatalog();
  }, [loadCatalog]);

  useEffect(() => {
    if (!order || isPaid(order.status) || order.status !== 'pending') return;
    if (!order.expiresAt) return;
    const timer = window.setInterval(() => setNowTick(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [order]);

  useEffect(() => {
    if (!order || isPaid(order.status)) return;
    if (order.status !== 'pending') return;
    const timer = window.setInterval(() => {
      void getPaymentOrder({
        orderCode: order.orderCode,
        productCode: order.productCode,
        subjectId: order.subjectId,
      })
        .then((next) => setOrder(next))
        .catch(() => {
          /* keep showing last known order */
        });
    }, 4000);
    return () => window.clearInterval(timer);
  }, [order]);

  const clearPendingOrder = () => {
    setOrder(null);
    setStep('pay');
    navigate(
      buildCheckoutPath({
        productCode,
        subjectType,
        subjectId,
        planCode: activePlan?.planCode,
        returnPath,
      }),
      { replace: true },
    );
  };

  const onPay = async () => {
    if (!paramsOk || !activePlan || !activeMethod) return;
    setPaying(true);
    setError(null);
    try {
      const payReturn = `${window.location.origin}${buildCheckoutPath({
        productCode,
        subjectType,
        subjectId,
        planCode: activePlan.planCode,
        returnPath,
      })}`;
      const created = await createPaymentOrder({
        productCode,
        subjectType,
        subjectId,
        planCode: activePlan.planCode,
        returnUrl: payReturn,
        cancelUrl: payReturn,
        preferredProvider: activeProvider,
      });
      try {
        sessionStorage.setItem('famixa.payUiMode', payUiMode);
      } catch {
        /* ignore */
      }
      setOrder(created);
      navigate(
        buildCheckoutPath({
          productCode,
          subjectType,
          subjectId,
          planCode: activePlan.planCode,
          returnPath,
          orderCode: created.orderCode,
        }),
        { replace: true },
      );
      if (created.checkoutUrl && isAutoPay(created.providerCode ?? activeProvider)) {
        window.location.href = created.checkoutUrl;
      }
    } catch (err) {
      setError(errMessage(err, 'Không tạo được đơn thanh toán.'));
    } finally {
      setPaying(false);
    }
  };

  const copyText = async (text: string, okMsg: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setCopyFeedback(okMsg);
      window.setTimeout(() => setCopyFeedback(null), 2500);
    } catch {
      setCopyFeedback('Chưa copy được — chọn và copy tay nhé');
      window.setTimeout(() => setCopyFeedback(null), 2500);
    }
  };

  const trialDays = daysUntil(sub?.trialEndsAt);
  const periodDays = daysUntil(sub?.currentPeriodEnd);
  const pendingOrder = order && order.status === 'pending';
  const paidOrder = order && isPaid(order.status);
  const orderExpireMs = order?.expiresAt ? Date.parse(order.expiresAt) - nowTick : null;
  const orderExpired = orderExpireMs != null && !Number.isNaN(orderExpireMs) && orderExpireMs <= 0;
  const ctaIsAuto = payUiMode === 'qr' && payosReady;

  const pendingPayMode = useMemo((): PayUiMode => {
    try {
      const raw = sessionStorage.getItem('famixa.payUiMode');
      if (raw === 'transfer' || raw === 'qr') return raw;
    } catch {
      /* ignore */
    }
    return payUiMode;
  }, [payUiMode, order?.orderCode]);

  const pendingQrUrl = useMemo(() => {
    if (!order) return null;
    if (order.qrCode && (order.qrCode.startsWith('data:') || order.qrCode.startsWith('http'))) {
      return order.qrCode;
    }
    // Manual / static VietQR công ty theo từng đơn
    const content = String(order.publicCode || order.description || order.orderCode);
    return buildKitVietQrUrl(order.amountVnd, content);
  }, [order]);

  const otherAvailable = useMemo(
    () =>
      methods.filter(
        (m) => m.available && m.providerCode.toLowerCase() !== 'payos',
      ),
    [methods],
  );

  const walletsComingSoon = useMemo(() => {
    const labels = [
      { code: 'momo', name: 'MoMo' },
      { code: 'zalopay', name: 'ZaloPay' },
      { code: 'vnpay', name: 'VNPay' },
      { code: 'applepay', name: 'Apple Pay' },
    ] as const;
    return labels.filter((w) => {
      const row = methods.find((m) => m.providerCode.toLowerCase() === w.code);
      return !row?.available;
    });
  }, [methods]);

  const selectPayUi = (mode: PayUiMode) => {
    setPayUiMode(mode);
    if (mode === 'qr' && payosReady) setSelectedMethod('payos');
    else if (manualReady) setSelectedMethod('manual');
    if (mode === 'qr') setOtherSheetOpen(false);
  };

  const pickOtherMethod = (providerCode: string) => {
    setSelectedMethod(providerCode);
    setPayUiMode(providerCode.toLowerCase() === 'manual' ? 'transfer' : 'qr');
    setOtherSheetOpen(false);
  };

  const otherMethodLabel = (m: PaymentMethod) => {
    const code = m.providerCode.toLowerCase();
    if (code === 'manual') return 'Chuyển khoản thủ công';
    if (code === 'momo') return 'MoMo';
    if (code === 'zalopay') return 'ZaloPay';
    if (code === 'vnpay') return 'VNPay';
    if (code === 'applepay') return 'Apple Pay';
    return m.displayName || m.providerCode;
  };

  const otherMethodHint = (m: PaymentMethod) => {
    const code = m.providerCode.toLowerCase();
    if (code === 'manual') return 'Sao chép STK BIDV + nội dung mã đơn';
    return 'Thanh toán qua cổng ví';
  };

  const selectedOtherLabel =
    payUiMode === 'transfer' ? 'Chuyển khoản thủ công' : null;

  const goBack = () => {
    if (step === 'pay' && !pendingOrder && !paidOrder) {
      setStep('plan');
      setError(null);
      return;
    }
    backToApp();
  };

  return (
    <div className="checkout-page">
      <header className="checkout-header">
        <button type="button" className="checkout-nav-back" onClick={goBack}>
          ←
        </button>
        <div>
          <p className="checkout-title">
            {paidOrder
              ? 'Thanh toán thành công'
              : pendingOrder
                ? 'Thanh toán'
                : step === 'plan'
                  ? 'Nâng cấp Famixa'
                  : 'Thanh toán'}
          </p>
          <p className="checkout-sub">
            {paidOrder
              ? 'Gia hạn đã kích hoạt'
              : pendingOrder
                ? 'Quét QR hoặc chuyển khoản đúng mã đơn'
                : step === 'plan'
                  ? 'Chọn gói phù hợp'
                  : activePlan
                    ? heroSkuLabel(
                        activePlan,
                        Math.max(0, heroPlans.findIndex((p) => p.planCode === activePlan.planCode)),
                      )
                    : 'Xác nhận & thanh toán'}
          </p>
        </div>
      </header>

      {loading ? (
        <p className="muted">Đang tải gói…</p>
      ) : (
        <>
          {error ? <div className="banner-error">{error}</div> : null}

          {paidOrder ? (
            <section className="checkout-card checkout-success">
              <h2>Thanh toán thành công</h2>
              <p>
                Đã nhận {formatVnd(order.amountVnd)} — mã{' '}
                <code>{order.publicCode || order.description}</code>
              </p>
              <button type="button" className="btn btn-primary" onClick={backToApp}>
                Quay lại app
              </button>
            </section>
          ) : null}

          {!paidOrder && step === 'plan' && !pendingOrder ? (
            <>
              {sub ? (
                <section className="checkout-current">
                  <div>
                    <span className="checkout-current-label">Gói hiện tại</span>
                    <strong>{currentPlanLabel(sub)}</strong>
                    {sub.status === 'trial' && trialDays != null ? (
                      <span className="checkout-current-meta">
                        Hết hạn sau {Math.max(0, trialDays)} ngày
                      </span>
                    ) : null}
                    {sub.status === 'active' && sub.isEntitled && periodDays != null ? (
                      <span className="checkout-current-meta">
                        Còn {Math.max(0, periodDays)} ngày
                      </span>
                    ) : null}
                    {!sub.isEntitled && sub.status !== 'trial' ? (
                      <span className="checkout-current-meta">Đã hết hạn — nâng cấp để tiếp tục</span>
                    ) : null}
                  </div>
                  <span className="checkout-current-cta">Nâng cấp</span>
                </section>
              ) : null}

              <section className="checkout-card">
                <h2>Chọn gói phù hợp</h2>
                {heroPlans.length === 0 ? (
                  <p className="muted">Chưa có gói active cho sản phẩm này.</p>
                ) : (
                  <ul className="checkout-plan-list">
                    {heroPlans.map((p, idx) => {
                      const tier = tierFromPlanCode(p.planCode);
                      const isActive = p.planCode === activePlan?.planCode;
                      const popular =
                        p.planCode === 'family_pro_month' ||
                        (idx === 1 && tier === 'pro' && !isYearlyPlan(p));
                      const monthRef =
                        isYearlyPlan(p)
                          ? plans.find(
                              (m) =>
                                !isYearlyPlan(m) &&
                                tierFromPlanCode(m.planCode) === tier &&
                                (m.amountVnd ?? 0) > 0,
                            ) ?? null
                          : null;
                      const pct = savingsPercentVsMonthly(p, monthRef);
                      const benefits = benefitsForTier(tier);
                      return (
                        <li key={p.planCode}>
                          <button
                            type="button"
                            className={`checkout-plan-card${isActive ? ' is-active' : ''}${popular ? ' is-popular' : ''}`}
                            onClick={() => setSelectedPlan(p.planCode)}
                          >
                            <span className="checkout-plan-card-top">
                              <span className="checkout-outcome">
                                {outcomeHeadlineForTier(tier)}
                              </span>
                              {popular ? (
                                <em className="checkout-badge">Được đề xuất</em>
                              ) : null}
                              {pct != null && pct > 0 ? (
                                <em className="checkout-badge is-save">Tiết kiệm {pct}%</em>
                              ) : null}
                            </span>
                            <strong className="checkout-plan-name">{heroSkuLabel(p, idx)}</strong>
                            <span className="checkout-plan-price">
                              {formatVnd(p.amountVnd)}
                              <small>
                                {' '}
                                / {isYearlyPlan(p) ? 'năm' : `${p.intervalDays} ngày`}
                              </small>
                            </span>
                            <span className="checkout-plan-pitch">
                              {tier === 'pro'
                                ? 'Giúp AI đồng hành cùng cả nhà, giảm việc nhắc nhở mỗi ngày.'
                                : tier === 'plus'
                                  ? 'Theo dõi tăng trưởng nhà với timeline và đề xuất vừa đủ.'
                                  : 'Nâng trải nghiệm Famixa cho cả gia đình.'}
                            </span>
                            <ul className="checkout-benefits">
                              {benefits.map((b) => (
                                <li key={b}>✔ {b}</li>
                              ))}
                            </ul>
                            {isActive ? (
                              <span className="checkout-selected-mark">Được chọn</span>
                            ) : null}
                          </button>
                        </li>
                      );
                    })}
                  </ul>
                )}
              </section>

              <button
                type="button"
                className="checkout-cta"
                disabled={!activePlan}
                onClick={() => {
                  setError(null);
                  setStep('pay');
                }}
              >
                <span>Tiếp tục</span>
              </button>
            </>
          ) : null}

          {!paidOrder && (step === 'pay' || pendingOrder) ? (
            <>
              {pendingOrder ? (
                <section className="checkout-pending">
                  <div className="checkout-pending-amount">
                    <span>
                      {pendingPayMode === 'qr' ? 'Quét QR để thanh toán' : 'Chuyển khoản thủ công'}
                    </span>
                    <strong>{formatVnd(order.amountVnd)}</strong>
                    {order.expiresAt && orderExpireMs != null && !Number.isNaN(orderExpireMs) ? (
                      <em className={orderExpired ? 'is-expired' : undefined}>
                        {orderExpired
                          ? 'Đơn đã hết hạn — tạo lại nhé'
                          : `Hiệu lực ${formatCountdown(orderExpireMs)}`}
                      </em>
                    ) : null}
                  </div>

                  {pendingPayMode === 'qr' ? (
                    <>
                      {pendingQrUrl ? (
                        <img
                          className="checkout-qr"
                          src={pendingQrUrl}
                          alt="QR chuyển khoản Famixa"
                        />
                      ) : null}
                      <p className="muted" style={{ textAlign: 'center', margin: 0 }}>
                        Mở app ngân hàng → Quét QR → xác nhận. Không cần nhập tay số TK.
                      </p>
                      <details className="checkout-bank-details">
                        <summary>Xem thông tin tài khoản (nếu cần)</summary>
                        <div className="checkout-bank-card" style={{ marginTop: 10 }}>
                          <div className="checkout-bank-row">
                            <span>Số tài khoản</span>
                            <strong>{KIT_BANK_TRANSFER.accountNumber}</strong>
                            <button
                              type="button"
                              className="checkout-copy-mini"
                              onClick={() =>
                                void copyText(
                                  KIT_BANK_TRANSFER.accountNumber,
                                  'Đã sao chép số tài khoản',
                                )
                              }
                            >
                              Sao chép
                            </button>
                          </div>
                          <div className="checkout-bank-row">
                            <span>Ngân hàng</span>
                            <strong>{KIT_BANK_TRANSFER.bankName}</strong>
                          </div>
                          <div className="checkout-bank-row">
                            <span>Chủ TK</span>
                            <strong>{KIT_BANK_TRANSFER.accountName}</strong>
                          </div>
                          <div className="checkout-bank-row">
                            <span>Nội dung</span>
                            <code>
                              {order.publicCode || order.description || order.orderCode}
                            </code>
                            <button
                              type="button"
                              className="checkout-copy-mini"
                              onClick={() =>
                                void copyText(
                                  String(
                                    order.publicCode || order.description || order.orderCode,
                                  ),
                                  'Đã sao chép nội dung',
                                )
                              }
                            >
                              Sao chép
                            </button>
                          </div>
                          {copyFeedback ? (
                            <p className="checkout-copy-ok">{copyFeedback}</p>
                          ) : null}
                        </div>
                      </details>
                    </>
                  ) : (
                    <>
                      <div className="checkout-bank-card">
                        <p className="checkout-bank-label">Chuyển khoản Internet Banking</p>
                        <div className="checkout-bank-row">
                          <span>Số tài khoản</span>
                          <strong>{KIT_BANK_TRANSFER.accountNumber}</strong>
                          <button
                            type="button"
                            className="checkout-copy-mini"
                            onClick={() =>
                              void copyText(
                                KIT_BANK_TRANSFER.accountNumber,
                                'Đã sao chép số tài khoản',
                              )
                            }
                          >
                            Sao chép
                          </button>
                        </div>
                        <div className="checkout-bank-row">
                          <span>Ngân hàng</span>
                          <strong>{KIT_BANK_TRANSFER.bankName}</strong>
                        </div>
                        <div className="checkout-bank-row">
                          <span>Chủ tài khoản</span>
                          <strong>{KIT_BANK_TRANSFER.accountName}</strong>
                        </div>
                        <div className="checkout-bank-row">
                          <span>Nội dung CK</span>
                          <code>
                            {order.publicCode || order.description || order.orderCode}
                          </code>
                          <button
                            type="button"
                            className="checkout-copy-mini"
                            onClick={() =>
                              void copyText(
                                String(
                                  order.publicCode || order.description || order.orderCode,
                                ),
                                'Đã sao chép nội dung chuyển khoản',
                              )
                            }
                          >
                            Sao chép
                          </button>
                        </div>
                        <div className="checkout-bank-row">
                          <span>Số tiền</span>
                          <strong>{formatVnd(order.amountVnd)}</strong>
                        </div>
                        {copyFeedback ? (
                          <p className="checkout-copy-ok">{copyFeedback}</p>
                        ) : null}
                      </div>
                      <p className="muted" style={{ margin: 0 }}>
                        Nhập đúng số TK + nội dung mã đơn — Famixa tự đối soát, không cần gửi ảnh.
                      </p>
                      {pendingQrUrl ? (
                        <details className="checkout-bank-details">
                          <summary>Hoặc quét QR phụ</summary>
                          <img
                            className="checkout-qr checkout-qr-sm"
                            src={pendingQrUrl}
                            alt="QR phụ"
                            style={{ marginTop: 12 }}
                          />
                        </details>
                      ) : null}
                    </>
                  )}

                  {order.checkoutUrl ? (
                    <a className="btn btn-primary" href={order.checkoutUrl}>
                      Mở cổng thanh toán
                    </a>
                  ) : null}

                  <p className="checkout-waiting" role="status">
                    {orderExpired
                      ? 'Đơn hết hạn.'
                      : 'Famixa đang chờ xác nhận… gia hạn ngay khi nhận tiền.'}
                  </p>

                  <button type="button" className="btn checkout-secondary" onClick={clearPendingOrder}>
                    {orderExpired ? 'Tạo đơn mới' : 'Đổi hình thức thanh toán'}
                  </button>
                </section>
              ) : (
                <>
                  {activePlan ? (
                    <section className="checkout-summary">
                      <h2>Bạn sẽ thanh toán</h2>
                      <div className="checkout-summary-row">
                        <span>
                          {heroSkuLabel(
                            activePlan,
                            Math.max(
                              0,
                              heroPlans.findIndex((p) => p.planCode === activePlan.planCode),
                            ),
                          )}
                        </span>
                        <strong>{formatVnd(activePlan.amountVnd)}</strong>
                      </div>
                      <div className="checkout-summary-row is-muted">
                        <span>
                          {isYearlyPlan(activePlan)
                            ? `${activePlan.intervalDays} ngày · theo năm`
                            : `${activePlan.intervalDays} ngày`}
                          {savePct != null && savePct > 0 ? ` · tiết kiệm ${savePct}%` : ''}
                        </span>
                      </div>
                      <div className="checkout-summary-row is-muted">
                        <span>Thuế / phí</span>
                        <span>Đã gồm (nếu có)</span>
                      </div>
                      <div className="checkout-summary-total">
                        <span>Tổng</span>
                        <strong>{formatVnd(activePlan.amountVnd)}</strong>
                      </div>
                    </section>
                  ) : null}

                  <section className="checkout-card">
                    <h2>Chọn cách thanh toán</h2>
                    {!payosReady && !manualReady && otherAvailable.length === 0 ? (
                      <p className="muted">Chưa có hình thức thanh toán khả dụng.</p>
                    ) : (
                      <ul className="checkout-plan-list">
                        {payosReady || manualReady ? (
                          <li>
                            <button
                              type="button"
                              className={`checkout-method${payUiMode === 'qr' ? ' is-active is-recommended' : ''}`}
                              onClick={() => selectPayUi('qr')}
                            >
                              <span className="checkout-method-main">
                                <strong>Hình thức Quét mã</strong>
                                <em className="checkout-badge">Khuyên dùng</em>
                              </span>
                              <span className="checkout-method-hint">
                                {payosReady
                                  ? 'Mở app ngân hàng / ví → quét → xong trong vài giây'
                                  : 'Chỉ cần quét mã — không nhập số tài khoản'}
                              </span>
                            </button>
                          </li>
                        ) : null}
                        {manualReady || otherAvailable.length > 0 ? (
                          <li>
                            <button
                              type="button"
                              className={`checkout-method checkout-method-other${
                                payUiMode === 'transfer' ? ' is-active' : ''
                              }`}
                              onClick={() => setOtherSheetOpen(true)}
                            >
                              <span className="checkout-method-main">
                                <strong>Hình thức khác</strong>
                                <span className="checkout-method-chevron" aria-hidden>
                                  ›
                                </span>
                              </span>
                              <span className="checkout-method-hint">
                                {selectedOtherLabel
                                  ? `Đã chọn: ${selectedOtherLabel}`
                                  : 'Chuyển khoản thủ công và các ví khác'}
                              </span>
                            </button>
                          </li>
                        ) : null}
                      </ul>
                    )}
                  </section>

                  {otherSheetOpen ? (
                    <div
                      className="sheet-backdrop"
                      role="presentation"
                      onClick={() => setOtherSheetOpen(false)}
                    >
                      <div
                        className="sheet checkout-other-sheet"
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="checkout-other-title"
                        onClick={(e) => e.stopPropagation()}
                      >
                        <h2 id="checkout-other-title">Hình thức khác</h2>
                        <p className="muted checkout-other-lead">
                          Chọn một cách thanh toán bên dưới.
                        </p>
                        <ul className="checkout-plan-list">
                          {otherAvailable.map((m) => {
                            const code = m.providerCode.toLowerCase();
                            const active =
                              (code === 'manual' && payUiMode === 'transfer') ||
                              (code !== 'manual' &&
                                selectedMethod.toLowerCase() === code &&
                                payUiMode !== 'transfer');
                            return (
                              <li key={m.providerCode}>
                                <button
                                  type="button"
                                  className={`checkout-method${active ? ' is-active' : ''}`}
                                  onClick={() => pickOtherMethod(m.providerCode)}
                                >
                                  <span className="checkout-method-main">
                                    <strong>{otherMethodLabel(m)}</strong>
                                  </span>
                                  <span className="checkout-method-hint">
                                    {otherMethodHint(m)}
                                  </span>
                                </button>
                              </li>
                            );
                          })}
                        </ul>
                        {walletsComingSoon.length > 0 ? (
                          <p className="checkout-wallets-soon">
                            Ví sắp mở:{' '}
                            {walletsComingSoon.map((w) => w.name).join(' · ')}
                          </p>
                        ) : null}
                        <button
                          type="button"
                          className="btn checkout-secondary"
                          onClick={() => setOtherSheetOpen(false)}
                        >
                          Đóng
                        </button>
                      </div>
                    </div>
                  ) : null}

                  <button
                    type="button"
                    className="checkout-cta"
                    disabled={paying || !activePlan || !paramsOk || !activeMethod}
                    onClick={() => void onPay()}
                  >
                    {paying ? (
                      <span>Đang tạo đơn…</span>
                    ) : (
                      <>
                        <span>{ctaIsAuto ? 'Thanh toán ngay' : 'Tiếp tục'}</span>
                        {activePlan ? <strong>{formatVnd(activePlan.amountVnd)}</strong> : null}
                        <small>An toàn & bảo mật</small>
                      </>
                    )}
                  </button>

                  <ul className="checkout-trust">
                    <li>✓ Xác nhận tự động</li>
                    <li>✓ Không cần gửi ảnh chuyển khoản</li>
                    <li>✓ Gia hạn ngay sau thanh toán</li>
                  </ul>

                  <button
                    type="button"
                    className="checkout-link-back"
                    onClick={() => setStep('plan')}
                  >
                    ← Đổi gói
                  </button>
                </>
              )}
            </>
          ) : null}

          {!pendingOrder && !paidOrder ? null : (
            <button type="button" className="checkout-back" onClick={backToApp}>
              ← Quay lại app
            </button>
          )}
        </>
      )}
    </div>
  );
}
