import { useEffect, useMemo, useRef, useState } from 'react';
import {
  App,
  Alert,
  Button,
  Collapse,
  InputNumber,
  Select,
  Space,
  Switch,
  Typography,
} from 'antd';
import { ArrowLeftOutlined, MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
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
import { priceCart, roundMoney } from '@/modules/sales/pos-pricing';
import { buildCreateSalePayload, buildDraftCompletePayload } from '@/modules/sales/pos-sale-payload';
import { validateCartBatchLabels } from '@/modules/sales/pos-batch';
import { usePosHotkeys } from '@/shared/hooks/usePosHotkeys';
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
import { useSalesDiscountPolicy } from '@/modules/sales/useSalesDiscountPolicy';
import { useCanSalesWrite } from '@/shared/auth/usePermission';
import {
  linkReservationSale,
  markReservationCollected,
} from '@/shared/api/reservations.api';
import { linkCustomerDraftOrderSale } from '@/shared/api/customer-draft-orders.api';

const moneyProps = {
  style: { width: '100%' },
  min: 0 as const,
  formatter: (v: string | number | undefined) => `${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, '.'),
  parser: (v: string | undefined) => Number(String(v ?? '').replace(/\./g, '')) as 0,
};

function SummaryRow({ label, value, strong }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className="checkout-summary-row">
      <Typography.Text type={strong ? undefined : 'secondary'}>{label}</Typography.Text>
      <Typography.Text strong={strong}>{value}</Typography.Text>
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
    loadedCustomerDraftOrderId,
    editingDraftId,
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
  const [shiftReady, setShiftReady] = useState<boolean | null>(null);
  const [vouchers, setVouchers] = useState<PosCustomerVoucher[]>([]);
  const [selectedVoucherId, setSelectedVoucherId] = useState<string>();
  const [customerLoyalty, setCustomerLoyalty] = useState<PosCustomerLoyalty | null>(null);
  const [redeemEnabled, setRedeemEnabled] = useState(false);
  const [redeemDiscountAmount, setRedeemDiscountAmount] = useState(0);
  const [payments, setPayments] = useState<PosCheckoutPaymentLine[]>([]);
  const [saving, setSaving] = useState(false);

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
    void fetchBatchModeSettings().then(setBatchMode);
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
        setSelectedVoucherId((prev) => (prev && items.some((v) => v.customerVoucherId === prev) ? prev : undefined));
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
  const changeDue = singleCash && cashTendered > payableTotal + 0.009 ? cashTendered - payableTotal : 0;
  const paidTotal = isFreeOrder ? 0 : computeAppliedPayment(payments, payableTotal);
  const creditAmount = computeCreditAmount(payments, payableTotal, isFreeOrder);
  const paymentOk =
    isFreeOrder ||
    paymentsAreValid(payments, payableTotal, {
      customerId: customer?.id,
      allowCredit: Boolean(customer?.allowCredit),
    });

  const updatePayment = (index: number, patch: Partial<PosCheckoutPaymentLine>) => {
    setPayments((prev) => {
      const next = prev.map((row, i) => (i === index ? { ...row, ...patch } : row));
      if (next.length >= 2 && index > 0) return rebalanceFirstRow(next, payableTotal);
      return next;
    });
  };

  const addPayment = () => {
    setPayments((prev) => {
      const allocated = singleCash
        ? payableTotal
        : roundMoney(sumNonCreditPayments(prev));
      const remaining = Math.max(0, payableTotal - allocated);
      if (prev.length === 1 && Math.abs(allocated - payableTotal) < 0.01) {
        return rebalanceFirstRow(
          [{ ...prev[0], amount: 0 }, { paymentMethod: SALES_PAYMENT_CASH, amount: 0 }],
          payableTotal,
        );
      }
      return rebalanceFirstRow([...prev, { paymentMethod: SALES_PAYMENT_CASH, amount: remaining }], payableTotal);
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

    setSaving(true);
    try {
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

      const payloadBase = buildCreateSalePayload(
        warehouseId,
        customer?.id,
        cart,
        orderDiscount,
        false,
        undefined,
        loyaltyDiscount > 0 ? loyaltyDiscount : undefined,
        selectedVoucherId,
      );
      await previewPosAllocation({ warehouseId, items: payloadBase.items });

      const apiPayments = isFreeOrder
        ? []
        : normalizePaymentsForApi(payments.length > 0 ? payments : defaultPayments(payableTotal), payableTotal);

      const order = editingDraftId
        ? await completeDraftSale(editingDraftId, {
            payments: apiPayments,
            ...buildDraftCompletePayload(
              customer?.id,
              cart,
              orderDiscount,
              loyaltyDiscount > 0 ? loyaltyDiscount : undefined,
              selectedVoucherId,
            ),
          })
        : await createSale({
            ...payloadBase,
            payments: apiPayments,
          });

      if (loadedReservationId) {
        try {
          await linkReservationSale(loadedReservationId, order.id);
          await markReservationCollected(loadedReservationId);
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

  usePosHotkeys(
    useMemo(
      () => ({
        onComplete: () => void submit(),
      }),
      [saving],
    ),
    !saving,
  );

  return (
    <div className="staff-shell">
      <header className="staff-header">
        <Space>
          <Button type="text" icon={<ArrowLeftOutlined />} onClick={() => navigate(-1)} />
          <Typography.Text strong>Thanh toán</Typography.Text>
        </Space>
      </header>

      <main className="staff-body checkout-body">
        {shiftReady === false ? (
          <Alert
            type="warning"
            showIcon
            message="Chưa mở ca"
            description="Quay lại POS và mở ca trước khi thanh toán."
            style={{ marginBottom: 16 }}
          />
        ) : null}

        <div className="checkout-total-card">
          <Typography.Title level={2} style={{ margin: 0, color: '#0f766e' }}>
            {formatMoney(payableTotal)}
          </Typography.Title>
          <Typography.Text type="secondary">{cart.length} sản phẩm</Typography.Text>
        </div>

        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12 }}>
          Khách: {customer ? `${customer.fullName} · ${customer.phone}` : 'Khách lẻ'}
          {customer?.allowCredit ? ' · được ghi nợ' : customer ? ' · không được nợ' : ''}
        </Typography.Text>

        {customer && !customer.allowCredit && canEditCustomerCredit ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            message="Khách chưa được phép ghi nợ"
            description="Vào Khách + OTP → chọn khách → Cài đặt ghi nợ nếu quản lý đồng ý."
          />
        ) : null}

        <div className="checkout-panel">
          <SummaryRow label="Tạm tính" value={formatMoney(priced.subtotalGross)} />
          {priced.lineDiscountTotal > 0 ? (
            <SummaryRow label="Chiết khấu dòng" value={`−${formatMoney(priced.lineDiscountTotal)}`} />
          ) : null}
          {priced.orderDiscountAmount > 0 ? (
            <SummaryRow label="Chiết khấu đơn" value={`−${formatMoney(priced.orderDiscountAmount)}`} />
          ) : null}
          {voucherDiscount > 0 ? (
            <SummaryRow label="Voucher" value={`−${formatMoney(voucherDiscount)}`} />
          ) : null}
          {loyaltyDiscount > 0 ? (
            <SummaryRow label="Đổi điểm" value={`−${formatMoney(loyaltyDiscount)}`} />
          ) : null}
          <SummaryRow label="Phải thu" value={formatMoney(payableTotal)} strong />
        </div>

        <Typography.Text strong style={{ display: 'block', margin: '16px 0 8px' }}>
          Chiết khấu đơn
        </Typography.Text>
        {canDiscount ? (
        <Space.Compact style={{ width: '100%', marginBottom: 16 }}>
          <Select
            style={{ width: '38%' }}
            placeholder="Loại CK"
            allowClear
            value={orderDiscount.discountType}
            options={[
              { value: SALES_DISCOUNT_TYPES.Percent, label: '%' },
              { value: SALES_DISCOUNT_TYPES.Fixed, label: 'VND' },
            ]}
            onChange={(discountType) =>
              setOrderDiscount({
                discountType: discountType ?? undefined,
                discountValue: discountType ? orderDiscount.discountValue ?? 0 : undefined,
              })
            }
          />
          <InputNumber
            {...moneyProps}
            style={{ width: '62%' }}
            placeholder="Giá trị"
            disabled={!orderDiscount.discountType}
            value={orderDiscount.discountValue}
            onChange={(v) =>
              setOrderDiscount({
                ...orderDiscount,
                discountValue: Number(v ?? 0),
              })
            }
          />
        </Space.Compact>
        ) : (
          <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 16, fontSize: 12 }}>
            Bạn không có quyền chiết khấu trên đơn.
          </Typography.Text>
        )}

        {cart.some((l) => (l.discountValue ?? 0) > 0) ? (
          <Collapse
            ghost
            style={{ marginBottom: 12 }}
            items={[
              {
                key: 'lines',
                label: 'Chiết khấu theo dòng (chỉnh tại POS)',
                children: (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Quay lại giỏ hàng → nhập CK dòng (% hoặc ₫) trên từng sản phẩm.
                  </Typography.Text>
                ),
              },
            ]}
          />
        ) : null}

        {customer && vouchers.length > 0 ? (
          <>
            <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
              Voucher khách
            </Typography.Text>
            <Select
              allowClear
              placeholder="Chọn voucher"
              style={{ width: '100%', marginBottom: 16 }}
              value={selectedVoucherId}
              options={vouchers.map((v) => ({
                value: v.customerVoucherId,
                label: `${v.voucherCode} · −${formatMoney(v.discountAmount)} (${v.voucherName})`,
              }))}
              onChange={(v) => setSelectedVoucherId(v)}
            />
          </>
        ) : null}

        {!customer ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            message="Đổi điểm loyalty"
            description="Quay lại POS và chọn khách (không bán khách lẻ) để trừ điểm thành tiền."
          />
        ) : null}

        {showLoyaltyPanel && customerLoyalty ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            message={
              customerLoyalty.pointsBalance > 0
                ? `Điểm: ${customerLoyalty.pointsBalance.toLocaleString()} · ≈ ${formatMoney(loyaltyPointsValue(customerLoyalty))}`
                : `Chưa có điểm · tích ${formatMoney(customerLoyalty.pointsPerAmount)} / điểm`
            }
            description={
              canRedeem ? (
                <Space direction="vertical" size={10} style={{ width: '100%', marginTop: 8 }}>
                  <Space align="center">
                    <Typography.Text>Đổi điểm thành tiền</Typography.Text>
                    <Switch checked={redeemEnabled} onChange={handleRedeemToggle} disabled={saving} />
                  </Space>
                  {redeemEnabled ? (
                    <>
                      <div>
                        <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginBottom: 4 }}>
                          Số tiền giảm (tối đa {formatMoney(maxRedeemMoney)})
                        </Typography.Text>
                        <InputNumber
                          {...moneyProps}
                          min={0}
                          max={maxRedeemMoney}
                          value={redeemDiscountAmount}
                          disabled={saving}
                          onChange={handleRedeemAmountChange}
                        />
                      </div>
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
              )
            }
          />
        ) : customer ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            message="Chưa bật tích điểm"
            description="Bật loyalty trong admin → Cài đặt bán hàng → Tích điểm."
          />
        ) : null}

        <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
          Hình thức thu
        </Typography.Text>

        {isFreeOrder ? (
          <Alert type="success" showIcon message="Đơn miễn phí sau chiết khấu / voucher" style={{ marginBottom: 12 }} />
        ) : (
          <>
            {payments.length >= 2 ? (
              <Alert
                type="info"
                showIcon
                message="Chia nhiều hình thức — dòng đầu tự cân theo các dòng còn lại"
                style={{ marginBottom: 12 }}
              />
            ) : null}

            {payments.map((row, index) => {
              const autoSplit = index === 0 && payments.length > 1;
              return (
                <div key={index} className="checkout-payment-row">
                  <Select
                    style={{ flex: 1 }}
                    value={row.paymentMethod}
                    options={paymentMethodOptions}
                    onChange={(paymentMethod) => updatePayment(index, { paymentMethod })}
                  />
                  <InputNumber
                    {...moneyProps}
                    style={{ flex: 1 }}
                    value={row.amount}
                    disabled={autoSplit}
                    onChange={(v) => updatePayment(index, { amount: Number(v ?? 0) })}
                  />
                  {payments.length > 1 ? (
                    <Button
                      type="text"
                      danger
                      icon={<MinusCircleOutlined />}
                      onClick={() => removePayment(index)}
                    />
                  ) : null}
                </div>
              );
            })}

            <Button type="dashed" block icon={<PlusOutlined />} onClick={addPayment} style={{ marginBottom: 12 }}>
              Thêm hình thức thu
            </Button>

            <div className="checkout-panel">
              <SummaryRow label="Đã thu" value={formatMoney(paidTotal)} />
              {creditAmount > 0.009 ? (
                <SummaryRow label="Ghi nợ" value={formatMoney(creditAmount)} />
              ) : null}
              {changeDue > 0 ? <SummaryRow label="Tiền thối" value={formatMoney(changeDue)} /> : null}
            </div>

            {!paymentOk ? (
              <Alert
                type="warning"
                showIcon
                style={{ marginTop: 12 }}
                message={
                  creditAmount > 0.009 && !customer?.allowCredit
                    ? 'Khách chưa được phép ghi nợ'
                    : 'Tổng thu + ghi nợ phải bằng số phải thu'
                }
              />
            ) : null}
          </>
        )}
      </main>

      <footer className="staff-footer">
        <Button
          type="primary"
          block
          size="large"
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
