import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import axios from 'axios';
import {
  buildCheckoutPath,
  createPaymentOrder,
  fetchPaymentSubscription,
  getPaymentOrder,
  listPaymentMethods,
  listPaymentPlans,
  paymentProductLabel,
  type PaymentMethod,
  type PaymentOrder,
  type PaymentPlan,
  type PaymentSubscription,
} from '@/shared/api/payment.api';

function formatVnd(amount: number): string {
  return amount.toLocaleString('vi-VN') + '₫';
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
  if (!raw) return '/who';
  if (!raw.startsWith('/') || raw.startsWith('//')) return '/who';
  return raw;
}

function methodLabel(code?: string): string {
  switch ((code ?? '').toLowerCase()) {
    case 'payos':
      return 'VietQR (PayOS)';
    case 'manual':
      return 'Chuyển khoản ngân hàng';
    case 'momo':
      return 'MoMo';
    case 'zalopay':
      return 'ZaloPay';
    case 'vnpay':
      return 'VNPay';
    default:
      return code || '—';
  }
}

/**
 * Shared Kit Payment checkout shell.
 * Deep-link: /pay?product=family_os&subjectType=family&subjectId=…&plan=starter_month&return=/who
 */
export function CheckoutPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();

  const productCode = (params.get('product') ?? '').trim();
  const subjectType = (params.get('subjectType') ?? '').trim();
  const subjectId = (params.get('subjectId') ?? '').trim();
  const planCodeParam = (params.get('plan') ?? '').trim();
  const returnPath = safeReturnPath(params.get('return'));
  const initialOrderCode = Number(params.get('orderCode') ?? 0) || 0;

  const [plans, setPlans] = useState<PaymentPlan[]>([]);
  const [methods, setMethods] = useState<PaymentMethod[]>([]);
  const [sub, setSub] = useState<PaymentSubscription | null>(null);
  const [order, setOrder] = useState<PaymentOrder | null>(null);
  const [selectedPlan, setSelectedPlan] = useState(planCodeParam || 'family_pro_month');
  const [selectedMethod, setSelectedMethod] = useState('manual');
  const [loading, setLoading] = useState(true);
  const [paying, setPaying] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const productLabel = paymentProductLabel(productCode);
  const paramsOk = Boolean(productCode && subjectType && subjectId);

  const checkoutPlans = useMemo(
    () => plans.filter((p) => p.planCode !== 'free' && (p.amountVnd ?? 0) > 0),
    [plans],
  );

  const activePlan = useMemo(() => {
    if (!checkoutPlans.length) return null;
    return (
      checkoutPlans.find((p) => p.planCode === selectedPlan) ??
      checkoutPlans.find((p) => p.planCode === 'family_pro_month') ??
      checkoutPlans.find((p) => p.planCode === 'plus_month') ??
      checkoutPlans.find((p) => p.planCode === 'starter_month') ??
      checkoutPlans[0]
    );
  }, [checkoutPlans, selectedPlan]);

  const activeMethod = useMemo(
    () => methods.find((m) => m.providerCode === selectedMethod) ?? null,
    [methods, selectedMethod],
  );

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
      const paid = planList.filter((p) => p.planCode !== 'free' && (p.amountVnd ?? 0) > 0);
      if (planCodeParam) setSelectedPlan(planCodeParam);
      else if (paid.find((p) => p.planCode === 'family_pro_month'))
        setSelectedPlan('family_pro_month');
      else if (paid[0]) setSelectedPlan(paid[0].planCode);

      const preferred =
        methodList.find((m) => m.providerCode === 'payos' && m.available) ??
        methodList.find((m) => m.providerCode === 'manual' && m.available) ??
        methodList.find((m) => m.available);
      if (preferred) setSelectedMethod(preferred.providerCode);

      if (initialOrderCode > 0) {
        const existing = await getPaymentOrder({
          orderCode: initialOrderCode,
          productCode,
          subjectId,
        });
        setOrder(existing);
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
    if (!paramsOk || !activePlan || !activeMethod?.available) return;
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
        preferredProvider: activeMethod.providerCode,
      });
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
      if (created.checkoutUrl) {
        window.location.href = created.checkoutUrl;
      }
    } catch (err) {
      setError(errMessage(err, 'Không tạo được đơn thanh toán.'));
    } finally {
      setPaying(false);
    }
  };

  const trialDays = daysUntil(sub?.trialEndsAt);
  const periodDays = daysUntil(sub?.currentPeriodEnd);

  return (
    <div className="checkout-page">
      <header className="checkout-header">
        <p className="brand-mark" style={{ margin: 0 }}>
          KIT Pay
        </p>
        <p className="brand-sub" style={{ margin: 0 }}>
          Thanh toán {productLabel}
        </p>
      </header>

      {loading ? (
        <p className="muted">Đang tải gói…</p>
      ) : (
        <>
          {error ? <div className="banner-error">{error}</div> : null}

          {sub ? (
            <section className="checkout-card">
              <h2>Gói hiện tại</h2>
              <p>
                Trạng thái: <strong>{sub.status}</strong>
                {sub.isEntitled ? ' · đang có quyền dùng' : ' · đã hết hạn'}
              </p>
              {sub.status === 'trial' && trialDays != null ? (
                <p className="muted">Dùng thử còn khoảng {trialDays} ngày.</p>
              ) : null}
              {sub.status === 'active' && periodDays != null ? (
                <p className="muted">Chu kỳ còn khoảng {periodDays} ngày.</p>
              ) : null}
            </section>
          ) : null}

          {order && isPaid(order.status) ? (
            <section className="checkout-card checkout-success">
              <h2>Thanh toán thành công</h2>
              <p>
                Đã nhận {formatVnd(order.amountVnd)} — mã{' '}
                <code>{order.publicCode || order.description}</code>
              </p>
              <Link className="btn btn-primary" to={returnPath}>
                Quay lại app
              </Link>
            </section>
          ) : (
            <>
              <section className="checkout-card">
                <h2>Chọn gói</h2>
                {checkoutPlans.length === 0 ? (
                  <p className="muted">Chưa có gói active cho sản phẩm này.</p>
                ) : (
                  <ul className="checkout-plan-list">
                    {checkoutPlans.map((p) => (
                      <li key={p.planCode}>
                        <button
                          type="button"
                          className={
                            p.planCode === activePlan?.planCode
                              ? 'checkout-plan is-active'
                              : 'checkout-plan'
                          }
                          onClick={() => setSelectedPlan(p.planCode)}
                          disabled={Boolean(order && order.status === 'pending')}
                        >
                          <strong>{p.displayName || p.planCode}</strong>
                          <span>
                            {formatVnd(p.amountVnd)} / {p.intervalDays} ngày
                          </span>
                          {p.planCode.includes('pro') ? (
                            <em className="muted"> · Gói khuyến nghị</em>
                          ) : null}
                        </button>
                      </li>
                    ))}
                  </ul>
                )}
              </section>

              {order && order.status === 'pending' ? (
                <section className="checkout-card">
                  <h2>Đơn đang chờ</h2>
                  <p>
                    Hình thức: <strong>{methodLabel(order.providerCode)}</strong>
                  </p>
                  <p>
                    Số tiền: <strong>{formatVnd(order.amountVnd)}</strong>
                  </p>
                  <p>
                    Nội dung / mã đối soát:{' '}
                    <code>{order.publicCode || order.description || order.orderCode}</code>
                  </p>
                  {(order.providerCode ?? '').toLowerCase() === 'manual' ? (
                    <p className="muted">
                      Chuyển khoản đúng mã này — hệ thống tự đối soát từng giao dịch.
                    </p>
                  ) : (
                    <p className="muted">Hoàn tất trên cổng thanh toán, trang sẽ tự cập nhật.</p>
                  )}
                  {order.qrCode &&
                  (order.qrCode.startsWith('data:') || order.qrCode.startsWith('http')) ? (
                    <img
                      className="checkout-qr"
                      src={order.qrCode}
                      alt="VietQR thanh toán"
                    />
                  ) : null}
                  {order.checkoutUrl ? (
                    <a className="btn btn-primary" href={order.checkoutUrl}>
                      Mở cổng thanh toán
                    </a>
                  ) : null}
                  <p className="muted" style={{ marginTop: 8 }}>
                    Đang chờ xác nhận… trang tự cập nhật khi thanh toán xong.
                  </p>
                  <button
                    type="button"
                    className="btn checkout-secondary"
                    onClick={clearPendingOrder}
                  >
                    Đổi hình thức thanh toán
                  </button>
                </section>
              ) : (
                <>
                  <section className="checkout-card">
                    <h2>Hình thức thanh toán</h2>
                    {methods.length === 0 ? (
                      <p className="muted">Chưa có hình thức thanh toán.</p>
                    ) : (
                      <ul className="checkout-plan-list">
                        {methods.map((m) => (
                          <li key={m.providerCode}>
                            <button
                              type="button"
                              className={
                                m.providerCode === selectedMethod
                                  ? 'checkout-plan is-active'
                                  : 'checkout-plan'
                              }
                              disabled={!m.available}
                              onClick={() => setSelectedMethod(m.providerCode)}
                            >
                              <strong>{m.displayName}</strong>
                              <span>{m.description}</span>
                              {!m.available ? (
                                <em className="checkout-method-soon">
                                  {m.unavailableReason || 'Sắp ra mắt'}
                                </em>
                              ) : null}
                            </button>
                          </li>
                        ))}
                      </ul>
                    )}
                  </section>

                  <button
                    type="button"
                    className="btn btn-primary"
                    disabled={
                      paying || !activePlan || !paramsOk || !activeMethod?.available
                    }
                    onClick={() => void onPay()}
                  >
                    {paying
                      ? 'Đang tạo đơn…'
                      : activePlan
                        ? `Thanh toán ${formatVnd(activePlan.amountVnd)}`
                        : 'Thanh toán'}
                  </button>
                </>
              )}
            </>
          )}

          <Link className="checkout-back" to={returnPath}>
            ← Quay lại
          </Link>
        </>
      )}
    </div>
  );
}
