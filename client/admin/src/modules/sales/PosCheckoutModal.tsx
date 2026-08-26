import { useEffect, useMemo, useState } from 'react';

import { Alert, App, Button, Form, Input, InputNumber, Modal, Select, Space, Switch, Tag, Tooltip, Typography } from 'antd';

import { MinusCircleOutlined, PlusOutlined, UserAddOutlined } from '@ant-design/icons';

import type {
  CustomerListItem,
  PosCheckoutConfirm,
  PosCheckoutPaymentLine,
  PosCustomerLoyalty,
  PosCustomerVoucher,
} from '@/shared/api/sales.types';

import { apiErrorMessage } from '@/shared/api/api-error';

import {
  formatDisplayMoney,
  moneyInputNumberPropsAllowZeroSuffix,
  moneyInputNumberStyle,
} from '@/shared/utils/money';

import { PosSummaryPanel, PosSummaryRow } from '@/modules/sales/pos-summary-ui';
import { defaultOrderReminderLabel } from '@/modules/sales/order-reminder-label';
import {
  buildCheckoutSymptomChips,
  type CartSymptomInferLine,
} from '@/modules/sales/checkout-symptom-chips';
import {
  fetchConsultationSymptomCatalog,
  type ConsultationSymptomCatalog,
  type ConsultationSymptomOption,
} from '@/shared/api/pharmacy-consultation.api';
import { useTranslation } from 'react-i18next';
import { useSalesEnums } from '@/shared/i18n/use-sales-enums';
import './pos-checkout-modal.css';



const PAYMENT_METHOD_CASH = 1;
const PAYMENT_METHOD_CREDIT = 5;



type Props = {

  open: boolean;

  loading?: boolean;

  totalAmount: number;

  subtotalGross: number;

  lineDiscountTotal: number;

  orderDiscountAmount: number;

  customerId?: string;

  customers?: CustomerListItem[];

  onCustomerChange?: (customerId: string | undefined) => void;

  /** Server-side customer search (debounced by parent). When set, Select does not filter locally. */
  onCustomerSearch?: (query: string) => void;

  customerSearchLoading?: boolean;

  onQuickAddCustomer?: () => void;

  customerAllowCredit?: boolean;

  customerCreditLimit?: number | null;

  customerCurrentOutstanding?: number;

  customerLoyalty?: PosCustomerLoyalty | null;

  customerVouchers?: PosCustomerVoucher[] | null;

  /** Cart product names for soft symptom chip suggestions. */
  cartLines?: CartSymptomInferLine[];

  /** When false (active consultation session), hide optional checkout chips. */
  symptomCaptureEnabled?: boolean;

  onCancel: () => void;

  onConfirm: (result: PosCheckoutConfirm) => void | Promise<void>;

};



const moneyFieldProps = {
  ...moneyInputNumberPropsAllowZeroSuffix,
  style: { ...moneyInputNumberStyle, width: 200 },
} as const;

function defaultPayments(total: number): PosCheckoutPaymentLine[] {

  return [{ paymentMethod: PAYMENT_METHOD_CASH, amount: total }];

}



function sumCashPayments(rows: PosCheckoutPaymentLine[]): number {

  return rows

    .filter((row) => Number(row.paymentMethod) !== PAYMENT_METHOD_CREDIT)

    .reduce((sum, row) => sum + Number(row?.amount ?? 0), 0);

}



function sumCreditPaymentRows(rows: PosCheckoutPaymentLine[]): number {

  return rows

    .filter((row) => Number(row.paymentMethod) === PAYMENT_METHOD_CREDIT)

    .reduce((sum, row) => sum + Number(row?.amount ?? 0), 0);

}



function isSingleCashPayment(rows: PosCheckoutPaymentLine[]): boolean {

  return rows.length === 1 && Number(rows[0]?.paymentMethod) === PAYMENT_METHOD_CASH;

}



function rebalanceFirstRow(rows: PosCheckoutPaymentLine[], totalAmount: number): PosCheckoutPaymentLine[] {

  if (rows.length < 2) return rows;

  const rest = rows.slice(1).reduce((sum, row) => sum + Number(row.amount ?? 0), 0);

  const firstAmount = Math.max(0, totalAmount - rest);

  return [{ ...rows[0], amount: firstAmount }, ...rows.slice(1)];

}



