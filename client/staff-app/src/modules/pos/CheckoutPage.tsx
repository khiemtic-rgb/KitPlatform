import { useEffect, useMemo, useState } from 'react';
import {
  App,
  Alert,
  Button,
  Collapse,
  Input,
  InputNumber,
  Select,
  Space,
  Switch,
  Tag,
  Typography,
} from 'antd';
import {
  MinusCircleOutlined,
  PhoneOutlined,
  PlusOutlined,
  ShoppingCartOutlined,
} from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import {
  completeDraftSale,
  createSale,
  fetchOpenShift,
  fetchPosCustomerLoyalty,
  fetchPosCustomerVouchers,
  fetchPosStockBulk,
  previewPosAllocation,
  fetchBatchModeSettings,
  fetchRxSettings,
} from '@/shared/api/sales.api';
import {
  SALES_DISCOUNT_TYPES,
  SALES_PAYMENT_CASH,
  SALES_PAYMENT_CREDIT,
  STAFF_PAYMENT_METHOD_OPTIONS,
  type PosCheckoutPaymentLine,
  type PosCustomerLoyalty,
  type PosCustomerVoucher,
  type TenantBatchModeValue,
} from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatMoney } from '@/shared/utils/money';
import { priceCart, roundMoney, lineNet } from '@/modules/sales/pos-pricing';
import { buildCreateSalePayload, buildDraftCompletePayload } from '@/modules/sales/pos-sale-payload';
import { defaultOrderReminderLabel } from '@/modules/sales/order-reminder-label';
import { validateCartBatchLabels } from '@/modules/sales/pos-batch';
import {
  canOfferLoyaltyRedeem,
  computeMaxLoyaltyRedeem,
  loyaltyPointsForDiscount,
  loyaltyPointsValue,
} from '@/modules/sales/pos-loyalty';
import { clearPosDraftEdit } from '@/modules/sales/sales-draft-helpers';
import {
  computeAppliedPayment,
  computeCreditAmount,
  defaultPayments,
  isSingleCashPayment,
  normalizePaymentsForApi,
  paymentsAreValid,
  rebalanceFirstRow,
  sumNonCreditPayments,
} from '@/modules/sales/pos-checkout-payments';
import { usePosSession } from '@/modules/pos/pos-session.store';
import { RX_POS_BLOCK_MESSAGE, shouldBlockRxAtPos } from '@/modules/pos/rx-dispensing';
import type { TenantRxSettings } from '@/shared/api/sales.types';
import { useSalesDiscountPolicy } from '@/modules/sales/useSalesDiscountPolicy';
import { useCanSalesWrite } from '@/shared/auth/usePermission';
import {
  linkReservationSale,
} from '@/shared/api/reservations.api';
import { linkCustomerDraftOrderSale } from '@/shared/api/customer-draft-orders.api';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';