function computeAppliedPayment(rows: PosCheckoutPaymentLine[], payableTotal: number): number {

  if (rows.length === 0) return 0;

  if (isSingleCashPayment(rows)) {

    return Math.min(Number(rows[0]?.amount ?? 0), payableTotal);

  }

  return Math.min(sumCashPayments(rows), payableTotal);

}



function normalizePaymentsForApi(

  rows: PosCheckoutPaymentLine[],

  payableTotal: number,

): PosCheckoutPaymentLine[] {

  if (rows.length === 0) return [];

  if (isSingleCashPayment(rows)) {

    const applied = computeAppliedPayment(rows, payableTotal);

    if (applied <= 0.009) return [];

    return [{ paymentMethod: PAYMENT_METHOD_CASH, amount: applied }];

  }

  return rows

    .map((row) => ({

      paymentMethod: Number(row.paymentMethod),

      amount: Number(row.amount ?? 0),

    }))

    .filter((row) => row.amount > 0.009 && row.paymentMethod !== PAYMENT_METHOD_CREDIT);

}



function roundMoney(v: number): number {

  return Math.round(v * 100) / 100;

}



function paymentsAreValid(

  rows: PosCheckoutPaymentLine[],

  payableTotal: number,

  options: { customerId?: string; allowCredit: boolean },

): boolean {

  if (payableTotal < 0.01) return true;

  if (rows.length === 0) return false;

  const cashPaid = computeAppliedPayment(rows, payableTotal);

  const creditRows = roundMoney(sumCreditPaymentRows(rows));

  if (cashPaid > payableTotal + 0.009) return false;

  if (creditRows > 0.009) {

    if (Math.abs(cashPaid + creditRows - payableTotal) > 0.01) return false;

    return Boolean(options.customerId && options.allowCredit);

  }

  if (Math.abs(cashPaid - payableTotal) < 0.01) return true;

  if (cashPaid < payableTotal - 0.009) {

    return Boolean(options.customerId && options.allowCredit);

  }

  return false;

}



function formatPoints(value: number): string {

  return Number.isInteger(value) ? value.toLocaleString('vi-VN') : value.toLocaleString('vi-VN', { maximumFractionDigits: 4 });

}



export function PosCheckoutModal({

  open,

  loading,

  totalAmount,

  subtotalGross,

  lineDiscountTotal,

  orderDiscountAmount,

  customerId,

  customers = [],

  onCustomerChange,

  onCustomerSearch,

  customerSearchLoading = false,

  onQuickAddCustomer,

  customerAllowCredit = false,

  customerCreditLimit,

  customerCurrentOutstanding = 0,

  customerLoyalty,

  customerVouchers,

  cartLines = [],

  symptomCaptureEnabled = false,

  onCancel,

  onConfirm,

}: Props) {
  const { t } = useTranslation('sales');
  const { t: tc } = useTranslation('common');
  const { message } = App.useApp();
  const { paymentMethodOptions } = useSalesEnums();

  const amountFieldLabel = (autoSplit: boolean) =>
    autoSplit ? t('pos.checkout.amountTenderedAutoSplit') : t('pos.checkout.amountTendered');

  const showLoyaltyPanel = Boolean(customerLoyalty?.loyaltyEnabled);

  const [selectedCustomerVoucherId, setSelectedCustomerVoucherId] = useState<string>();

  const selectedVoucher = useMemo(
    () => customerVouchers?.find((v) => v.customerVoucherId === selectedCustomerVoucherId),
    [customerVouchers, selectedCustomerVoucherId],
  );

  const voucherDiscount = selectedVoucher ? roundMoney(selectedVoucher.discountAmount) : 0;

  const orderAfterVoucher = roundMoney(Math.max(0, totalAmount - voucherDiscount));

  const maxRedeemMoney = useMemo(() => {
    if (!customerLoyalty || orderAfterVoucher <= 0) return 0;
    const capByPercent = roundMoney((orderAfterVoucher * customerLoyalty.maxRedeemPercent) / 100);
    return Math.min(customerLoyalty.maxRedeemDiscountAmount, capByPercent, orderAfterVoucher);
  }, [customerLoyalty, orderAfterVoucher]);

  const canOfferRedeem =

    showLoyaltyPanel && (customerLoyalty?.pointsBalance ?? 0) > 0 && maxRedeemMoney > 0;



  const loyaltyPointsValue =

    customerLoyalty && customerLoyalty.pointsBalance > 0

      ? customerLoyalty.pointsBalance * customerLoyalty.amountPerPoint

      : 0;



  const [redeemEnabled, setRedeemEnabled] = useState(false);

  const [redeemDiscountAmount, setRedeemDiscountAmount] = useState(0);

  const [orderReminderEnabled, setOrderReminderEnabled] = useState(false);

  const [orderReminderLabel, setOrderReminderLabel] = useState(() => defaultOrderReminderLabel());

  const [orderReminderDaysSupply, setOrderReminderDaysSupply] = useState(30);



  const loyaltyDiscount =

    redeemEnabled && canOfferRedeem ? roundMoney(Math.max(0, redeemDiscountAmount)) : 0;

  const payableTotal = roundMoney(Math.max(0, orderAfterVoucher - loyaltyDiscount));

  const redeemPointsUsed =

    loyaltyDiscount > 0 && customerLoyalty && customerLoyalty.amountPerPoint > 0

      ? loyaltyDiscount / customerLoyalty.amountPerPoint

      : 0;



  const isFreeOrder = payableTotal < 0.01;

  const [payments, setPayments] = useState<PosCheckoutPaymentLine[]>(() => defaultPayments(payableTotal));

  const [submitting, setSubmitting] = useState(false);

  const [submitError, setSubmitError] = useState<string | null>(null);

  const [symptomCatalog, setSymptomCatalog] = useState<ConsultationSymptomCatalog | null>(null);

  const [selectedSymptomCodes, setSelectedSymptomCodes] = useState<string[]>([]);



  useEffect(() => {

    if (!open) return;

    setSubmitError(null);

    setSubmitting(false);

    setRedeemEnabled(false);

    setRedeemDiscountAmount(0);

    setOrderReminderEnabled(false);

    setOrderReminderLabel(defaultOrderReminderLabel());

    setOrderReminderDaysSupply(30);

    setSelectedCustomerVoucherId(undefined);

    setSelectedSymptomCodes([]);

  }, [open, totalAmount]);



  useEffect(() => {

    if (!open || !symptomCaptureEnabled) {
      setSymptomCatalog(null);
      return;
    }
    let cancelled = false;
    void fetchConsultationSymptomCatalog()
      .then((catalog) => {
        if (!cancelled) setSymptomCatalog(catalog);
      })
      .catch(() => {
        if (!cancelled) setSymptomCatalog(null);
      });
    return () => {
      cancelled = true;
    };
  }, [open, symptomCaptureEnabled]);



  const symptomChips = useMemo(
    () => (symptomCaptureEnabled ? buildCheckoutSymptomChips(symptomCatalog, cartLines, 8) : []),
    [symptomCaptureEnabled, symptomCatalog, cartLines],
  );

  const symptomConfirm = useMemo(() => {
    if (!symptomCaptureEnabled || selectedSymptomCodes.length === 0) return {};
    return { symptomCodes: selectedSymptomCodes };
  }, [symptomCaptureEnabled, selectedSymptomCodes]);

  const toggleSymptom = (opt: ConsultationSymptomOption) => {
    setSelectedSymptomCodes((prev) =>
      prev.includes(opt.code) ? prev.filter((c) => c !== opt.code) : [...prev, opt.code],
    );
  };



  const orderReminderConfirm = useMemo(() => {
    if (!customerId || !orderReminderEnabled || orderReminderDaysSupply < 1) return {};
    return {
      orderReminderLabel: orderReminderLabel.trim() || defaultOrderReminderLabel(),
      orderReminderDaysSupply: orderReminderDaysSupply,
    };
  }, [customerId, orderReminderEnabled, orderReminderDaysSupply, orderReminderLabel]);



  useEffect(() => {

    if (!open) return;

    setPayments(isFreeOrder ? [] : defaultPayments(payableTotal));

  }, [isFreeOrder, open, payableTotal]);



  const singleCash = !isFreeOrder && isSingleCashPayment(payments);

  const cashTendered = singleCash ? Number(payments[0]?.amount ?? 0) : 0;

  const changeDue = singleCash && cashTendered > payableTotal + 0.009 ? cashTendered - payableTotal : 0;



  const paidTotal = useMemo(() => {

    if (isFreeOrder) return 0;

    return computeAppliedPayment(payments, payableTotal);

  }, [isFreeOrder, payableTotal, payments]);



  const explicitCredit = useMemo(

    () => roundMoney(sumCreditPaymentRows(payments)),

    [payments],

  );



  const creditAmount = useMemo(() => {

    if (isFreeOrder) return 0;

    const implicitCredit = roundMoney(Math.max(0, payableTotal - paidTotal));

    return explicitCredit > 0.009 ? explicitCredit : implicitCredit;

  }, [explicitCredit, isFreeOrder, paidTotal, payableTotal]);



  const selectedCustomer = useMemo(

    () => customers.find((c) => c.id === customerId),

    [customers, customerId],

  );



  const paymentOk = useMemo(() => {

    if (isFreeOrder) return true;

    if (creditAmount > 0.009) {

      if (!customerId) return false;

      if (!customerAllowCredit) return false;

      if (customerCreditLimit != null && customerCreditLimit > 0) {

        if (customerCurrentOutstanding + creditAmount > customerCreditLimit + 0.009) return false;

      }

    }

    return paymentsAreValid(payments, payableTotal, {

      customerId,

      allowCredit: customerAllowCredit,

    });

  }, [

    creditAmount,

    customerAllowCredit,

    customerCreditLimit,

    customerCurrentOutstanding,

    customerId,

    isFreeOrder,

    payableTotal,

    payments,

  ]);



  const busy = submitting || loading;



  const handleRedeemToggle = (checked: boolean) => {

    setSubmitError(null);

    setRedeemEnabled(checked);

    if (checked && canOfferRedeem) {

      setRedeemDiscountAmount(maxRedeemMoney);

    } else {

      setRedeemDiscountAmount(0);

    }

  };



  const handleRedeemAmountChange = (value: number | null) => {

    setSubmitError(null);

    const next = Math.max(0, Math.min(Number(value ?? 0), maxRedeemMoney));

    setRedeemDiscountAmount(next);

  };



  const updatePayment = (index: number, patch: Partial<PosCheckoutPaymentLine>) => {

    setSubmitError(null);

    setPayments((prev) => {

      const next = prev.map((row, i) => (i === index ? { ...row, ...patch } : row));

      if (next.length >= 2 && index > 0) {

        return rebalanceFirstRow(next, payableTotal);

      }

      return next;

    });

  };



  const handleAddPayment = () => {

    setSubmitError(null);

    setPayments((prev) => {

      const allocated = singleCash

        ? payableTotal

        : roundMoney(sumCashPayments(prev) + sumCreditPaymentRows(prev));

      const remaining = Math.max(0, payableTotal - allocated);

      if (prev.length === 1 && Math.abs(allocated - payableTotal) < 0.01) {

        return rebalanceFirstRow(

          [{ ...prev[0], amount: 0 }, { paymentMethod: 3, amount: 0 }],

          payableTotal,

        );

      }

      return rebalanceFirstRow([...prev, { paymentMethod: 3, amount: remaining }], payableTotal);

    });

  };



  const handleRemovePayment = (index: number) => {

    setSubmitError(null);

    setPayments((prev) => {

      const next = prev.filter((_, i) => i !== index);

      if (next.length === 0) return defaultPayments(payableTotal);

      if (next.length === 1) return [{ ...next[0], amount: payableTotal }];

      return rebalanceFirstRow(next, payableTotal);

    });

  };



  const handleSubmit = async () => {

    if (busy) return;

    setSubmitError(null);

    setSubmitting(true);



    try {

      if (isFreeOrder) {

        await Promise.resolve(

          onConfirm({

            payments: [],

            ...(selectedCustomerVoucherId ? { customerVoucherId: selectedCustomerVoucherId } : {}),

            ...(loyaltyDiscount > 0 ? { loyaltyDiscountAmount: loyaltyDiscount } : {}),

            ...orderReminderConfirm,

            ...symptomConfirm,

          }),

        );

        return;

      }



      const rows = payments.length > 0 ? payments : defaultPayments(payableTotal);

      if (!paymentsAreValid(rows, payableTotal, { customerId, allowCredit: customerAllowCredit })) {

        const err = creditAmount > 0.009
          ? t('pos.checkout.errors.checkCredit')
          : t('pos.checkout.errors.paymentMismatch');

        setSubmitError(err);

        message.warning(err);

        return;

      }



      await Promise.resolve(

        onConfirm({

          payments: normalizePaymentsForApi(rows, payableTotal),

          ...(selectedCustomerVoucherId ? { customerVoucherId: selectedCustomerVoucherId } : {}),

          ...(loyaltyDiscount > 0 ? { loyaltyDiscountAmount: loyaltyDiscount } : {}),

          ...orderReminderConfirm,

          ...symptomConfirm,

        }),

      );

    } catch (error) {

      const errMsg = apiErrorMessage(error, t('pos.checkout.errors.createSaleFailed'));

      setSubmitError(errMsg);

      message.error(errMsg);

    } finally {

      setSubmitting(false);

    }

  };



  const blockReason = useMemo(() => {

    if (isFreeOrder || paymentOk) return null;

    if (creditAmount > 0.009) {

      if (!customerId) return t('pos.checkout.errors.selectCustomerCredit');

      if (!customerAllowCredit) return t('pos.checkout.errors.customerCreditNotAllowed');

      if (

        customerCreditLimit != null &&

        customerCreditLimit > 0 &&

        customerCurrentOutstanding + creditAmount > customerCreditLimit + 0.009

      ) {

        return t('pos.checkout.errors.creditLimitExceeded', {
          limit: formatDisplayMoney(customerCreditLimit),
          outstanding: formatDisplayMoney(customerCurrentOutstanding),
        });

      }

    }

    if (singleCash && cashTendered > payableTotal + 0.009) {

      return null;

    }

    if (singleCash && cashTendered < payableTotal - 0.009 && !customerAllowCredit) {

      return t('pos.checkout.errors.payOrCredit', { payable: formatDisplayMoney(payableTotal) });

    }

    if (!singleCash && paidTotal > payableTotal + 0.009) {

      return t('pos.checkout.errors.overpaid', { payable: formatDisplayMoney(payableTotal) });

    }

    return t('pos.checkout.errors.allocationMismatch', {
      payable: formatDisplayMoney(payableTotal),
      paid: formatDisplayMoney(paidTotal),
    });

  }, [

    cashTendered,

    creditAmount,

    customerAllowCredit,

    customerCreditLimit,

    customerCurrentOutstanding,

    customerId,

    isFreeOrder,

    paidTotal,

    payableTotal,

    paymentOk,

    singleCash,

    t,

  ]);



  const totalDiscountAmount = lineDiscountTotal + orderDiscountAmount;



  return (

    <Modal

      className="pos-checkout-modal"

      title={t('pos.checkout.title')}

      open={open}

      onCancel={onCancel}

      footer={[

        <Button key="cancel" onClick={onCancel} disabled={busy}>

          {tc('actions.cancel')}

        </Button>,

        <Button key="ok" type="primary" loading={busy} onClick={() => void handleSubmit()}>

          {t('pos.checkout.completeSale')}

        </Button>,

      ]}

      width={780}

      destroyOnClose

      maskClosable={false}

      centered={false}

      style={{ top: 48, paddingBottom: 0 }}

      styles={{ body: { paddingTop: 12, paddingBottom: 12 } }}

    >

      <div className="pos-checkout-top-row">

        <div className="pos-checkout-summary">

          <PosSummaryPanel>

            <PosSummaryRow label={t('pos.checkout.subtotal')} value={formatDisplayMoney(subtotalGross)} />

            {lineDiscountTotal > 0 && (

              <PosSummaryRow

                label={t('pos.checkout.lineDiscount')}

                value={`−${formatDisplayMoney(lineDiscountTotal)}`}

                danger

              />

            )}

            {orderDiscountAmount > 0 && (

              <PosSummaryRow

                label={t('pos.checkout.orderDiscount')}

                value={`−${formatDisplayMoney(orderDiscountAmount)}`}

                danger

              />

            )}

            {totalDiscountAmount > 0 && (

              <PosSummaryRow

                label={t('pos.checkout.totalDiscount')}

                value={`−${formatDisplayMoney(totalDiscountAmount)}`}

                danger

              />

            )}

            {voucherDiscount > 0 && (

              <PosSummaryRow

                label={t('pos.checkout.voucher')}

                value={`−${formatDisplayMoney(voucherDiscount)}`}

                danger

              />

            )}

            {loyaltyDiscount > 0 && (

              <PosSummaryRow

                label={t('pos.checkout.loyaltyDiscount')}

                value={`−${formatDisplayMoney(loyaltyDiscount)}`}

                danger

              />

            )}

            <PosSummaryRow label={t('pos.checkout.payable')} value={formatDisplayMoney(payableTotal)} strong />

          </PosSummaryPanel>

        </div>

        {showLoyaltyPanel && customerLoyalty ? (

          <div className="pos-checkout-loyalty">

            <div className="pos-checkout-loyalty-row">

              <Typography.Text style={{ fontSize: 13, lineHeight: 1.35 }}>

                {customerLoyalty.pointsBalance > 0

                  ? t('pos.checkout.loyaltyHasPoints', {
                      points: formatPoints(customerLoyalty.pointsBalance),
                      value: formatDisplayMoney(loyaltyPointsValue),
                      amountPerPoint: formatDisplayMoney(customerLoyalty.amountPerPoint),
                    })
                  : t('pos.checkout.loyaltyNoPoints', {
                      amountPerPoint: formatDisplayMoney(customerLoyalty.amountPerPoint),
                    })}

              </Typography.Text>

              {canOfferRedeem ? (

                <Space size={6} align="center">

                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>

                    {t('pos.checkout.redeemNow')}

                  </Typography.Text>

                  <Switch

                    size="small"

                    checked={redeemEnabled}

                    disabled={busy}

                    checkedChildren={tc('actions.yes')}
                    unCheckedChildren={tc('actions.no')}

                    onChange={handleRedeemToggle}

                  />

                </Space>

              ) : null}

            </div>

            <div className="pos-checkout-loyalty-meta">

              {customerLoyalty.maxRedeemPercent < 100

                ? t('pos.checkout.loyaltyMaxPercent', {
                    percent: customerLoyalty.maxRedeemPercent,
                    amount: formatDisplayMoney(maxRedeemMoney),
                  })
                : maxRedeemMoney > 0
                  ? t('pos.checkout.loyaltyMaxAmount', { amount: formatDisplayMoney(maxRedeemMoney) })
                  : canOfferRedeem
                    ? t('pos.checkout.loyaltyCannotRedeem')
                    : customerLoyalty.pointsBalance <= 0
                      ? t('pos.checkout.earnRule', { amount: formatDisplayMoney(customerLoyalty.pointsPerAmount) })
                      : null}

              {redeemEnabled && loyaltyDiscount > 0

                ? ` · ${t('pos.checkout.redeemApprox', { points: formatPoints(redeemPointsUsed) })}`

                : null}

            </div>

            {redeemEnabled ? (

              <Space wrap align="start" size={8} style={{ marginTop: 6 }}>

                <Form.Item label={t('pos.checkout.loyaltyDiscount')} style={{ marginBottom: 0 }}>

                  <InputNumber

                    size="small"

                    {...moneyInputNumberPropsAllowZeroSuffix}

                    min={0}

                    max={maxRedeemMoney}

                    value={redeemDiscountAmount}

                    disabled={busy}

                    onChange={handleRedeemAmountChange}

                    style={{ ...moneyInputNumberStyle, width: 140 }}

                  />

                </Form.Item>

              </Space>

            ) : null}

          </div>

        ) : null}

      </div>



      <div className="pos-checkout-section">

        <Typography.Text strong style={{ fontSize: 13 }}>{t('pos.checkout.customer')}</Typography.Text>

        <Space.Compact block style={{ width: '100%', marginTop: 4 }}>

          <Select

            allowClear

            showSearch

            filterOption={false}

            loading={customerSearchLoading}

            onSearch={onCustomerSearch}

            placeholder={t('pos.checkout.customerPlaceholderCredit')}

            style={{ width: onQuickAddCustomer ? 'calc(100% - 32px)' : '100%' }}

            value={customerId}

            disabled={busy || !onCustomerChange}

            options={customers.map((c) => ({

              value: c.id,

              label: `${c.customerCode} — ${c.fullName}${c.phone ? ` · ${c.phone}` : ''}${c.customerGroupName ? ` · ${c.customerGroupName}` : ''}`,

            }))}

            onChange={(value) => onCustomerChange?.(value)}

          />

          {onQuickAddCustomer ? (

            <Tooltip title={t('pos.checkout.quickAddCustomer')}>

              <Button icon={<UserAddOutlined />} disabled={busy} onClick={onQuickAddCustomer} />

            </Tooltip>

          ) : null}

        </Space.Compact>

        {(selectedCustomer || creditAmount > 0.009 || customerId) ? (

          <div className="pos-checkout-customer-meta">

            <div className="pos-checkout-customer-meta__left">

              {selectedCustomer ? (

                <Space size={[6, 4]} wrap className="pos-checkout-customer-tags">

                  {selectedCustomer.allowCredit ? (

                    <Tag color="gold">{t('pos.checkout.creditAllowed')}</Tag>

                  ) : (

                    <Tag>{t('pos.checkout.creditNotAllowed')}</Tag>

                  )}

                  {(selectedCustomer.currentOutstanding ?? 0) > 0.009 ? (

                    <Tag color="orange">

                      {t('pos.checkout.outstanding', { amount: formatDisplayMoney(selectedCustomer.currentOutstanding) })}

                    </Tag>

                  ) : null}

                </Space>

              ) : creditAmount > 0.009 ? (

                <Typography.Text type="warning" style={{ fontSize: 12 }}>

                  {t('pos.checkout.selectCustomerForCredit')}

                </Typography.Text>

              ) : null}

            </div>

            {customerId ? (

              <Space align="center" size={8} className="pos-checkout-order-reminder-inline">

                <Typography.Text strong style={{ fontSize: 13 }}>{t('pos.checkout.orderReminderTitle')}</Typography.Text>

                <Switch

                  size="small"

                  checked={orderReminderEnabled}

                  disabled={busy}

                  checkedChildren={tc('actions.yes')}

                  unCheckedChildren={tc('actions.no')}

                  onChange={(checked) => {

                    setOrderReminderEnabled(checked);

                    if (checked && !orderReminderLabel.trim()) {

                      setOrderReminderLabel(defaultOrderReminderLabel());

                    }

                  }}

                />

              </Space>

            ) : null}

          </div>

        ) : null}

        {customerId && orderReminderEnabled ? (

          <Space wrap align="start" size={8} style={{ width: '100%', marginTop: 6 }}>

            <Form.Item label={t('pos.checkout.orderReminderLabel')} style={{ marginBottom: 0 }}>

              <Input

                size="small"

                maxLength={120}

                value={orderReminderLabel}

                disabled={busy}

                placeholder={defaultOrderReminderLabel()}

                style={{ width: 260 }}

                onChange={(e) => setOrderReminderLabel(e.target.value)}

              />

            </Form.Item>

            <Form.Item label={t('pos.checkout.orderReminderDays')} style={{ marginBottom: 0 }}>

              <InputNumber

                size="small"

                min={1}

                max={730}

                value={orderReminderDaysSupply}

                disabled={busy}

                style={{ width: 88 }}

                onChange={(value) => setOrderReminderDaysSupply(Math.max(1, Number(value ?? 30)))}

              />

            </Form.Item>

          </Space>

        ) : null}

      </div>



      {(customerVouchers?.length ?? 0) > 0 ? (

        <div className="pos-checkout-section">

          <Typography.Text strong style={{ fontSize: 13 }}>{t('pos.checkout.customerVouchers')}</Typography.Text>

          <Select

            allowClear

            placeholder={t('pos.checkout.voucherPlaceholder')}

            style={{ width: '100%', marginTop: 4 }}

            value={selectedCustomerVoucherId}

            options={customerVouchers!.map((v) => ({

              value: v.customerVoucherId,

              label: `${v.voucherCode} · −${formatDisplayMoney(v.discountAmount)} (${v.voucherName})`,

            }))}

            onChange={(value) => {

              setSubmitError(null);

              setSelectedCustomerVoucherId(value);

              setRedeemEnabled(false);

              setRedeemDiscountAmount(0);

            }}

          />

        </div>

      ) : null}



      {submitError && (

        <Alert type="error" showIcon message={submitError} style={{ marginBottom: 10 }} closable onClose={() => setSubmitError(null)} />

      )}



      {symptomChips.length > 0 ? (

        <div className="pos-checkout-section pos-checkout-symptoms">

          <div className="pos-checkout-section-head">

            <Typography.Text strong style={{ fontSize: 13 }}>

              {t('pos.checkout.symptomsOptional')}

            </Typography.Text>

            <Typography.Text type="secondary" style={{ fontSize: 11 }}>

              {t('pos.checkout.symptomsHint')}

            </Typography.Text>

            {selectedSymptomCodes.length > 0 ? (

              <Typography.Text type="secondary" style={{ fontSize: 11 }}>

                {t('pos.checkout.symptomsSelected', { count: selectedSymptomCodes.length })}

              </Typography.Text>

            ) : null}

          </div>

          <div className="pos-checkout-symptom-chips">

            {symptomChips.map((opt) => {

              const selected = selectedSymptomCodes.includes(opt.code);

              return (

                <Tag

                  key={opt.code}

                  color={selected ? 'blue' : undefined}

                  style={{ cursor: 'pointer', userSelect: 'none' }}

                  onClick={() => toggleSymptom(opt)}

                >

                  {opt.label}

                </Tag>

              );

            })}

          </div>

        </div>

      ) : null}



      {isFreeOrder ? (

        <Alert

          type="success"

          showIcon

          message={t('pos.checkout.freeOrder')}

        />

      ) : (

        <>

          {payments.length >= 2 && (

            <Alert

              type="info"

              showIcon

              style={{ marginBottom: 12 }}

              message={t('pos.checkout.splitPayments')}

            />

          )}



          <Form layout="vertical" requiredMark={false} className="pos-checkout-payment-form" size="small">

            {payments.map((row, index) => {

              const autoSplit = index === 0 && payments.length > 1;

              return (

                <Space key={index} align="start" wrap style={{ marginBottom: 6, width: '100%' }}>

                  <Form.Item label={t('pos.checkout.paymentMethod')} required style={{ marginBottom: 0 }}>

                    <Select

                      size="small"

                      style={{ width: 150 }}

                      value={row.paymentMethod}

                      options={paymentMethodOptions}

                      disabled={busy}

                      onChange={(value) => updatePayment(index, { paymentMethod: value })}

                    />

                  </Form.Item>

                  <Form.Item label={amountFieldLabel(autoSplit)} style={{ marginBottom: 0 }}>

                    <InputNumber

                      size="small"

                      {...moneyFieldProps}

                      value={row.amount}

                      disabled={busy || autoSplit}

                      onChange={(value) => updatePayment(index, { amount: Number(value ?? 0) })}

                    />

                  </Form.Item>

                  {payments.length > 1 && (

                    <Button

                      type="text"

                      danger

                      icon={<MinusCircleOutlined />}

                      disabled={busy}

                      onClick={() => handleRemovePayment(index)}

                      style={{ marginTop: 30 }}

                    />

                  )}

                </Space>

              );

            })}

          </Form>



          <Button type="dashed" block icon={<PlusOutlined />} onClick={handleAddPayment} disabled={busy}>

            {t('pos.checkout.addPaymentMethod')}

          </Button>



          <div style={{ marginTop: 8 }}>

            <Typography.Text style={{ fontSize: 13 }}>

              {t('pos.checkout.collected')}:{' '}

              <Typography.Text type={paymentOk ? 'success' : 'danger'} strong>

                {formatDisplayMoney(paidTotal)}

              </Typography.Text>

              {creditAmount > 0.009 ? (

                <>

                  {' '}

                  · {t('pos.checkout.creditLine')}:{' '}

                  <Typography.Text strong style={{ color: '#d48806' }}>

                    {formatDisplayMoney(creditAmount)}

                  </Typography.Text>

                </>

              ) : null}

              <Typography.Text type="secondary"> / {formatDisplayMoney(payableTotal)}</Typography.Text>

            </Typography.Text>

            {changeDue > 0 && (

              <Typography.Text style={{ display: 'block', marginTop: 4, fontSize: 13 }}>

                {t('pos.checkout.changeDue')}:{' '}

                <Typography.Text strong style={{ color: '#1677ff' }}>

                  {formatDisplayMoney(changeDue)}

                </Typography.Text>

              </Typography.Text>

            )}

            {blockReason && (

              <Typography.Paragraph type="danger" style={{ marginTop: 8, marginBottom: 0 }}>

                {blockReason}

              </Typography.Paragraph>

            )}

          </div>

        </>

      )}

    </Modal>

  );

}