const moneyProps = {
  style: { width: '100%' },
  min: 0 as const,
  formatter: (v: string | number | undefined) => `${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, '.'),
  parser: (v: string | undefined) => Number(String(v ?? '').replace(/\./g, '')) as 0,
};

function digitsOnly(value: string): string {
  return value.replace(/\D/g, '');
}

function hasUsablePhone(phone?: string | null): boolean {
  const d = digitsOnly(phone ?? '');
  return d.length >= 9 && d.length <= 12;
}

function roundUpCash(amount: number, step: number): number {
  if (amount <= 0) return 0;
  return Math.ceil(amount / step) * step;
}

function SummaryRow({
  label,
  value,
  strong,
  muted,
}: {
  label: string;
  value: string;
  strong?: boolean;
  muted?: boolean;
}) {
  return (
    <div className={`checkout-summary-row${strong ? ' is-strong' : ''}${muted ? ' is-muted' : ''}`}>
      <span>{label}</span>
      <span>{value}</span>
    </div>
  );
}

export function CheckoutPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const {
    warehouseId,
    cart,
    customer,
    orderDiscount,
    setOrderDiscount,
    clearCart,
    loadedReservationId,
    loadedReservationNumber,
    loadedCustomerDraftOrderId,
    loadedCustomerDraftNumber,
    loadedPrescriptionId,
    loadedPrescriptionCode,
    editingDraftId,
    editingDraftNumber,
    clearDraftEdit,
  } = usePosSession();

  const { canDiscount, maxPercent } = useSalesDiscountPolicy();
  const canEditCustomerCredit = useCanSalesWrite();
  const paymentMethodOptions = useMemo(
    () =>
      STAFF_PAYMENT_METHOD_OPTIONS.filter(
        (opt) => opt.value !== SALES_PAYMENT_CREDIT || Boolean(customer?.allowCredit),
      ),
    [customer?.allowCredit],
  );
  const [batchMode, setBatchMode] = useState<TenantBatchModeValue>('suggest');
  const [rxSettings, setRxSettings] = useState<TenantRxSettings>({
    enforcementMode: 'off',
    posBlockedAudit: true,
  });
  const [shiftReady, setShiftReady] = useState<boolean | null>(null);
  const [vouchers, setVouchers] = useState<PosCustomerVoucher[]>([]);
  const [selectedVoucherId, setSelectedVoucherId] = useState<string>();
  const [customerLoyalty, setCustomerLoyalty] = useState<PosCustomerLoyalty | null>(null);
  const [redeemEnabled, setRedeemEnabled] = useState(false);
  const [redeemDiscountAmount, setRedeemDiscountAmount] = useState(0);
  const [payments, setPayments] = useState<PosCheckoutPaymentLine[]>([]);
  const [saving, setSaving] = useState(false);
  const [orderReminderEnabled, setOrderReminderEnabled] = useState(false);
  const [orderReminderLabel, setOrderReminderLabel] = useState(() => defaultOrderReminderLabel());
  const [orderReminderDaysSupply, setOrderReminderDaysSupply] = useState(30);

  useEffect(() => {
    if (cart.length === 0) navigate('/pos', { replace: true });
  }, [cart.length, navigate]);

  useEffect(() => {
    if (!warehouseId) {
      setShiftReady(false);
      return;
    }
    void fetchOpenShift(warehouseId)
      .then((shift) => setShiftReady(Boolean(shift)))
      .catch(() => setShiftReady(false));
  }, [warehouseId]);

  useEffect(() => {
    void Promise.all([fetchBatchModeSettings(), fetchRxSettings()]).then(([mode, rx]) => {
      setBatchMode(mode);
      setRxSettings(rx);
    });
  }, []);

  const priced = useMemo(() => priceCart(cart, orderDiscount), [cart, orderDiscount]);
  const selectedVoucher = useMemo(
    () => vouchers.find((v) => v.customerVoucherId === selectedVoucherId),
    [vouchers, selectedVoucherId],
  );
  const voucherDiscount = selectedVoucher ? roundMoney(selectedVoucher.discountAmount) : 0;
  const orderAfterVoucher = roundMoney(Math.max(0, priced.totalAmount - voucherDiscount));
  const maxRedeemMoney = useMemo(
    () => (customerLoyalty ? computeMaxLoyaltyRedeem(orderAfterVoucher, customerLoyalty) : 0),
    [customerLoyalty, orderAfterVoucher],
  );
  const showLoyaltyPanel = Boolean(customerLoyalty?.loyaltyEnabled);
  const canRedeem = canOfferLoyaltyRedeem(customerLoyalty, maxRedeemMoney);
  const loyaltyDiscount =
    redeemEnabled && canRedeem ? roundMoney(Math.max(0, redeemDiscountAmount)) : 0;
  const payableTotal = roundMoney(Math.max(0, orderAfterVoucher - loyaltyDiscount));
  const redeemPointsUsed =
    loyaltyDiscount > 0 && customerLoyalty && customerLoyalty.amountPerPoint > 0
      ? loyaltyPointsForDiscount(loyaltyDiscount, customerLoyalty)
      : 0;
  const isFreeOrder = payableTotal < 0.01;
  const itemQty = useMemo(() => cart.reduce((sum, line) => sum + Number(line.quantity || 0), 0), [cart]);

  const sourceLabel = useMemo(() => {
    if (loadedReservationNumber) return `Giữ hàng ${loadedReservationNumber}`;
    if (loadedCustomerDraftNumber) return `Đơn nháp app ${loadedCustomerDraftNumber}`;
    if (loadedPrescriptionCode) return `Đơn thuốc ${loadedPrescriptionCode}`;
    if (editingDraftNumber) return `Nháp quầy ${editingDraftNumber}`;
    return null;
  }, [
    loadedReservationNumber,
    loadedCustomerDraftNumber,
    loadedPrescriptionCode,
    editingDraftNumber,
  ]);

  useEffect(() => {
    if (!customer?.id || priced.totalAmount <= 0) {
      setVouchers([]);
      setSelectedVoucherId(undefined);
      setCustomerLoyalty(null);
      setRedeemEnabled(false);
      setRedeemDiscountAmount(0);
      return;
    }
    void fetchPosCustomerVouchers(customer.id, priced.totalAmount)
      .then((items) => {
        setVouchers(items);
        setSelectedVoucherId((prev) =>
          prev && items.some((v) => v.customerVoucherId === prev) ? prev : undefined,
        );
      })
      .catch(() => setVouchers([]));
    void fetchPosCustomerLoyalty(customer.id, priced.totalAmount).then(setCustomerLoyalty);
  }, [customer?.id, priced.totalAmount]);

  useEffect(() => {
    setRedeemEnabled(false);
    setRedeemDiscountAmount(0);
  }, [customer?.id, selectedVoucherId, priced.totalAmount]);

  const handleRedeemToggle = (checked: boolean) => {
    setRedeemEnabled(checked);
    if (checked && canRedeem) {
      setRedeemDiscountAmount(maxRedeemMoney);
    } else {
      setRedeemDiscountAmount(0);
    }
  };

  const handleRedeemAmountChange = (value: number | null) => {
    const next = Math.max(0, Math.min(Number(value ?? 0), maxRedeemMoney));
    setRedeemDiscountAmount(next);
  };

  useEffect(() => {
    setPayments(isFreeOrder ? [] : defaultPayments(payableTotal));
  }, [isFreeOrder, payableTotal]);

  const singleCash = !isFreeOrder && isSingleCashPayment(payments);
  const cashTendered = singleCash ? Number(payments[0]?.amount ?? 0) : 0;
  const changeDue =
    singleCash && cashTendered > payableTotal + 0.009 ? cashTendered - payableTotal : 0;
  const paidTotal = isFreeOrder ? 0 : computeAppliedPayment(payments, payableTotal);
  const creditAmount = computeCreditAmount(payments, payableTotal, isFreeOrder);
  const paymentOk =
    isFreeOrder ||
    paymentsAreValid(payments, payableTotal, {
      customerId: customer?.id,
      allowCredit: Boolean(customer?.allowCredit),
    });

  const paymentBlockReason = !paymentOk
    ? creditAmount > 0.009 && !customer?.allowCredit
      ? 'Khách chưa được phép ghi nợ'
      : 'Tổng thu + ghi nợ phải bằng số phải thu'
    : shiftReady === false
      ? 'Chưa mở ca'
      : null;

  const updatePayment = (index: number, patch: Partial<PosCheckoutPaymentLine>) => {
    setPayments((prev) => {
      const next = prev.map((row, i) => (i === index ? { ...row, ...patch } : row));
      if (next.length >= 2 && index > 0) return rebalanceFirstRow(next, payableTotal);
      return next;
    });
  };

  const setCashAmount = (amount: number) => {
    setPayments([{ paymentMethod: SALES_PAYMENT_CASH, amount: roundMoney(amount) }]);
  };

  const setFullCredit = () => {
    if (!customer?.allowCredit) {
      message.warning('Khách chưa được phép ghi nợ');
      return;
    }
    setPayments([{ paymentMethod: SALES_PAYMENT_CREDIT, amount: payableTotal }]);
  };

  const addPayment = () => {
    setPayments((prev) => {
      const allocated = singleCash ? payableTotal : roundMoney(sumNonCreditPayments(prev));
      const remaining = Math.max(0, payableTotal - allocated);
      if (prev.length === 1 && Math.abs(allocated - payableTotal) < 0.01) {
        return rebalanceFirstRow(
          [
            { ...prev[0], amount: 0 },
            { paymentMethod: SALES_PAYMENT_CASH, amount: 0 },
          ],
          payableTotal,
        );
      }
      return rebalanceFirstRow(
        [...prev, { paymentMethod: SALES_PAYMENT_CASH, amount: remaining }],
        payableTotal,
      );
    });
  };

  const removePayment = (index: number) => {
    setPayments((prev) => {
      const next = prev.filter((_, i) => i !== index);
      if (next.length === 0) return defaultPayments(payableTotal);
      if (next.length === 1) return [{ ...next[0], amount: payableTotal }];
      return rebalanceFirstRow(next, payableTotal);
    });
  };

  const submit = async () => {
    if (!warehouseId || cart.length === 0) return;
    if (!shiftReady) {
      message.warning('Mở ca trước khi thanh toán');
      navigate('/pos', { replace: true });
      return;
    }
    if (!paymentOk) {
      message.warning(
        creditAmount > 0.009
          ? 'Kiểm tra ghi nợ hoặc chọn khách được phép ghi nợ'
          : 'Tổng thu chưa khớp số phải thu',
      );
      return;
    }
    const batchError = validateCartBatchLabels(cart, batchMode);
    if (batchError) {
      message.warning(batchError);
      navigate('/pos');
      return;
    }
    if (cart.some((line) => shouldBlockRxAtPos(line.dispensingClass, rxSettings.enforcementMode))) {
      message.error(RX_POS_BLOCK_MESSAGE);
      navigate('/pos');
      return;
    }

    setSaving(true);
    try {
      const invalidLine = cart.find(
        (line) => !line.productUnitId || !line.productId || Number(line.quantity) <= 0,
      );
      if (invalidLine) {
        message.error(
          `${invalidLine.productName || invalidLine.productCode || 'Sản phẩm'}: dòng không hợp lệ — quay POS chọn lại.`,
        );
        setSaving(false);
        return;
      }

      const stockMap = await fetchPosStockBulk(
        warehouseId,
        cart.map((c) => c.productUnitId),
      );
      for (const line of cart) {
        const stock = stockMap[line.productUnitId];
        if (stock != null && line.quantity > stock) {
          message.error(`${line.productName}: vượt tồn (${stock})`);
          setSaving(false);
          return;
        }
      }

      const orderReminder =
        customer?.id && orderReminderEnabled && orderReminderDaysSupply >= 1
          ? {
              label: orderReminderLabel.trim() || defaultOrderReminderLabel(),
              daysSupply: orderReminderDaysSupply,
            }
          : undefined;

      const payloadBase = buildCreateSalePayload(
        warehouseId,
        customer?.id,
        cart,
        orderDiscount,
        false,
        undefined,
        loyaltyDiscount > 0 ? loyaltyDiscount : undefined,
        selectedVoucherId,
        loadedPrescriptionId ?? undefined,
        orderReminder,
      );
      await previewPosAllocation({ warehouseId, items: payloadBase.items });

      const apiPayments = isFreeOrder
        ? []
        : normalizePaymentsForApi(
            payments.length > 0 ? payments : defaultPayments(payableTotal),
            payableTotal,
          );

      const order = editingDraftId
        ? await completeDraftSale(editingDraftId, {
            payments: apiPayments,
            ...buildDraftCompletePayload(
              customer?.id,
              cart,
              orderDiscount,
              loyaltyDiscount > 0 ? loyaltyDiscount : undefined,
              selectedVoucherId,
              loadedPrescriptionId ?? undefined,
              orderReminder,
            ),
          })
        : await createSale({
            ...payloadBase,
            payments: apiPayments,
          });

      if (loadedReservationId) {
        try {
          // link-sale đã gắn SO + chuyển Collected — không gọi /collected thêm (sẽ lỗi Ready→Collected).
          await linkReservationSale(loadedReservationId, order.id);
        } catch {
          message.warning('Đã bán nhưng chưa cập nhật trạng thái giữ hàng');
        }
      }

      if (loadedCustomerDraftOrderId) {
        try {
          await linkCustomerDraftOrderSale(loadedCustomerDraftOrderId, order.id);
        } catch {
          message.warning('Đã bán nhưng chưa cập nhật trạng thái đơn nháp khách');
        }
      }

      clearCart();
      clearDraftEdit();
      clearPosDraftEdit();
      navigate('/receipt', { replace: true, state: { order, autoPrint: true } });
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hoàn tất được thanh toán'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="staff-shell">
      <StaffPageHeader
        title="Thanh toán"
        subtitle={`${itemQty} SP · ${formatMoney(payableTotal)}`}
        backTo="/pos"
      />

      <main className="staff-body checkout-body">
        {shiftReady === false ? (
          <Alert
            type="warning"
            showIcon
            message="Chưa mở ca"
            description="Quay lại POS và mở ca trước khi thanh toán."
            action={
              <Button size="small" onClick={() => navigate('/pos')}>
                Về POS
              </Button>
            }
            style={{ marginBottom: 12 }}
          />
        ) : null}

        {sourceLabel ? (
          <Alert
            type="info"
            showIcon
            className="checkout-source"
            message={sourceLabel}
            description="Hoàn tất sẽ gắn đơn bán với nguồn này."
            style={{ marginBottom: 12 }}
          />
        ) : null}

        <section className="checkout-hero">
          <Typography.Text type="secondary" className="checkout-hero__label">
            Phải thu
          </Typography.Text>
          <div className="checkout-hero__amount">{formatMoney(payableTotal)}</div>
          <Typography.Text type="secondary" className="checkout-hero__meta">
            {cart.length} dòng · {itemQty} sản phẩm
          </Typography.Text>
        </section>

        <section className="checkout-customer">
          <div className="checkout-customer__main">
            <Typography.Text strong>{customer?.fullName || 'Khách lẻ'}</Typography.Text>
            {customer ? (
              <div className="checkout-customer__meta">
                {hasUsablePhone(customer.phone) ? (
                  <a className="checkout-customer__phone" href={`tel:${digitsOnly(customer.phone)}`}>
                    <PhoneOutlined /> {customer.phone}
                  </a>
                ) : (
                  <span className="checkout-customer__phone-missing">Chưa có SĐT</span>
                )}
                {customer.allowCredit ? (
                  <Tag color="green">Được ghi nợ</Tag>
                ) : (
                  <Tag>Không ghi nợ</Tag>
                )}
              </div>
            ) : (
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                Chưa chọn khách — không đổi điểm / voucher / nhắc hết đơn.
              </Typography.Text>
            )}
          </div>
          <Button
            type="text"
            icon={<ShoppingCartOutlined />}
            aria-label="Sửa giỏ"
            onClick={() => navigate('/pos')}
          >
            Giỏ
          </Button>
        </section>

        {customer && !customer.allowCredit && canEditCustomerCredit ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            message="Khách chưa được phép ghi nợ"
            description="Vào Khách + OTP → chọn khách → Cài đặt ghi nợ nếu quản lý đồng ý."
          />
        ) : null}

        <section className="checkout-panel">
          <div className="checkout-panel__title">Sản phẩm</div>
          <ul className="checkout-lines">
            {cart.map((line) => (
              <li key={line.key} className="checkout-lines__item">
                <div className="checkout-lines__name">
                  <Typography.Text ellipsis>{line.productName}</Typography.Text>
                  <Typography.Text type="secondary" className="checkout-lines__sub">
                    {line.productCode} · {line.quantity} {line.unitName}
                    {line.batchLabel ? ` · Lô ${line.batchLabel}` : ''}
                  </Typography.Text>
                </div>
                <Typography.Text strong className="checkout-lines__amt">
                  {formatMoney(lineNet(line))}
                </Typography.Text>
              </li>
            ))}
          </ul>
        </section>

        <section className="checkout-panel">
          <div className="checkout-panel__title">Tóm tắt tiền</div>
          <SummaryRow label="Tạm tính" value={formatMoney(priced.subtotalGross)} />
          {priced.lineDiscountTotal > 0 ? (
            <SummaryRow label="CK dòng" value={`−${formatMoney(priced.lineDiscountTotal)}`} muted />
          ) : null}
          {priced.orderDiscountAmount > 0 ? (
            <SummaryRow label="CK đơn" value={`−${formatMoney(priced.orderDiscountAmount)}`} muted />
          ) : null}
          {voucherDiscount > 0 ? (
            <SummaryRow label="Voucher" value={`−${formatMoney(voucherDiscount)}`} muted />
          ) : null}
          {loyaltyDiscount > 0 ? (
            <SummaryRow label="Đổi điểm" value={`−${formatMoney(loyaltyDiscount)}`} muted />
          ) : null}
          <SummaryRow label="Phải thu" value={formatMoney(payableTotal)} strong />
        </section>

        <section className="checkout-panel">
          <div className="checkout-panel__title">Chiết khấu đơn</div>
          {canDiscount ? (
            <>
              <div className="checkout-discount-grid">
                <div>
                  <Typography.Text type="secondary" className="checkout-field-label">
                    Loại CK
                  </Typography.Text>
                  <Select
                    size="large"
                    style={{ width: '100%' }}
                    placeholder="Chọn"
                    allowClear
                    value={orderDiscount.discountType}
                    options={[
                      { value: SALES_DISCOUNT_TYPES.Percent, label: '% phần trăm' },
                      { value: SALES_DISCOUNT_TYPES.Fixed, label: 'Số tiền (VND)' },
                    ]}
                    onChange={(discountType) =>
                      setOrderDiscount({
                        discountType: discountType ?? undefined,
                        discountValue: discountType ? orderDiscount.discountValue ?? 0 : undefined,
                      })
                    }
                  />
                </div>
                <div>
                  <Typography.Text type="secondary" className="checkout-field-label">
                    Giá trị
                    {orderDiscount.discountType === SALES_DISCOUNT_TYPES.Percent && maxPercent != null
                      ? ` (≤ ${maxPercent}%)`
                      : ''}
                  </Typography.Text>
                  <InputNumber
                    {...moneyProps}
                    size="large"
                    placeholder="0"
                    disabled={!orderDiscount.discountType}
                    value={orderDiscount.discountValue}
                    max={
                      orderDiscount.discountType === SALES_DISCOUNT_TYPES.Percent && maxPercent != null
                        ? maxPercent
                        : undefined
                    }
                    onChange={(v) =>
                      setOrderDiscount({
                        ...orderDiscount,
                        discountValue: Number(v ?? 0),
                      })
                    }
                  />
                </div>
              </div>
              {cart.some((l) => (l.discountValue ?? 0) > 0) ? (
                <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8, fontSize: 12 }}>
                  Đã có CK dòng trên giỏ — chỉnh lại tại POS nếu cần.
                </Typography.Text>
              ) : null}
            </>
          ) : (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Bạn không có quyền chiết khấu trên đơn.
            </Typography.Text>
          )}
        </section>

        {customer && vouchers.length > 0 ? (
          <section className="checkout-panel">
            <div className="checkout-panel__title">Voucher khách</div>
            <Select
              size="large"
              allowClear
              placeholder="Chọn voucher"
              style={{ width: '100%' }}
              value={selectedVoucherId}
              options={vouchers.map((v) => ({
                value: v.customerVoucherId,
                label: `${v.voucherCode} · −${formatMoney(v.discountAmount)} (${v.voucherName})`,
              }))}
              onChange={(v) => setSelectedVoucherId(v)}
            />
          </section>
        ) : null}

        {!customer ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            message="Đổi điểm / voucher"
            description="Quay POS chọn khách (không bán khách lẻ) để dùng điểm và voucher."
          />
        ) : null}

        {showLoyaltyPanel && customerLoyalty ? (
          <section className="checkout-panel checkout-panel--loyalty">
            <div className="checkout-panel__title">Tích điểm</div>
            <Typography.Text style={{ display: 'block', marginBottom: 8 }}>
              {customerLoyalty.pointsBalance > 0
                ? `Điểm: ${customerLoyalty.pointsBalance.toLocaleString()} · ≈ ${formatMoney(loyaltyPointsValue(customerLoyalty))}`
                : `Chưa có điểm · tích ${formatMoney(customerLoyalty.pointsPerAmount)} / điểm`}
            </Typography.Text>
            {canRedeem ? (
              <Space direction="vertical" size={10} style={{ width: '100%' }}>
                <div className="checkout-switch-row">
                  <Typography.Text>Đổi điểm thành tiền</Typography.Text>
                  <Switch checked={redeemEnabled} onChange={handleRedeemToggle} disabled={saving} />
                </div>
                {redeemEnabled ? (
                  <>
                    <Typography.Text type="secondary" className="checkout-field-label">
                      Số tiền giảm (tối đa {formatMoney(maxRedeemMoney)})
                    </Typography.Text>
                    <InputNumber
                      {...moneyProps}
                      size="large"
                      min={0}
                      max={maxRedeemMoney}
                      value={redeemDiscountAmount}
                      disabled={saving}
                      onChange={handleRedeemAmountChange}
                    />
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      Còn phải thu: {formatMoney(payableTotal)}
                      {loyaltyDiscount > 0
                        ? ` · ≈ ${redeemPointsUsed.toLocaleString(undefined, { maximumFractionDigits: 2 })} điểm`
                        : ''}
                    </Typography.Text>
                  </>
                ) : null}
              </Space>
            ) : (
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {customerLoyalty.pointsBalance <= 0
                  ? 'Khách chưa có điểm để đổi'
                  : `Đơn hiện tại không đủ điều kiện đổi điểm (tối đa ${formatMoney(maxRedeemMoney)})`}
              </Typography.Text>
            )}
          </section>
        ) : customer ? (
          <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12, fontSize: 12 }}>
            Tích điểm chưa bật cho nhà thuốc này.
          </Typography.Text>
        ) : null}

        {customer ? (
          <section className="checkout-panel">
            <div className="checkout-switch-row">
              <div>
                <div className="checkout-panel__title" style={{ marginBottom: 0 }}>
                  Lịch nhắc hết đơn
                </div>
                <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                  Gợi ý mua lại khi hết liệu trình
                </Typography.Text>
              </div>
              <Switch
                checked={orderReminderEnabled}
                disabled={saving}
                checkedChildren="Bật"
                unCheckedChildren="Tắt"
                onChange={(checked) => {
                  setOrderReminderEnabled(checked);
                  if (checked && !orderReminderLabel.trim()) {
                    setOrderReminderLabel(defaultOrderReminderLabel());
                  }
                }}
              />
            </div>
            {orderReminderEnabled ? (
              <div className="checkout-reminder-fields">
                <Typography.Text type="secondary" className="checkout-field-label">
                  Nhãn nhắc
                </Typography.Text>
                <Input
                  size="large"
                  maxLength={120}
                  value={orderReminderLabel}
                  disabled={saving}
                  placeholder={defaultOrderReminderLabel()}
                  onChange={(e) => setOrderReminderLabel(e.target.value)}
                />
                <Typography.Text type="secondary" className="checkout-field-label">
                  Số ngày dùng
                </Typography.Text>
                <InputNumber
                  size="large"
                  style={{ width: '100%' }}
                  min={1}
                  max={730}
                  value={orderReminderDaysSupply}
                  disabled={saving}
                  onChange={(value) => setOrderReminderDaysSupply(Math.max(1, Number(value ?? 30)))}
                />
              </div>
            ) : null}
          </section>
        ) : null}

        <section className="checkout-panel">
          <div className="checkout-panel__title">Hình thức thu</div>

          {isFreeOrder ? (
            <Alert type="success" showIcon message="Đơn miễn phí sau chiết khấu / voucher" />
          ) : (
            <>
              {singleCash ? (
                <div className="checkout-cash-presets">
                  <Button size="middle" onClick={() => setCashAmount(payableTotal)}>
                    Đủ
                  </Button>
                  <Button size="middle" onClick={() => setCashAmount(roundUpCash(payableTotal, 1000))}>
                    Tròn 1k
                  </Button>
                  <Button size="middle" onClick={() => setCashAmount(roundUpCash(payableTotal, 5000))}>
                    Tròn 5k
                  </Button>
                  <Button size="middle" onClick={() => setCashAmount(roundUpCash(payableTotal, 10000))}>
                    Tròn 10k
                  </Button>
                  {customer?.allowCredit ? (
                    <Button size="middle" onClick={setFullCredit}>
                      Ghi nợ hết
                    </Button>
                  ) : null}
                </div>
              ) : null}

              {payments.length >= 2 ? (
                <Alert
                  type="info"
                  showIcon
                  message="Chia nhiều hình thức — dòng đầu tự cân theo các dòng còn lại"
                  style={{ marginBottom: 10 }}
                />
              ) : null}

              {payments.map((row, index) => {
                const autoSplit = index === 0 && payments.length > 1;
                return (
                  <div key={index} className="checkout-payment-row">
                    <Select
                      size="large"
                      style={{ flex: 1.1 }}
                      value={row.paymentMethod}
                      options={paymentMethodOptions}
                      onChange={(paymentMethod) => updatePayment(index, { paymentMethod })}
                    />
                    <InputNumber
                      {...moneyProps}
                      size="large"
                      style={{ flex: 1 }}
                      value={row.amount}
                      disabled={autoSplit}
                      onChange={(v) => updatePayment(index, { amount: Number(v ?? 0) })}
                    />
                    {payments.length > 1 ? (
                      <Button
                        type="text"
                        danger
                        className="checkout-payment-remove"
                        icon={<MinusCircleOutlined />}
                        aria-label="Xóa hình thức"
                        onClick={() => removePayment(index)}
                      />
                    ) : null}
                  </div>
                );
              })}

              <Button
                type="dashed"
                block
                size="large"
                icon={<PlusOutlined />}
                onClick={addPayment}
                style={{ marginBottom: 10 }}
              >
                Thêm hình thức thu
              </Button>

              <SummaryRow label="Đã thu" value={formatMoney(paidTotal)} />
              {creditAmount > 0.009 ? (
                <SummaryRow label="Ghi nợ" value={formatMoney(creditAmount)} />
              ) : null}
              {changeDue > 0 ? (
                <SummaryRow label="Tiền thối" value={formatMoney(changeDue)} strong />
              ) : null}

              {!paymentOk ? (
                <Alert type="warning" showIcon style={{ marginTop: 10 }} message={paymentBlockReason} />
              ) : null}
            </>
          )}
        </section>

        {cart.some((l) => (l.discountValue ?? 0) > 0) ? (
          <Collapse
            ghost
            style={{ marginBottom: 8 }}
            items={[
              {
                key: 'lines',
                label: 'Gợi ý: CK dòng chỉnh tại POS',
                children: (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Quay lại giỏ hàng → nhập CK dòng (% hoặc ₫) trên từng sản phẩm.
                  </Typography.Text>
                ),
              },
            ]}
          />
        ) : null}
      </main>

      <footer className="staff-footer checkout-footer">
        {paymentBlockReason ? (
          <Typography.Text type="danger" className="checkout-footer__hint">
            {paymentBlockReason}
          </Typography.Text>
        ) : changeDue > 0 ? (
          <Typography.Text type="secondary" className="checkout-footer__hint">
            Thối {formatMoney(changeDue)}
          </Typography.Text>
        ) : creditAmount > 0.009 ? (
          <Typography.Text type="secondary" className="checkout-footer__hint">
            Ghi nợ {formatMoney(creditAmount)}
          </Typography.Text>
        ) : (
          <Typography.Text type="secondary" className="checkout-footer__hint">
            {formatMoney(payableTotal)} · {itemQty} SP
          </Typography.Text>
        )}
        <Button
          type="primary"
          block
          size="large"
          className="checkout-footer__btn"
          loading={saving}
          disabled={shiftReady === false || cart.length === 0 || !paymentOk}
          onClick={() => void submit()}
        >
          Hoàn tất & in bill
        </Button>
      </footer>
    </div>
  );
}
